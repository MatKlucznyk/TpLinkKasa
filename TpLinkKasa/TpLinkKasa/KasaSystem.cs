using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharp;                          				// For Basic SIMPL# Classes
using Crestron.SimplSharp.Net.Https;
using Avg.Communications.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Avg.ModuleFramework.Logging;

namespace TpLinkKasa
{
    public static class KasaSystem
    {
        internal static string BaseUrl = "https://wap.tplinkcloud.com";

        public static string Username { get; set; }
        public static string Password { get; set; }

        internal static string Token;
        internal static List<KasaDeviceInfo> Devices = new List<KasaDeviceInfo>();
        internal static Dictionary<string, KasaDeviceSubscriptionEvent> SubscribedDevices = new Dictionary<string, KasaDeviceSubscriptionEvent>();
        private static int _tokenGetCnt;

        internal static readonly HttpsClientPool Client = new HttpsClientPool(50);
        internal static Logger KasaLogger = new Logger("TpLinkKasa");

        internal static bool RegisterDevice(string alias)
        {
            try
            {
                lock (SubscribedDevices)
                {
                    if (SubscribedDevices.ContainsKey(alias)) return false;
                    SubscribedDevices.Add(alias, new KasaDeviceSubscriptionEvent());

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
                        Token = body["result"]["token"].ToString().Replace("\"", string.Empty);
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

        public static ushort GetSystem()
        {
            try
            {
                if (Username.Length <= 0 || Password.Length <= 0)
                    throw new ArgumentException("Username and Password cannot be empty");
                GetToken();

                if (Token == null) return (ushort) (Devices.Count > 0 ? 1 : 0);
                if (Token.Length <= 0) return (ushort) (Devices.Count > 0 ? 1 : 0);

                var headers = new Dictionary<string, string>()
                {
                    { "Content-Type", "application/json" }
                };

                var response = Client.SendRequest($"{BaseUrl}?token={Token}", Crestron.SimplSharp.Net.AuthMethod.NONE, RequestType.Post, headers, string.Empty, string.Empty, "{\"method\":\"getDeviceList\"}", KasaLogger);

                if (response == null) return (ushort) (Devices.Count > 0 ? 1 : 0);
                if (response.Status != 200) return (ushort) (Devices.Count > 0 ? 1 : 0);
                if (response.Content.Length <= 0) return (ushort) (Devices.Count > 0 ? 1 : 0);
                var body = JObject.Parse(response.Content);

                if (body["result"] == null) return (ushort) (Devices.Count > 0 ? 1 : 0);
                if (body["result"]["deviceList"] == null) return (ushort) (Devices.Count > 0 ? 1 : 0);
                Devices =
                    JsonConvert.DeserializeObject<List<KasaDeviceInfo>>(
                        body["result"]["deviceList"].ToString());

                foreach (
                    var device in
                        Devices.Where(
                            device => SubscribedDevices.ContainsKey(device.Alias)))
                {
                    SubscribedDevices[device.Alias].Fire(
                        new KasaDeviceEventArgs(eKasaDeviceEventId.GetNow, 1));
                }

                return (ushort) (Devices.Count > 0 ? 1 : 0);
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
