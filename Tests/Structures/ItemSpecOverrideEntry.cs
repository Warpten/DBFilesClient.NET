using DBFilesClient.NET;

namespace Tests.Structures
{
    [DBFileName("ItemSpecOverride")]
    public sealed class ItemSpecOverrideEntry
    {
        public uint ItemID;
        public ushort SpecID;
    }
}