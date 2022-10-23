using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

namespace TpLinkKasa
{
    public class KasaDeviceChildren
    {
        private KasaDeviceChild[] _children;

        public KasaDeviceChild[] Children { get { return _children;} }
        public ushort Count { get { return (ushort)_children.Count(); } }

        public KasaDeviceChildren()
        {
        }

        public KasaDeviceChildren(KasaDeviceChild[] children)
        {
            _children = children;
        }
    }
}