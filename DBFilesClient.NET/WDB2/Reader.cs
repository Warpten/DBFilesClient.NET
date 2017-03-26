using System;
using System.IO;

namespace DBFilesClient.NET.WDB2
{
    internal class Reader<T> : WDBC.Reader<T> where T : class, new()
    {
        internal Reader(Stream fileStream) : base(fileStream)
        {
        }

        protected override void LoadHeader()
        {
            FileHeader.RecordCount = ReadInt32();
            if (FileHeader.RecordCount == 0)
                return;

            FileHeader.FieldCount = ReadInt32();
            FileHeader.RecordSize = ReadInt32();
            FileHeader.StringTableSize = ReadInt32();
            BaseStream.Position += 12;
            FileHeader.MinIndex = ReadInt32();
            FileHeader.MaxIndex = ReadInt32();

            FileHeader.HasStringTable = FileHeader.StringTableSize != 0;

            // BaseStream.Position += 8 + (FileHeader.MaxIndex - FileHeader.MinIndex + 1) * (4 + 2);

            FileHeader.StringTableOffset = BaseStream.Length - FileHeader.StringTableSize;
        }
    }
}
