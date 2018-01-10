using DBFilesClient2.NET.Implementations.Serializers;
using DBFilesClient2.NET.Internals;
using System;

namespace DBFilesClient2.NET.Implementations
{
    internal interface IStorageReader<TKey, TValue>
        where TKey : struct
        where TValue : class, new()
    {
        Type ValueType { get; }
        Type KeyType { get; }

        StorageOptions Options { get; }

        void LoadFile(BinaryReader reader);
        bool ParseHeader(BinaryReader reader);

        ISerializer<TKey, TValue> Serializer { get; set; }

        event Action<TKey, TValue> RecordLoaded;
        event Action<long, string> StringLoaded;

        FieldMetadata[] TypeMembers { get; }

        IStorageHeader Header { get; }

        ICommonTable<TKey, TValue> CommonTable { get; }
    }
}
