using DBFilesClient2.NET.Implementations;
using DBFilesClient2.NET.Implementations.Serializers;
using DBFilesClient2.NET.Implementations.WDB2;
using DBFilesClient2.NET.Implementations.WDB5;
using DBFilesClient2.NET.Implementations.WDB6;
using DBFilesClient2.NET.Implementations.WDBC;
using DBFilesClient2.NET.Implementations.WDC1;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace DBFilesClient2.NET
{
    public class Storage<TKey, TValue> : IDictionary<TKey, TValue> where TValue : class, new() where TKey : struct
    {
        private Dictionary<TKey, TValue> _store = new Dictionary<TKey, TValue>();

        internal int RecordCount { get; set; }
        public int TableHash { get; private set; }
        public int IndexColumn { get; private set; }

        public event Action<long, string> StringLoaded;
  
        public Storage(string filePath, StorageOptions options)
        {
            using (var stream = File.OpenRead(filePath))
                FromStream(stream, options);
        }

        public Storage(Stream fileStream, StorageOptions options)
        {
            FromStream(fileStream, options);
        }

        private void FromStream(Stream dataStream, StorageOptions options)
        {
            if (!dataStream.CanSeek)
                throw new InvalidOperationException($"The stream provided to {GetType().Name} needs to support seek operations!");

            if (options.MemoryBuffer)
            {
                using (var memoryStream = new MemoryStream())
                {
                    dataStream.CopyTo(memoryStream);
                    memoryStream.Position = 0;
                    FromStreamImpl(memoryStream, options);
                }
            }
            else
                FromStreamImpl(dataStream, options);
        }

        private void FromStreamImpl(Stream dataStream, StorageOptions options)
        {
            var buffer = new byte[4];
            dataStream.Read(buffer, 0, 4);
            var signature = buffer[0] | (buffer[1] << 8) | (buffer[2] << 16) | (buffer[3] << 24);

            IStorageReader<TKey, TValue> fileReader = null;
            switch (signature)
            {
                case 0x31434457: // WDC1
                    fileReader = new WDC1Reader<TKey, TValue>(options);
                    break;
                case 0x36424457: // WDB6
                    fileReader = new WDB6Reader<TKey, TValue>(options);
                    break;
                case 0x35424457: // WDB5
                    fileReader = new WDB5Reader<TKey, TValue>(options);
                    break;
                case 0x32424457: // WDB2
                    fileReader = new WDB2Reader<TKey, TValue>(options);
                    break;
                case 0x43424457: // WDBC
                    fileReader = new WDBCReader<TKey, TValue>(options);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown signature 0x{signature:X8} for this DBC!");
            }

            fileReader.Serializer = SerializerFactory.CreateInstance<TKey, TValue>(signature);
            fileReader.Serializer.Storage = fileReader;

            using (var reader = SerializerFactory.CreateReader(signature, dataStream, options))
            {
                if (!fileReader.ParseHeader(reader))
                    return;

                Debug.Assert(fileReader.Serializer != null);

                // Only bind string pool if we wanted to parse it.
                if (options.LoadStringPool)
                    fileReader.StringLoaded += StringLoaded;

                fileReader.RecordLoaded += Add;
                fileReader.LoadFile(reader);
                fileReader.RecordLoaded -= Add;

                if (options.LoadStringPool)
                    fileReader.StringLoaded -= StringLoaded;
            }
        }

        public bool ContainsKey(TKey key) => _store.ContainsKey(key);

        public void Add(TKey key, TValue value) => _store.Add(key, value);

        public bool Remove(TKey key) => _store.Remove(key);

        public bool TryGetValue(TKey key, out TValue value) => _store.TryGetValue(key, out value);

        public void Clear() => _store.Clear();

        public bool Contains(KeyValuePair<TKey, TValue> item) => ((IDictionary<TKey, TValue>)_store).Contains(item);

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            ((IDictionary<TKey, TValue>)_store).CopyTo(array, arrayIndex);
        }

        public bool Remove(KeyValuePair<TKey, TValue> item) => ((IDictionary<TKey, TValue>)_store).Remove(item);
    
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => ((IDictionary<TKey, TValue>)_store).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => ((IDictionary<TKey, TValue>)_store).GetEnumerator();

        public void Add(KeyValuePair<TKey, TValue> item) => ((IDictionary<TKey, TValue>)_store).Add(item);

        public ICollection<TKey> Keys => ((IDictionary<TKey, TValue>)_store).Keys;

        public ICollection<TValue> Values => ((IDictionary<TKey, TValue>)_store).Values;

        public int Count => ((IDictionary<TKey, TValue>)_store).Count;

        public bool IsReadOnly => ((IDictionary<TKey, TValue>)_store).IsReadOnly;

        public TValue this[TKey key] {
            get => ((IDictionary<TKey, TValue>)_store)[key];
            set => ((IDictionary<TKey, TValue>)_store)[key] = value;
        }
    }
}
