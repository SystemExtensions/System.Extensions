
namespace System.Extensions.Net
{
    using System.IO;
    using System.Text;
    using System.Buffers;
    using System.Diagnostics;
    using System.Net.Security;
    using System.Collections.Generic;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using System.Extensions.Http;
    using HttpVersion = Http.HttpVersion;
    public static class HttpServiceExtensions
    {
        private static Property<HttpRequest> _Connection = new Property<HttpRequest>("HttpServiceExtensions.Connection");
        public static IConnection Connection(this HttpRequest @this)
        {
            if (@this == null)
                return null;
            return (IConnection)@this.Properties[_Connection];
        }
        public static void Connection(this HttpRequest @this, IConnection connection)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            @this.Properties[_Connection] = connection;
        }
        public static IHttpService Use(this IHttpService @this, params IHttpHandler[] handlers)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (handlers == null)
                throw new ArgumentNullException(nameof(handlers));

            var _handlers = new IHttpHandler[handlers.Length + 1];
            handlers.CopyTo(_handlers, 0);
            _handlers[handlers.Length] = @this.Handler;
            @this.Handler = HttpHandler.CreatePipeline(_handlers);
            return @this;
        }
        public static IHttpService Use(this IHttpService @this, Func<HttpRequest, IHttpHandler, Task<HttpResponse>> module)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (module == null)
                throw new ArgumentNullException(nameof(module));

            var _module = HttpHandler.CreateModule(module);
            _module.Handler = @this.Handler;
            @this.Handler = _module;
            return @this;
        }
        public class HttpOptions
        {
            public int KeepAliveTimeout = -60000;//60s-120s
            public int ReceiveTimeout = -10000;//10s-20s
            public int SendTimeout = -10000;//10s-20s
            public int MaxHeaderSize = 40 << 10;//40K(<Large GC) MaxHeaderSize
        }
        public class HttpsOptions
        {
            public int KeepAliveTimeout = -60000;//60s-120s
            public int ReceiveTimeout = -10000;//10s-20s
            public int SendTimeout = -10000;//10s-20s
            public int MaxHeaderSize = 40 << 10;//40K(<Large GC) MaxHeaderSize
            public X509Certificate Certificate;//
            public int HandShakeTimeout = -5000;//5s-10s
        }
        public static IHttpService UseHttp(this IConnectionService @this, Action<HttpOptions> options, IHttpHandler handler)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            var optionsValue = new HttpOptions();
            options?.Invoke(optionsValue);
            var service = new HttpService(
                optionsValue.KeepAliveTimeout,
                optionsValue.ReceiveTimeout,
                optionsValue.SendTimeout,
                optionsValue.MaxHeaderSize
                );
            service.Handler = handler;
            @this.Handler = service;
            return service;
        }
        public static IHttpService UseHttp(this IConnectionService @this, Action<HttpOptions, HttpRouter> handler)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var optionsValue = new HttpOptions();
            var router = new HttpRouter();
            handler.Invoke(optionsValue, router);
            var service = new HttpService(
                optionsValue.KeepAliveTimeout,
                optionsValue.ReceiveTimeout,
                optionsValue.SendTimeout,
                optionsValue.MaxHeaderSize
                );
            service.Handler = router;
            @this.Handler = service;
            return service;
        }
        public static IHttpService UseHttp(this IConnectionService @this, Action<HttpOptions> options, Func<HttpRequest, Task<HttpResponse>> handler)
        {
            return UseHttp(@this, options, HttpHandler.Create(handler));
        }
        public static IHttpService UseHttps(this IConnectionService @this, Action<HttpsOptions> options, IHttpHandler handler)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            var optionsValue = new HttpsOptions();
            options?.Invoke(optionsValue);
            if (optionsValue.Certificate == null)
                throw new ArgumentNullException(nameof(optionsValue.Certificate));
            var http = new HttpService(
                optionsValue.KeepAliveTimeout,
                optionsValue.ReceiveTimeout,
                optionsValue.SendTimeout,
                optionsValue.MaxHeaderSize
                );
            http.Handler = handler;
            var https = new Service(http, optionsValue.Certificate, optionsValue.HandShakeTimeout);
            @this.Handler = https;
            return https;
        }
        public static IHttpService UseHttps(this IConnectionService @this, Action<HttpsOptions, HttpRouter> handler)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var optionsValue = new HttpsOptions();
            var router = new HttpRouter();
            handler?.Invoke(optionsValue, router);
            if (optionsValue.Certificate == null)
                throw new ArgumentNullException(nameof(optionsValue.Certificate));
            var http = new HttpService(
                optionsValue.KeepAliveTimeout,
                optionsValue.ReceiveTimeout,
                optionsValue.SendTimeout,
                optionsValue.MaxHeaderSize
                );
            http.Handler = router;
            var https = new Service(http, optionsValue.Certificate, optionsValue.HandShakeTimeout);
            @this.Handler = https;
            return https;
        }
        public static IHttpService UseHttps(this IConnectionService @this, Action<HttpsOptions> options, Func<HttpRequest, Task<HttpResponse>> handler)
        {
            return UseHttps(@this, options, HttpHandler.Create(handler));
        }

        #region private
        private class Service : IHttpService
        {
            private SslServerAuthenticationOptions _sslOptions;
            private HttpService _http;
            private TaskTimeoutQueue<IConnection> _handShakeQueue;
            public Service(HttpService http, X509Certificate cert, int handShakeTimeout)
            {
                Debug.Assert(http != null);
                Debug.Assert(cert != null);
                _http = http;
                _sslOptions = new SslServerAuthenticationOptions()
                {
                    ServerCertificate = cert,
                    EnabledSslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12 | SslProtocols.Tls13,
                    AllowRenegotiation = false,
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                    ClientCertificateRequired = false
                };
                if (handShakeTimeout != 0)
                    _handShakeQueue = new TaskTimeoutQueue<IConnection>(handShakeTimeout);
            }
            public async Task HandleAsync(IConnection connection)
            {
                var ssl = await connection.UseSslAsync(_sslOptions).Timeout(_handShakeQueue);
                try
                {
                    await _http.HandleAsync(ssl);
                }
                finally
                {
                    ssl.Close();
                }
            }
            public IHttpHandler Handler
            {
                get => _http.Handler;
                set => _http.Handler = value;
            }
        }
        #endregion

        #region ReceiveAsync SendAsync TODO??? Property
        public static Task<HttpResponse> SendAsync(this ClientConnection @this, HttpRequest request)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            return new HttpClientConnection(@this, request).SendAsync();
        }
        private class HttpClientConnection : IDisposable
        {
            #region const
            private const int _MaxHeaderSize = 64 * 1024;//64K
            private const byte _SPByte = (byte)' ', _HTByte = (byte)'\t', _CRByte = (byte)'\r', _LFByte = (byte)'\n', _COLONByte = (byte)':';
            private const long _Version10Long = 3471766442030158920, _Version11Long = 3543824036068086856;
            private const long _OKLong = 724317918291046450;//200 OK\r\n
            private static byte[] _Hex = Encoding.ASCII.GetBytes("0123456789abcdef");
            private enum State { Cr, Lf, LfCr, Name, Colon, Value };
            #endregion

            public HttpClientConnection(ClientConnection connection, HttpRequest request)
            {
                Debug.Assert(connection != null);
                Debug.Assert(request != null);

                _connection = connection;
                _request = request;
                _request.RegisterForDispose(this);
            }

            private ClientConnection _connection;
            private HttpRequest _request;
            private HttpResponse _response;

            //headers
            private bool _connectionClose;
            private bool _headOnly;
            private State _state;
            private int _headerName;
            private int _headerSize;

            //read
            private int _start;
            private int _end;
            private int _position;
            private unsafe byte* _pRead;
            private Memory<byte> _read;
            private MemoryHandle _readHandle;
            private IDisposable _readDisposable;
            private Queue<(Memory<byte>, IDisposable)> _readQueue;

            //write
            private bool _transferChunked;
            private long _contentLength = -1;
            private int _available;
            private unsafe byte* _pWrite;
            private Memory<byte> _write;
            private MemoryHandle _writeHandle;
            private IDisposable _writeDisposable;
            private Queue<(Memory<byte>, IDisposable)> _writeQueue;
            private void TryWrite()
            {
                if (_available > 0)
                    return;

                if (_write.Length > 0)
                {
                    if (_writeQueue == null)
                        _writeQueue = new Queue<(Memory<byte>, IDisposable)>();
                    _writeQueue.Enqueue((_write, _writeDisposable));
                    _writeHandle.Dispose();
                }
                _write = ConnectionExtensions.GetBytes(out _writeDisposable);
                _available = _write.Length;
                _writeHandle = _write.Pin();
                unsafe { _pWrite = (byte*)_writeHandle.Pointer; }
            }
            private void Write(byte value)
            {
                if (_available == 0)
                    TryWrite();

                unsafe
                {
                    var pData = _pWrite + (_write.Length - _available);
                    *pData = value;
                    _available -= 1;
                }
            }
            private void Write(int value)
            {
                Span<char> chars = stackalloc char[11];
                value.TryFormat(chars, out var charsWritten);//TODO?? if
                Write(chars.Slice(0, charsWritten));
            }
            private void Write(long value)
            {
                Span<char> chars = stackalloc char[20];
                value.TryFormat(chars, out var charsWritten);//TODO?? if
                Write(chars.Slice(0, charsWritten));
            }
            private void Write(ReadOnlySpan<char> value)
            {
                if (value.IsEmpty)
                    return;
                unsafe
                {
                    fixed (char* pValue = value)
                    {
                        var tempCount = value.Length;//剩余长度
                        while (tempCount > 0)
                        {
                            TryWrite();
                            var bytesToCopy = tempCount < _available ? tempCount : _available;
                            var pData = pValue + (value.Length - tempCount);
                            var pTempBytes = _pWrite + (_write.Length - _available);
                            var tempBytesToCopy = bytesToCopy;

                            while (tempBytesToCopy > 4)
                            {
                                *(pTempBytes) = (byte)*(pData);
                                *(pTempBytes + 1) = (byte)*(pData + 1);
                                *(pTempBytes + 2) = (byte)*(pData + 2);
                                *(pTempBytes + 3) = (byte)*(pData + 3);
                                pTempBytes += 4;
                                pData += 4;
                                tempBytesToCopy -= 4;
                            }
                            while (tempBytesToCopy > 0)
                            {
                                *(pTempBytes) = (byte)*(pData);
                                pTempBytes += 1;
                                pData += 1;
                                tempBytesToCopy -= 1;
                            }

                            tempCount -= bytesToCopy;
                            _available -= bytesToCopy;
                        }
                    }
                }
            }
            private void WriteCrLf()
            {
                if (_available >= 2)
                {
                    unsafe
                    {
                        var pData = _pWrite + (_write.Length - _available);
                        *(short*)pData = 2573;
                        _available -= 2;
                    }
                }
                else
                {
                    Write("\r\n");
                }
            }
            private void WriteCrLfCrLf()
            {
                if (_available >= 4)
                {
                    unsafe
                    {
                        var pData = _pWrite + (_write.Length - _available);
                        *(int*)pData = 168626701;
                        _available -= 4;
                    }
                }
                else
                {
                    Write("\r\n\r\n");
                }
            }
            private void WriteColonSpace()
            {
                if (_available >= 2)
                {
                    unsafe
                    {
                        var pData = _pWrite + (_write.Length - _available);
                        *(short*)pData = 8250;
                        _available -= 2;
                    }
                }
                else
                {
                    Write(": ");
                }
            }
            private void WriteContentLengthSpace()
            {
                if (_available >= 16)
                {
                    unsafe
                    {
                        var pData = _pWrite + (_write.Length - _available);
                        *(long*)pData = 3275364211029339971;
                        *(long*)(pData + 8) = 2322283407023695180;
                        _available -= 16;
                    }
                }
                else
                {
                    Write("Content-Length: ");
                }
            }
            private void WriteMethodSpace()
            {
                if (_request.Method == null)
                    _request.Method = _request.Content == null ? HttpMethod.Get : HttpMethod.Post;

                var method = _request.Method;
                if (method == HttpMethod.Get)
                {
                    if (_available >= 4)
                    {
                        unsafe
                        {
                            var pData = _pWrite + (_write.Length - _available);
                            *(int*)pData = 542393671;
                            _available -= 4;
                        }
                    }
                    else
                    {
                        Write("GET ");
                    }
                }
                else if (method == HttpMethod.Post)
                {
                    if (_available >= 8)
                    {
                        unsafe
                        {
                            var pData = _pWrite + (_write.Length - _available);
                            *(long*)pData = 138853699408;
                            _available -= 5;
                        }
                    }
                    else
                    {
                        Write("POST ");
                    }
                }
                else if (method == HttpMethod.Put)
                {
                    if (_available >= 4)
                    {
                        unsafe
                        {
                            var pData = _pWrite + (_write.Length - _available);
                            *(int*)pData = 542397776;
                            _available -= 4;
                        }
                    }
                    else
                    {
                        Write("PUT ");
                    }
                }
                else if (method == HttpMethod.Delete)
                {
                    if (_available >= 8)
                    {
                        unsafe
                        {
                            var pData = _pWrite + (_write.Length - _available);
                            *(long*)pData = 9083427496936772;
                            _available -= 7;
                        }
                    }
                    else
                    {
                        Write("DELETE ");
                    }
                }
                else
                {
                    Write(method.ToString());
                    Write(" ");
                }
            }
            private void WriteRequestUri()
            {
                Debug.Assert(_request.Method != null);
                var url = _request.Url;
                if (_request.Method == HttpMethod.Connect)
                {
                    Write(url.Host);
                    if (url.Port.HasValue)
                    {
                        Write((byte)':');
                        Write(url.Port.Value);
                    }
                }
                else
                {
                    Write(string.IsNullOrEmpty(url.Path) ? "/" : url.Path);
                    Write(url.Query);
                }
            }
            private void WriteSpaceVersionCrLf()
            {
                if (_request.Version == null)
                    _request.Version = HttpVersion.Version11;//default or(version == HttpVersion.Version11&&version==null)

                var version = _request.Version;
                if (version == HttpVersion.Version11)
                {
                    if (_available >= 12)
                    {
                        unsafe
                        {
                            var pData = _pWrite + (_write.Length - _available);
                            *(long*)pData = 3328493621662205984;
                            *(int*)(pData + 8) = 658737;
                            _available -= 11;
                        }
                    }
                    else
                    {
                        Write(" HTTP/1.1\r\n");
                    }
                }
                else if (version == HttpVersion.Version10)
                {
                    if (_available >= 12)
                    {
                        unsafe
                        {
                            var pData = _pWrite + (_write.Length - _available);
                            *(long*)pData = 3328493621662205984;
                            *(int*)(pData + 8) = 658736;
                            _available -= 11;
                        }
                    }
                    else
                    {
                        Write(" HTTP/1.0\r\n");
                    }
                }
                else
                {
                    throw new NotSupportedException(nameof(HttpVersion));
                }
            }
            private void WriteHeaders()
            {
                if (_request.Headers.Contains(HttpHeaders.Expect))
                    throw new NotSupportedException(HttpHeaders.Expect);

                if (!_request.Headers.Contains(HttpHeaders.Host))
                {
                    if (_available >= 8)
                    {
                        unsafe
                        {
                            var pData = _pWrite + (_write.Length - _available);
                            *(long*)pData = 35435433914184;
                            _available -= 6;
                        }
                    }
                    else
                    {
                        Write("Host: ");
                    }
                    Write(_request.Url.Host);
                    if (_request.Url.Port.HasValue)
                    {
                        Write((byte)':');
                        Write(_request.Url.Port.Value);
                    }
                    WriteCrLf();
                }

                Debug.Assert(!_connectionClose);
                if (_request.Headers.TryGetValue(HttpHeaders.Connection, out var connection))
                {
                    _connectionClose = connection.EqualsIgnoreCase("close");
                }
                else if (_request.Version == HttpVersion.Version10)
                {
                    if (_available >= 24)
                    {
                        unsafe
                        {
                            var pData = _pWrite + (_write.Length - _available);
                            *(long*)pData = 7598807758576447299;
                            *(long*)(pData + 8) = 8098991015672311407;
                            *(long*)(pData + 16) = 724346674325774637;
                        }
                        _available -= 24;
                    }
                    else
                    {
                        Write("Connection: keep-alive\r\n");
                    }
                }

                if (_request.Headers.TryGetValue(HttpHeaders.TransferEncoding, out var transferEncoding))
                {
                    if (transferEncoding.EqualsIgnoreCaseWhiteSpace("chunked"))
                    {
                        _transferChunked = true;
                    }
                    else if (!transferEncoding.EqualsIgnoreCaseWhiteSpace("identity"))
                    {
                        throw new NotSupportedException($"{HttpHeaders.TransferEncoding}:{transferEncoding}");
                    }
                }
                if (_request.Headers.TryGetValue(HttpHeaders.ContentLength, out var contentLength))
                {
                    if (!long.TryParse(contentLength, out _contentLength) || _contentLength < 0)
                    {
                        throw new NotSupportedException($"{HttpHeaders.ContentLength}:{contentLength}");
                    }
                }

                for (int i = 0; i < _request.Headers.Count; i++)
                {
                    var header = _request.Headers[i];
                    Write(header.Key);
                    WriteColonSpace();
                    Write(header.Value);
                    WriteCrLf();
                }
            }
            private async Task SendAsync(int offset)
            {
                Debug.Assert(offset > 0);
                if (_writeQueue == null)
                {
                    //var test = Encoding.UTF8.GetString(_write.Slice(0, offset).Span);
                    await _connection.SendAsync(_write.Slice(0, offset));
                }
                else
                {
                    while (_writeQueue.TryDequeue(out var write, out var disposable))
                    {
                        try
                        {
                            await _connection.SendAsync(write);
                        }
                        finally
                        {
                            disposable.Dispose();
                        }
                    }
                    _writeQueue = null;
                    await _connection.SendAsync(_write.Slice(0, offset));
                }
            }
            private async Task WriteAsync()
            {
                TryWrite();
                try
                {
                    WriteMethodSpace();
                    WriteRequestUri();
                    WriteSpaceVersionCrLf();
                    WriteHeaders();
                    if (_transferChunked)//偏移 0
                    {
                        #region Chunked
                        WriteCrLf();
                        Debug.Assert(_write.Length > 17);
                        var contentOffset = _write.Length - _available;
                        if (_request.Content == null)//0\r\n\r\n
                        {
                            if (_available >= 5)
                            {
                                unsafe
                                {
                                    _pWrite[contentOffset] = (byte)'0';
                                    _pWrite[contentOffset + 1] = (byte)'\r';
                                    _pWrite[contentOffset + 2] = (byte)'\n';
                                    _pWrite[contentOffset + 3] = (byte)'\r';
                                    _pWrite[contentOffset + 4] = (byte)'\n';
                                }
                                await SendAsync(contentOffset + 5);
                                return;
                            }
                            else
                            {
                                await SendAsync(contentOffset);
                                await _connection.SendAsync(new[] { (byte)'0', (byte)'\r', (byte)'\n', (byte)'\r', (byte)'\n', });
                                return;
                            }
                        }
                        if (_available > 1024)
                        {
                            var tempOffset = contentOffset + 10;//偏移10个字节 Int32\r\n
                            var tempCount = _available - 12;//10 2 最后留两个字节\r\n
                            var result = await _request.Content.ReadAsync(_write.Slice(tempOffset, tempCount));
                            if (result == 0)
                            {
                                unsafe
                                {
                                    _pWrite[contentOffset] = (byte)'0';
                                    _pWrite[contentOffset + 1] = (byte)'\r';
                                    _pWrite[contentOffset + 2] = (byte)'\n';
                                    _pWrite[contentOffset + 3] = (byte)'\r';
                                    _pWrite[contentOffset + 4] = (byte)'\n';////0\r\n\r\n
                                }
                                await SendAsync(contentOffset + 5);
                                return;
                            }
                            tempOffset += result;
                            tempCount -= result;
                            if (tempCount >= 5 && _response.Content.Available == 0)
                            {
                                unsafe
                                {
                                    _pWrite[contentOffset] = _Hex[((result >> 0x1c) & 0x0f)];
                                    _pWrite[contentOffset + 1] = _Hex[((result >> 0x18) & 0x0f)];
                                    _pWrite[contentOffset + 2] = _Hex[((result >> 0x14) & 0x0f)];
                                    _pWrite[contentOffset + 3] = _Hex[((result >> 0x10) & 0x0f)];
                                    _pWrite[contentOffset + 4] = _Hex[((result >> 0x0c) & 0x0f)];
                                    _pWrite[contentOffset + 5] = _Hex[((result >> 0x08) & 0x0f)];
                                    _pWrite[contentOffset + 6] = _Hex[((result >> 0x04) & 0x0f)];
                                    _pWrite[contentOffset + 7] = _Hex[((result >> 0x00) & 0x0f)];
                                    _pWrite[contentOffset + 8] = (byte)'\r';
                                    _pWrite[contentOffset + 9] = (byte)'\n';
                                    _pWrite[tempOffset] = (byte)'\r';
                                    _pWrite[tempOffset + 1] = (byte)'\n';
                                    _pWrite[tempOffset + 2] = (byte)'0';
                                    _pWrite[tempOffset + 3] = (byte)'\r';
                                    _pWrite[tempOffset + 4] = (byte)'\n';
                                    _pWrite[tempOffset + 5] = (byte)'\r';
                                    _pWrite[tempOffset + 6] = (byte)'\n';////0\r\n\r\n
                                }
                                await SendAsync(tempOffset + 7);
                                return;
                            }
                            unsafe
                            {
                                _pWrite[contentOffset] = _Hex[((result >> 0x1c) & 0x0f)];
                                _pWrite[contentOffset + 1] = _Hex[((result >> 0x18) & 0x0f)];
                                _pWrite[contentOffset + 2] = _Hex[((result >> 0x14) & 0x0f)];
                                _pWrite[contentOffset + 3] = _Hex[((result >> 0x10) & 0x0f)];
                                _pWrite[contentOffset + 4] = _Hex[((result >> 0x0c) & 0x0f)];
                                _pWrite[contentOffset + 5] = _Hex[((result >> 0x08) & 0x0f)];
                                _pWrite[contentOffset + 6] = _Hex[((result >> 0x04) & 0x0f)];
                                _pWrite[contentOffset + 7] = _Hex[((result >> 0x00) & 0x0f)];
                                _pWrite[contentOffset + 8] = (byte)'\r';
                                _pWrite[contentOffset + 9] = (byte)'\n';
                                _pWrite[tempOffset] = (byte)'\r';
                                _pWrite[tempOffset + 1] = (byte)'\n';
                            }
                            await SendAsync(tempOffset + 2);
                        }
                        else
                        {
                            await SendAsync(contentOffset);//_write.Length - _available
                        }
                        var count = _write.Length - 12;//10 2 最后留两个字节\r\n
                        for (; ; )
                        {
                            var result = await _request.Content.ReadAsync(_write.Slice(10, count));
                            if (result == 0)
                            {
                                unsafe
                                {
                                    _pWrite[0] = (byte)'0';
                                    _pWrite[1] = (byte)'\r';
                                    _pWrite[2] = (byte)'\n';
                                    _pWrite[3] = (byte)'\r';
                                    _pWrite[4] = (byte)'\n';////0\r\n\r\n
                                }
                                await _connection.SendAsync(_write.Slice(0, 5));
                                return;
                            }
                            var tempOffset = result + 10;
                            var tempCount = count - result;
                            if (tempCount >= 5 && _response.Content.Available == 0)
                            {
                                unsafe
                                {
                                    _pWrite[0] = _Hex[((result >> 0x1c) & 0x0f)];
                                    _pWrite[1] = _Hex[((result >> 0x18) & 0x0f)];
                                    _pWrite[2] = _Hex[((result >> 0x14) & 0x0f)];
                                    _pWrite[3] = _Hex[((result >> 0x10) & 0x0f)];
                                    _pWrite[4] = _Hex[((result >> 0x0c) & 0x0f)];
                                    _pWrite[5] = _Hex[((result >> 0x08) & 0x0f)];
                                    _pWrite[6] = _Hex[((result >> 0x04) & 0x0f)];
                                    _pWrite[7] = _Hex[((result >> 0x00) & 0x0f)];
                                    _pWrite[8] = (byte)'\r';
                                    _pWrite[9] = (byte)'\n';
                                    _pWrite[tempOffset] = (byte)'\r';
                                    _pWrite[tempOffset + 1] = (byte)'\n';
                                    _pWrite[tempOffset + 2] = (byte)'0';
                                    _pWrite[tempOffset + 3] = (byte)'\r';
                                    _pWrite[tempOffset + 4] = (byte)'\n';
                                    _pWrite[tempOffset + 5] = (byte)'\r';
                                    _pWrite[tempOffset + 6] = (byte)'\n';////0\r\n\r\n
                                }
                                await _connection.SendAsync(_write.Slice(0, tempOffset + 7));
                                return;
                            }
                            unsafe
                            {
                                _pWrite[0] = _Hex[((result >> 0x1c) & 0x0f)];
                                _pWrite[1] = _Hex[((result >> 0x18) & 0x0f)];
                                _pWrite[2] = _Hex[((result >> 0x14) & 0x0f)];
                                _pWrite[3] = _Hex[((result >> 0x10) & 0x0f)];
                                _pWrite[4] = _Hex[((result >> 0x0c) & 0x0f)];
                                _pWrite[5] = _Hex[((result >> 0x08) & 0x0f)];
                                _pWrite[6] = _Hex[((result >> 0x04) & 0x0f)];
                                _pWrite[7] = _Hex[((result >> 0x00) & 0x0f)];
                                _pWrite[8] = (byte)'\r';
                                _pWrite[9] = (byte)'\n';
                                _pWrite[tempOffset] = (byte)'\r';
                                _pWrite[tempOffset + 1] = (byte)'\n';
                            }
                            await _connection.SendAsync(_write.Slice(0, tempOffset + 2));
                        }
                        throw new InvalidDataException("chunked");
                        #endregion
                    }
                    else if (_request.Content == null)
                    {
                        WriteCrLf();
                        await SendAsync(_write.Length - _available);
                    }
                    else
                    {
                        #region ContentLength
                        if (_contentLength == -1)
                        {
                            _contentLength = _request.Content.ComputeLength();
                            if (_contentLength < 0)
                            {
                                var stream = await _request.Content.ReadStreamAsync();
                                _request.RegisterForDispose(stream);
                                _request.Content = new StreamContent(stream);
                                _contentLength = stream.Length;
                                Debug.Assert(_contentLength >= 0);
                            }
                            WriteContentLengthSpace();
                            Write(_contentLength);
                            WriteCrLfCrLf();
                        }
                        else
                        {
                            WriteCrLf();
                        }
                        if (_contentLength == 0)
                        {
                            await SendAsync(_write.Length - _available);
                            return;
                        }
                        Debug.Assert(_contentLength > 0);
                        var sum = 0;
                        var tempOffset = _write.Length - _available;
                        if (_contentLength <= _available || _available > 1024)
                        {
                            var result = await _request.Content.ReadAsync(_write.Slice(tempOffset, _available));
                            if (result == 0)
                                throw new ArgumentException(nameof(_request.Content));
                            tempOffset += result;
                            sum += result;
                        }
                        await SendAsync(tempOffset);
                        for (; ; )
                        {
                            var result = await _request.Content.ReadAsync(_write);
                            if (result == 0)
                            {
                                if (sum != _contentLength)
                                    throw new ArgumentException("content contentLength Not Match");
                                return;
                            }
                            sum += result;
                            if (sum > _contentLength)
                                throw new ArgumentException("content contentLength Not Match");

                            await _connection.SendAsync(_write.Slice(0, result));
                        }
                        throw new InvalidDataException("contentLength");
                        #endregion
                    }
                }
                finally
                {
                    #region Dispose
                    Debug.Assert(_write.Length > 0);
                    _transferChunked = false;
                    _contentLength = -1;
                    _available = 0;
                    _write = Memory<byte>.Empty;
                    _writeHandle.Dispose();
                    unsafe { _pWrite = (byte*)0; }
                    _writeDisposable.Dispose();
                    _writeDisposable = null;
                    if (_writeQueue != null)
                    {
                        while (_writeQueue.TryDequeue(out var _, out var disposable))
                        {
                            disposable.Dispose();
                        }
                        _writeQueue = null;
                    }
                    #endregion
                }
            }
            private async ValueTask ReceiveAsync()
            {
                Debug.Assert(_position == _end);
                if (_headerSize > _MaxHeaderSize)
                    throw new InvalidDataException("Response Header Too Long");

                if (_end < _read.Length)
                {
                    var result = await _connection.ReceiveAsync(_read.Slice(_end));
                    if (result == 0)
                        throw new InvalidDataException("FIN");

                    _end += result;
                    _headerSize += result;
                }
                else if (_start == _end)
                {
                    var result = await _connection.ReceiveAsync(_read);
                    if (result == 0)
                        throw new InvalidDataException("FIN");

                    _start = 0;
                    _position = 0;
                    _end = result;
                    _headerSize += result;
                }
                else
                {
                    Debug.Assert(_end == _read.Length);
                    if (_readQueue == null)
                    {
                        if (_start == 0 || (_start << 1) > _read.Length)//_start过半了
                        {
                            if (_readQueue == null)
                                _readQueue = new Queue<(Memory<byte>, IDisposable)>();

                            _readQueue.Enqueue((_read.Slice(_start), _readDisposable));
                            _readHandle.Dispose();

                            _read = ConnectionExtensions.GetBytes(out _readDisposable);
                            _readHandle = _read.Pin();
                            unsafe { _pRead = (byte*)_readHandle.Pointer; }

                            var result = await _connection.ReceiveAsync(_read);
                            if (result == 0)
                                throw new InvalidDataException("FIN");

                            _start = 0;
                            _position = 0;
                            _end = result;
                            _headerSize += result;
                        }
                        else
                        {
                            var count = _end - _start;
                            _read.Span.Slice(_start).CopyTo(_read.Span.Slice(0, count));

                            _start = 0;
                            _position = count;
                            _end = count;

                            var result = await _connection.ReceiveAsync(_read.Slice(_end));
                            if (result == 0)
                                throw new InvalidDataException("FIN");

                            _end += result;
                            _headerSize += result;
                        }
                    }
                    else
                    {
                        _readQueue.Enqueue((_read.Slice(_start), _readDisposable));
                        _readHandle.Dispose();

                        _read = ConnectionExtensions.GetBytes(out _readDisposable);
                        _readHandle = _read.Pin();
                        unsafe { _pRead = (byte*)_readHandle.Pointer; }

                        var result = await _connection.ReceiveAsync(_read);
                        if (result == 0)
                            throw new InvalidDataException("FIN");

                        _start = 0;
                        _position = 0;
                        _end = result;
                        _headerSize += result;
                    }
                }
            }
            private ReadOnlySpan<byte> ReadBytes()
            {
                if (_readQueue == null)
                {
                    unsafe
                    {
                        var span = new ReadOnlySpan<byte>(_pRead + _start, _position - _start - 1);
                        _start = _position;
                        return span;
                    }
                }
                else
                {
                    unsafe
                    {
                        var span = new ReadOnlySpan<byte>(_pRead + _start, _position - _start - 1);
                        _start = _position;

                        var length = span.Length;
                        foreach ((var read, var _) in _readQueue)
                        {
                            length += read.Length;
                        }
                        var bytes = new byte[length].AsSpan();
                        var tempBytes = bytes;
                        foreach ((var read, var disposable) in _readQueue)
                        {
                            read.Span.CopyTo(tempBytes);
                            tempBytes = tempBytes.Slice(read.Length);
                            disposable.Dispose();
                        }
                        span.CopyTo(tempBytes);//有问题
                        _readQueue = null;//删除临时队列
                        return bytes;
                    }
                }
            }
            private void ReadStatusCode()
            {
                var statusCodeBytes = ReadBytes();
                if (statusCodeBytes.Length > 11)
                    throw new FormatException(nameof(_response.StatusCode));

                Span<char> temp = stackalloc char[statusCodeBytes.Length];
                for (int i = 0; i < temp.Length; i++)
                {
                    temp[i] = (char)statusCodeBytes[i];
                }
                _response.StatusCode = int.Parse(temp);//throw 
            }
            private void ReadReasonPhrase()
            {
                var reasonPhraseBytes = ReadBytes();
                var reasonPhrase = FeaturesExtensions.GetReasonPhrase(_response.StatusCode);

                if (string.IsNullOrEmpty(reasonPhrase))
                {
                    _response.ReasonPhrase = reasonPhraseBytes.ToByteString();
                }
                else if (reasonPhraseBytes.EqualsByteString(reasonPhrase))
                {
                    _response.ReasonPhrase = reasonPhrase;
                }
                else
                {
                    _response.ReasonPhrase = reasonPhraseBytes.ToByteString();
                }
            }
            private void ReadHeader()
            {
                var headerBytes = ReadBytes();//{name}: {value}
                unsafe
                {
                    fixed (byte* pData = headerBytes)
                    {
                        var nameBytes = headerBytes.Slice(0, _headerName);
                        var valueBytes = headerBytes.Slice(_headerName + 2);
                        switch (_headerName)
                        {
                            case 3:
                                if (*(int*)pData == 979724097)//Age:
                                {
                                    _response.Headers.Add(HttpHeaders.Age, valueBytes.ToByteString());
                                    return;
                                }
                                break;
                            case 4:
                                if (*(int*)pData == 1702125892)//Date
                                {
                                    _response.Headers.Add(HttpHeaders.Date, valueBytes.ToByteString());
                                    return;
                                }
                                else if (*(int*)pData == 1734431813)//ETag
                                {
                                    _response.Headers.Add(HttpHeaders.ETag, valueBytes.ToByteString());
                                    return;
                                }
                                else if (*(int*)pData == 2037539158)//Vary
                                {
                                    _response.Headers.Add(HttpHeaders.Vary, valueBytes.ToByteString());
                                    return;
                                }
                                break;
                            case 5:
                                if (*(int*)pData == 1869376577 && *(pData + 4) == 119)//Allow
                                {
                                    _response.Headers.Add(HttpHeaders.Allow, valueBytes.ToByteString());
                                    return;
                                }
                                break;
                            case 6:
                                if (*(long*)pData == 2322294337967383891)//Server: /
                                {
                                    _response.Headers.Add(HttpHeaders.Server, valueBytes.ToByteString());
                                    return;
                                }
                                else if (*(long*)pData == 2322275680376681040)//Pragma: /
                                {
                                    _response.Headers.Add(HttpHeaders.Pragma, valueBytes.ToByteString());
                                    return;
                                }
                                break;
                            case 7:
                                if (*(long*)pData == 4211821618591201349)//Expires:
                                {
                                    _response.Headers.Add(HttpHeaders.Expires, valueBytes.ToByteString());
                                    return;
                                }
                                else if (*(long*)pData == 4208740731325932882)//Refresh:
                                {
                                    _response.Headers.Add(HttpHeaders.Refresh, valueBytes.ToByteString());
                                    return;
                                }
                                break;
                            case 8:
                                if (*(long*)pData == 7957695015157985100)//Location
                                {
                                    _response.Headers.Add(HttpHeaders.Location, valueBytes.ToByteString());
                                    return;
                                }
                                break;
                            case 10:
                                if (*(long*)pData == 7598807758576447299 && *(short*)(pData + 8) == 28271)//Connection
                                {
                                    if (valueBytes.EqualsByteString("keep-alive"))
                                    {
                                        _response.Headers.Add(HttpHeaders.Connection, "keep-alive");
                                    }
                                    else if (valueBytes.EqualsByteString("close"))
                                    {
                                        _response.Headers.Add(HttpHeaders.Connection, "close");
                                    }
                                    else if (valueBytes.EqualsByteString("Keep-Alive"))
                                    {
                                        _response.Headers.Add(HttpHeaders.Connection, "Keep-Alive");
                                    }
                                    else if (valueBytes.EqualsByteString("Close"))
                                    {
                                        _response.Headers.Add(HttpHeaders.Connection, "Close");
                                    }
                                    else
                                    {
                                        _response.Headers.Add(HttpHeaders.Connection, valueBytes.ToByteString());
                                    }
                                    return;
                                }
                                else if (*(long*)pData == 7741528618789266771 && *(short*)(pData + 8) == 25961)//Set-Cookie
                                {
                                    _response.Headers.Add(HttpHeaders.SetCookie, valueBytes.ToByteString());
                                    return;
                                }
                                break;
                            case 12:
                                if (*(long*)pData == 3275364211029339971 && *(int*)(pData + 8) == 1701869908)//Content-Type
                                {
                                    //text/html; charset=utf-8
                                    //text/html
                                    _response.Headers.Add(HttpHeaders.ContentType, valueBytes.ToByteString());
                                    return;
                                }
                                break;
                            case 13:
                                if (*(long*)pData == 8017301675215905091 && *(int*)(pData + 8) == 1869771886 && *(pData + 12) == 108)//Cache-Control
                                {
                                    _response.Headers.Add(HttpHeaders.CacheControl, valueBytes.ToByteString());
                                    return;
                                }
                                else if (*(long*)pData == 5921517111148241729 && *(int*)(pData + 8) == 1701277281 && *(pData + 12) == 115)//Accept-Ranges
                                {
                                    if (valueBytes.EqualsByteString("bytes"))
                                    {
                                        _response.Headers.Add(HttpHeaders.AcceptRanges, "bytes");
                                    }
                                    else
                                    {
                                        _response.Headers.Add(HttpHeaders.AcceptRanges, valueBytes.ToByteString());
                                    }
                                    return;
                                }
                                else if (*(long*)pData == 7237087983830262092 && *(int*)(pData + 8) == 1701406313 && *(pData + 12) == 100)//Last-Modified
                                {
                                    _response.Headers.Add(HttpHeaders.LastModified, valueBytes.ToByteString());
                                    return;
                                }
                                else if (*(long*)pData == 3275364211029339971 && *(int*)(pData + 8) == 1735287122 && *(pData + 12) == 101)//Content-Range
                                {
                                    _response.Headers.Add(HttpHeaders.ContentRange, valueBytes.ToByteString());
                                    return;
                                }
                                break;
                            case 14:
                                if (*(long*)pData == 3275364211029339971 && *(long*)(pData + 8) == 2322283407023695180)//Content-Length: /
                                {
                                    _response.Headers.Add(HttpHeaders.ContentLength, valueBytes.ToByteString());
                                    return;
                                }
                                break;
                            case 16:
                                if (*(long*)pData == 3275364211029339971 && *(long*)(pData + 8) == 7453010313431182917)//Content-Encoding
                                {
                                    if (valueBytes.EqualsByteString("gzip"))
                                    {
                                        _response.Headers.Add(HttpHeaders.ContentEncoding, "gzip");
                                    }
                                    else
                                    {
                                        _response.Headers.Add(HttpHeaders.ContentEncoding, valueBytes.ToByteString());
                                    }
                                    return;
                                }
                                break;
                            case 17:
                                if (*(long*)pData == 8243107338930713172 && *(long*)(pData + 8) == 7956000646299010349 && *(pData + 16) == 103)//Transfer-Encoding
                                {
                                    if (valueBytes.EqualsByteString("chunked"))
                                    {
                                        _response.Headers.Add(HttpHeaders.TransferEncoding, "chunked");
                                    }
                                    else
                                    {
                                        _response.Headers.Add(HttpHeaders.TransferEncoding, valueBytes.ToByteString());
                                    }
                                    return;
                                }
                                break;
                            case 19:
                                if (*(long*)pData == 3275364211029339971 && *(long*)(pData + 8) == 8388362703419435332 && *(int*)(pData + 16) == 980316009)//Content-Disposition:
                                {
                                    _response.Headers.Add(HttpHeaders.ContentDisposition, valueBytes.ToByteString());
                                    return;
                                }
                                break;
                            case 27:
                                if (*(long*)pData == 4840652113952596801 && *(long*)(pData + 8) == 4696529212334698095 && *(long*)(pData + 16) == 7598222578023361644 && *(int*)(pData + 24) == 980314471)//Access-Control-Allow-Origin:
                                {
                                    _response.Headers.Add("Access-Control-Allow-Origin", valueBytes.ToByteString());
                                    return;
                                }
                                break;
                            default:
                                break;
                        }
                        _response.Headers.Add(nameBytes.ToByteString(), valueBytes.ToByteString());
                    }
                }
            }
            private async Task ReadAsync()
            {
                #region Receive
                Debug.Assert(_read.IsEmpty);
                _read = ConnectionExtensions.GetBytes(out _readDisposable);
                _readHandle = _read.Pin();
                unsafe { _pRead = (byte*)_readHandle.Pointer; }
                var result = await _connection.ReceiveAsync(_read);
                if (result == 0)
                    throw new InvalidOperationException("FIN");
                _start = 0;
                _end = result;
                _headerSize = result;
                #endregion

                Debug.Assert(_response == null);
                _response = new HttpResponse();

                #region Version
                Debug.WriteLine("parse version");
                Debug.Assert(_start == 0);
                if (_end >= 9)
                {
                    unsafe
                    {
                        if (_pRead[8] != _SPByte)
                            throw new NotSupportedException(nameof(HttpVersion));

                        var versionLong = *(long*)(_pRead);
                        if (versionLong == _Version11Long)
                        {
                            _response.Version = HttpVersion.Version11;
                            _start += 9;
                            goto statusCode;
                        }
                        else if (versionLong == _Version10Long)
                        {
                            _response.Version = HttpVersion.Version10;
                            _start += 9;
                            goto statusCode;
                        }
                        else
                        {
                            throw new NotSupportedException(nameof(HttpVersion));
                        }
                    }
                }
                else
                {
                    for (_position = _start; ;)
                    {
                        for (; _position < _end;)
                        {
                            unsafe
                            {
                                if (_pRead[_position++] == _SPByte)
                                {
                                    var versionLong = BitConverter.ToInt64(ReadBytes());//exception
                                    if (versionLong == _Version11Long)
                                    {
                                        _response.Version = HttpVersion.Version11;
                                        goto statusCode;
                                    }
                                    else if (versionLong == _Version10Long)
                                    {
                                        _response.Version = HttpVersion.Version10;
                                        goto statusCode;
                                    }
                                    else
                                    {
                                        throw new NotSupportedException(nameof(HttpVersion));
                                    }
                                }
                            }
                        }
                        await ReceiveAsync();
                    }
                }
                #endregion

                #region StatusCode
                statusCode:
                Debug.WriteLine("parse status");
                if (_end - _start >= 8)
                {
                    unsafe
                    {
                        if (*(long*)(_pRead + _start) == _OKLong)
                        {
                            _response.StatusCode = 200;
                            _response.ReasonPhrase = "OK";
                            _start += 8;
                            _state = State.Lf;
                            goto headers;
                        }
                        else if (_pRead[_start + 3] == _SPByte)
                        {
                            var s1 = _pRead[_start];
                            var s2 = _pRead[_start + 1];
                            var s3 = _pRead[_start + 2];
                            if (s1 >= '0' && s1 <= '9' && s2 >= '0' && s2 <= '9' && s3 >= '0' && s3 <= '9')
                            {
                                _response.StatusCode = 100 * (s1 - '0') + 10 * (s2 - '0') + (s3 - '0');
                                _start += 4;
                                goto reasonPhrase;
                            }
                            else
                            {
                                var temp = stackalloc char[3] { (char)s1, (char)s2, (char)s3 };
                                _response.StatusCode = int.Parse(new Span<char>(temp, 3));//throw 
                                _start += 4;
                                goto reasonPhrase;
                            }
                        }
                    }
                }
                for (_position = _start; ;)
                {
                    for (; _position < _end;)
                    {
                        unsafe
                        {
                            var tempByte = _pRead[_position++];
                            if (tempByte == _SPByte)
                            {
                                ReadStatusCode();
                                goto reasonPhrase;
                            }
                            else if (tempByte == _CRByte)
                            {
                                ReadStatusCode();
                                _state = State.Cr;
                                goto headers;
                            }
                        }
                    }
                    await ReceiveAsync();
                }
                #endregion

                #region ReasonPhrase
                reasonPhrase:
                Debug.WriteLine("parse reasonPhrase");
                for (_position = _start; ;)
                {
                    for (; _position < _end;)
                    {
                        unsafe
                        {
                            if (_pRead[_position++] == _CRByte)
                            {
                                ReadReasonPhrase();
                                _state = State.Cr;
                                goto headers;
                            }
                        }
                    }
                    await ReceiveAsync();
                }
                #endregion

                #region Headers
                headers:
                Debug.WriteLine("parse headers");//headersLf
                for (_position = _start; ;)
                {
                    for (; _position < _end;)
                    {
                        unsafe
                        {
                            var tempByte = _pRead[_position++];
                            switch (_state)
                            {
                                case State.Cr:
                                    if (tempByte != _LFByte)
                                        throw new InvalidDataException(@"\r\n");
                                    _start += 1;
                                    _state = State.Lf;
                                    continue;
                                case State.Lf:
                                    if (tempByte == _CRByte)
                                    {
                                        _start += 1;
                                        _state = State.LfCr;
                                        continue;
                                    }
                                    goto case State.Name;
                                case State.LfCr:
                                    if (tempByte != _LFByte)
                                        throw new InvalidDataException(@"\r\n\r\n");
                                    _start += 1;
                                    if (!_connectionClose)
                                    {
                                        if (_response.Headers.TryGetValue(HttpHeaders.Connection, out var connection))
                                        {
                                            if (connection.EqualsIgnoreCase("close"))
                                            {
                                                _connectionClose = true;
                                            }
                                        }
                                        else if (_response.Version == HttpVersion.Version10)
                                        {
                                            _connectionClose = true;
                                        }
                                    }
                                    goto content;
                                case State.Name:
                                    for (; ; )
                                    {
                                        if (tempByte == _COLONByte)
                                        {
                                            _headerName = _position - _start - 1;//获取Name索引
                                            if (_readQueue != null)
                                            {
                                                foreach ((var read, var _) in _readQueue)
                                                {
                                                    _headerName += read.Length;
                                                }
                                            }
                                            _state = State.Colon;
                                            break;
                                        }
                                        if (_position == _end)//数据不足
                                            break;
                                        tempByte = _pRead[_position++];
                                    }
                                    continue;
                                case State.Colon:
                                    if (tempByte != _SPByte && tempByte != _HTByte)
                                        throw new InvalidDataException(@":[SP,HT]");
                                    _state = State.Value;
                                    continue;
                                case State.Value:
                                    for (; ; )
                                    {
                                        if (tempByte == _CRByte)
                                        {
                                            _state = State.Cr;
                                            ReadHeader();
                                            break;
                                        }
                                        if (_position == _end)
                                            break;
                                        tempByte = _pRead[_position++];
                                    }
                                    continue;
                            }
                        }
                    }
                    await ReceiveAsync();
                }
                #endregion

                #region Content
                content:
                Debug.WriteLine("parse content");
                if (!_headOnly && _response.StatusCode != 204)
                {
                    if (_response.Headers.TryGetValue(HttpHeaders.TransferEncoding, out var transferEncoding) && transferEncoding.EqualsIgnoreCaseWhiteSpace("chunked"))
                    {
                        _response.Content = new ChunkedContent(this);
                    }
                    else if (_response.Headers.TryGetValue(HttpHeaders.ContentLength, out var contentLengthValue))
                    {
                        if (!long.TryParse(contentLengthValue, out var contentLength) && contentLength < 0)
                            throw new FormatException(HttpHeaders.ContentLength);

                        if (contentLength > 0)
                        {
                            _response.Content = new ContentLengthContent(contentLength, this);
                        }
                    }
                    else if (_connectionClose && (_request.Version == HttpVersion.Version10 || _response.Version == HttpVersion.Version10))
                    {
                        _response.Content = new ConnectionCloseContent(this);
                    }
                }
                #endregion
            }
            public async Task<HttpResponse> SendAsync()
            {
                _headOnly = _request.Method == HttpMethod.Head;
                await WriteAsync();
                await ReadAsync();
                //TODO? Remove
                Debug.Assert(_response != null);
                FeaturesExtensions.RegisterForDispose(_request, _response);
                return _response;
            }
            public void Dispose()
            {
                if (_read.Length > 0)
                {
                    _read = Memory<byte>.Empty;
                    _readHandle.Dispose();
                    unsafe { _pRead = (byte*)0; }
                    _readDisposable.Dispose();
                    _readDisposable = null;
                }
            }
            private class ContentLengthContent : IHttpContent
            {
                public ContentLengthContent(long length, HttpClientConnection connection)
                {
                    Debug.Assert(length > 0);
                    _position = 0;
                    _length = length;
                    _connection = connection;
                }

                private long _position;
                private long _length;
                private HttpClientConnection _connection;
                public long Available => _length - _position;
                public long Length => _length;
                public bool Rewind() => false;
                public long ComputeLength() => _length;
                public int Read(Span<byte> buffer)
                {
                    if (_length == _position)
                        return 0;

                    var count = buffer.Length;
                    if (count == 0)
                        return 0;

                    var available = _length - _position;
                    var toRead = _connection._end - _connection._start;
                    if (toRead > 0)
                    {
                        if (toRead > available)
                            throw new InvalidDataException("not match");
                        toRead = toRead > count ? count : toRead;
                        _connection._read.Span.Slice(_connection._start, toRead).CopyTo(buffer);
                        _connection._start += toRead;
                        _position += toRead;
                        return toRead;
                    }
                    else
                    {
                        toRead = available > count ? count : (int)available;
                        var result = _connection._connection.Receive(buffer.Slice(0, toRead));
                        if (result == 0)
                            throw new InvalidDataException("FIN");
                        _position += result;
                        return result;
                    }
                }
                public int Read(byte[] buffer, int offset, int count)
                {
                    return Read(buffer.AsSpan(offset, count));
                }
                public async ValueTask<int> ReadAsync(Memory<byte> buffer)
                {
                    if (_length == _position)
                        return 0;

                    var count = buffer.Length;
                    if (count == 0)
                        return 0;

                    var available = _length - _position;
                    var toRead = _connection._end - _connection._start;
                    if (toRead > 0)
                    {
                        if (toRead > available)
                            throw new InvalidDataException("not match");
                        toRead = toRead > count ? count : toRead;
                        _connection._read.Span.Slice(_connection._start, toRead).CopyTo(buffer.Span);
                        _connection._start += toRead;
                        _position += toRead;
                        return toRead;
                    }
                    else
                    {
                        toRead = available > count ? count : (int)available;
                        var result = await _connection._connection.ReceiveAsync(buffer.Slice(0, toRead));
                        if (result == 0)
                            throw new InvalidDataException("FIN");
                        _position += result;
                        return result;
                    }
                }
                public ValueTask<int> ReadAsync(byte[] buffer, int offset, int count)
                {
                    return ReadAsync(buffer.AsMemory(offset, count));
                }
            }
            private class ChunkedContent : IHttpContent
            {
                private enum State { Size, Extension, SizeCr, Data, DataCr, Trailer, TrailerCr, TrailerCrLf, TrailerCrLfCr };
                public ChunkedContent(HttpClientConnection connection)
                {
                    _state = State.Size;
                    _connection = connection;
                }

                private State _state;
                private int _chunkSize;
                private int _trailer;
                private HttpClientConnection _connection;
                public long Available => _chunkSize == -1 ? 0 : -1;
                public long Length => -1;
                public bool Rewind() => false;
                public long ComputeLength() => -1;
                private void Receive()
                {
                    var result = _connection._connection.Receive(_connection._read.Span);
                    if (result == 0)
                        throw new InvalidDataException("FIN");
                    _connection._start = 0;
                    _connection._end = result;
                }
                private async ValueTask ReceiveAsync()
                {
                    var result = await _connection._connection.ReceiveAsync(_connection._read);
                    if (result == 0)
                        throw new InvalidDataException("FIN");
                    _connection._start = 0;
                    _connection._end = result;
                }
                public int Read(Span<byte> buffer)
                {
                    if (_chunkSize == -1)
                        return 0;

                    var count = buffer.Length;
                    if (count == 0)
                        return 0;

                    Debug.Assert(_chunkSize >= 0);
                    if (_chunkSize > 0)
                        goto chunkData;

                    for (; ; )
                    {
                        for (; _connection._start < _connection._end;)
                        {
                            unsafe
                            {
                                var tempByte = _connection._pRead[_connection._start++];
                                switch (_state)
                                {
                                    case State.Size:
                                        if (tempByte == _CRByte)
                                            _state = _chunkSize == 0 ? State.TrailerCr : State.SizeCr;
                                        else
                                        {
                                            if (tempByte >= 'a' && tempByte <= 'f')
                                                _chunkSize = 16 * _chunkSize + (10 + tempByte - 'a');
                                            else if (tempByte >= 'A' && tempByte <= 'F')
                                                _chunkSize = 16 * _chunkSize + (10 + tempByte - 'A');
                                            else if (tempByte >= '0' && tempByte <= '9')
                                                _chunkSize = 16 * _chunkSize + (tempByte - '0');
                                            else if (tempByte == ';')//; 扩展
                                                _state = State.Extension;
                                            else
                                                throw new InvalidDataException(nameof(_chunkSize));
                                        }
                                        continue;
                                    case State.Extension:
                                        if (tempByte == _CRByte)
                                        {
                                            _state = _chunkSize == 0 ? State.TrailerCr : State.SizeCr;
                                        }
                                        continue;
                                    case State.SizeCr:
                                        if (tempByte != _LFByte)
                                            throw new InvalidDataException(@"\r\n\r\n");
                                        goto chunkData;
                                    case State.Data:
                                        if (tempByte != _CRByte)
                                            throw new InvalidDataException(@"\r\n\r\n");
                                        _state = State.DataCr;
                                        continue;
                                    case State.DataCr:
                                        if (tempByte != _LFByte)
                                            throw new InvalidDataException(@"\r\n\r\n");
                                        _state = State.Size;
                                        continue;
                                    case State.TrailerCr:
                                        if (tempByte != _LFByte)
                                            throw new InvalidDataException(@"\r\n\r\n");
                                        _state = State.TrailerCrLf;
                                        continue;
                                    case State.TrailerCrLf:
                                        if (tempByte == _CRByte)
                                            _state = State.TrailerCrLfCr;
                                        else
                                            _state = State.Trailer;
                                        continue;
                                    case State.Trailer:
                                        if (tempByte == _CRByte)
                                            _state = State.TrailerCr;
                                        else
                                        {
                                            const int maxTrailer = 16 << 10;
                                            if (_trailer++ > maxTrailer)
                                                throw new InvalidDataException(nameof(maxTrailer));
                                        }
                                        continue;
                                    case State.TrailerCrLfCr:
                                        if (tempByte != _LFByte)
                                            throw new InvalidDataException(@"\r\n\r\n");
                                        _chunkSize = -1;
                                        return 0;
                                }
                            }
                        }
                        Receive();
                    }
                chunkData:
                    var toRead = _connection._end - _connection._start;
                    if (toRead > 0)
                    {
                        toRead = toRead > _chunkSize ? _chunkSize : toRead;
                        toRead = toRead > count ? count : toRead;
                        _connection._read.Span.Slice(_connection._start, toRead).CopyTo(buffer);
                        _connection._start += toRead;
                        _chunkSize -= toRead;
                        if (_chunkSize == 0)
                            _state = State.Data;
                        return toRead;
                    }
                    else
                    {
                        toRead = _chunkSize > count ? count : _chunkSize;
                        var result = _connection._connection.Receive(buffer.Slice(0, toRead));
                        if (result == 0)
                            throw new InvalidDataException("FIN");
                        _chunkSize -= result;
                        if (_chunkSize == 0)
                            _state = State.Data;
                        return result;
                    }
                }
                public int Read(byte[] buffer, int offset, int count)
                {
                    return Read(buffer.AsSpan(offset, count));
                }
                public async ValueTask<int> ReadAsync(Memory<byte> buffer)
                {
                    if (_chunkSize == -1)
                        return 0;

                    var count = buffer.Length;
                    if (count == 0)
                        return 0;

                    Debug.Assert(_chunkSize >= 0);
                    if (_chunkSize > 0)
                        goto chunkData;

                    for (; ; )
                    {
                        for (; _connection._start < _connection._end;)
                        {
                            unsafe
                            {
                                var tempByte = _connection._pRead[_connection._start++];
                                switch (_state)
                                {
                                    case State.Size:
                                        if (tempByte == _CRByte)
                                            _state = _chunkSize == 0 ? State.TrailerCr : State.SizeCr;
                                        else
                                        {
                                            if (tempByte >= 'a' && tempByte <= 'f')
                                                _chunkSize = 16 * _chunkSize + (10 + tempByte - 'a');
                                            else if (tempByte >= 'A' && tempByte <= 'F')
                                                _chunkSize = 16 * _chunkSize + (10 + tempByte - 'A');
                                            else if (tempByte >= '0' && tempByte <= '9')
                                                _chunkSize = 16 * _chunkSize + (tempByte - '0');
                                            else if (tempByte == ';')//; 扩展
                                                _state = State.Extension;
                                            else
                                                throw new InvalidDataException(nameof(_chunkSize));
                                        }
                                        continue;
                                    case State.Extension:
                                        if (tempByte == _CRByte)
                                        {
                                            _state = _chunkSize == 0 ? State.TrailerCr : State.SizeCr;
                                        }
                                        continue;
                                    case State.SizeCr:
                                        if (tempByte != _LFByte)
                                            throw new InvalidDataException(@"\r\n\r\n");
                                        goto chunkData;
                                    case State.Data:
                                        if (tempByte != _CRByte)
                                            throw new InvalidDataException(@"\r\n\r\n");
                                        _state = State.DataCr;
                                        continue;
                                    case State.DataCr:
                                        if (tempByte != _LFByte)
                                            throw new InvalidDataException(@"\r\n\r\n");
                                        _state = State.Size;
                                        continue;
                                    case State.TrailerCr:
                                        if (tempByte != _LFByte)
                                            throw new InvalidDataException(@"\r\n\r\n");
                                        _state = State.TrailerCrLf;
                                        continue;
                                    case State.TrailerCrLf:
                                        if (tempByte == _CRByte)
                                            _state = State.TrailerCrLfCr;
                                        else
                                            _state = State.Trailer;
                                        continue;
                                    case State.Trailer:
                                        if (tempByte == _CRByte)
                                            _state = State.TrailerCr;
                                        else
                                        {
                                            const int maxTrailer = 16 << 10;
                                            if (_trailer++ > maxTrailer)
                                                throw new InvalidDataException(nameof(maxTrailer));
                                        }
                                        continue;
                                    case State.TrailerCrLfCr:
                                        if (tempByte != _LFByte)
                                            throw new InvalidDataException(@"\r\n\r\n");
                                        _chunkSize = -1;
                                        return 0;
                                }
                            }
                        }
                        await ReceiveAsync();
                    }
                chunkData:
                    var toRead = _connection._end - _connection._start;
                    if (toRead > 0)
                    {
                        toRead = toRead > _chunkSize ? _chunkSize : toRead;
                        toRead = toRead > count ? count : toRead;
                        _connection._read.Span.Slice(_connection._start, toRead).CopyTo(buffer.Span);
                        _connection._start += toRead;
                        _chunkSize -= toRead;
                        if (_chunkSize == 0)
                            _state = State.Data;
                        return toRead;
                    }
                    else
                    {
                        toRead = _chunkSize > count ? count : _chunkSize;
                        var result = await _connection._connection.ReceiveAsync(buffer.Slice(0, toRead));
                        if (result == 0)
                            throw new InvalidDataException("FIN");
                        _chunkSize -= result;
                        if (_chunkSize == 0)
                            _state = State.Data;
                        return result;
                    }
                }
                public ValueTask<int> ReadAsync(byte[] buffer, int offset, int count)
                {
                    return ReadAsync(buffer.AsMemory(offset, count));
                }
            }
            private class ConnectionCloseContent : IHttpContent
            {
                public ConnectionCloseContent(HttpClientConnection connection)
                {
                    Debug.Assert(connection._connectionClose);
                    _connection = connection;
                }

                private HttpClientConnection _connection;
                public long Available => _connection == null ? 0 : -1;
                public long Length => -1;
                public bool Rewind() => false;
                public long ComputeLength() => -1;
                public int Read(Span<byte> buffer)
                {
                    if (_connection == null)
                        return 0;

                    var count = buffer.Length;
                    if (count == 0)
                        return 0;

                    var toRead = _connection._end - _connection._start;
                    if (toRead > 0)
                    {
                        toRead = toRead > count ? count : toRead;
                        _connection._read.Span.Slice(_connection._start, toRead).CopyTo(buffer);
                        _connection._start += toRead;
                        return toRead;
                    }
                    else
                    {
                        var result = _connection._connection.Receive(buffer);
                        if (result == 0)
                            _connection = null;
                        return result;
                    }
                }
                public int Read(byte[] buffer, int offset, int count)
                {
                    return Read(buffer.AsSpan(offset, count));
                }
                public async ValueTask<int> ReadAsync(Memory<byte> buffer)
                {
                    if (_connection == null)
                        return 0;

                    var count = buffer.Length;
                    if (count == 0)
                        return 0;

                    var toRead = _connection._end - _connection._start;
                    if (toRead > 0)
                    {
                        toRead = toRead > count ? count : toRead;
                        _connection._read.Span.Slice(_connection._start, toRead).CopyTo(buffer.Span);
                        _connection._start += toRead;
                        return toRead;
                    }
                    else
                    {
                        var result = await _connection._connection.ReceiveAsync(buffer);
                        if (result == 0)
                            _connection = null;
                        return result;
                    }
                }
                public ValueTask<int> ReadAsync(byte[] buffer, int offset, int count)
                {
                    return ReadAsync(buffer.AsMemory(offset, count));
                }
            }
        }
        public static Task ReceiveAsync(this IConnection @this, HttpRequest request)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var reader = new RequestReader(@this, request);
            return reader.ReadAsync();
        }
        public static Task SendAsync(this IConnection @this, HttpResponse response, HttpRequest request)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (response == null)
                throw new ArgumentNullException(nameof(response));
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var writer = new ResponseWriter(@this, response, request);
            return writer.WriteAsync();
        }
        private class RequestReader : IDisposable
        {
            #region const
            private const int _MaxHeaderSize = 64 * 1024;//64K
            private const byte _SPByte = (byte)' ', _HTByte = (byte)'\t', _CRByte = (byte)'\r', _LFByte = (byte)'\n', _COLONByte = (byte)':';
            private const long _GetLong = 542393671, _PostLong = 138853699408, _OptionsLong = 2329291534720323663,
                _HeadLong = 138584081736, _ConnectLong = 2329560872202948419, _DeleteLong = 9083427496936772,
                _PatchLong = 35494739329360, _TraceLong = 35481853186644, _PutLong = 542397776;
            private const long _Mask4 = 4294967295, _Mask5 = 1099511627775, _Mask6 = 281474976710655, _Mask7 = 72057594037927935, _Mask8 = -1;
            private const long _Version10Long = 3471766442030158920, _Version11Long = 3543824036068086856;
            private static byte[] _ContinueBytes = Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n");
            private enum State { Cr, Lf, LfCr, Name, Colon, Value };
            #endregion
            public RequestReader(IConnection connection, HttpRequest request)
            {
                Debug.Assert(connection != null);
                Debug.Assert(request != null);
                _connection = connection;
                _request = request;
                _request.RegisterForDispose(this);
            }

            private IConnection _connection;
            private HttpRequest _request;

            //headers
            private State _state;
            private int _headerName;
            private int _headerSize;

            //read
            private int _start;
            private int _end;
            private int _position;
            private unsafe byte* _pRead;
            private Memory<byte> _read;
            private MemoryHandle _readHandle;
            private IDisposable _readDisposable;
            private Queue<(Memory<byte>, IDisposable)> _readQueue;
            private async ValueTask ReceiveAsync()
            {
                Debug.Assert(_position == _end);
                if (_headerSize > _MaxHeaderSize)
                    throw new InvalidDataException("Request Header Too Long");

                if (_end < _read.Length)
                {
                    var result = await _connection.ReceiveAsync(_read.Slice(_end));
                    if (result == 0)
                        throw new InvalidDataException("FIN");

                    _end += result;
                    _headerSize += result;
                }
                else if (_start == _end)
                {
                    var result = await _connection.ReceiveAsync(_read);
                    if (result == 0)
                        throw new InvalidDataException("FIN");

                    _start = 0;
                    _position = 0;
                    _end = result;
                    _headerSize += result;
                }
                else
                {
                    Debug.Assert(_end == _read.Length);
                    if (_readQueue == null)
                    {
                        if (_start == 0 || (_start << 1) > _read.Length)//_start过半了
                        {
                            if (_readQueue == null)
                                _readQueue = new Queue<(Memory<byte>, IDisposable)>();

                            _readQueue.Enqueue((_read.Slice(_start), _readDisposable));
                            _readHandle.Dispose();

                            _read = ConnectionExtensions.GetBytes(out _readDisposable);
                            _readHandle = _read.Pin();
                            unsafe { _pRead = (byte*)_readHandle.Pointer; }

                            var result= await _connection.ReceiveAsync(_read);
                            if (result == 0)
                                throw new InvalidDataException("FIN");

                            _start = 0;
                            _position = 0;
                            _end = result;
                            _headerSize += result;
                        }
                        else
                        {
                            var count = _end - _start;
                            _read.Span.Slice(_start).CopyTo(_read.Span.Slice(0, count));

                            _start = 0;
                            _position = count;
                            _end = count;

                            var result = await _connection.ReceiveAsync(_read.Slice(_end));
                            if (result == 0)
                                throw new InvalidDataException("FIN");

                            _end += result;
                            _headerSize += result;
                        }
                    }
                    else
                    {
                        _readQueue.Enqueue((_read.Slice(_start), _readDisposable));
                        _readHandle.Dispose();

                        _read = ConnectionExtensions.GetBytes(out _readDisposable);
                        _readHandle = _read.Pin();
                        unsafe { _pRead = (byte*)_readHandle.Pointer; }


                        var result = await _connection.ReceiveAsync(_read);
                        if (result == 0)
                            throw new InvalidDataException("FIN");

                        _start = 0;
                        _position = 0;
                        _end = result;
                        _headerSize += _end;
                    }
                }
            }
            private ReadOnlySpan<byte> ReadBytes()
            {
                if (_readQueue == null)
                {
                    unsafe
                    {
                        var span = new ReadOnlySpan<byte>(_pRead + _start, _position - _start - 1);
                        _start = _position;
                        return span;
                    }
                }
                else
                {
                    unsafe
                    {
                        var span = new ReadOnlySpan<byte>(_pRead + _start, _position - _start - 1);
                        _start = _position;

                        var length = span.Length;
                        foreach ((var read, var _) in _readQueue)
                        {
                            length += read.Length;
                        }
                        var bytes = new byte[length].AsSpan();
                        var tempBytes = bytes;
                        foreach ((var read, var disposable) in _readQueue)
                        {
                            read.Span.CopyTo(tempBytes);
                            tempBytes = tempBytes.Slice(read.Length);
                            disposable.Dispose();
                        }
                        span.CopyTo(tempBytes);
                        _readQueue = null;
                        return bytes;
                    }
                }
            }
            private void ReadMethod()
            {
                var methodBytes = ReadBytes();
                if (methodBytes.EqualsByteString("GET"))
                {
                    _request.Method = HttpMethod.Get;
                }
                else if (methodBytes.EqualsByteString("POST"))
                {
                    _request.Method = HttpMethod.Post;
                }
                else if (methodBytes.EqualsByteString("OPTIONS"))
                {
                    _request.Method = HttpMethod.Options;
                }
                else if (methodBytes.EqualsByteString("HEAD"))
                {
                    _request.Method = HttpMethod.Head;
                }
                else if (methodBytes.EqualsByteString("DELETE"))
                {
                    _request.Method = HttpMethod.Delete;
                }
                else if (methodBytes.EqualsByteString("PUT"))
                {
                    _request.Method = HttpMethod.Put;
                }
                else if (methodBytes.EqualsByteString("CONNECT"))
                {
                    _request.Method = HttpMethod.Connect;
                }
                else
                {
                    throw new InvalidDataException(methodBytes.ToByteString()).StatusCode(405);
                }
            }
            private void ReadRequestUri()
            {
                var requestUri = ReadBytes();
                var length = requestUri.Length;
                if (length == 0)
                    throw new InvalidDataException("RequestUri Is Empty");

                if (requestUri[0] == '/')
                {
                    var queryIndex = -1;
                    var fragmentIndex = -1;
                    for (int i = 1; i < length; i++)
                    {
                        if (requestUri[i] == '?')
                        {
                            if (queryIndex == -1)
                                queryIndex = i;
                        }
                        else if (requestUri[i] == '#')
                        {
                            if (queryIndex == -1)
                                queryIndex = -2;
                            if (fragmentIndex == -1)
                                fragmentIndex = i;
                        }
                    }
                    if (queryIndex > 0)
                    {
                        _request.Url.Path = requestUri.Slice(0, queryIndex).ToByteString();
                        if (fragmentIndex > 0)
                        {
                            _request.Url.Query = requestUri.Slice(queryIndex, fragmentIndex - queryIndex).ToByteString();
                            _request.Url.Fragment = requestUri.Slice(fragmentIndex).ToByteString();
                        }
                        else
                        {
                            _request.Url.Query = requestUri.Slice(queryIndex).ToByteString();
                        }
                    }
                    else if (fragmentIndex > 0)
                    {
                        _request.Url.Path = requestUri.Slice(0, fragmentIndex).ToByteString();
                        _request.Url.Fragment = requestUri.Slice(fragmentIndex).ToByteString();
                    }
                    else
                    {
                        _request.Url.Path = requestUri.ToByteString();
                    }

                }
                else if (_request.Method == HttpMethod.Options && requestUri.Length == 1 && requestUri[0] == '*')
                {
                    Debug.WriteLine("OPTIONS *");
                }
                else if (_request.Method == HttpMethod.Connect)
                {
                    _request.Url.Authority = requestUri.ToByteString();
                }
                else
                {
                    _request.Url.AbsoluteUri = requestUri.ToByteString();
                }
            }
            private void ReadVersion()
            {
                var versionBytes = ReadBytes();
                if (versionBytes.Length != 8)
                    throw new InvalidDataException(versionBytes.ToByteString()).StatusCode(505);

                var versionLong = BitConverter.ToInt64(versionBytes);
                if (versionLong == _Version11Long)
                {
                    _request.Version = HttpVersion.Version11;
                }
                else if (versionLong == _Version10Long)
                {
                    _request.Version = HttpVersion.Version10;
                }
                else
                {
                    throw new InvalidDataException(versionBytes.ToByteString()).StatusCode(505);
                }
            }
            private void ReadHeader()
            {
                var headerBytes = ReadBytes();
                unsafe
                {
                    fixed (byte* pData = headerBytes)
                    {
                        var nameBytes = headerBytes.Slice(0, _headerName);
                        var valueBytes = headerBytes.Slice(_headerName + 2);
                        switch (_headerName)
                        {
                            case 2:
                                if (*(short*)pData == 17748)//TE
                                {
                                    _request.Headers.Add(HttpHeaders.TE, valueBytes.ToByteString());
                                    return;
                                }
                                break;
                            case 3:
                                if (*(int*)pData == 979462486)//Via:
                                {
                                    _request.Headers.Add(HttpHeaders.Via, valueBytes.ToByteString());
                                    return;
                                }
                                break;
                            case 4:
                                if (*(int*)pData == 1953722184)//Host
                                {
                                    _request.Headers.Add(HttpHeaders.Host, valueBytes.ToByteString());
                                    return;
                                }
                                else if (*(int*)pData == 1702125892)//Date
                                {
                                    _request.Headers.Add(HttpHeaders.Date, valueBytes.ToByteString());
                                    return;
                                }
                                break;
                            case 5:
                                if (*(int*)pData == 1735287122 && *(pData + 4) == 101)//Range
                                {
                                    _request.Headers.Add(HttpHeaders.Range, valueBytes.ToByteString());
                                    return;
                                }
                                break;
                            case 6:
                                if (*(long*)pData == 2322280061311348547)//Cookie: /
                                {
                                    _request.Headers.Add(HttpHeaders.Cookie, valueBytes.ToByteString());
                                    return;
                                }
                                else if (*(long*)pData == 2322296583949083457)//Accept: /
                                {
                                    _request.Headers.Add(HttpHeaders.Accept, valueBytes.ToByteString());
                                    return;
                                }
                                else if (*(long*)pData == 2322289956848497231)//Origin: /
                                {
                                    _request.Headers.Add(HttpHeaders.Origin, valueBytes.ToByteString());
                                    return;
                                }
                                else if (*(long*)pData == 2322275680376681040)//Pragma: /
                                {
                                    _request.Headers.Add(HttpHeaders.Pragma, valueBytes.ToByteString());
                                    return;
                                }
                                else if (*(long*)pData == 2322296528115365957)//Expect: /
                                {
                                    _request.Headers.Add(HttpHeaders.Expect, valueBytes.ToByteString());
                                    return;
                                }
                                break;
                            case 7:
                                if (*(long*)pData == 4211540143546721618)//Referer:
                                {
                                    _request.Headers.Add(HttpHeaders.Referer, valueBytes.ToByteString());
                                    return;
                                }
                                else if (*(long*)pData == 4207879796541583445)//Upgrade:
                                {
                                    _request.Headers.Add(HttpHeaders.Upgrade, valueBytes.ToByteString());
                                    return;
                                }
                                else if (*(long*)pData == 4208453775904629079)//Warning:
                                {
                                    _request.Headers.Add(HttpHeaders.Warning, valueBytes.ToByteString());
                                    return;
                                }
                                break;
                            case 8:
                                if (*(long*)pData == 7521983763894330953)//If-Match
                                {
                                    _request.Headers.Add(HttpHeaders.IfMatch, valueBytes.ToByteString());
                                    return;
                                }
                                else if (*(long*)pData == 7306930284701509193)//If-Range
                                {
                                    _request.Headers.Add(HttpHeaders.IfRange, valueBytes.ToByteString());
                                    return;
                                }
                                break;
                            case 10:
                                if (*(long*)pData == 7598807758576447299 && *(short*)(pData + 8) == 28271)//Connection
                                {
                                    if (valueBytes.EqualsByteString("keep-alive"))
                                    {
                                        _request.Headers.Add(HttpHeaders.Connection, "keep-alive");
                                    }
                                    else if (valueBytes.EqualsByteString("close"))
                                    {
                                        _request.Headers.Add(HttpHeaders.Connection, "close");
                                    }
                                    else if (valueBytes.EqualsByteString("Keep-Alive"))
                                    {
                                        _request.Headers.Add(HttpHeaders.Connection, "Keep-Alive");
                                    }
                                    else if (valueBytes.EqualsByteString("Close"))
                                    {
                                        _request.Headers.Add(HttpHeaders.Connection, "Close");
                                    }
                                    else
                                    {
                                        _request.Headers.Add(HttpHeaders.Connection, valueBytes.ToByteString());
                                    }
                                    return;
                                }
                                else if (*(long*)pData == 7306880583880504149 && *(short*)(pData + 8) == 29806)//User-Agent
                                {
                                    _request.Headers.Add(HttpHeaders.UserAgent, valueBytes.ToByteString());
                                    return;
                                }
                                break;
                            case 12:
                                if (*(long*)pData == 3275364211029339971 && *(int*)(pData + 8) == 1701869908)//Content-Type
                                {
                                    _request.Headers.Add(HttpHeaders.ContentType, valueBytes.ToByteString());
                                    return;
                                }
                                else if (*(long*)pData == 8607064185059696973 && *(int*)(pData + 8) == 1935962721)//Max-Forwards
                                {
                                    _request.Headers.Add(HttpHeaders.MaxForwards, valueBytes.ToByteString());
                                    return;
                                }
                                break;
                            case 13:
                                if (*(long*)pData == 8017301675215905091 && *(int*)(pData + 8) == 1869771886 && *(pData + 12) == 108)//Cache-Control
                                {
                                    _request.Headers.Add(HttpHeaders.CacheControl, valueBytes.ToByteString());
                                    return;
                                }
                                else if (*(long*)pData == 8820707168001226049 && *(int*)(pData + 8) == 1869182049 && *(pData + 12) == 110)//Authorization
                                {
                                    _request.Headers.Add(HttpHeaders.Authorization, valueBytes.ToByteString());
                                    return;
                                }
                                else if (*(long*)pData == 3271142128686556745 && *(int*)(pData + 8) == 1668571469 && *(pData + 12) == 104)//If-None-Match
                                {
                                    _request.Headers.Add(HttpHeaders.IfNoneMatch, valueBytes.ToByteString());
                                    return;
                                }
                                break;
                            case 14:
                                if (*(long*)pData == 3275364211029339971 && *(long*)(pData + 8) == 2322283407023695180)//Content-Length: /
                                {
                                    _request.Headers.Add(HttpHeaders.ContentLength, valueBytes.ToByteString());
                                    return;
                                }
                                else if (*(long*)pData == 4840653200579322689 && *(long*)(pData + 8) == 2322296536940306792)//Accept-Charset: /
                                {
                                    _request.Headers.Add(HttpHeaders.AcceptCharset, valueBytes.ToByteString());
                                    return;
                                }
                                break;
                            case 15:
                                if (*(long*)pData == 4984768388655178561 && *(long*)(pData + 8) == 4208453775736660846)//Accept-Encoding:
                                {
                                    _request.Headers.Add(HttpHeaders.AcceptEncoding, valueBytes.ToByteString());
                                    return;
                                }
                                else if (*(long*)pData == 5489171546920674113 && *(long*)(pData + 8) == 4207883095126797921)//Accept-Language:
                                {
                                    _request.Headers.Add(HttpHeaders.AcceptLanguage, valueBytes.ToByteString());
                                    return;
                                }
                                break;
                            case 17:
                                if (*(long*)pData == 8243107338930713172 && *(long*)(pData + 8) == 7956000646299010349 && *(pData + 16) == 103)//Transfer-Encoding
                                {
                                    if (valueBytes.EqualsByteString("chunked"))
                                    {
                                        _request.Headers.Add(HttpHeaders.TransferEncoding, "chunked");
                                    }
                                    else
                                    {
                                        _request.Headers.Add(HttpHeaders.TransferEncoding, valueBytes.ToByteString());
                                    }
                                    return;
                                }
                                else if (*(long*)pData == 7379539893622236745 && *(long*)(pData + 8) == 7164779863157794153 && *(pData + 16) == 101)//If-Modified-Since
                                {
                                    _request.Headers.Add(HttpHeaders.IfModifiedSince, valueBytes.ToByteString());
                                    return;
                                }
                                break;
                            case 19:
                                if (*(long*)pData == 7237123446850545225 && *(long*)(pData + 8) == 7589459706270803561 && *(int*)(pData + 16) == 979723118)//If-Unmodified-Since:
                                {
                                    _request.Headers.Add(HttpHeaders.IfUnmodifiedSince, valueBytes.ToByteString());
                                    return;
                                }
                                else if (*(long*)pData == 8449084375658623568 && *(long*)(pData + 8) == 8386118574450632820 && *(int*)(pData + 16) == 980316009)//Proxy-Authorization:
                                {
                                    _request.Headers.Add(HttpHeaders.ProxyAuthorization, valueBytes.ToByteString());
                                    return;
                                }
                                break;
                            case 25:
                                if (*(long*)pData == 3271131074048520277 && *(long*)(pData + 8) == 7310034214940012105 && *(long*)(pData + 16) == 8391162085809410605 && *(pData + 24) == 115)//Upgrade-Insecure-Requests
                                {
                                    _request.Headers.Add(HttpHeaders.UpgradeInsecureRequests, valueBytes.ToByteString());
                                    return;
                                }
                                break;
                            default:
                                break;
                        }
                        _request.Headers.Add(nameBytes.ToByteString(), valueBytes.ToByteString());
                    }
                }
            }
            public async Task ReadAsync()
            {
                Debug.Assert(_start == 0);
                _read = ConnectionExtensions.GetBytes(out _readDisposable);
                _readHandle = _read.Pin();
                unsafe { _pRead = (byte*)_readHandle.Pointer; }
                _headerSize = await _connection.ReceiveAsync(_read);
                if (_headerSize == 0)
                    throw new InvalidDataException("FIN");
                _end = _headerSize;

                #region Method
                Debug.WriteLine("parse method");
                if (_end - _start >= 8)
                {
                    unsafe
                    {
                        var methodLong = *(long*)(_pRead + _start);
                        if ((methodLong & _Mask4) == _GetLong)
                        {
                            _request.Method = HttpMethod.Get;
                            _start += 4;
                        }
                        else if ((methodLong & _Mask5) == _PostLong)
                        {
                            _request.Method = HttpMethod.Post;
                            _start += 5;
                        }
                        else if ((methodLong & _Mask8) == _OptionsLong)
                        {
                            _request.Method = HttpMethod.Options;
                            _start += 8;
                        }
                        else if ((methodLong & _Mask5) == _HeadLong)
                        {
                            _request.Method = HttpMethod.Head;
                            _start += 5;
                        }
                        else if ((methodLong & _Mask7) == _DeleteLong)
                        {
                            _request.Method = HttpMethod.Delete;
                            _start += 7;
                        }
                        else if ((methodLong & _Mask4) == _PutLong)
                        {
                            _request.Method = HttpMethod.Put;
                            _start += 4;
                        }
                        else if ((methodLong & _Mask8) == _ConnectLong)
                        {
                            _request.Method = HttpMethod.Connect;
                            _start += 8;
                        }
                        else
                        {
                            //var temp = Encoding.ASCII.GetString(_pRead + _start, _end - _start);
                            throw new InvalidDataException(new ReadOnlySpan<byte>(_pRead + _start, 8).ToByteString()).StatusCode(405);
                        }
                        goto requestUri;
                    }
                }
                else
                {
                    for (_position = _start; ;)
                    {
                        for (; _position < _end;)
                        {
                            unsafe
                            {
                                if (_pRead[_position++] == _SPByte)
                                {
                                    ReadMethod();
                                    goto requestUri;
                                }
                            }
                        }
                        await ReceiveAsync();
                    }
                }
                #endregion

                #region RequestUri
                requestUri:
                Debug.WriteLine("parse requestUri");//TODO utf-8支持? 判断>127
                for (_position = _start; ;)
                {
                    for (; _position < _end;)
                    {
                        unsafe
                        {
                            if (_pRead[_position++] == _SPByte)
                            {
                                ReadRequestUri();
                                goto version;
                            }
                        }
                    }
                    await ReceiveAsync();
                }
                #endregion

                #region Version
                version:
                Debug.WriteLine("parse version");
                if (_end - _start >= 9)
                {
                    unsafe
                    {
                        if (_pRead[_start + 8] != _CRByte)
                            throw new InvalidDataException(new ReadOnlySpan<byte>(_pRead + _start, 8).ToByteString()).StatusCode(505);

                        long versionLong = *(long*)(_pRead + _start);
                        if (versionLong == _Version11Long)
                        {
                            _request.Version = HttpVersion.Version11;
                            _start += 9;
                            _state = State.Cr;
                            goto headers;
                        }
                        else if (versionLong == _Version10Long)
                        {
                            _request.Version = HttpVersion.Version10;
                            _start += 9;
                            _state = State.Cr;
                            goto headers;
                        }
                        else
                        {
                            throw new InvalidDataException(new ReadOnlySpan<byte>(_pRead + _start, 8).ToByteString()).StatusCode(505);
                        }
                    }
                }
                else
                {
                    for (_position = _start; ;)
                    {
                        for (; _position < _end;)
                        {
                            unsafe
                            {
                                if (_pRead[_position++] == _CRByte)
                                {
                                    ReadVersion();
                                    _state = State.Cr;
                                    goto headers;
                                }
                            }
                        }
                        await ReceiveAsync();
                    }
                }
                #endregion

                #region Headers
                headers:
                Debug.WriteLine("parse headers");//headersLf
                for (_position = _start; ;)
                {
                    for (; _position < _end;)
                    {
                        unsafe
                        {
                            var tempByte = _pRead[_position++];
                            switch (_state)
                            {
                                case State.Cr:
                                    if (tempByte != _LFByte)
                                        throw new InvalidDataException(@"\r\n");
                                    _start += 1;
                                    _state = State.Lf;
                                    continue;
                                case State.Lf:
                                    if (tempByte == _CRByte)
                                    {
                                        _start += 1;
                                        _state = State.LfCr;
                                        continue;
                                    }
                                    goto case State.Name;
                                case State.LfCr:
                                    if (tempByte != _LFByte)
                                        throw new InvalidDataException(@"\r\n\r\n");
                                    _start += 1;
                                    #region Host Connection
                                    if (string.IsNullOrEmpty(_request.Url.Scheme))
                                    {
                                        _request.Url.Scheme = _connection.Security == null ? Url.SchemeHttp : Url.SchemeHttps;
                                        if (_request.Headers.TryGetValue(HttpHeaders.Host, out var authority))
                                        {
                                            _request.Url.Authority = authority;
                                        }
                                        else
                                        {
                                            throw new InvalidDataException("Invalid Host");
                                        }
                                    }
                                    #endregion
                                    goto content;
                                case State.Name:
                                    for (; ; )
                                    {
                                        if (tempByte == _COLONByte)
                                        {
                                            _headerName = _position - _start - 1;
                                            if (_readQueue != null)
                                            {
                                                foreach ((var read, var _) in _readQueue)
                                                {
                                                    _headerName += read.Length;
                                                }
                                            }
                                            _state = State.Colon;
                                            break;
                                        }
                                        if (_position == _end)
                                            break;
                                        tempByte = _pRead[_position++];
                                    }
                                    continue;
                                case State.Colon:
                                    if (tempByte != _SPByte && tempByte != _HTByte)
                                        throw new InvalidDataException(@":[SP,HT]");
                                    _state = State.Value;
                                    continue;
                                case State.Value:
                                    for (; ; )
                                    {
                                        if (tempByte == _CRByte)
                                        {
                                            _state = State.Cr;
                                            ReadHeader();
                                            break;
                                        }
                                        if (_position == _end)
                                            break;
                                        tempByte = _pRead[_position++];
                                    }
                                    continue;
                            }
                        }
                    }
                    await ReceiveAsync();
                }
                #endregion

                #region Content
                content:
                Debug.WriteLine("parse content");
                if (_request.Headers.TryGetValue(HttpHeaders.TransferEncoding, out var transferEncoding) && transferEncoding.EqualsIgnoreCaseWhiteSpace("chunked"))
                {
                    if (_start >= _end)
                    {
                        if (_request.Headers.TryGetValue(HttpHeaders.Expect, out var expect) && "100-continue".EqualsIgnoreCase(expect))
                            await _connection.SendAsync(_ContinueBytes);
                    }
                    _request.Content = new ChunkedContent(this);
                }
                else if (_request.Headers.TryGetValue(HttpHeaders.ContentLength, out var contentLengthValue))
                {
                    if (!long.TryParse(contentLengthValue, out var contentLength) && contentLength < 0)
                        throw new FormatException($"{HttpHeaders.ContentLength}:{contentLengthValue}");

                    if (contentLength > 0)
                    {
                        if (_start >= _end)
                        {
                            if (_request.Headers.TryGetValue(HttpHeaders.Expect, out var expect) && "100-continue".EqualsIgnoreCase(expect))
                                await _connection.SendAsync(_ContinueBytes);
                        }
                        _request.Content = new ContentLengthContent(contentLength, this);
                    }
                }
                #endregion
            }
            public void Dispose()
            {
                Debug.Assert(_read.Length > 0);
                _read = Memory<byte>.Empty;
                _readHandle.Dispose();
                unsafe { _pRead = (byte*)0; }
                _readDisposable.Dispose();
                _readDisposable = null;
                if (_readQueue != null)
                {
                    while (_readQueue.TryDequeue(out var _, out var disposable))
                    {
                        disposable.Dispose();
                    }
                    _readQueue = null;
                }
            }
            private class ContentLengthContent : IHttpContent
            {
                public ContentLengthContent(long length, RequestReader reader)
                {
                    Debug.Assert(length > 0);
                    _position = 0;
                    _length = length;
                    _reader = reader;
                }

                private long _position;
                private long _length;
                private RequestReader _reader;
                public long Available => _length - _position;
                public long Length => _length;
                public bool Rewind() => false;
                public long ComputeLength() => _length;
                public int Read(Span<byte> buffer)
                {
                    if (_length == _position)
                        return 0;

                    var count = buffer.Length;
                    if (count == 0)
                        return 0;

                    var available = _length - _position;
                    var toRead = _reader._end - _reader._start;
                    if (toRead > 0)
                    {
                        toRead = toRead > available ? (int)available : toRead;
                        toRead = toRead > count ? count : toRead;
                        _reader._read.Span.Slice(_reader._start, toRead).CopyTo(buffer);
                        _reader._start += toRead;
                        _position += toRead;
                        return toRead;
                    }
                    else
                    {
                        toRead = available > count ? count : (int)available;
                        var result = _reader._connection.Receive(buffer.Slice(0, toRead));
                        if (result == 0)
                            throw new InvalidDataException("FIN");
                        _position += result;
                        return result;
                    }
                }
                public int Read(byte[] buffer, int offset, int count)
                {
                    return Read(buffer.AsSpan(offset, count));
                }
                public async ValueTask<int> ReadAsync(Memory<byte> buffer)
                {
                    if (_length == _position)
                        return 0;

                    var count = buffer.Length;
                    if (count == 0)
                        return 0;

                    var available = _length - _position;
                    var toRead = _reader._end - _reader._start;
                    if (toRead > 0)
                    {
                        toRead = toRead > available ? (int)available : toRead;
                        toRead = toRead > count ? count : toRead;
                        _reader._read.Span.Slice(_reader._start, toRead).CopyTo(buffer.Span);
                        _reader._start += toRead;
                        _position += toRead;
                        return toRead;
                    }
                    else
                    {
                        toRead = available > count ? count : (int)available;
                        var result = await _reader._connection.ReceiveAsync(buffer.Slice(0, toRead));
                        if (result == 0)
                            throw new InvalidDataException("FIN");
                        _position += result;
                        return result;
                    }
                }
                public ValueTask<int> ReadAsync(byte[] buffer, int offset, int count)
                {
                    return ReadAsync(buffer.AsMemory(offset, count));
                }
            }
            private class ChunkedContent : IHttpContent
            {
                private enum State { Size, Extension, SizeCr, Data, DataCr, Trailer, TrailerCr, TrailerCrLf, TrailerCrLfCr };
                public ChunkedContent(RequestReader reader)
                {
                    _state = State.Size;
                    _reader = reader;
                }

                private State _state;
                private int _chunkSize;
                private int _trailer;
                private RequestReader _reader;
                public long Available => _chunkSize == -1 ? 0 : -1;
                public long Length => -1;
                public bool Rewind() => false;
                public long ComputeLength() => -1;
                private void Receive()
                {
                    var result = _reader._connection.Receive(_reader._read.Span);
                    if (result == 0)
                        throw new InvalidDataException("FIN");
                    _reader._start = 0;
                    _reader._end = result;
                }
                private async ValueTask ReceiveAsync()
                {
                    var result = await _reader._connection.ReceiveAsync(_reader._read);
                    if (result == 0)
                        throw new InvalidDataException("FIN");
                    _reader._start = 0;
                    _reader._end = result;
                }
                public int Read(Span<byte> buffer)
                {
                    if (_chunkSize == -1)
                        return 0;

                    var count = buffer.Length;
                    if (count == 0)
                        return 0;

                    Debug.Assert(_chunkSize >= 0);
                    if (_chunkSize > 0)
                        goto chunkData;

                    for (; ; )
                    {
                        for (; _reader._start < _reader._end;)
                        {
                            unsafe
                            {
                                var tempByte = _reader._pRead[_reader._start++];
                                switch (_state)
                                {
                                    case State.Size:
                                        if (tempByte == _CRByte)
                                            _state = _chunkSize == 0 ? State.TrailerCr : State.SizeCr;
                                        else
                                        {
                                            if (tempByte >= 'a' && tempByte <= 'f')
                                                _chunkSize = 16 * _chunkSize + (10 + tempByte - 'a');
                                            else if (tempByte >= 'A' && tempByte <= 'F')
                                                _chunkSize = 16 * _chunkSize + (10 + tempByte - 'A');
                                            else if (tempByte >= '0' && tempByte <= '9')
                                                _chunkSize = 16 * _chunkSize + (tempByte - '0');
                                            else if (tempByte == ';')//; 扩展
                                                _state = State.Extension;
                                            else
                                                throw new InvalidDataException(nameof(_chunkSize));
                                        }
                                        continue;
                                    case State.Extension:
                                        if (tempByte == _CRByte)
                                        {
                                            _state = _chunkSize == 0 ? State.TrailerCr : State.SizeCr;
                                        }
                                        continue;
                                    case State.SizeCr:
                                        if (tempByte != _LFByte)
                                            throw new InvalidDataException(@"\r\n\r\n");
                                        goto chunkData;
                                    case State.Data:
                                        if (tempByte != _CRByte)
                                            throw new InvalidDataException(@"\r\n\r\n");
                                        _state = State.DataCr;
                                        continue;
                                    case State.DataCr:
                                        if (tempByte != _LFByte)
                                            throw new InvalidDataException(@"\r\n\r\n");
                                        _state = State.Size;
                                        continue;
                                    case State.TrailerCr:
                                        if (tempByte != _LFByte)
                                            throw new InvalidDataException(@"\r\n\r\n");
                                        _state = State.TrailerCrLf;
                                        continue;
                                    case State.TrailerCrLf:
                                        if (tempByte == _CRByte)
                                            _state = State.TrailerCrLfCr;
                                        else
                                            _state = State.Trailer;
                                        continue;
                                    case State.Trailer:
                                        if (tempByte == _CRByte)
                                            _state = State.TrailerCr;
                                        else
                                        {
                                            const int maxTrailer = 16 << 10;
                                            if (_trailer++ > maxTrailer)
                                                throw new InvalidDataException(nameof(maxTrailer));
                                        }
                                        continue;
                                    case State.TrailerCrLfCr:
                                        if (tempByte != _LFByte)
                                            throw new InvalidDataException(@"\r\n\r\n");
                                        _chunkSize = -1;
                                        return 0;
                                }
                            }
                        }
                        Receive();
                    }
                chunkData:
                    var toRead = _reader._end - _reader._start;
                    if (toRead > 0)
                    {
                        toRead = toRead > _chunkSize ? _chunkSize : toRead;
                        toRead = toRead > count ? count : toRead;
                        _reader._read.Span.Slice(_reader._start, toRead).CopyTo(buffer);
                        _reader._start += toRead;
                        _chunkSize -= toRead;
                        if (_chunkSize == 0)
                            _state = State.Data;
                        return toRead;
                    }
                    else
                    {
                        toRead = _chunkSize > count ? count : _chunkSize;
                        var result = _reader._connection.Receive(buffer.Slice(0, toRead));
                        if (result == 0)
                            throw new InvalidDataException("FIN");
                        _chunkSize -= result;
                        if (_chunkSize == 0)
                            _state = State.Data;
                        return result;
                    }
                }
                public int Read(byte[] buffer, int offset, int count)
                {
                    return Read(buffer.AsSpan(offset, count));
                }
                public async ValueTask<int> ReadAsync(Memory<byte> buffer)
                {
                    if (_chunkSize == -1)
                        return 0;

                    var count = buffer.Length;
                    if (count == 0)
                        return 0;

                    Debug.Assert(_chunkSize >= 0);
                    if (_chunkSize > 0)
                        goto chunkData;

                    for (; ; )
                    {
                        for (; _reader._start < _reader._end;)
                        {
                            unsafe
                            {
                                var tempByte = _reader._pRead[_reader._start++];
                                switch (_state)
                                {
                                    case State.Size:
                                        if (tempByte == _CRByte)
                                            _state = _chunkSize == 0 ? State.TrailerCr : State.SizeCr;
                                        else
                                        {
                                            if (tempByte >= 'a' && tempByte <= 'f')
                                                _chunkSize = 16 * _chunkSize + (10 + tempByte - 'a');
                                            else if (tempByte >= 'A' && tempByte <= 'F')
                                                _chunkSize = 16 * _chunkSize + (10 + tempByte - 'A');
                                            else if (tempByte >= '0' && tempByte <= '9')
                                                _chunkSize = 16 * _chunkSize + (tempByte - '0');
                                            else if (tempByte == ';')//; 扩展
                                                _state = State.Extension;
                                            else
                                                throw new InvalidDataException(nameof(_chunkSize));
                                        }
                                        continue;
                                    case State.Extension:
                                        if (tempByte == _CRByte)
                                        {
                                            _state = _chunkSize == 0 ? State.TrailerCr : State.SizeCr;
                                        }
                                        continue;
                                    case State.SizeCr:
                                        if (tempByte != _LFByte)
                                            throw new InvalidDataException(@"\r\n\r\n");
                                        goto chunkData;
                                    case State.Data:
                                        if (tempByte != _CRByte)
                                            throw new InvalidDataException(@"\r\n\r\n");
                                        _state = State.DataCr;
                                        continue;
                                    case State.DataCr:
                                        if (tempByte != _LFByte)
                                            throw new InvalidDataException(@"\r\n\r\n");
                                        _state = State.Size;
                                        continue;
                                    case State.TrailerCr:
                                        if (tempByte != _LFByte)
                                            throw new InvalidDataException(@"\r\n\r\n");
                                        _state = State.TrailerCrLf;
                                        continue;
                                    case State.TrailerCrLf:
                                        if (tempByte == _CRByte)
                                            _state = State.TrailerCrLfCr;
                                        else
                                            _state = State.Trailer;
                                        continue;
                                    case State.Trailer:
                                        if (tempByte == _CRByte)
                                            _state = State.TrailerCr;
                                        else
                                        {
                                            const int maxTrailer = 16 << 10;
                                            if (_trailer++ > maxTrailer)
                                                throw new InvalidDataException(nameof(maxTrailer));
                                        }
                                        continue;
                                    case State.TrailerCrLfCr:
                                        if (tempByte != _LFByte)
                                            throw new InvalidDataException(@"\r\n\r\n");
                                        _chunkSize = -1;
                                        return 0;
                                }
                            }
                        }
                        await ReceiveAsync();
                    }
                chunkData:
                    var toRead = _reader._end - _reader._start;
                    if (toRead > 0)
                    {
                        toRead = toRead > _chunkSize ? _chunkSize : toRead;
                        toRead = toRead > count ? count : toRead;
                        _reader._read.Span.Slice(_reader._start, toRead).CopyTo(buffer.Span);
                        _reader._start += toRead;
                        _chunkSize -= toRead;
                        if (_chunkSize == 0)
                            _state = State.Data;
                        return toRead;
                    }
                    else
                    {
                        toRead = _chunkSize > count ? count : _chunkSize;
                        var result = await _reader._connection.ReceiveAsync(buffer.Slice(0, toRead));
                        if (result == 0)
                            throw new InvalidDataException("FIN");
                        _chunkSize -= result;
                        if (_chunkSize == 0)
                            _state = State.Data;
                        return result;
                    }
                }
                public ValueTask<int> ReadAsync(byte[] buffer, int offset, int count)
                {
                    return ReadAsync(buffer.AsMemory(offset, count));
                }
            }
        }
        private class ResponseWriter
        {
            #region const
            private static byte[] _Hex = Encoding.ASCII.GetBytes("0123456789abcdef");
            private static byte[] _Status100 = Encoding.ASCII.GetBytes($" 100 {FeaturesExtensions.GetReasonPhrase(100)}\r\n");
            private static byte[] _Status101 = Encoding.ASCII.GetBytes($" 101 {FeaturesExtensions.GetReasonPhrase(101)}\r\n");
            private static byte[] _Status102 = Encoding.ASCII.GetBytes($" 102 {FeaturesExtensions.GetReasonPhrase(102)}\r\n");
            private static byte[] _Status200 = Encoding.ASCII.GetBytes($" 200 {FeaturesExtensions.GetReasonPhrase(200)}\r\n");
            private static byte[] _Status201 = Encoding.ASCII.GetBytes($" 201 {FeaturesExtensions.GetReasonPhrase(201)}\r\n");
            private static byte[] _Status202 = Encoding.ASCII.GetBytes($" 202 {FeaturesExtensions.GetReasonPhrase(202)}\r\n");
            private static byte[] _Status203 = Encoding.ASCII.GetBytes($" 203 {FeaturesExtensions.GetReasonPhrase(203)}\r\n");
            private static byte[] _Status204 = Encoding.ASCII.GetBytes($" 204 {FeaturesExtensions.GetReasonPhrase(204)}\r\n");
            private static byte[] _Status205 = Encoding.ASCII.GetBytes($" 205 {FeaturesExtensions.GetReasonPhrase(205)}\r\n");
            private static byte[] _Status206 = Encoding.ASCII.GetBytes($" 206 {FeaturesExtensions.GetReasonPhrase(206)}\r\n");
            private static byte[] _Status207 = Encoding.ASCII.GetBytes($" 207 {FeaturesExtensions.GetReasonPhrase(207)}\r\n");
            private static byte[] _Status226 = Encoding.ASCII.GetBytes($" 226 {FeaturesExtensions.GetReasonPhrase(226)}\r\n");
            private static byte[] _Status300 = Encoding.ASCII.GetBytes($" 300 {FeaturesExtensions.GetReasonPhrase(300)}\r\n");
            private static byte[] _Status301 = Encoding.ASCII.GetBytes($" 301 {FeaturesExtensions.GetReasonPhrase(301)}\r\n");
            private static byte[] _Status302 = Encoding.ASCII.GetBytes($" 302 {FeaturesExtensions.GetReasonPhrase(302)}\r\n");
            private static byte[] _Status303 = Encoding.ASCII.GetBytes($" 303 {FeaturesExtensions.GetReasonPhrase(303)}\r\n");
            private static byte[] _Status304 = Encoding.ASCII.GetBytes($" 304 {FeaturesExtensions.GetReasonPhrase(304)}\r\n");
            private static byte[] _Status305 = Encoding.ASCII.GetBytes($" 305 {FeaturesExtensions.GetReasonPhrase(305)}\r\n");
            private static byte[] _Status307 = Encoding.ASCII.GetBytes($" 307 {FeaturesExtensions.GetReasonPhrase(307)}\r\n");
            private static byte[] _Status308 = Encoding.ASCII.GetBytes($" 308 {FeaturesExtensions.GetReasonPhrase(308)}\r\n");
            private static byte[] _Status400 = Encoding.ASCII.GetBytes($" 400 {FeaturesExtensions.GetReasonPhrase(400)}\r\n");
            private static byte[] _Status401 = Encoding.ASCII.GetBytes($" 401 {FeaturesExtensions.GetReasonPhrase(401)}\r\n");
            private static byte[] _Status402 = Encoding.ASCII.GetBytes($" 402 {FeaturesExtensions.GetReasonPhrase(402)}\r\n");
            private static byte[] _Status403 = Encoding.ASCII.GetBytes($" 403 {FeaturesExtensions.GetReasonPhrase(403)}\r\n");
            private static byte[] _Status404 = Encoding.ASCII.GetBytes($" 404 {FeaturesExtensions.GetReasonPhrase(404)}\r\n");
            private static byte[] _Status405 = Encoding.ASCII.GetBytes($" 405 {FeaturesExtensions.GetReasonPhrase(405)}\r\n");
            private static byte[] _Status406 = Encoding.ASCII.GetBytes($" 406 {FeaturesExtensions.GetReasonPhrase(406)}\r\n");
            private static byte[] _Status407 = Encoding.ASCII.GetBytes($" 407 {FeaturesExtensions.GetReasonPhrase(407)}\r\n");
            private static byte[] _Status408 = Encoding.ASCII.GetBytes($" 408 {FeaturesExtensions.GetReasonPhrase(408)}\r\n");
            private static byte[] _Status409 = Encoding.ASCII.GetBytes($" 409 {FeaturesExtensions.GetReasonPhrase(409)}\r\n");
            private static byte[] _Status410 = Encoding.ASCII.GetBytes($" 410 {FeaturesExtensions.GetReasonPhrase(410)}\r\n");
            private static byte[] _Status411 = Encoding.ASCII.GetBytes($" 411 {FeaturesExtensions.GetReasonPhrase(411)}\r\n");
            private static byte[] _Status412 = Encoding.ASCII.GetBytes($" 412 {FeaturesExtensions.GetReasonPhrase(412)}\r\n");
            private static byte[] _Status413 = Encoding.ASCII.GetBytes($" 413 {FeaturesExtensions.GetReasonPhrase(413)}\r\n");
            private static byte[] _Status414 = Encoding.ASCII.GetBytes($" 414 {FeaturesExtensions.GetReasonPhrase(414)}\r\n");
            private static byte[] _Status415 = Encoding.ASCII.GetBytes($" 415 {FeaturesExtensions.GetReasonPhrase(415)}\r\n");
            private static byte[] _Status416 = Encoding.ASCII.GetBytes($" 416 {FeaturesExtensions.GetReasonPhrase(416)}\r\n");
            private static byte[] _Status417 = Encoding.ASCII.GetBytes($" 417 {FeaturesExtensions.GetReasonPhrase(417)}\r\n");
            private static byte[] _Status426 = Encoding.ASCII.GetBytes($" 426 {FeaturesExtensions.GetReasonPhrase(426)}\r\n");
            private static byte[] _Status500 = Encoding.ASCII.GetBytes($" 500 {FeaturesExtensions.GetReasonPhrase(500)}\r\n");
            private static byte[] _Status501 = Encoding.ASCII.GetBytes($" 501 {FeaturesExtensions.GetReasonPhrase(501)}\r\n");
            private static byte[] _Status502 = Encoding.ASCII.GetBytes($" 502 {FeaturesExtensions.GetReasonPhrase(502)}\r\n");
            private static byte[] _Status503 = Encoding.ASCII.GetBytes($" 503 {FeaturesExtensions.GetReasonPhrase(503)}\r\n");
            private static byte[] _Status504 = Encoding.ASCII.GetBytes($" 504 {FeaturesExtensions.GetReasonPhrase(504)}\r\n");
            private static byte[] _Status505 = Encoding.ASCII.GetBytes($" 505 {FeaturesExtensions.GetReasonPhrase(505)}\r\n");
            private static byte[] _Status510 = Encoding.ASCII.GetBytes($" 510 {FeaturesExtensions.GetReasonPhrase(510)}\r\n");
            #endregion
            public ResponseWriter(IConnection connection, HttpResponse response, HttpRequest request)
            {
                Debug.Assert(connection != null);
                Debug.Assert(response != null);
                Debug.Assert(request != null);
                _connection = connection;
                _response = response;
                _request = request;
            }

            private IConnection _connection;
            private HttpResponse _response;
            private HttpRequest _request;
            private bool _connectionClose;

            //write
            private bool _headOnly;
            private bool _transferChunked;
            private long _contentLength = -1;
            private int _available;
            private unsafe byte* _pWrite;
            private Memory<byte> _write;
            private MemoryHandle _writeHandle;
            private IDisposable _writeDisposable;
            private Queue<(Memory<byte>, IDisposable)> _writeQueue;
            private void TryWrite()
            {
                if (_available > 0)
                    return;

                if (_write.Length > 0)
                {
                    if (_writeQueue == null)
                        _writeQueue = new Queue<(Memory<byte>, IDisposable)>();

                    _writeQueue.Enqueue((_write, _writeDisposable));
                    _writeHandle.Dispose();
                }
                _write = ConnectionExtensions.GetBytes(out _writeDisposable);
                _available = _write.Length;
                _writeHandle = _write.Pin();
                unsafe { _pWrite = (byte*)_writeHandle.Pointer; }
            }
            private void Write(byte value)
            {
                if (_available == 0)
                    TryWrite();

                unsafe
                {
                    var pData = _pWrite + (_write.Length - _available);
                    *pData = value;
                    _available -= 1;
                }
            }
            private void Write(int value)
            {
                Span<char> chars = stackalloc char[11];
                value.TryFormat(chars, out var charsWritten);//TODO?? if
                Write(chars.Slice(0, charsWritten));
            }
            private void Write(long value)
            {
                Span<char> chars = stackalloc char[20];
                value.TryFormat(chars, out var charsWritten);//TODO?? if
                Write(chars.Slice(0, charsWritten));
            }
            private void Write(ReadOnlySpan<char> value)
            {
                if (value.IsEmpty)
                    return;
                unsafe
                {
                    fixed (char* pValue = value)
                    {
                        var tempCount = value.Length;
                        while (tempCount > 0)
                        {
                            TryWrite();
                            var bytesToCopy = tempCount < _available ? tempCount : _available;
                            var pData = pValue + (value.Length - tempCount);
                            var pTempBytes = _pWrite + (_write.Length - _available);
                            var tempBytesToCopy = bytesToCopy;

                            while (tempBytesToCopy > 4)
                            {
                                *(pTempBytes) = (byte)*(pData);
                                *(pTempBytes + 1) = (byte)*(pData + 1);
                                *(pTempBytes + 2) = (byte)*(pData + 2);
                                *(pTempBytes + 3) = (byte)*(pData + 3);
                                pTempBytes += 4;
                                pData += 4;
                                tempBytesToCopy -= 4;
                            }
                            while (tempBytesToCopy > 0)
                            {
                                *(pTempBytes) = (byte)*(pData);
                                pTempBytes += 1;
                                pData += 1;
                                tempBytesToCopy -= 1;
                            }

                            tempCount -= bytesToCopy;
                            _available -= bytesToCopy;
                        }
                    }
                }
            }
            private void Write(byte[] bytes)
            {
                Debug.Assert(bytes != null && bytes.Length > 0);
                var tempOffset = 0;
                var tempCount = bytes.Length;
                unsafe
                {
                    fixed (byte* pSrc = bytes)
                    {
                        while (tempCount > 0)
                        {
                            TryWrite();
                            var bytesToCopy = tempCount < _available ? tempCount : _available;
                            Buffer.MemoryCopy(pSrc + tempOffset, _pWrite + (_write.Length - _available), bytesToCopy, bytesToCopy);
                            tempOffset += bytesToCopy;
                            tempCount -= bytesToCopy;
                            _available -= bytesToCopy;
                        }
                    }
                }
            }
            private void WriteCrLf()
            {
                if (_available >= 2)
                {
                    unsafe
                    {
                        var pData = _pWrite + (_write.Length - _available);
                        *(short*)pData = 2573;
                        _available -= 2;
                    }
                }
                else
                {
                    Write("\r\n");
                }
            }
            private void WriteColonSpace()
            {
                if (_available >= 2)
                {
                    unsafe
                    {
                        var pData = _pWrite + (_write.Length - _available);
                        *(short*)pData = 8250;
                        _available -= 2;
                    }
                }
                else
                {
                    Write(": ");
                }
            }
            private void WriteSpaceStatusCrLf(int statusCode)
            {
                switch (statusCode)
                {
                    case 100:
                        Write(_Status100);
                        break;
                    case 101:
                        Write(_Status101);
                        break;
                    case 102:
                        Write(_Status102);
                        break;
                    case 200://Remove?
                        Write(_Status200);
                        break;
                    case 201:
                        Write(_Status201);
                        break;
                    case 202:
                        Write(_Status202);
                        break;
                    case 203:
                        Write(_Status203);
                        break;
                    case 204:
                        Write(_Status204);
                        break;
                    case 205:
                        Write(_Status205);
                        break;
                    case 206:
                        Write(_Status206);
                        break;
                    case 207:
                        Write(_Status207);
                        break;
                    case 226:
                        Write(_Status226);
                        break;
                    case 300:
                        Write(_Status300);
                        break;
                    case 301:
                        Write(_Status301);
                        break;
                    case 302:
                        Write(_Status302);
                        break;
                    case 303:
                        Write(_Status303);
                        break;
                    case 304:
                        Write(_Status304);
                        break;
                    case 305:
                        Write(_Status305);
                        break;
                    case 307:
                        Write(_Status307);
                        break;
                    case 308:
                        Write(_Status308);
                        break;
                    case 400:
                        Write(_Status400);
                        break;
                    case 401:
                        Write(_Status401);
                        break;
                    case 402:
                        Write(_Status402);
                        break;
                    case 403:
                        Write(_Status403);
                        break;
                    case 404:
                        Write(_Status404);
                        break;
                    case 405:
                        Write(_Status405);
                        break;
                    case 406:
                        Write(_Status406);
                        break;
                    case 407:
                        Write(_Status407);
                        break;
                    case 408:
                        Write(_Status408);
                        break;
                    case 409:
                        Write(_Status409);
                        break;
                    case 410:
                        Write(_Status410);
                        break;
                    case 411:
                        Write(_Status411);
                        break;
                    case 412:
                        Write(_Status412);
                        break;
                    case 413:
                        Write(_Status413);
                        break;
                    case 414:
                        Write(_Status414);
                        break;
                    case 415:
                        Write(_Status415);
                        break;
                    case 416:
                        Write(_Status416);
                        break;
                    case 417:
                        Write(_Status417);
                        break;
                    case 426:
                        Write(_Status426);
                        break;
                    case 500:
                        Write(_Status500);
                        break;
                    case 501:
                        Write(_Status501);
                        break;
                    case 502:
                        Write(_Status502);
                        break;
                    case 503:
                        Write(_Status503);
                        break;
                    case 504:
                        Write(_Status504);
                        break;
                    case 505:
                        Write(_Status505);
                        break;
                    case 510:
                        Write(_Status510);
                        break;
                    default:
                        Write((byte)' ');
                        Write(statusCode);
                        Write((byte)' ');
                        Write("No ReasonPhrase");
                        WriteCrLf();
                        break;
                }
            }
            private void WriteVersionStatusCrLf()
            {
                if (_response.Version == null)
                    _response.Version = HttpVersion.Version11;

                unsafe
                {
                    if (_response.Version == HttpVersion.Version11)
                    {
                        if (_response.ReasonPhrase == null)
                        {
                            if (_response.StatusCode == 200)
                            {
                                if (_available >= 17)
                                {
                                    var pData = _pWrite + (_write.Length - _available);
                                    *(long*)pData = 3543824036068086856;
                                    *(long*)(pData + 8) = 957946345412375072;
                                    *(pData + 16) = 10;
                                    _available -= 17;
                                }
                                else
                                {
                                    Write("HTTP/1.1 200 OK\r\n");
                                }
                            }
                            else
                            {
                                if (_available >= 8)
                                {
                                    var pData = _pWrite + (_write.Length - _available);
                                    *(long*)pData = 3543824036068086856;
                                    _available -= 8;
                                }
                                else
                                {
                                    Write("HTTP/1.1");
                                }
                                WriteSpaceStatusCrLf(_response.StatusCode);
                            }
                        }
                        else
                        {
                            if (_available >= 9)
                            {
                                var pData = _pWrite + (_write.Length - _available);
                                *(long*)pData = 3543824036068086856;
                                *(pData + 8) = (byte)' ';
                                _available -= 9;
                            }
                            else
                            {
                                Write("HTTP/1.1 ");
                            }
                            Write(_response.StatusCode);
                            Write((byte)' ');
                            Write(_response.ReasonPhrase);
                            WriteCrLf();
                        }
                    }
                    else if (_response.Version == HttpVersion.Version10)
                    {
                        if (_response.ReasonPhrase == null)
                        {
                            if (_response.StatusCode == 200)
                            {
                                if (_available >= 17)
                                {
                                    var pData = _pWrite + (_write.Length - _available);
                                    *(long*)pData = 3471766442030158920;
                                    *(long*)(pData + 8) = 957946345412375072;
                                    *(pData + 16) = 10;
                                    _available -= 17;
                                }
                                else
                                {
                                    Write("HTTP/1.0 200 OK\r\n");
                                }
                            }
                            else
                            {
                                if (_available >= 8)
                                {
                                    var pData = _pWrite + (_write.Length - _available);
                                    *(long*)pData = 3471766442030158920;
                                    _available -= 8;
                                }
                                else
                                {
                                    Write("HTTP/1.0");
                                }
                                WriteSpaceStatusCrLf(_response.StatusCode);
                            }
                        }
                        else
                        {
                            if (_available >= 9)
                            {
                                var pData = _pWrite + (_write.Length - _available);
                                *(long*)pData = 3471766442030158920;
                                *(pData + 8) = (byte)' ';
                                _available -= 9;
                            }
                            else
                            {
                                Write("HTTP/1.0 ");
                            }
                            Write(_response.StatusCode);
                            Write((byte)' ');
                            Write(_response.ReasonPhrase);
                            WriteCrLf();
                        }
                    }
                    else
                    {
                        throw new NotSupportedException($"{nameof(HttpVersion)}:{_response.Version}");
                    }
                }
            }
            private void WriteHeaders()
            {
                Debug.Assert(!_transferChunked);
                Debug.Assert(_contentLength == -1);
                if (_response.Headers.TryGetValue(HttpHeaders.TransferEncoding, out var transferEncoding))
                {
                    if (transferEncoding.EqualsIgnoreCaseWhiteSpace("chunked"))
                    {
                        _transferChunked = true;
                    }
                    else if (!transferEncoding.EqualsIgnoreCaseWhiteSpace("identity"))
                    {
                        throw new NotSupportedException($"{HttpHeaders.TransferEncoding}:{transferEncoding}");
                    }
                }
                if (_response.Headers.TryGetValue(HttpHeaders.ContentLength, out var contentLength))
                {
                    if (!long.TryParse(contentLength, out _contentLength) || _contentLength < 0)
                    {
                        throw new NotSupportedException($"{HttpHeaders.ContentLength}:{contentLength}");
                    }
                }
                //request
                {
                    if (_request.Headers.TryGetValue(HttpHeaders.Connection, out var connection))
                    {
                        _connectionClose = connection.EqualsIgnoreCase("close");
                    }
                    else
                    {
                        _connectionClose = _request.Version == HttpVersion.Version10;
                    }
                    _headOnly = _request.Method == HttpMethod.Head;
                }
                if (_transferChunked)
                {
                    if (_response.StatusCode == 204)//TODO
                    {
                        _headOnly = true;
                    }
                }
                else if (_contentLength != -1)
                {
                    if (_contentLength == 0 || _response.Content == null || _response.StatusCode == 204)//TODO
                    {
                        _headOnly = true;
                    }
                }
                else
                {
                    if (_response.Content == null || _response.StatusCode == 204)
                    {
                        _headOnly = true;
                        //?  Content-Length: 0\r\n\0
                        if (_available >= 20)
                        {
                            unsafe
                            {
                                var pData = _pWrite + (_write.Length - _available);
                                *(long*)pData = 3275364211029339971;
                                *(long*)(pData + 8) = 2322283407023695180;
                                *(int*)(pData + 16) = 658736;
                                _available -= 19;
                            }
                        }
                        else
                        {
                            Write("Content-Length: 0\r\n");
                        }
                    }
                    else
                    {
                        _contentLength = _response.Content.Length;
                        if (_contentLength == -1)
                        {
                            if (_response.Version == HttpVersion.Version10)
                            {
                                _contentLength = _response.Content.ComputeLength();
                                if (_contentLength == -1)
                                {
                                    _connectionClose = true;
                                }
                                else
                                {
                                    if (_available >= 16)
                                    {
                                        unsafe
                                        {
                                            var pData = _pWrite + (_write.Length - _available);
                                            *(long*)pData = 3275364211029339971;
                                            *(long*)(pData + 8) = 2322283407023695180;
                                            _available -= 16;
                                        }
                                    }
                                    else
                                    {
                                        Write("Content-Length: ");
                                    }
                                    Write(_contentLength);
                                    WriteCrLf();
                                }
                            }
                            else
                            {
                                if (_available >= 28)
                                {
                                    unsafe
                                    {
                                        var pData = _pWrite + (_write.Length - _available);
                                        *(long*)pData = 8243107338930713172;
                                        *(long*)(pData + 8) = 7956000646299010349;
                                        *(long*)(pData + 16) = 7741253900696566375;
                                        *(int*)(pData + 24) = 168649829;
                                        _available -= 28;
                                    }
                                }
                                else
                                {
                                    Write("Transfer-Encoding: chunked\r\n");
                                }
                                _transferChunked = true;
                            }
                        }
                        else
                        {
                            if (_available >= 16)
                            {
                                unsafe
                                {
                                    var pData = _pWrite + (_write.Length - _available);
                                    *(long*)pData = 3275364211029339971;
                                    *(long*)(pData + 8) = 2322283407023695180;
                                    _available -= 16;
                                }
                            }
                            else
                            {
                                Write("Content-Length: ");
                            }
                            Write(_contentLength);
                            WriteCrLf();
                        }
                    }
                }

                if (_connectionClose)
                {
                    if (_response.Headers.TryGetValue(HttpHeaders.Connection, out var connection))
                    {
                        if (!connection.EqualsIgnoreCase("close"))
                        {
                            _response.Headers[HttpHeaders.Connection] = "close";
                        }
                    }
                    else if (_response.Version == HttpVersion.Version11)
                    {
                        if (_available >= 20)
                        {
                            unsafe
                            {
                                var pData = _pWrite + (_write.Length - _available);
                                *(long*)pData = 7598807758576447299;
                                *(long*)(pData + 8) = 8317986209774857839;
                                *(int*)(pData + 16) = 658789;
                                _available -= 19;
                            }
                        }
                        else
                        {
                            Write("Connection: close\r\n");
                        }
                    }
                }
                else
                {
                    if (_response.Headers.TryGetValue(HttpHeaders.Connection, out var connection))
                    {
                        if (connection.EqualsIgnoreCase("close"))
                        {
                            _connectionClose = true;
                        }
                    }
                    else if (_response.Version == HttpVersion.Version10)
                    {
                        if (_available >= 24)
                        {
                            unsafe
                            {
                                var pData = _pWrite + (_write.Length - _available);
                                *(long*)pData = 7598807758576447299;
                                *(long*)(pData + 8) = 8098991015672311407;
                                *(long*)(pData + 16) = 724346674325774637;
                                _available -= 24;
                            }
                        }
                        else
                        {
                            Write("Connection: keep-alive\r\n");
                        }
                    }
                }

                for (int i = 0; i < _response.Headers.Count; i++)
                {
                    var header = _response.Headers[i];
                    Write(header.Key);
                    WriteColonSpace();
                    Write(header.Value);
                    WriteCrLf();
                }

                WriteCrLf();
            }
            private async Task SendAsync(int offset)
            {
                Debug.Assert(offset > 0);
                if (_writeQueue != null)
                {
                    while (_writeQueue.TryDequeue(out var write, out var disposable))
                    {
                        try
                        {
                            await _connection.SendAsync(write);
                        }
                        finally
                        {
                            disposable.Dispose();
                        }
                    }
                    _writeQueue = null;
                }
                await _connection.SendAsync(_write.Slice(0, offset));
            }
            public async Task WriteAsync()
            {
                TryWrite();
                try
                {
                    WriteVersionStatusCrLf();
                    WriteHeaders();
                    if (_headOnly)
                    {
                        await SendAsync(_write.Length - _available);
                        return;
                    }
                    if (_transferChunked)
                    {
                        #region Chunked
                        Debug.Assert(_write.Length > 17);
                        var contentOffset = _write.Length - _available;
                        if (_response.Content == null)//0\r\n\r\n
                        {
                            if (_available >= 5)
                            {
                                unsafe
                                {
                                    _pWrite[contentOffset] = (byte)'0';
                                    _pWrite[contentOffset + 1] = (byte)'\r';
                                    _pWrite[contentOffset + 2] = (byte)'\n';
                                    _pWrite[contentOffset + 3] = (byte)'\r';
                                    _pWrite[contentOffset + 4] = (byte)'\n';
                                }
                                await SendAsync(contentOffset + 5);
                                return;
                            }
                            else
                            {
                                await SendAsync(contentOffset);
                                await _connection.SendAsync(new[] { (byte)'0', (byte)'\r', (byte)'\n', (byte)'\r', (byte)'\n', });
                                return;
                            }
                        }
                        if (_available > 1024)
                        {
                            var tempOffset = contentOffset + 10;//偏移10个字节 Int32\r\n
                            var tempCount = _available - 12;//10 2 最后留两个字节\r\n
                            var result = await _response.Content.ReadAsync(_write.Slice(tempOffset, tempCount));
                            if (result == 0)
                            {
                                unsafe
                                {
                                    _pWrite[contentOffset] = (byte)'0';
                                    _pWrite[contentOffset + 1] = (byte)'\r';
                                    _pWrite[contentOffset + 2] = (byte)'\n';
                                    _pWrite[contentOffset + 3] = (byte)'\r';
                                    _pWrite[contentOffset + 4] = (byte)'\n';////0\r\n\r\n
                                }
                                await SendAsync(contentOffset + 5);
                                return;
                            }
                            tempOffset += result;
                            tempCount -= result;
                            if (tempCount >= 5 && _response.Content.Available == 0)
                            {
                                unsafe
                                {
                                    _pWrite[contentOffset] = _Hex[((result >> 0x1c) & 0x0f)];
                                    _pWrite[contentOffset + 1] = _Hex[((result >> 0x18) & 0x0f)];
                                    _pWrite[contentOffset + 2] = _Hex[((result >> 0x14) & 0x0f)];
                                    _pWrite[contentOffset + 3] = _Hex[((result >> 0x10) & 0x0f)];
                                    _pWrite[contentOffset + 4] = _Hex[((result >> 0x0c) & 0x0f)];
                                    _pWrite[contentOffset + 5] = _Hex[((result >> 0x08) & 0x0f)];
                                    _pWrite[contentOffset + 6] = _Hex[((result >> 0x04) & 0x0f)];
                                    _pWrite[contentOffset + 7] = _Hex[((result >> 0x00) & 0x0f)];
                                    _pWrite[contentOffset + 8] = (byte)'\r';
                                    _pWrite[contentOffset + 9] = (byte)'\n';
                                    _pWrite[tempOffset] = (byte)'\r';
                                    _pWrite[tempOffset + 1] = (byte)'\n';
                                    _pWrite[tempOffset + 2] = (byte)'0';
                                    _pWrite[tempOffset + 3] = (byte)'\r';
                                    _pWrite[tempOffset + 4] = (byte)'\n';
                                    _pWrite[tempOffset + 5] = (byte)'\r';
                                    _pWrite[tempOffset + 6] = (byte)'\n';////0\r\n\r\n
                                }
                                await SendAsync(tempOffset + 7);
                                return;
                            }
                            unsafe
                            {
                                _pWrite[contentOffset] = _Hex[((result >> 0x1c) & 0x0f)];
                                _pWrite[contentOffset + 1] = _Hex[((result >> 0x18) & 0x0f)];
                                _pWrite[contentOffset + 2] = _Hex[((result >> 0x14) & 0x0f)];
                                _pWrite[contentOffset + 3] = _Hex[((result >> 0x10) & 0x0f)];
                                _pWrite[contentOffset + 4] = _Hex[((result >> 0x0c) & 0x0f)];
                                _pWrite[contentOffset + 5] = _Hex[((result >> 0x08) & 0x0f)];
                                _pWrite[contentOffset + 6] = _Hex[((result >> 0x04) & 0x0f)];
                                _pWrite[contentOffset + 7] = _Hex[((result >> 0x00) & 0x0f)];
                                _pWrite[contentOffset + 8] = (byte)'\r';
                                _pWrite[contentOffset + 9] = (byte)'\n';
                                _pWrite[tempOffset] = (byte)'\r';
                                _pWrite[tempOffset + 1] = (byte)'\n';
                            }
                            await SendAsync(tempOffset + 2);
                        }
                        else
                        {
                            await SendAsync(contentOffset);//_write.Length - _available
                        }
                        var count = _write.Length - 12;//10 2 最后留两个字节\r\n
                        for (; ; )
                        {
                            var result = await _response.Content.ReadAsync(_write.Slice(10, count));
                            if (result == 0)
                            {
                                unsafe
                                {
                                    _pWrite[0] = (byte)'0';
                                    _pWrite[1] = (byte)'\r';
                                    _pWrite[2] = (byte)'\n';
                                    _pWrite[3] = (byte)'\r';
                                    _pWrite[4] = (byte)'\n';////0\r\n\r\n
                                }
                                await _connection.SendAsync(_write.Slice(0, 5));
                                return;
                            }
                            var tempOffset = result + 10;
                            var tempCount = count - result;
                            if (tempCount >= 5 && _response.Content.Available == 0)
                            {
                                unsafe
                                {
                                    _pWrite[0] = _Hex[((result >> 0x1c) & 0x0f)];
                                    _pWrite[1] = _Hex[((result >> 0x18) & 0x0f)];
                                    _pWrite[2] = _Hex[((result >> 0x14) & 0x0f)];
                                    _pWrite[3] = _Hex[((result >> 0x10) & 0x0f)];
                                    _pWrite[4] = _Hex[((result >> 0x0c) & 0x0f)];
                                    _pWrite[5] = _Hex[((result >> 0x08) & 0x0f)];
                                    _pWrite[6] = _Hex[((result >> 0x04) & 0x0f)];
                                    _pWrite[7] = _Hex[((result >> 0x00) & 0x0f)];
                                    _pWrite[8] = (byte)'\r';
                                    _pWrite[9] = (byte)'\n';
                                    _pWrite[tempOffset] = (byte)'\r';
                                    _pWrite[tempOffset + 1] = (byte)'\n';
                                    _pWrite[tempOffset + 2] = (byte)'0';
                                    _pWrite[tempOffset + 3] = (byte)'\r';
                                    _pWrite[tempOffset + 4] = (byte)'\n';
                                    _pWrite[tempOffset + 5] = (byte)'\r';
                                    _pWrite[tempOffset + 6] = (byte)'\n';////0\r\n\r\n
                                }
                                await _connection.SendAsync(_write.Slice(0, tempOffset + 7));
                                return;
                            }
                            unsafe
                            {
                                _pWrite[0] = _Hex[((result >> 0x1c) & 0x0f)];
                                _pWrite[1] = _Hex[((result >> 0x18) & 0x0f)];
                                _pWrite[2] = _Hex[((result >> 0x14) & 0x0f)];
                                _pWrite[3] = _Hex[((result >> 0x10) & 0x0f)];
                                _pWrite[4] = _Hex[((result >> 0x0c) & 0x0f)];
                                _pWrite[5] = _Hex[((result >> 0x08) & 0x0f)];
                                _pWrite[6] = _Hex[((result >> 0x04) & 0x0f)];
                                _pWrite[7] = _Hex[((result >> 0x00) & 0x0f)];
                                _pWrite[8] = (byte)'\r';
                                _pWrite[9] = (byte)'\n';
                                _pWrite[tempOffset] = (byte)'\r';
                                _pWrite[tempOffset + 1] = (byte)'\n';
                            }
                            await _connection.SendAsync(_write.Slice(0, tempOffset + 2));
                        }
                        throw new InvalidDataException("chunked");
                        #endregion
                    }
                    else if (_contentLength == -1)
                    {
                        #region HTTP/1.0
                        Debug.Assert(_connectionClose);
                        var tempOffset = _write.Length - _available;
                        if (_available > 1024)
                        {
                            var result = await _response.Content.ReadAsync(_write.Slice(tempOffset, _available));
                            if (result == 0)
                            {
                                await SendAsync(tempOffset);
                                return;
                            }
                            tempOffset += result;
                        }
                        await SendAsync(tempOffset);
                        for (; ; )
                        {
                            int result = await _response.Content.ReadAsync(_write);
                            if (result == 0)
                            {
                                return;
                            }
                            await _connection.SendAsync(_write.Slice(0, result));
                        }
                        #endregion
                    }
                    else
                    {
                        #region ContentLength
                        if (_contentLength <= 0)
                        {
                            await SendAsync(_write.Length - _available);
                            return;
                        }
                        var sum = 0;
                        var tempOffset = _write.Length - _available;
                        if (_contentLength <= _available || _available > 1024)
                        {
                            var result = await _response.Content.ReadAsync(_write.Slice(tempOffset, _available));
                            if (result == 0)
                                throw new ArgumentException(nameof(_response.Content));
                            tempOffset += result;
                            sum += result;
                        }
                        await SendAsync(tempOffset);
                        for (; ; )
                        {
                            var result = await _response.Content.ReadAsync(_write);
                            if (result == 0)
                            {
                                if (sum != _contentLength)
                                    throw new ArgumentException("content contentLength Not Match");
                                return;
                            }
                            sum += result;
                            if (sum > _contentLength)
                                throw new ArgumentException("content contentLength Not Match");

                            await _connection.SendAsync(_write.Slice(0, result));
                        }
                        throw new InvalidDataException("contentLength");
                        #endregion
                    }
                }
                finally
                {
                    #region Dispose
                    _transferChunked = false;
                    _contentLength = -1;
                    _write = Memory<byte>.Empty;
                    _writeHandle.Dispose();
                    unsafe { _pWrite = (byte*)0; }
                    _available = 0;
                    _writeDisposable.Dispose();
                    _writeDisposable = null;
                    if (_writeQueue != null)
                    {
                        while (_writeQueue.TryDequeue(out var _, out var disposable))
                        {
                            disposable.Dispose();
                        }
                        _writeQueue = null;
                    }
                    #endregion
                }
            }
        }
        #endregion


        //public static IHttpService UseHttp(this TcpServer @this, Action<HttpRequest, HttpResponse> handler)
        //{
        //    if (@this == null)
        //        throw new ArgumentNullException(nameof(@this));
        //    if (handler == null)
        //        throw new ArgumentNullException(nameof(handler));

        //    var service = new HttpService();
        //    service.Handler = HttpHandler.Create(handler);
        //    @this.Handler = service;
        //    return service;
        //}
        //public static IHttpService UseHttp(this TcpServer @this, Func<HttpRequest, HttpResponse, ValueTask> handler)
        //{
        //    if (@this == null)
        //        throw new ArgumentNullException(nameof(@this));
        //    if (handler == null)
        //        throw new ArgumentNullException(nameof(handler));

        //    var service = new HttpService();
        //    service.Handler = HttpHandler.Create(handler);
        //    @this.Handler = service;
        //    return service;
        //}
    }
}
