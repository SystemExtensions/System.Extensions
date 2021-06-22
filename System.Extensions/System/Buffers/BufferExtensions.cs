
namespace System.Buffers
{
    using System.IO;
    using System.Text;
    using System.Reflection.Emit;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    public static class BufferExtensions
    {   
        public static int SizeOf<T>() where T : struct
        {
            return SizeOfType<T>.Value;
        }
        public static TextWriter AsWriter(this BufferWriter<char> @this) 
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            return new _TextWriter(@this);
        }
        public static TextWriter AsWriter(this BufferWriter<char> @this, IFormatProvider formatProvider)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (formatProvider == null)
                throw new ArgumentNullException(nameof(formatProvider));

            return new _TextWriter(@this, formatProvider);
        }
        public static string ToString(this Buffer<char> @this, int startIndex)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            var seq = @this.Sequence.Slice(startIndex);
            var length = checked((int)seq.Length);
            unsafe
            {
                var value = new string('\0', length);
                fixed (char* pDest = value)
                {
                    seq.CopyTo(new Span<char>(pDest, length));
                }
                return value;
            }
        }
        public static string ToString(this Buffer<char> @this, int startIndex, int count)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            var seq = @this.Sequence.Slice(startIndex, count);
            var length = checked((int)seq.Length);
            unsafe
            {
                var value = new string('\0', length);
                fixed (char* pDest = value)
                {
                    seq.CopyTo(new Span<char>(pDest, length));
                }
                return value;
            }
        }
        public static void Write(this BufferWriter<char> @this, byte value, string format = null, IFormatProvider provider = null)
        {
            if (value.TryFormat(@this.GetSpan(), out var charsWritten, format, provider))
            {
                @this.Advance(charsWritten);
            }
            else
            {
                @this.Write(value.ToString(format, provider));
            }
        }
        public static void Write(this BufferWriter<char> @this, sbyte value, string format = null, IFormatProvider provider = null)
        {
            if (value.TryFormat(@this.GetSpan(), out var charsWritten, format, provider))
            {
                @this.Advance(charsWritten);
            }
            else
            {
                @this.Write(value.ToString(format, provider));
            }
        }
        public static void Write(this BufferWriter<char> @this, short value, string format = null, IFormatProvider provider = null)
        {
            if (value.TryFormat(@this.GetSpan(), out var charsWritten, format, provider))
            {
                @this.Advance(charsWritten);
            }
            else
            {
                @this.Write(value.ToString(format, provider));
            }
        }
        public static void Write(this BufferWriter<char> @this, ushort value, string format = null, IFormatProvider provider = null)
        {
            if (value.TryFormat(@this.GetSpan(), out var charsWritten, format, provider))
            {
                @this.Advance(charsWritten);
            }
            else
            {
                @this.Write(value.ToString(format, provider));
            }
        }
        public static void Write(this BufferWriter<char> @this, int value, string format = null, IFormatProvider provider = null)
        {
            if (value.TryFormat(@this.GetSpan(), out var charsWritten, format, provider))
            {
                @this.Advance(charsWritten);
            }
            else
            {
                @this.Write(value.ToString(format, provider));
            }
        }
        public static void Write(this BufferWriter<char> @this, uint value, string format = null, IFormatProvider provider = null)
        {
            if (value.TryFormat(@this.GetSpan(), out var charsWritten, format, provider))
            {
                @this.Advance(charsWritten);
            }
            else
            {
                @this.Write(value.ToString(format, provider));
            }
        }
        public static void Write(this BufferWriter<char> @this, long value, string format = null, IFormatProvider provider = null)
        {
            if (value.TryFormat(@this.GetSpan(), out var charsWritten, format, provider))
            {
                @this.Advance(charsWritten);
            }
            else
            {
                @this.Write(value.ToString(format, provider));
            }
        }
        public static void Write(this BufferWriter<char> @this, ulong value, string format = null, IFormatProvider provider = null)
        {
            if (value.TryFormat(@this.GetSpan(), out var charsWritten, format, provider))
            {
                @this.Advance(charsWritten);
            }
            else
            {
                @this.Write(value.ToString(format, provider));
            }
        }
        public static void Write(this BufferWriter<char> @this, float value, string format = null, IFormatProvider provider = null)
        {
            if (value.TryFormat(@this.GetSpan(), out var charsWritten, format, provider))
            {
                @this.Advance(charsWritten);
            }
            else
            {
                @this.Write(value.ToString(format, provider));
            }
        }
        public static void Write(this BufferWriter<char> @this, double value, string format = null, IFormatProvider provider = null)
        {
            if (value.TryFormat(@this.GetSpan(), out var charsWritten, format, provider))
            {
                @this.Advance(charsWritten);
            }
            else
            {
                @this.Write(value.ToString(format, provider));
            }
        }
        public static void Write(this BufferWriter<char> @this, decimal value, string format = null, IFormatProvider provider = null)
        {
            if (value.TryFormat(@this.GetSpan(), out var charsWritten, format, provider))
            {
                @this.Advance(charsWritten);
            }
            else
            {
                @this.Write(value.ToString(format, provider));
            }
        }
        public static void Write(this BufferWriter<char> @this, DateTime value, string format = null, IFormatProvider provider = null)
        {
            if (value.TryFormat(@this.GetSpan(), out var charsWritten, format, provider))
            {
                @this.Advance(charsWritten);
            }
            else
            {
                @this.Write(value.ToString(format, provider));
            }
        }
        public static void Write(this BufferWriter<char> @this, DateTimeOffset value, string format = null, IFormatProvider provider = null)
        {
            if (value.TryFormat(@this.GetSpan(), out var charsWritten, format, provider))
            {
                @this.Advance(charsWritten);
            }
            else
            {
                @this.Write(value.ToString(format, provider));
            }
        }
        public static void Write(this BufferWriter<char> @this, Guid value, string format = null)
        {
            if (value.TryFormat(@this.GetSpan(), out var charsWritten, format))
            {
                @this.Advance(charsWritten);
            }
            else
            {
                @this.Write(value.ToString(format));
            }
        }
        //TODO? WriteLine
        public static Stream AsStream(this byte[] @this)
        {
            return new _MemoryStream(@this);
        }
        public static Stream AsStream(this Memory<byte> @this)
        {
            return new _MemoryStream(@this);
        }
        public static Stream AsStream(this ReadOnlyMemory<byte> @this)
        {
            return new _MemoryStream(@this);
        }
        public static Stream AsStream(this ReadOnlySequence<byte> @this)
        {
            return new SequenceStream(@this);
        }
        #region private
        private static class SizeOfType<T> where T : struct
        {
            static SizeOfType()
            {
                var sizeOfType = new DynamicMethod("SizeOfType", typeof(int), Type.EmptyTypes);
                var il = sizeOfType.GetILGenerator();
                il.Emit(OpCodes.Sizeof, typeof(T));
                il.Emit(OpCodes.Ret);
                Value = (int)sizeOfType.Invoke(null, null);
            }

            public static readonly int Value;
        }
        private class _TextWriter : TextWriter
        {
            private BufferWriter<char> _writer;
            public _TextWriter(BufferWriter<char> writer) 
            {
                _writer = writer;
                GC.SuppressFinalize(this);
            }
            public _TextWriter(BufferWriter<char> writer, IFormatProvider formatProvider) 
                : base(formatProvider)
            {
                _writer = writer;
                GC.SuppressFinalize(this);
            }
            public override Encoding Encoding => Encoding.Unicode;
            public override void Write(char value) => _writer.Write(value);
            public override void Write(int value) => _writer.Write(value, provider: FormatProvider);
            public override void Write(uint value) => _writer.Write(value, provider: FormatProvider);
            public override void Write(long value) => _writer.Write(value, provider: FormatProvider);
            public override void Write(ulong value) => _writer.Write(value, provider: FormatProvider);
            public override void Write(float value) => _writer.Write(value, provider: FormatProvider);
            public override void Write(double value) => _writer.Write(value, provider: FormatProvider);
            public override void Write(decimal value) => _writer.Write(value, provider: FormatProvider);
            public override void Write(char[] buffer, int index, int count) => _writer.Write(buffer.AsSpan(index, count));
            public override void Write(ReadOnlySpan<char> buffer) => _writer.Write(buffer);
            public override void Write(char[] buffer) => _writer.Write(buffer);
            public override void Write(string value) => _writer.Write(value);
            public override string ToString() => _writer.ToString();
        }
        private class _MemoryStream : Stream
        {
            public _MemoryStream(ReadOnlyMemory<byte> bytes)
            {
                _bytes = bytes;
                _count = bytes.Length;
                GC.SuppressFinalize(this);//TODO????
            }
            private ReadOnlyMemory<byte> _bytes;
            private int _count;
            private int _position;
            public override bool CanRead => true;
            public override bool CanWrite => false;
            public override bool CanSeek => true;
            public override long Length => _count;
            public override long Position { get => _position; set => _position = (int)value; }
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
            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                return new ValueTask<int>(Read(buffer.Span));
            }
            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return Task.FromResult(Read(buffer.AsSpan(offset, count)));
            }
            public override long Seek(long offset, SeekOrigin origin)
            {
                var position = _position;
                switch (origin)
                {
                    case SeekOrigin.Begin:
                        position = (int)offset;
                        break;
                    case SeekOrigin.Current:
                        position += (int)offset;
                        break;
                    case SeekOrigin.End:
                        position = _count + (int)offset;
                        break;
                    default:
                        throw new NotSupportedException(nameof(origin));
                }

                if (position < 0 || position >= _count)
                    throw new ArgumentOutOfRangeException(nameof(offset));

                _position = position;
                return position;
            }
            public override void SetLength(long value) => throw new NotSupportedException(nameof(SetLength));
            public override void Flush()
            {

            }
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException(nameof(Write));
        }
        private class SequenceStream : Stream
        {
            public SequenceStream(ReadOnlySequence<byte> bytes)
            {
                _bytes = bytes;
                _count = bytes.Length;
                GC.SuppressFinalize(this);//TODO????
            }
            private ReadOnlySequence<byte> _bytes;
            private long _count;
            private long _position;
            public override bool CanRead => true;
            public override bool CanWrite => false;
            public override bool CanSeek => true;
            public override long Length => _count;
            public override long Position { get => _position; set => _position = value; }
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
            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                return new ValueTask<int>(Read(buffer.Span));
            }
            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return Task.FromResult(Read(buffer.AsSpan(offset, count)));
            }
            public override long Seek(long offset, SeekOrigin origin)
            {
                var position = _position;
                switch (origin)
                {
                    case SeekOrigin.Begin:
                        position = offset;
                        break;
                    case SeekOrigin.Current:
                        position += offset;
                        break;
                    case SeekOrigin.End:
                        position = _count + offset;
                        break;
                    default:
                        throw new NotSupportedException(nameof(origin));
                }

                if (position < 0 || position >= _count)
                    throw new ArgumentOutOfRangeException(nameof(offset));

                _position = position;
                return position;
            }
            public override void SetLength(long value) => throw new NotSupportedException(nameof(SetLength));
            public override void Flush()
            {

            }
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException(nameof(Write));
        }
        #endregion

        //public static void Write(this BufferWriter<char> @this, char ch, int repeatCount)
        //{
        //    if (repeatCount <= 0)
        //        return;

        //    var charSpan = @this.GetSpan();
        //    if (charSpan.Length >= repeatCount)
        //    {
        //        for (int i = 0; i < repeatCount; i++)
        //        {
        //            charSpan[i] = ch;
        //        }
        //        @this.Advance(repeatCount);
        //    }
        //    else
        //    {
        //        var tempCount = repeatCount;
        //        for (; ; )
        //        {
        //            var charsToCopy = tempCount < charSpan.Length ? tempCount : charSpan.Length;
        //            for (int i = 0; i < charsToCopy; i++)
        //            {
        //                charSpan[i] = ch;
        //            }
        //            @this.Advance(charsToCopy);
        //            tempCount -= charsToCopy;
        //            Debug.Assert(tempCount >= 0);
        //            if (tempCount == 0)
        //                return;
        //            charSpan = @this.GetSpan();
        //        }
        //    }
        //}
    }
}
