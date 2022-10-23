using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Newtonsoft.Json;

namespace TpLinkKasa
{
    public class KasaDeviceChild
    {
        [JsonProperty("id")]
        public string ID { get; set; }

        [JsonProperty("state")]
        public ushort State { get; set;}

        [JsonProperty("alias")]
        public string Alias { get; set;}
    }
}