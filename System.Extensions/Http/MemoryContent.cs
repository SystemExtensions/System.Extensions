
namespace System.Extensions.Http
{
    using System.Buffers;
    using System.Diagnostics;
    using System.Threading.Tasks;
    [DebuggerDisplay("Length = {Length}, MemoryContent")]
    public abstract class MemoryContent : IHttpContent
    {
        private static Provider<Buffer<byte>> _Provider =
            Provider<Buffer<byte>>.CreateFromProcessor(() => Buffer<byte>.Create(8192, ArrayPool<byte>.Shared, 8192, out var _), 1024, (buff) => buff.Clear());
        public static void Register(Provider<Buffer<byte>> provider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            _Provider = provider;
        }
        public static void Register(out Provider<Buffer<byte>> provider)
        {
            provider = _Provider;
        }
        public static Buffer<byte> Rent(out IDisposable disposable)
        {
            if (_Provider.TryGetValue(out var buffer, out disposable))
                return buffer;

            return Buffer<byte>.Create(ArrayPool<byte>.Shared, 8192, out disposable);
        }
        #region abstract
        public abstract ReadOnlySequence<byte> Sequence { get; }
        public abstract long Available { get; }
        public abstract long Length { get; }
        public abstract bool Rewind();
        public abstract long ComputeLength();
        public abstract int Read(Span<byte> buffer);
        public abstract int Read(byte[] buffer, int offset, int count);
        public abstract ValueTask<int> ReadAsync(Memory<byte> buffer);
        public abstract ValueTask<int> ReadAsync(byte[] buffer, int offset, int count);
        #endregion
        public static MemoryContent Create(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            return new ByteArrayContent(bytes, 0, bytes.Length);
        }
        public static MemoryContent Create(byte[] bytes, int offset, int count)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));
            if (offset < 0 || offset >= bytes.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || count > bytes.Length - offset)
                throw new ArgumentOutOfRangeException(nameof(count));

            return new ByteArrayContent(bytes, offset, count);
        }
        public static MemoryContent Create(ReadOnlyMemory<byte> bytes)
        {
            return new _MemoryContent(bytes);
        }
        public static MemoryContent Create(ReadOnlySequence<byte> bytes)
        {
            if (bytes.IsSingleSegment)
                return new _MemoryContent(bytes.First);

            return new SequenceContent(bytes);
        }
        #region private
        private class ByteArrayContent : MemoryContent
        {
            public ByteArrayContent(byte[] bytes, int offset, int count)
            {
                _bytes = bytes;
                _offset = offset;
                _count = count;
            }
            private byte[] _bytes;
            private int _offset;
            private int _count;
            private int _position;
            public override ReadOnlySequence<byte> Sequence => new ReadOnlySequence<byte>(_bytes, _offset, _count);
            public override long Available => _count - _position;
            public override long Length => _count;
            public override bool Rewind()
            {
                _position = 0;
                return true;
            }
            public override long ComputeLength() => _count;
            public override int Read(Span<byte> buffer)
            {
                if (_position == _count)
                    return 0;

                var length = buffer.Length;
                if (length == 0)
                    return 0;

                var toCopy = _count - _position;
                if (length < toCopy)
                    toCopy = length;
                unsafe
                {
                    fixed (byte* pBytes = _bytes, pBuffer = buffer)
                    {
                        Buffer.MemoryCopy(pBytes + _offset + _position, pBuffer, toCopy, toCopy);
                    }
                }
                _position += toCopy;
                return toCopy;
            }
            public override int Read(byte[] buffer, int offset, int count)
            {
                return Read(buffer.AsSpan(offset, count));
            }
            public override ValueTask<int> ReadAsync(Memory<byte> buffer)
            {
                return new ValueTask<int>(Read(buffer.Span));
            }
            public override ValueTask<int> ReadAsync(byte[] buffer, int offset, int count)
            {
                return new ValueTask<int>(Read(buffer.AsSpan(offset, count)));
            }
        }
        private class _MemoryContent : MemoryContent
        {
            public _MemoryContent(ReadOnlyMemory<byte> bytes)
            {
                _bytes = bytes;
                _count = bytes.Length;
            }
            private ReadOnlyMemory<byte> _bytes;
            private int _count;
            private int _position;
            public override ReadOnlySequence<byte> Sequence => new ReadOnlySequence<byte>(_bytes);
            public override long Available => _count - _position;
            public override long Length => _count;
            public override bool Rewind()
            {
                _position = 0;
                return true;
            }
            public override long ComputeLength() => _count;
            public override int Read(Span<byte> buffer)
            {
                if (_position == _count)
                    return 0;

                var length = buffer.Length;
                if (length == 0)
                    return 0;

                var toCopy = _count - _position;
                if (length < toCopy)
                    toCopy = length;
                unsafe
                {
                    fixed (byte* pBytes = _bytes.Span, pBuffer = buffer)
                    {
                        Buffer.MemoryCopy(pBytes + _position, pBuffer, toCopy, toCopy);
                    }
                }
                _position += toCopy;
                return toCopy;
            }
            public override int Read(byte[] buffer, int offset, int count)
            {
                return Read(buffer.AsSpan(offset, count));
            }
            public override ValueTask<int> ReadAsync(Memory<byte> buffer)
            {
                return new ValueTask<int>(Read(buffer.Span));
            }
            public override ValueTask<int> ReadAsync(byte[] buffer, int offset, int count)
            {
                return new ValueTask<int>(Read(buffer.AsSpan(offset, count)));
            }
        }
        private class SequenceContent : MemoryContent
        {
            public SequenceContent(ReadOnlySequence<byte> bytes)
            {
                _bytes = bytes;
                _count = bytes.Length;
            }
            private ReadOnlySequence<byte> _bytes;
            private long _count;
            private long _position;
            public override ReadOnlySequence<byte> Sequence => _bytes;
            public override long Available => _count - _position;
            public override long Length => _count;
            public override bool Rewind()
            {
                _position = 0;
                return true;
            }
            public override long ComputeLength() => _count;
            public override int Read(Span<byte> buffer)
            {
                if (_position == _count)
                    return 0;

                var length = buffer.Length;
                if (length == 0)
                    return 0;

                var seq = _bytes.Slice(_position);
                var bytesSum = 0;
                foreach (var segm in seq)
                {
                    var toCopy = length - bytesSum;
                    if (toCopy > segm.Length)
                    {
                        toCopy = segm.Length;
                        segm.Span.CopyTo(buffer.Slice(bytesSum));
                    }
                    else
                    {
                        segm.Span.Slice(0, toCopy).CopyTo(buffer.Slice(bytesSum));
                        Debug.Assert(bytesSum + toCopy == length);
                    }
                    bytesSum += toCopy;
                    if (bytesSum == length)
                        break;
                }
                _position += bytesSum;
                return bytesSum;
            }
            public override int Read(byte[] buffer, int offset, int count)
            {
                return Read(buffer.AsSpan(offset, count));
            }
            public override ValueTask<int> ReadAsync(Memory<byte> buffer)
            {
                return new ValueTask<int>(Read(buffer.Span));
            }
            public override ValueTask<int> ReadAsync(byte[] buffer, int offset, int count)
            {
                return new ValueTask<int>(Read(buffer.AsSpan(offset, count)));
            }
        }
        #endregion
    }
}
