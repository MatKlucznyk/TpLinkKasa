using Crestron.SimplSharp;
using Crestron.SimplSharp.Net.Https;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

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
        public ushort SupportsBrightness { get { return _supportsBrightness ? (ushort)1 : (ushort)0; } }
        public ushort SupportsRelayState { get { return _supportsRelayState ? (ushort)1 : (ushort)0; }}
        public ushort SupportsHue { get { return _supportsHue ? (ushort)1 : (ushort)0; }}
        public ushort SupportsSaturation { get { return _supportsSaturation ? (ushort)1 : (ushort)0; }}
        public ushort HasChildren { get { return _hasChildren ? (ushort)1 : (ushort)0; }}
        public ushort TotalChildren { get; private set; }

        public delegate void NewRelayState(ushort state);
        public delegate void NewBrightness(ushort bri);
        public delegate void NewHue(ushort hue);
        public delegate void NewSaturation(ushort sat);
        public delegate void NewChildrenData(KasaDeviceChildren children);
        public NewRelayState OnNewRelayState { get; set; }
        public NewBrightness OnNewBrightness { get; set; }
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

                var headers = new Dictionary<string, string>()
                {
                    { "Content-Type", "application/json" }
                };

                var content = "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + _device.DeviceId + "\",\"requestData\":\"{\\\"system\\\":{\\\"get_sysinfo\\\":{}}}\"}}";

                var response = KasaSystem.Client.SendRequest($"{KasaSystem.BaseUrl}?token={KasaSystem.Token}", Crestron.SimplSharp.Net.AuthMethod.NONE, RequestType.Post, headers, string.Empty, string.Empty, content, KasaSystem.KasaLogger);

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
                            KasaSystem.GetSystem();
                            return;
                        }
                    }
                }

                var data = body["result"]?["responseData"]?.ToString().Replace("\\\"", "\"");
                if(string.IsNullOrEmpty(data)) return;
                var switchData = JObject.Parse(data);

                if (switchData["system"] == null) return;
                if (switchData["system"]["get_sysinfo"] == null) return;

                if (switchData["system"]["get_sysinfo"]["relay_state"] != null)
                {
                    _supportsRelayState = true;
                    RelayState = switchData["system"]["get_sysinfo"]["relay_state"].ToObject<ushort>();

                    OnNewRelayState?.Invoke(RelayState);
                }
                else
                {
                    _supportsRelayState = false;
                }

                if (switchData["system"]["get_sysinfo"]["brightness"] != null)
                {
                    _supportsBrightness = true;

                    var bri = switchData["system"]["get_sysinfo"]["brightness"].ToObject<int>();

                    Brightness =
                        (ushort) CrestronEnvironment.ScaleWithLimits(bri, 100, 0, ushort.MaxValue, ushort.MinValue);

                    OnNewBrightness?.Invoke(Brightness);
                }
                else
                {
                    _supportsBrightness = false;
                }

                if (switchData["system"]["get_sysinfo"]["light_state"] != null)
                {
                    _supportsRelayState = true;
                    _supportsBrightness = true;
                    _supportsHue = true;
                    _supportsSaturation = true;

                    RelayState = switchData["system"]["get_sysinfo"]["light_state"]["on_off"].ToObject<ushort>();
                    var bri = switchData["system"]["get_sysinfo"]["light_state"]["brightness"].ToObject<int>();
                    var hue = switchData["system"]["get_sysinfo"]["light_state"]["hue"].ToObject<int>();
                    var sat = switchData["system"]["get_sysinfo"]["light_state"]["saturation"].ToObject<int>();

                    Brightness =
                        (ushort)CrestronEnvironment.ScaleWithLimits(bri, 100, 0, ushort.MaxValue, ushort.MinValue);
                    Hue = (ushort) CrestronEnvironment.ScaleWithLimits(hue, 360, 0, ushort.MaxValue, ushort.MinValue);
                    Saturation =
                        (ushort) CrestronEnvironment.ScaleWithLimits(sat, 100, 0, ushort.MaxValue, ushort.MinValue);

                    OnNewRelayState?.Invoke(RelayState);

                    OnNewBrightness?.Invoke(Brightness);

                    OnNewHue?.Invoke(Hue);

                    OnNewSaturation?.Invoke(Saturation);
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

                    OnNewChildrenData?.Invoke(new KasaDeviceChildren(_children.ToArray()));
                }
            }
            catch (SocketException se)
            {
                KasaSystem.KasaLogger.LogException(se);
            }
            catch (HttpsException he)
            {
                KasaSystem.KasaLogger.LogException(he);
            }
            catch (Exception ex)
            {
                KasaSystem.KasaLogger.LogException(ex);
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

                var headers = new Dictionary<string, string>()
                {
                    { "Content-Type", "application/json" }
                };

                string powerBody;

                if (!_supportsHue && !_supportsSaturation)
                    powerBody = "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + _device.DeviceId +
                                "\",\"requestData\":\"{\\\"system\\\":{\\\"set_relay_state\\\":{\\\"state\\\":1}}}\"}}";
                else
                    powerBody = "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + _device.DeviceId +
                                  "\",\"requestData\":\"{\\\"smartlife.iot.smartbulb.lightingservice\\\":{\\\"transition_light_state\\\":{\\\"on_off\\\":1}}}\"}}";

                var response = KasaSystem.Client.SendRequest($"{KasaSystem.BaseUrl}?token={KasaSystem.Token}", Crestron.SimplSharp.Net.AuthMethod.NONE, RequestType.Post, headers, string.Empty, string.Empty, powerBody, KasaSystem.KasaLogger);

                if (response == null) return;
                if (response.Status != 200) return;
                if (response.Content.Length <= 0) return;

                var body = JObject.Parse(response.Content);

                if (body["error_code"] == null) return;
                if (Convert.ToInt16(body["error_code"].ToString()) != 0)
                {
                    if (body["msg"] == null) return;
                    if (body["msg"].ToObject<string>() == "API rate limit exceeded")
                    {
                        KasaSystem.GetSystem();
                    }

                    return;
                }

                RelayState = 1;

                OnNewRelayState?.Invoke(RelayState);
            }
            catch (SocketException se)
            {
                KasaSystem.KasaLogger.LogException(se);
            }
            catch (HttpsException he)
            {
                KasaSystem.KasaLogger.LogException(he);
            }
            catch (Exception ex)
            {
                KasaSystem.KasaLogger.LogException(ex);
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

                var headers = new Dictionary<string, string>()
                {
                    { "Content-Type", "application/json" }
                };

                string powerBody;

                if (!_supportsHue && !_supportsSaturation)
                    powerBody = "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + _device.DeviceId +
                                "\",\"requestData\":\"{\\\"system\\\":{\\\"set_relay_state\\\":{\\\"state\\\":0}}}\"}}";
                else
                    powerBody = "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + _device.DeviceId +
                                  "\",\"requestData\":\"{\\\"smartlife.iot.smartbulb.lightingservice\\\":{\\\"transition_light_state\\\":{\\\"on_off\\\":0}}}\"}}";

                var response = KasaSystem.Client.SendRequest($"{KasaSystem.BaseUrl}?token={KasaSystem.Token}", Crestron.SimplSharp.Net.AuthMethod.NONE, RequestType.Post, headers, string.Empty, string.Empty, powerBody, KasaSystem.KasaLogger);

                if (response == null) return;
                if (response.Status != 200) return;
                if (response.Content.Length <= 0) return;

                var body = JObject.Parse(response.Content);

                if (body["error_code"] == null) return;
                if (Convert.ToInt16(body["error_code"].ToString()) != 0)
                {
                    if (body["msg"] == null) return;
                    if (body["msg"].ToObject<string>() == "API rate limit exceeded")
                    {
                        KasaSystem.GetSystem();
                    }

                    return;
                }

                RelayState = 0;

                OnNewRelayState?.Invoke(RelayState);
            }
            catch (SocketException se)
            {
                KasaSystem.KasaLogger.LogException(se);
            }
            catch (HttpsException he)
            {
                KasaSystem.KasaLogger.LogException(he);
            }
            catch (Exception ex)
            {
                KasaSystem.KasaLogger.LogException(ex);
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

                var headers = new Dictionary<string, string>()
                {
                    { "Content-Type", "application/json" }
                };

                var content = "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + _device.DeviceId + "\",\"child\":\"" + _children[index - 1].ID + "\",\"requestData\":\"{\\\"system\\\":{\\\"set_relay_state\\\":{\\\"state\\\":1}}}\"}}";

                var response = KasaSystem.Client.SendRequest($"{KasaSystem.BaseUrl}?token={KasaSystem.Token}", Crestron.SimplSharp.Net.AuthMethod.NONE, RequestType.Post, headers, string.Empty, string.Empty, content, KasaSystem.KasaLogger);

                if (response == null) return;
                if (response.Status != 200) return;
                if (response.Content.Length <= 0) return;

                var body = JObject.Parse(response.Content);

                if (body["error_code"] == null) return;
                if (Convert.ToInt16(body["error_code"].ToString()) != 0)
                {
                    if (body["msg"] == null) return;
                    if (body["msg"].ToObject<string>() == "API rate limit exceeded")
                    {
                        KasaSystem.GetSystem();
                    }

                    return;
                }

                _children[index - 1].State = 1;

                OnNewChildrenData?.Invoke(new KasaDeviceChildren(_children.ToArray()));
            }
            catch (SocketException se)
            {
                KasaSystem.KasaLogger.LogException(se);
            }
            catch (HttpsException he)
            {
                KasaSystem.KasaLogger.LogException(he);
            }
            catch (Exception ex)
            {
                KasaSystem.KasaLogger.LogException(ex);
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

                var headers = new Dictionary<string, string>()
                {
                    { "Content-Type", "application/json" }
                };

                var content = "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + _device.DeviceId + "\",\"child\":\"" + _children[index - 1].ID + "\",\"requestData\":\"{\\\"system\\\":{\\\"set_relay_state\\\":{\\\"state\\\":0}}}\"}}";

                var response = KasaSystem.Client.SendRequest($"{KasaSystem.BaseUrl}?token={KasaSystem.Token}", Crestron.SimplSharp.Net.AuthMethod.NONE, RequestType.Post, headers, string.Empty, string.Empty, content, KasaSystem.KasaLogger);

                if (response == null) return;
                if (response.Status != 200) return;
                if (response.Content.Length <= 0) return;

                var body = JObject.Parse(response.Content);

                if (body["error_code"] == null) return;
                if (Convert.ToInt16(body["error_code"].ToString()) != 0)
                {
                    if (body["msg"] == null) return;
                    if (body["msg"].ToObject<string>() == "API rate limit exceeded")
                    {
                        KasaSystem.GetSystem();
                    }

                    return;
                }

                _children[index - 1].State = 0;

                OnNewChildrenData?.Invoke(new KasaDeviceChildren(_children.ToArray()));
            }
            catch (SocketException se)
            {
                KasaSystem.KasaLogger.LogException(se);
            }
            catch (HttpsException he)
            {
                KasaSystem.KasaLogger.LogException(he);
            }
            catch (Exception ex)
            {
                KasaSystem.KasaLogger.LogException(ex);
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

                    var headers = new Dictionary<string, string>()
                    {
                        { "Content-Type", "application/json" }
                    };

                    string briBody;

                    if (!_supportsHue && !_supportsSaturation)
                        briBody = "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + _device.DeviceId +
                                  "\",\"requestData\":\"{\\\"smartlife.iot.dimmer\\\":{\\\"set_brightness\\\":{\\\"brightness\\\":" +
                                  sBri.ToString() + "}}}\"}}";
                    else
                        briBody = "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + _device.DeviceId +
                                  "\",\"requestData\":\"{\\\"smartlife.iot.smartbulb.lightingservice\\\":{\\\"transition_light_state\\\":{\\\"brightness\\\":" +
                                  sBri.ToString() + "}}}\"}}";

                    
                    var response = KasaSystem.Client.SendRequest($"{KasaSystem.BaseUrl}?token={KasaSystem.Token}", Crestron.SimplSharp.Net.AuthMethod.NONE, RequestType.Post, headers, string.Empty, string.Empty, briBody, KasaSystem.KasaLogger);

                    if (response.Content == null) return;
                    if (response.Status != 200) return;
                    if (response.Content.Length <= 0) return;

                    var body = JObject.Parse(response.Content);

                    if (body["error_code"] == null) return;
                    if (Convert.ToInt16(body["error_code"].ToString()) != 0)
                    {
                        if (body["msg"] == null) return;
                        if (body["msg"].ToObject<string>() == "API rate limit exceeded")
                        {
                            KasaSystem.GetSystem();
                        }

                        return;
                    }

                    Brightness = bri;

                    OnNewBrightness?.Invoke(Brightness);
                }
                else
                {
                    throw new InvalidOperationException("This device does not support brightness");
                }
            }
            catch (SocketException se)
            {
                KasaSystem.KasaLogger.LogException(se);
            }
            catch (HttpsException he)
            {
                KasaSystem.KasaLogger.LogException(he);
            }
            catch (InvalidOperationException ie)
            {
                KasaSystem.KasaLogger.LogException(ie);
            }
            catch (Exception ex)
            {
                KasaSystem.KasaLogger.LogException(ex);
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

                    var headers = new Dictionary<string, string>()
                    {
                        { "Content-Type", "application/json" }
                    };

                    var hueBody = "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + _device.DeviceId +
                                  "\",\"requestData\":\"{\\\"smartlife.iot.smartbulb.lightingservice\\\":{\\\"transition_light_state\\\":{\\\"hue\\\":" +
                                  sHue.ToString() + "}}}\"}}";

                    var response = KasaSystem.Client.SendRequest($"{KasaSystem.BaseUrl}?token={KasaSystem.Token}", Crestron.SimplSharp.Net.AuthMethod.NONE, RequestType.Post, headers, string.Empty, string.Empty, hueBody, KasaSystem.KasaLogger);

                    if (response == null) return;
                    if (response.Content == null) return;
                    if (response.Status != 200) return;
                    if (response.Content.Length <= 0) return;

                    var body = JObject.Parse(response.Content);

                    if (body["error_code"] == null) return;
                    if (Convert.ToInt16(body["error_code"].ToString()) != 0)
                    {
                        if (body["msg"] == null) return;
                        if (body["msg"].ToObject<string>() == "API rate limit exceeded")
                        {
                            KasaSystem.GetSystem();
                        }

                        return;
                    }

                    Hue = hue;

                    OnNewHue?.Invoke(Hue);
                }
                else
                {
                    throw new InvalidOperationException("This device does not support hue");
                }
            }
            catch (SocketException se)
            {
                KasaSystem.KasaLogger.LogException(se);
            }
            catch (HttpsException he)
            {
                KasaSystem.KasaLogger.LogException(he);
            }
            catch (InvalidOperationException ie)
            {
                KasaSystem.KasaLogger.LogException(ie);
            }
            catch (Exception ex)
            {
                KasaSystem.KasaLogger.LogException(ex);
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

                    var headers = new Dictionary<string, string>()
                    {
                        { "Content-Type", "application/json" }
                    };

                    var satBody = "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + _device.DeviceId +
                                  "\",\"requestData\":\"{\\\"smartlife.iot.smartbulb.lightingservice\\\":{\\\"transition_light_state\\\":{\\\"saturation\\\":" +
                                  sSat.ToString() + "}}}\"}}";



                    var response = KasaSystem.Client.SendRequest($"{KasaSystem.BaseUrl}?token={KasaSystem.Token}", Crestron.SimplSharp.Net.AuthMethod.NONE, RequestType.Post, headers, string.Empty, string.Empty, satBody, KasaSystem.KasaLogger);

                    if (response == null) return;
                    if (response.Content == null) return;
                    if (response.Status != 200) return;
                    if (response.Content.Length <= 0) return;

                    var body = JObject.Parse(response.Content);

                    if (body["error_code"] == null) return;
                    if (Convert.ToInt16(body["error_code"].ToString()) != 0)
                    {
                        if (body["msg"] == null) return;
                        if (body["msg"].ToObject<string>() == "API rate limit exceeded")
                        {
                            KasaSystem.GetSystem();
                        }

                        return;
                    }

                    Saturation = sat;

                    OnNewSaturation?.Invoke(Saturation);
                }
                else
                {
                    throw new InvalidOperationException("This device does not support saturation");
                }
            }
            catch (SocketException se)
            {
                KasaSystem.KasaLogger.LogException(se);
            }
            catch (HttpsException he)
            {
                KasaSystem.KasaLogger.LogException(he);
            }
            catch (InvalidOperationException ie)
            {
                KasaSystem.KasaLogger.LogException(ie);
            }
            catch (Exception ex)
            {
                KasaSystem.KasaLogger.LogException(ex);
            }
        }
    }
}