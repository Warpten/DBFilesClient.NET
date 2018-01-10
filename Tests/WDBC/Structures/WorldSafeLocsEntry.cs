using DBFilesClient2.NET.Attributes;

namespace DBFilesClient.NET.UnitTests.WDBC.Structures
{
    [DBFileName("WorldSafeLocs")]
    public sealed class WorldSafeLocsEntry
    {
        [Index]
        public int ID { get; set; }
        public uint MapID { get; set; }
        [StoragePresence(StoragePresence.Include, SizeConst = 3)]
        public float[] Position { get; set; }
        [StoragePresence(StoragePresence.Include, SizeConst = 16)]
        public string[] Name { get; set; }
        public int NameFlags { get; set; }
    }
}
