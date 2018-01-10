using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBFilesClient2.NET.Internals
{
    internal class FragmentStream : Stream
    {
        private Stream _stream;
        private int _startOffset;
        private int _size;

        public FragmentStream(Stream parentStream, int startOffset, int size)
        {
            _stream = parentStream;
            _startOffset = startOffset;
            _size = size;
        }

        public override bool CanRead => _stream.CanRead;
        public override bool CanSeek => _stream.CanSeek;
        public override bool CanWrite => _stream.CanWrite;

        public override long Length => _size;

        public override long Position
        {
            get => _stream.Position - _startOffset;
            set {
                if (value >= _size)
                    throw new InvalidOperationException("Out of bounds seek");

                _stream.Position = _startOffset + value;
            }
        }

        public override void Flush() => _stream.Flush();

        public override int Read(byte[] buffer, int offset, int count)
        {
            var remainingBytes = (_startOffset + _size) - count;
            if (count > remainingBytes)
                throw new InvalidOperationException($"Trying to read too many bytes at once! {remainingBytes} available, {count} requested");

            return Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                {
                    if (offset < _startOffset || offset >= _startOffset + _size)
                        throw new InvalidOperationException("Trying to seek outside of the frame!");
                    break;
                }
                case SeekOrigin.Current:
                {
                    var remainingBytes = (_startOffset + _size) - Position;
                    if (offset > remainingBytes)
                        throw new InvalidOperationException("Trying to seek outside of the frame!");
                    break;
                }
                case SeekOrigin.End:
                {
                    var maxBackSeek = _stream.Position - _startOffset;
                    if (-offset > maxBackSeek)
                        throw new InvalidOperationException("Trying to seek outside of the frame!");
                    break;
                }
            }
            return _stream.Seek(offset, origin);
        }

        public override void SetLength(long value) => _stream.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count) => _stream.Write(buffer, offset, count);

        private void TrySeekToStart(long offset)
        {
            if (offset < _startOffset || offset >= (_startOffset + _size))
                _stream.Position = _startOffset;
        }
    }
}
