using System.Linq;

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