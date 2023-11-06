using Newtonsoft.Json;

namespace TpLinkKasa
{
    public class KasaDeviceInfo
    {
        [JsonProperty("deviceId")]
        public string DeviceId { get; set; }
        
        [JsonProperty("alias")]
        public string Alias { get; set; }

        [JsonProperty("deviceModel")]
        public string DeviceModel { get; set; }
    }
}