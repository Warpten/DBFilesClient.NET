using DBFilesClient2.NET.Attributes;

namespace DBFilesClient.NET.UnitTests.WDBC.Structures
{
    [DBFileName("FactionTemplate")]
    public sealed class FactionTemplateEntry
    {
        [Index]
        public int ID { get; set; }
        public uint Faction { get; set; }
        public uint Flags { get; set; }
        public uint FactionGroup { get; set; }
        public uint FriendGroup { get; set; }
        public uint EnemyGroup { get; set; }
        [StoragePresence(StoragePresence.Include, SizeConst = 4)]
        public uint[] Enemies { get; set; }
        [StoragePresence(StoragePresence.Include, SizeConst = 4)]
        public uint[] Friends { get; set; }
    }
}
