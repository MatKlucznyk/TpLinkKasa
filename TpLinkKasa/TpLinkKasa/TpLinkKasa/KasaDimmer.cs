using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.Net.Https;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TpLinkKasa
{
    public class KasaDimmer
    {
        private string Alias;
        private KasaDevice dimmer;

        public ushort RelayState { get; set; }
        public ushort Brightness { get; set; }

        public delegate void NewRelayState(ushort state);
        public delegate void Newbrightness(ushort bri);
        public NewRelayState onNewRelayState { get; set; }
        public Newbrightness onNewBrightness { get; set; }

        public void Initialize(string alias)
        {
            Alias = alias;

            if (KasaSystem.RegisterDevice(Alias))
            {
                KasaSystem.SubscribedDevices[Alias].OnNewEvent += new EventHandler<KasaDeviceEventArgs>(KasaDimmer_OnNewEvent);
            }
        }

        void KasaDimmer_OnNewEvent(object sender, KasaDeviceEventArgs e)
        {
            switch (e.Id)
            {
                case eKasaDeviceEventId.GetNow:
                    GetDimmer();
                    break;
                case eKasaDeviceEventId.RelayState:
                    RelayState = Convert.ToUInt16(e.Value);
                    break;
                case eKasaDeviceEventId.Brightness:
                    Brightness = Convert.ToUInt16(e.Value);
                    break;
                default:
                    break;
            }
        }

        public void GetDimmer()
        {
            try
            {
                if ((dimmer = KasaSystem.Devices.Find(x => x.alias == Alias)) != null)
                {

                    if (KasaSystem.Token != null)
                    {
                        if (KasaSystem.Token.Length > 0)
                        {
                            using (HttpsClient client = new HttpsClient())
                            {
                                client.TimeoutEnabled = true;
                                client.Timeout = 10;
                                client.HostVerification = false;
                                client.PeerVerification = false;
                                client.AllowAutoRedirect = false;

                                HttpsClientRequest request = new HttpsClientRequest();

                                request.Url.Parse(string.Format("https://wap.tplinkcloud.com?token={0}", KasaSystem.Token));
                                request.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Post;
                                request.Header.AddHeader(new HttpsHeader("Content-Type", "application/json"));

                                request.ContentString = "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + dimmer.deviceId + "\",\"requestData\":\"{\\\"system\\\":{\\\"get_sysinfo\\\":{}}}\"}}";

                                HttpsClientResponse response = client.Dispatch(request);

                                if (response.ContentString != null)
                                {
                                    if (response.ContentString.Length > 0)
                                    {
                                        JObject body = JObject.Parse(response.ContentString);

                                        if (body["result"] != null)
                                        {
                                            if (body["result"]["responseData"] != null)
                                            {
                                                var data = body["result"]["responseData"].ToString().Replace("\\\"", "\"");
                                                data = data.Remove(0, 1);
                                                data = data.Remove(data.Length - 1, 1);

                                                JObject switchData = JObject.Parse(data);

                                                if (switchData["system"] != null)
                                                {
                                                    if (switchData["system"]["get_sysinfo"] != null)
                                                    {
                                                        if (switchData["system"]["get_sysinfo"]["relay_state"] != null)
                                                        {
                                                            RelayState = Convert.ToUInt16(switchData["system"]["get_sysinfo"]["relay_state"].ToString());

                                                            if (onNewRelayState != null)
                                                            {
                                                                onNewRelayState(RelayState);
                                                            }
                                                        }

                                                        if (switchData["system"]["get_sysinfo"]["brightness"] != null)
                                                        {

                                                            Brightness = (ushort)Math.Round(KasaSystem.ScaleUp(Convert.ToDouble(switchData["system"]["get_sysinfo"]["brightness"].ToString())));

                                                            if (onNewBrightness != null)
                                                            {
                                                                onNewBrightness(Brightness);
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (SocketException se)
            {
                ErrorLog.Exception("SocketException occured in KasaDimmer.KasaDimmer_OnNewEvent - ", se);
            }
            catch (HttpsException he)
            {
                ErrorLog.Exception("SocketException occured in KasaDimmer.KasaDimmer_OnNewEvent - ", he);
            }
            catch (Exception ex)
            {
                ErrorLog.Exception("SocketException occured in KasaDimmer.KasaDimmer_OnNewEvent - ", ex);
            }
        }

        public void PowerOn()
        {
            try
            {
                if ((dimmer = KasaSystem.Devices.Find(x => x.alias == Alias)) != null)
                {

                    if (KasaSystem.Token != null)
                    {
                        if (KasaSystem.Token.Length > 0)
                        {
                            using (HttpsClient client = new HttpsClient())
                            {
                                client.TimeoutEnabled = true;
                                client.Timeout = 10;
                                client.HostVerification = false;
                                client.PeerVerification = false;
                                client.AllowAutoRedirect = false;

                                HttpsClientRequest request = new HttpsClientRequest();

                                request.Url.Parse(string.Format("https://wap.tplinkcloud.com?token={0}", KasaSystem.Token));
                                request.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Post;
                                request.Header.AddHeader(new HttpsHeader("Content-Type", "application/json"));

                                request.ContentString = "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + dimmer.deviceId + "\",\"requestData\":\"{\\\"system\\\":{\\\"set_relay_state\\\":{\\\"state\\\":1}}}\"}}";

                                HttpsClientResponse response = client.Dispatch(request);

                                if (response.ContentString != null)
                                {
                                    if (response.ContentString.Length > 0)
                                    {
                                        JObject body = JObject.Parse(response.ContentString);

                                        if (body["error_code"] != null)
                                        {
                                            if (Convert.ToInt16(body["error_code"].ToString()) == 0)
                                            {
                                                RelayState = 1;

                                                if (onNewRelayState != null)
                                                {
                                                    onNewRelayState(RelayState);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (SocketException se)
            {
                ErrorLog.Exception("SocketException occured in KasaDimmer.PowerOn - ", se);
            }
            catch (HttpsException he)
            {
                ErrorLog.Exception("SocketException occured in KasaDimmer.PowerOn - ", he);
            }
            catch (Exception ex)
            {
                ErrorLog.Exception("SocketException occured in KasaDimmer.PowerOn - ", ex);
            }
        }

        public void PowerOff()
        {
            try
            {
                if ((dimmer = KasaSystem.Devices.Find(x => x.alias == Alias)) != null)
                {

                    if (KasaSystem.Token != null)
                    {
                        if (KasaSystem.Token.Length > 0)
                        {
                            using (HttpsClient client = new HttpsClient())
                            {
                                client.TimeoutEnabled = true;
                                client.Timeout = 10;
                                client.HostVerification = false;
                                client.PeerVerification = false;
                                client.AllowAutoRedirect = false;

                                HttpsClientRequest request = new HttpsClientRequest();

                                request.Url.Parse(string.Format("https://wap.tplinkcloud.com?token={0}", KasaSystem.Token));
                                request.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Post;
                                request.Header.AddHeader(new HttpsHeader("Content-Type", "application/json"));

                                request.ContentString = "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + dimmer.deviceId + "\",\"requestData\":\"{\\\"system\\\":{\\\"set_relay_state\\\":{\\\"state\\\":0}}}\"}}";

                                HttpsClientResponse response = client.Dispatch(request);

                                if (response.ContentString != null)
                                {
                                    if (response.ContentString.Length > 0)
                                    {
                                        JObject body = JObject.Parse(response.ContentString);

                                        if (body["error_code"] != null)
                                        {
                                            if (Convert.ToInt16(body["error_code"].ToString()) == 0)
                                            {
                                                RelayState = 0;

                                                if (onNewRelayState != null)
                                                {
                                                    onNewRelayState(RelayState);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (SocketException se)
            {
                ErrorLog.Exception("SocketException occured in KasaDimmer.PowerOff - ", se);
            }
            catch (HttpsException he)
            {
                ErrorLog.Exception("SocketException occured in KasaDimmer.PowerOff - ", he);
            }
            catch (Exception ex)
            {
                ErrorLog.Exception("SocketException occured in KasaDimmer.PowerOff - ", ex);
            }
        }

        public void SetBrightness(ushort bri)
        {
            try
            {
                if ((dimmer = KasaSystem.Devices.Find(x => x.alias == Alias)) != null)
                {

                    if (KasaSystem.Token != null)
                    {
                        if (KasaSystem.Token.Length > 0)
                        {
                            using (HttpsClient client = new HttpsClient())
                            {
                                client.TimeoutEnabled = true;
                                client.Timeout = 10;
                                client.HostVerification = false;
                                client.PeerVerification = false;
                                client.AllowAutoRedirect = false;

                                HttpsClientRequest request = new HttpsClientRequest();

                                request.Url.Parse(string.Format("https://wap.tplinkcloud.com?token={0}", KasaSystem.Token));
                                request.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Post;
                                request.Header.AddHeader(new HttpsHeader("Content-Type", "application/json"));

                                var sBri = (ushort)Math.Round(KasaSystem.ScaleDown(Convert.ToDouble(bri)));

                                request.ContentString = "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + dimmer.deviceId + "\",\"requestData\":\"{\\\"smartlife.iot.dimmer\\\":{\\\"set_brightness\\\":{\\\"brightness\\\":" + sBri.ToString() + "}}},\"}}";

                                HttpsClientResponse response = client.Dispatch(request);

                                if (response.ContentString != null)
                                {
                                    if (response.ContentString.Length > 0)
                                    {
                                        JObject body = JObject.Parse(response.ContentString);

                                        if (body["error_code"] != null)
                                        {
                                            if (Convert.ToInt16(body["error_code"].ToString()) == 0)
                                            {
                                                Brightness = bri;

                                                if (onNewBrightness != null)
                                                {
                                                    onNewBrightness(Brightness);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (SocketException se)
            {
                ErrorLog.Exception("SocketException occured in KasaDimmer.SetBrightness - ", se);
            }
            catch (HttpsException he)
            {
                ErrorLog.Exception("SocketException occured in KasaDimmer.SetBrightness - ", he);
            }
            catch (Exception ex)
            {
                ErrorLog.Exception("SocketException occured in KasaDimmer.SetBrightness - ", ex);
            }
        }
    }
}