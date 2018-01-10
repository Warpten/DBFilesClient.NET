using DBFilesClient2.NET.Attributes;
using DBFilesClient2.NET.Exceptions;
using DBFilesClient2.NET.Internals;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace DBFilesClient2.NET.Implementations
{
    internal interface ICommonTable<TKey, TValue>
        where TKey : struct
        where TValue : class, new()
    {
        long GetMemberOffset(int memberIndex, TKey recordKey);
    }

    internal class WDCCommonTable<TKey, TValue> : ICommonTable<TKey, TValue>
        where TKey : struct
        where TValue : class, new()
    {
        private Dictionary<int, FieldStore<TKey>> _store = new Dictionary<int, FieldStore<TKey>>();

        public WDCCommonTable(IStorageReader<TKey, TValue> storage, BinaryReader reader)
        {
            var memberIndex = 0;
            foreach (var memberMeta in storage.TypeMembers)
            {
                if (memberMeta.MemberInfo.GetCustomAttribute<IndexAttribute>() != null && storage.Header.IndexTable.Exists)
                    continue;

                if (memberMeta.Compression == MemberCompression.CommonData)
                {
                    var store = new FieldStore<TKey>(storage.Header.CommonTable, memberMeta);

                    store.LoadTable(reader, memberMeta.Type);
                    _store.Add(memberIndex , store);
                }

                ++memberIndex;
            }
        }

        public long GetMemberOffset(int memberIndex, TKey recordKey)
        {
            if (!_store.ContainsKey(memberIndex))
                return long.MinValue;

            return _store[memberIndex].GetOffset(recordKey);
        }
    }

    internal class FieldStore<TKey> where TKey : struct
    {
        private Dictionary<TKey, long> _offsetStore = new Dictionary<TKey, long>();
        private long _startOffset, _endOffset;

        public FieldStore(BlockInfo blockInfo, FieldMetadata fieldMeta)
        {
            _startOffset = blockInfo.StartOffset + fieldMeta.AdditionalDataOffset;
            _endOffset = blockInfo.StartOffset + fieldMeta.AdditionalDataOffset + fieldMeta.AdditionalDataSize;
        }

        public void LoadTable(BinaryReader reader, Type fieldType)
        {
            reader.BaseStream.Position = _startOffset;

            var fieldSize = SizeCache.GetSizeOf(fieldType);

            for (var i = 0; reader.BaseStream.Position < _endOffset; ++i)
            {
                var key = reader.ReadStruct<TKey>();
                _offsetStore.Add(key, reader.BaseStream.Position);

                //! TODO: Is this still padded in WDC1?
                // Read over the structure, padding if necessary (padding was introduced somewhere in 7.3)
                reader.BaseStream.Position += fieldSize;
                if (fieldSize != 4 && reader.PeekChar() == '\0')
                    reader.BaseStream.Position += 4 - fieldSize;
            }
        }

        public long GetOffset(TKey key) =>
            _offsetStore.TryGetValue(key, out var val) ? val : long.MinValue;
    }
}
