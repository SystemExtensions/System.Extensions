
namespace System.Extensions
{
    using System.Text;
    using System.Runtime.InteropServices;
    public class Url
    {
        static Url()
        {
            _IsSafeChar['A'] = true;
            _IsSafeChar['B'] = true;
            _IsSafeChar['C'] = true;
            _IsSafeChar['D'] = true;
            _IsSafeChar['E'] = true;
            _IsSafeChar['F'] = true;
            _IsSafeChar['G'] = true;
            _IsSafeChar['H'] = true;
            _IsSafeChar['I'] = true;
            _IsSafeChar['J'] = true;
            _IsSafeChar['K'] = true;
            _IsSafeChar['L'] = true;
            _IsSafeChar['M'] = true;
            _IsSafeChar['N'] = true;
            _IsSafeChar['O'] = true;
            _IsSafeChar['P'] = true;
            _IsSafeChar['Q'] = true;
            _IsSafeChar['R'] = true;
            _IsSafeChar['S'] = true;
            _IsSafeChar['T'] = true;
            _IsSafeChar['U'] = true;
            _IsSafeChar['V'] = true;
            _IsSafeChar['W'] = true;
            _IsSafeChar['X'] = true;
            _IsSafeChar['Y'] = true;
            _IsSafeChar['Z'] = true;

            _IsSafeChar['a'] = true;
            _IsSafeChar['b'] = true;
            _IsSafeChar['c'] = true;
            _IsSafeChar['d'] = true;
            _IsSafeChar['e'] = true;
            _IsSafeChar['f'] = true;
            _IsSafeChar['g'] = true;
            _IsSafeChar['h'] = true;
            _IsSafeChar['i'] = true;
            _IsSafeChar['j'] = true;
            _IsSafeChar['k'] = true;
            _IsSafeChar['l'] = true;
            _IsSafeChar['m'] = true;
            _IsSafeChar['n'] = true;
            _IsSafeChar['o'] = true;
            _IsSafeChar['p'] = true;
            _IsSafeChar['q'] = true;
            _IsSafeChar['r'] = true;
            _IsSafeChar['s'] = true;
            _IsSafeChar['t'] = true;
            _IsSafeChar['u'] = true;
            _IsSafeChar['v'] = true;
            _IsSafeChar['w'] = true;
            _IsSafeChar['x'] = true;
            _IsSafeChar['y'] = true;
            _IsSafeChar['z'] = true;

            _IsSafeChar['0'] = true;
            _IsSafeChar['1'] = true;
            _IsSafeChar['2'] = true;
            _IsSafeChar['3'] = true;
            _IsSafeChar['4'] = true;
            _IsSafeChar['5'] = true;
            _IsSafeChar['6'] = true;
            _IsSafeChar['7'] = true;
            _IsSafeChar['8'] = true;
            _IsSafeChar['9'] = true;

            _IsSafeChar['-'] = true;
            _IsSafeChar['_'] = true;
            _IsSafeChar['.'] = true;
            _IsSafeChar['~'] = true;
        }

        #region const
        private static bool[] _IsSafeChar = new bool[128];
        private const int _MaxStackSize = 160;
        public const string SchemeHttp = "http";
        public const string SchemeHttps = "https";
        public const string SchemeDelimiter = "://";
        public const char UserInfoDelimiter = '@';
        public const char PortDelimiter = ':';
        public const char PathDelimiter = '/';
        public const char QueryDelimiter = '?';
        public const char FragmentDelimiter = '#';
        #endregion

        #region Utf8Decoder
        [ThreadStatic]
        private static Decoder _Utf8Decoder;
        private static Decoder GetUtf8Decoder()
        {
            var decoder = _Utf8Decoder;
            if (decoder == null)
            {
                decoder = Encoding.UTF8.GetDecoder();
                _Utf8Decoder = decoder;
            }
            decoder.Reset();
            return decoder;
        }
        #endregion

