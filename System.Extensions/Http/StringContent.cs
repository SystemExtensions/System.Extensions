
namespace System.Extensions.Http
{
    using System.Text;
    using System.Buffers;
    using System.Diagnostics;
    using System.Threading.Tasks;
    [DebuggerDisplay("Length = {Length}, StringContent")]
    public abstract class StringContent : IHttpContent
    {
        private static Provider<Buffer<char>> _Provider =
            Provider<Buffer<char>>.CreateFromProcessor(() => Buffer<char>.Create(4096, ArrayPool<char>.Shared, 4096, out var _), 1024, (buff) => buff.Clear());
        public static void Register(Provider<Buffer<char>> provider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            _Provider = provider;
        }
        public static void Register(out Provider<Buffer<char>> provider) 
        {
            provider = _Provider;
        }
        public static Buffer<char> Rent(out IDisposable disposable)
        {
            if (_Provider.TryGetValue(out var buffer, out disposable))
                return buffer;
            //ArrayPool.Shared.Rent()
            return Buffer<char>.Create(ArrayPool<char>.Shared, 4096, out disposable);
        }
        #region abstract
        public abstract Encoding Encoding { get; }
        public abstract ReadOnlySequence<char> Sequence { get; }
        public abstract long Available { get; }
        public abstract long Length { get; }
        public abstract bool Rewind();
        public abstract long ComputeLength();
        public abstract int Read(Span<byte> buffer);
        public abstract int Read(byte[] buffer, int offset, int count);
        public abstract ValueTask<int> ReadAsync(Memory<byte> buffer);
        public abstract ValueTask<int> ReadAsync(byte[] buffer, int offset, int count);
        #endregion
        public static StringContent Create(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            return new _StringContent(value, Encoding.UTF8);
        }
        public static StringContent Create(string value,Encoding encoding)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            return new _StringContent(value, encoding);
        }
        public static StringContent Create(ReadOnlyMemory<char> value)
        {
            return new MemoryContent(value, Encoding.UTF8);
        }
        public static StringContent Create(ReadOnlyMemory<char> value, Encoding encoding)
        {
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            return new MemoryContent(value, encoding);
        }
        public static StringContent Create(ReadOnlySequence<char> value)
        {
            if (value.IsSingleSegment)
                return new MemoryContent(value.First, Encoding.UTF8);

            return new SequenceContent(value, Encoding.UTF8);
        }
        public static StringContent Create(ReadOnlySequence<char> value, Encoding encoding)
        {
            if (value.IsSingleSegment)
                return new MemoryContent(value.First, encoding);

            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            return new SequenceContent(value, encoding);
        }
        #region private
        private class _StringContent : StringContent
        {
            public _StringContent(string value, Encoding encoding)
            {
                _value = value;
                _encoding = encoding;
            }

            private string _value;
            private int _position, _byteCount = -1;
            private Encoder _encoder;
            private Encoding _encoding;
            public override Encoding Encoding => _encoding;
            public override ReadOnlySequence<char> Sequence => new ReadOnlySequence<char>(_value.AsMemory());
            public override long Available => _position == _value.Length ? 0 : -1;
            public override long Length => _byteCount;
            public override bool Rewind()
            {
                _position = 0;
                return true;
            }
            public override long ComputeLength()
            {
                if (_byteCount == -1)
                    _byteCount = _encoding.GetByteCount(_value);

                return _byteCount;
            }
            public override int Read(Span<byte> buffer)
            {
                if (_position == _value.Length)
                    return 0;

                var length = buffer.Length;
                if (length == 0)
                    return 0;

                if (_encoder == null)
                {
                    if ((_byteCount > 0 && _byteCount <= length) || _encoding.GetMaxByteCount(_value.Length) <= length)
                    {
                        var result = _encoding.GetBytes(_value, buffer);
                        _position = _value.Length;
                        return result;
                    }
                    _encoder = _encoding.GetEncoder();
                }
                int charsUsed, bytesUsed;
                unsafe
                {
                    fixed (char* pValue = _value) 
                    fixed (byte* pBytes = buffer)
                    {
                        _encoder.Convert(pValue + _position, _value.Length - _position, pBytes, length, true, out charsUsed, out bytesUsed, out _);
                        _position += charsUsed;
                        return bytesUsed;
                    }
                }
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
            public override string ToString() => _value;
        }
        private class MemoryContent : StringContent
        {
            public MemoryContent(ReadOnlyMemory<char> value, Encoding encoding)
            {
                _value = value;
                _encoding = encoding;
            }

