using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBFilesClient2.NET.Implementations
{
    internal interface IStorageHeader
    {
        int RecordCount { get; set; }
        int FieldCount { get; set; }
        int RecordSize { get; set; }

        int MinIndex { get; set; }
        int MaxIndex { get; set; }

        int IndexColumn { get; set; }

        BlockInfo StringTable { get; }
        BlockInfo RecordTable { get; }

        // Needed by the serializer.
        BlockInfo IndexTable { get; }
        BlockInfo PalletTable { get; }
        BlockInfo CommonTable { get; }

        // BlockInfo MemberMetadata { get; }
        // BlockInfo ExtendedMemberMetadata { get; }
        // BlockInfo CopyTable { get; }
        // BlockInfo OffsetMap { get; }
        // BlockInfo VariableRecordData { get; }
        // BlockInfo PalletTable { get; }
    }

    internal class BlockInfo
    {
        private bool _exists = false;

        public bool Exists {
            get => _exists && Size != 0;
            set => _exists = value;
        }

        public long StartOffset { get; set; } = 0;
        public long EndOffset => StartOffset + (Exists ? Size : 0);

        public int Size { get; set; } = 0;

        public bool SeekTo(Stream dataStream)
        {
            if (!Exists || !dataStream.CanSeek)
                return false;

            dataStream.Position = StartOffset;
            return true;
        }
    }
}
