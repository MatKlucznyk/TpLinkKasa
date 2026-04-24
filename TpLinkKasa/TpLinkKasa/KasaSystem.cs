using Avg.Communications.Net;
using Avg.ModuleFramework.Logging;
using Crestron.SimplSharp;                          				// For Basic SIMPL# Classes
using Crestron.SimplSharp.Net.Https;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TpLinkKasa
{
    /// <summary>
    /// Provides static methods and properties for managing authentication and device discovery with the TP-Link Kasa
    /// cloud system.
    /// </summary>
    /// <remarks>This class is intended for use in applications that interact with TP-Link Kasa devices via
    /// the cloud API. It manages user credentials, handles authentication, and retrieves device information. All
    /// members are static and thread-safe where required. Before calling device-related methods, set the Username and
    /// Password properties with valid TP-Link Kasa cloud credentials.</remarks>
    public static class KasaSystem
    {
        internal const string BaseUrl = "https://wap.tplinkcloud.com";

        private static readonly object _syncLock = new object();
        internal static readonly HttpsClientPool Client = new HttpsClientPool(50);
        internal static readonly Logger KasaLogger = new Logger("TpLinkKasa");

        internal static string Token { get { lock (_syncLock) { return _token; } } }
        private  static List<KasaDeviceInfo> _devices = new List<KasaDeviceInfo>();
        private static Dictionary<string, KasaDeviceSubscriptionEvent> _subscribedDevices = new Dictionary<string, KasaDeviceSubscriptionEvent>();
        private static int _tokenGetCnt;
        private static string _token;
        
        /// <summary>
        /// Sets the username associated with the current context.
        /// </summary>
        public static string Username { private get; set; }

        /// <summary>
        /// Sets the password used for authentication or encryption purposes.
        /// </summary>
        /// <remarks>The password value can only be set; it cannot be retrieved through this property.
        /// This design helps prevent accidental exposure of sensitive information in application code.</remarks>
        public static string Password { private get; set; }

        internal static bool TryRegisterDevice(string alias, out KasaDeviceSubscriptionEvent subscriptionEvent)
        {
            subscriptionEvent = null;
            try
            {
                lock (_syncLock)
                {
                    if (_subscribedDevices.ContainsKey(alias)) return false;
                    _subscribedDevices.Add(alias, new KasaDeviceSubscriptionEvent());
                    subscriptionEvent = _subscribedDevices[alias];

                    return true;
                }
            }
            catch (Exception e)
            {
                KasaLogger.LogException(e);
                return false;
            }
        }

        private static void GetToken()
        {
            try
            {
                if (Username.Length > 0 && Password.Length > 0)
                {
                    var tCnt = Interlocked.Increment(ref _tokenGetCnt);
                    if (tCnt >= int.MaxValue)
                    {
                        Interlocked.Exchange(ref _tokenGetCnt, 0);
                    }

                    var headers = new Dictionary<string, string>()
                    {
                        { "Content-Type", "application/json" }
                    };

                    var content = "{\"method\":\"login\",\"params\":{\"appType\":\"Crestron" + tCnt + "\",\"cloudUserName\":\"" + Username + "\",\"cloudPassword\":\"" + Password + "\",\"terminalUUID\":\"3df98660-6155-4a7d-bc70-8622d41c767e\"}}";

                    var response = Client.SendRequest(BaseUrl, Crestron.SimplSharp.Net.AuthMethod.NONE, RequestType.Post, headers, string.Empty, string.Empty, content, KasaLogger);


                    if (response == null) return;
                    if (response.Status != 200) return;
                    if (response.Content.Length <= 0) return;
                    var body = JObject.Parse(response.Content);

                    if (body["error_code"] != null)
                    {
                        if (body["msg"] != null)
                        {
                            if (body["msg"].ToObject<string>() == "API rate limit exceeded")
                            {
                                GetToken();
                                return;
                            }
                        }
                    }
                    if (body["result"] == null) return;
                    if (body["result"]["token"] != null)
                    {
                        lock(_syncLock) _token = body["result"]["token"].ToString().Replace("\"", string.Empty);
                    }
                }
                else
                {
                    throw new ArgumentException("Username and Password cannot be empty");
                }

            }
            catch (SocketException se)
            {
                KasaLogger.LogException(se);
            }
            catch (HttpsException he)
            {
                KasaLogger.LogException(he);
            }
            catch (Exception e)
            {
                KasaLogger.LogException(e);
            }
        }

        /// <summary>
        /// Retrieves the current list of devices from the system and updates the internal device collection.
        /// </summary>
        /// <remarks>This method attempts to authenticate and query the system for the latest device list.
        /// If authentication fails or the request is unsuccessful, the internal device collection remains unchanged.
        /// The method logs exceptions and returns 0 in case of errors.</remarks>
        /// <returns>A value of 1 if one or more devices are found; otherwise, 0.</returns>
        public static ushort GetSystem()
        {
            try
            {
                if (Username.Length <= 0 || Password.Length <= 0)
                    throw new ArgumentException("Username and Password cannot be empty");
                GetToken();

                var token = Token;

                if (token == null) return (ushort) (_devices.Count > 0 ? 1 : 0);
                if (token.Length <= 0) return (ushort) (_devices.Count > 0 ? 1 : 0);

                var headers = new Dictionary<string, string>()
                {
                    { "Content-Type", "application/json" }
                };

                var response = Client.SendRequest($"{BaseUrl}?token={token}", Crestron.SimplSharp.Net.AuthMethod.NONE, RequestType.Post, headers, string.Empty, string.Empty, "{\"method\":\"getDeviceList\"}", KasaLogger);

                if (response == null) return (ushort) (_devices.Count > 0 ? 1 : 0);
                if (response.Status != 200) return (ushort) (_devices.Count > 0 ? 1 : 0);
                if (response.Content.Length <= 0) return (ushort) (_devices.Count > 0 ? 1 : 0);
                var body = JObject.Parse(response.Content);

                if (body["result"] == null) return (ushort) (_devices.Count > 0 ? 1 : 0);
                if (body["result"]["deviceList"] == null) return (ushort) (_devices.Count > 0 ? 1 : 0);
                List<KasaDeviceInfo> devices;
                devices = JsonConvert.DeserializeObject<List<KasaDeviceInfo>>(
                            body["result"]["deviceList"].ToString());
                lock (_syncLock) _devices = devices;

                foreach (
                    var device in
                        devices.Where(
                            device => _subscribedDevices.ContainsKey(device.Alias)))
                {
                    _subscribedDevices[device.Alias].Fire(
                        new KasaDeviceEventArgs(eKasaDeviceEventId.GetNow, 1));
                }

                return (ushort) (devices.Count > 0 ? 1 : 0);
            }
            catch (SocketException se)
            {
                KasaLogger.LogException(se);
                return 0;
            }
            catch (HttpsException he)
            {
                KasaLogger.LogException(he);
                return 0;
            }
            catch (Exception e)
            {
                KasaLogger.LogException(e);
                return 0;
            }
        }
    }
}
