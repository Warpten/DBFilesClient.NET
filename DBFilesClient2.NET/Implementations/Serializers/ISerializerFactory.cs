using DBFilesClient2.NET.Implementations.WDB2;
using DBFilesClient2.NET.Implementations.WDB5;
using DBFilesClient2.NET.Implementations.WDB6;
using DBFilesClient2.NET.Implementations.WDBC;
using System;
using System.IO;

namespace DBFilesClient2.NET.Implementations.Serializers
{
    internal interface ISerializer<TKey, TValue>
        where TKey : struct
        where TValue : class, new()
    {
        /// <summary>
        /// This property should be lazy-init'ed: it does not get called if the file does not have a copy table, or
        /// if it has an index table.
        /// </summary>
        Func<TValue, TKey> KeyGetter { get; }

        /// <summary>
        /// This property should be lazy-init'ed: it does not get called if the file does not have an index table
        /// and if it doesn't have a copy table either.
        /// </summary>
        Action<TValue, TKey> KeySetter { get; }

        /// <summary>
        /// The size, in bytes, of the record. This assumes that strings are not inlined.
        /// </summary>
        int RecordSize { get; }

        void SetStorage(IStorageReader<TKey, TValue> storage);

        TValue Deserialize(IStorageReader<TKey, TValue> reader);
    }

    internal interface ISerializer<TKey, TValue, THeader> : ISerializer<TKey, TValue>
        where TKey : struct
        where TValue : class, new()
        where THeader : IStorageHeader, new()
    {
        Action<TKey, TValue, ICommonTable<TKey, TValue>, BinaryReader> CommonTableDeserializer { get; }
    }

    internal static class SerializerFactory
    {
        public static ISerializer<TKey, TValue> CreateInstance<TKey, TValue>(int signature)
            where TKey : struct
            where TValue : class, new()
        {
            switch (signature)
            {
                case 0x43424457: // WDBC
                    return new WDBSerializer<TKey, TValue, WDBCHeader>();
                case 0x32424457: // WDB2
                    return new WDBSerializer<TKey, TValue, WDB2Header>();
                case 0x35424457: // WDB5
                    return new WDBSerializer<TKey, TValue, WDB5Header>();
                case 0x36424457: // WDB6
                    return new WDBSerializer<TKey, TValue, WDB6Header>();
                case 0x31434457: // WDC1
                    return new WDCSerializer<TKey, TValue, WDC1Header>();
                default:
                    throw new InvalidOperationException("Unknown signature");
            }
        }
    }
}
