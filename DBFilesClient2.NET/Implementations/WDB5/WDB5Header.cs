
namespace DBFilesClient2.NET.Implementations.WDB5
{
    internal class WDB5Header : IStorageHeader
    {
        public int RecordCount { get; set; }
        public int FieldCount { get; set; }
        public int RecordSize { get; set; }

        public int MinIndex { get; set; }
        public int MaxIndex { get; set; }

        public int IndexColumn { get; set; }

        public BlockInfo MemberMetadata { get; } = new BlockInfo();
        public BlockInfo RecordTable { get; } = new BlockInfo();
        public BlockInfo StringTable { get; } = new BlockInfo();
        public BlockInfo OffsetMap { get; } = new BlockInfo();
        public BlockInfo IndexTable { get; } = new BlockInfo();
        public BlockInfo CopyTable { get; } = new BlockInfo();
        public BlockInfo CommonTable { get; } = new BlockInfo();

        public BlockInfo PalletTable { get; } = null;
    }
}