            private ReadOnlyMemory<char> _value;
            private int _position, _byteCount = -1;
            private Encoder _encoder;
            private Encoding _encoding;
            public override Encoding Encoding => _encoding;
            public override ReadOnlySequence<char> Sequence => new ReadOnlySequence<char>(_value);
            public override long Available => _position == _value.Length ? 0 : -1;
            public override long Length => _byteCount;
            public override bool Rewind()
            {
                _position = 0;
                return true;
            }
            public override long ComputeLength()
            {
                if (_byteCount == -1)
                    _byteCount = _encoding.GetByteCount(_value.Span);

                return _byteCount;
            }
            public override int Read(Span<byte> buffer)
            {
                if (_position == _value.Length)
                    return 0;

                var length = buffer.Length;
                if (length == 0)
                    return 0;

                if (_encoder == null)
                {
                    if ((_byteCount > 0 && _byteCount <= length) || _encoding.GetMaxByteCount(_value.Length) <= length)
                    {
                        var result = _encoding.GetBytes(_value.Span, buffer);
                        _position = _value.Length;
                        return result;
                    }
                    _encoder = _encoding.GetEncoder();
                }
                int charsUsed, bytesUsed;
                unsafe
                {
                    fixed (char* pValue = _value.Span) 
                    fixed (byte* pBytes = buffer)
                    {
                        _encoder.Convert(pValue + _position, _value.Length - _position, pBytes, length, true, out charsUsed, out bytesUsed, out _);
                        _position += charsUsed;
                        return bytesUsed;
                    }
                }
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
            public override string ToString() => new string(_value.Span);
        }
        private class SequenceContent : StringContent
        {
            public SequenceContent(ReadOnlySequence<char> value, Encoding encoding)
            {
                _value = value;
                _encoding = encoding;
                _encoder = encoding.GetEncoder();
            }

            private ReadOnlySequence<char> _value;
            private long _position, _byteCount = -1;
            private Encoder _encoder;
            private Encoding _encoding;
            public override Encoding Encoding => _encoding;
            public override ReadOnlySequence<char> Sequence => _value;
            public override long Available => _position == _value.Length ? 0 : -1;
            public override long Length => _byteCount;
            public override bool Rewind()
            {
                _position = 0;
                return true;
            }
            public override long ComputeLength()
            {
                if (_byteCount == -1)
                    _byteCount = _encoding.GetByteCount(_value);

                return _byteCount;
            }
            public override int Read(Span<byte> buffer)
            {
                if (_position == _value.Length)
                    return 0;

                var length = buffer.Length;
                if (length == 0)
                    return 0;

                Debug.Assert(_encoder != null);
                var bytesSum = 0;
                var position = _value.GetPosition(_position);
                unsafe 
                {
                    fixed (byte* pBytes = buffer)
                    {
                        int charsUsed, bytesUsed;
                        while (_value.TryGet(ref position, out var value))
                        {
                            fixed (char* pValue = value.Span)
                            {
                                _encoder.Convert(pValue, value.Length, pBytes + bytesSum, length - bytesSum, true, out charsUsed, out bytesUsed, out _);
                                _position += charsUsed;
                                bytesSum += bytesUsed;
                                if (charsUsed == value.Length && length - bytesSum > 16) //_encoding.GetMaxByteCount(1)
                                    continue;
                                break;
                            }
                        }
                    }
                }
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
            public override string ToString() => _value.ToString();
        }
        #endregion
    }
}