        #region private
        private ReadOnlyMemory<char> _schemeSource;
        private ReadOnlyMemory<char> _userInfoSource;
        private ReadOnlyMemory<char> _hostSource;
        private ReadOnlyMemory<char> _pathSource;
        private ReadOnlyMemory<char> _querySource;
        private ReadOnlyMemory<char> _fragmentSource;
        private string _scheme;
        private string _userInfo;
        private string _host;
        private int? _port;
        private string _path;
        private string _query;
        private string _fragment;
        #endregion
        public struct Components
        {
            private Url @this;
            public Components(Url url)
            {
                @this = url;
            }
            public ReadOnlyMemory<char> Scheme
            {
                get => @this._schemeSource;
                set
                {
                    @this._scheme = null;
                    @this._schemeSource = value;
                }
            }
            public ReadOnlyMemory<char> UserInfo
            {
                get => @this._userInfoSource;
                set
                {
                    @this._userInfo = null;
                    @this._userInfoSource = value;
                }
            }
            public ReadOnlyMemory<char> Host
            {
                get => @this._hostSource;
                set
                {
                    @this._host = null;
                    @this._hostSource = value;
                }
            }
            public ReadOnlyMemory<char> Path
            {
                get => @this._pathSource;
                set
                {
                    @this._path = null;
                    @this._pathSource = value;
                }
            }
            public ReadOnlyMemory<char> Query
            {
                get => @this._querySource;
                set
                {
                    @this._query = null;
                    @this._querySource = value;
                }
            }
            public ReadOnlyMemory<char> Fragment
            {
                get => @this._fragmentSource;
                set
                {
                    @this._fragment = null;
                    @this._fragmentSource = value;
                }
            }
        }
        public Url()
        { }
        public Url(string urlString)
        {

        }
        public string Scheme
        {
            get
            {
                if (_scheme == null)
                    _scheme = _schemeSource.ToString();

                return _scheme;
            }
            set
            {
                _scheme = value == null ? string.Empty : value;
                _schemeSource = value.AsMemory();
            }
        }
        public string UserInfo
        {
            get
            {
                if (_userInfo == null)
                    _userInfo = _userInfoSource.ToString();

                return _userInfo;
            }
            set
            {
                _userInfo = value == null ? string.Empty : value;
                _userInfoSource = value.AsMemory();
            }
        }
        public string Host
        {
            get
            {
                if (_host == null)
                    _host = _hostSource.ToString();

                return _host;
            }
            set
            {
                _host = value == null ? string.Empty : value;
                _hostSource = value.AsMemory();
            }
        }
        public int? Port
        {
            get { return _port; }

            set
            {
                if (value <= 0 || value > ushort.MaxValue)
                    throw new ArgumentOutOfRangeException(nameof(Port));

                _port = value;
            }
        }
        public string Path
        {
            get
            {
                if (_path == null)
                    _path = _pathSource.ToString();

                return _path;
            }
            set
            {
                _path = value == null ? string.Empty : value;
                _pathSource = value.AsMemory();
            }
        }
        public string Query
        {
            get
            {
                if (_query == null)
                    _query = _querySource.ToString();

                return _query;
            }
            set
            {
                _query = value == null ? string.Empty : value;
                _querySource = value.AsMemory();
            }
        }
        public string Fragment
        {
            get
            {
                if (_fragment == null)
                    _fragment = _fragmentSource.ToString();

                return _fragment;
            }
            set
            {
                _fragment = value == null ? string.Empty : value;
                _fragmentSource = value.AsMemory();
            }
        }
        public override string ToString()
        {
            var schemeSource = _schemeSource;
            var userInfoSource = _userInfoSource;
            var hostSource = _hostSource;
            var port = _port;
            var pathSource = _pathSource;
            var querySource = _querySource;
            var fragmentSource = _fragmentSource;

            var sb = StringExtensions.Rent();
            if (schemeSource.Length > 0)
            {
                sb.Append(schemeSource.Span);
                sb.Append(SchemeDelimiter);
            }
            if (userInfoSource.Length > 0)
            {
                sb.Append(userInfoSource.Span);
                sb.Append(UserInfoDelimiter);
            }
            if (hostSource.Length > 0)
            {
                sb.Append(hostSource.Span);
            }
            if (port.HasValue)
            {
                sb.Append(PortDelimiter);
                sb.Append(port.Value);
            }
            sb.Append(pathSource.Span);
            sb.Append(querySource.Span);
            sb.Append(fragmentSource.Span);
            return StringExtensions.GetReturn(sb);
        }
        public static string Encode(string stringToEncode)
        {
            if (stringToEncode == null)
                throw new ArgumentNullException(nameof(stringToEncode));

            var length = stringToEncode.Length;
            if (length == 0)
                return string.Empty;
            int safeCount = 0;
            unsafe
            {
                fixed (char* pSrc = stringToEncode)
                {
                    for (int i = 0; i < length; i++)
                    {
                        var temp = pSrc[i];
                        if (temp < 128 && _IsSafeChar[temp])
                            safeCount++;
                    }
                    if (safeCount == length)
                        return stringToEncode;
                    int byteCount = Encoding.UTF8.GetByteCount(pSrc,length);
                    var value = new string('\0', byteCount + (byteCount - safeCount) * 2);//除了单字节外，多字节UTF-8码的后续字节均以10开头。
                    fixed (char* pDest = value)
                    {
                        var bytes = (byte*)pDest + (value.Length * 2 - byteCount);
                        Encoding.UTF8.GetBytes(pSrc, length, bytes, byteCount);
                        var pData = pDest;
                        for (int i = 0; i < byteCount; i++)
                        {
                            var temp = bytes[i];
                            if (temp < 128 && _IsSafeChar[temp])
                            {
                                *pData++ = (char)temp;
                            }
                            else
                            {
                                var h = (temp >> 4) & 0xf;
                                var l = temp & 0x0f;
                                *pData++ = '%';
                                *pData++ = h <= 9 ? (char)(h + '0') : (char)(h - 10 + 'A');
                                *pData++ = l <= 9 ? (char)(l + '0') : (char)(l - 10 + 'A');
                            }
                        }
                    }
                    return value;
                }
            }
        }
        public static string Encode(ReadOnlySpan<char> charsToEncode)
        {
            var length = charsToEncode.Length;
            if (length == 0)
                return string.Empty;
            int safeCount = 0;
            unsafe
            {
                fixed (char* pSrc = &MemoryMarshal.GetReference(charsToEncode))
                {
                    for (int i = 0; i < length; i++)
                    {
                        var temp = pSrc[i];
                        if (temp < 128 && _IsSafeChar[temp])
                            safeCount++;
                    }
                    if (safeCount == length)
                        return new string(pSrc, 0, length);
                    int byteCount = Encoding.UTF8.GetByteCount(pSrc,length);
                    var value = new string('\0', byteCount + (byteCount - safeCount) * 2);
                    fixed (char* pDest = value )
                    {
                        var bytes = (byte*)pDest + (value.Length * 2 - byteCount);
                        Encoding.UTF8.GetBytes(pSrc, length, bytes, byteCount);
                        var pData = pDest;
                        for (int i = 0; i < byteCount; i++)
                        {
                            var temp = bytes[i];
                            if (temp < 128 && _IsSafeChar[temp])
                            {
                                *pData++ = (char)temp;
                            }
                            else
                            {
                                var h = (temp >> 4) & 0xf;
                                var l = temp & 0x0f;
                                *pData++ = '%';
                                *pData++ = h <= 9 ? (char)(h + '0') : (char)(h - 10 + 'A');
                                *pData++ = l <= 9 ? (char)(l + '0') : (char)(l - 10 + 'A');
                            }
                        }
                    }
                    return value;
                }
            }
        }
        public static string Encode(StringBuffer sbToEncode)
        {
            if (sbToEncode == null)
                throw new ArgumentNullException(nameof(sbToEncode));

            var length = sbToEncode.Length;
            if (length == 0)
                return string.Empty;
            var sbEnumerable = sbToEncode.GetEnumerable();
            if (sbEnumerable.Count == 1)
                return Encode(sbEnumerable[0].Span);

            int safeCount = 0;
            foreach (var segm in sbEnumerable)
            {
                var segmSpan = segm.Span;
                for (int i = 0; i < segmSpan.Length; i++)
                {
                    var temp = segmSpan[i];
                    if (temp < 128 && _IsSafeChar[temp])
                    {
                        safeCount++;
                    }
                }
            }
            if (safeCount == length)
                return sbToEncode.ToString();
            var byteCount = Encoding.UTF8.GetByteCount(sbToEncode);
            var value = new string('\0', byteCount + (byteCount - safeCount) * 2);
            unsafe
            {
                fixed (char* pDest = value)
                {
                    var pData = pDest;
                    var bytes = (byte*)pDest + (value.Length * 2 - byteCount);
                    foreach (var segm in sbEnumerable)
                    {
                        var segmSpan = segm.Span;
                        fixed (char* pSrc = &MemoryMarshal.GetReference(segmSpan))
                        {
                            var count = Encoding.UTF8.GetBytes(pSrc, segmSpan.Length, bytes, byteCount);
                            for (int i = 0; i < count; i++)
                            {
                                var temp = bytes[i];
                                if (temp < 128 && _IsSafeChar[temp])
                                {
                                    *pData++ = (char)temp;
                                }
                                else
                                {
                                    var h = (temp >> 4) & 0xf;
                                    var l = temp & 0x0f;
                                    *pData++ = '%';
                                    *pData++ = h <= 9 ? (char)(h + '0') : (char)(h - 10 + 'A');
                                    *pData++ = l <= 9 ? (char)(l + '0') : (char)(l - 10 + 'A');
                                }
                            }
                            bytes += count;
                            byteCount -= count;
                        }
                    }
                }
            }
            return value;
        }
        public static string Encode(string stringToEncode, Encoding encoding)
        {
            if (stringToEncode == null)
                throw new ArgumentNullException(nameof(stringToEncode));
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            //if (encoding == Encoding.UTF8)
            //    return Encode(stringToEncode);
            var length = stringToEncode.Length;
            if (length == 0)
                return string.Empty;
            int encodeIndex = -1;
            unsafe
            {
                fixed (char* pSrc = stringToEncode)
                {
                    for (int i = 0; i < length; i++)
                    {
                        var temp = pSrc[i];
                        if (temp >= 128 || !_IsSafeChar[temp])
                        {
                            encodeIndex = i;
                            break;
                        }
                    }
                    if (encodeIndex == -1)
                        return stringToEncode;
                    var byteCount= encoding.GetMaxByteCount(length- encodeIndex);
                    if (byteCount > _MaxStackSize)
                        byteCount = _MaxStackSize;
                    var bytes = stackalloc byte[byteCount];
                    var encoder = encoding.GetEncoder();
                    var sb = StringExtensions.Rent();
                    sb.Append(pSrc, encodeIndex);
                    for (int i = encodeIndex; i < length; i++)
                    {
                        var temp = pSrc[i];
                        if (encodeIndex == -1)
                        {
                            if (temp < 128 && _IsSafeChar[temp])
                                sb.Append(temp);
                            else
                                encodeIndex = i;
                        }
                        else
                        {
                            if (temp < 128 && _IsSafeChar[temp])
                            {
                                var pData = pSrc + encodeIndex;
                                bool completed; int charsUsed, bytesUsed, charCount = i - encodeIndex;
                                do
                                {
                                    encoder.Convert(pData, charCount, bytes, byteCount, true, out charsUsed, out bytesUsed, out completed);
                                    charCount -= charsUsed;
                                    pData += charsUsed;
                                    for (int j = 0; j < bytesUsed; j++)
                                    {
                                        var b = bytes[j];
                                        var h = (b >> 4) & 0xf;
                                        var l = b & 0x0f;
                                        sb.Append('%');
                                        sb.Append(h <= 9 ? (char)(h + '0') : (char)(h - 10 + 'A'));
                                        sb.Append(l <= 9 ? (char)(l + '0') : (char)(l - 10 + 'A'));
                                    }
                                } while (charCount > 0);
                                sb.Append(temp);
                                encodeIndex = -1;
                            }
                        }
                    }
                    if (encodeIndex != -1)
                    {
                        var pData = pSrc + encodeIndex;
                        bool completed; int charsUsed, bytesUsed, charCount = length - encodeIndex;
                        do
                        {
                            encoder.Convert(pData, charCount, bytes, byteCount, true, out charsUsed, out bytesUsed, out completed);
                            charCount -= charsUsed;
                            pData += charsUsed;
                            for (int j = 0; j < bytesUsed; j++)
                            {
                                var b = bytes[j];
                                var h = (b >> 4) & 0xf;
                                var l = b & 0x0f;
                                sb.Append('%');
                                sb.Append(h <= 9 ? (char)(h + '0') : (char)(h - 10 + 'A'));
                                sb.Append(l <= 9 ? (char)(l + '0') : (char)(l - 10 + 'A'));
                            }
                        } while (charCount > 0);
                    }
                    return StringExtensions.GetReturn(sb);
                }
            }
        }
        public static string Encode(ReadOnlySpan<char> charsToEncode, Encoding encoding)
        {
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            //if (encoding == Encoding.UTF8)
            //    return Encode(stringToEncode);
            var length = charsToEncode.Length;
            if (length == 0)
                return string.Empty;
            var encodeIndex = -1;
            unsafe
            {
                fixed (char* pSrc = &MemoryMarshal.GetReference(charsToEncode))
                {
                    for (int i = 0; i < length; i++)
                    {
                        var temp = pSrc[i];
                        if (temp >= 128 || !_IsSafeChar[temp])
                        {
                            encodeIndex = i;
                            break;
                        }
                    }
                    if (encodeIndex == -1)
                        return new string(pSrc, 0, length);
                    var byteCount = encoding.GetMaxByteCount(length - encodeIndex);//?>160
                    if (byteCount > _MaxStackSize)
                        byteCount = _MaxStackSize;
                    var bytes = stackalloc byte[byteCount];
                    var encoder = encoding.GetEncoder();
                    var sb = StringExtensions.Rent();
                    sb.Append(pSrc, encodeIndex);
                    for (int i = encodeIndex; i < length; i++)
                    {
                        var temp = pSrc[i];
                        if (encodeIndex == -1)
                        {
                            if (temp < 128 && _IsSafeChar[temp])
                                sb.Append(temp);
                            else
                                encodeIndex = i;
                        }
                        else
                        {
                            if (temp < 128 && _IsSafeChar[temp])
                            {
                                var pData = pSrc + encodeIndex;
                                bool completed; int charsUsed, bytesUsed, charCount = i - encodeIndex;
                                do
                                {
                                    encoder.Convert(pData, charCount, bytes, byteCount, true, out charsUsed, out bytesUsed, out completed);
                                    charCount -= charsUsed;
                                    pData += charsUsed;
                                    for (int j = 0; j < bytesUsed; j++)
                                    {
                                        var b = bytes[j];
                                        var h = (b >> 4) & 0xf;
                                        var l = b & 0x0f;
                                        sb.Append('%');
                                        sb.Append(h <= 9 ? (char)(h + '0') : (char)(h - 10 + 'A'));
                                        sb.Append(l <= 9 ? (char)(l + '0') : (char)(l - 10 + 'A'));
                                    }
                                } while (charCount > 0);
                                sb.Append(temp);
                                encodeIndex = -1;
                            }
                        }
                    }
                    if (encodeIndex != -1)
                    {
                        var pData = pSrc + encodeIndex;
                        bool completed; int charsUsed, bytesUsed, charCount = length - encodeIndex;
                        do
                        {
                            encoder.Convert(pData, charCount, bytes, byteCount, true, out charsUsed, out bytesUsed, out completed);
                            charCount -= charsUsed;
                            pData += charsUsed;
                            for (int j = 0; j < bytesUsed; j++)
                            {
                                var b = bytes[j];
                                var h = (b >> 4) & 0xf;
                                var l = b & 0x0f;
                                sb.Append('%');
                                sb.Append(h <= 9 ? (char)(h + '0') : (char)(h - 10 + 'A'));
                                sb.Append(l <= 9 ? (char)(l + '0') : (char)(l - 10 + 'A'));
                            }
                        } while (charCount > 0);
                    }
                    return StringExtensions.GetReturn(sb);
                }
            }
        }
        public static string Encode(StringBuffer sbToEncode, Encoding encoding)
        {
            if (sbToEncode == null)
                throw new ArgumentNullException(nameof(sbToEncode));
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            //if (encoding == Encoding.UTF8)
            //    return Encode(stringToEncode);
            var length = sbToEncode.Length;
            if (length == 0)
                return string.Empty;

            var sbEnumerable = sbToEncode.GetEnumerable();
            if (sbEnumerable.Count == 1)
                return Encode(sbEnumerable[0].Span, encoding);

            var sb = StringExtensions.Rent();
            var encoder = encoding.GetEncoder();
            unsafe
            {
                var byteCount = encoding.GetMaxByteCount(length);//放在循环内?
                if (byteCount > _MaxStackSize)
                    byteCount = _MaxStackSize;
                var bytes = stackalloc byte[byteCount];
                foreach (var segm in sbEnumerable)
                {
                    var segmLength = segm.Length;
                    var encodeIndex = -1;
                    fixed (char* pSrc = &MemoryMarshal.GetReference(segm.Span))
                    {
                        for (int i = 0; i < segmLength; i++)
                        {
                            var temp = pSrc[i];
                            if (temp >= 128 || !_IsSafeChar[temp])
                            {
                                encodeIndex = i;
                                break;
                            }
                        }
                        if (encodeIndex == -1)
                        {
                            sb.Append(pSrc, segmLength);
                            continue;
                        }
                        sb.Append(pSrc, encodeIndex);
                        for (int i = encodeIndex; i < segmLength; i++)
                        {
                            var temp = pSrc[i];
                            if (encodeIndex == -1)
                            {
                                if (temp < 128 && _IsSafeChar[temp])
                                    sb.Append(temp);
                                else
                                    encodeIndex = i;
                            }
                            else
                            {
                                if (temp < 128 && _IsSafeChar[temp])
                                {
                                    var pData = pSrc + encodeIndex;
                                    bool completed; int charsUsed, bytesUsed, charCount = i - encodeIndex;
                                    do
                                    {
                                        encoder.Convert(pData, charCount, bytes, byteCount, true, out charsUsed, out bytesUsed, out completed);
                                        charCount -= charsUsed;
                                        pData += charsUsed;
                                        for (int j = 0; j < bytesUsed; j++)
                                        {
                                            var b = bytes[j];
                                            var h = (b >> 4) & 0xf;
                                            var l = b & 0x0f;
                                            sb.Append('%');
                                            sb.Append(h <= 9 ? (char)(h + '0') : (char)(h - 10 + 'A'));
                                            sb.Append(l <= 9 ? (char)(l + '0') : (char)(l - 10 + 'A'));
                                        }
                                    } while (charCount > 0);
                                    sb.Append(temp);
                                    encodeIndex = -1;
                                }
                            }
                        }
                        if (encodeIndex != -1)
                        {
                            var pData = pSrc + encodeIndex;
                            bool completed; int charsUsed, bytesUsed, charCount = segmLength - encodeIndex;
                            do
                            {
                                encoder.Convert(pData, charCount, bytes, byteCount, true, out charsUsed, out bytesUsed, out completed);
                                charCount -= charsUsed;
                                pData += charsUsed;
                                for (int j = 0; j < bytesUsed; j++)
                                {
                                    var b = bytes[j];
                                    var h = (b >> 4) & 0xf;
                                    var l = b & 0x0f;
                                    sb.Append('%');
                                    sb.Append(h <= 9 ? (char)(h + '0') : (char)(h - 10 + 'A'));
                                    sb.Append(l <= 9 ? (char)(l + '0') : (char)(l - 10 + 'A'));
                                }
                            } while (charCount > 0);
                        }
                    }
                }
            }
            return StringExtensions.GetReturn(sb);
        }
        public static string Decode(string stringToDecode)
        {
            if (stringToDecode == null)
                throw new ArgumentNullException(nameof(stringToDecode));

            var length = stringToDecode.Length;
            if (length == 0)
                return string.Empty;
            unsafe
            {
                fixed (char* pSrc = stringToDecode)
                {
                    var hasPlus = false;
                    var percentCount = 0;
                    for (int i = 0; i < length;)
                    {
                        switch (pSrc[i])
                        {
                            case '%':
                                percentCount++;
                                if (i + 2 >= length)
                                    throw new FormatException(nameof(stringToDecode));
                                i += 3;
                                break;
                            case '+':
                                hasPlus = true;
                                i++;
                                break;
                            default:
                                i++;
                                break;
                        }
                    }
                    if (percentCount == 0)
                        return hasPlus ? stringToDecode.Replace('+', ' ') : stringToDecode;

                    //!hasPlus 通过分支减少判断?
                    var byteCount = length - percentCount * 2;
                    if (byteCount <= _MaxStackSize)
                    {
                        var bytes = stackalloc byte[byteCount];
                        var bytesOffset = 0;
                        for (int i = 0; i < length;)
                        {
                            switch (pSrc[i])
                            {
                                case '%':
                                    var hHex = pSrc[i + 1];
                                    var lHex = pSrc[i + 2];
                                    var h = (hHex >= '0' && hHex <= '9') ? hHex - '0' :
                                            (hHex >= 'A' && hHex <= 'F') ? hHex - 'A' + 10 :
                                            (hHex >= 'a' && hHex <= 'f') ? hHex - 'a' + 10 : -1;
                                    var l = (lHex >= '0' && lHex <= '9') ? lHex - '0' :
                                            (lHex >= 'A' && lHex <= 'F') ? lHex - 'A' + 10 :
                                            (lHex >= 'a' && lHex <= 'f') ? lHex - 'a' + 10 : -1;
                                    if (h == -1 || l == -1)
                                        throw new FormatException(nameof(stringToDecode));
                                    bytes[bytesOffset++] = (byte)((h << 4) | l);
                                    i += 3;
                                    break;
                                case '+':
                                    bytes[bytesOffset++] = (byte)' ';
                                    i++;
                                    break;
                                default:
                                    bytes[bytesOffset++] = (byte)pSrc[i];
                                    i++;
                                    break;
                            }
                        }
                        return Encoding.UTF8.GetString(bytes, byteCount);
                    }
                    else
                    {
                        byteCount = _MaxStackSize;
                        var bytes = stackalloc byte[byteCount];
                        var bytesOffset = 0;
                        var sb = StringExtensions.Rent();
                        var decoder = GetUtf8Decoder();
                        for (int i = 0; i < length;)
                        {
                            switch (pSrc[i])
                            {
                                case '%':
                                    var hHex = pSrc[i + 1];
                                    var lHex = pSrc[i + 2];
                                    var h = (hHex >= '0' && hHex <= '9') ? hHex - '0' :
                                            (hHex >= 'A' && hHex <= 'F') ? hHex - 'A' + 10 :
                                            (hHex >= 'a' && hHex <= 'f') ? hHex - 'a' + 10 : -1;
                                    var l = (lHex >= '0' && lHex <= '9') ? lHex - '0' :
                                            (lHex >= 'A' && lHex <= 'F') ? lHex - 'A' + 10 :
                                            (lHex >= 'a' && lHex <= 'f') ? lHex - 'a' + 10 : -1;
                                    if (h == -1 || l == -1)
                                        throw new FormatException(nameof(stringToDecode));
                                    bytes[bytesOffset++] = (byte)((h << 4) | l);
                                    i += 3;
                                    break;
                                case '+':
                                    bytes[bytesOffset++] = (byte)' ';
                                    i++;
                                    break;
                                default:
                                    bytes[bytesOffset++] = (byte)pSrc[i];
                                    i++;
                                    break;
                            }
                            if (bytesOffset == byteCount)
                            {
                                sb.Append(bytes, bytesOffset, false, decoder);
                                bytesOffset = 0;
                            }
                        }
                        if (bytesOffset > 0)
                            sb.Append(bytes, bytesOffset, true, decoder);

                        return StringExtensions.GetReturn(sb);
                    }
                }
            }
        }
        public static string Decode(ReadOnlySpan<char> charsToDecode)
        {
            var length = charsToDecode.Length;
            if (length == 0)
                return string.Empty;
            unsafe
            {
                fixed (char* pSrc = &MemoryMarshal.GetReference(charsToDecode))
                {
                    var hasPlus = false;
                    var percentCount = 0; 
                    for (int i = 0; i < length;)
                    {
                        switch (pSrc[i])
                        {
                            case '%':
                                percentCount++;
                                if (i + 2 >= length)
                                    throw new FormatException(nameof(charsToDecode));
                                i += 3;
                                break;
                            case '+':
                                hasPlus = true;
                                i++;
                                break;
                            default:
                                i++;
                                break;
                        }
                    }
                    if (percentCount == 0)
                    {
                        if(!hasPlus)
                            return new string(pSrc, 0, length);

                        var value = new string('\0', length);
                        fixed (char* pDest = value)
                        {
                            for (int i = 0; i < length; i++)
                            {
                                if (pSrc[i] == '+')
                                    pDest[i] = ' ';
                                else
                                    pDest[i] = pSrc[i];
                            }
                        }
                        return value;
                    }
                    //!hasPlus 通过分支减少判断?
                    var byteCount = length - percentCount * 2;
                    if (byteCount <= _MaxStackSize)
                    {
                        var bytes = stackalloc byte[byteCount];
                        var bytesOffset = 0;
                        for (int i = 0; i < length;)
                        {
                            switch (pSrc[i])
                            {
                                case '%':
                                    var hHex = pSrc[i + 1];
                                    var lHex = pSrc[i + 2];
                                    var h = (hHex >= '0' && hHex <= '9') ? hHex - '0' :
                                            (hHex >= 'A' && hHex <= 'F') ? hHex - 'A' + 10 :
                                            (hHex >= 'a' && hHex <= 'f') ? hHex - 'a' + 10 : -1;
                                    var l = (lHex >= '0' && lHex <= '9') ? lHex - '0' :
                                            (lHex >= 'A' && lHex <= 'F') ? lHex - 'A' + 10 :
                                            (lHex >= 'a' && lHex <= 'f') ? lHex - 'a' + 10 : -1;
                                    if (h == -1 || l == -1)
                                        throw new FormatException(nameof(charsToDecode));
                                    bytes[bytesOffset++] = (byte)((h << 4) | l);
                                    i += 3;
                                    break;
                                case '+':
                                    bytes[bytesOffset++] = (byte)' ';
                                    i++;
                                    break;
                                default:
                                    bytes[bytesOffset++] = (byte)pSrc[i];
                                    i++;
                                    break;
                            }
                        }
                        return Encoding.UTF8.GetString(bytes, byteCount);
                    }
                    else
                    {
                        byteCount = _MaxStackSize;
                        var bytes = stackalloc byte[byteCount];
                        var bytesOffset = 0;
                        var sb = StringExtensions.Rent();
                        var decoder = GetUtf8Decoder();
                        for (int i = 0; i < length;)
                        {
                            switch (pSrc[i])
                            {
                                case '%':
                                    var hHex = pSrc[i + 1];
                                    var lHex = pSrc[i + 2];
                                    var h = (hHex >= '0' && hHex <= '9') ? hHex - '0' :
                                            (hHex >= 'A' && hHex <= 'F') ? hHex - 'A' + 10 :
                                            (hHex >= 'a' && hHex <= 'f') ? hHex - 'a' + 10 : -1;
                                    var l = (lHex >= '0' && lHex <= '9') ? lHex - '0' :
                                            (lHex >= 'A' && lHex <= 'F') ? lHex - 'A' + 10 :
                                            (lHex >= 'a' && lHex <= 'f') ? lHex - 'a' + 10 : -1;
                                    if (h == -1 || l == -1)
                                        throw new FormatException(nameof(charsToDecode));
                                    bytes[bytesOffset++] = (byte)((h << 4) | l);
                                    i += 3;
                                    break;
                                case '+':
                                    bytes[bytesOffset++] = (byte)' ';
                                    i++;
                                    break;
                                default:
                                    bytes[bytesOffset++] = (byte)pSrc[i];
                                    i++;
                                    break;
                            }
                            if (bytesOffset == byteCount)
                            {
                                sb.Append(bytes, bytesOffset, false, decoder);
                                bytesOffset = 0;
                            }
                        }
                        if (bytesOffset > 0)
                            sb.Append(bytes, bytesOffset, true, decoder);

                        return StringExtensions.GetReturn(sb);
                    }
                }
            }
        }
        public static string Decode(StringBuffer sbToDecode)
        {
            return Decode(sbToDecode, Encoding.UTF8);
        }
        public static string Decode(string stringToDecode, Encoding encoding)
        {
            if (stringToDecode == null)
                throw new ArgumentNullException(nameof(stringToDecode));
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            //if (encoding == Encoding.UTF8)
            //    return Decode(stringToDecode);
            var length = stringToDecode.Length;
            if (length == 0)
                return string.Empty;
            unsafe
            {
                fixed (char* pSrc = stringToDecode)
                {
                    var hasPlus = false;
                    var percentCount = 0;
                    for (int i = 0; i < length;)
                    {
                        switch (pSrc[i])
                        {
                            case '%':
                                percentCount++;
                                if (i + 2 >= length)
                                    throw new FormatException(nameof(stringToDecode));
                                i += 3;
                                break;
                            case '+':
                                hasPlus = true;
                                i++;
                                break;
                            default:
                                i++;
                                break;
                        }
                    }
                    if (percentCount == 0)
                        return hasPlus ? stringToDecode.Replace('+', ' ') : stringToDecode;
                    var sb = StringExtensions.Rent();
                    var decoder = encoding.GetDecoder();
                    var byteCount = length - percentCount * 2;
                    if (byteCount > _MaxStackSize)
                        byteCount = _MaxStackSize;
                    var bytes = stackalloc byte[byteCount];
                    var pos = 0;
                    for (; ; )
                    {
                        for (; ; )
                        {
                            if (pos == length)
                                return StringExtensions.GetReturn(sb);
                            var temp = pSrc[pos];
                            if (temp == '%')
                                break;
                            pos += 1;
                            sb.Append(temp == '+' ? ' ' : temp);
                        }
                        var bytesOffset = 0;
                        for (; ; )
                        {
                            var hHex = pSrc[pos + 1];
                            var lHex = pSrc[pos + 2];
                            var h = (hHex >= '0' && hHex <= '9') ? hHex - '0' :
                                    (hHex >= 'A' && hHex <= 'F') ? hHex - 'A' + 10 :
                                    (hHex >= 'a' && hHex <= 'f') ? hHex - 'a' + 10 : -1;
                            var l = (lHex >= '0' && lHex <= '9') ? lHex - '0' :
                                    (lHex >= 'A' && lHex <= 'F') ? lHex - 'A' + 10 :
                                    (lHex >= 'a' && lHex <= 'f') ? lHex - 'a' + 10 : -1;
                            if (h == -1 || l == -1)
                            {
                                StringExtensions.Return(sb);
                                throw new FormatException(nameof(stringToDecode));
                            }
                            bytes[bytesOffset++] = (byte)((h << 4) | l);
                            if (bytesOffset == byteCount)
                            {
                                sb.Append(bytes, byteCount, false, decoder);
                                bytesOffset = 0;
                            }
                            pos += 3;
                            if (pos == length)
                            {
                                if (bytesOffset > 0)
                                    sb.Append(bytes, bytesOffset, true, decoder);
                                return StringExtensions.GetReturn(sb);
                            }
                            if (pSrc[pos] != '%')
                            {
                                if (bytesOffset > 0)
                                    sb.Append(bytes, bytesOffset, true, decoder);
                                sb.Append(pSrc[pos] == '+' ? ' ' : pSrc[pos]);
                                pos += 1;
                                break;
                            }
                        }
                    }
                }
            }
        }
        public static string Decode(ReadOnlySpan<char> charsToDecode, Encoding encoding)
        {
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            //if (encoding == Encoding.UTF8)
            //    return Decode(charsToDecode);
            var length = charsToDecode.Length;
            if (length == 0)
                return string.Empty;
            unsafe
            {
                fixed (char* pSrc = &MemoryMarshal.GetReference(charsToDecode))
                {
                    var hasPlus = false;
                    var percentCount = 0;
                    for (int i = 0; i < length;)
                    {
                        switch (pSrc[i])
                        {
                            case '%':
                                percentCount++;
                                if (i + 2 >= length)
                                    throw new FormatException(nameof(charsToDecode));
                                i += 3;
                                break;
                            case '+':
                                hasPlus = true;
                                i++;
                                break;
                            default:
                                i++;
                                break;
                        }
                    }
                    if (percentCount == 0)
                    {
                        if(!hasPlus)
                            return new string(pSrc, 0, length);

                        var value = new string('\0', length);
                        fixed (char* pDest = value)
                        {
                            for (int i = 0; i < length; i++)
                            {
                                if (pSrc[i] == '+')
                                    pDest[i] = ' ';
                                else
                                    pDest[i] = pSrc[i];
                            }
                        }
                        return value;
                    }
                    var sb = StringExtensions.Rent();
                    var decoder = encoding.GetDecoder();
                    var byteCount = length - percentCount * 2;
                    if (byteCount > _MaxStackSize)
                        byteCount = _MaxStackSize;
                    var bytes = stackalloc byte[byteCount];
                    var pos = 0;
                    for (; ; )
                    {
                        for (; ; )
                        {
                            if (pos == length)
                                return StringExtensions.GetReturn(sb);
                            var temp = pSrc[pos];
                            if (temp == '%')
                                break;
                            pos += 1;
                            sb.Append(temp == '+' ? ' ' : temp);
                        }
                        var bytesOffset = 0;
                        for (; ; )
                        {
                            var hHex = pSrc[pos + 1];
                            var lHex = pSrc[pos + 2];
                            var h = (hHex >= '0' && hHex <= '9') ? hHex - '0' :
                                    (hHex >= 'A' && hHex <= 'F') ? hHex - 'A' + 10 :
                                    (hHex >= 'a' && hHex <= 'f') ? hHex - 'a' + 10 : -1;
                            var l = (lHex >= '0' && lHex <= '9') ? lHex - '0' :
                                    (lHex >= 'A' && lHex <= 'F') ? lHex - 'A' + 10 :
                                    (lHex >= 'a' && lHex <= 'f') ? lHex - 'a' + 10 : -1;
                            if (h == -1 || l == -1)
                            {
                                StringExtensions.Return(sb);
                                throw new FormatException(nameof(charsToDecode));
                            }
                            bytes[bytesOffset++] = (byte)((h << 4) | l);
                            if (bytesOffset == byteCount)
                            {
                                sb.Append(bytes, byteCount, false, decoder);
                                bytesOffset = 0;
                            }
                            pos += 3;
                            if (pos == length)
                            {
                                if (bytesOffset > 0)
                                    sb.Append(bytes, bytesOffset, true, decoder);
                                return StringExtensions.GetReturn(sb);
                            }
                            if (pSrc[pos] != '%')
                            {
                                if (bytesOffset > 0)
                                    sb.Append(bytes, bytesOffset, true, decoder);
                                sb.Append(pSrc[pos] == '+' ? ' ' : pSrc[pos]);
                                pos += 1;
                                break;
                            }
                        }
                    }
                }
            }
        }
        public static string Decode(StringBuffer sbToDecode, Encoding encoding)
        {
            if (sbToDecode == null)
                throw new ArgumentNullException(nameof(sbToDecode));
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            var length = sbToDecode.Length;
            if (length == 0)
                return string.Empty;
            var sbEnumerable = sbToDecode.GetEnumerable();
            if (sbEnumerable.Count == 1)
                return Decode(sbEnumerable[0].Span, encoding);

            var hasPlus = false;
            var percentCount = 0;//连续的%的个数
            var sbEnumerator = sbEnumerable.GetEnumerator();
            while (sbEnumerator.MoveNext())
            {
                var segmSpan = sbEnumerator.Current.Span;
                var segmLength = segmSpan.Length;
                for (int i = 0; i < segmLength;)
                {
                    if (segmSpan[i] != '%')
                    {
                        if (segmSpan[i] == '+')
                            hasPlus = true;
                        i += 1;
                        continue;
                    }
                    var tempPercentCount = 0;
                    for (; ; )
                    {
                        tempPercentCount++;
                        if (i + 2 < segmLength)
                        {
                            i += 3;
                        }
                        else if (i + 1 == segmLength)
                        {
                            if (!sbEnumerator.MoveNext())
                                throw new FormatException(nameof(sbToDecode));
                            segmSpan = sbEnumerator.Current.Span;
                            segmLength = segmSpan.Length;
                            if (segmLength == 1)
                            {
                                if (!sbEnumerator.MoveNext())
                                    throw new FormatException(nameof(sbToDecode));
                                segmSpan = sbEnumerator.Current.Span;
                                segmLength = segmSpan.Length;
                                i = 1;
                            }
                            else
                            {
                                i = 2;
                            }
                        }
                        else if (i + 2 == segmLength)
                        {
                            if (!sbEnumerator.MoveNext())
                                throw new FormatException(nameof(sbToDecode));
                            segmSpan = sbEnumerator.Current.Span;
                            segmLength = segmSpan.Length;
                            i = 1;
                        }

                        if (i == segmLength)
                        {
                            if (!sbEnumerator.MoveNext())
                            {
                                if (tempPercentCount > percentCount)
                                    percentCount = tempPercentCount;
                                goto decode;
                            }
                            segmSpan = sbEnumerator.Current.Span;
                            segmLength = segmSpan.Length;
                            i = 0;
                        }
                        if (segmSpan[i]=='%')
                            continue;

                        if (tempPercentCount > percentCount)
                            percentCount = tempPercentCount;
                        if (segmSpan[i] == '+')
                            hasPlus = true;
                        i += 1;
                        break;
                    }
                }
            }

        decode:
            Console.WriteLine(percentCount);
            if (percentCount == 0)
            {
                if (!hasPlus)
                    return sbToDecode.ToString();

                var value = sbToDecode.ToString();
                if (value.Length != length)
                    throw new InvalidOperationException(nameof(sbToDecode));
                unsafe
                {
                    fixed (char* pData = value)
                    {
                        for (int i = 0; i < length; i++)
                        {
                            if (pData[i] == '+')
                                pData[i] = ' ';
                        }
                    }
                }
                return value;
            }
            unsafe
            {
                var byteCount = percentCount * 2;
                if (byteCount > _MaxStackSize)
                    byteCount = _MaxStackSize;
                var bytes = stackalloc byte[byteCount];
                sbEnumerator.Reset();
                while (sbEnumerator.MoveNext())
                {
                    var segmSpan = sbEnumerator.Current.Span;
                    var segmLength = segmSpan.Length;
                    for (int i = 0; i < segmLength;)
                    {
                        if (segmSpan[i] != '%')
                        {
                            if (segmSpan[i] == '+')
                                hasPlus = true;
                            i += 1;
                            continue;
                        }
                        var tempPercentCount = 0;
                        for (; ; )
                        {
                            tempPercentCount++;
                            if (i + 2 < segmLength)
                            {
                                i += 3;
                            }
                            else if (i + 1 == segmLength)
                            {
                                if (!sbEnumerator.MoveNext())
                                    throw new FormatException(nameof(sbToDecode));
                                segmSpan = sbEnumerator.Current.Span;
                                segmLength = segmSpan.Length;
                                if (segmLength == 1)
                                {
                                    if (!sbEnumerator.MoveNext())
                                        throw new FormatException(nameof(sbToDecode));
                                    segmSpan = sbEnumerator.Current.Span;
                                    segmLength = segmSpan.Length;
                                    i = 1;
                                }
                                else
                                {
                                    i = 2;
                                }
                            }
                            else if (i + 2 == segmLength)
                            {
                                if (!sbEnumerator.MoveNext())
                                    throw new FormatException(nameof(sbToDecode));
                                segmSpan = sbEnumerator.Current.Span;
                                segmLength = segmSpan.Length;
                                i = 1;
                            }

                            if (i == segmLength)
                            {
                                if (!sbEnumerator.MoveNext())
                                {
                                    if (tempPercentCount > percentCount)
                                        percentCount = tempPercentCount;
                                    goto decode;
                                }
                                segmSpan = sbEnumerator.Current.Span;
                                segmLength = segmSpan.Length;
                                i = 0;
                            }
                            if (segmSpan[i] == '%')
                                continue;

                            if (tempPercentCount > percentCount)
                                percentCount = tempPercentCount;
                            if (segmSpan[i] == '+')
                                hasPlus = true;
                            i += 1;
                            break;
                        }
                    }
                }
            }
           
            return string.Empty;
        }
    }
}
