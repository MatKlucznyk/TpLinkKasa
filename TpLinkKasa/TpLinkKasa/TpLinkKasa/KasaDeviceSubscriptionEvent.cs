using System;
using System.Linq;

namespace TpLinkKasa
{
    internal class KasaDeviceSubscriptionEvent
    {
        private event EventHandler<KasaDeviceEventArgs> onNewEvent = delegate { };

        public event EventHandler<KasaDeviceEventArgs> OnNewEvent
        {
            add
            {
                if (!onNewEvent.GetInvocationList().Contains(value))
                {
                    onNewEvent += value;
                }
            }
            remove
            {
                onNewEvent -= value;
            }
        }

        internal void Fire(KasaDeviceEventArgs e)
        {
            onNewEvent(null, e);
        }
    }
}