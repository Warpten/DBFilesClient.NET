using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace DBFilesClient.NET.WDB2
{
    internal class Reader<T> : NET.Reader<T> where T : class, new()
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
            var stringTableSize = ReadInt32();
            BaseStream.Position += 12;
            FileHeader.MinIndex = ReadInt32();
            FileHeader.MaxIndex = ReadInt32();

            FileHeader.HasStringTable = stringTableSize != 0;

            // BaseStream.Position += 8 + (FileHeader.MaxIndex - FileHeader.MinIndex + 1) * (4 + 2);

            FileHeader.StringTableOffset = BaseStream.Length - stringTableSize;
        }

        protected override void LoadRecords()
        {
            for (var i = 0; i < FileHeader.RecordCount; ++i)
            {
                LoadRecord();
                BaseStream.Position += FileHeader.RecordSize;
            }
        }

        private void LoadRecord()
        {
            var key = ReadInt32();
            BaseStream.Position -= 4;
            TriggerRecordLoaded(key, RecordReader(this));
        }
    }
}
