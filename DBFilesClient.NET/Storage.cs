using System;
using System.Collections.Generic;
using System.IO;

namespace DBFilesClient.NET
{
    public class Storage<T> : Dictionary<int, T> where T : class, new()
    {
        public Storage(string fileName)
        {
            using (var stream = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var binaryReader = new BinaryReader(stream))
            {
                var signature = binaryReader.ReadInt32();
                Reader baseReader;
                switch (signature)
                {
                    case 0x35424457:
                        baseReader = new DB5.Reader<T>(stream);
                        break;
                    case 0x43424457:
                        baseReader = new DBC.Reader<T>(stream);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(signature.ToString("X"));
                }

                baseReader.OnRecordLoaded += (index, record) => this[index] = (T) record;
            }
        } 
    }
}
