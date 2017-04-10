using System.Runtime.InteropServices;

namespace Tests.Structures
{
    [DBFile("TaxiPathNode")]
    public class TaxiPathNodeEntry
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public float[] Loc;
        public uint Delay;
        public ushort PathID;
        public ushort MapID;
        public ushort ArrivalEventID;
        public ushort DepartureEventID;
        public byte NodeIndex;
        public byte Flags;
        public uint ID;
    }
}