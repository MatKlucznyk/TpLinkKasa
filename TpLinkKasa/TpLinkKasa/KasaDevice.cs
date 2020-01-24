using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

namespace TpLinkKasa
{
    public class KasaDevice
    {
        public string deviceId { get; set; }
        public string alias { get; set; }
        public string deviceModel { get; set; }
    }
}