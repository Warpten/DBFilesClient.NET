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
        int Size { get; }

        IStorageReader<TKey, TValue> Storage { get; set; }

        Func<BinaryReader, TValue> Deserializer { get; }
        Func<TValue, TKey> GetKey { get; }
        Action<TValue, TKey> SetKey { get; }

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

        public static BinaryReader CreateReader(int signature, Stream stream, StorageOptions options)
        {
            switch (signature)
            {
                case 0x43424457: // WDBC
                case 0x32424457: // WDB2
                case 0x35424457: // WDB5
                case 0x36424457: // WDB6
                    return new BinaryReader(stream, options);
                case 0x31434457: // WDC1
                    return new BitReader(stream, options);
                default:
                    throw new InvalidOperationException("Unknown signature");
            }
        }
    }
}
