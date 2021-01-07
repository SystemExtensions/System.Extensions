
namespace System.Buffers
{
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Reflection.Emit;
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
        public static void Write(this BufferWriter<char> @this, char ch, int repeatCount)
        {
            if (repeatCount <= 0)
                return;

            var charSpan = @this.GetSpan();
            if (charSpan.Length >= repeatCount)
            {
                for (int i = 0; i < repeatCount; i++)
                {
                    charSpan[i] = ch;
                }
                @this.Advance(repeatCount);
            }
            else
            {
                var tempCount = repeatCount;
                for (; ; )
                {
                    var charsToCopy = tempCount < charSpan.Length ? tempCount : charSpan.Length;
                    for (int i = 0; i < charsToCopy; i++)
                    {
                        charSpan[i] = ch;
                    }
                    @this.Advance(charsToCopy);
                    tempCount -= charsToCopy;
                    Debug.Assert(tempCount >= 0);
                    if (tempCount == 0)
                        return;
                    charSpan = @this.GetSpan();
                }
            }
        }
        //TODO? Write<T>(T value) Write<T>(T value,format)
        //TODO? NumberFormatInfo.InvariantInfo
        public static void Write(this BufferWriter<char> @this, byte value)
        {
            var charSpan = @this.GetSpan();
            if (value.TryFormat(charSpan, out var charsWritten))
            {
                @this.Advance(charsWritten);
            }
            else
            {
                Span<char> dest = stackalloc char[3];
                if (value.TryFormat(dest, out charsWritten))
                {
                    @this.Write(dest.Slice(0, charsWritten));
                }
                else
                {
                    @this.Write(value.ToString());
                }
            }
        }
        public static void Write(this BufferWriter<char> @this, sbyte value)
        {
            var charSpan = @this.GetSpan();
            if (value.TryFormat(charSpan, out var charsWritten))
            {
                @this.Advance(charsWritten);
            }
            else
            {
                Span<char> dest = stackalloc char[4];
                if (value.TryFormat(dest, out charsWritten))
                {
                    @this.Write(dest.Slice(0, charsWritten));
                }
                else
                {
                    @this.Write(value.ToString());
                }
            }
        }
        public static void Write(this BufferWriter<char> @this, short value)
        {
            var charSpan = @this.GetSpan();
            if (value.TryFormat(charSpan, out var charsWritten))
            {
                @this.Advance(charsWritten);
            }
            else
            {
                Span<char> dest = stackalloc char[6];
                if (value.TryFormat(dest, out charsWritten))
                {
                    @this.Write(dest.Slice(0, charsWritten));
                }
                else
                {
                    @this.Write(value.ToString());
                }
            }
        }
        public static void Write(this BufferWriter<char> @this, ushort value)
        {
            var charSpan = @this.GetSpan();
            if (value.TryFormat(charSpan, out var charsWritten))
            {
                @this.Advance(charsWritten);
            }
            else
            {
                Span<char> dest = stackalloc char[5];
                if (value.TryFormat(dest, out charsWritten))
                {
                    @this.Write(dest.Slice(0, charsWritten));
                }
                else
                {
                    @this.Write(value.ToString());
                }
            }
        }
        public static void Write(this BufferWriter<char> @this, int value)
        {
            var charSpan = @this.GetSpan();
            if (value.TryFormat(charSpan, out var charsWritten))
            {
                @this.Advance(charsWritten);
            }
            else
            {
                Span<char> dest = stackalloc char[11];
                if (value.TryFormat(dest, out charsWritten))
                {
                    @this.Write(dest.Slice(0, charsWritten));
                }
                else
                {
                    @this.Write(value.ToString());
                }
            }
        }
        public static void Write(this BufferWriter<char> @this, uint value)
        {
            var charSpan = @this.GetSpan();
            if (value.TryFormat(charSpan, out var charsWritten))
            {
                @this.Advance(charsWritten);
            }
            else
            {
                Span<char> dest = stackalloc char[10];
                if (value.TryFormat(dest, out charsWritten))
                {
                    @this.Write(dest.Slice(0, charsWritten));
                }
                else
                {
                    @this.Write(value.ToString());
                }
            }
        }
        public static void Write(this BufferWriter<char> @this, long value)
        {
            var charSpan = @this.GetSpan();
            if (value.TryFormat(charSpan, out var charsWritten))
            {
                @this.Advance(charsWritten);
            }
            else
            {
                Span<char> dest = stackalloc char[20];
                if (value.TryFormat(dest, out charsWritten))
                {
                    @this.Write(dest.Slice(0, charsWritten));
                }
                else
                {
                    @this.Write(value.ToString());
                }
            }
        }
        public static void Write(this BufferWriter<char> @this, ulong value)
        {
            var charSpan = @this.GetSpan();
            if (value.TryFormat(charSpan, out var charsWritten))
            {
                @this.Advance(charsWritten);
            }
            else
            {
                Span<char> dest = stackalloc char[20];
                if (value.TryFormat(dest, out charsWritten))
                {
                    @this.Write(dest.Slice(0, charsWritten));
                }
                else
                {
                    @this.Write(value.ToString());
                }
            }
        }
        public static void Write(this BufferWriter<char> @this, float value)
        {
            var charSpan = @this.GetSpan();
            if (value.TryFormat(charSpan, out var charsWritten))
            {
                @this.Advance(charsWritten);
            }
            else
            {
                Span<char> dest = stackalloc char[24];
                if (value.TryFormat(dest, out charsWritten))
                {
                    @this.Write(dest.Slice(0, charsWritten));
                }
                else
                {
                    @this.Write(value.ToString());
                }
            }
        }
        public static void Write(this BufferWriter<char> @this, double value)
        {
            var charSpan = @this.GetSpan();
            if (value.TryFormat(charSpan, out var charsWritten))
            {
                @this.Advance(charsWritten);
            }
            else
            {
                Span<char> dest = stackalloc char[24];
                if (value.TryFormat(dest, out charsWritten))
                {
                    @this.Write(dest.Slice(0, charsWritten));
                }
                else
                {
                    @this.Write(value.ToString());
                }
            }
        }
        public static void Write(this BufferWriter<char> @this, decimal value)
        {
            var charSpan = @this.GetSpan();
            if (value.TryFormat(charSpan, out var charsWritten))
            {
                @this.Advance(charsWritten);
            }
            else
            {
                Span<char> dest = stackalloc char[24];
                if (value.TryFormat(dest, out charsWritten))
                {
                    @this.Write(dest.Slice(0, charsWritten));
                }
                else
                {
                    @this.Write(value.ToString());
                }
            }
        }
        //TODO? Remove
        public static void Write(this BufferWriter<char> @this, DateTime value)
        {
            var charSpan = @this.GetSpan();
            if (value.TryFormat(charSpan, out var charsWritten))
            {
                @this.Advance(charsWritten);
            }
            else
            {
                Span<char> dest = stackalloc char[32];
                if (value.TryFormat(dest, out charsWritten))
                {
                    @this.Write(dest.Slice(0, charsWritten));
                }
                else
                {
                    @this.Write(value.ToString());
                }
            }
        }
        public static void Write(this BufferWriter<char> @this, DateTime value, string format)
        {
            var charSpan = @this.GetSpan();
            if (value.TryFormat(charSpan, out var charsWritten, format))
            {
                @this.Advance(charsWritten);
            }
            else
            {
                @this.Write(value.ToString(format));
            }
        }
        //TODO? Remove
        public static void Write(this BufferWriter<char> @this, DateTimeOffset value)
        {
            var charSpan = @this.GetSpan();
            if (value.TryFormat(charSpan, out var charsWritten))
            {
                @this.Advance(charsWritten);
            }
            else
            {
                Span<char> dest = stackalloc char[32];
                if (value.TryFormat(dest, out charsWritten))
                {
                    @this.Write(dest.Slice(0, charsWritten));
                }
                else
                {
                    @this.Write(value.ToString());
                }
            }
        }
        public static void Write(this BufferWriter<char> @this, DateTimeOffset value, string format)
        {
            var charSpan = @this.GetSpan();
            if (value.TryFormat(charSpan, out var charsWritten, format))
            {
                @this.Advance(charsWritten);
            }
            else
            {
                @this.Write(value.ToString(format));
            }
        }
        public static void Write(this BufferWriter<char> @this, Guid value)
        {
            var charSpan = @this.GetSpan();
            if (value.TryFormat(charSpan, out var charsWritten))
            {
                @this.Advance(charsWritten);
            }
            else
            {
                Span<char> dest = stackalloc char[36];
                if (value.TryFormat(dest, out charsWritten))
                {
                    @this.Write(dest.Slice(0, charsWritten));
                }
                else
                {
                    @this.Write(value.ToString());
                }
            }
        }
        public static void Write(this BufferWriter<char> @this, Guid value, string format)
        {
            var charSpan = @this.GetSpan();
            if (value.TryFormat(charSpan, out var charsWritten, format))
            {
                @this.Advance(charsWritten);
            }
            else
            {
                @this.Write(value.ToString(format));
            }
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
            public override Encoding Encoding => Encoding.Unicode;
            public override void Write(char value) => _writer.Write(value);
            public override void Write(int value) => _writer.Write(value);
            public override void Write(uint value) => _writer.Write(value);
            public override void Write(long value) => _writer.Write(value);
            public override void Write(ulong value) => _writer.Write(value);
            public override void Write(float value) => _writer.Write(value);
            public override void Write(double value) => _writer.Write(value);
            public override void Write(decimal value) => _writer.Write(value);
            public override void Write(char[] buffer, int index, int count)
            {
                _writer.Write(buffer.AsSpan(index, count));
            }
            public override void Write(ReadOnlySpan<char> buffer)
            {
                _writer.Write(buffer);
            }
            public override void Write(char[] buffer)
            {
                _writer.Write(buffer);
            }
            public override void Write(string value)
            {
                _writer.Write(value);
            }
        }
        #endregion
    }
}
