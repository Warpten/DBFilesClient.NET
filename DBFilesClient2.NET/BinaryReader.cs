using DBFilesClient2.NET.Internals;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Text;

namespace DBFilesClient2.NET
{
    internal interface IBinaryReader
    {
        string ReadString(Encoding encoding);
        string ReadString();

        uint ReadUInt24();
        int ReadInt24();

        sbyte ReadSByte(int bitCount = 8);
        byte ReadByte(int bitCount = 8);

        short ReadInt16(int bitCount = 16);
        ushort ReadUInt16(int bitCount = 16);

        int ReadInt32(int bitCount = 32);
        uint ReadUInt32(int bitCount = 32);

        long ReadInt64(int bitCount = 64);
        ulong ReadUInt64(int bitCount = 64);

        float ReadSingle(int bitCount = 32);

        bool ReadBit();

        int BitPosition { get; set; }
        long Position { get; set; }

        void ResetBitReader();
    }

    public abstract class BinaryReader : System.IO.BinaryReader, IBinaryReader
    {
        private int _byte;
        private bool _canRead = false;
        private int _counter = 0;

        private StorageOptions _options;

        public BinaryReader(Stream baseStream, StorageOptions options) : base(baseStream)
        {
            _options = options;
        }

        #region ReadString
        protected string ReadStringDirect(Encoding encoding)
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

        public abstract string ReadString(Encoding encoding);

        public new string ReadString() => ReadString(Encoding.UTF8);
        #endregion

        public int ReadInt24()
        {
            var bytes = ReadBytes(3);
            return bytes[0] | (bytes[1] << 8) | (bytes[2] << 16);
        }

        public uint ReadUInt24() => (uint)ReadInt24();

        public sbyte ReadSByte(int bitCount = 8)
        {
#if CONTRACTS
            Contract.Requires(bitCount <= 8);
#endif

            // If the buffer is provisioned, we are not aligned
            if (_canRead)
                return (sbyte)(ReadBits(bitCount) & 0xFF);

            return base.ReadSByte();
        }

        public byte ReadByte(int bitCount = 8)
        {
#if CONTRACTS
            Contract.Requires(bitCount <= 16);
#endif

            // If the buffer is provisioned, we are not aligned
            if (_canRead)
                return (byte)(ReadBits(bitCount) & 0xFF);

            return base.ReadByte();
        }

        public short ReadInt16(int bitCount = 16)
        {
#if CONTRACTS
            Contract.Requires(bitCount <= 16);
#endif

            // If the buffer is provisioned, we are not aligned
            if (_canRead)
                return (short)(ReadBits(bitCount) & 0xFFFF);

            return base.ReadInt16();
        }

        public ushort ReadUInt16(int bitCount = 16)
        {
#if CONTRACTS
            Contract.Requires(bitCount <= 16);
#endif

            // If the buffer is provisioned, we are not aligned
            if (_canRead)
                return (ushort)(ReadBits(bitCount) & 0xFFFF);

            return base.ReadUInt16();
        }

        public int ReadInt32(int bitCount = 32)
        {
#if CONTRACTS
            Contract.Requires(bitCount <= 32);
#endif

            // If the buffer is provisioned, we are not aligned
            if (_canRead)
                return (int)(ReadBits(bitCount) & 0xFFFFFFFF);

            return base.ReadInt32();
        }

        public uint ReadUInt32(int bitCount = 32)
        {
#if CONTRACTS
            Contract.Requires(bitCount <= 32);
#endif

            // If the buffer is provisioned, we are not aligned
            if (_canRead)
                return (uint)(ReadBits(bitCount) & 0xFFFFFFFF);

            return base.ReadUInt32();
        }

        public long ReadInt64(int bitCount = 64)
        {
#if CONTRACTS
            Contract.Requires(bitCount <= 64);
#endif

            // If the buffer is provisioned, we are not aligned
            if (_canRead)
                return ReadBits(bitCount);

            return base.ReadInt64();
        }
        
        public ulong ReadUInt64(int bitCount = 64)
        {
#if CONTRACTS
            Contract.Requires(bitCount <= 64);
#endif

            // If the buffer is provisioned, we are not aligned
            if (_canRead)
                return (ulong)ReadBits(bitCount);

            return base.ReadUInt64();
        }

        public unsafe float ReadSingle(int bitCount = 32)
        {
            var intValue = ReadUInt32(bitCount);
            var floatValue = intValue.ReinterpretCast<uint, float>();
            return floatValue;
        }

        #region Untyped bit reading
        public bool ReadBit()
        {
            // do we need to provision our bit buffer ?
            if (!_canRead)
            {
                _byte = base.ReadByte();
                _canRead = _byte != -1;
            }

            if (!_canRead)
                throw new IndexOutOfRangeException();

            var value = ((_byte & 0xFF) >> (7 - _counter)) & 1;

            _counter++;
            if (_counter > 7)
            {
                _counter = 0;
                _canRead = false;
            }

            return value != 0;
        }

        private long ReadBits(int bitCount)
        {
            var value = 0L;
            for (var i = bitCount - 1; i >= 0; --i)
                if (ReadBit())
                    value |= (long)(1 << i);

            return value;
        }

        public int BitPosition
        {
            get => _counter;
            set
            {
                _canRead = false;
                _counter = value;
            }
        }

        public long Position
        {
            get => BaseStream.Position;
            set
            {
                BaseStream.Position = value;
                ResetBitReader();
            }
        }

        public void ResetBitReader()
        {
            _canRead = false;
            _counter = 0;
        }
        #endregion
    }
}
