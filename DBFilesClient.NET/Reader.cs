using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace DBFilesClient.NET
{
    internal abstract class Reader : BinaryReader
    {
        protected long StringTableOffset { get; set; }

        public string ReadInlineString()
        {
            var stringStart = BaseStream.Position;
            var stringLength = 0;
            while (ReadByte() != '\0')
                ++stringLength;
            BaseStream.Position = stringStart;

            if (stringLength == 0)
                return string.Empty;

            var stringValue = Encoding.UTF8.GetString(ReadBytes(stringLength));
            ReadByte();

            return stringValue;
        }

        public virtual string ReadTableString()
        {
            // Store position of the next field in this record.
            var oldPosition = BaseStream.Position + 4;

            // Compute offset to string in table.
            BaseStream.Position = ReadInt32() + StringTableOffset;

            // Read the string inline.
            var stringValue = ReadInlineString();

            // Restore stream position.
            BaseStream.Position = oldPosition;
            return stringValue;
        }

        public event Action<int, object> OnRecordLoaded;

        protected void TriggerRecordLoaded(int key, object record) => 
            OnRecordLoaded?.Invoke(key, record);

        protected Reader(Stream data) : base(data)
        {
            Debug.Assert(data is MemoryStream);
        }

        internal abstract void Load();

        public int ReadInt24()
        {
            return ReadByte() | (ReadByte() << 8) | (ReadByte() << 16);
        }

        // ReSharper disable once UnusedMember.Global
        public uint ReadUInt24() => (uint)ReadInt24();
    }
}
