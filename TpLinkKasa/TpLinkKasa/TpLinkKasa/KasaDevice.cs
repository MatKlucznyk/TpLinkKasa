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
    public class KasaDevice
    {
        private string alias;
        private KasaDeviceInfo device;

        private ushort brightness;
        private ushort relayState;
        private ushort supportsBrightness;

        public ushort RelayState { get { return relayState; } }
        public ushort Brightness { get { return brightness; } }
        public ushort SupportsBrightness { get { return supportsBrightness; } }

        public delegate void NewRelayState(ushort state);
        public delegate void Newbrightness(ushort bri);
        public NewRelayState onNewRelayState { get; set; }
        public Newbrightness onNewBrightness { get; set; }

        public void Initialize(string alias)
        {
            this.alias = alias;

            if (KasaSystem.RegisterDevice(alias))
            {
                KasaSystem.SubscribedDevices[alias].OnNewEvent += new EventHandler<KasaDeviceEventArgs>(KasaDevice_OnNewEvent);
            }
        }

        void KasaDevice_OnNewEvent(object sender, KasaDeviceEventArgs e)
        {
            switch (e.Id)
            {
                case eKasaDeviceEventId.GetNow:
                    GetDevice();
                    break;
                case eKasaDeviceEventId.RelayState:
                    relayState = Convert.ToUInt16(e.Value);
                    break;
                case eKasaDeviceEventId.Brightness:
                    brightness = Convert.ToUInt16(e.Value);
                    break;
                default:
                    break;
            }
        }

        public void GetDevice()
        {
            try
            {
                if ((device = KasaSystem.Devices.Find(x => x.alias == alias)) != null)
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
                                client.IncludeHeaders = false;

                                HttpsClientRequest request = new HttpsClientRequest();

                                request.Url.Parse(string.Format("https://wap.tplinkcloud.com?token={0}", KasaSystem.Token));
                                request.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Post;
                                request.Header.AddHeader(new HttpsHeader("Content-Type", "application/json"));

                                request.ContentString = "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + device.deviceId + "\",\"requestData\":\"{\\\"system\\\":{\\\"get_sysinfo\\\":{}}}\"}}";

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
                                                /*data = data.Remove(0, 1);
                                                data = data.Remove(data.Length - 1, 1);*/

                                                JObject switchData = JObject.Parse(data);

                                                if (switchData["system"] != null)
                                                {
                                                    if (switchData["system"]["get_sysinfo"] != null)
                                                    {
                                                        if (switchData["system"]["get_sysinfo"]["relay_state"] != null)
                                                        {
                                                            relayState = Convert.ToUInt16(switchData["system"]["get_sysinfo"]["relay_state"].ToString());

                                                            if (onNewRelayState != null)
                                                            {
                                                                onNewRelayState(relayState);
                                                            }
                                                        }

                                                        if (switchData["system"]["get_sysinfo"]["brightness"] != null)
                                                        {
                                                            supportsBrightness = 1;

                                                            brightness = (ushort)Math.Round(KasaSystem.ScaleUp(Convert.ToDouble(switchData["system"]["get_sysinfo"]["brightness"].ToString())));

                                                            if (onNewBrightness != null)
                                                            {
                                                                onNewBrightness(brightness);
                                                            }
                                                        }
                                                        else
                                                        {
                                                            supportsBrightness = 0;
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
                ErrorLog.Exception("SocketException occured in KasaDevice.KasaDevice_OnNewEvent - ", se);
            }
            catch (HttpsException he)
            {
                ErrorLog.Exception("SocketException occured in KasaDevice.KasaDevice_OnNewEvent - ", he);
            }
            catch (Exception ex)
            {
                ErrorLog.Exception("SocketException occured in KasaDevice.KasaDevice_OnNewEvent - ", ex);
            }
        }

        public void PowerOn()
        {
            try
            {
                if ((device = KasaSystem.Devices.Find(x => x.alias == alias)) != null)
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
                                client.IncludeHeaders = false;

                                HttpsClientRequest request = new HttpsClientRequest();

                                request.Url.Parse(string.Format("https://wap.tplinkcloud.com?token={0}", KasaSystem.Token));
                                request.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Post;
                                request.Header.AddHeader(new HttpsHeader("Content-Type", "application/json"));

                                request.ContentString = "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + device.deviceId + "\",\"requestData\":\"{\\\"system\\\":{\\\"set_relay_state\\\":{\\\"state\\\":1}}}\"}}";

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
                                                relayState = 1;

                                                if (onNewRelayState != null)
                                                {
                                                    onNewRelayState(relayState);
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
                ErrorLog.Exception("SocketException occured in KasaDevice.PowerOn - ", se);
            }
            catch (HttpsException he)
            {
                ErrorLog.Exception("SocketException occured in KasaDevice.PowerOn - ", he);
            }
            catch (Exception ex)
            {
                ErrorLog.Exception("SocketException occured in KasaDevice.PowerOn - ", ex);
            }
        }

        public void PowerOff()
        {
            try
            {
                if ((device = KasaSystem.Devices.Find(x => x.alias == alias)) != null)
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
                                client.IncludeHeaders = false;

                                HttpsClientRequest request = new HttpsClientRequest();

                                request.Url.Parse(string.Format("https://wap.tplinkcloud.com?token={0}", KasaSystem.Token));
                                request.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Post;
                                request.Header.AddHeader(new HttpsHeader("Content-Type", "application/json"));

                                request.ContentString = "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + device.deviceId + "\",\"requestData\":\"{\\\"system\\\":{\\\"set_relay_state\\\":{\\\"state\\\":0}}}\"}}";

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
                                                relayState = 0;

                                                if (onNewRelayState != null)
                                                {
                                                    onNewRelayState(relayState);
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
                ErrorLog.Exception("SocketException occured in KasaDevice.PowerOff - ", se);
            }
            catch (HttpsException he)
            {
                ErrorLog.Exception("SocketException occured in KasaDevice.PowerOff - ", he);
            }
            catch (Exception ex)
            {
                ErrorLog.Exception("SocketException occured in KasaDevice.PowerOff - ", ex);
            }
        }

        public void SetBrightness(ushort bri)
        {
            try
            {
                if (supportsBrightness == 1)
                {
                    if ((device = KasaSystem.Devices.Find(x => x.alias == alias)) != null)
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
                                    client.IncludeHeaders = false;

                                    HttpsClientRequest request = new HttpsClientRequest();

                                    request.Url.Parse(string.Format("https://wap.tplinkcloud.com?token={0}", KasaSystem.Token));
                                    request.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Post;
                                    request.Header.AddHeader(new HttpsHeader("Content-Type", "application/json"));

                                    var sBri = (ushort)Math.Round(KasaSystem.ScaleDown(Convert.ToDouble(bri)));

                                    request.ContentString = "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + device.deviceId + "\",\"requestData\":\"{\\\"smartlife.iot.dimmer\\\":{\\\"set_brightness\\\":{\\\"brightness\\\":" + sBri.ToString() + "}}},\"}}";

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
                                                    brightness = bri;

                                                    if (onNewBrightness != null)
                                                    {
                                                        onNewBrightness(brightness);
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
                else
                {
                    throw new InvalidOperationException("This device does not support brightness");
                }
            }
            catch (SocketException se)
            {
                ErrorLog.Exception("SocketException occured in KasaDevice.SetBrightness - ", se);
            }
            catch (HttpsException he)
            {
                ErrorLog.Exception("SocketException occured in KasaDevice.SetBrightness - ", he);
            }
            catch (InvalidOperationException ie)
            {
                ErrorLog.Exception("InvalidoperationException occured in KasaDevice.SetBrightness - ", ie);
            }
            catch (Exception ex)
            {
                ErrorLog.Exception("SocketException occured in KasaDevice.SetBrightness - ", ex);
            }
        }
    }
}