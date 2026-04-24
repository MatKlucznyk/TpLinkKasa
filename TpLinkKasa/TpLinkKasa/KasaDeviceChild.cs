using Newtonsoft.Json;

namespace TpLinkKasa
{
    /// <summary>
    /// Represents a child device associated with a Kasa smart device, such as an individual outlet or switch within a
    /// multi-outlet device.
    /// </summary>
    /// <remarks>This class provides properties to access and modify the identity, state, and alias of a child
    /// device. It is typically used when interacting with Kasa devices that support multiple controllable
    /// components.</remarks>
    public class KasaDeviceChild
    {
        /// <summary>
        /// Gets or sets the unique identifier for the entity.
        /// </summary>
        [JsonProperty("id")]
        public string ID { get; set; }

        /// <summary>
        /// Gets or sets the current state code represented as an unsigned 16-bit integer.
        /// </summary>
        [JsonProperty("state")]
        public ushort State { get; set;}

        /// <summary>
        /// Gets or sets the alternate name or identifier associated with the object.
        /// </summary>
        [JsonProperty("alias")]
        public string Alias { get; set;}
    }
}