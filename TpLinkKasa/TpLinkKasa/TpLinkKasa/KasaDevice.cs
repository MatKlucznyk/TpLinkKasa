using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.Net.Https;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using HttpsUtility.Symbols;

namespace TpLinkKasa
{
    public class KasaDevice
    {
        private string _alias;
        private KasaDeviceInfo _device;
        //private HttpsClient client;

        private ushort _brightness;
        private ushort _relayState;
        private bool _supportsBrightness;
        private bool _supportsRelayState;
        private ushort _hasChildren;
        private ushort _totalChildern;
        private List<KasaDeviceChild> _children;

        public ushort RelayState { get { return _relayState; } }
        public ushort Brightness { get { return _brightness; } }
        public ushort SupportsBrightness { get { return Convert.ToUInt16(_supportsBrightness); } }
        public ushort SupportsRelayState { get { return Convert.ToUInt16(_supportsRelayState);}}
        public ushort HasChildren { get { return _hasChildren; } }
        public ushort TotalChildren { get { return _totalChildern; } }

        public delegate void NewRelayState(ushort state);
        public delegate void Newbrightness(ushort bri);
        public delegate void NewChildrenData(KasaDeviceChildren children);
        public NewRelayState onNewRelayState { get; set; }
        public Newbrightness onNewBrightness { get; set; }
        public NewChildrenData onNewChildrenData { get; set; }

        public void Initialize(string alias)
        {
            _alias = alias;

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
                    _relayState = Convert.ToUInt16(e.Value);
                    break;
                case eKasaDeviceEventId.Brightness:
                    _brightness = Convert.ToUInt16(e.Value);
                    break;
                default:
                    break;
            }
        }

