using DBFilesClient2.NET.Attributes;
using DBFilesClient2.NET.Exceptions;
using DBFilesClient2.NET.Internals;
using System;
using System.Linq;
using System.Reflection;

namespace DBFilesClient2.NET.Implementations.WDB5
{
    internal class WDB5Reader<TKey, TValue> : BaseStorageReader<TKey, TValue, WDB5Header>
        where TValue : class, new()
        where TKey : struct
    {
        public WDB5Reader(StorageOptions options) : base(options)
        {

        }

        public override bool ParseHeader(BinaryReader reader)
        {
            _header.RecordCount = reader.ReadInt32();
            if (_header.RecordCount == 0)
                return false;

            _header.FieldCount = reader.ReadInt32();
            _header.RecordSize = reader.ReadInt32();

            var stringTableSize = reader.ReadInt32();

            reader.BaseStream.Position += 4 + 4; // Table and Layout hash

            _header.MinIndex = reader.ReadInt32();
            _header.MaxIndex = reader.ReadInt32();

            reader.BaseStream.Position += 4; // Locales
            
            _header.CopyTable.Size = reader.ReadInt32();
            _header.CopyTable.Exists = true; // On by default, unless size is 0

            var flags = reader.ReadInt16();

            _header.IndexColumn = reader.ReadInt16();

            _header.OffsetMap.Exists = (flags & 0x01) != 0;
            _header.IndexTable.Exists = (flags & 0x04) != 0;
            _header.StringTable.Exists = (flags & 0x01) == 0;

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

            // Compute size, padding it to fit record
            var calculatedSize = (int)Math.Ceiling((float)Serializer.Size / largestFieldSize) * largestFieldSize;
            if (_header.RecordSize != calculatedSize && _header.StringTable.Exists)
                throw new InvalidStructureException<TValue>(ExceptionReason.StructureSizeMismatch, _header.RecordSize, calculatedSize);

            _header.RecordTable.Exists = true;
            _header.RecordTable.StartOffset = reader.BaseStream.Position;
            _header.RecordTable.Size = _header.RecordCount * _header.RecordSize;

            _header.StringTable.StartOffset = _header.RecordTable.EndOffset;
            _header.StringTable.Size = stringTableSize;

            _header.OffsetMap.StartOffset = _header.StringTable.EndOffset;
            _header.OffsetMap.Size = (_header.MaxIndex - _header.MinIndex + 1) * (4 + 2);
    
            _header.IndexTable.StartOffset = _header.IndexTable.Exists ? _header.IndexTable.EndOffset : _header.OffsetMap.EndOffset;
            _header.IndexTable.Size = SizeCache<TKey>.Size * _header.RecordCount;

            _header.CopyTable.StartOffset = _header.IndexTable.EndOffset;
            return true;
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

                if (_header.IndexTable.Exists)
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
