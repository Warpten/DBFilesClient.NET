using DBFilesClient2.NET.Attributes;

namespace DBFilesClient.NET.UnitTests.WDC1.Structures
{
    [DBFileName("Map")]
    public sealed class MapEntry
    {
        [Index]
        public int ID { get; set; } // 0
        public string Directory { get; set; } // 1
        public string Name { get; set; } // 2
        public string HordeDescription { get; set; } // 3
        public string AllianceDescription { get; set; } // 4
        public string PvpObjective { get; set; } // 5
        public string PvpDescription { get; set; } // 6
        [StoragePresence(StoragePresence.Include, SizeConst = 2)]
        public int[] Flags { get; set; }
        public float MinimapIconScale { get; set; }
        [StoragePresence(StoragePresence.Include, SizeConst = 2)]
        public float[] CorpseCoordinates { get; set; } // 8
        public ushort AreaTableID { get; set; } // 9
        public short LoadingScreenID { get; set; } // 10
        public short CorpseMapID { get; set; } // 11 
        public short TimeOfDayOverride { get; set; } // 12
        public short ParentMapID { get; set; } // 13
        public short CosmeticParentMapID { get; set; } // 14
        public short WindSettingsID { get; set; } // 15 
        public byte InstanceType { get; set; } // 16
        public byte MapType { get; set; } // 17
        public byte ExpansionID { get; set; } // 18
        public byte MaxPlayers { get; set; } // 19
        public byte TimeOffset { get; set; } // 20
    }
}
