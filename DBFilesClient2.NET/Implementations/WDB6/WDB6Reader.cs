using DBFilesClient2.NET.Exceptions;
using DBFilesClient2.NET.Implementations.Serializers;
using DBFilesClient2.NET.Implementations.WDB5;
using DBFilesClient2.NET.Internals;
using System;
using System.Linq;

namespace DBFilesClient2.NET.Implementations.WDB6
{
    internal class WDB6Reader<TKey, TValue> : WDB5Reader<TKey, TValue> where TValue : class, new() where TKey : struct
    {
        //private CommonBlockParser<TKey, TValue, WDB6Header> _commonBlock;
        private int TotalFieldCount { get; set; }

        public WDB6Reader(StorageOptions options) : base(options)
        {

        }

        public override bool ParseHeader(BinaryReader reader)
        {
            _header.RecordCount = reader.ReadInt32();

            if (Header.RecordCount == 0)
                return false;

            _header.FieldCount = reader.ReadInt32();
            _header.RecordSize = reader.ReadInt32();
            var stringTableSize = reader.ReadInt32();
            reader.BaseStream.Position += 4 + 4; // Table and Layout hash
            _header.MinIndex = reader.ReadInt32();
            _header.MaxIndex = reader.ReadInt32();
            reader.BaseStream.Position += 4; // Locales
            var copyTableSize = reader.ReadInt32();
            var flags = reader.ReadInt16();
            _header.IndexColumn = reader.ReadInt16();
            TotalFieldCount = reader.ReadInt32();

            var commonTableSize = reader.ReadInt32();

            _header.StringTable.Exists = (flags & 0x01) == 0;
            _header.IndexTable.Exists = (flags & 0x04) != 0;
            _header.OffsetMap.Exists = (flags & 0x01) != 0;

            TypeMembers = new FieldMetadata[_header.FieldCount];
            for (var i = 0; i < _header.FieldCount; ++i)
            {
                TypeMembers[i] = new FieldMetadata();
                TypeMembers[i].ByteSize = (32 - reader.ReadInt16()) / 8;
                TypeMembers[i].OffsetInRecord = reader.ReadUInt16();
            }

            GenerateMemberMetadata(reader);

            var largestFieldSize = TypeMembers.Max(k => k.ByteSize);
            var smallestFieldSize = TypeMembers.Min(k => k.ByteSize);

            var calculatedSize = (int)Math.Ceiling((float)Serializer.Size / largestFieldSize) * largestFieldSize;
            if (Header.RecordSize != calculatedSize && Header.StringTable.Exists)
                throw new InvalidStructureException<TValue>(ExceptionReason.StructureSizeMismatch, _header.RecordSize, calculatedSize);

            _header.RecordTable.Exists = true;
            _header.RecordTable.Size = _header.RecordSize * _header.RecordCount;
            _header.RecordTable.StartOffset = reader.BaseStream.Position;

            _header.StringTable.StartOffset = _header.RecordTable.EndOffset;
            _header.StringTable.Size = stringTableSize;

            _header.OffsetMap.StartOffset = _header.StringTable.EndOffset;
            _header.OffsetMap.Size = (Header.MaxIndex - _header.MinIndex + 1) * (4 + 2);

            _header.IndexTable.StartOffset = _header.StringTable.Exists ? _header.StringTable.EndOffset : _header.OffsetMap.EndOffset;
            _header.IndexTable.Size = SizeCache<TKey>.Size * _header.RecordCount;

            _header.CopyTable.StartOffset = _header.IndexTable.EndOffset;
            _header.CopyTable.Size = copyTableSize;
            return true;
        }

        protected override void LoadCommonDataTable(BinaryReader reader)
        {
            // _commonBlock = new CommonBlockParser<TKey, TValue, WDB6Header>(this, reader);
        }

        protected override void LoadRecords(BinaryReader reader)
        {
            if (!Options.LoadRecords)
                return;

            reader.BaseStream.Position = _header.RecordTable.StartOffset;

            for (var i = 0; i < _header.RecordCount; ++i)
            {
                var recordOffset = reader.BaseStream.Position;

                var newRecord = Serializer.Deserializer(reader);
                var newKey = _header.IndexTable.Exists ? IndexTable[i] : Serializer.GetKey(newRecord);

                // deserialize the common block here - refactor needed

                if (Header.IndexTable.Exists)
                    Serializer.SetKey(newRecord, newKey);

                // Store the offset to the record and skip to the next, thus making sure
                // to take record padding into consideration.
                OffsetMap[newKey] = recordOffset;

#if DEBUG
                if (reader.BaseStream.Position > recordOffset + _header.RecordSize)
                    throw new InvalidOperationException();
#endif

                reader.BaseStream.Position = recordOffset + _header.RecordSize;

                OnRecordLoaded(newKey, newRecord);
            }

            if (_header.CopyTable.Exists)
            {
                reader.BaseStream.Position = _header.CopyTable.StartOffset;

                for (var i = 0; i < _header.CopyTable.Size / (SizeCache<TKey>.Size * 2); ++i)
                {
                    var newKey = reader.ReadStruct<TKey>();
                    var oldKey = reader.ReadStruct<TKey>();

                    reader.BaseStream.Position = OffsetMap[oldKey];
                    var newRecord = Serializer.Deserializer(reader);
                    Serializer.SetKey(newRecord, newKey);
                    OnRecordLoaded(newKey, newRecord);
                }
            }
        }
    }
}
