using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace DBFilesClient.NET
{
    public class Storage<T> : Dictionary<int, T>, IStorage where T : class, new()
    {
        public Type RecordType => typeof(T);
        public int Signature { get; set; }

        public Storage(Stream fileStream)
        {
            FromStream(fileStream);
        }

        private void FromStream(Stream dataStream)
        {
            Debug.Assert(dataStream.CanSeek, "The provided data stream must support seek operations!");

            using (var binaryReader = new BinaryReader(dataStream))
            {
                Signature = binaryReader.ReadInt32();

                Reader baseReader;
                switch (Signature)
                {
                    case 0x35424457:
                        baseReader = new WDB5.Reader<T>(dataStream);
                        break;
                    case 0x32424457:
                        baseReader = new WDB2.Reader<T>(dataStream);
                        break;
                    case 0x43424457:
                        baseReader = new WDBC.Reader<T>(dataStream);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(Signature.ToString("X"));
                }

                baseReader.OnRecordLoaded += (index, record) => this[index] = (T)record;
                baseReader.Load();
            }
        }

        public Storage(string fileName)
        {
            using (var fileStream = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var fileBytes = new byte[fileStream.Length];
                fileStream.Read(fileBytes, 0, fileBytes.Length);

                using (var memoryStream = new MemoryStream(fileBytes))
                    FromStream(memoryStream);
            }
        }
    }
}
