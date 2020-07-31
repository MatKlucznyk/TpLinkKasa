using System;
using System.Text;
using System.Collections.Generic;
using Crestron.SimplSharp;                          				// For Basic SIMPL# Classes
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Crestron.SimplSharp.Net.Https;

namespace TpLinkKasa
{
    public static class KasaSystem
    {
        public static string Username { get; set; }
        public static string Password { get; set; }

        internal static string Token;
        internal static List<KasaDeviceInfo> Devices = new List<KasaDeviceInfo>();
        internal static Dictionary<string, KasaDeviceSubscriptionEvent> SubscribedDevices = new Dictionary<string, KasaDeviceSubscriptionEvent>();

        internal static bool RegisterDevice(string alias)
        {
            try
            {
                lock (SubscribedDevices)
                {
                    if (!SubscribedDevices.ContainsKey(alias))
                    {
                        SubscribedDevices.Add(alias, new KasaDeviceSubscriptionEvent());

                        return true;
                    }
                    else
                        return false;
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
                    using (HttpsClient client = new HttpsClient())
                    {
                        client.TimeoutEnabled = true;
                        client.Timeout = 10;
                        client.HostVerification = false;
                        client.PeerVerification = false;
                        client.AllowAutoRedirect = false;

                        HttpsClientRequest request = new HttpsClientRequest();

                        request.Url.Parse("https://wap.tplinkcloud.com");
                        request.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Post;
                        request.Header.AddHeader(new HttpsHeader("Content-Type", "application/json"));

                        request.ContentString = "{\"method\":\"login\",\"params\":{\"appType\":\"Crestron\",\"cloudUserName\":\"" + Username + "\",\"cloudPassword\":\"" + Password + "\",\"terminalUUID\":\"3df98660-6155-4a7d-bc70-8622d41c767e\"}}";

                        HttpsClientResponse response = client.Dispatch(request);

                        if (response.ContentString != null)
                        {
                            if (response.ContentString.Length > 0)
                            {
                                JObject body = JObject.Parse(response.ContentString);

                                if (body["result"] != null)
                                {
                                    if (body["result"]["token"] != null)
                                    {
                                        Token = body["result"]["token"].ToString().Replace("\"", string.Empty);
                                    }
                                }
                            }
                        }
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
                if (Username.Length > 0 && Password.Length > 0)
                {
                    GetToken();

                    if (Token != null)
                    {
                        if (Token.Length > 0)
                        {
                            using (HttpsClient client = new HttpsClient())
                            {
                                client.TimeoutEnabled = true;
                                client.Timeout = 10;
                                client.HostVerification = false;
                                client.PeerVerification = false;
                                client.AllowAutoRedirect = false;

                                HttpsClientRequest request = new HttpsClientRequest();

                                request.Url.Parse(string.Format("https://wap.tplinkcloud.com?token={0}", Token));
                                request.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Post;
                                request.Header.AddHeader(new HttpsHeader("Content-Type", "application/json"));

                                request.ContentString = "{\"method\":\"getDeviceList\"}";

                                HttpsClientResponse response = client.Dispatch(request);

                                if (response.ContentString != null)
                                {
                                    if (response.ContentString.Length > 0)
                                    {
                                        JObject body = JObject.Parse(response.ContentString);

                                        if (body["result"] != null)
                                        {
                                            if (body["result"]["deviceList"] != null)
                                            {
                                                Devices = JsonConvert.DeserializeObject<List<KasaDeviceInfo>>(body["result"]["deviceList"].ToString());

                                                foreach (var device in Devices)
                                                {
                                                    if (SubscribedDevices.ContainsKey(device.alias))
                                                    {
                                                        SubscribedDevices[device.alias].Fire(new KasaDeviceEventArgs(eKasaDeviceEventId.GetNow, 1));
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (Devices.Count > 0)
                        return 1;
                    else
                        return 0;
                }

                else
                {
                    throw new ArgumentException("Username and Password cannot be emtpy");
                }

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

        internal static double ScaleUp(double level)
        {
            double scaleLevel = level;
            double levelScaled = (scaleLevel * (65535.0 / 100.0));
            if (levelScaled == 1)
                levelScaled = 0;
            return levelScaled;
        }

        internal static double ScaleDown(double level)
        {
            double scaleLevel = level;
            double levelScaled = (scaleLevel / (65535.0 / 100.0));
            if (levelScaled == 0)
                levelScaled = 1;
            return levelScaled;
        }
    }
}
