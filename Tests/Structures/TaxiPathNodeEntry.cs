using DBFilesClient.NET;

namespace Tests.Structures
{
    [DBFileName("TaxiPathNode")]
    public sealed class TaxiPathNodeEntry
    {
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