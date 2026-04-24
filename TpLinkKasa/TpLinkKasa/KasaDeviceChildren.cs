using System.Linq;

namespace TpLinkKasa
{
    /// <summary>
    /// Represents a collection of child devices associated with a Kasa device.
    /// </summary>
    public class KasaDeviceChildren
    {
        private KasaDeviceChild[] _children;

        /// <summary>
        /// Gets the collection of child devices associated with this device.
        /// </summary>
        public KasaDeviceChild[] Children { get { return _children;} }

        /// <summary>
        /// Gets the number of child elements contained in the collection.
        /// </summary>
        public ushort Count { get { return (ushort)_children.Count(); } }

        /// <summary>
        /// Initializes a new instance of the KasaDeviceChildren class.
        /// </summary>
        public KasaDeviceChildren()
        {
        }

        /// <summary>
        /// Initializes a new instance of the KasaDeviceChildren class with the specified collection of child devices.
        /// </summary>
        /// <param name="children">An array of KasaDeviceChild objects representing the child devices to associate with this instance. Cannot
        /// be null.</param>
        public KasaDeviceChildren(KasaDeviceChild[] children)
        {
            _children = children;
        }
    }
}