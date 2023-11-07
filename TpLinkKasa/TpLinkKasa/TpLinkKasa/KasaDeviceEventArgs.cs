using System;

namespace TpLinkKasa
{
    internal class KasaDeviceEventArgs : EventArgs
    {
        public eKasaDeviceEventId Id;
        public int Value;

        public KasaDeviceEventArgs(eKasaDeviceEventId id, int value)
        {
            this.Id = id;
            this.Value = value;
        }
    }

    internal enum eKasaDeviceEventId
    {
        GetNow = 1,
        RelayState = 2,
        Brightness = 3
    }
}