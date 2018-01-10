using DBFilesClient2.NET.Internals;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DBFilesClient2.NET
{
    public class BinaryReader : System.IO.BinaryReader
    {
        private StorageOptions _options;

        public BinaryReader(Stream baseStream, StorageOptions options) : base(baseStream)
        {
            _options = options;
        }
        
        private string ReadStringDirect(Encoding encoding)
        {
            var byteList = new List<byte>();
            byte b;
            while ((b = ReadByte()) != 0)
                byteList.Add(b);

            var result = encoding.GetString(byteList.ToArray());
            if (_options.InternStrings)
                result = string.Intern(result);
            return result;
        }

        public virtual string ReadString(Encoding encoding)
        {
            if (UseInlineStrings)
                return ReadStringDirect(encoding);

            // Not used if strings are inlined, so doesn't matter.
            var oldPosition = BaseStream.Position + 4;

            BaseStream.Position = ReadInt32() + StringTableOffset;
            var result = ReadStringDirect(encoding);
            BaseStream.Position = oldPosition;
            
            return result;
        }

        public new string ReadString() => ReadString(Encoding.UTF8);

        public int ReadInt24()
        {
            var bytes = ReadBytes(3);
            return bytes[0] | (bytes[1] << 8) | (bytes[2] << 16);
        }

        public uint ReadUInt24() => (uint)ReadInt24();

        public long StringTableOffset {
            get;
            set;
        } = 0;
        public bool UseInlineStrings { get; set; } = false;

        public virtual bool ReadBit()
        {
            throw new NotImplementedException();
        }

        public virtual long ReadBits(int bitCount)
        {
            throw new NotImplementedException();
        }

        public virtual long BitPosition
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        public virtual long Position
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        public virtual void ResetBitReader()
        {
            throw new NotImplementedException();
        }
    }
}
