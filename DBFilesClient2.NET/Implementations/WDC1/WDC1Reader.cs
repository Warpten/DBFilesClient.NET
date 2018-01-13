using DBFilesClient2.NET.Attributes;
using DBFilesClient2.NET.Exceptions;
using DBFilesClient2.NET.Implementations.WDBC;
using DBFilesClient2.NET.Internals;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DBFilesClient2.NET.Implementations.WDC1
{
    internal class WDC1Reader<TKey, TValue> : BaseStorageReader<TKey, TValue, WDC1Header>
        where TKey : struct
        where TValue : class, new()
    {
        private int _totalFieldCount;

        internal WDC1Reader(Stream baseStream, StorageOptions options) : base(baseStream, options)
        {
        }

        public override bool ParseHeader()
        {
            Header.RecordCount  = ReadInt32();
            Header.FieldCount   = ReadInt32();
            Header.RecordSize   = ReadInt32();
            var stringTableSize  = ReadInt32();

            BaseStream.Position += 4 + 4;

            Header.MinIndex    = ReadInt32();
            Header.MaxIndex    = ReadInt32();
            var locale          = ReadInt32();
            var copyTableSize   = ReadInt32();
            var flags           = ReadInt16();
            Header.IndexColumn = ReadInt16();
            _totalFieldCount    = ReadInt32();

            var bitpackedDataOffset = ReadInt32(); // irrelevant to parsing

            var lookupColumnCount    = ReadInt32();
            long offsetMapOffset     = ReadInt32();
            var indexTableSize       = ReadInt32();
            var fieldStorageInfoSize = ReadInt32();
            var commonDataSize       = ReadInt32();
            var palletDataSize       = ReadInt32();
            var relationshipDataSize = ReadInt32();

            var fieldStructures = new FieldMetadata[_totalFieldCount];

            for (var i = 0; i < _totalFieldCount; ++i)
            {
                var structure = new FieldMetadata();
                structure.ByteSize = (32 - ReadInt16()) / 8;
                structure.OffsetInRecord = ReadUInt16();
                fieldStructures[i] = structure;
            }

            // Already estimate array size
            for (var i = 0; i < _totalFieldCount - 1; ++i)
            {
                // If that field is 0-bytes, it means we have a packed field.
                if (fieldStructures[i].ByteSize == 0)
                    continue;

                fieldStructures[i].GuessedArraySize = (int)((fieldStructures[i + 1].OffsetInRecord - fieldStructures[i].OffsetInRecord) / fieldStructures[i].ByteSize);
            }
            Header.RecordTable.Exists        = (flags & 0x01) == 0;
            Header.StringTable.Exists        = (flags & 0x01) == 0;
            Header.OffsetMap.Exists          = (flags & 0x01) != 0;
            Header.VariableRecordData.Exists = (flags & 0x01) != 0;

            Header.RecordTable.StartOffset = BaseStream.Position;
            Header.RecordTable.Size        = Header.RecordSize * Header.RecordCount;

            Header.StringTable.StartOffset = Header.RecordTable.EndOffset;
            Header.StringTable.Size        = stringTableSize;

            Header.VariableRecordData.StartOffset = BaseStream.Position;
            Header.VariableRecordData.Size        = (int)(offsetMapOffset - BaseStream.Position);

            Header.OffsetMap.StartOffset = Header.VariableRecordData.EndOffset;
            Header.OffsetMap.Size        = (Header.MaxIndex - Header.MinIndex + 1) * (4 + 2);

            Header.IndexTable.Exists      = true;
            Header.IndexTable.StartOffset = Header.OffsetMap.Exists ? Header.OffsetMap.EndOffset : Header.StringTable.EndOffset;
            Header.IndexTable.Size        = indexTableSize;

            Header.CopyTable.Exists      = true;
            Header.CopyTable.StartOffset = Header.IndexTable.EndOffset;
            Header.CopyTable.Size        = copyTableSize;
            
            Header.ExtendedMemberMetadata.Exists      = true;
            Header.ExtendedMemberMetadata.StartOffset = Header.CopyTable.EndOffset;
            Header.ExtendedMemberMetadata.Size        = fieldStorageInfoSize;

            Header.PalletTable.Exists      = true;
            Header.PalletTable.StartOffset = Header.ExtendedMemberMetadata.EndOffset;
            Header.PalletTable.Size        = palletDataSize;

            Header.CommonTable.Exists      = true;
            Header.CommonTable.StartOffset = Header.PalletTable.EndOffset;
            Header.CommonTable.Size        = commonDataSize;

            BaseStream.Position = Header.ExtendedMemberMetadata.StartOffset;
            var memberIndex = 0;
            for (var i = 0; BaseStream.Position < Header.ExtendedMemberMetadata.EndOffset; ++i, ++memberIndex)
            {
                var isIndexMember = Members[memberIndex].GetCustomAttribute<IndexAttribute>() != null;
                if (isIndexMember && memberIndex == Header.IndexColumn && Header.IndexTable.Exists)
                    ++memberIndex;

                var bitOffset = ReadUInt16();
                var bitSize = ReadUInt16();

                fieldStructures[i].BitOffsetInRecord = bitOffset;
                fieldStructures[i].BitSize = bitSize;

                fieldStructures[i].AdditionalDataSize = ReadInt32();
                fieldStructures[i].Compression = (MemberCompression)ReadUInt32();

                fieldStructures[i].CompressionData = this.ReadStruct<CompressionData>();
                fieldStructures[i].MemberInfo = Members[memberIndex];

                for (var j = 0; j < i; ++j)
                {
                    if (fieldStructures[j].Compression != fieldStructures[i].Compression)
                    {
                        // Different blocks, don't add sizes...
                        var belongsToMatchingBlock = false;

                        // Unless...
                        if (fieldStructures[i].Compression == MemberCompression.BitpackedIndexed)
                            belongsToMatchingBlock = fieldStructures[j].Compression == MemberCompression.BitpackedIndexedArray;
                        else if (fieldStructures[i].Compression == MemberCompression.BitpackedIndexedArray)
                            belongsToMatchingBlock = fieldStructures[j].Compression == MemberCompression.BitpackedIndexed;

                        if (!belongsToMatchingBlock)
                            continue;
                    }

                    fieldStructures[i].AdditionalDataOffset += fieldStructures[j].AdditionalDataSize;
                }
            }

            TypeMembers = fieldStructures;

            // Be horribly assertive here, since we have a completely correct structure definition
            for (var i = 0; i < TypeMembers.Length; ++i)
            {
                var memberInfo = TypeMembers[i];
                if (memberInfo.MemberInfo.GetCustomAttribute<IndexAttribute>() != null && Header.IndexTable.Exists)
                    continue;

                var isMemberArray = memberInfo.MemberInfo.GetMemberType().IsArray;
                if (memberInfo.Compression == MemberCompression.BitpackedIndexedArray)
                {
                    var storagePresenceAttribute = memberInfo.MemberInfo.GetCustomAttribute<StoragePresenceAttribute>();
                    if (storagePresenceAttribute != null)
                    {
                        if (memberInfo.CompressionData.BitpackedIndexedArray.ArraySize != storagePresenceAttribute.SizeConst)
                            throw new InvalidStructureException<TValue>(ExceptionReason.InvalidArraySize, memberInfo.MemberInfo.Name,
                                storagePresenceAttribute.SizeConst, memberInfo.CompressionData.BitpackedIndexedArray.ArraySize);
                    }
                    //! TODO: Complain if StoragePresenceAttribute is missing?
                }
                else if (memberInfo.Compression == MemberCompression.Bitpacked)
                {
                    var isSigned = Convert.ToBoolean(memberInfo.BaseType.GetField("MinValue").GetValue(null));
                    if (memberInfo.CompressionData.Bitpacked.Signed != isSigned)
                        throw new InvalidStructureException<TValue>(ExceptionReason.MemberShouldBeSigned, memberInfo.MemberInfo.Name);
                }
            }

            // var largestFieldSize = TypeMembers.Max(k => k.ByteSize);
            // var smallestFieldSize = TypeMembers.Min(k => k.ByteSize);
            // 
            // var calculatedSize = (int)Math.Ceiling((float)Serializer.Size / largestFieldSize) * largestFieldSize;
            // if (Header.RecordSize != calculatedSize && Header.StringTable.Exists)
            //     throw new InvalidStructureException<TValue>(ExceptionReason.StructureSizeMismatch, Header.RecordSize, calculatedSize);
            return true;
        }

        protected override void LoadRecords()
        {
            if (!Options.LoadRecords)
                return;

            if (Header.StringTable.Exists && Header.RecordTable.Exists)
            {
                BaseStream.Position = Header.RecordTable.StartOffset;

                for (var i = 0; i < Header.RecordCount; ++i)
                {
                    var recordOffset = BaseStream.Position;

                    var newRecord = Serializer.Deserialize(this);
                    var newKey = Header.IndexTable.Exists ? IndexTable[i] : Serializer.KeyGetter(newRecord);
                    if (Header.IndexTable.Exists)
                        Serializer.KeySetter(newRecord, newKey);

                    if (Header.CommonTable.Exists)
                    {
                        var oldPosition = BaseStream.Position;

                        // Serializer.CommonTableDeserializer(newKey, newRecord, CommonTable, this);

                        BaseStream.Position = oldPosition;
                    }

                    OnRecordLoaded(newKey, newRecord);

                    OffsetMap[newKey] = recordOffset;

                    if (BaseStream.Position > recordOffset + Header.RecordSize)
                        throw new InvalidOperationException();

                    Position = recordOffset + Header.RecordSize;
                }
            }
            else if (Header.OffsetMap.Exists && Header.VariableRecordData.Exists)
            {
                BaseStream.Position = Header.OffsetMap.StartOffset;

                var offsetList = new List<Tuple<uint, ushort>>();
                for (var i = 0; i < Header.MaxIndex - Header.MinIndex + 1; ++i)
                    offsetList.Add(Tuple.Create(ReadUInt32(), ReadUInt16()));

                BaseStream.Position = Header.VariableRecordData.StartOffset;

                for (var i = 0; i < offsetList.Count; ++i)
                {
                    var newRecord = Serializer.Deserialize(this);
                    var newKey = Header.IndexTable.Exists ? IndexTable[i] : Serializer.KeyGetter(newRecord);
                    if (Header.IndexTable.Exists)
                        Serializer.KeySetter(newRecord, newKey);
                    
                    OnRecordLoaded(newKey, newRecord);
                    
                    if (BaseStream.Position != offsetList[i].Item1 + offsetList[i].Item2)
                        throw new InvalidOperationException();
                }
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
