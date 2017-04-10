namespace Tests.Structures
{
    [DBFile("ItemModifiedAppearance")]
    public class ItemModifiedAppearanceEntry
    {
        public uint ItemID;
        public ushort AppearanceID;
        public byte AppearanceModID;
        public byte Index;
        public byte SourceType;
        public uint ID;
    }
}