        public void GetDevice()
        {
            try
            {
                if ((_device = KasaSystem.Devices.Find(x => x.Alias == _alias)) != null)
                {

                    if (KasaSystem.Token != null)
                    {
                        if (KasaSystem.Token.Length > 0)
                        {
                            /*HttpsClientRequest request = new HttpsClientRequest();


                            request.Url.Parse(string.Format("https://wap.tplinkcloud.com?token={0}", KasaSystem.Token));
                            request.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Post;
                            request.Header.AddHeader(new HttpsHeader("Content-Type", "application/json"));

                            request.ContentString = "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + device.deviceId + "\",\"requestData\":\"{\\\"system\\\":{\\\"get_sysinfo\\\":{}}}\"}}";

                            HttpsClientResponse response = client.Dispatch(request);*/

                            var response = KasaSystem.Client.Post(string.Format("https://wap.tplinkcloud.com?token={0}", KasaSystem.Token), SimplHttpsClient.ParseHeaders("Content-Type: application/json"), "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + _device.DeviceId + "\",\"requestData\":\"{\\\"system\\\":{\\\"get_sysinfo\\\":{}}}\"}}");

                            if (response != null)
                            {
                                if (response.Status == 200)
                                {
                                    if (response.Content.Length > 0)
                                    {
                                        var body = JObject.Parse(response.Content);

                                        if (body["result"] != null)
                                        {
                                            if (body["result"]["responseData"] != null)
                                            {
                                                var data = body["result"]["responseData"].ToString().Replace("\\\"", "\"");
                                                /*data = data.Remove(0, 1);
                                                data = data.Remove(data.Length - 1, 1);*/

                                                var switchData = JObject.Parse(data);

                                                if (switchData["system"] != null)
                                                {
                                                    if (switchData["system"]["get_sysinfo"] != null)
                                                    {
                                                        if (switchData["system"]["get_sysinfo"]["relay_state"] != null)
                                                        {
                                                            _supportsRelayState = true;
                                                            _relayState = switchData["system"]["get_sysinfo"]["relay_state"].ToObject<ushort>();

                                                            if (onNewRelayState != null)
                                                            {
                                                                onNewRelayState(_relayState);
                                                            }
                                                        }

                                                        if (switchData["system"]["get_sysinfo"]["brightness"] != null)
                                                        {
                                                            _supportsBrightness = true;

                                                            _brightness = (ushort)Math.Round(KasaSystem.ScaleUp(switchData["system"]["get_sysinfo"]["brightness"].ToObject<Double>()));

                                                            if (onNewBrightness != null)
                                                            {
                                                                onNewBrightness(_brightness);
                                                            }
                                                        }
                                                        else
                                                        {
                                                            _supportsBrightness = false;
                                                        }

                                                        if (switchData["system"]["get_sysinfo"]["child_num"] != null)
                                                        {

                                                            _totalChildern = switchData["system"]["get_sysinfo"]["child_num"].ToObject<ushort>();

                                                            if (_totalChildern > 0)
                                                            {
                                                                _hasChildren = 1;
                                                            }
                                                        }

                                                        if (switchData["system"]["get_sysinfo"]["children"] != null)
                                                        {
                                                            _children = switchData["system"]["get_sysinfo"]["children"].ToObject<List<KasaDeviceChild>>();

                                                            if (onNewChildrenData != null)
                                                            {
                                                                onNewChildrenData(new KasaDeviceChildren(_children.ToArray()));
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
                if (_supportsRelayState)
                {
                    if ((_device = KasaSystem.Devices.Find(x => x.Alias == _alias)) != null)
                    {

                        if (KasaSystem.Token != null)
                        {
                            if (KasaSystem.Token.Length > 0)
                            {
                                /*HttpsClientRequest request = new HttpsClientRequest();

                                request.Url.Parse(string.Format("https://wap.tplinkcloud.com?token={0}", KasaSystem.Token));
                                request.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Post;
                                request.Header.AddHeader(new HttpsHeader("Content-Type", "application/json"));

                                request.ContentString = "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + device.deviceId + "\",\"requestData\":\"{\\\"system\\\":{\\\"set_relay_state\\\":{\\\"state\\\":1}}}\"}}";

                                HttpsClientResponse response = client.Dispatch(request);*/

                                var response = KasaSystem.Client.Post(string.Format("https://wap.tplinkcloud.com?token={0}", KasaSystem.Token), SimplHttpsClient.ParseHeaders("Content-Type: application/json"), "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + _device.DeviceId + "\",\"requestData\":\"{\\\"system\\\":{\\\"set_relay_state\\\":{\\\"state\\\":1}}}\"}}");

                                if (response != null)
                                {
                                    if (response.Status == 200)
                                    {
                                        if (response.Content.Length > 0)
                                        {
                                            var body = JObject.Parse(response.Content);

                                            if (body["error_code"] != null)
                                            {
                                                if (Convert.ToInt16(body["error_code"].ToString()) == 0)
                                                {
                                                    _relayState = 1;

                                                    if (onNewRelayState != null)
                                                    {
                                                        onNewRelayState(_relayState);
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
                if (_supportsRelayState)
                {
                    if ((_device = KasaSystem.Devices.Find(x => x.Alias == _alias)) != null)
                    {

                        if (KasaSystem.Token != null)
                        {
                            if (KasaSystem.Token.Length > 0)
                            {
                                /*HttpsClientRequest request = new HttpsClientRequest();

                                request.Url.Parse(string.Format("https://wap.tplinkcloud.com?token={0}", KasaSystem.Token));
                                request.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Post;
                                request.Header.AddHeader(new HttpsHeader("Content-Type", "application/json"));

                                request.ContentString = "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + device.deviceId + "\",\"requestData\":\"{\\\"system\\\":{\\\"set_relay_state\\\":{\\\"state\\\":0}}}\"}}";

                                HttpsClientResponse response = client.Dispatch(request);*/

                                var response = KasaSystem.Client.Post(string.Format("https://wap.tplinkcloud.com?token={0}", KasaSystem.Token), SimplHttpsClient.ParseHeaders("Content-Type: application/json"), "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + _device.DeviceId + "\",\"requestData\":\"{\\\"system\\\":{\\\"set_relay_state\\\":{\\\"state\\\":0}}}\"}}");

                                if (response != null)
                                {
                                    if (response.Status == 200)
                                    {
                                        if (response.Content.Length > 0)
                                        {
                                            JObject body = JObject.Parse(response.Content);

                                            if (body["error_code"] != null)
                                            {
                                                if (Convert.ToInt16(body["error_code"].ToString()) == 0)
                                                {
                                                    _relayState = 0;

                                                    if (onNewRelayState != null)
                                                    {
                                                        onNewRelayState(_relayState);
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

        public void PowerOnChild(ushort index)
        {
            try
            {
                if (_hasChildren == 1)
                {
                    if (_totalChildern >= index)
                    {
                        if (_children[index - 1] != null)
                        {
                            if ((_device = KasaSystem.Devices.Find(x => x.Alias == _alias)) != null)
                            {

                                if (KasaSystem.Token != null)
                                {
                                    if (KasaSystem.Token.Length > 0)
                                    {
                                        /*HttpsClientRequest request = new HttpsClientRequest();

                                        request.Url.Parse(string.Format("https://wap.tplinkcloud.com?token={0}", KasaSystem.Token));
                                        request.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Post;
                                        request.Header.AddHeader(new HttpsHeader("Content-Type", "application/json"));

                                        request.ContentString = "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + device.deviceId + "\",\"requestData\":\"{\\\"system\\\":{\\\"set_relay_state\\\":{\\\"state\\\":1}}}\"}}";

                                        HttpsClientResponse response = client.Dispatch(request);*/

                                        var response = KasaSystem.Client.Post(string.Format("https://wap.tplinkcloud.com?token={0}", KasaSystem.Token), SimplHttpsClient.ParseHeaders("Content-Type: application/json"), "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + _device.DeviceId + "\",\"child\":\"" + _children[index - 1].ID + "\",\"requestData\":\"{\\\"system\\\":{\\\"set_relay_state\\\":{\\\"state\\\":1}}}\"}}");

                                        if (response != null)
                                        {
                                            if (response.Status == 200)
                                            {
                                                if (response.Content.Length > 0)
                                                {
                                                    var body = JObject.Parse(response.Content);

                                                    if (body["error_code"] != null)
                                                    {
                                                        if (Convert.ToInt16(body["error_code"].ToString()) == 0)
                                                        {
                                                            _children[index - 1].State = 1;

                                                            if (onNewChildrenData != null)
                                                            {
                                                                onNewChildrenData(new KasaDeviceChildren(_children.ToArray()));
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
                ErrorLog.Exception("SocketException occured in KasaDevice.PowerOnChild - ", se);
            }
            catch (HttpsException he)
            {
                ErrorLog.Exception("SocketException occured in KasaDevice.PowerOnChild - ", he);
            }
            catch (Exception ex)
            {
                ErrorLog.Exception("SocketException occured in KasaDevice.PowerOnChild - ", ex);
            }
        }

        public void PowerOffChild(ushort index)
        {
            try
            {
                if (_hasChildren == 1)
                {
                    if (_totalChildern >= index)
                    {
                        if (_children[index - 1] != null)
                        {
                            if ((_device = KasaSystem.Devices.Find(x => x.Alias == _alias)) != null)
                            {

                                if (KasaSystem.Token != null)
                                {
                                    if (KasaSystem.Token.Length > 0)
                                    {
                                        /*HttpsClientRequest request = new HttpsClientRequest();

                                        request.Url.Parse(string.Format("https://wap.tplinkcloud.com?token={0}", KasaSystem.Token));
                                        request.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Post;
                                        request.Header.AddHeader(new HttpsHeader("Content-Type", "application/json"));

                                        request.ContentString = "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + device.deviceId + "\",\"requestData\":\"{\\\"system\\\":{\\\"set_relay_state\\\":{\\\"state\\\":1}}}\"}}";

                                        HttpsClientResponse response = client.Dispatch(request);*/

                                        var response = KasaSystem.Client.Post(string.Format("https://wap.tplinkcloud.com?token={0}", KasaSystem.Token), SimplHttpsClient.ParseHeaders("Content-Type: application/json"), "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + _device.DeviceId + "\",\"child\":\"" + _children[index - 1].ID + "\",\"requestData\":\"{\\\"system\\\":{\\\"set_relay_state\\\":{\\\"state\\\":0}}}\"}}");

                                        if (response != null)
                                        {
                                            if (response.Status == 200)
                                            {
                                                if (response.Content.Length > 0)
                                                {
                                                    var body = JObject.Parse(response.Content);

                                                    if (body["error_code"] != null)
                                                    {
                                                        if (Convert.ToInt16(body["error_code"].ToString()) == 0)
                                                        {
                                                            _children[index - 1].State = 0;

                                                            if (onNewChildrenData != null)
                                                            {
                                                                onNewChildrenData(new KasaDeviceChildren(_children.ToArray()));
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
                ErrorLog.Exception("SocketException occured in KasaDevice.PowerOnChild - ", se);
            }
            catch (HttpsException he)
            {
                ErrorLog.Exception("SocketException occured in KasaDevice.PowerOnChild - ", he);
            }
            catch (Exception ex)
            {
                ErrorLog.Exception("SocketException occured in KasaDevice.PowerOnChild - ", ex);
            }
        }

        public void SetBrightness(ushort bri)
        {
            try
            {
                if (_supportsBrightness)
                {
                    if ((_device = KasaSystem.Devices.Find(x => x.Alias == _alias)) != null)
                    {

                        if (KasaSystem.Token != null)
                        {
                            if (KasaSystem.Token.Length > 0)
                            {
                                /*
                                HttpsClientRequest request = new HttpsClientRequest();

                                request.Url.Parse(string.Format("https://wap.tplinkcloud.com?token={0}", KasaSystem.Token));
                                request.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Post;
                                request.Header.AddHeader(new HttpsHeader("Content-Type", "application/json"));

                                var sBri = (ushort)Math.Round(KasaSystem.ScaleDown(Convert.ToDouble(bri)));

                                request.ContentString = "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + device.deviceId + "\",\"requestData\":\"{\\\"smartlife.iot.dimmer\\\":{\\\"set_brightness\\\":{\\\"brightness\\\":" + sBri.ToString() + "}}},\"}}";

                                HttpsClientResponse response = client.Dispatch(request);
                                 */
                                var sBri = (ushort)Math.Round(KasaSystem.ScaleDown(Convert.ToDouble(bri)));
                                var response = KasaSystem.Client.Post(string.Format("https://wap.tplinkcloud.com?token={0}", KasaSystem.Token), SimplHttpsClient.ParseHeaders("Content-Type: application/json"), "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + _device.DeviceId + "\",\"requestData\":\"{\\\"smartlife.iot.dimmer\\\":{\\\"set_brightness\\\":{\\\"brightness\\\":" + sBri.ToString() + "}}},\"}}");

                                if (response.Content != null)
                                {
                                    if (response.Status == 200)
                                    {
                                        if (response.Content.Length > 0)
                                        {
                                            var body = JObject.Parse(response.Content);

                                            if (body["error_code"] != null)
                                            {
                                                if (Convert.ToInt16(body["error_code"].ToString()) == 0)
                                                {
                                                    _brightness = bri;

                                                    if (onNewBrightness != null)
                                                    {
                                                        onNewBrightness(_brightness);
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