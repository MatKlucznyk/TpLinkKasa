using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharp;                          				// For Basic SIMPL# Classes
using Crestron.SimplSharp.Net.Https;
using HttpsUtility.Https;
using HttpsUtility.Symbols;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TpLinkKasa
{
    public static class KasaSystem
    {
        public static string Username { get; set; }
        public static string Password { get; set; }

        internal static string Token;
        internal static List<KasaDeviceInfo> Devices = new List<KasaDeviceInfo>();
        internal static Dictionary<string, KasaDeviceSubscriptionEvent> SubscribedDevices = new Dictionary<string, KasaDeviceSubscriptionEvent>();

        internal static readonly HttpsClientPool Client = SingletonHttpsClientPool.Instance.ClientPool;
  
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
                ErrorLog.Exception("Exception occured in System.RegisterDevice - ", e);
                return false;
            }
        }

        private static void GetToken()
        {
            try
            {
                if (Username.Length > 0 && Password.Length > 0)
                {
                    var response = Client.Post("https://wap.tplinkcloud.com", SimplHttpsClient.ParseHeaders("Content-Type: application/json"), "{\"method\":\"login\",\"params\":{\"appType\":\"Crestron\",\"cloudUserName\":\"" + Username + "\",\"cloudPassword\":\"" + Password + "\",\"terminalUUID\":\"3df98660-6155-4a7d-bc70-8622d41c767e\"}}");

                    if (response == null) return;
                    if (response.Status != 200) return;
                    if (response.Content.Length <= 0) return;
                    var body = JObject.Parse(response.Content);

                    if (body["result"] == null) return;
                    if (body["result"]["token"] != null)
                    {
                        Token = body["result"]["token"].ToString().Replace("\"", string.Empty);
                    }
                }
                else
                {
                    throw new ArgumentException("Username and Password cannot be emtpy");
                }

            }
            catch (SocketException se)
            {
                ErrorLog.Exception("SocketException occured in System.GetToken - ", se);
            }
            catch (HttpsException he)
            {
                ErrorLog.Exception("HttpsException occured in System.GetToken - ", he);
            }
            catch (Exception e)
            {
                ErrorLog.Exception("Exception occured in System.GetToken - ", e);
            }
        }

        public static ushort GetSystem()
        {
            try
            {
                if (Username.Length <= 0 || Password.Length <= 0)
                    throw new ArgumentException("Username and Password cannot be emtpy");
                GetToken();

                if (Token == null) return (ushort) (Devices.Count > 0 ? 1 : 0);
                if (Token.Length <= 0) return (ushort) (Devices.Count > 0 ? 1 : 0);
                var response = Client.Post(string.Format("https://wap.tplinkcloud.com?token={0}", Token),
                    SimplHttpsClient.ParseHeaders("Content-Type: application/json"),
                    "{\"method\":\"getDeviceList\"}");

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
                ErrorLog.Exception("SocketException occured in System.GetSystem - ", se);
                return 0;
            }
            catch (HttpsException he)
            {
                ErrorLog.Exception("HttpsException occured in System.GetSystem - ", he);
                return 0;
            }
            catch (Exception e)
            {
                ErrorLog.Exception("Exception occured in System.GetSystem - ", e);
                return 0;
            }
        }
    }
}
