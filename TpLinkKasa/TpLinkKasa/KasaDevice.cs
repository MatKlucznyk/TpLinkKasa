using Crestron.SimplSharp;
using Crestron.SimplSharp.Net.Https;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace TpLinkKasa
{
    /// <summary>
    /// Represents a TP-Link Kasa smart device, providing methods and properties to control and monitor its state, such
    /// as power, brightness, hue, saturation, and child device management.
    /// </summary>
    /// <remarks>Use this class to interact with a Kasa device, including turning it on or off, adjusting
    /// brightness and color settings, and managing child devices if supported. The class exposes events for state
    /// changes, allowing clients to respond to updates in real time. Before performing operations, ensure the device is
    /// properly initialized and subscribed to receive updates. Not all devices support all features; check the relevant
    /// support properties before invoking feature-specific methods.</remarks>
    public class KasaDevice
    {
        private readonly object _syncLock = new object();
        private bool _isInitialized;
        private string _alias;
        private KasaDeviceInfo _device;
        private bool _supportsBrightness;
        private bool _supportsRelayState;
        private bool _supportsHue;
        private bool _supportsSaturation;
        private bool _hasChildren;
        private List<KasaDeviceChild> _children;
        private volatile bool _isSubscribed;

        /// <summary>
        /// Gets the current state of the relay as an unsigned 16-bit integer.
        /// </summary>
        public ushort RelayState { get; private set; }

        /// <summary>
        /// Gets the current brightness level.
        /// </summary>
        public ushort Brightness { get; private set; }

        /// <summary>
        /// Gets the hue value represented as an unsigned 16-bit integer.
        /// </summary>
        public ushort Hue { get; private set; }

        /// <summary>
        /// Gets the saturation level for the current instance.
        /// </summary>
        public ushort Saturation { get; private set; }
        
        /// <summary>
        /// Gets a value indicating whether brightness adjustment is supported.
        /// </summary>
        /// <remarks>A value of 1 indicates that brightness adjustment is supported; a value of 0
        /// indicates it is not.</remarks>
        public ushort SupportsBrightness { get { return _supportsBrightness ? (ushort)1 : (ushort)0; } }

        /// <summary>
        /// Gets a value indicating whether relay state is supported.
        /// </summary>
        public ushort SupportsRelayState { get { return _supportsRelayState ? (ushort)1 : (ushort)0; }}

        /// <summary>
        /// Gets a value indicating whether hue adjustment is supported.
        /// </summary>
        public ushort SupportsHue { get { return _supportsHue ? (ushort)1 : (ushort)0; }}

        /// <summary>
        /// Gets a value indicating whether saturation is supported by the current instance.
        /// </summary>
        public ushort SupportsSaturation { get { return _supportsSaturation ? (ushort)1 : (ushort)0; }}

        /// <summary>
        /// Gets a value indicating whether the current element has child elements.
        /// </summary>
        public ushort HasChildren { get { return _hasChildren ? (ushort)1 : (ushort)0; }}

        /// <summary>
        /// Gets the total number of child elements associated with this instance.
        /// </summary>
        public ushort TotalChildren { get; private set; }

        /// <summary>
        /// Represents a method that handles changes to the relay state.
        /// </summary>
        /// <param name="state">The new state of the relay, represented as an unsigned 16-bit integer.</param>
        public delegate void NewRelayState(ushort state);

        /// <summary>
        /// Represents a method that handles changes to brightness values.
        /// </summary>
        /// <param name="bri">The new brightness value. Valid values are typically in the range supported by the application.</param>
        public delegate void NewBrightness(ushort bri);

        /// <summary>
        /// Represents a method that handles an event when a new hue value is set.
        /// </summary>
        /// <param name="hue">The hue value, specified as an unsigned 16-bit integer. Represents the new hue to be processed or applied.</param>
        public delegate void NewHue(ushort hue);

        /// <summary>
        /// Represents a method that handles an event when the saturation value changes.
        /// </summary>
        /// <param name="sat">The new saturation value. Typically ranges from 0 to 65535, where the valid range and interpretation depend
        /// on the context in which the delegate is used.</param>
        public delegate void NewSaturation(ushort sat);

        /// <summary>
        /// Represents a method that handles new data for Kasa device children.
        /// </summary>
        /// <param name="children">The KasaDeviceChildren instance containing the new data to be processed.</param>
        public delegate void NewChildrenData(KasaDeviceChildren children);

        /// <summary>
        /// Gets or sets the delegate that is invoked when a new relay state is available.
        /// </summary>
        /// <remarks>Assign a method to this property to handle updates when the relay state changes. This
        /// is typically used to react to state changes in relay-based communication scenarios.</remarks>
        public NewRelayState OnNewRelayState { get; set; }

        /// <summary>
        /// Gets or sets the handler that is invoked when a new brightness value is available.
        /// </summary>
        public NewBrightness OnNewBrightness { get; set; }

        /// <summary>
        /// Gets or sets the handler that is invoked when a new hue is detected.
        /// </summary>
        public NewHue OnNewHue { get; set; }

        /// <summary>
        /// Gets or sets the handler that is invoked when a new saturation value is available.
        /// </summary>
        public NewSaturation OnNewSaturation { get; set; }

        /// <summary>
        /// Gets or sets the data representing new child elements to be processed or displayed.
        /// </summary>
        public NewChildrenData OnNewChildrenData { get; set; }


        /// <summary>
        /// Initializes the device with the specified alias and subscribes to device events if registration succeeds.
        /// </summary>
        /// <remarks>If the device is successfully registered, event subscriptions are established to
        /// receive device updates. Calling this method multiple times with different aliases will update the
        /// registration and event subscription accordingly.</remarks>
        /// <param name="alias">The unique alias used to identify and register the device. Cannot be null or empty.</param>
        public void Initialize(string alias)
        {
            if(_isInitialized) return;

            lock(_syncLock)
            {
                if (_isInitialized) return;
                if (string.IsNullOrEmpty(alias)) throw new ArgumentException("Alias cannot be null or empty.", nameof(alias));

                _alias = alias;

                if (KasaSystem.TryRegisterDevice(alias, out var subscriptionEvent))
                {
                    subscriptionEvent.OnNewEvent += new EventHandler<KasaDeviceEventArgs>(KasaDevice_OnNewEvent);
                    _isSubscribed = true;
                }
                _isInitialized = true;
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

        private bool EnsureInitialized()
        {
            if (!_isInitialized)
            {
                KasaSystem.KasaLogger.LogNotice($"Device with alias {_alias} is not initialized. Please initialize the device before performing this operation.");
                return false;
            }
            return true;
        }

        private string SendRequest(string content)
        {
            var token = KasaSystem.Token;
            if (string.IsNullOrEmpty(token)) return null;
            var headers = new Dictionary<string, string>()
            {
                { "Content-Type", "application/json" }
            };
            var response = KasaSystem.Client.SendRequest($"{KasaSystem.BaseUrl}?token={token}", Crestron.SimplSharp.Net.AuthMethod.NONE, RequestType.Post, headers, string.Empty, string.Empty, content, KasaSystem.KasaLogger);
            if (response == null) return null;
            if (response.Status != 200) return null;
            if (string.IsNullOrEmpty(response.Content)) return null;
            return response.Content;
        }

        private bool EnsureSubscribed()
        {
            if (!_isSubscribed)
            {
                KasaSystem.KasaLogger.LogNotice($"Device with alias {_alias} is not subscribed to events. Please subscribe to receive updates and control the device.");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Retrieves the current device state and updates related properties such as relay state, brightness, hue,
        /// saturation, and child device information.
        /// </summary>
        /// <remarks>This method communicates with the device to obtain its latest status. It updates
        /// properties and invokes related events if new data is available. If the API rate limit is exceeded, the
        /// method attempts to refresh the system state. This method does not throw exceptions; all exceptions are
        /// logged internally.</remarks>
        public void GetDevice()
        {
            try
            {
                if (!EnsureInitialized()) return;
                if (!EnsureSubscribed()) return;

                var content = "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + _device.DeviceId + "\",\"requestData\":\"{\\\"system\\\":{\\\"get_sysinfo\\\":{}}}\"}}";

                var response = SendRequest(content);

                if (response == null) return;;

                var body = JObject.Parse(response);

                if (body["error_code"] != null && body["msg"] != null)
                {
                    if (body["msg"].ToObject<string>() == "API rate limit exceeded")
                    {
                        KasaSystem.GetSystem();
                        return;
                    }
                }

                var data = body["result"]?["responseData"]?.ToString().Replace("\\\"", "\"");
                if(string.IsNullOrEmpty(data)) return;
                var switchData = JObject.Parse(data);

                if (switchData["system"] == null || switchData["system"]["get_sysinfo"] == null) return;

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

        /// <summary>
        /// Turns the device on by setting its relay state to enabled.
        /// </summary>
        /// <remarks>This method has no effect if the device does not support relay state or if the device
        /// is not currently subscribed. If the device supports color features, a different command is used to power on
        /// the device. The method handles API rate limiting and logs network-related exceptions internally. No
        /// exception is thrown to the caller.</remarks>
        public void PowerOn()
        {
            try
            {
                if (!EnsureInitialized()) return;
                if (!EnsureSubscribed()) return;
                if (!_supportsRelayState) return;

                string powerBody;

                if (!_supportsHue && !_supportsSaturation)
                    powerBody = "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + _device.DeviceId +
                                "\",\"requestData\":\"{\\\"system\\\":{\\\"set_relay_state\\\":{\\\"state\\\":1}}}\"}}";
                else
                    powerBody = "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + _device.DeviceId +
                                  "\",\"requestData\":\"{\\\"smartlife.iot.smartbulb.lightingservice\\\":{\\\"transition_light_state\\\":{\\\"on_off\\\":1}}}\"}}";

                var response = SendRequest(powerBody);

                if (response == null) return;

                var body = JObject.Parse(response);

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

        /// <summary>
        /// Turns off the device by setting its relay state to off.
        /// </summary>
        /// <remarks>This method has no effect if the device does not support relay state or if the device
        /// is not currently subscribed. If the device is already powered off, calling this method does nothing.
        /// Exceptions encountered during the operation are logged but not propagated.</remarks>
        public void PowerOff()
        {
            try
            {
                if (!EnsureInitialized()) return;
                if (!EnsureSubscribed()) return;
                if (!_supportsRelayState) return;

                string powerBody;

                if (!_supportsHue && !_supportsSaturation)
                    powerBody = "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + _device.DeviceId +
                                "\",\"requestData\":\"{\\\"system\\\":{\\\"set_relay_state\\\":{\\\"state\\\":0}}}\"}}";
                else
                    powerBody = "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + _device.DeviceId +
                                  "\",\"requestData\":\"{\\\"smartlife.iot.smartbulb.lightingservice\\\":{\\\"transition_light_state\\\":{\\\"on_off\\\":0}}}\"}}";

                var response = SendRequest(powerBody);

                if (response == null) return;

                var body = JObject.Parse(response);

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

        /// <summary>
        /// Powers on the specified child device by index.
        /// </summary>
        /// <remarks>If the specified index is out of range, the child device does not exist, or the
        /// device is not properly subscribed, the method performs no action. This method logs exceptions that occur
        /// during the operation.</remarks>
        /// <param name="index">The one-based index of the child device to power on. Must be greater than or equal to 1 and less than or
        /// equal to the total number of children.</param>
        public void PowerOnChild(ushort index)
        {
            try
            {
                if (!EnsureInitialized()) return;
                if (!EnsureSubscribed()) return;
                if (!_hasChildren) return;
                if (TotalChildren < index) return;

                KasaDeviceChild child;

                lock(_syncLock)
                {
                    child = _children[index - 1];
                }
                if (child == null) return;
                var content = "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + _device.DeviceId + "\",\"child\":\"" + child.ID + "\",\"requestData\":\"{\\\"system\\\":{\\\"set_relay_state\\\":{\\\"state\\\":1}}}\"}}";

                var response = SendRequest(content);

                if (response == null) return;

                var body = JObject.Parse(response);

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

                child.State = 1;

                KasaDeviceChild[] childrenArray;
                lock (_syncLock)
                {
                    _children[index - 1] = child;
                    childrenArray = _children.ToArray();
                }

                OnNewChildrenData?.Invoke(new KasaDeviceChildren(childrenArray));
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

        /// <summary>
        /// Turns off the specified child device by setting its relay state to off.
        /// </summary>
        /// <remarks>If the specified child device does not exist, is not available, or the operation
        /// cannot be performed, the method completes without effect. The method logs exceptions internally and does not
        /// throw them to the caller.</remarks>
        /// <param name="index">The one-based index of the child device to power off. Must be greater than or equal to 1 and less than or
        /// equal to the total number of children.</param>
        public void PowerOffChild(ushort index)
        {
            try
            {
                if (!EnsureInitialized()) return;
                if (!EnsureSubscribed()) return;
                if (!_hasChildren) return;
                if (TotalChildren < index) return;

                KasaDeviceChild child;
                lock (_syncLock)
                {
                    if (_children[index - 1] == null) return;
                    child = _children[index - 1];
                }
                

                var content = "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + _device.DeviceId + "\",\"child\":\"" + child.ID + "\",\"requestData\":\"{\\\"system\\\":{\\\"set_relay_state\\\":{\\\"state\\\":0}}}\"}}";

                var response = SendRequest(content);

                if (response == null) return;

                var body = JObject.Parse(response);

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

                child.State = 0;

                KasaDeviceChild[] childrenArray;
                lock(_syncLock)
                {
                    _children[index - 1] = child;
                    childrenArray = _children.ToArray();
                }

                OnNewChildrenData?.Invoke(new KasaDeviceChildren(childrenArray));
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

        /// <summary>
        /// Sets the brightness level of the device.
        /// </summary>
        /// <remarks>This method has no effect if the device does not support brightness control. If the
        /// device is not subscribed or a communication error occurs, the brightness may not be updated. The method will
        /// throw an exception if brightness is not supported by the device.</remarks>
        /// <param name="bri">The desired brightness value to set. Must be between 0 and 100, where 0 represents the minimum brightness
        /// and 100 represents the maximum.</param>
        public void SetBrightness(ushort bri)
        {
            try
            {
                if (!EnsureInitialized()) return;
                if (!EnsureSubscribed()) return;
                if (_supportsBrightness) throw new InvalidOperationException("This device does not support brightness"); 

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


                var response = SendRequest(briBody);

                if (response == null) return;

                var body = JObject.Parse(response);

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

        /// <summary>
        /// Sets the hue value of the device, adjusting its color if supported.
        /// </summary>
        /// <remarks>This method has no effect if the device does not support hue adjustment. If the
        /// device is not subscribed or the operation fails, the hue value will not be updated. The method may log
        /// exceptions internally but does not throw them to the caller.</remarks>
        /// <param name="hue">The hue value to set, in the range 0 to 65535. Values outside this range may be scaled to the device's
        /// supported range.</param>
        public void SetHue(ushort hue)
        {
            try
            {
                if (!EnsureInitialized()) return;
                if (!EnsureSubscribed()) return;
                if (!_supportsHue) throw new InvalidOperationException("This device does not support hue"); 

                var sHue = CrestronEnvironment.ScaleWithLimits(hue, ushort.MaxValue, ushort.MinValue, 360, 0);

                var hueBody = "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + _device.DeviceId +
                              "\",\"requestData\":\"{\\\"smartlife.iot.smartbulb.lightingservice\\\":{\\\"transition_light_state\\\":{\\\"hue\\\":" +
                              sHue.ToString() + "}}}\"}}";

                var response = SendRequest(hueBody);

                if (response == null) return;

                var body = JObject.Parse(response);

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

        /// <summary>
        /// Sets the saturation level of the device.
        /// </summary>
        /// <remarks>This method has no effect if the device does not support saturation adjustment. If
        /// the device is not subscribed or an error occurs during the request, the saturation value will not be
        /// updated.</remarks>
        /// <param name="sat">The desired saturation value. Must be between 0 and 100, where 0 represents no saturation and 100 represents
        /// full saturation.</param>
        public void SetSaturation(ushort sat)
        {
            try
            {
                if (!EnsureInitialized()) return;
                if (!EnsureSubscribed()) return;
                if (_supportsSaturation) throw new InvalidOperationException("This device does not support saturation");             

                var sSat = CrestronEnvironment.ScaleWithLimits(sat, ushort.MaxValue, ushort.MinValue, 100, 0);

                var satBody = "{\"method\":\"passthrough\",\"params\":{\"deviceId\":\"" + _device.DeviceId +
                              "\",\"requestData\":\"{\\\"smartlife.iot.smartbulb.lightingservice\\\":{\\\"transition_light_state\\\":{\\\"saturation\\\":" +
                              sSat.ToString() + "}}}\"}}";



                var response = SendRequest(satBody);

                if (response == null) return;

                var body = JObject.Parse(response);

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