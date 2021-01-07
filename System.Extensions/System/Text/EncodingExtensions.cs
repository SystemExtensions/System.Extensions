
namespace System.Text
{
    using System.Buffers;
    using System.Diagnostics;
    public static class EncodingExtensions
    {
        public static byte[] GetBytes(this Encoding @this, ReadOnlySpan<char> chars)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            if (chars.IsEmpty)
                return Array.Empty<byte>();

            var bytes = new byte[@this.GetByteCount(chars)];
            var result = @this.GetBytes(chars, bytes);
            Debug.Assert(result == bytes.Length);
            return bytes;
        }
        public static byte[] GetBytes(this Encoding @this, ReadOnlySequence<char> chars)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            if (chars.IsSingleSegment)
                return GetBytes(@this, chars.First.Span);

            var bytes = new byte[@this.GetByteCount(chars)];
            var tempBytes = bytes.AsSpan();
            foreach (var segm in chars)
            {
                var result = @this.GetBytes(segm.Span, tempBytes);
                tempBytes = tempBytes.Slice(result);
            }
            return bytes;
        }
        public static int GetBytes(this Encoding @this, ReadOnlySequence<char> chars, Span<byte> bytes)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            var length = chars.Length;
            if (length == 0)
                return 0;

            if (chars.IsSingleSegment)
                return @this.GetBytes(chars.First.Span, bytes);

            var count = 0;
            var tempBytes = bytes;
            foreach (var segm in chars)
            {
                var result= @this.GetBytes(segm.Span, tempBytes);
                count += result;
                tempBytes = tempBytes.Slice(result);
            }
            return count;
        }
        public static int GetChars(this Encoding @this, ReadOnlySequence<byte> bytes, Span<char> chars)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            var length = bytes.Length;
            if (length == 0)
                return 0;

            if (bytes.IsSingleSegment)
                return @this.GetChars(bytes.First.Span, chars);

            var decoder = @this.GetDecoder();
            var count = 0;
            var tempChars = chars;
            foreach (var segm in bytes)
            {
                var result = decoder.GetChars(segm.Span, tempChars, false);
                count += result;
                tempChars = tempChars.Slice(result);
            }
            return count;
        }
        public static int GetByteCount(this Encoding @this, ReadOnlySequence<char> chars)
        {
            if (@this == null || chars.IsEmpty)
                return 0;
            if (chars.IsSingleSegment)
                return @this.GetByteCount(chars.First.Span);
            var sum = 0;
            foreach (var segm in chars)
            {
                sum += @this.GetByteCount(segm.Span);
            }
            return sum;
        }
        public static int GetCharCount(this Encoding @this, ReadOnlySequence<byte> bytes)
        {
            //https://github.com/dotnet/corefx/issues/34392
            //BUG
            //if (@this == null || bytes.IsEmpty)
            //    return 0;
            //if (bytes.IsSingleSegment)
            //    return @this.GetCharCount(bytes.First.Span);

            //var decoder = GetDecoder(@this);
            //var sum = 0;
            //foreach (var segm in bytes)
            //{
            //    sum += decoder.GetCharCount(segm.Span, false);
            //}
            //return sum;

            if (bytes.IsSingleSegment)
                return @this.GetCharCount(bytes.First.Span);

            unsafe
            {
                var decoder = @this.GetDecoder();
                var seq = bytes.GetEnumerator();
                seq.MoveNext();
                var bytesSpan = seq.Current.Span;
                Span<char> charsSpan = stackalloc char[128];
                var charCount = 0;
                bool completed; int charsUsed, bytesUsed;
                for (; ; )
                {
                    decoder.Convert(bytesSpan, charsSpan, false, out bytesUsed, out charsUsed, out completed);
                    bytesSpan = bytesSpan.Slice(bytesUsed);
                    charCount += charsUsed;
                    if (bytesSpan.Length == 0)
                    {
                        if (!seq.MoveNext())
                            break;
                        bytesSpan = seq.Current.Span;
                    }
                }
                return charCount;
            }
        }
        public static string GetString(this Encoding @this, ReadOnlySequence<byte> bytes)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            if (bytes.IsEmpty)
                return string.Empty;
            if (bytes.IsSingleSegment)
                return @this.GetString(bytes.First.Span);

            var sb = StringExtensions.ThreadRent(out var disposable);
            try
            {
                sb.WriteBytes(bytes, @this);

                return sb.ToString();
            }
            finally
            {
                disposable.Dispose();
            }
        }
        public static string GetString(this Encoding @this, ReadOnlySpan<char> byteString)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            var length = byteString.Length;
            if (length == 0)
                return string.Empty;

            if (length > 1024)
            {
                var bytes = new byte[length];
                for (int i = 0; i < length; i++)
                {
                    bytes[i] = (byte)byteString[i];
                }
                return @this.GetString(bytes);
            }
            else
            {
                //use stackalloc
                Span<byte> bytes = stackalloc byte[length];
                for (int i = 0; i < length; i++)
                {
                    bytes[i] = (byte)byteString[i];
                }
                return @this.GetString(bytes);
            }
        }
        public static int WriteBytes(this BufferWriter<char> @this, ReadOnlySpan<byte> bytes, Encoding encoding)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            if (bytes.IsEmpty)
                return 0;

            var decoder = encoding.GetDecoder();
            var bytesSpan = bytes;
            var charsSpan = @this.GetSpan();
            var charCount = 0;
            bool completed; int charsUsed, bytesUsed;
            for (; ; )
            {
                decoder.Convert(bytesSpan, charsSpan, bytesSpan.IsEmpty, out bytesUsed, out charsUsed, out completed);
                bytesSpan = bytesSpan.Slice(bytesUsed);
                charsSpan = charsSpan.Slice(charsUsed);

                charCount += charsUsed;
                @this.Advance(charsUsed);
                if (bytesSpan.IsEmpty && completed)
                    break;

                if (charsSpan.IsEmpty)
                    charsSpan = @this.GetSpan();
            }
            return charCount;
        }
        public static int WriteBytes(this BufferWriter<char> @this, ReadOnlySequence<byte> bytes, Encoding encoding)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            if (bytes.IsSingleSegment)
                return WriteBytes(@this, bytes.First.Span, encoding);

            var decoder = encoding.GetDecoder();
            var seq = bytes.GetEnumerator();
            seq.MoveNext();
            var bytesSpan = seq.Current.Span;
            var charsSpan = @this.GetSpan();
            var charCount = 0;
            bool completed; int charsUsed, bytesUsed;
            for (; ; )
            {
                decoder.Convert(bytesSpan, charsSpan, bytesSpan.IsEmpty, out bytesUsed, out charsUsed, out completed);
                bytesSpan = bytesSpan.Slice(bytesUsed);
                charsSpan = charsSpan.Slice(charsUsed);

                charCount += charsUsed;
                @this.Advance(charsUsed);
                if (bytesSpan.IsEmpty)
                {
                    if (seq.MoveNext())
                    {
                        bytesSpan = seq.Current.Span;
                    }
                    else if (completed)
                    {
                        break;
                    }
                }
                if (charsSpan.IsEmpty)
                    charsSpan = @this.GetSpan();
            }
            return charCount;
        }
        public static void WriteBytes(this BufferWriter<char> @this, ReadOnlySpan<byte> bytes, bool flush, Decoder decoder)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (decoder == null)
                throw new ArgumentNullException(nameof(decoder));

            var bytesSpan = bytes;
            var charsSpan = @this.GetSpan();
            bool completed; int charsUsed, bytesUsed;
            for (; ; )
            {
                decoder.Convert(bytesSpan, charsSpan, flush, out bytesUsed, out charsUsed, out completed);
                bytesSpan = bytesSpan.Slice(bytesUsed);
                charsSpan = charsSpan.Slice(charsUsed);
                @this.Advance(charsUsed);
                if (bytesSpan.IsEmpty && (!flush || completed))
                    return;

                if (charsSpan.IsEmpty)
                    charsSpan = @this.GetSpan();
            }
        }
        public static unsafe void WriteBytes(this BufferWriter<char> @this, byte* bytes, int byteCount, bool flush, Decoder decoder)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (decoder == null)
                throw new ArgumentNullException(nameof(decoder));
            if (byteCount < 0)
                throw new ArgumentOutOfRangeException(nameof(byteCount));

            var pData = bytes;
            bool completed; int charsUsed, bytesUsed;
            for (; ; )
            {
                var span = @this.GetSpan();
                fixed (char* pDest = span)
                {
                    decoder.Convert(pData, byteCount, pDest, span.Length, flush, out bytesUsed, out charsUsed, out completed);
                    @this.Advance(charsUsed);
                    byteCount -= bytesUsed;
                    pData += bytesUsed;

                    if (byteCount == 0 && (!flush || completed))
                        return;
                }
            }
        }
        public static int WriteChars(this BufferWriter<byte> @this, ReadOnlySpan<char> chars, Encoding encoding)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            if (chars.IsEmpty)
                return 0;

            const int _Size = 16;
            var encoder = encoding.GetEncoder();
            var charsSpan = chars;
            var bytesSpan = @this.GetSpan(_Size);
            var byteCount = 0;
            bool completed; int charsUsed, bytesUsed;
            for (; ; )
            {
                encoder.Convert(charsSpan, bytesSpan, charsSpan.IsEmpty, out charsUsed, out bytesUsed, out completed);
                charsSpan = charsSpan.Slice(charsUsed);
                bytesSpan = bytesSpan.Slice(bytesUsed);

                byteCount += bytesUsed;
                @this.Advance(bytesUsed);
                if (charsSpan.IsEmpty && completed)
                    break;

                if (bytesSpan.Length < _Size)
                    bytesSpan = @this.GetSpan(_Size);
            }
            return byteCount;
        }
        public static int WriteChars(this BufferWriter<byte> @this, ReadOnlySequence<char> chars, Encoding encoding)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            if (chars.IsSingleSegment)
                return WriteChars(@this, chars.First.Span, encoding);

            const int _Size = 16;
            var encoder = encoding.GetEncoder();
            var seq = chars.GetEnumerator();
            seq.MoveNext();
            var charsSpan = seq.Current.Span;
            var bytesSpan = @this.GetSpan(_Size);
            var byteCount = 0;
            bool completed; int charsUsed, bytesUsed;
            for (; ; )
            {
                encoder.Convert(charsSpan, bytesSpan, bytesSpan.IsEmpty, out charsUsed, out bytesUsed, out completed);
                charsSpan = charsSpan.Slice(charsUsed);
                bytesSpan = bytesSpan.Slice(bytesUsed);

                byteCount += bytesUsed;
                @this.Advance(bytesUsed);
                if (charsSpan.IsEmpty)
                {
                    if (seq.MoveNext())
                    {
                        charsSpan = seq.Current.Span;
                    }
                    else if (completed) 
                    {
                        break;
                    }
                }
                if (bytesSpan.Length < _Size)
                    bytesSpan = @this.GetSpan(_Size);
            }
            return byteCount;
        }
        public static void WriteChars(this BufferWriter<byte> @this, ReadOnlySpan<char> chars, bool flush, Encoder encoder)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (encoder == null)
                throw new ArgumentNullException(nameof(encoder));

            const int _Size = 16;
            var charsSpan = chars;
            var bytesSpan = @this.GetSpan(_Size);
            bool completed; int charsUsed, bytesUsed;
            for (; ; )
            {
                encoder.Convert(charsSpan, bytesSpan, flush, out charsUsed, out bytesUsed, out completed);
                charsSpan = charsSpan.Slice(charsUsed);
                bytesSpan = bytesSpan.Slice(bytesUsed);
                @this.Advance(bytesUsed);
                if (charsSpan.IsEmpty && (!flush || completed))
                    return;

                if (bytesSpan.Length < _Size)
                    bytesSpan = @this.GetSpan(_Size);
            }
        }
        public static unsafe void WriteChars(this BufferWriter<byte> @this, char* chars, int charCount, bool flush, Encoder encoder)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (encoder == null)
                throw new ArgumentNullException(nameof(encoder));
            if (charCount < 0)
                throw new ArgumentOutOfRangeException(nameof(charCount));

            const int _Size = 16;
            var pData = chars;
            bool completed; int charsUsed, bytesUsed;
            for (; ; )
            {
                var span = @this.GetSpan(_Size);
                fixed (byte* pDest = span)
                {
                    encoder.Convert(pData, charCount, pDest, span.Length, flush, out charsUsed, out bytesUsed, out completed);
                    @this.Advance(bytesUsed);
                    charCount -= charsUsed;
                    pData += charsUsed;

                    if (charCount == 0 && (!flush || completed))
                        return;
                }
            }
        }
        public static int WriteByteString(this BufferWriter<byte> @this, ReadOnlySpan<char> chars)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            for (var pos = 0; ;)
            {
                var toCopy = chars.Length - pos;
                if (toCopy <= 0)
                    return chars.Length;

                var charSpan = @this.GetSpan();
                if (toCopy > charSpan.Length)
                    toCopy = charSpan.Length;

                for (int i = 0; i < toCopy; i++)
                {
                    var temp = chars[pos++];
                    charSpan[i] = (byte)temp;
                }
                @this.Advance(toCopy);
            }
        }
    }
}
