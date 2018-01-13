using DBFilesClient2.NET.Exceptions;
using DBFilesClient2.NET.Internals;
using System;
using System.IO;
using System.Linq;

namespace DBFilesClient2.NET.Implementations.WDBC
{
    internal class WDBCReader<TKey, TValue> : BaseStorageReader<TKey, TValue, WDBCHeader>
        where TValue : class, new()
        where TKey : struct
    {
        public WDBCReader(Stream baseStream, StorageOptions options) : base(baseStream, options)
        {
        }

        public override bool ParseHeader()
        {
            Header.RecordCount = ReadInt32();
            if (Header.RecordCount == 0)
                return false;

            Header.FieldCount      = ReadInt32();
            Header.RecordSize      = ReadInt32();

            Header.StringTable.Exists = true;
            Header.StringTable.Size   = ReadInt32();

            TypeMembers = new FieldMetadata[Members.Length];
            for (var i = 0; i < Members.Length; ++i)
            {
                TypeMembers[i] = new FieldMetadata();
                TypeMembers[i].ByteSize = SizeCache.GetSizeOf(Members[i].GetMemberType());
                TypeMembers[i].MemberInfo = Members[i];

                if (i > 0)
                    TypeMembers[i].OffsetInRecord = (uint)(TypeMembers[i - 1].OffsetInRecord + TypeMembers[i - 1].ByteSize * TypeMembers[i - 1].GetArraySize());
                else
                    TypeMembers[i].OffsetInRecord = 0;
            }

            if (TypeMembers.Sum(t => t.GetArraySize()) != Header.FieldCount)
                throw new InvalidStructureException<TValue>(ExceptionReason.StructureSizeMismatch, Header.RecordSize, Serializer.RecordSize);

            Header.RecordTable.Exists = true;
            Header.RecordTable.StartOffset = BaseStream.Position;
            Header.RecordTable.Size        = Header.RecordSize * Header.RecordCount;

            Header.StringTable.Exists = true;
            Header.StringTable.StartOffset = Header.RecordTable.EndOffset;
            return true;
        }

        protected override void LoadRecords()
        {
            if (!Options.LoadRecords)
                return;

            BaseStream.Position = Header.RecordTable.StartOffset;

            for (var i = 0; i < Header.RecordCount; ++i)
            {
                var recordOffset = BaseStream.Position;

                var newRecord = Serializer.Deserialize(this);
                var newKey = Serializer.KeyGetter(newRecord);

                // Store the offset to the record and skip to the next, thus making sure
                // to take record padding into consideration.
                OffsetMap[newKey] = recordOffset;

#if DEBUG
                if (BaseStream.Position > recordOffset + Header.RecordSize)
                    throw new InvalidOperationException();
#endif

                BaseStream.Position = recordOffset + Header.RecordSize;

                OnRecordLoaded(newKey, newRecord);
            }
        }
    }
}
