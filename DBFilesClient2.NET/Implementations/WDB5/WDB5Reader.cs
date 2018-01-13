using DBFilesClient2.NET.Attributes;
using DBFilesClient2.NET.Exceptions;
using DBFilesClient2.NET.Internals;
using System;
using System.IO;
using System.Linq;

namespace DBFilesClient2.NET.Implementations.WDB5
{
    internal class WDB5Reader<TKey, TValue> : BaseStorageReader<TKey, TValue, WDB5Header>
        where TValue : class, new()
        where TKey : struct
    {
        public WDB5Reader(Stream baseStream, StorageOptions options) : base(baseStream, options)
        {

        }

        public override bool ParseHeader()
        {
            Header.RecordCount = ReadInt32();
            if (Header.RecordCount == 0)
                return false;

            Header.FieldCount = ReadInt32();
            Header.RecordSize = ReadInt32();

            var stringTableSize = ReadInt32();

            BaseStream.Position += 4 + 4; // Table and Layout hash

            Header.MinIndex = ReadInt32();
            Header.MaxIndex = ReadInt32();

            BaseStream.Position += 4; // Locales
            
            Header.CopyTable.Size = ReadInt32();
            Header.CopyTable.Exists = true; // On by default, unless size is 0

            var flags = ReadInt16();

            Header.IndexColumn = ReadInt16();

            Header.OffsetMap.Exists = (flags & 0x01) != 0;
            Header.IndexTable.Exists = (flags & 0x04) != 0;
            Header.StringTable.Exists = (flags & 0x01) == 0;

            TypeMembers = new FieldMetadata[Header.FieldCount];
            for (var i = 0; i < Header.FieldCount; ++i)
            {
                TypeMembers[i] = new FieldMetadata();
                TypeMembers[i].ByteSize = (32 - ReadInt16()) / 8;
                TypeMembers[i].OffsetInRecord = ReadUInt16();
            }

            GenerateMemberMetadata();

            var largestFieldSize = TypeMembers.Max(k => k.ByteSize);
            var smallestFieldSize = TypeMembers.Min(k => k.ByteSize);

            // Compute size, padding it to fit record
            var calculatedSize = (int)Math.Ceiling((float)Serializer.RecordSize / largestFieldSize) * largestFieldSize;
            if (Header.RecordSize != calculatedSize && Header.StringTable.Exists)
                throw new InvalidStructureException<TValue>(ExceptionReason.StructureSizeMismatch, Header.RecordSize, calculatedSize);

            Header.RecordTable.Exists = true;
            Header.RecordTable.StartOffset = BaseStream.Position;
            Header.RecordTable.Size = Header.RecordCount * Header.RecordSize;

            Header.StringTable.StartOffset = Header.RecordTable.EndOffset;
            Header.StringTable.Size = stringTableSize;

            Header.OffsetMap.StartOffset = Header.StringTable.EndOffset;
            Header.OffsetMap.Size = (Header.MaxIndex - Header.MinIndex + 1) * (4 + 2);
    
            Header.IndexTable.StartOffset = Header.IndexTable.Exists ? Header.IndexTable.EndOffset : Header.OffsetMap.EndOffset;
            Header.IndexTable.Size = SizeCache<TKey>.Size * Header.RecordCount;

            Header.CopyTable.StartOffset = Header.IndexTable.EndOffset;
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
                var newKey = Header.IndexTable.Exists ? IndexTable[i] : Serializer.KeyGetter(newRecord);

                if (Header.IndexTable.Exists)
                    Serializer.KeySetter(newRecord, newKey);

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

            if (Header.CopyTable.Exists)
            {
                BaseStream.Position = Header.CopyTable.StartOffset;

                for (var i = 0; i < Header.CopyTable.Size / (SizeCache<TKey>.Size * 2); ++i)
                {
                    var newKey = this.ReadStruct<TKey>();
                    var oldKey = this.ReadStruct<TKey>();

                    BaseStream.Position = OffsetMap[oldKey];
                    var newRecord = Serializer.Deserialize(this);
                    Serializer.KeySetter(newRecord, newKey);
                    OnRecordLoaded(newKey, newRecord);
                }
            }
        }
    }
}
