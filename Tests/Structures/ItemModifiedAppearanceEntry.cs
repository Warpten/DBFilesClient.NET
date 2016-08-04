using DBFilesClient.NET;

namespace Tests.Structures
{
    [DBFileName("ItemModifiedAppearance")]
    public sealed class ItemModifiedAppearanceEntry
    {
        public uint ItemID;
        public ushort AppearanceID;
        public byte AppearanceModID;
        public byte Index;
        public byte SourceType;
        public uint ID;
    }
}