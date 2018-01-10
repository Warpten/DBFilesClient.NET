using DBFilesClient2.NET.Exceptions;
using DBFilesClient2.NET.Internals;
using System;
using System.Linq;

namespace DBFilesClient2.NET.Implementations.WDBC
{
    internal class WDBCReader<TKey, TValue> : BaseStorageReader<TKey, TValue, WDBCHeader>
        where TValue : class, new()
        where TKey : struct
    {
        public WDBCReader(StorageOptions options) : base(options)
        {
        }

        public override bool ParseHeader(BinaryReader reader)
        {
            Header.RecordCount = reader.ReadInt32();
            if (Header.RecordCount == 0)
                return false;

            _header.FieldCount      = reader.ReadInt32();
            _header.RecordSize      = reader.ReadInt32();

            _header.StringTable.Exists = true;
            _header.StringTable.Size   = reader.ReadInt32();

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

            if (TypeMembers.Sum(t => t.GetArraySize()) != _header.FieldCount)
                throw new InvalidStructureException<TValue>(ExceptionReason.StructureSizeMismatch, _header.RecordSize, Serializer.Size);

            // Check size matches
            if (_header.RecordSize != Serializer.Size)
                throw new InvalidStructureException<TValue>(ExceptionReason.StructureSizeMismatch, _header.RecordSize, Serializer.Size);

            _header.RecordTable.Exists = true;
            _header.RecordTable.StartOffset = reader.BaseStream.Position;
            _header.RecordTable.Size        = Header.RecordSize * Header.RecordCount;

            _header.StringTable.Exists = true;
            _header.StringTable.StartOffset = Header.RecordTable.EndOffset;
            return true;
        }

        protected override void LoadRecords(BinaryReader reader)
        {
            if (!Options.LoadRecords)
                return;

            reader.BaseStream.Position = Header.RecordTable.StartOffset;

            for (var i = 0; i < Header.RecordCount; ++i)
            {
                var recordOffset = reader.BaseStream.Position;

                var newRecord = Serializer.Deserializer(reader);
                var newKey = Serializer.GetKey(newRecord);

                // Store the offset to the record and skip to the next, thus making sure
                // to take record padding into consideration.
                OffsetMap[newKey] = recordOffset;

#if DEBUG
                if (reader.BaseStream.Position > recordOffset + Header.RecordSize)
                    throw new InvalidOperationException();
#endif

                reader.BaseStream.Position = recordOffset + Header.RecordSize;

                OnRecordLoaded(newKey, newRecord);
            }
        }
    }
}
