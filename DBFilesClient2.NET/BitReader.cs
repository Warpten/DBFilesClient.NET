using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBFilesClient2.NET
{
    public class BitReader : BinaryReader
    {
        private int _byte;
        private bool _canRead = false;
        private int _counter = 0;

        public BitReader(Stream stream, StorageOptions options) : base(stream, options)
        {
        }

        public override bool ReadBit()
        {
            // do we need to provision our bit buffer ?
            if (!_canRead)
            {
                _byte = base.ReadByte();
                _canRead = _byte != -1;
            }

            // we are at EOF
            if (!_canRead)
                throw new IndexOutOfRangeException();

            // get current bit and update our counter
            var value = ((_byte & 0xFF) >> (7 - _counter)) & 1;

            _counter++;
            if (_counter > 7)
            {
                _counter = 0;
                _canRead = false;
            }

            return value != 0;
        }

        public override long ReadBits(int bitCount)
        {
            var value = 0L;
            for (var i = bitCount - 1; i >= 0; --i)
                if (ReadBit())
                    value |= (long)(1 << i);

            Console.WriteLine($"Read {bitCount} bits: {value}");          

            return value;
        }

        public override int ReadInt32()
        {
            var value = base.ReadInt32();
            // Console.WriteLine($"Read int32: {value}");
            return value;
        }

        public override long BitPosition
        {
            get {
                var pos = _canRead ? ((BaseStream.Position - 1) * 8 + _counter) : (BaseStream.Position * 8 + _counter);
                //Console.WriteLine();
                //Console.WriteLine($"Acquiring bit position: {pos} ({BaseStream.Position - (_canRead ? 1 : 0)}, {_counter} bits in)");
                return pos;
            }
            set {
                var byteCount = value / 8;
                var bitCount = (int)(value % 8);
                
                if (BaseStream.Position != byteCount)
                {
                    BaseStream.Position = byteCount;
                    _canRead = false;
                }

                _counter = bitCount;

                //Console.WriteLine($"Bit position set to: {value} ({BaseStream.Position}, {_counter} bits in)");
            }
        }

        public override long Position
        {
            get => BaseStream.Position;
            set
            {
                BaseStream.Position = value;

                _canRead = false;
                _counter = 0;

                // Console.WriteLine($"Byte position set to: {value}");
            }
        }

        public override void ResetBitReader()
        {
            _canRead = false;
            _counter = 0;
        }
    }
}
