using DBFilesClient2.NET.Attributes;
using DBFilesClient2.NET.Exceptions;
using DBFilesClient2.NET.Implementations.WDBC;
using DBFilesClient2.NET.Internals;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace DBFilesClient2.NET.Implementations.WDC1
{
    internal class WDC1Reader<TKey, TValue> : BaseStorageReader<TKey, TValue, WDC1Header>
        where TKey : struct
        where TValue : class, new()
    {
        private int _totalFieldCount;

        public WDC1Reader(StorageOptions options) : base(options)
        {
        }

        public override bool ParseHeader(BinaryReader reader)
        {
            _header.RecordCount  = reader.ReadInt32();
            _header.FieldCount   = reader.ReadInt32();
            _header.RecordSize   = reader.ReadInt32();
            var stringTableSize = reader.ReadInt32();

            reader.BaseStream.Position += 4 + 4;

            _header.MinIndex    = reader.ReadInt32();
            _header.MaxIndex    = reader.ReadInt32();
            var locale          = reader.ReadInt32();
            var copyTableSize   = reader.ReadInt32();
            var flags           = reader.ReadInt16();
            _header.IndexColumn = reader.ReadInt16();
            _totalFieldCount    = reader.ReadInt32();

            var bitpackedDataOffset = reader.ReadInt32(); // irrelevant to parsing

            var lookupColumnCount    = reader.ReadInt32();
            long offsetMapOffset     = reader.ReadInt32();
            var indexTableSize       = reader.ReadInt32();
            var fieldStorageInfoSize = reader.ReadInt32();
            var commonDataSize       = reader.ReadInt32();
            var palletDataSize       = reader.ReadInt32();
            var relationshipDataSize = reader.ReadInt32();

            var fieldStructures = new FieldMetadata[_totalFieldCount];

            for (var i = 0; i < _totalFieldCount; ++i)
            {
                var structure = new FieldMetadata();
                structure.ByteSize = (32 - reader.ReadInt16()) / 8;
                structure.OffsetInRecord = reader.ReadUInt16();
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
            _header.RecordTable.Exists        = (flags & 0x01) == 0;
            _header.StringTable.Exists        = (flags & 0x01) == 0;
            _header.OffsetMap.Exists          = (flags & 0x01) != 0;
            _header.VariableRecordData.Exists = (flags & 0x01) != 0;

            _header.RecordTable.StartOffset = reader.BaseStream.Position;
            _header.RecordTable.Size        = _header.RecordSize * _header.RecordCount;

            _header.StringTable.StartOffset = _header.RecordTable.EndOffset;
            _header.StringTable.Size        = stringTableSize;

            _header.VariableRecordData.StartOffset = reader.BaseStream.Position;
            _header.VariableRecordData.Size        = (int)(offsetMapOffset - reader.BaseStream.Position);

            _header.OffsetMap.StartOffset = _header.VariableRecordData.EndOffset;
            _header.OffsetMap.Size        = (_header.MaxIndex - _header.MinIndex + 1) * (4 + 2);

            _header.IndexTable.Exists      = true;
            _header.IndexTable.StartOffset = _header.OffsetMap.Exists ? _header.OffsetMap.EndOffset : _header.StringTable.EndOffset;
            _header.IndexTable.Size        = indexTableSize;

            _header.CopyTable.Exists      = true;
            _header.CopyTable.StartOffset = _header.IndexTable.EndOffset;
            _header.CopyTable.Size        = copyTableSize;
            
            _header.ExtendedMemberMetadata.Exists      = true;
            _header.ExtendedMemberMetadata.StartOffset = _header.CopyTable.EndOffset;
            _header.ExtendedMemberMetadata.Size        = fieldStorageInfoSize;

            _header.PalletTable.Exists      = true;
            _header.PalletTable.StartOffset = _header.ExtendedMemberMetadata.EndOffset;
            _header.PalletTable.Size        = palletDataSize;

            _header.CommonTable.Exists      = true;
            _header.CommonTable.StartOffset = _header.PalletTable.EndOffset;
            _header.CommonTable.Size        = commonDataSize;

            reader.BaseStream.Position = _header.ExtendedMemberMetadata.StartOffset;
            var memberIndex = 0;
            for (var i = 0; reader.BaseStream.Position < _header.ExtendedMemberMetadata.EndOffset; ++i, ++memberIndex)
            {
                var isIndexMember = Members[memberIndex].GetCustomAttribute<IndexAttribute>() != null;
                if (isIndexMember && memberIndex == _header.IndexColumn && _header.IndexTable.Exists)
                    ++memberIndex;

                var bitOffset = reader.ReadUInt16();
                var bitSize = reader.ReadUInt16();

                fieldStructures[i].BitOffsetInRecord = bitOffset;
                fieldStructures[i].BitSize = bitSize;

                fieldStructures[i].AdditionalDataSize = reader.ReadInt32();
                fieldStructures[i].Compression = (MemberCompression)reader.ReadUInt32();

                fieldStructures[i].CompressionData = reader.ReadStruct<CompressionData>();
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
                if (memberInfo.MemberInfo.GetCustomAttribute<IndexAttribute>() != null && _header.IndexTable.Exists)
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
            //     throw new InvalidStructureException<TValue>(ExceptionReason.StructureSizeMismatch, _header.RecordSize, calculatedSize);
            return true;
        }

        protected override void LoadRecords(BinaryReader reader)
        {
            if (!Options.LoadRecords)
                return;

            if (_header.StringTable.Exists && _header.RecordTable.Exists)
            {
                reader.BaseStream.Position = _header.RecordTable.StartOffset;

                for (var i = 0; i < _header.RecordCount; ++i)
                {
                    var recordOffset = reader.BaseStream.Position;

                    var newRecord = Serializer.Deserializer(reader);
                    var newKey = _header.IndexTable.Exists ? IndexTable[i] : Serializer.GetKey(newRecord);
                    if (_header.IndexTable.Exists)
                        Serializer.SetKey(newRecord, newKey);

                    if (_header.CommonTable.Exists)
                    {
                        var oldPosition = reader.BaseStream.Position;

                        Serializer.CommonTableDeserializer(newKey, newRecord, CommonTable, reader);

                        reader.BaseStream.Position = oldPosition;
                    }

                    OnRecordLoaded(newKey, newRecord);

                    OffsetMap[newKey] = recordOffset;

                    if (reader.BaseStream.Position > recordOffset + _header.RecordSize)
                        throw new InvalidOperationException();

                    reader.Position = recordOffset + _header.RecordSize;
                }
            }
            else if (_header.OffsetMap.Exists && _header.VariableRecordData.Exists)
            {
                reader.BaseStream.Position = _header.OffsetMap.StartOffset;

                var offsetList = new List<Tuple<uint, ushort>>();
                for (var i = 0; i < _header.MaxIndex - _header.MinIndex + 1; ++i)
                    offsetList.Add(Tuple.Create(reader.ReadUInt32(), reader.ReadUInt16()));

                reader.BaseStream.Position = _header.VariableRecordData.StartOffset;

                for (var i = 0; i < offsetList.Count; ++i)
                {
                    var newRecord = Serializer.Deserializer(reader);
                    var newKey = _header.IndexTable.Exists ? IndexTable[i] : Serializer.GetKey(newRecord);
                    if (_header.IndexTable.Exists)
                        Serializer.SetKey(newRecord, newKey);
                    
                    OnRecordLoaded(newKey, newRecord);
                    
                    if (reader.BaseStream.Position != offsetList[i].Item1 + offsetList[i].Item2)
                        throw new InvalidOperationException();
                }
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
