
namespace System
{
    using System.Buffers;
    using System.Diagnostics;
    public static class StringExtensions
    {
        //public const int Large = 40 << 10;
        public static bool Equals(this string @this, ReadOnlySpan<char> value)
        {
            return @this.AsSpan().Equals(value, StringComparison.Ordinal);
        }
        public static bool EqualsIgnoreCase(this string @this, string value)
        {
            if (@this == null)
                return value == null;

            return @this.Equals(value, StringComparison.OrdinalIgnoreCase);
        }
        public static bool EqualsIgnoreCase(this string @this, ReadOnlySpan<char> value)
        {
            return @this.AsSpan().Equals(value, StringComparison.OrdinalIgnoreCase);
        }
        public static bool EqualsIgnoreCase(this ReadOnlySpan<char> @this, ReadOnlySpan<char> value)
        {
            return @this.Equals(value, StringComparison.OrdinalIgnoreCase);
        }
        public static bool EqualsIgnoreCaseWhiteSpace(this string @this, string value)
        {
            if (ReferenceEquals(@this, value))
                return true;

            return EqualsIgnoreCaseWhiteSpace(@this.AsSpan(), value.AsSpan());
        }
        public static bool EqualsIgnoreCaseWhiteSpace(this ReadOnlySpan<char> @this, ReadOnlySpan<char> value)
        {
            if (@this.Length == value.Length)
                @this.Equals(value, StringComparison.OrdinalIgnoreCase);

            return @this.Trim().Equals(value.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        //TODO? EqualsIgnoreWhiteSpace
        public static string Concat(ReadOnlySpan<char> arg0, ReadOnlySpan<char> arg1)
        {
            var length = arg0.Length + arg1.Length;
            if (length == 0)
                return string.Empty;

            var value = new string('\0', length);
            unsafe
            {
                fixed (char* pDest = value)
                {
                    var destSpan = new Span<char>(pDest, length);
                    arg0.CopyTo(destSpan);
                    arg1.CopyTo(destSpan.Slice(arg0.Length));
                }
            }
            return value;
        }
        public static string Concat(ReadOnlySpan<char> arg0, ReadOnlySpan<char> arg1, ReadOnlySpan<char> arg2)
        {
            var length = arg0.Length + arg1.Length + arg2.Length;
            if (length == 0)
                return string.Empty;

            var value = new string('\0', length);
            unsafe
            {
                fixed (char* pDest = value)
                {
                    var destSpan = new Span<char>(pDest, length);
                    arg0.CopyTo(destSpan);
                    arg1.CopyTo(destSpan.Slice(arg0.Length));
                    arg2.CopyTo(destSpan.Slice(arg0.Length + arg1.Length));
                }
            }
            return value;
        }
        public static string Concat(ReadOnlySpan<char> arg0, ReadOnlySpan<char> arg1, ReadOnlySpan<char> arg2, ReadOnlySpan<char> arg3)
        {
            var length = arg0.Length + arg1.Length + arg2.Length + arg3.Length;
            if (length == 0)
                return string.Empty;

            var value = new string('\0', length);
            unsafe
            {
                fixed (char* pDest = value)
                {
                    var destSpan = new Span<char>(pDest, length);
                    arg0.CopyTo(destSpan);
                    arg1.CopyTo(destSpan.Slice(arg0.Length));
                    arg2.CopyTo(destSpan.Slice(arg0.Length + arg1.Length));
                    arg3.CopyTo(destSpan.Slice(arg0.Length + arg1.Length + arg2.Length));
                }
            }
            return value;
        }
        public static T[] Split<T>(this string @this, char separator, StringSplitOptions options = StringSplitOptions.RemoveEmptyEntries)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            var values = @this.Split(separator, options);
            var length = values.Length;
            var result = new T[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = Converter.Convert<string, T>(values[i]);
            }
            return result;
        }
        public static T[] Split<T>(this string @this, string separator, StringSplitOptions options = StringSplitOptions.RemoveEmptyEntries)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            var values = @this.Split(separator, options);
            var length = values.Length;
            var result = new T[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = Converter.Convert<string, T>(values[i]);
            }
            return result;
        }
        public static string ToSnakeCase(string name)
        {
            if (name == null)
                return null;
            var length = name.Length;
            if (length == 0)
                return string.Empty;
            if (length == 1)
                return name.ToLower();
            var sb = ThreadRent(out var disposable);
            try
            {
                sb.Write(char.ToLower(name[0]));
                var pre = char.ToUpper(name[0]);//上一个强制转为大写
                for (int i = 1; i < length;)//AsabcUIDName uid_name  StringID string_id;  UID-name
                {
                    var temp = name[i++];
                    if (temp >= 'A' && temp <= 'Z')//Upper
                    {
                        if (pre >= 'a' && pre <= 'z') //自己是大写 上一个是小写字符 自己前边加-
                        {
                            sb.Write('_');

                        }
                        else if (pre >= 'A' && pre <= 'Z' && i < length)//自己是大写 上一个是大写 后一个是小写 自己前边加-
                        {
                            if (name[i] >= 'a' && name[i] <= 'z')
                            {
                                sb.Write('_');
                            }
                        }
                    }
                    sb.Write(char.ToLower(temp));
                    pre = temp;
                }
                return sb.ToString();
            }
            finally
            {
                disposable.Dispose();
            }
        }
        public static string ToByteString(this ReadOnlySpan<byte> @this)
        {
            var length = @this.Length;
            if (length == 0)
                return string.Empty;

            var @string = new string('\0', length);
            unsafe
            {
                fixed (byte* pBytes = @this)
                fixed (char* pData = @string)
                {
                    var pSrc = pBytes;
                    var pDest = pData;
                    while (length >= 4)
                    {
                        pDest[0] = (char)pSrc[0];
                        pDest[1] = (char)pSrc[1];
                        pDest[2] = (char)pSrc[2];
                        pDest[3] = (char)pSrc[3];
                        pSrc += 4;
                        pDest += 4;
                        length -= 4;
                    }
                    while (length > 0)
                    {
                        pDest[0] = (char)pSrc[0];
                        pSrc += 1;
                        pDest += 1;
                        length -= 1;
                    }
                }
            }
            return @string;
        }
        public static string ToByteString(this byte[] @this)
        {
            return ToByteString(new ReadOnlySpan<byte>(@this));
        }
        public static string ToByteString(this Span<byte> @this)
        {
            return ToByteString((ReadOnlySpan<byte>)@this);
        }
        public static bool EqualsByteString(this ReadOnlySpan<byte> @this, string value)
        {
            var length = @this.Length;
            if (string.IsNullOrEmpty(value))
                return length == 0;
            if (length != value.Length)
                return false;

            unsafe
            {
                fixed (byte* pBytes = @this)
                fixed(char* pString=value)
                {
                    var pA = pBytes;
                    var pB = pString;
                    while (length >= 4)
                    {
                        if (pA[0] != pB[0] || pA[1] != pB[1] || pA[2] != pB[2] || pA[3] != pB[3])
                            return false;
                        pA += 4;
                        pB += 4;
                        length -= 4;
                    }
                    while (length > 0)
                    {
                        if (pA[0] != pB[0])
                            return false;
                        pA += 1;
                        pB += 1;
                        length -= 1;
                    }
                    return true;
                }
            }
        }
        public static Buffer<char> ThreadRent(out IDisposable disposable)
        {
            var buffer = _InstanceA;
            if (buffer == null)
            {
                buffer = new CharBuffer();
                _InstanceA = buffer;
                disposable = buffer;
                return buffer.Buffer;
            }
            else if (buffer.Available)
            {
                buffer.Available = false;
                disposable = buffer;
                return buffer.Buffer;
            }
            buffer = _InstanceB;
            if (buffer == null)
            {
                buffer = new CharBuffer();
                _InstanceB = buffer;
                disposable = buffer;
                return buffer.Buffer;
            }
            else if (buffer.Available)
            {
                buffer.Available = false;
                disposable = buffer;
                return buffer.Buffer;
            }
            return Rent(out disposable);
        }
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
            disposable = Disposable.Empty;
            return Buffer<char>.Create(32);
        }

        #region private
        [ThreadStatic] private static CharBuffer _InstanceA;
        [ThreadStatic] private static CharBuffer _InstanceB;
        private class CharBuffer : IDisposable
        {
            //public int ManagedThreadId = Threading.Thread.CurrentThread.ManagedThreadId;
            public Buffer<char> Buffer = Buffer<char>.Create(4096, ArrayPool<char>.Shared, 4096, out _);//TODO?? Factory
            public bool Available;
            public void Dispose()
            {
                //Debug.Assert(ManagedThreadId == Threading.Thread.CurrentThread.ManagedThreadId);
                Debug.Assert(!Available);
                Buffer.Clear();
                Available = true;
            }
        }

        private static Provider<Buffer<char>> _Provider = Provider<Buffer<char>>.CreateFromProcessor(() => Buffer<char>.Create(4096, ArrayPool<char>.Shared, 4096, out _), 64, (sb) => sb.Clear());
        #endregion
    }
}
