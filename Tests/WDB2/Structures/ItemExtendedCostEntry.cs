using DBFilesClient2.NET.Attributes;

namespace DBFilesClient.NET.UnitTests.WDB2.Structures
{
    [DBFileName("ItemExtendedCost")]
    public sealed class ItemExtendedCostEntry
    {
        [Index]
        public int ID { get; set; }
        public uint RequiredHonorPoints { get; set; }
        public uint RequiredArenaPoints { get; set; }
        public uint RequiredArenaSlot { get; set; }

        [StoragePresence(StoragePresence.Include, SizeConst = 5)]
        public uint[] RequiredItem { get; set; }

        [StoragePresence(StoragePresence.Include, SizeConst = 5)]
        public uint[] RequiredItemCount { get; set; }

        public uint RequiredPersonalArenaRating { get; set; }
        public uint ItemPurchaseGroup { get; set; }

        [StoragePresence(StoragePresence.Include, SizeConst = 5)]
        public uint[] RequiredCurrency { get; set; }

        [StoragePresence(StoragePresence.Include, SizeConst = 5)]
        public uint[] RequiredCurrencyCount { get; set; }

        public uint Unk1 { get; set; }
        public uint Unk2 { get; set; }
        public uint Unk3 { get; set; }
        public uint Unk4 { get; set; }
        public uint Unk5 { get; set; }
    }
}
