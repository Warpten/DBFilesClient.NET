using DBFilesClient2.NET.Attributes;

namespace DBFilesClient.NET.UnitTests.WDB2.Structures
{
    [DBFileName("Map")]
    public sealed class MapEntry
    {
        [Index]
        public int ID { get; set; }
        public string InternalName { get; set; }
        public uint InstanceType { get; set; } // MapType
        public uint Flags { get; set; }
        public uint MapType { get; set; } // 18179 name
        public uint IsPvP { get; set; }
        public string MapName { get; set; }
        public uint AreaTableId { get; set; }
        public string HordeIntro { get; set; }
        public string AllianceIntro { get; set; }
        public int LoadingScreenId { get; set; }
        public float BattlefieldMapIconScale { get; set; }
        public int CorpseMapId { get; set; }
        [StoragePresence(StoragePresence.Include, SizeConst = 2)]
        public float[] CorpseEntrance { get; set; }
        public int TimeOfDayOverride { get; set; }
        public uint Expansion { get; set; }
        public uint ExpireTime { get; set; }
        public uint MaxPlayers { get; set; }
        public int RootPhaseMap { get; set; }
    }
}
