using System;
using System.Collections.Generic;
using System.IO;

namespace DBFilesClient.NET
{
    public class Storage<T> : Dictionary<int, T>, IStorage where T : class, new()
    {
        #region Header
        public Type RecordType => typeof(T);
        public int Signature { get; set; }

        public bool HasIndexTable { get; set; }
        public bool HasStringTable { get; set; }
        public ushort IndexField { get; set; }
        #endregion

        public Storage(Stream fileStream, bool readOnly = true)
        {
            FromStream(fileStream);
        }

        private void FromStream(Stream dataStream)
        {
            using (var binaryReader = new BinaryReader(dataStream))
            {
                Signature = binaryReader.ReadInt32();

                Reader<T> fileReader;
                switch (Signature)
                {
                    case 0x36424457:
                        fileReader = new WDB6.Reader<T>(dataStream);
                        break;
                    case 0x35424457:
                        fileReader = new WDB5.Reader<T>(dataStream);
                        break;
                    case 0x32424457:
                        fileReader = new WDB2.Reader<T>(dataStream);
                        break;
                    case 0x43424457:
                        fileReader = new WDBC.Reader<T>(dataStream);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(Signature.ToString("X"));
                }

                fileReader.OnRecordLoaded += (index, record) => this[index] = (T)record;
                fileReader.Load();

                HasIndexTable = fileReader.FileHeader.HasIndexTable;
                HasStringTable = fileReader.FileHeader.HasStringTable;
                IndexField = fileReader.FileHeader.IndexField;
            }
        }

        public Storage(string fileName, bool readOnly = true)
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
