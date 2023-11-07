using System;
using System.Collections.Generic;
using Crestron.SimplSharp;
using Crestron.SimplSharp.Net.Https;
using HttpsUtility.Symbols;
using Newtonsoft.Json.Linq;

namespace TpLinkKasa
{
    public class KasaDevice
    {
        private string _alias;
        private KasaDeviceInfo _device;

        private bool _supportsBrightness;
        private bool _supportsRelayState;
        private bool _supportsHue;
        private bool _supportsSaturation;
        private bool _hasChildren;
        private List<KasaDeviceChild> _children;

        public ushort RelayState { get; private set; }
        public ushort Brightness { get; private set; }
        public ushort Hue { get; private set; }
        public ushort Saturation { get; private set; }
        public ushort SupportsBrightness { get { return Convert.ToUInt16(_supportsBrightness); } }
        public ushort SupportsRelayState { get { return Convert.ToUInt16(_supportsRelayState);}}
        public ushort SupportsHue { get { return Convert.ToUInt16(_supportsHue); }}
        public ushort SupportsSaturation { get { return Convert.ToUInt16(_supportsSaturation); }}
        public ushort HasChildren { get { return Convert.ToUInt16(_hasChildren); }}
        public ushort TotalChildren { get; private set; }

        public delegate void NewRelayState(ushort state);
        public delegate void Newbrightness(ushort bri);
        public delegate void NewHue(ushort hue);
        public delegate void NewSaturation(ushort sat);
        public delegate void NewChildrenData(KasaDeviceChildren children);
        public NewRelayState OnNewRelayState { get; set; }
        public Newbrightness OnNewBrightness { get; set; }
        public NewHue OnNewHue { get; set; }
        public NewSaturation OnNewSaturation { get; set; }
        public NewChildrenData OnNewChildrenData { get; set; }

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
                    RelayState = Convert.ToUInt16(e.Value);
                    break;
                case eKasaDeviceEventId.Brightness:
                    Brightness = Convert.ToUInt16(e.Value);
                    break;
                default:
                    break;
            }
        }

        public void GetDevice()
        {
            try
            {
                if ((_device = KasaSystem.Devices.Find(x => x.Alias == _alias)) == null) return;
                if (KasaSystem.Token == null) return;
                if (KasaSystem.Token.Length <= 0) return;

                var response = KasaSystem.Client.Post(string.Format("https://wap.tplinkcloud.com?token={0}", KasaSystem.Token), SimplHttpsClient.ParseHeaders("Content-Type: application/json"), "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + _device.DeviceId + "\",\"requestData\":\"{\\\"system\\\":{\\\"get_sysinfo\\\":{}}}\"}}");

                if (response == null) return;
                if (response.Status != 200) return;
                if (response.Content.Length <= 0) return;

                var body = JObject.Parse(response.Content);

                if (body["result"] == null) return;
                if (body["result"]["responseData"] == null) return;

                var data = body["result"]["responseData"].ToString().Replace("\\\"", "\"");
                var switchData = JObject.Parse(data);

                if (switchData["system"] == null) return;
                if (switchData["system"]["get_sysinfo"] == null) return;

                if (switchData["system"]["get_sysinfo"]["relay_state"] != null)
                {
                    _supportsRelayState = true;
                    RelayState = switchData["system"]["get_sysinfo"]["relay_state"].ToObject<ushort>();

                    if (OnNewRelayState != null)
                    {
                        OnNewRelayState(RelayState);
                    }
                }

                if (switchData["system"]["get_sysinfo"]["brightness"] != null)
                {
                    _supportsBrightness = true;

                    var bri = switchData["system"]["get_sysinfo"]["brightness"].ToObject<int>();

                    Brightness =
                        (ushort) CrestronEnvironment.ScaleWithLimits(bri, 100, 0, ushort.MaxValue, ushort.MinValue);

                    if (OnNewBrightness != null)
                    {
                        OnNewBrightness(Brightness);
                    }
                }
                else
                {
                    _supportsBrightness = false;
                }

                if (switchData["system"]["get_sysinfo"]["light_state"] != null)
                {
                    _supportsBrightness = true;
                    _supportsHue = true;
                    _supportsSaturation = true;

                    var bri = switchData["system"]["get_sysinfo"]["light_state"]["brightness"].ToObject<int>();
                    var hue = switchData["system"]["get_sysinfo"]["light_state"]["hue"].ToObject<int>();
                    var sat = switchData["system"]["get_sysinfo"]["light_state"]["saturation"].ToObject<int>();

                    Brightness =
                        (ushort)CrestronEnvironment.ScaleWithLimits(bri, 100, 0, ushort.MaxValue, ushort.MinValue);
                    Hue = (ushort) CrestronEnvironment.ScaleWithLimits(hue, 360, 0, ushort.MaxValue, ushort.MinValue);
                    Saturation =
                        (ushort) CrestronEnvironment.ScaleWithLimits(sat, 100, 0, ushort.MaxValue, ushort.MinValue);

                    if (OnNewBrightness != null)
                    {
                        OnNewBrightness(Brightness);
                    }

                    if (OnNewHue != null)
                    {
                        OnNewHue(Hue);
                    }

                    if (OnNewSaturation != null)
                    {
                        OnNewSaturation(Saturation);
                    }
                }

                if (switchData["system"]["get_sysinfo"]["child_num"] != null)
                {

                    TotalChildren = switchData["system"]["get_sysinfo"]["child_num"].ToObject<ushort>();

                    if (TotalChildren > 0)
                    {
                        _hasChildren = true;
                    }
                }

                if (switchData["system"]["get_sysinfo"]["children"] != null)
                {
                    _children = switchData["system"]["get_sysinfo"]["children"].ToObject<List<KasaDeviceChild>>();

                    if (OnNewChildrenData != null)
                    {
                        OnNewChildrenData(new KasaDeviceChildren(_children.ToArray()));
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
                if (!_supportsRelayState) return;
                if ((_device = KasaSystem.Devices.Find(x => x.Alias == _alias)) == null) return;
                if (KasaSystem.Token == null) return;
                if (KasaSystem.Token.Length <= 0) return;

                var response = KasaSystem.Client.Post(string.Format("https://wap.tplinkcloud.com?token={0}", KasaSystem.Token), SimplHttpsClient.ParseHeaders("Content-Type: application/json"), "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + _device.DeviceId + "\",\"requestData\":\"{\\\"system\\\":{\\\"set_relay_state\\\":{\\\"state\\\":1}}}\"}}");

                if (response == null) return;
                if (response.Status != 200) return;
                if (response.Content.Length <= 0) return;

                var body = JObject.Parse(response.Content);

                if (body["error_code"] == null) return;
                if (Convert.ToInt16(body["error_code"].ToString()) != 0) return;

                RelayState = 1;

                if (OnNewRelayState != null)
                {
                    OnNewRelayState(RelayState);
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
                if (!_supportsRelayState) return;
                if ((_device = KasaSystem.Devices.Find(x => x.Alias == _alias)) == null) return;
                if (KasaSystem.Token == null) return;
                if (KasaSystem.Token.Length <= 0) return;

                var response = KasaSystem.Client.Post(string.Format("https://wap.tplinkcloud.com?token={0}", KasaSystem.Token), SimplHttpsClient.ParseHeaders("Content-Type: application/json"), "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + _device.DeviceId + "\",\"requestData\":\"{\\\"system\\\":{\\\"set_relay_state\\\":{\\\"state\\\":0}}}\"}}");

                if (response == null) return;
                if (response.Status != 200) return;
                if (response.Content.Length <= 0) return;

                var body = JObject.Parse(response.Content);

                if (body["error_code"] == null) return;
                if (Convert.ToInt16(body["error_code"].ToString()) != 0) return;

                RelayState = 0;

                if (OnNewRelayState != null)
                {
                    OnNewRelayState(RelayState);
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
                if (!_hasChildren) return;
                if (TotalChildren < index) return;
                if (_children[index - 1] == null) return;
                if ((_device = KasaSystem.Devices.Find(x => x.Alias == _alias)) == null) return;
                if (KasaSystem.Token == null) return;
                if (KasaSystem.Token.Length <= 0) return;

                var response = KasaSystem.Client.Post(string.Format("https://wap.tplinkcloud.com?token={0}", KasaSystem.Token), SimplHttpsClient.ParseHeaders("Content-Type: application/json"), "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + _device.DeviceId + "\",\"child\":\"" + _children[index - 1].ID + "\",\"requestData\":\"{\\\"system\\\":{\\\"set_relay_state\\\":{\\\"state\\\":1}}}\"}}");

                if (response == null) return;
                if (response.Status != 200) return;
                if (response.Content.Length <= 0) return;

                var body = JObject.Parse(response.Content);

                if (body["error_code"] == null) return;
                if (Convert.ToInt16(body["error_code"].ToString()) != 0) return;

                _children[index - 1].State = 1;

                if (OnNewChildrenData != null)
                {
                    OnNewChildrenData(new KasaDeviceChildren(_children.ToArray()));
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
                if (!_hasChildren) return;
                if (TotalChildren < index) return;
                if (_children[index - 1] == null) return;
                if ((_device = KasaSystem.Devices.Find(x => x.Alias == _alias)) == null) return;
                if (KasaSystem.Token == null) return;
                if (KasaSystem.Token.Length <= 0) return;

                var response = KasaSystem.Client.Post(string.Format("https://wap.tplinkcloud.com?token={0}", KasaSystem.Token), SimplHttpsClient.ParseHeaders("Content-Type: application/json"), "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + _device.DeviceId + "\",\"child\":\"" + _children[index - 1].ID + "\",\"requestData\":\"{\\\"system\\\":{\\\"set_relay_state\\\":{\\\"state\\\":0}}}\"}}");

                if (response == null) return;
                if (response.Status != 200) return;
                if (response.Content.Length <= 0) return;

                var body = JObject.Parse(response.Content);

                if (body["error_code"] == null) return;
                if (Convert.ToInt16(body["error_code"].ToString()) != 0) return;

                _children[index - 1].State = 0;

                if (OnNewChildrenData != null)
                {
                    OnNewChildrenData(new KasaDeviceChildren(_children.ToArray()));
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
                    if ((_device = KasaSystem.Devices.Find(x => x.Alias == _alias)) == null) return;
                    if (KasaSystem.Token == null) return;
                    if (KasaSystem.Token.Length <= 0) return;

                    var sBri = CrestronEnvironment.ScaleWithLimits(bri, ushort.MaxValue, ushort.MinValue, 100, 0);
                    string briBody;

                    if (!_supportsHue && !_supportsSaturation)
                        briBody = "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + _device.DeviceId +
                                  "\",\"requestData\":\"{\\\"smartlife.iot.dimmer\\\":{\\\"set_brightness\\\":{\\\"brightness\\\":" +
                                  sBri.ToString() + "}}}\"}}";
                    else
                        briBody = "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + _device.DeviceId +
                                  "\",\"requestData\":\"{\\\"smartlife.iot.smartbulb.lightingservice\\\":{\\\"transition_light_state\\\":{\\\"brightness\\\":" +
                                  sBri.ToString() + "}}}\"}}";

                    
                    var response = KasaSystem.Client.Post(string.Format("https://wap.tplinkcloud.com?token={0}", KasaSystem.Token), SimplHttpsClient.ParseHeaders("Content-Type: application/json"), briBody);

                    if (response.Content == null) return;
                    if (response.Status != 200) return;
                    if (response.Content.Length <= 0) return;

                    var body = JObject.Parse(response.Content);

                    if (body["error_code"] == null) return;
                    if (Convert.ToInt16(body["error_code"].ToString()) != 0) return;

                    Brightness = bri;

                    if (OnNewBrightness != null)
                    {
                        OnNewBrightness(Brightness);
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

        public void SetHue(ushort hue)
        {
            try
            {
                if (_supportsHue)
                {
                    if ((_device = KasaSystem.Devices.Find(x => x.Alias == _alias)) == null) return;
                    if (KasaSystem.Token == null) return;
                    if (KasaSystem.Token.Length <= 0) return;

                    var sHue = CrestronEnvironment.ScaleWithLimits(hue, ushort.MaxValue, ushort.MinValue, 360, 0);

                    var hueBody = "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + _device.DeviceId +
                                  "\",\"requestData\":\"{\\\"smartlife.iot.smartbulb.lightingservice\\\":{\\\"transition_light_state\\\":{\\\"hue\\\":" +
                                  sHue.ToString() + "}}}\"}}";

                    var response = KasaSystem.Client.Post(string.Format("https://wap.tplinkcloud.com?token={0}", KasaSystem.Token), SimplHttpsClient.ParseHeaders("Content-Type: application/json"), hueBody);

                    if (response.Content == null) return;
                    if (response.Status != 200) return;
                    if (response.Content.Length <= 0) return;

                    var body = JObject.Parse(response.Content);

                    if (body["error_code"] == null) return;
                    if (Convert.ToInt16(body["error_code"].ToString()) != 0) return;

                    Hue = hue;

                    if (OnNewHue != null)
                    {
                        OnNewHue(Hue);
                    }
                }
                else
                {
                    throw new InvalidOperationException("This device does not support hue");
                }
            }
            catch (SocketException se)
            {
                ErrorLog.Exception("SocketException occured in KasaDevice.SetHue - ", se);
            }
            catch (HttpsException he)
            {
                ErrorLog.Exception("SocketException occured in KasaDevice.SetHue - ", he);
            }
            catch (InvalidOperationException ie)
            {
                ErrorLog.Exception("InvalidoperationException occured in KasaDevice.SetHue - ", ie);
            }
            catch (Exception ex)
            {
                ErrorLog.Exception("SocketException occured in KasaDevice.SetHue - ", ex);
            }
        }

        public void SetSaturation(ushort sat)
        {
            try
            {
                if (_supportsSaturation)
                {
                    if ((_device = KasaSystem.Devices.Find(x => x.Alias == _alias)) == null) return;
                    if (KasaSystem.Token == null) return;
                    if (KasaSystem.Token.Length <= 0) return;

                    var sSat = CrestronEnvironment.ScaleWithLimits(sat, ushort.MaxValue, ushort.MinValue, 100, 0);

                    var satBody = "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + _device.DeviceId +
                                  "\",\"requestData\":\"{\\\"smartlife.iot.smartbulb.lightingservice\\\":{\\\"transition_light_state\\\":{\\\"saturation\\\":" +
                                  sSat.ToString() + "}}}\"}}";



                    var response = KasaSystem.Client.Post(string.Format("https://wap.tplinkcloud.com?token={0}", KasaSystem.Token), SimplHttpsClient.ParseHeaders("Content-Type: application/json"), satBody);

                    if (response.Content == null) return;
                    if (response.Status != 200) return;
                    if (response.Content.Length <= 0) return;

                    var body = JObject.Parse(response.Content);

                    if (body["error_code"] == null) return;
                    if (Convert.ToInt16(body["error_code"].ToString()) != 0) return;

                    Saturation = sat;

                    if (OnNewSaturation != null)
                    {
                        OnNewSaturation(Saturation);
                    }
                }
                else
                {
                    throw new InvalidOperationException("This device does not support saturation");
                }
            }
            catch (SocketException se)
            {
                ErrorLog.Exception("SocketException occured in KasaDevice.SetSaturation - ", se);
            }
            catch (HttpsException he)
            {
                ErrorLog.Exception("SocketException occured in KasaDevice.SetSaturation - ", he);
            }
            catch (InvalidOperationException ie)
            {
                ErrorLog.Exception("InvalidoperationException occured in KasaDevice.SetSaturation - ", ie);
            }
            catch (Exception ex)
            {
                ErrorLog.Exception("SocketException occured in KasaDevice.SetSaturation - ", ex);
            }
        }
    }
}