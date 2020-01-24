using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

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