using DBFilesClient2.NET.Implementations.Serializers;
using DBFilesClient2.NET.Internals;
using System;

namespace DBFilesClient2.NET.Implementations
{
    internal interface IStorageReader<TKey, TValue> : IBinaryReader
        where TKey : struct
        where TValue : class, new()
    {
        Type ValueType { get; }
        Type KeyType { get; }

        StorageOptions Options { get; }

        #region File loading
        void LoadFile();
        bool ParseHeader();
        void CheckRecordSize();
        #endregion

        #region Events
        event Action<TKey, TValue> RecordLoaded;
        event Action<long, string> StringLoaded;
        #endregion

        FieldMetadata[] TypeMembers { get; }

        ISerializer<TKey, TValue> Serializer { get; set; }
    }

    internal interface IStorageReader<TKey, TValue, THeader> : IStorageReader<TKey, TValue>
        where TKey : struct
        where TValue : class, new()
        where THeader : IStorageHeader, new()
    {
        THeader Header { get; }

        ICommonTable<TKey, TValue> CommonTable { get; }
    }
}
