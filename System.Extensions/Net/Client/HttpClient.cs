
namespace System.Extensions.Http
{
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Buffers;
    using System.Diagnostics;
    using System.Net.Security;
    using System.Security.Authentication;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Extensions.Net;
    public abstract class HttpClient
    {
        #region static
        private static HttpClient _Default = Create(8, 8, 2, 16);
        public static HttpClient Default => _Default;
        #endregion
        public abstract Task<HttpResponse> SendAsync(HttpRequest request);
        public static HttpClient Create(int httpConenctions, int httpsConenctions, int http2Conenctions, int http2Streams)
        {
            if (httpConenctions <= 0)
                throw new ArgumentOutOfRangeException(nameof(httpConenctions));
            if (httpsConenctions <= 0)
                throw new ArgumentOutOfRangeException(nameof(httpsConenctions));
            if (http2Conenctions <= 0)
                throw new ArgumentOutOfRangeException(nameof(http2Conenctions));
            if (http2Streams <= 0)
                throw new ArgumentOutOfRangeException(nameof(http2Streams));

            return new CollectionHttpClient(httpConenctions, httpsConenctions, http2Conenctions, http2Streams);//SslClientAuthenticationOptions
        }
        public static HttpClient CreateHttp(string host, int port, int maxConenctions)
        {
            if (host == null)
                throw new ArgumentNullException(nameof(host));
            if (port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort)
                throw new ArgumentOutOfRangeException(nameof(port));
            if (maxConenctions <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxConenctions));

            return CreateHttp(() => ClientConnection.Create(host, port), maxConenctions)
                .Use((request, client) => {
                    var url = request.Url;
                    if (string.IsNullOrEmpty(url.Host))
                    {
                        request.Url.Scheme = Url.SchemeHttp;
                        request.Url.Host = host;
                        request.Url.Port = port == 80 ? null : (int?)port;
                    }
                    return client.SendAsync(request);
                });
        }
        public static HttpClient CreateHttp(Func<ClientConnection> factory, int maxConenctions)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));
            if (maxConenctions <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxConenctions));

            return new Http1Client(factory, maxConenctions, false);
        }
        public static HttpClient CreateHttps(string host, int port, int maxConenctions)
        {
            if (host == null)
                throw new ArgumentNullException(nameof(host));
            if (port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort)
                throw new ArgumentOutOfRangeException(nameof(port));
            if (maxConenctions <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxConenctions));

            return CreateHttp(() => ClientConnection.Create(host, port).UseSsl(
                new SslClientAuthenticationOptions
                {
                    EnabledSslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12 | SslProtocols.Tls13,
                    TargetHost = host
                }), maxConenctions)
               .Use((request, client) =>
               {
                   var url = request.Url;
                   if (string.IsNullOrEmpty(url.Host))
                   {
                       request.Url.Scheme = Url.SchemeHttps;
                       request.Url.Host = host;
                       request.Url.Port = port == 443 ? null : (int?)port;
                   }
                   return client.SendAsync(request);
               });
        }
        public static HttpClient CreateHttpProxy(string proxyHost, int proxyPort, int maxConenctions)
        {
            if (proxyHost == null)
                throw new ArgumentNullException(nameof(proxyHost));
            if (proxyPort < IPEndPoint.MinPort || proxyPort > IPEndPoint.MaxPort)
                throw new ArgumentOutOfRangeException(nameof(proxyPort));
            if (maxConenctions <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxConenctions));

            return CreateHttpProxy(() => ClientConnection.Create(proxyHost, proxyPort), maxConenctions)
                .Use((request, client) =>
                {
                    var url = request.Url;
                    if (string.IsNullOrEmpty(url.Host))
                        throw new ArgumentNullException(nameof(url.Host));

                    return client.SendAsync(request);
                });
        }
        public static HttpClient CreateHttpProxy(Func<ClientConnection> factory, int maxConenctions)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));
            if (maxConenctions <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxConenctions));

            return new Http1Client(factory, maxConenctions, true);
        }
        public static HttpClient CreateHttpsProxy(string proxyHost, int proxyPort, int maxConenctions, string host, int port)
        {
            if (proxyHost == null)
                throw new ArgumentNullException(nameof(proxyHost));
            if (proxyPort < IPEndPoint.MinPort || proxyPort > IPEndPoint.MaxPort)
                throw new ArgumentOutOfRangeException(nameof(proxyPort));
            if (maxConenctions <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxConenctions));
            if (host == null)
                throw new ArgumentNullException(nameof(host));
            if (port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort)
                throw new ArgumentOutOfRangeException(nameof(port));

            return CreateHttp(() =>
                new ProxyConnection(ClientConnection.Create(proxyHost, proxyPort), host, port)
                .UseSsl(
                new SslClientAuthenticationOptions
                {
                    EnabledSslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12 | SslProtocols.Tls13,
                    TargetHost = host
                }), maxConenctions)
                .Use((request, client) =>
                {
                    var url = request.Url;
                    if (string.IsNullOrEmpty(url.Host))
                    {
                        request.Url.Scheme = Url.SchemeHttps;
                        request.Url.Host = host;
                        request.Url.Port = port == 443 ? null : (int?)port;
                    }
                    return client.SendAsync(request);
                });
        }
        public static HttpClient CreateHttpsProxy(string proxyHost, int proxyPort, int maxConenctions)
        {
            if (proxyHost == null)
                throw new ArgumentNullException(nameof(proxyHost));
            if (proxyPort < IPEndPoint.MinPort || proxyPort > IPEndPoint.MaxPort)
                throw new ArgumentOutOfRangeException(nameof(proxyPort));
            if (maxConenctions <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxConenctions));

            return new ProxyClient(proxyHost, proxyPort, maxConenctions);
        }
        public static HttpClient CreateHttp2(string host, int port, int maxConenctions, int maxStreams)
        {
            if (host == null)
                throw new ArgumentNullException(nameof(host));
            if (port > IPEndPoint.MaxPort || port < IPEndPoint.MinPort)
                throw new ArgumentOutOfRangeException(nameof(port));
            if (maxConenctions <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxConenctions));
            if (maxStreams <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxStreams));

            return CreateHttp2(() =>
                new Http2Connection(ClientConnection.Create(host, port).UseSsl(
                    new SslClientAuthenticationOptions
                    {
                        ApplicationProtocols = new List<SslApplicationProtocol>() { SslApplicationProtocol.Http2 },
                        EnabledSslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12 | SslProtocols.Tls13,
                        TargetHost = host
                    }))
                , maxConenctions, maxStreams)
                .Use((request,client)=> {
                    var url = request.Url;
                    if (string.IsNullOrEmpty(url.Host))
                    {
                        request.Url.Scheme = Url.SchemeHttps;
                        request.Url.Host = host;
                        request.Url.Port = port == 443 ? null : (int?)port;
                    }
                    return client.SendAsync(request);
                });
        }
        public static HttpClient CreateHttp2(Func<ClientConnection> factory, int maxConenctions, int maxStreams)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));
            if (maxConenctions <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxConenctions));
            if (maxStreams <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxStreams));

            return new Http2Client(factory, maxConenctions, maxStreams);
        }
        #region private
        private class Http1Client : HttpClient
        {
            private bool _isProxy;//代理就发送绝对Url 不处理Host头 不是代理就发送相对Url 处理host头(判断有无host头)
            private Connection[] _connections;
            private Connection _idle;
            private Queue<RequestTask> _requestQueue;//lock (_requestQueue)
            public Http1Client(Func<ClientConnection> factory, int maxConenctions, bool isProxy)
            {
                Debug.Assert(factory != null);
                Debug.Assert(maxConenctions > 0);

                _connections = new Connection[maxConenctions];
                Connection temp = null;
                for (int i = 0; i < maxConenctions; i++)
                {
                    var connection = new Connection(this, factory);
                    _connections[i] = connection;
                    if (temp != null)
                        temp.Next = connection;
                    temp = connection;
                }
                _idle = _connections[0];
                _isProxy = isProxy;
                _requestQueue = new Queue<RequestTask>(64);
            }
            public class RequestTask : IDisposable
            {
                public const int WaitingToRun = 0;
                public const int Running = 1;
                public const int RanToCompletion = 2;
                public const int Faulted = 3;
                public int Status;//TODO?? CAS
                public ClientConnection Connection;
                public HttpRequest Request;
                public TaskCompletionSource<HttpResponse> ResponseTcs;
                public bool Run(ClientConnection connection)
                {
                    lock (this)
                    {
                        if (Status == WaitingToRun)
                        {
                            Status = Running;
                            Connection = connection;
                            return true;
                        }
                        return false;
                    }
                }
                public bool Complete()
                {
                    lock (this)
                    {
                        if (Status == Running)
                        {
                            Status = RanToCompletion;
                            return true;
                        }
                        return false;
                    }
                }
                public void Dispose()
                {
                    lock (this)
                    {
                        if (Status == WaitingToRun)
                        {
                            Status = Faulted;
                            ResponseTcs.SetException(new TaskCanceledException(ResponseTcs.Task));
                        }
                        else if (Status == Running)
                        {
                            Debug.Assert(Connection != null);
                            Status = Faulted;
                            if (ResponseTcs.TrySetException(new TaskCanceledException(ResponseTcs.Task)))
                            {
                                Connection.Close();
                            }
                        }
                    }
                }
            }
            public class Connection 
            {
                #region const
                private const int _MaxHeaderSize = 64 * 1024;//64K
                private const byte _SPByte = (byte)' ', _HTByte = (byte)'\t', _CRByte = (byte)'\r', _LFByte = (byte)'\n', _COLONByte = (byte)':';
                private const long _Version10Long = 3471766442030158920, _Version11Long = 3543824036068086856;
                private const long _OKLong = 724317918291046450;//200 OK\r\n
                private static byte[] _Hex = Encoding.ASCII.GetBytes("0123456789abcdef");//{ 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 97, 98, 99, 100, 101, 102 }
                private enum State { Cr, Lf, LfCr, Name, Colon, Value };
                #endregion

                public Connection Next;
                public Connection(Http1Client client, Func<ClientConnection> factory)
                {
                    _client = client;
                    _factory = factory;
                }

                private bool _active;
                private Http1Client _client;
                private Func<ClientConnection> _factory;
                private ClientConnection _connection;

                //task
                private Task<int> _keepAliveTask;
                private RequestTask _requestTask;
                private HttpRequest _request;
                private TaskCompletionSource<object> _requestTcs;
                private HttpResponse _response;
                private TaskCompletionSource<HttpResponse> _responseTcs;

                //headers
                private bool _connectionClose;
                private State _state;
                private int _headerName;
                private bool _headOnly;
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
                    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                        //不支持的是否抛出异常
                        Write(method.ToString());
                        Write(" ");
                    }
                }
                private void WriteRequestUri()
                {
                    var url = _request.Url;
                    if (_client._isProxy)
                    {
                        Write(url.Scheme);
                        Write(Url.SchemeDelimiter);
                        if (!string.IsNullOrEmpty(url.UserInfo))
                        {
                            Write(url.UserInfo);
                            Write((byte)'@');
                        }
                        Write(url.Host);
                        if (url.Port.HasValue)
                        {
                            Write((byte)':');
                            unsafe
                            {
                                Span<char> chars = stackalloc char[5];//0-65535
                                url.Port.Value.TryFormat(chars, out var charsWritten);
                                Write(chars.Slice(0, charsWritten));
                            }
                        }
                    }
                    //TODO (options *)?
                    Write(string.IsNullOrEmpty(url.Path) ? "/" : url.Path);
                    Write(url.Query);
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

                    if (!_request.Headers.Contains(HttpHeaders.Host))//代理是否区别对待?
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
                            _transferChunked = true;//强制使用chunked传输
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
                    if (_keepAliveTask == null || _keepAliveTask.IsCompleted)
                    {
                        await _connection.OpenAsync();
                        _keepAliveTask = _connection.ReceiveAsync(_read).AsTask();
                    }

                    if (_requestTask.Status == RequestTask.Faulted)
                        throw new TaskCanceledException(_requestTask.ResponseTcs.Task);

                    if (_writeQueue == null)
                    {
                        await _connection.SendAsync(_write.Slice(0, offset));
                        if (!_requestTask.Run(_connection))
                        {
                            _connectionClose = true;
                            throw new TaskCanceledException(_requestTask.ResponseTcs.Task);
                        }
                    }
                    else
                    {
                        _writeQueue.TryDequeue(out var write, out var disposable);
                        Debug.Assert(write.Length > 0);
                        try
                        {
                            await _connection.SendAsync(write);
                        }
                        finally
                        {
                            disposable.Dispose();
                        }
                        if (!_requestTask.Run(_connection))
                        {
                            _connectionClose = true;
                            throw new TaskCanceledException(_requestTask.ResponseTcs.Task);
                        }
                        while (_writeQueue.TryDequeue(out write, out disposable))
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
                        throw new InvalidDataException(nameof(_MaxHeaderSize));

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
                    //var text = statusCodeBytes.ToByteString();
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
                    Debug.Assert(_keepAliveTask != null);
                    _headerSize = await _keepAliveTask;
                    if (_headerSize == 0)
                        throw new InvalidOperationException(nameof(ClientConnection));
                    _start = 0;
                    _end = _headerSize;
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
                            var content = new ChunkedContent(this);
                            _response.RegisterForDispose(content);
                            _response.Content = content;
                            return;
                        }
                        else if (_response.Headers.TryGetValue(HttpHeaders.ContentLength, out var contentLengthValue))
                        {
                            if (!long.TryParse(contentLengthValue, out var contentLength) && contentLength < 0)
                                throw new FormatException(HttpHeaders.ContentLength);

                            if (contentLength > 0)
                            {
                                var content = new ContentLengthContent(contentLength, this);
                                _response.RegisterForDispose(content);
                                _response.Content = content;
                                return;
                            }
                        }
                        else if (_connectionClose && (_request.Version == HttpVersion.Version10 || _response.Version == HttpVersion.Version10))
                        {
                            //nginx[1.0发起的请求 1.1响应还有内容]

                            var content = new ConnectionCloseContent(this);
                            _response.RegisterForDispose(content);
                            _response.Content = content;
                            return;
                        }
                    }
                    _requestTask.Complete();
                    _requestTcs.TrySetResult(null);
                    #endregion
                }
                private async void SendAsync()
                {
                    for (; ; )
                    {
                        Debug.Assert(_requestTask != null);
                        if (_requestTask.Status == RequestTask.WaitingToRun)
                        {
                            _request = _requestTask.Request;
                            _responseTcs = _requestTask.ResponseTcs;
                            _requestTcs = new TaskCompletionSource<object>();
                            _headOnly = _request.Method == HttpMethod.Head;
                            try
                            {
                                if (_keepAliveTask == null && _connection.Connected) 
                                {
                                    _keepAliveTask = _connection.ReceiveAsync(_read).AsTask();
                                }
                                //TODO? parallel
                                await WriteAsync();
                                await ReadAsync();
                                Debug.Assert(_response != null);
                                FeaturesExtensions.RegisterForDispose(_request, _response);
                                if (!_responseTcs.TrySetResult(_response))
                                {
                                    _response.Dispose();
                                }
                            }
                            catch (Exception ex)
                            {
                                _requestTcs.TrySetResult(null);
                                if (_responseTcs.TrySetException(ex))
                                {
                                    _connection.Close();
                                }
                            }
                            await _requestTcs.Task;
                            if (_connectionClose)//|| !_connection.Connected
                            {
                                _connection.Close();
                            }
                            if (_keepAliveTask != null) 
                            {
                                try { await _keepAliveTask; } catch { }
                            }

                            //Reset
                            _requestTask = null;
                            _request = null;
                            _requestTcs = null;
                            _response = null;
                            _responseTcs = null;
                            _headerSize = 0;
                            _connectionClose = false;
                            _keepAliveTask = null;
                            if (_readQueue != null)
                            {
                                while (_readQueue.TryDequeue(out var _, out var disposable))
                                {
                                    disposable.Dispose();
                                }
                                _readQueue = null;
                            }
                        }
                        //Next
                        lock (_client._requestQueue)//Interlocked.CompareExchange(ref _client.Sync,)
                        {
                            if (_client._requestQueue.TryDequeue(out _requestTask))
                                continue;
                            _active = false;//not lock
                            if (_connection.Connected)
                            {
                                _keepAliveTask = Task.Run<int>(async () =>
                                {
                                    Debug.Assert(_start <= _end);
                                    var result = 0;
                                    try { result = await _connection.ReceiveAsync(_read); } catch { }
                                    lock (this)
                                    {
                                        if (!_active)
                                        {
                                            _read = Memory<byte>.Empty;
                                            _readHandle.Dispose();
                                            unsafe { _pRead = (byte*)0; }
                                            _readDisposable.Dispose();
                                            _readDisposable = null;
                                            _connection.Close();
                                            _keepAliveTask = null;
                                        }
                                    }
                                    return result;
                                });
                            }
                            else 
                            {
                                _read = Memory<byte>.Empty;
                                _readHandle.Dispose();
                                unsafe { _pRead = (byte*)0; }
                                _readDisposable.Dispose();
                                _readDisposable = null;
                            }
                            Next = _client._idle;
                            _client._idle = this;
                            break;
                        }
                    }
                }
                public void Active(RequestTask requestTask)
                {
                    Debug.Assert(requestTask != null);
                    lock (this)
                    {
                        Debug.Assert(!_active);
                        _active = true;
                    }

                    if (_connection == null)
                    {
                        _connection = _factory();
                        _factory = null;
                    }

                    _requestTask = requestTask;

                    if (_keepAliveTask == null) 
                    {
                        Debug.Assert(_read.Length == 0);
                        _read = ConnectionExtensions.GetBytes(out _readDisposable);
                        _readHandle = _read.Pin();
                        unsafe { _pRead = (byte*)_readHandle.Pointer; }
                    }

                    SendAsync();
                }
                private class ContentLengthContent : IHttpContent, IDisposable
                {
                    public ContentLengthContent(long length, Connection connection)
                    {
                        Debug.Assert(length > 0);
                        _position = 0;
                        _length = length;
                        _connection = connection;
                    }

                    private volatile int _status;//1= read 2= dispose
                    private long _position;
                    private long _length;
                    private Connection _connection;
                    public long Available => _length - _position;
                    public long Length => _length;
                    public bool Rewind() => false;
                    public long ComputeLength() => _length;
                    public int Read(Span<byte> buffer)
                    {
                        if (_status == 2)
                            throw new ObjectDisposedException(nameof(ContentLengthContent));

                        if (_length == _position)
                            return 0;

                        var count = buffer.Length;
                        if (count == 0)
                            return 0;

                        if (Interlocked.CompareExchange(ref _status, 1, 0) == 2)
                            throw new ObjectDisposedException(nameof(ContentLengthContent));
                        try
                        {
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
                        finally
                        {
                            if (_position == _length)
                            {
                                _connection._requestTask.Complete();
                                _connection._requestTcs.SetResult(null);
                            }
                            var status = Interlocked.CompareExchange(ref _status, 0, 1);
                            Debug.Assert(status == 1);
                        }
                    }
                    public int Read(byte[] buffer, int offset, int count)
                    {
                        return Read(buffer.AsSpan(offset, count));
                    }
                    public async ValueTask<int> ReadAsync(Memory<byte> buffer)
                    {
                        if (_status == 2)
                            throw new ObjectDisposedException(nameof(ContentLengthContent));

                        if (_length == _position)
                            return 0;

                        var count = buffer.Length;
                        if (count == 0)
                            return 0;

                        if (Interlocked.CompareExchange(ref _status, 1, 0) == 2)
                            throw new ObjectDisposedException(nameof(ContentLengthContent));
                        try
                        {
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
                        finally
                        {
                            if (_position == _length)
                            {
                                _connection._requestTask.Complete();
                                _connection._requestTcs.SetResult(null);
                            }
                            var status = Interlocked.CompareExchange(ref _status, 0, 1);
                            Debug.Assert(status == 1);
                        }
                    }
                    public ValueTask<int> ReadAsync(byte[] buffer, int offset, int count)
                    {
                        return ReadAsync(buffer.AsMemory(offset, count));
                    }
                    public void Dispose()
                    {
                        var status = Interlocked.CompareExchange(ref _status, 2, 0);
                        if (status == 2)
                            return;
                        if (status != 0)
                        {
                            _connection._connection.Close();
                            var spinWait = new SpinWait();
                            do
                            {
                                spinWait.SpinOnce();
                                status = Interlocked.CompareExchange(ref _status, 2, 0);
                            } while (status != 0);
                        }
                        Debug.Assert(status == 0);
                        if (_position != _length)
                        {
                            _connection._connectionClose = true;
                            _connection._requestTcs.TrySetResult(null);
                        }
                    }
                }
                private class ChunkedContent : IHttpContent, IDisposable
                {
                    private enum State { Size, Extension, SizeCr, Data, DataCr, Trailer, TrailerCr, TrailerCrLf, TrailerCrLfCr };
                    public ChunkedContent(Connection connection)
                    {
                        _state = State.Size;
                        _connection = connection;
                    }

                    private volatile int _status;//1= read 2= dispose
                    private State _state;
                    private int _chunkSize;//-1就是读完了
                    private int _trailer;//忽略掉
                    private Connection _connection;
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
                        if (_status == 2)
                            throw new ObjectDisposedException(nameof(ChunkedContent));

                        if (_chunkSize == -1)
                            return 0;

                        var count = buffer.Length;
                        if (count == 0)
                            return 0;

                        Debug.Assert(_chunkSize >= 0);
                        if (Interlocked.CompareExchange(ref _status, 1, 0) == 2)
                            throw new ObjectDisposedException(nameof(ChunkedContent));
                        try
                        {
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
                        finally
                        {
                            if (_chunkSize == -1)
                            {
                                _connection._requestTask.Complete();
                                _connection._requestTcs.SetResult(null);
                            }
                            var status = Interlocked.CompareExchange(ref _status, 0, 1);
                            Debug.Assert(status == 1);
                        }
                    }
                    public int Read(byte[] buffer, int offset, int count)
                    {
                        return Read(buffer.AsSpan(offset, count));
                    }
                    public async ValueTask<int> ReadAsync(Memory<byte> buffer)
                    {
                        if (_status == 2)
                            throw new ObjectDisposedException(nameof(ChunkedContent));

                        if (_chunkSize == -1)
                            return 0;

                        var count = buffer.Length;
                        if (count == 0)
                            return 0;

                        Debug.Assert(_chunkSize >= 0);
                        if (Interlocked.CompareExchange(ref _status, 1, 0) == 2)
                            throw new ObjectDisposedException(nameof(ChunkedContent));
                        try
                        {
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
                        finally
                        {
                            if (_chunkSize == -1)
                            {
                                _connection._requestTask.Complete();
                                _connection._requestTcs.SetResult(null);
                            }
                            var status = Interlocked.CompareExchange(ref _status, 0, 1);
                            Debug.Assert(status == 1);
                        }
                    }
                    public ValueTask<int> ReadAsync(byte[] buffer, int offset, int count)
                    {
                        return ReadAsync(buffer.AsMemory(offset, count));
                    }
                    public void Dispose()
                    {
                        var status = Interlocked.CompareExchange(ref _status, 2, 0);
                        if (status == 2)
                            return;
                        if (status != 0)
                        {
                            _connection._connection.Close();
                            var spinWait = new SpinWait();
                            do
                            {
                                Debug.WriteLine("SpinOnce");
                                spinWait.SpinOnce();
                                status = Interlocked.CompareExchange(ref _status, 2, 0);
                            } while (status != 0);
                        }
                        Debug.Assert(status == 0);
                        if (_chunkSize != -1)
                        {
                            _connection._connectionClose = true;
                            _connection._requestTcs.TrySetResult(null);
                        }
                    }
                }
                private class ConnectionCloseContent : IHttpContent, IDisposable
                {
                    public ConnectionCloseContent(Connection connection)
                    {
                        Debug.Assert(connection._connectionClose);

                        _connection = connection;
                        _connection._connectionClose = true;
                    }

                    private volatile int _status;//1= read 2= dispose
                    private bool _fin;
                    private Connection _connection;
                    public long Available => _fin ? 0 : -1;
                    public long Length => -1;
                    public bool Rewind() => false;
                    public long ComputeLength() => -1;
                    public int Read(Span<byte> buffer)
                    {
                        if (_status == 2)
                            throw new ObjectDisposedException(nameof(ConnectionCloseContent));

                        if (_fin)
                            return 0;

                        var count = buffer.Length;
                        if (count == 0)
                            return 0;

                        if (Interlocked.CompareExchange(ref _status, 1, 0) == 2)
                            throw new ObjectDisposedException(nameof(ConnectionCloseContent));
                        try
                        {
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
                                    _fin = true;
                                return result;
                            }
                        }
                        finally
                        {
                            var status = Interlocked.CompareExchange(ref _status, 0, 1);
                            Debug.Assert(status == 1);
                            if (_fin)
                            {
                                _connection._requestTask.Complete();
                                _connection._requestTcs.SetResult(null);
                            }
                        }
                    }
                    public int Read(byte[] buffer, int offset, int count)
                    {
                        return Read(buffer.AsSpan(offset, count));
                    }
                    public async ValueTask<int> ReadAsync(Memory<byte> buffer)
                    {
                        if (_status == 2)
                            throw new ObjectDisposedException(nameof(ConnectionCloseContent));

                        if (_fin)
                            return 0;

                        var count = buffer.Length;
                        if (count == 0)
                            return 0;

                        if (Interlocked.CompareExchange(ref _status, 1, 0) == 2)
                            throw new ObjectDisposedException(nameof(ConnectionCloseContent));
                        try
                        {
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
                                    _fin = true;
                                return result;
                            }
                        }
                        finally
                        {
                            if (_fin)
                            {
                                _connection._requestTask.Complete();
                                _connection._requestTcs.SetResult(null);
                            }
                            var status = Interlocked.CompareExchange(ref _status, 0, 1);
                            Debug.Assert(status == 1);
                        }
                    }
                    public ValueTask<int> ReadAsync(byte[] buffer, int offset, int count)
                    {
                        return ReadAsync(buffer.AsMemory(offset, count));
                    }
                    public void Dispose()
                    {
                        var status = Interlocked.CompareExchange(ref _status, 2, 0);
                        if (status == 2)
                            return;
                        if (status != 0)
                        {
                            _connection._connection.Close();
                            var spinWait = new SpinWait();
                            do
                            {
                                spinWait.SpinOnce();
                                status = Interlocked.CompareExchange(ref _status, 2, 0);
                            } while (status != 0);
                        }
                        Debug.Assert(status == 0);
                        if (!_fin)
                        {
                            _connection._requestTcs.SetResult(null);
                        }
                    }
                }
            }
            public override Task<HttpResponse> SendAsync(HttpRequest request)
            {
                if (request == null)
                    throw new ArgumentNullException(nameof(request));

                var requestTask = new RequestTask()
                {
                    Status = RequestTask.WaitingToRun,
                    Request = request,
                    ResponseTcs = new TaskCompletionSource<HttpResponse>(TaskCreationOptions.RunContinuationsAsynchronously)
                };
                request.RegisterForDispose(requestTask);

                Connection connection = null;
                lock (_requestQueue)
                {
                    if (_idle == null)//没有空闲连接
                    {
                        _requestQueue.Enqueue(requestTask);
                    }
                    else
                    {
                        Debug.Assert(_requestQueue.Count == 0);
                        connection = _idle;
                        _idle = connection.Next;
                        connection.Next = null;
                    }
                }

                if (connection != null)//拿到空闲连接
                {
                    connection.Active(requestTask);
                }

                return requestTask.ResponseTcs.Task;
            }
        }
        private class Http2Client : HttpClient
        {
            private Connection[] _connections;
            private Connection _active;
            private Connection _activing;
            private Queue<Connection> _idle;
            private Queue<RequestTask> _requestQueue;
            public Http2Client(Func<ClientConnection> factory, int maxConenctions, int maxStreams)
            {
                _connections = new Connection[maxConenctions];
                Connection temp = null;
                for (int i = 0; i < maxConenctions; i++)
                {
                    var connection = new Connection(this, factory, maxStreams);
                    _connections[i] = connection;
                    if (temp != null)
                        temp.Next = connection;
                    temp = connection;
                }
                _active = _connections[0];
                _idle = new Queue<Connection>(maxStreams * maxConenctions);
                _requestQueue = new Queue<RequestTask>(64);
            }
            public class RequestTask : IDisposable
            {
                public const int WaitingToRun = 0;
                public const int Running = 1;
                public const int RanToCompletion = 2;
                public const int Faulted = 3;
                public int Status;
                public HttpRequest Request;
                public TaskCompletionSource<HttpResponse> ResponseTcs;
                public bool Run()
                {
                    lock (this)
                    {
                        if (Status == WaitingToRun)
                        {
                            Status = Running;
                            return true;
                        }
                        return false;
                    }
                }
                public bool Complete()
                {
                    lock (this)
                    {
                        if (Status == Running)
                        {
                            Status = RanToCompletion;
                            return true;
                        }
                        return false;
                    }
                }
                public void Dispose()
                {
                    lock (this)
                    {
                        if (Status == WaitingToRun)
                        {
                            Status = Faulted;
                            ResponseTcs.SetException(new TaskCanceledException(ResponseTcs.Task));
                        }
                        else if (Status == Running)
                        {
                            Status = Faulted;
                            ResponseTcs.TrySetException(new TaskCanceledException(ResponseTcs.Task));
                            //TODO??? RstStream
                        }
                    }
                }
            }
            public class Connection
            {
                #region const
                private const int _MaxHeaders = 64 * 1024;//64K
                private const int _InitialWindowSize = 65535;
                private const int _ReceiveWindow = 2 * 1024 * 1024;
                private const int _WindowUpdate = 1 * 1024 * 1024;
                private const int _HeaderTableSize = 4096, _MaxHeaderTableSize = 65536;
                private const int _MaxStreamId = int.MaxValue;//客户端最大流ID(这个Id不用)
                private const int _MinFrameSize = 16384, _MaxFrameSize = 16777215;//https://tools.ietf.org/html/rfc7540#section-4.2
                private static byte[] _StartupBytes;//Preface Settings WindowUpdate
                private static (State state, int prefix)[] _Ready;
                private enum State : byte { Ready, Indexed /*1*/, Indexing/*01*/, WithoutIndexing/*0000*/, NeverIndexed/*0001*/ , SizeUpdate/*001*/ }
                private static (uint code, int bitLength)[] _EncodingTable = new (uint code, int bitLength)[]
                {
                    /*    (  0)  |11111111|11000                      */     ( 0x1ff8, 13),
                    /*    (  1)  |11111111|11111111|1011000           */     ( 0x7fffd8, 23),
                    /*    (  2)  |11111111|11111111|11111110|0010     */    ( 0xfffffe2, 28),
                    /*    (  3)  |11111111|11111111|11111110|0011     */    ( 0xfffffe3, 28),
                    /*    (  4)  |11111111|11111111|11111110|0100     */    ( 0xfffffe4, 28),
                    /*    (  5)  |11111111|11111111|11111110|0101     */    ( 0xfffffe5, 28),
                    /*    (  6)  |11111111|11111111|11111110|0110     */    ( 0xfffffe6, 28),
                    /*    (  7)  |11111111|11111111|11111110|0111     */    ( 0xfffffe7, 28),
                    /*    (  8)  |11111111|11111111|11111110|1000     */    ( 0xfffffe8, 28),
                    /*    (  9)  |11111111|11111111|11101010          */     ( 0xffffea, 24),
                    /*    ( 10)  |11111111|11111111|11111111|111100   */   ( 0x3ffffffc, 30),
                    /*    ( 11)  |11111111|11111111|11111110|1001     */    ( 0xfffffe9, 28),
                    /*    ( 12)  |11111111|11111111|11111110|1010     */    ( 0xfffffea, 28),
                    /*    ( 13)  |11111111|11111111|11111111|111101   */   ( 0x3ffffffd, 30),
                    /*    ( 14)  |11111111|11111111|11111110|1011     */    ( 0xfffffeb, 28),
                    /*    ( 15)  |11111111|11111111|11111110|1100     */    ( 0xfffffec, 28),
                    /*    ( 16)  |11111111|11111111|11111110|1101     */    ( 0xfffffed, 28),
                    /*    ( 17)  |11111111|11111111|11111110|1110     */    ( 0xfffffee, 28),
                    /*    ( 18)  |11111111|11111111|11111110|1111     */    ( 0xfffffef, 28),
                    /*    ( 19)  |11111111|11111111|11111111|0000     */    ( 0xffffff0, 28),
                    /*    ( 20)  |11111111|11111111|11111111|0001     */    ( 0xffffff1, 28),
                    /*    ( 21)  |11111111|11111111|11111111|0010     */    ( 0xffffff2, 28),
                    /*    ( 22)  |11111111|11111111|11111111|111110   */   ( 0x3ffffffe, 30),
                    /*    ( 23)  |11111111|11111111|11111111|0011     */    ( 0xffffff3, 28),
                    /*    ( 24)  |11111111|11111111|11111111|0100     */    ( 0xffffff4, 28),
                    /*    ( 25)  |11111111|11111111|11111111|0101     */    ( 0xffffff5, 28),
                    /*    ( 26)  |11111111|11111111|11111111|0110     */    ( 0xffffff6, 28),
                    /*    ( 27)  |11111111|11111111|11111111|0111     */    ( 0xffffff7, 28),
                    /*    ( 28)  |11111111|11111111|11111111|1000     */    ( 0xffffff8, 28),
                    /*    ( 29)  |11111111|11111111|11111111|1001     */    ( 0xffffff9, 28),
                    /*    ( 30)  |11111111|11111111|11111111|1010     */    ( 0xffffffa, 28),
                    /*    ( 31)  |11111111|11111111|11111111|1011     */    ( 0xffffffb, 28),
                    /*' ' ( 32)  |010100                              */         ( 0x14, 6),
                    /*'!' ( 33)  |11111110|00                         */        ( 0x3f8, 10),
                    /*'"' ( 34)  |11111110|01                         */        ( 0x3f9, 10),
                    /*'#' ( 35)  |11111111|1010                       */        ( 0xffa, 12),
                    /*'$' ( 36)  |11111111|11001                      */      ( 0x1ff9, 13),
                    /*'%' ( 37)  |010101                              */         ( 0x15, 6),
                    /*'&' ( 38)  |11111000                            */         ( 0xf8, 8),
                    /*''' ( 39)  |11111111|010                        */       ( 0x7fa, 11),
                    /*'(' ( 40)  |11111110|10                         */       ( 0x3fa, 10),
                    /*')' ( 41)  |11111110|11                         */       ( 0x3fb, 10),
                    /*'*' ( 42)  |11111001                            */         ( 0xf9, 8),
                    /*'+' ( 43)  |11111111|011                        */       ( 0x7fb, 11),
                    /*',' ( 44)  |11111010                            */         ( 0xfa, 8),
                    /*'-' ( 45)  |010110                              */         ( 0x16, 6),
                    /*'.' ( 46)  |010111                              */         ( 0x17, 6),
                    /*'/' ( 47)  |011000                              */         ( 0x18, 6),
                    /*'0' ( 48)  |00000                               */          ( 0x0, 5),
                    /*'1' ( 49)  |00001                               */          ( 0x1, 5),
                    /*'2' ( 50)  |00010                               */          ( 0x2, 5),
                    /*'3' ( 51)  |011001                              */         ( 0x19, 6),
                    /*'4' ( 52)  |011010                              */         ( 0x1a, 6),
                    /*'5' ( 53)  |011011                              */         ( 0x1b, 6),
                    /*'6' ( 54)  |011100                              */         ( 0x1c, 6),
                    /*'7' ( 55)  |011101                              */         ( 0x1d, 6),
                    /*'8' ( 56)  |011110                              */         ( 0x1e, 6),
                    /*'9' ( 57)  |011111                              */         ( 0x1f, 6),
                    /*':' ( 58)  |1011100                             */         ( 0x5c, 7),
                    /*';' ( 59)  |11111011                            */         ( 0xfb, 8),
                    /*'<' ( 60)  |11111111|1111100                    */      ( 0x7ffc, 15),
                    /*'=' ( 61)  |100000                              */         ( 0x20, 6),
                    /*'>' ( 62)  |11111111|1011                       */       ( 0xffb, 12),
                    /*'?' ( 63)  |11111111|00                         */       ( 0x3fc, 10),
                    /*'@' ( 64)  |11111111|11010                      */      ( 0x1ffa, 13),
                    /*'A' ( 65)  |100001                              */         ( 0x21, 6),
                    /*'B' ( 66)  |1011101                             */         ( 0x5d, 7),
                    /*'C' ( 67)  |1011110                             */         ( 0x5e, 7),
                    /*'D' ( 68)  |1011111                             */         ( 0x5f, 7),
                    /*'E' ( 69)  |1100000                             */         ( 0x60, 7),
                    /*'F' ( 70)  |1100001                             */         ( 0x61, 7),
                    /*'G' ( 71)  |1100010                             */         ( 0x62, 7),
                    /*'H' ( 72)  |1100011                             */         ( 0x63, 7),
                    /*'I' ( 73)  |1100100                             */         ( 0x64, 7),
                    /*'J' ( 74)  |1100101                             */         ( 0x65, 7),
                    /*'K' ( 75)  |1100110                             */         ( 0x66, 7),
                    /*'L' ( 76)  |1100111                             */         ( 0x67, 7),
                    /*'M' ( 77)  |1101000                             */         ( 0x68, 7),
                    /*'N' ( 78)  |1101001                             */         ( 0x69, 7),
                    /*'O' ( 79)  |1101010                             */         ( 0x6a, 7),
                    /*'P' ( 80)  |1101011                             */         ( 0x6b, 7),
                    /*'Q' ( 81)  |1101100                             */         ( 0x6c, 7),
                    /*'R' ( 82)  |1101101                             */         ( 0x6d, 7),
                    /*'S' ( 83)  |1101110                             */         ( 0x6e, 7),
                    /*'T' ( 84)  |1101111                             */         ( 0x6f, 7),
                    /*'U' ( 85)  |1110000                             */         ( 0x70, 7),
                    /*'V' ( 86)  |1110001                             */         ( 0x71, 7),
                    /*'W' ( 87)  |1110010                             */         ( 0x72, 7),
                    /*'X' ( 88)  |11111100                            */         ( 0xfc, 8),
                    /*'Y' ( 89)  |1110011                             */         ( 0x73, 7),
                    /*'Z' ( 90)  |11111101                            */         ( 0xfd, 8),
                    /*'[' ( 91)  |11111111|11011                      */      ( 0x1ffb, 13),
                    /*'\' ( 92)  |11111111|11111110|000               */     ( 0x7fff0, 19),
                    /*']' ( 93)  |11111111|11100                      */      ( 0x1ffc, 13),
                    /*'^' ( 94)  |11111111|111100                     */      ( 0x3ffc, 14),
                    /*'_' ( 95)  |100010                              */         ( 0x22, 6),
                    /*'`' ( 96)  |11111111|1111101                    */      ( 0x7ffd, 15),
                    /*'a' ( 97)  |00011                               */          ( 0x3, 5),
                    /*'b' ( 98)  |100011                              */         ( 0x23, 6),
                    /*'c' ( 99)  |00100                               */          ( 0x4, 5),
                    /*'d' (100)  |100100                              */         ( 0x24, 6),
                    /*'e' (101)  |00101                               */          ( 0x5, 5),
                    /*'f' (102)  |100101                              */         ( 0x25, 6),
                    /*'g' (103)  |100110                              */         ( 0x26, 6),
                    /*'h' (104)  |100111                              */         ( 0x27, 6),
                    /*'i' (105)  |00110                               */          ( 0x6, 5),
                    /*'j' (106)  |1110100                             */         ( 0x74, 7),
                    /*'k' (107)  |1110101                             */         ( 0x75, 7),
                    /*'l' (108)  |101000                              */         ( 0x28, 6),
                    /*'m' (109)  |101001                              */         ( 0x29, 6),
                    /*'n' (110)  |101010                              */         ( 0x2a, 6),
                    /*'o' (111)  |00111                               */          ( 0x7, 5),
                    /*'p' (112)  |101011                              */         ( 0x2b, 6),
                    /*'q' (113)  |1110110                             */         ( 0x76, 7),
                    /*'r' (114)  |101100                              */         ( 0x2c, 6),
                    /*'s' (115)  |01000                               */          ( 0x8, 5),
                    /*'t' (116)  |01001                               */          ( 0x9, 5),
                    /*'u' (117)  |101101                              */         ( 0x2d, 6),
                    /*'v' (118)  |1110111                             */         ( 0x77, 7),
                    /*'w' (119)  |1111000                             */         ( 0x78, 7),
                    /*'x' (120)  |1111001                             */         ( 0x79, 7),
                    /*'y' (121)  |1111010                             */         ( 0x7a, 7),
                    /*'z' (122)  |1111011                             */         ( 0x7b, 7),
                    /*'(' (123)  |11111111|1111110                    */       ( 0x7ffe, 15),
                    /*'|' (124)  |11111111|100                        */        ( 0x7fc, 11),
                    /*')' (125)  |11111111|111101                     */       ( 0x3ffd, 14),
                    /*'~' (126)  |11111111|11101                      */       ( 0x1ffd, 13),
                    /*    (127)  |11111111|11111111|11111111|1100     */    ( 0xffffffc, 28),
                    /*    (128)  |11111111|11111110|0110              */      ( 0xfffe6, 20),
                    /*    (129)  |11111111|11111111|010010            */     ( 0x3fffd2, 22),
                    /*    (130)  |11111111|11111110|0111              */      ( 0xfffe7, 20),
                    /*    (131)  |11111111|11111110|1000              */      ( 0xfffe8, 20),
                    /*    (132)  |11111111|11111111|010011            */     ( 0x3fffd3, 22),
                    /*    (133)  |11111111|11111111|010100            */     ( 0x3fffd4, 22),
                    /*    (134)  |11111111|11111111|010101            */     ( 0x3fffd5, 22),
                    /*    (135)  |11111111|11111111|1011001           */     ( 0x7fffd9, 23),
                    /*    (136)  |11111111|11111111|010110            */     ( 0x3fffd6, 22),
                    /*    (137)  |11111111|11111111|1011010           */     ( 0x7fffda, 23),
                    /*    (138)  |11111111|11111111|1011011           */     ( 0x7fffdb, 23),
                    /*    (139)  |11111111|11111111|1011100           */     ( 0x7fffdc, 23),
                    /*    (140)  |11111111|11111111|1011101           */     ( 0x7fffdd, 23),
                    /*    (141)  |11111111|11111111|1011110           */     ( 0x7fffde, 23),
                    /*    (142)  |11111111|11111111|11101011          */     ( 0xffffeb, 24),
                    /*    (143)  |11111111|11111111|1011111           */     ( 0x7fffdf, 23),
                    /*    (144)  |11111111|11111111|11101100          */     ( 0xffffec, 24),
                    /*    (145)  |11111111|11111111|11101101          */     ( 0xffffed, 24),
                    /*    (146)  |11111111|11111111|010111            */     ( 0x3fffd7, 22),
                    /*    (147)  |11111111|11111111|1100000           */     ( 0x7fffe0, 23),
                    /*    (148)  |11111111|11111111|11101110          */     ( 0xffffee, 24),
                    /*    (149)  |11111111|11111111|1100001           */     ( 0x7fffe1, 23),
                    /*    (150)  |11111111|11111111|1100010           */     ( 0x7fffe2, 23),
                    /*    (151)  |11111111|11111111|1100011           */     ( 0x7fffe3, 23),
                    /*    (152)  |11111111|11111111|1100100           */     ( 0x7fffe4, 23),
                    /*    (153)  |11111111|11111110|11100             */     ( 0x1fffdc, 21),
                    /*    (154)  |11111111|11111111|011000            */     ( 0x3fffd8, 22),
                    /*    (155)  |11111111|11111111|1100101           */     ( 0x7fffe5, 23),
                    /*    (156)  |11111111|11111111|011001            */     ( 0x3fffd9, 22),
                    /*    (157)  |11111111|11111111|1100110           */     ( 0x7fffe6, 23),
                    /*    (158)  |11111111|11111111|1100111           */     ( 0x7fffe7, 23),
                    /*    (159)  |11111111|11111111|11101111          */     ( 0xffffef, 24),
                    /*    (160)  |11111111|11111111|011010            */     ( 0x3fffda, 22),
                    /*    (161)  |11111111|11111110|11101             */     ( 0x1fffdd, 21),
                    /*    (162)  |11111111|11111110|1001              */      ( 0xfffe9, 20),
                    /*    (163)  |11111111|11111111|011011            */     ( 0x3fffdb, 22),
                    /*    (164)  |11111111|11111111|011100            */     ( 0x3fffdc, 22),
                    /*    (165)  |11111111|11111111|1101000           */     ( 0x7fffe8, 23),
                    /*    (166)  |11111111|11111111|1101001           */     ( 0x7fffe9, 23),
                    /*    (167)  |11111111|11111110|11110             */     ( 0x1fffde, 21),
                    /*    (168)  |11111111|11111111|1101010           */     ( 0x7fffea, 23),
                    /*    (169)  |11111111|11111111|011101            */     ( 0x3fffdd, 22),
                    /*    (170)  |11111111|11111111|011110            */     ( 0x3fffde, 22),
                    /*    (171)  |11111111|11111111|11110000          */     ( 0xfffff0, 24),
                    /*    (172)  |11111111|11111110|11111             */     ( 0x1fffdf, 21),
                    /*    (173)  |11111111|11111111|011111            */     ( 0x3fffdf, 22),
                    /*    (174)  |11111111|11111111|1101011           */     ( 0x7fffeb, 23),
                    /*    (175)  |11111111|11111111|1101100           */     ( 0x7fffec, 23),
                    /*    (176)  |11111111|11111111|00000             */     ( 0x1fffe0, 21),
                    /*    (177)  |11111111|11111111|00001             */     ( 0x1fffe1, 21),
                    /*    (178)  |11111111|11111111|100000            */     ( 0x3fffe0, 22),
                    /*    (179)  |11111111|11111111|00010             */     ( 0x1fffe2, 21),
                    /*    (180)  |11111111|11111111|1101101           */     ( 0x7fffed, 23),
                    /*    (181)  |11111111|11111111|100001            */     ( 0x3fffe1, 22),
                    /*    (182)  |11111111|11111111|1101110           */     ( 0x7fffee, 23),
                    /*    (183)  |11111111|11111111|1101111           */     ( 0x7fffef, 23),
                    /*    (184)  |11111111|11111110|1010              */      ( 0xfffea, 20),
                    /*    (185)  |11111111|11111111|100010            */     ( 0x3fffe2, 22),
                    /*    (186)  |11111111|11111111|100011            */     ( 0x3fffe3, 22),
                    /*    (187)  |11111111|11111111|100100            */     ( 0x3fffe4, 22),
                    /*    (188)  |11111111|11111111|1110000           */     ( 0x7ffff0, 23),
                    /*    (189)  |11111111|11111111|100101            */     ( 0x3fffe5, 22),
                    /*    (190)  |11111111|11111111|100110            */     ( 0x3fffe6, 22),
                    /*    (191)  |11111111|11111111|1110001           */     ( 0x7ffff1, 23),
                    /*    (192)  |11111111|11111111|11111000|00       */    ( 0x3ffffe0, 26),
                    /*    (193)  |11111111|11111111|11111000|01       */    ( 0x3ffffe1, 26),
                    /*    (194)  |11111111|11111110|1011              */      ( 0xfffeb, 20),
                    /*    (195)  |11111111|11111110|001               */      ( 0x7fff1, 19),
                    /*    (196)  |11111111|11111111|100111            */     ( 0x3fffe7, 22),
                    /*    (197)  |11111111|11111111|1110010           */     ( 0x7ffff2, 23),
                    /*    (198)  |11111111|11111111|101000            */     ( 0x3fffe8, 22),
                    /*    (199)  |11111111|11111111|11110110|0        */    ( 0x1ffffec, 25),
                    /*    (200)  |11111111|11111111|11111000|10       */    ( 0x3ffffe2, 26),
                    /*    (201)  |11111111|11111111|11111000|11       */    ( 0x3ffffe3, 26),
                    /*    (202)  |11111111|11111111|11111001|00       */    ( 0x3ffffe4, 26),
                    /*    (203)  |11111111|11111111|11111011|110      */    ( 0x7ffffde, 27),
                    /*    (204)  |11111111|11111111|11111011|111      */    ( 0x7ffffdf, 27),
                    /*    (205)  |11111111|11111111|11111001|01       */    ( 0x3ffffe5, 26),
                    /*    (206)  |11111111|11111111|11110001          */     ( 0xfffff1, 24),
                    /*    (207)  |11111111|11111111|11110110|1        */    ( 0x1ffffed, 25),
                    /*    (208)  |11111111|11111110|010               */      ( 0x7fff2, 19),
                    /*    (209)  |11111111|11111111|00011             */     ( 0x1fffe3, 21),
                    /*    (210)  |11111111|11111111|11111001|10       */    ( 0x3ffffe6, 26),
                    /*    (211)  |11111111|11111111|11111100|000      */    ( 0x7ffffe0, 27),
                    /*    (212)  |11111111|11111111|11111100|001      */    ( 0x7ffffe1, 27),
                    /*    (213)  |11111111|11111111|11111001|11       */    ( 0x3ffffe7, 26),
                    /*    (214)  |11111111|11111111|11111100|010      */    ( 0x7ffffe2, 27),
                    /*    (215)  |11111111|11111111|11110010          */     ( 0xfffff2, 24),
                    /*    (216)  |11111111|11111111|00100             */     ( 0x1fffe4, 21),
                    /*    (217)  |11111111|11111111|00101             */     ( 0x1fffe5, 21),
                    /*    (218)  |11111111|11111111|11111010|00       */    ( 0x3ffffe8, 26),
                    /*    (219)  |11111111|11111111|11111010|01       */    ( 0x3ffffe9, 26),
                    /*    (220)  |11111111|11111111|11111111|1101     */    ( 0xffffffd, 28),
                    /*    (221)  |11111111|11111111|11111100|011      */    ( 0x7ffffe3, 27),
                    /*    (222)  |11111111|11111111|11111100|100      */    ( 0x7ffffe4, 27),
                    /*    (223)  |11111111|11111111|11111100|101      */    ( 0x7ffffe5, 27),
                    /*    (224)  |11111111|11111110|1100              */      ( 0xfffec, 20),
                    /*    (225)  |11111111|11111111|11110011          */     ( 0xfffff3, 24),
                    /*    (226)  |11111111|11111110|1101              */      ( 0xfffed, 20),
                    /*    (227)  |11111111|11111111|00110             */     ( 0x1fffe6, 21),
                    /*    (228)  |11111111|11111111|101001            */     ( 0x3fffe9, 22),
                    /*    (229)  |11111111|11111111|00111             */     ( 0x1fffe7, 21),
                    /*    (230)  |11111111|11111111|01000             */     ( 0x1fffe8, 21),
                    /*    (231)  |11111111|11111111|1110011           */     ( 0x7ffff3, 23),
                    /*    (232)  |11111111|11111111|101010            */     ( 0x3fffea, 22),
                    /*    (233)  |11111111|11111111|101011            */     ( 0x3fffeb, 22),
                    /*    (234)  |11111111|11111111|11110111|0        */    ( 0x1ffffee, 25),
                    /*    (235)  |11111111|11111111|11110111|1        */    ( 0x1ffffef, 25),
                    /*    (236)  |11111111|11111111|11110100          */     ( 0xfffff4, 24),
                    /*    (237)  |11111111|11111111|11110101          */     ( 0xfffff5, 24),
                    /*    (238)  |11111111|11111111|11111010|10       */    ( 0x3ffffea, 26),
                    /*    (239)  |11111111|11111111|1110100           */     ( 0x7ffff4, 23),
                    /*    (240)  |11111111|11111111|11111010|11       */    ( 0x3ffffeb, 26),
                    /*    (241)  |11111111|11111111|11111100|110      */    ( 0x7ffffe6, 27),
                    /*    (242)  |11111111|11111111|11111011|00       */    ( 0x3ffffec, 26),
                    /*    (243)  |11111111|11111111|11111011|01       */    ( 0x3ffffed, 26),
                    /*    (244)  |11111111|11111111|11111100|111      */    ( 0x7ffffe7, 27),
                    /*    (245)  |11111111|11111111|11111101|000      */    ( 0x7ffffe8, 27),
                    /*    (246)  |11111111|11111111|11111101|001      */    ( 0x7ffffe9, 27),
                    /*    (247)  |11111111|11111111|11111101|010      */    ( 0x7ffffea, 27),
                    /*    (248)  |11111111|11111111|11111101|011      */    ( 0x7ffffeb, 27),
                    /*    (249)  |11111111|11111111|11111111|1110     */    ( 0xffffffe, 28),
                    /*    (250)  |11111111|11111111|11111101|100      */    ( 0x7ffffec, 27),
                    /*    (251)  |11111111|11111111|11111101|101      */    ( 0x7ffffed, 27),
                    /*    (252)  |11111111|11111111|11111101|110      */    ( 0x7ffffee, 27),
                    /*    (253)  |11111111|11111111|11111101|111      */    ( 0x7ffffef, 27),
                    /*    (254)  |11111111|11111111|11111110|000      */    ( 0x7fffff0, 27),
                    /*    (255)  |11111111|11111111|11111011|10       */    ( 0x3ffffee, 26),
                    /*EOS (256)  |11111111|11111111|11111111|111111   */   ( 0x3fffffff, 30)
                };
                //https://github.com/aspnet/AspNetCore/blob/master/src/Servers/Kestrel/Core/src/Internal/Http2/HPack/Huffman.cs
                private static (int codeLength, int[] codes)[] _DecodingTable = new[]
                {
                    (5, new[] { 48, 49, 50, 97, 99, 101, 105, 111, 115, 116 }),
                    (6, new[] { 32, 37, 45, 46, 47, 51, 52, 53, 54, 55, 56, 57, 61, 65, 95, 98, 100, 102, 103, 104, 108, 109, 110, 112, 114, 117 }),
                    (7, new[] { 58, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80, 81, 82, 83, 84, 85, 86, 87, 89, 106, 107, 113, 118, 119, 120, 121, 122 }),
                    (8, new[] { 38, 42, 44, 59, 88, 90 }),
                    (10, new[] { 33, 34, 40, 41, 63 }),
                    (11, new[] { 39, 43, 124 }),
                    (12, new[] { 35, 62 }),
                    (13, new[] { 0, 36, 64, 91, 93, 126 }),
                    (14, new[] { 94, 125 }),
                    (15, new[] { 60, 96, 123 }),
                    (19, new[] { 92, 195, 208 }),
                    (20, new[] { 128, 130, 131, 162, 184, 194, 224, 226 }),
                    (21, new[] { 153, 161, 167, 172, 176, 177, 179, 209, 216, 217, 227, 229, 230 }),
                    (22, new[] { 129, 132, 133, 134, 136, 146, 154, 156, 160, 163, 164, 169, 170, 173, 178, 181, 185, 186, 187, 189, 190, 196, 198, 228, 232, 233 }),
                    (23, new[] { 1, 135, 137, 138, 139, 140, 141, 143, 147, 149, 150, 151, 152, 155, 157, 158, 165, 166, 168, 174, 175, 180, 182, 183, 188, 191, 197, 231, 239 }),
                    (24, new[] { 9, 142, 144, 145, 148, 159, 171, 206, 215, 225, 236, 237 }),
                    (25, new[] { 199, 207, 234, 235 }),
                    (26, new[] { 192, 193, 200, 201, 202, 205, 210, 213, 218, 219, 238, 240, 242, 243, 255 }),
                    (27, new[] { 203, 204, 211, 212, 214, 221, 222, 223, 241, 244, 245, 246, 247, 248, 250, 251, 252, 253, 254 }),
                    (28, new[] { 2, 3, 4, 5, 6, 7, 8, 11, 12, 14, 15, 16, 17, 18, 19, 20, 21, 23, 24, 25, 26, 27, 28, 29, 30, 31, 127, 220, 249 }),
                    (30, new[] { 10, 13, 22, 256 })
                };
                #endregion

                public Connection Next;
                static Connection()
                {
                    _Ready = new (State, int)[256];
                    for (int i = 0; i < 256; i++)
                    {
                        if ((i & 0b1000_0000) == 0b1000_0000)
                            _Ready[i] = (State.Indexed, i & 0b0111_1111);
                        else if ((i & 0b1100_0000) == 0b0100_0000)
                            _Ready[i] = (State.Indexing, i & 0b0011_1111);
                        else if ((i & 0b1110_0000) == 0b0010_0000)
                            _Ready[i] = (State.SizeUpdate, i & 0b0001_1111);
                        else if ((i & 0b1111_0000) == 0b0001_0000)
                            _Ready[i] = (State.NeverIndexed, i & 0b0000_1111);
                        else if ((i & 0b1111_0000) == 0b0000_0000)
                            _Ready[i] = (State.WithoutIndexing, i & 0b0000_1111);
                        else
                            throw new InvalidOperationException("never");
                    }

                    #region StartupBytes
                    _StartupBytes = new byte[58];
                    //preface=24
                    Encoding.ASCII.GetBytes("PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n", _StartupBytes);
                    //settings =9
                    _StartupBytes[24] = 0;
                    _StartupBytes[25] = 0;
                    _StartupBytes[26] = 12;
                    _StartupBytes[27] = 0x4;
                    _StartupBytes[28] = 0;
                    _StartupBytes[29] = 0;
                    _StartupBytes[30] = 0;
                    _StartupBytes[31] = 0;
                    _StartupBytes[32] = 0;
                    //EnablePush=6
                    _StartupBytes[33] = 0;
                    _StartupBytes[34] = 0x2;
                    _StartupBytes[35] = 0;
                    _StartupBytes[36] = 0;
                    _StartupBytes[37] = 0;
                    _StartupBytes[38] = 0;
                    //InitialWindowSize=6
                    _StartupBytes[39] = 0;
                    _StartupBytes[40] = 0x4;
                    _StartupBytes[41] = (byte)((_ReceiveWindow & 0xFF000000) >> 24);
                    _StartupBytes[42] = (byte)((_ReceiveWindow & 0x00FF0000) >> 16);
                    _StartupBytes[43] = (byte)((_ReceiveWindow & 0x0000FF00) >> 8);
                    _StartupBytes[44] = (byte)(_ReceiveWindow & 0x000000FF);

                    //windowUpdate =9
                    _StartupBytes[45] = 0;
                    _StartupBytes[46] = 0;
                    _StartupBytes[47] = 4;
                    _StartupBytes[48] = 0x8;
                    _StartupBytes[49] = 0;
                    _StartupBytes[50] = 0;
                    _StartupBytes[51] = 0;
                    _StartupBytes[52] = 0;
                    _StartupBytes[53] = 0;
                    //increment=4
                    var increment = _ReceiveWindow - 65535;
                    _StartupBytes[54] = (byte)((increment & 0xFF000000) >> 24);
                    _StartupBytes[55] = (byte)((increment & 0x00FF0000) >> 16);
                    _StartupBytes[56] = (byte)((increment & 0x0000FF00) >> 8);
                    _StartupBytes[57] = (byte)(increment & 0x000000FF);
                    #endregion
                }
                public class Http2Stream : IDisposable
                {
                    public RequestTask RequestTask;
                    public int StreamId;
                    public bool Closed;
                    public HttpRequest Request;
                    public IHttpContent RequestBody;//volatile???
                    public HttpResponse Response;
                    public Http2Content ResponseBody;
                    public TaskCompletionSource<HttpResponse> ResponseTcs;
                    public void Dispose() 
                    {
                        RequestBody = null;
                    }
                }
                public class Http2Content : IHttpContent, IDisposable
                {
                    public Http2Content(Connection connection,Http2Stream stream)
                    {
                        _connectionId = connection._id;
                        _connection = connection;
                        _stream = stream;
                        _receiveQueue = new Queue<UnmanagedMemory<byte>>();

                        if (_stream.Response.Headers.TryGetValue("content-length", out var contentLength))
                        {
                            _length = long.Parse(contentLength);
                            if (_length < 0)
                                throw new InvalidDataException("content-length");
                        }
                    }
                    private int _connectionId;
                    private Connection _connection;
                    private Http2Stream _stream;
                    private int _windowUpdate;
                    private Exception _exception;
                    private long _totalReceive;
                    private long _position = 0;//if -1 end
                    private long _length = -1;
                    private int _available;
                    private UnmanagedMemory<byte> _receive;//TODO ConnectionExtensions.GetBytes
                    private Queue<UnmanagedMemory<byte>> _receiveQueue;
                    private Memory<byte> _read;
                    private TaskCompletionSource<int> _readWaiter;
                    private void WindowUpdate(int increment)
                    {
                        if (increment == 0)
                            return;

                        lock (_connection)
                        {
                            if (_connectionId != _connection._id)
                                return;

                            _windowUpdate += increment;
                            if (_windowUpdate > _WindowUpdate)
                            {
                                var windowUpdate = _windowUpdate;
                                _connection.Enqueue(() => {
                                    _connection.WriteFrame(4, 0x8, 0, _stream.StreamId);
                                    _connection.Write(windowUpdate);
                                });
                                _windowUpdate = 0;
                            }
                            _connection._windowUpdate += increment;
                            if (_connection._windowUpdate > _WindowUpdate)
                            {
                                var windowUpdate = _connection._windowUpdate;
                                _connection.Enqueue(() => {
                                    _connection.WriteFrame(4, 0x8, 0, 0);
                                    _connection.Write(windowUpdate);
                                });
                                _connection._windowUpdate = 0;
                                _connection._receiveWindow += windowUpdate;
                            }
                        }
                    }
                    public long Available => (_length == -1 && _position != -1) ? -1 : _length - _position;
                    public long Length => _length;
                    public long ComputeLength() => _length;
                    public bool Rewind() => false;
                    public int Read(Span<byte> buffer)
                    {
                        if (buffer.IsEmpty)
                            return 0;

                        var result = 0;
                        for (; ; )
                        {
                            var readWaiter = default(TaskCompletionSource<int>);
                            lock (_stream)
                            {
                                if (_exception != null)
                                    throw _exception;

                                if (_position == _length)
                                    return 0;

                                if (_available > 0)
                                {
                                    result = Math.Min(buffer.Length, _available);
                                    _receive.GetSpan().Slice(_receive.Length - _available, result).CopyTo(buffer);
                                    _available -= result;
                                    _position += result;
                                    if (_available == 0)
                                    {
                                        _receive.Dispose();
                                        if (_receiveQueue.TryDequeue(out _receive))//_receive = null;
                                        {
                                            _available = _receive.Length;
                                        }
                                    }
                                    break;
                                }
                                if (_stream.Closed && _length == -1)
                                {
                                    _position = -1;
                                    return 0;
                                }
                                readWaiter = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                                _readWaiter = readWaiter;
                            }
                            readWaiter.Task.Wait();
                        }
                        Debug.Assert(result > 0);
                        WindowUpdate(result);
                        return result;
                    }
                    public int Read(byte[] buffer, int offset, int count)
                    {
                        return ReadAsync(buffer.AsMemory(offset, count)).Result;
                    }
                    public async ValueTask<int> ReadAsync(Memory<byte> buffer)
                    {
                        if (buffer.IsEmpty)
                            return 0;

                        var readWaiter = default(TaskCompletionSource<int>);
                        var result = 0;
                        lock (_stream)
                        {
                            if (_exception != null)
                                throw _exception;

                            if (_position == _length)
                                return 0;

                            if (_available > 0)
                            {
                                result = Math.Min(buffer.Length, _available);
                                _receive.GetSpan().Slice(_receive.Length - _available, result).CopyTo(buffer.Span);
                                _available -= result;
                                _position += result;
                                if (_available == 0)
                                {
                                    _receive.Dispose();
                                    if (_receiveQueue.TryDequeue(out _receive))
                                    {
                                        _available = _receive.Length;
                                    }
                                }
                                goto windowUpdate;
                            }
                            if (_stream.Closed && _length == -1)
                            {
                                _position = -1;
                                return 0;
                            }
                            readWaiter = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                            _readWaiter = readWaiter;
                            _read = buffer;
                        }
                        result = await readWaiter.Task;
                    windowUpdate:
                        WindowUpdate(result);
                        return result;
                    }
                    public ValueTask<int> ReadAsync(byte[] buffer, int offset, int count)
                    {
                        return ReadAsync(buffer.AsMemory(offset, count));
                    }
                    public void OnData(ReadOnlySpan<byte> payload)
                    {
                        if (payload.Length == 0)
                            return;

                        lock (_connection) 
                        {
                            if (_stream.Closed) 
                            {
                                _connection._windowUpdate += payload.Length;
                                if (_connection._windowUpdate > _WindowUpdate)
                                {
                                    var windowUpdate = _connection._windowUpdate;
                                    _connection.Enqueue(() => {
                                        _connection.WriteFrame(4, 0x8, 0, 0);
                                        _connection.Write(windowUpdate);
                                    });
                                    _connection._windowUpdate = 0;
                                    _connection._receiveWindow += windowUpdate;
                                }
                                return;
                            }
                            Monitor.Enter(_stream);
                        }
                        try
                        {
                            Debug.Assert(Monitor.IsEntered(_stream));
                            _totalReceive += payload.Length;
                            if (_length != -1 && _totalReceive > _length)
                                throw new InvalidDataException("totalReceive Too Large");//连接异常(Dispose _receive)

                            if (_readWaiter != null)//_available == 0
                            {
                                Debug.Assert(_available == 0);
                                Debug.Assert(_receiveQueue.Count == 0);
                                if (_read.IsEmpty)
                                {
                                    _readWaiter.SetResult(0);
                                    _readWaiter = null;
                                }
                                else
                                {
                                    var toRead = Math.Min(payload.Length, _read.Length);
                                    payload.Slice(0, toRead).CopyTo(_read.Span);
                                    payload = payload.Slice(toRead);
                                    _position += toRead;
                                    _read = Memory<byte>.Empty;
                                    _readWaiter.SetResult(toRead);
                                    _readWaiter = null;
                                }
                                if (payload.Length > 0)
                                {
                                    _receive = new UnmanagedMemory<byte>(payload.Length);
                                    payload.CopyTo(_receive.GetSpan());
                                    _available = payload.Length;
                                }
                            }
                            else if (_available > 0)
                            {
                                var reveive = new UnmanagedMemory<byte>(payload.Length);
                                payload.CopyTo(reveive.GetSpan());
                                _receiveQueue.Enqueue(reveive);
                            }
                            else
                            {
                                _receive = new UnmanagedMemory<byte>(payload.Length);
                                payload.CopyTo(_receive.GetSpan());
                                _available = payload.Length;
                            }
                        }
                        finally
                        {
                            Monitor.Exit(_stream);
                        }
                    }
                    public void Close(Exception exception)
                    {
                        Debug.Assert(Monitor.IsEntered(_connection));
                        Debug.Assert(_stream.Closed);
                        //in lock(_connection)
                        lock (_stream)
                        {
                            if (exception == null)
                            {
                                if (_length != -1 && _length != _totalReceive)
                                {
                                    Debug.Assert(_length > _totalReceive);
                                    exception = new InvalidDataException("content-length!=totalReceive");//直接抛出异常?
                                }
                                else
                                {
                                    if (_readWaiter != null)
                                    {
                                        _readWaiter.TrySetResult(0);
                                        _readWaiter = null;
                                        _read = Memory<byte>.Empty;
                                    }
                                    return;
                                }
                            }
                            Debug.Assert(exception != null);
                            _exception = exception;
                            if (_readWaiter != null)
                            {
                                _readWaiter.SetException(exception);
                                _readWaiter = null;
                                _read = Memory<byte>.Empty;
                            }
                            else if (_receive != null)
                            {
                                _receive.Dispose();
                                _receive = null;
                                while (_receiveQueue.TryDequeue(out var receive))
                                {
                                    receive.Dispose();
                                }
                            }

                            var increment = (int)(_totalReceive - _position);
                            if (increment > 0)//所有未读的加到连接Window上
                            {
                                _connection._windowUpdate += increment;
                                if (_connection._windowUpdate >= _WindowUpdate)
                                {
                                    var windowUpdate = _connection._windowUpdate;
                                    _connection.Enqueue(() => {
                                        _connection.WriteFrame(4, 0x8, 0, 0);
                                        _connection.Write(windowUpdate);
                                    });
                                    _connection._windowUpdate = 0;
                                    _connection._receiveWindow += windowUpdate;
                                }
                            }
                        }
                    }
                    public void Dispose()
                    {
                        if (_exception != null)
                            return;

                        lock (_connection)
                        {
                            lock (_stream)
                            {
                                if (_exception != null)
                                    return;

                                _exception = new ObjectDisposedException(nameof(Http2Content));
                                if (_readWaiter != null)
                                {
                                    Debug.Assert(_receive == null);
                                    _readWaiter.TrySetException(_exception);
                                    _readWaiter = null;
                                    _read = Memory<byte>.Empty;
                                }
                                else if (_receive != null)
                                {
                                    _receive.Dispose();
                                    _receive = null;
                                    while (_receiveQueue.TryDequeue(out var receive))
                                    {
                                        receive.Dispose();
                                    }
                                }
                            }

                            if (_length != _position && _connectionId == _connection._id)
                            {
                                var increment = (int)(_totalReceive - _position);
                                if (increment > 0)
                                {
                                    _connection._windowUpdate += increment;
                                    if (_connection._windowUpdate >= _WindowUpdate)
                                    {
                                        var windowUpdate = _connection._windowUpdate;
                                        _connection.Enqueue(() => {
                                            _connection.WriteFrame(4, 0x8, 0, 0);
                                            _connection.Write(windowUpdate);
                                        });
                                        _connection._windowUpdate = 0;
                                        _connection._receiveWindow += windowUpdate;
                                    }
                                }
                            }
                            if (!_stream.Closed)
                            {
                                Debug.WriteLine("Send Rst_Stream");
                                _connection.Enqueue(() => {//Rst_Stream
                                    _connection.WriteFrame(4, 0x3, 0, _stream.StreamId);
                                    _connection.Write(0x0);
                                });
                                _stream.Closed = true;
                                _connection._streams.Remove(_stream.StreamId);
                                _connection.NextStream();
                            }
                        }
                    }
                }
                public Connection(Http2Client client, Func<ClientConnection> factory, int initialMaxStreams)
                {
                    _client = client;
                    _factory = factory;
                    _initialMaxStreams = initialMaxStreams;
                    _maxStreams = initialMaxStreams;
                    _nextStreamId = 1;//TODO?? 3
                    _streams = new Dictionary<int, Http2Stream>();

                    _sendQueue = new Queue<(Action, Http2Stream)>();
                    _dataQueue = new Queue<Http2Stream>();

                    //TODO? _writeQueue默认不为Null
                    //_writeQueue = new Queue<(Memory<byte>, IDisposable)>();//NotNull

                    _readTask = Task.CompletedTask;
                    _writeTask = Task.CompletedTask;

                    _maxFrameSize = _MinFrameSize;
                    _initialWindowSize = _InitialWindowSize;
                    _sendWindow = _initialWindowSize;
                    _receiveWindow = _ReceiveWindow;

                    _encoderTable = new EncoderTable(_HeaderTableSize);
                    _decoderTable = new DecoderTable(_HeaderTableSize);
                }

                private int _id;//TODO Remove, new Connection()
                private bool _active;
                private Http2Client _client;
                private Func<ClientConnection> _factory;
                private ClientConnection _connection;

                //Settings
                private int _initialMaxStreams;
                private int _maxStreams;
                private int _nextStreamId;
                private int _initialWindowSize;//64K
                private int _sendWindow;
                private int _receiveWindow;
                private int _windowUpdate;
                private int _maxFrameSize;

                private int _activeStreams;
                private Dictionary<int, Http2Stream> _streams;//流对象重复利用?
                private Queue<(Action, Http2Stream)> _sendQueue;//TODO PriorityQueue
                private Queue<Http2Stream> _dataQueue;//一个流的Data没发送完不可能发送另外的流的Data<_pendingStreams

                private EncoderTable _encoderTable;
                private DecoderTable _decoderTable;

                private Task _readTask;
                private Task _writeTask;
                private TaskCompletionSource<object> _writeWaiter;
                private TaskCompletionSource<object> _closeWaiter;

                //frame
                private int _frameLength;
                private byte _frameType;
                private byte _frameFlags;
                private int _frameStreamId;

                //read
                private int _start;
                private int _end;
                private int _position;
                private unsafe byte* _pRead;
                private Memory<byte> _read;
                private MemoryHandle _readHandle;
                private IDisposable _readDisposable;
                private Queue<(Memory<byte>, IDisposable)> _readQueue;
                private int _headers;
                private Queue<UnmanagedMemory<byte>> _headersQueue;

                //write
                private int _available;
                private unsafe byte* _pWrite;
                private Memory<byte> _write;
                private MemoryHandle _writeHandle;
                private IDisposable _writeDisposable;
                private Queue<(Memory<byte>, IDisposable)> _writeQueue;
                private void Close(int lastStreamId, Exception exception)
                {
                    lock (this)//_retryQueue
                    {
                        foreach ((var streamId, var stream) in _streams)
                        {
                            Debug.Assert(!stream.Closed);
                            if (streamId > lastStreamId)
                            {
                                stream.Closed = true;
                                _streams.Remove(streamId);
                                _activeStreams -= 1;
                                Debug.Assert(exception != null);
                                if (stream.ResponseBody == null)
                                    stream.ResponseTcs.TrySetException(exception);
                                else
                                    stream.ResponseBody.Close(exception);
                            }
                        }

                        if (_activeStreams == 0)
                        {
                            Debug.Assert(_streams.Count == 0);
                            _activeStreams = -1;
                            if (_writeWaiter != null)
                            {
                                _writeWaiter.SetException(new IOException("Close"));
                                _writeWaiter = null;
                            }
                            if (_closeWaiter != null)
                            {
                                _closeWaiter.SetResult(null);
                                return;
                            }
                            _closeWaiter = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
                            _closeWaiter.SetResult(null);
                        }
                        else
                        {
                            if (_closeWaiter != null)
                                return;
                            _closeWaiter = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
                        }
                    }
                    ThreadPool.QueueUserWorkItem(async (_) => {
                        await _closeWaiter.Task;//TODO Timeout?
                        Debug.Assert(_activeStreams == -1);
                        _id += 1;
                        try { _connection.Close(); } catch { }
                        try { await _writeTask; } catch { }
                        try { await _readTask; } catch { }
                        _readTask = Task.CompletedTask;
                        _writeTask = Task.CompletedTask;
                        _sendQueue.Clear();
                        _dataQueue.Clear();

                        _maxFrameSize = _MinFrameSize;
                        _initialWindowSize = _InitialWindowSize;
                        _sendWindow = _initialWindowSize;
                        _receiveWindow = _ReceiveWindow;

                        _windowUpdate = 0;
                        _maxStreams = _initialMaxStreams;
                        _nextStreamId = 1;

                        _decoderTable.Size = 0;
                        _decoderTable.MaxSize = _HeaderTableSize;

                        _activeStreams = 0;
                        _closeWaiter = null;

                        lock (_client)
                        {
                            var requestQueue = _client._requestQueue;
                            var idle = _client._idle;
                            lock (this)
                            {
                                if (_active)
                                {
                                    lock (requestQueue)
                                    {
                                        if (idle.Count > 0)
                                        {
                                            var connections = idle.ToArray();
                                            idle.Clear();
                                            foreach (var connection in connections)
                                            {
                                                if (connection != this)
                                                    idle.Enqueue(connection);
                                            }
                                        }
                                    }
                                    _active = false;
                                    Next = _client._active;
                                    _client._active = this;
                                    if (_client._activing == null) 
                                    {
                                        TryActive();
                                    }
                                }
                                else
                                {
                                    Debug.Assert(_client._activing == this);
                                    _client._activing = null;
                                    Next = _client._active;
                                    _client._active = this;
                                    TryActive();
                                }
                            }
                        }
                    });
                }
                private void Enqueue(Action writer)
                {
                    //in lock(this)
                    Debug.Assert(Monitor.IsEntered(this));
                    Debug.Assert(writer != null);
                    _sendQueue.Enqueue((writer, null));
                    if (_writeWaiter != null)
                    {
                        _writeWaiter.SetResult(null);
                        _writeWaiter = null;
                    }
                }
                private void Enqueue(RequestTask requestTask)
                {
                    Debug.Assert(Monitor.IsEntered(this));
                    Debug.Assert(_closeWaiter == null);
                    Debug.Assert(_nextStreamId < _MaxStreamId);
                    Debug.Assert(_activeStreams < _maxStreams);
                    var stream = new Http2Stream()
                    {
                        RequestTask = requestTask,
                        Request = requestTask.Request,
                        RequestBody = requestTask.Request.Content,
                        ResponseTcs = requestTask.ResponseTcs,
                        StreamId = _nextStreamId
                    };
                    stream.Request.RegisterForDispose(stream);
                    _nextStreamId += 2;
                    _activeStreams += 1;
                    _streams.Add(stream.StreamId, stream);
                    _sendQueue.Enqueue((null, stream));
                    if (_writeWaiter != null)
                    {
                        _writeWaiter.SetResult(null);
                        _writeWaiter = null;
                    }
                    if (_nextStreamId == _MaxStreamId)
                    {
                        Close(_MaxStreamId, null);
                    }
                }
                private void TryActive()
                {
                    Debug.Assert(Monitor.IsEntered(_client));
                    Debug.Assert(Monitor.IsEntered(this));
                    Debug.Assert(_client._active == this);
                    Debug.Assert(_client._activing == null);
                    var requestQueue = _client._requestQueue;
                    lock (requestQueue)
                    {
                        while (requestQueue.TryDequeue(out var requestTask))
                        {
                            if (requestTask.Run())
                            {
                                _client._activing = this;
                                _client._active = Next;
                                Active(requestTask);
                                break;
                            }
                        }
                    }
                }
                private void AddStreams(int streams)
                {
                    Debug.Assert(Monitor.IsEntered(this));
                    Debug.Assert(_closeWaiter == null);
                    var requestQueue = _client._requestQueue;
                    var idle = _client._idle;
                    lock (requestQueue)
                    {
                        for (; streams > 0; streams--)
                        {
                            if (_closeWaiter != null)
                                return;
                            if (_activeStreams >= _maxStreams)
                                return;
                            var @break = true;
                            while (requestQueue.TryDequeue(out var requestTask))
                            {
                                if (requestTask.Run())
                                {
                                    Enqueue(requestTask);
                                    @break = false;
                                    break;
                                }
                            }
                            if (@break)
                                break;
                        }
                        for (; streams > 0; streams--)
                        {
                            idle.Enqueue(this);
                        }

                    }
                }
                private void NextStream()
                {
                    Debug.Assert(Monitor.IsEntered(this));
                    Debug.Assert(_activeStreams > 0);
                    _activeStreams -= 1;
                    if (_closeWaiter == null)
                    {
                        if (_activeStreams < _maxStreams)
                        {
                            var requestQueue = _client._requestQueue;
                            lock (requestQueue)
                            {
                                while (requestQueue.TryDequeue(out var requestTask))
                                {
                                    if (requestTask.Run())
                                    {
                                        Enqueue(requestTask);
                                        return;
                                    }
                                }
                                Debug.Assert(requestQueue.Count == 0);
                                _client._idle.Enqueue(this);
                            }
                        }
                    }
                    else
                    {
                        Debug.Assert(_closeWaiter != null);
                        Debug.Assert(_activeStreams >= 0);
                        if (_activeStreams == 0)
                        {
                            Debug.Assert(_streams.Count == 0);
                            _activeStreams = -1;
                            if (_writeWaiter != null)
                            {
                                _writeWaiter.SetException(new IOException("Close"));
                                _writeWaiter = null;
                            }
                            _closeWaiter.SetResult(null);
                        }
                    }
                }
                private void TryWrite()
                {
                    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                    Debug.Assert(_available > _StartupBytes.Length + 9);
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
                private void Write(ReadOnlySpan<byte> value)
                {
                    if (value.IsEmpty)
                        return;

                    unsafe
                    {
                        fixed (byte* pValue = value)
                        {
                            var tempOffset = 0;
                            var tempCount = value.Length;
                            while (tempCount > 0)
                            {
                                TryWrite();
                                var bytesToCopy = tempCount < _available ? tempCount : _available;
                                Buffer.MemoryCopy(pValue + tempOffset, _pWrite + (_write.Length - _available), bytesToCopy, bytesToCopy);
                                tempOffset += bytesToCopy;
                                tempCount -= bytesToCopy;
                                _available -= bytesToCopy;
                            }
                        }
                    }
                }
                private void Write(int value)
                {
                    if (_available >= 4)
                    {
                        unsafe
                        {
                            var pData = _pWrite + (_write.Length - _available);
                            pData[0] = (byte)((value & 0xFF000000) >> 24);
                            pData[1] = (byte)((value & 0x00FF0000) >> 16);
                            pData[2] = (byte)((value & 0x0000FF00) >> 8);
                            pData[3] = (byte)(value & 0x000000FF);
                            _available -= 4;
                        }
                    }
                    else
                    {
                        Span<byte> pData = stackalloc byte[4];
                        pData[0] = (byte)((value & 0xFF000000) >> 24);
                        pData[1] = (byte)((value & 0x00FF0000) >> 16);
                        pData[2] = (byte)((value & 0x0000FF00) >> 8);
                        pData[3] = (byte)(value & 0x000000FF);
                        Write(pData);
                    }
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
                private void WriteFrame(int length, byte type, byte flags, int streamId)
                {
                    if (_available >= 9)
                    {
                        unsafe
                        {
                            var pData = _pWrite + (_write.Length - _available);
                            pData[0] = (byte)((length & 0x00FF0000) >> 16);
                            pData[1] = (byte)((length & 0x0000FF00) >> 8);
                            pData[2] = (byte)(length & 0x000000FF);
                            pData[3] = type;
                            pData[4] = flags;
                            pData[5] = (byte)((streamId & 0xFF000000) >> 24);
                            pData[6] = (byte)((streamId & 0x00FF0000) >> 16);
                            pData[7] = (byte)((streamId & 0x0000FF00) >> 8);
                            pData[8] = (byte)(streamId & 0x000000FF);
                            _available -= 9;
                        }
                    }
                    else
                    {
                        Span<byte> pData = stackalloc byte[9];
                        pData[0] = (byte)((length & 0x00FF0000) >> 16);
                        pData[1] = (byte)((length & 0x0000FF00) >> 8);
                        pData[2] = (byte)(length & 0x000000FF);
                        pData[3] = type;
                        pData[4] = flags;
                        pData[5] = (byte)((streamId & 0xFF000000) >> 24);
                        pData[6] = (byte)((streamId & 0x00FF0000) >> 16);
                        pData[7] = (byte)((streamId & 0x0000FF00) >> 8);
                        pData[8] = (byte)(streamId & 0x000000FF);
                        Write(pData);
                    }
                }
                private void WriteFrame(byte type, int streamId, out Span<byte> flags, out Span<byte> len1, out Span<byte> len2, out Span<byte> len3)
                {
                    //TODO? ref byte
                    if (_available >= 9)
                    {
                        var pData = _write.Slice(_write.Length - _available).Span;
                        len1 = pData.Slice(0, 1);
                        len2 = pData.Slice(1, 1);
                        len3 = pData.Slice(2, 1);
                        pData[3] = type;
                        flags = pData.Slice(4, 1);
                        pData[5] = (byte)((streamId & 0xFF000000) >> 24);
                        pData[6] = (byte)((streamId & 0x00FF0000) >> 16);
                        pData[7] = (byte)((streamId & 0x0000FF00) >> 8);
                        pData[8] = (byte)(streamId & 0x000000FF);
                        _available -= 9;
                    }
                    else
                    {
                        TryWrite();
                        len1 = _write.Span.Slice(_write.Length - _available, 1);
                        _available += 1;
                        TryWrite();
                        len2 = _write.Span.Slice(_write.Length - _available, 1);
                        _available += 1;
                        TryWrite();
                        len3 = _write.Span.Slice(_write.Length - _available, 1);
                        _available += 1;
                        TryWrite();
                        unsafe
                        {
                            _pWrite[_write.Length - _available] = type;
                            _available += 1;
                        }
                        TryWrite();
                        flags = _write.Span.Slice(_write.Length - _available, 1);
                        _available += 1;
                        Write(streamId);
                    }
                }
                private void WriteHpack(byte prefix, int prefixBits, int value)
                {
                    Debug.Assert(prefixBits >= 0 && prefixBits <= 8);

                    prefixBits = 0xFF >> (8 - prefixBits);
                    if (value < prefixBits)
                    {
                        Write((byte)(prefix | value));
                    }
                    else
                    {
                        Write((byte)(prefix | prefixBits));
                        value = value - prefixBits;
                        for (; ; )
                        {
                            if ((value & ~0x7F) == 0)
                            {
                                Write((byte)value);
                                return;
                            }
                            else
                            {
                                Write((byte)((value & 0x7F) | 0x80));
                                value >>= 7;
                            }
                        }
                    }
                }
                private void WriteHuffman(string value)
                {
                    if (string.IsNullOrEmpty(value))
                    {
                        WriteHpack(0b10000000, 7, 0);
                        return;
                    }
                    //huffmanLength
                    {
                        var count = 0L;
                        var i = 0;
                        while (i < value.Length)
                        {
                            count += _EncodingTable[value[i++]].bitLength;
                        }
                        WriteHpack(0b10000000, 7, (int)((count + 7) >> 3));
                    }
                    //huffmanBytes
                    {
                        ulong b = 0;//buffer
                        var bits = 0;//bit数
                        var i = 0;
                        while (i < value.Length)
                        {
                            char ch = value[i++];
                            if (ch >= 128 || ch < 0)//256?
                                throw new InvalidDataException("Huffman");

                            var (code, bitLength) = _EncodingTable[ch];
                            b <<= bitLength;
                            b |= code;
                            bits += bitLength;
                            while (bits >= 8)
                            {
                                bits -= 8;
                                Write((byte)(b >> bits));
                            }
                        }
                        if (bits > 0)
                        {
                            b <<= (8 - bits);
                            b |= (uint)(0xFF >> bits);
                            Write((byte)b);
                        }
                    }
                }
                private void WriteHeaders(Http2Stream stream)
                {
                    Debug.Assert(_writeQueue == null);
                    //>_StartupBytes+9
                    var offset = (_write.Length - _available + 9);
                    WriteFrame(0x1, stream.StreamId, out var flags, out var len1, out var len2, out var len3);

                    var request = stream.Request;

                    #region :method
                    if (request.Method == null)
                        request.Method = request.Content == null ? HttpMethod.Get : HttpMethod.Post;
                    var method = request.Method;
                    if (method == HttpMethod.Get)
                    {
                        WriteHpack(0b1000_0000, 7, 2);
                    }
                    else if (method == HttpMethod.Post)
                    {
                        WriteHpack(0b1000_0000, 7, 3);
                    }
                    else
                    {
                        var methodString = method.ToString();
                        WriteHpack(0b0000_0000, 4, 2);//不索引
                        WriteHpack(0b0000_0000, 7, methodString.Length);
                        Write(methodString);
                    }
                    #endregion

                    #region :scheme
                    var scheme = request.Url.Scheme;
                    if (scheme.EqualsIgnoreCase(Url.SchemeHttps))
                    {
                        WriteHpack(0b1000_0000, 7, 7);
                    }
                    else if (scheme.EqualsIgnoreCase(Url.SchemeHttp))
                    {
                        WriteHpack(0b1000_0000, 7, 6);
                    }
                    else
                    {
                        WriteHpack(0b0000_0000, 4, 6);
                        WriteHpack(0b0000_0000, 7, scheme.Length);
                        Write(scheme);
                    }
                    #endregion

                    #region :path
                    var path = request.Url.Path;
                    if (string.IsNullOrEmpty(path) || path.Length == 1)
                    {
                        WriteHpack(0b10000000, 7, 4);
                    }
                    else
                    {
                        WriteHpack(0b00000000, 4, 4);
                        WriteHpack(0b00000000, 7, path.Length);
                        Write(path);
                    }
                    #endregion

                    var headers = request.Headers;
                    if (headers.Contains(HttpHeaders.Expect))
                        throw new NotSupportedException(HttpHeaders.Expect);

                    if (headers.TryGetValue(HttpHeaders.TransferEncoding, out var transferEncoding) && !transferEncoding.EqualsIgnoreCase("identity"))
                        throw new NotSupportedException($"{HttpHeaders.TransferEncoding}:{transferEncoding}");

                    #region :authority
                    if (headers.TryGetValue(HttpHeaders.Host, out var host))
                    {
                        WriteHpack(0b0000_0000, 4, 1);
                        WriteHpack(0b0000_0000, 7, host.Length);
                        Write(host);
                    }
                    else
                    {
                        host = request.Url.Host;
                        var port = request.Url.Port;
                        if (port.HasValue)
                        {
                            Span<char> span = stackalloc char[11];
                            port.Value.TryFormat(span, out var charsWritten);
                            span = span.Slice(0, charsWritten);
                            WriteHpack(0b0000_0000, 4, 1);
                            WriteHpack(0b0000_0000, 7, host.Length + 1 + span.Length);
                            Write(host);
                            Write((byte)':');
                            Write(span);
                        }
                        else
                        {
                            WriteHpack(0b0000_0000, 4, 1);
                            WriteHpack(0b0000_0000, 7, host.Length);
                            Write(host);
                        }
                    }
                    #endregion

                    if (request.Content != null && !headers.Contains(HttpHeaders.ContentLength))//TODO?
                    {
                        var contentLength = request.Content.ComputeLength();
                        if (contentLength != -1)
                        {
                            Span<char> span = stackalloc char[20];
                            contentLength.TryFormat(span, out var charsWritten);
                            span = span.Slice(0, charsWritten);
                            WriteHpack(0b0000_0000, 4, 28);
                            WriteHpack(0b0000_0000, 7, span.Length);
                            Write(span);
                        }
                    }

                    for (int i = 0; i < headers.Count; i++)//转换成小写
                    {
                        var header = headers[i];
                        if (_encoderTable.TryGetIndex(header.Key, out var index))
                        {
                            if (index > 0)
                            {
                                //Raw
                                //WriteHpack(0b00000000, 4, index);
                                //WriteHpack(0b00000000, 7, header.Value.Length);
                                //Write(header.Value);

                                //Huffman
                                WriteHpack(0b0000_0000, 4, index);
                                WriteHuffman(header.Value);
                            }
                        }
                        else
                        {
                            WriteHpack(0b0000_0000, 4, 0);
                            WriteHpack(0b0000_0000, 7, header.Key.Length);
                            //LowerCase
                            unsafe
                            {
                                fixed (char* pValue = header.Key)
                                {
                                    var tempCount = header.Key.Length;
                                    while (tempCount > 0)
                                    {
                                        TryWrite();
                                        var bytesToCopy = tempCount < _available ? tempCount : _available;
                                        var pData = pValue + (header.Key.Length - tempCount);
                                        var pTempBytes = _pWrite + (_write.Length - _available);
                                        var tempBytesToCopy = bytesToCopy;
                                        while (tempBytesToCopy > 0)
                                        {
                                            *pTempBytes = (*pData >= 'A' && *pData <= 'Z') ? (byte)(*pData + 32) : (byte)*pData;
                                            pTempBytes += 1;
                                            pData += 1;
                                            tempBytesToCopy -= 1;
                                        }
                                        tempCount -= bytesToCopy;
                                        _available -= bytesToCopy;
                                    }
                                }
                            }
                            WriteHuffman(header.Value);
                            //WriteHpack(0b00000000, 7, header.Value.Length);
                            //Write(header.Value);
                        }
                    }

                    var frameSize = _maxFrameSize;
                    var length = _write.Length - _available;
                    if (_writeQueue != null)
                    {
                        foreach ((var write, var _) in _writeQueue)
                        {
                            length += write.Length;
                        }
                    }
                    length -= offset;
                    if (length <= frameSize)
                    {
                        len1[0] = (byte)((length & 0x00FF0000) >> 16);
                        len2[0] = (byte)((length & 0x0000FF00) >> 8);
                        len3[0] = (byte)(length & 0x000000FF);
                        if (request.Content == null)
                            flags[0] = 0b0000_0100 | 0b0000_0001;//EndHeaders EndStream
                        else
                            flags[0] = 0b0000_0100;//EndHeaders
                    }
                    else
                    {
                        var headersFlag = true;//当前帧是Headers还是Continuation
                        var frameOffset = -offset;
                        var writeQueue = new Queue<(Memory<byte>, IDisposable)>();
                        if (_writeQueue != null)
                        {
                            foreach ((var write, var disposable) in _writeQueue)
                            {
                                var temp = write.Length;
                                for (; ; )
                                {
                                    var toFrame = frameSize - frameOffset;
                                    if (temp >= toFrame)
                                    {
                                        if (headersFlag)
                                        {
                                            len1[0] = (byte)((frameSize & 0x00FF0000) >> 16);
                                            len2[0] = (byte)((frameSize & 0x0000FF00) >> 8);
                                            len3[0] = (byte)(frameSize & 0x000000FF);
                                            if (request.Content == null)
                                                flags[0] = 0b00000001;//EndStream
                                            else
                                                flags[0] = 0b00000000;
                                            headersFlag = false;
                                        }
                                        else
                                        {
                                            len1[0] = (byte)((frameSize & 0x00FF0000) >> 16);
                                            len2[0] = (byte)((frameSize & 0x0000FF00) >> 8);
                                            len3[0] = (byte)(frameSize & 0x000000FF);
                                            flags[0] = 0b00000000;
                                        }
                                        var frameHeader = new byte[9];//Continuation TODO? use Copy
                                        len1 = frameHeader.AsSpan(0, 1);
                                        len2 = frameHeader.AsSpan(1, 1);
                                        len3 = frameHeader.AsSpan(2, 1);
                                        flags = frameHeader.AsSpan(4, 1);
                                        frameHeader[3] = 0x9;
                                        frameHeader[5] = (byte)((stream.StreamId & 0xFF000000) >> 24);
                                        frameHeader[6] = (byte)((stream.StreamId & 0x00FF0000) >> 16);
                                        frameHeader[7] = (byte)((stream.StreamId & 0x0000FF00) >> 8);
                                        frameHeader[8] = (byte)(stream.StreamId & 0x000000FF);
                                        frameOffset = 0;
                                        if (temp == toFrame)
                                        {
                                            writeQueue.Enqueue((write.Slice(write.Length - toFrame), disposable));
                                            writeQueue.Enqueue((frameHeader, Disposable.Empty));
                                            break;
                                        }
                                        writeQueue.Enqueue((write.Slice(write.Length - temp, toFrame), Disposable.Empty));
                                        temp -= toFrame;
                                        writeQueue.Enqueue((frameHeader, Disposable.Empty));
                                    }
                                    else
                                    {
                                        writeQueue.Enqueue((write.Slice(write.Length - temp), disposable));
                                        frameOffset += temp;
                                        break;
                                    }
                                }
                            }
                        }
                        //_write
                        {
                            var temp = _write.Length - _available;
                            for (; ; )
                            {
                                var toFrame = frameSize - frameOffset;
                                if (temp > toFrame)
                                {
                                    if (headersFlag)
                                    {
                                        len1[0] = (byte)((frameSize & 0x00FF0000) >> 16);
                                        len2[0] = (byte)((frameSize & 0x0000FF00) >> 8);
                                        len3[0] = (byte)(frameSize & 0x000000FF);
                                        if (request.Content == null)
                                            flags[0] = 0b0000_0001;//EndStream
                                        else
                                            flags[0] = 0b0000_0000;
                                        headersFlag = false;
                                    }
                                    else
                                    {
                                        len1[0] = (byte)((frameSize & 0x00FF0000) >> 16);
                                        len2[0] = (byte)((frameSize & 0x0000FF00) >> 8);
                                        len3[0] = (byte)(frameSize & 0x000000FF);
                                        flags[0] = 0b0000_0000;
                                    }
                                    var tempBytes = new byte[toFrame + 9];
                                    //frameHeader
                                    len1 = tempBytes.AsSpan(toFrame, 1);
                                    len2 = tempBytes.AsSpan(toFrame + 1, 1);
                                    len3 = tempBytes.AsSpan(toFrame + 2, 1);
                                    flags = tempBytes.AsSpan(toFrame + 4, 1);
                                    tempBytes[toFrame + 3] = 0x9;
                                    tempBytes[toFrame + 5] = (byte)((stream.StreamId & 0xFF000000) >> 24);
                                    tempBytes[toFrame + 6] = (byte)((stream.StreamId & 0x00FF0000) >> 16);
                                    tempBytes[toFrame + 7] = (byte)((stream.StreamId & 0x0000FF00) >> 8);
                                    tempBytes[toFrame + 8] = (byte)(stream.StreamId & 0x000000FF);

                                    _write.Slice(_write.Length - _available - temp, toFrame).Span.CopyTo(tempBytes);
                                    writeQueue.Enqueue((tempBytes, Disposable.Empty));
                                    temp -= toFrame;
                                    frameOffset = 0;
                                }
                                else
                                {
                                    Debug.Assert(temp > 0);
                                    Debug.Assert(headersFlag == false);

                                    var available = _write.Length - temp - _available;
                                    _write.Slice(available, temp).CopyTo(_write);
                                    _available += available;
                                    _writeQueue = writeQueue;

                                    frameSize = temp + frameOffset;
                                    len1[0] = (byte)((frameSize & 0x00FF0000) >> 16);
                                    len2[0] = (byte)((frameSize & 0x0000FF00) >> 8);
                                    len3[0] = (byte)(frameSize & 0x000000FF);
                                    flags[0] = 0b0000_0100;//EndHeaders
                                    break;
                                }
                            }
                        }
                    }
                }
                private async Task WriteAsync()
                {
                    TryWrite();
                    try
                    {
                        for (; ; )
                        {
                            var sendWindow = 0;
                            var stream = default(Http2Stream);
                            var writer = default(Action);
                            var writeWaiter = default(TaskCompletionSource<object>);
                            lock (this)
                            {
                                if (_dataQueue.Count > 0 && _sendWindow >= 4096)
                                {
                                    while (_dataQueue.TryPeek(out stream))
                                    {
                                        if (stream.Closed)
                                        {
                                            var __stream = _dataQueue.Dequeue();
                                            Debug.Assert(__stream == stream);
                                            continue;
                                        }
                                        break;
                                    }
                                }
                                if (stream != null)
                                {
                                    sendWindow = _sendWindow;
                                    _sendWindow = 0;
                                }
                                else 
                                {
                                    if (_sendQueue.TryDequeue(out writer, out stream))
                                    {
                                        if (stream != null && stream.Request.Content != null)
                                        {
                                            _dataQueue.Enqueue(stream);
                                        }
                                    }
                                    else
                                    {
                                        if (_activeStreams == -1)
                                            return;

                                        Debug.Assert(_writeWaiter == null);
                                        writeWaiter = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
                                        _writeWaiter = writeWaiter;
                                    }
                                }
                            }
                            if (writeWaiter != null)
                            {
                                await writeWaiter.Task;
                                continue;
                            }
                            if (sendWindow > 0)
                            {
                                Debug.Assert(_available == _write.Length);
                                Debug.Assert(stream != null);
                                Debug.Assert(stream.Request.Content != null);
                                for (; ; )
                                {
                                    Debug.Assert(sendWindow >= 4096);
                                    Debug.Assert(_write.Length > 9);
                                    unsafe
                                    {
                                        _pWrite[3] = 0x0;//Data
                                        _pWrite[5] = (byte)((stream.StreamId & 0xFF000000) >> 24);
                                        _pWrite[6] = (byte)((stream.StreamId & 0x00FF0000) >> 16);
                                        _pWrite[7] = (byte)((stream.StreamId & 0x0000FF00) >> 8);
                                        _pWrite[8] = (byte)(stream.StreamId & 0x000000FF);
                                    }
                                    var requestBody = stream.RequestBody;
                                    if (requestBody == null) 
                                    {
                                        lock (this)
                                        {
                                            _sendWindow += sendWindow;
                                            var __stream = _dataQueue.Dequeue();
                                            Debug.Assert(__stream == stream);
                                            break;
                                        }
                                    }
                                    var result = await requestBody.ReadAsync(_write.Slice(9, Math.Min(_maxFrameSize, Math.Min(sendWindow, _write.Length - 9))));
                                    var endStream = result == 0 || requestBody.Available == 0;
                                    unsafe
                                    {
                                        _pWrite[0] = (byte)((result & 0x00FF0000) >> 16);
                                        _pWrite[1] = (byte)((result & 0x0000FF00) >> 8);
                                        _pWrite[2] = (byte)(result & 0x000000FF);
                                        if (endStream)
                                            _pWrite[4] = 0b0000_0001;//EndStream
                                        else
                                            _pWrite[4] = 0b0000_0000;

                                    }
                                    await _connection.SendAsync(_write.Slice(0, result + 9));
                                    sendWindow -= result;
                                    lock (this) 
                                    {
                                        if (endStream || stream.Closed)
                                        {
                                            _sendWindow += sendWindow;
                                            var __stream = _dataQueue.Dequeue();
                                            Debug.Assert(__stream == stream);
                                            break;
                                        }
                                        if (sendWindow < 4096)
                                        {
                                            Debug.Assert(sendWindow >= 0);
                                            _sendWindow += sendWindow;
                                            break;
                                        }
                                    }
                                }
                                continue;
                            }
                            else
                            {
                                for (; ; )
                                {
                                    if (writer != null)
                                    {
                                        writer.Invoke();
                                    }
                                    else if (stream != null)
                                    {
                                        WriteHeaders(stream);
                                    }
                                    else
                                    {
                                        await _connection.SendAsync(_write.Slice(0, _write.Length - _available));
                                        _available = _write.Length;
                                        break;
                                    }
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
                                        await _connection.SendAsync(_write.Slice(0, _write.Length - _available));
                                        _available = _write.Length;
                                        break;
                                    }
                                    if (_available < 1024)
                                    {
                                        await _connection.SendAsync(_write.Slice(0, _write.Length - _available));
                                        _available = _write.Length;
                                        break;
                                    }
                                    lock (this)
                                    {
                                        if (_sendQueue.TryDequeue(out writer, out stream))
                                        {
                                            if (stream != null && stream.Request.Content != null)
                                            {
                                                _dataQueue.Enqueue(stream);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Close(-1, ex);
                    }
                    finally
                    {
                        #region Dispose
                        Debug.Assert(_write.Length > 0);
                        _available = 0;
                        _write = Memory<byte>.Empty;
                        _writeHandle.Dispose();
                        unsafe { _pWrite = (byte*)0; }
                        _writeDisposable.Dispose();
                        _writeDisposable = null;
                        if (_writeQueue != null)
                        {
                            while (_writeQueue.TryDequeue(out _, out var disposable))
                            {
                                disposable.Dispose();
                            }
                            _writeQueue = null;
                        }
                        #endregion
                    }
                }
                private async ValueTask ReceiveAsync(int length)
                {
                    var toReceive = _end - _start;
                    if (toReceive >= length)//length=0
                    {
                        _position = _start + length;
                    }
                    else
                    {
                        if (toReceive == 0)
                        {
                            var result = await _connection.ReceiveAsync(_read);
                            if (result == 0)
                                throw new InvalidDataException("FIN");

                            _start = 0;
                            _end = result;
                            toReceive = result;
                        }
                        while (toReceive < length)
                        {
                            if (_end < _read.Length)
                            {
                                var result = await _connection.ReceiveAsync(_read.Slice(_end));
                                if (result == 0)
                                    throw new InvalidDataException("FIN");

                                _end += result;
                                toReceive += result;
                            }
                            else
                            {
                                Debug.Assert(_end == _read.Length);
                                if (_readQueue == null)
                                {
                                    if (_start == 0 || (_start << 1) > _read.Length)//_start过半了
                                    {
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
                                        _end = result;
                                        toReceive += result;
                                    }
                                    else
                                    {
                                        var count = _end - _start;
                                        _read.Span.Slice(_start).CopyTo(_read.Span.Slice(0, count));
                                        _start = 0;
                                        _end = count;

                                        var result = await _connection.ReceiveAsync(_read.Slice(_end));
                                        if (result == 0)
                                            throw new InvalidDataException("FIN");

                                        _end += result;
                                        toReceive += result;
                                    }
                                }
                                else
                                {
                                    _readQueue.Enqueue((_read.Slice(_start), _readDisposable));
                                    _readHandle.Dispose();

                                    _read = ConnectionExtensions.GetBytes(out _readDisposable);
                                    _readHandle = _read.Pin();
                                    unsafe { _pRead = (byte*)_readHandle.Pointer; }

                                    var result= await _connection.ReceiveAsync(_read);
                                    if (result == 0)
                                        throw new InvalidDataException("FIN");

                                    _start = 0;
                                    _end = result;
                                    toReceive += result;
                                }
                            }
                        }
                        _position = _end - (toReceive - length);
                    }
                }
                private async ValueTask ReadFrameAsync()
                {
                    const int frameHeaderLength = 9;
                    void ReadFrame()
                    {
                        var frameBytes = ReadBytes();
                        Debug.Assert(frameBytes.Length == frameHeaderLength);
                        _frameLength = (frameBytes[0] << 16) | (frameBytes[1] << 8) | frameBytes[2];
                        _frameType = frameBytes[3];
                        _frameFlags = frameBytes[4];
                        _frameStreamId = ((frameBytes[5] << 24) | (frameBytes[6] << 16) | (frameBytes[7] << 8) | frameBytes[8]) & 0x7FFFFFFF;

                        Debug.WriteLine($"StreamId={_frameStreamId};Type={_frameType};Flags={_frameFlags};Length={_frameLength}");

                        if (_frameLength > _maxFrameSize)
                            throw new ProtocolViolationException("Protocol Error");
                    }
                    await ReceiveAsync(frameHeaderLength);
                    ReadFrame();
                }
                private async ValueTask DrainFrameAsync()
                {
                    Debug.Assert(_readQueue == null);

                    if (_frameLength == 0)
                        return;

                    while (_frameLength > 0)
                    {
                        var  available = _end - _start;
                        if (available <= 0)
                        {
                            available = await _connection.ReceiveAsync(_read);
                            if (available == 0)
                                throw new InvalidOperationException("FIN");

                            _start = 0;
                            _end = available;
                        }
                        var toDrain = Math.Min(available, _frameLength);
                        _frameLength -= toDrain;
                        _start += toDrain;
                    }
                    Debug.Assert(_frameLength == 0);
                }
                private ReadOnlySpan<byte> ReadBytes()
                {
                    Debug.Assert(_position >= _start);
                    Debug.Assert(_position <= _end);

                    if (_readQueue == null)
                    {
                        unsafe
                        {
                            var span = new ReadOnlySpan<byte>(_pRead + _start, _position - _start);
                            _start = _position;
                            return span;
                        }
                    }
                    else
                    {
                        unsafe
                        {
                            var span = new ReadOnlySpan<byte>(_pRead + _start, _position - _start);
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
                private async Task ReadAsync()
                {
                    _read = ConnectionExtensions.GetBytes(out _readDisposable);
                    _readHandle = _read.Pin();
                    unsafe { _pRead = (byte*)_readHandle.Pointer; }
                    try
                    {
                        //Settings
                        await ReadFrameAsync();
                        if (_frameType != 0x4)
                            throw new ProtocolViolationException("Settings");
                        await ReadSettingsAsync();
                        lock (_client)
                        {
                            lock (this)
                            {
                                if (_closeWaiter != null)
                                    return;
                                Debug.Assert(!_active);
                                Debug.Assert(_activeStreams == 1);
                                Debug.Assert(_client._activing == this);
                                _active = true;
                                _client._activing = null;
                                AddStreams(_maxStreams - _activeStreams);
                            }
                            var active = _client._active;
                            if (active != null)
                            {
                                lock (active)
                                {
                                    active.TryActive();
                                }
                            }
                        }
                        for (; ; )
                        {
                            await ReadFrameAsync();
                            switch (_frameType)
                            {
                                case 0x0://Data
                                    await ReadDataAsync();//如果丢弃Data别忘记发送WindowUpdate
                                    break;
                                case 0x1://Headers 
                                    await ReadHeadersAsync();
                                    break;
                                case 0x3://RstStream
                                    await ReadRstStreamAsync();
                                    break;
                                case 0x4://Settings
                                    await ReadSettingsAsync();
                                    break;
                                case 0x6://Ping
                                    await ReadPingAsync();
                                    break;
                                case 0x7://GoAway
                                    await ReadGoAwayAsync();
                                    break;
                                case 0x8://WindowUpdate
                                    await ReadWindowUpdateAsync();
                                    break;
                                case 0x2://Priority
                                    await DrainFrameAsync();
                                    break;
                                case 0x5://PushPromise
                                case 0x9://Continuation
                                    throw new ProtocolViolationException("Protocol Error");
                                default:
                                    await DrainFrameAsync();
                                    break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Close(-1, ex);
                    }
                    finally
                    {
                        #region Dispose
                        Debug.Assert(_read.Length > 0);
                        _start = 0;
                        _end = 0;
                        _position = 0;
                        _headers = 0;
                        _read = Memory<byte>.Empty;
                        _readHandle.Dispose();
                        unsafe { _pRead = (byte*)0; }
                        _readDisposable.Dispose();
                        _readDisposable = null;
                        if (_readQueue != null)
                        {
                            while (_readQueue.TryDequeue(out _, out var disposable))
                            {
                                disposable.Dispose();
                            }
                            _readQueue = null;
                        }
                        if (_headersQueue != null)
                        {
                            while (_headersQueue.TryDequeue(out var disposable))
                            {
                                disposable.Dispose();
                            }
                            _headersQueue = null;
                        }
                        #endregion
                    }
                }
                private async ValueTask ReadSettingsAsync()
                {
                    const int settingLength = 6;
                    void ReadSetting()
                    {
                        var settingBytes = ReadBytes();
                        Debug.Assert(settingBytes.Length == settingLength);
                        ushort settingId = (ushort)((settingBytes[0] << 8) | settingBytes[1]);
                        uint settingValue = (uint)((settingBytes[2] << 24) | (settingBytes[3] << 16) | (settingBytes[4] << 8) | settingBytes[5]);
                        switch (settingId)
                        {
                            case 0x1://HeaderTableSize
                                Debug.WriteLine($"Settings-HeaderTableSize:{settingValue}");
                                if (settingValue > _MaxHeaderTableSize)
                                    throw new ProtocolViolationException("Setting HeaderTableSize");

                                _decoderTable.MaxSize = (int)settingValue;
                                break;
                            case 0x3://MaxConcurrentStreams
                                Debug.WriteLine($"Settings-MaxConcurrentStreams:{settingValue}");
                                if (settingValue == 0 || settingValue > 0x7FFFFFFF)
                                    throw new ProtocolViolationException("Setting MaxConcurrentStreams");

                                lock (this)
                                {
                                    var maxStreams = _maxStreams;
                                    _maxStreams = Math.Min(_initialMaxStreams, (int)settingValue);
                                    if (_active && _closeWaiter != null)
                                        AddStreams(_maxStreams - maxStreams);
                                }
                                break;
                            case 0x4://InitialWindowSize
                                Debug.WriteLine($"Settings-InitialWindowSize:{settingValue}");
                                if (settingValue == 0 || settingValue > 0x7FFFFFFF)
                                    throw new ProtocolViolationException("Setting InitialWindowSize");

                                _initialWindowSize = (int)settingValue;
                                //lock (this) server 会发送windowUpdate帧更新窗口
                                //{
                                //    var increment = (int)settingValue - _initialWindowSize;//may<0
                                //    _initialWindowSize = (int)settingValue;//需要锁吗
                                //    _sendWindow += increment;
                                //}
                                break;
                            case 0x5://MaxFrameSize
                                Debug.WriteLine($"Settings-MaxFrameSize:{settingValue}");
                                if (settingValue < _MinFrameSize || settingValue > _MaxFrameSize)
                                    throw new ProtocolViolationException("Setting MaxFrameSize");

                                _maxFrameSize = (int)settingValue;
                                break;
                            default:
                                //ignore
                                break;
                        }
                    }

                    if ((_frameFlags & 0b00000001) != 0)//SettingsAck
                    {
                        if (_frameLength != 0)
                            throw new ProtocolViolationException("SettingsAck length must 0");

                        Debug.WriteLine($"SettingsAck:Receive");
                        //Ignore
                    }
                    else
                    {
                        if (_frameLength == 0)
                            return;
                        if ((_frameLength % 6) != 0)
                            throw new ProtocolViolationException("Settings Size Error");

                        while (_frameLength > 0)
                        {
                            await ReceiveAsync(settingLength);
                            _frameLength -= settingLength;
                            ReadSetting();
                        }

                        //SettingsAck
                        lock (this)
                        {
                            Enqueue(() => { WriteFrame(0, 0x4, 0b00000001, 0); });
                        }
                    }
                }
                private async ValueTask ReadRstStreamAsync()
                {
                    const int rstStreamLength = 4;
                    void ReadRstStream()
                    {
                        var rstStreamBytes = ReadBytes();
                        Debug.Assert(rstStreamBytes.Length == rstStreamLength);
                        var errorCode = (rstStreamBytes[0] << 24) | (rstStreamBytes[1] << 16) | (rstStreamBytes[2] << 8) | rstStreamBytes[3];
                        Debug.WriteLine($"RstStream:{_frameStreamId},ErrorCode:{errorCode}");

                        Http2Stream stream;
                        lock (this)
                        {
                            if (!_streams.TryGetValue(_frameStreamId, out stream))
                                return;

                            if (stream.Closed)
                                return;
                            stream.Closed = true;
                            _streams.Remove(stream.StreamId);
                            if (stream.ResponseBody == null)
                            {
                                stream.ResponseTcs.TrySetException(new ProtocolViolationException("Receive Rst_Stream"));
                            }
                            else
                            {
                                stream.ResponseBody.Close(new ProtocolViolationException("Receive Rst_Stream"));
                            }
                            NextStream();
                        }
                    }
                    if (_frameStreamId == 0)
                        throw new ProtocolViolationException("Protocol Error");
                    if (_frameLength != rstStreamLength)
                        throw new ProtocolViolationException("RstStream length must 4");

                    await ReceiveAsync(rstStreamLength);
                    ReadRstStream();
                }
                private async ValueTask ReadPingAsync()
                {
                    const int pingLength = 8;
                    void ReadPing()//long or 2*int
                    {
                        var pingBytes = ReadBytes();
                        Debug.WriteLine($"Ping:{Encoding.ASCII.GetString(pingBytes)}");
                        Debug.Assert(pingBytes.Length == pingLength);
                        //OpaqueData
                        var opaqueData1 = (pingBytes[0] << 24) | (pingBytes[1] << 16) | (pingBytes[2] << 8) | pingBytes[3];
                        var opaqueData2 = (pingBytes[4] << 24) | (pingBytes[5] << 16) | (pingBytes[6] << 8) | pingBytes[7];
                        lock (this)
                        {
                            Enqueue(() => {
                                WriteFrame(8, 0x6, 0b00000001, 0);//Ack
                                Write(opaqueData1);
                                Write(opaqueData2);
                            });
                        }
                    }

                    if (_frameStreamId != 0)
                        throw new ProtocolViolationException("Protocol Error");
                    if (_frameLength != pingLength)
                        throw new ProtocolViolationException("Ping length must 8");

                    if ((_frameFlags & 0b00000001) != 0)//PingAck
                    {
                        Debug.WriteLine($"PingAck:Receive");
                        await DrainFrameAsync();
                    }
                    else
                    {
                        await ReceiveAsync(pingLength);
                        ReadPing();
                    }
                }
                private async ValueTask ReadGoAwayAsync()
                {
                    void ReadGoAway()
                    {
                        var goAwayBytes = ReadBytes();
                        var lastStreamId = ((goAwayBytes[0] << 24) | (goAwayBytes[1] << 16) | (goAwayBytes[2] << 8) | goAwayBytes[3]) & 0x7FFFFFFF;
                        var errorCode = (goAwayBytes[4] << 24) | (goAwayBytes[5] << 16) | (goAwayBytes[6] << 8) | goAwayBytes[7];
                        if (lastStreamId == 0)
                            throw new ProtocolViolationException("Protocol Error");

                        Debug.WriteLine($"GoAway:lastStreamId={lastStreamId},errorCode={errorCode}");
                        Close(lastStreamId, new ProtocolViolationException($"GoAway:ErrorCode={errorCode}"));
                    }
                    if (_frameStreamId != 0)
                        throw new ProtocolViolationException("Protocol Error");
                    if (_frameLength < 8)
                        throw new ProtocolViolationException("Frame Size Error");

                    await ReceiveAsync(8);
                    ReadGoAway();
                    //Additional Debug Data
                    _frameLength -= 8;
                    await DrainFrameAsync();
                }
                private async ValueTask ReadWindowUpdateAsync()
                {
                    const int windowUpdateLength = 4;
                    void ReadWindowUpdate()
                    {
                        var windowUpdateBytes = ReadBytes();
                        Debug.Assert(windowUpdateBytes.Length == windowUpdateLength);
                        var increment = ((windowUpdateBytes[0] << 24) | (windowUpdateBytes[1] << 16) | (windowUpdateBytes[2] << 8) | windowUpdateBytes[3]) & 0x7FFFFFFF;
                        Debug.WriteLine($"WindowUpdate:{increment} StreamId:{_frameStreamId}");
                        if (_frameStreamId > 0)
                            return;//TODO?
                        lock (this)
                        {
                            _sendWindow += increment;
                            if (_writeWaiter != null && _sendWindow > 0)
                            {
                                _writeWaiter.SetResult(null);
                                _writeWaiter = null;
                            }
                        }
                    }

                    await ReceiveAsync(windowUpdateLength);
                    ReadWindowUpdate();
                }
                private async ValueTask ReadDataAsync()
                {
                    Http2Stream stream;
                    lock (this)
                    {
                        if (_frameLength > _receiveWindow)
                            throw new ProtocolViolationException("Flow_Control_Error");

                        _receiveWindow -= _frameLength;
                        _streams.TryGetValue(_frameStreamId, out stream);
                    }
                    
                    if (stream == null)//Ignore
                    {
                        Debug.WriteLine("Ignore DataFrame");
                        var increment = _frameLength;
                        await DrainFrameAsync();
                        lock (this)//WindowUpdate
                        {
                            _windowUpdate += increment;
                            if (_windowUpdate > _WindowUpdate)
                            {
                                var windowUpdate = _windowUpdate;
                                Enqueue(() => {
                                    WriteFrame(4, 0x8, 0, 0);
                                    Write(windowUpdate);
                                });
                                _windowUpdate = 0;
                                _receiveWindow += windowUpdate;
                            }
                            return;
                        }
                    }

                    Debug.Assert(stream.ResponseBody != null);
                    if ((_frameFlags & 0b00001000) != 0)//Padded
                    {
                        if (_frameLength == 0)
                            throw new ProtocolViolationException("Padded");

                        await ReceiveAsync(1);
                        var padLength = ReadBytes()[0];
                        _frameLength -= 1;

                        if (_frameLength < padLength)
                            throw new ProtocolViolationException("Padded");

                        while (_frameLength > padLength)
                        {
                            var available = _end - _start;
                            if (available <= 0)
                            {
                                available = await _connection.ReceiveAsync(_read);
                                if (available == 0)
                                    throw new InvalidOperationException("FIN");
                                _start = 0;
                                _end = available;
                            }
                            var toRead = Math.Min(available, _frameLength - padLength);
                            stream.ResponseBody.OnData(_read.Span.Slice(_start, toRead));
                            _frameLength -= toRead;
                            _start += toRead;
                        }
                        await DrainFrameAsync();
                        var increment = padLength + 1;
                        lock (this)//WindowUpdate
                        {
                            _windowUpdate += increment;
                            if (_windowUpdate > _WindowUpdate)
                            {
                                var windowUpdate = _windowUpdate;
                                Enqueue(() => {
                                    WriteFrame(4, 0x8, 0, 0);
                                    Write(windowUpdate);
                                });
                                _windowUpdate = 0;
                                _receiveWindow += windowUpdate;
                            }
                        }
                    }
                    else
                    {
                        while (_frameLength > 0)
                        {
                            var available = _end - _start;
                            if (available <= 0)
                            {
                                available = await _connection.ReceiveAsync(_read);
                                if (available == 0)
                                    throw new InvalidOperationException("FIN");
                                _start = 0;
                                _end = available;
                            }
                            var toRead = Math.Min(available, _frameLength);
                            stream.ResponseBody.OnData(_read.Span.Slice(_start, toRead));
                            _frameLength -= toRead;
                            _start += toRead;
                        }
                    }

                    if ((_frameFlags & 0b00000001) != 0)//EndStream
                    {
                        lock (this)
                        {
                            if (stream.Closed)
                                return;
                            stream.Closed = true;
                            _streams.Remove(stream.StreamId);
                            stream.ResponseBody.Close(null);
                            NextStream();
                        }
                    }
                }
                private async ValueTask ReadHeadersAsync()
                {
                    Http2Stream stream;
                    lock (this)
                    {
                        //TODO Trailers?
                        if (!_streams.TryGetValue(_frameStreamId, out stream))
                            throw new ProtocolViolationException("StreamId");
                    }
                    if (stream.Response != null)
                        throw new ProtocolViolationException("Headers");

                    stream.Response = new HttpResponse();
                    await ReceiveAsync(_frameLength);
                    if (!ReadHeaders(stream,out var endStream))//endHeaders
                    {
                        for (; ; )
                        {
                            await ReadFrameAsync();
                            if (_frameType != 0x9)
                                throw new ProtocolViolationException("Continuation");
                            await ReceiveAsync(_frameLength);
                            if (ReadContinuation(stream))
                            {
                                break;
                            }
                        }
                    }

                    lock (this)
                    {
                        if (stream.Closed)
                            return;

                        if (endStream)
                        {
                            stream.Closed = true;
                            _streams.Remove(stream.StreamId);
                            FeaturesExtensions.RegisterForDispose(stream.Request, stream.Response);
                            stream.ResponseTcs.TrySetResult(stream.Response);
                            NextStream();
                        }
                        else
                        {
                            var content = new Http2Content(this, stream);
                            stream.ResponseBody = content;
                            stream.Response.Content = content;
                            stream.Response.RegisterForDispose(content);
                            FeaturesExtensions.RegisterForDispose(stream.Request, stream.Response);
                            stream.ResponseTcs.TrySetResult(stream.Response);
                        }
                    }
                }
                private bool ReadHeaders(Http2Stream stream, out bool endStream)
                {
                    var headersBytes = ReadBytes();
                    if ((_frameFlags & 0b00001000) != 0)//Padded
                    {
                        if (headersBytes.Length == 0)
                            throw new ProtocolViolationException();

                        int padLength = headersBytes[0];
                        headersBytes = headersBytes.Slice(1);

                        if (headersBytes.Length < padLength)
                            throw new ProtocolViolationException();

                        headersBytes = headersBytes.Slice(0, headersBytes.Length - padLength);
                    }

                    if ((_frameFlags & 0b00100000) != 0)//Priority
                    {
                        if (headersBytes.Length < 5)//StreamDependency(4)+Weight(1)
                            throw new ProtocolViolationException();

                        headersBytes = headersBytes.Slice(5);//ignore
                    }

                    _headers = headersBytes.Length;
                    if (_headers > _MaxHeaders)
                        throw new ProtocolViolationException($"MaxHeaders:{_MaxHeaders}");

                    endStream = (_frameFlags & 0b00000001) != 0;//EndStream

                    if ((_frameFlags & 0b00000100) != 0)//EndHeaders
                    {
                        ReadHeaders(headersBytes, stream.Response);
                        return true;
                    }
                    
                    if (headersBytes.Length > 0)//是否允许为空
                    {
                        Debug.Assert(_headersQueue == null);
                        _headersQueue = new Queue<UnmanagedMemory<byte>>();
                        var tempBytes = new UnmanagedMemory<byte>(headersBytes.Length);
                        headersBytes.CopyTo(tempBytes.GetSpan());
                        _headersQueue.Enqueue(tempBytes);
                    }
                    return false;
                }
                private bool ReadContinuation(Http2Stream stream)
                {
                    if (_frameStreamId != stream.StreamId)
                        throw new ProtocolViolationException("StreamId");

                    var headersBytes = ReadBytes();
                    _headers += headersBytes.Length;
                    if (_headers > _MaxHeaders)
                        throw new ProtocolViolationException($"MaxHeaders:{_MaxHeaders}");

                    if ((_frameFlags & 0b00000100) != 0)//EndHeaders
                    {
                        ReadHeaders(headersBytes, stream.Response);
                        return true;
                    }

                    if (headersBytes.Length > 0)
                    {
                        Debug.Assert(_headersQueue != null);
                        var tempBytes = new UnmanagedMemory<byte>(headersBytes.Length);
                        headersBytes.CopyTo(tempBytes.GetSpan());
                        _headersQueue.Enqueue(tempBytes);
                    }
                    return false;
                }
                private void ReadHeaders(ReadOnlySpan<byte> headersBytes, HttpResponse response)
                {
                    //NotSupported \0
                    if (_headersQueue != null)
                    {
                        var tempBytes = new byte[_headers];
                        var tempSpan = tempBytes.AsSpan();
                        while (_headersQueue.TryDequeue(out var headerBytes))
                        {
                            headerBytes.GetSpan().CopyTo(tempSpan);
                            tempSpan = tempSpan.Slice(headerBytes.Length);
                            headerBytes.Dispose();
                        }
                        _headersQueue = null;
                        Debug.Assert(tempSpan.Length == headersBytes.Length);
                        headersBytes.CopyTo(tempSpan);
                        headersBytes = tempBytes;
                    }
                    if (headersBytes.IsEmpty)
                        throw new ProtocolViolationException("HeaderBlock Empty");

                    //是否异常包装
                    (var state, var prefix) = _Ready[headersBytes[0]];
                    var offset = 1;
                    if (state == State.SizeUpdate)//https://tools.ietf.org/html/rfc7541#section-4.2
                    {
                        var size = prefix == 0b0001_1111 ? ReadHpack(headersBytes, prefix, ref offset) : prefix;
                        if (size > _decoderTable.MaxSize)
                            throw new ProtocolViolationException("Update HeaderTableSize");
                        _decoderTable.Size = size;
                        state = State.Ready;
                    }
                    int? statusCode = null;//Only Once :status
                    for (; ; )
                    {
                        switch (state)
                        {
                            case State.Ready:
                                (state, prefix) = _Ready[headersBytes[offset++]];
                                continue;
                            case State.Indexed://0b1000_0000
                                {
                                    if (prefix == 0)
                                        throw new ProtocolViolationException("Index");

                                    var index = prefix == 0b0111_1111 ? ReadHpack(headersBytes, prefix, ref offset) : prefix;
                                    if (!_decoderTable.TryGetField(index, out var name, out var value))
                                        throw new ProtocolViolationException("Index Not Found");
                                    Debug.Assert(name.Length > 0);
                                    if (name[0] == ':')
                                    {
                                        if (name != ":status" || statusCode.HasValue)
                                            throw new ProtocolViolationException(":status");
                                        //index
                                        switch (index)
                                        {
                                            case 8:
                                                statusCode = 200;
                                                break;
                                            case 9:
                                                statusCode = 204;
                                                break;
                                            case 10:
                                                statusCode = 206;
                                                break;
                                            case 11:
                                                statusCode = 304;
                                                break;
                                            case 12:
                                                statusCode = 400;
                                                break;
                                            case 13:
                                                statusCode = 404;
                                                break;
                                            case 14:
                                                statusCode = 500;
                                                break;
                                            default:
                                                statusCode = int.Parse(value);
                                                break;
                                        }
                                    }
                                    else
                                    {
                                        response.Headers.Add(name, value);
                                    }
                                    state = State.Ready;
                                    break;
                                }
                            case State.Indexing://0b0100_0000
                                if (prefix == 0)//Name Value
                                {
                                    var name = ReadLiteral(headersBytes, ref offset);
                                    var value = ReadLiteral(headersBytes, ref offset);
                                    if (string.IsNullOrEmpty(name))
                                        throw new ProtocolViolationException("HeaderName Empty");
                                    for (int i = 0; i < name.Length; i++)
                                    {
                                        if (name[i] >= 65 && name[i] <= 90)//A-Z
                                            throw new ProtocolViolationException("HeaderName Must LowerCase");
                                    }
                                    if (name[0] == ':')
                                    {
                                        if (name != ":status" || statusCode.HasValue)
                                            throw new ProtocolViolationException(":status");

                                        statusCode = int.Parse(value);
                                    }
                                    else
                                    {
                                        response.Headers.Add(name, value);
                                    }
                                    _decoderTable.Add(name, value);
                                    state = State.Ready;
                                    break;
                                }
                                else
                                {
                                    var index = prefix == 0b0011_1111 ? ReadHpack(headersBytes, prefix, ref offset) : prefix;
                                    if (!_decoderTable.TryGetField(index, out var name, out var _))
                                        throw new ProtocolViolationException("Index Not Found");
                                    var value = ReadLiteral(headersBytes, ref offset);
                                    if (name[0] == ':')
                                    {
                                        if (name != ":status" || statusCode.HasValue)
                                            throw new ProtocolViolationException(":status");

                                        statusCode = int.Parse(value);
                                    }
                                    else
                                    {
                                        response.Headers.Add(name, value);
                                    }
                                    _decoderTable.Add(name, value);
                                    state = State.Ready;
                                    break;
                                }
                            case State.WithoutIndexing://0000
                            case State.NeverIndexed://0001
                                if (prefix == 0)//Name Value
                                {
                                    var name = ReadLiteral(headersBytes, ref offset);
                                    var value = ReadLiteral(headersBytes, ref offset);
                                    if (string.IsNullOrEmpty(name))
                                        throw new ProtocolViolationException("HeaderName Empty");
                                    for (int i = 0; i < name.Length; i++)
                                    {
                                        if (name[i] >= 65 && name[i] <= 90)//A-Z
                                            throw new ProtocolViolationException("HeaderName Must LowerCase");
                                    }
                                    if (name[0] == ':')
                                    {
                                        if (name != ":status" || statusCode.HasValue)
                                            throw new ProtocolViolationException(":status");

                                        statusCode = int.Parse(value);
                                    }
                                    else
                                    {
                                        response.Headers.Add(name, value);
                                    }
                                    state = State.Ready;
                                    break;
                                }
                                else
                                {
                                    var index = prefix == 0b0000_1111 ? ReadHpack(headersBytes, prefix, ref offset) : prefix;
                                    if (!_decoderTable.TryGetField(index, out var name, out var _))
                                        throw new ProtocolViolationException("Index Not Found");
                                    var value = ReadLiteral(headersBytes, ref offset);
                                    if (name[0] == ':')
                                    {
                                        if (name != ":status" || statusCode.HasValue)
                                            throw new ProtocolViolationException(":status");

                                        statusCode = int.Parse(value);
                                    }
                                    else
                                    {
                                        response.Headers.Add(name, value);
                                    }
                                    state = State.Ready;
                                    break;
                                }
                            case State.SizeUpdate://001
                                throw new ProtocolViolationException("SizeUpdate");
                            default:
                                throw new InvalidOperationException("Never");
                        }

                        if (offset == _headers)
                            break;
                    }
                    if (!statusCode.HasValue || state != State.Ready)
                        throw new ProtocolViolationException();

                    response.StatusCode = statusCode.Value;
                    response.Version = HttpVersion.Version20;
                }
                private static int ReadHpack(ReadOnlySpan<byte> headersBytes, int prefix, ref int offset)
                {
                    long value = prefix;
                    var bits = 0;
                    while (bits < 32)
                    {
                        var b = headersBytes[offset++];
                        //value = value + (b & 0b0111_1111) << bits;运算符优先级
                        value += (b & 0b0111_1111) << bits;
                        if ((b & 0b1000_0000) == 0)
                            break;
                        bits += 7;
                    }
                    if (value > int.MaxValue)
                        throw new ProtocolViolationException("> int.MaxValue");

                    return (int)value;
                }
                private static string ReadHuffman(ReadOnlySpan<byte> bytes)
                {
                    if (bytes.IsEmpty)
                        return string.Empty;

                    var sb = StringExtensions.ThreadRent(out var disposable);
                    try
                    {
                        var i = 0;
                        var lastDecodedBits = 0;
                        while (i < bytes.Length)
                        {
                            var next = (uint)(bytes[i] << 24 + lastDecodedBits);
                            next |= (i + 1 < bytes.Length ? (uint)(bytes[i + 1] << 16 + lastDecodedBits) : 0);
                            next |= (i + 2 < bytes.Length ? (uint)(bytes[i + 2] << 8 + lastDecodedBits) : 0);
                            next |= (i + 3 < bytes.Length ? (uint)(bytes[i + 3] << lastDecodedBits) : 0);
                            next |= (i + 4 < bytes.Length ? (uint)(bytes[i + 4] >> (8 - lastDecodedBits)) : 0);

                            var ones = (uint)(int.MinValue >> (8 - lastDecodedBits - 1));
                            if (i == bytes.Length - 1 && lastDecodedBits > 0 && (next & ones) == ones)
                                break;

                            var validBits = Math.Min(30, (8 - lastDecodedBits) + (bytes.Length - i - 1) * 8);

                            var ch = -1;
                            var decodedBits = 0;
                            var codeMax = 0;
                            for (var j = 0; j < _DecodingTable.Length && _DecodingTable[j].codeLength <= validBits; j++)
                            {
                                var (codeLength, codes) = _DecodingTable[j];

                                if (j > 0)
                                {
                                    codeMax <<= codeLength - _DecodingTable[j - 1].codeLength;
                                }

                                codeMax += codes.Length;

                                var mask = int.MinValue >> (codeLength - 1);
                                var masked = (next & mask) >> (32 - codeLength);

                                if (masked < codeMax)
                                {
                                    decodedBits = codeLength;
                                    ch = codes[codes.Length - (codeMax - masked)];
                                    break;
                                }
                            }

                            if (ch == -1)
                                throw new InvalidDataException("Huffman");
                            else if (ch == 256)
                                throw new InvalidDataException("Huffman");

                            sb.Write((char)ch);

                            lastDecodedBits += decodedBits;
                            i += lastDecodedBits / 8;

                            lastDecodedBits %= 8;
                        }
                        return sb.ToString();
                    }
                    finally
                    {
                        disposable.Dispose();
                    }
                }
                private static string ReadLiteral(ReadOnlySpan<byte> headersBytes, ref int offset)
                {
                    var @byte = headersBytes[offset++];
                    if ((@byte & 0b1000_0000) == 0b1000_0000)//huffman
                    {
                        var prefix = @byte & 0b0111_1111;
                        var length = prefix == 0b0111_1111 ? ReadHpack(headersBytes, prefix, ref offset) : prefix;

                        var huffmanBytes = headersBytes.Slice(offset, length);
                        offset += length;
                        return ReadHuffman(huffmanBytes);
                    }
                    else
                    {
                        var prefix = @byte & 0b0111_1111;
                        var length = prefix == 0b0111_1111 ? ReadHpack(headersBytes, prefix, ref offset) : prefix;

                        var literalBytes = headersBytes.Slice(offset, length);
                        offset += length;
                        return literalBytes.ToByteString();
                    }
                }
                public bool TryEnqueue(RequestTask requestTask)
                {
                    Debug.Assert(requestTask != null);
                    lock (this)
                    {
                        if (_closeWaiter != null)
                            return false;

                        if (_activeStreams >= _maxStreams)
                            return false;

                        Enqueue(requestTask);
                        return true;
                    }
                }
                public void Active(RequestTask requestTask)
                {
                    Debug.WriteLine("开始激活");
                    Debug.Assert(requestTask != null);
                    Debug.Assert(Monitor.IsEntered(_client));
                    Debug.Assert(Monitor.IsEntered(this));
                    if (_connection == null)
                    {
                        _connection = _factory();
                        _factory = null;
                    }
                    Debug.Assert(!_active);
                    Debug.Assert(_activeStreams == 0);
                    Debug.Assert(_streams.Count == 0);
                    Debug.Assert(_sendQueue.Count == 0);
                    _sendQueue.Enqueue((() => Write(_StartupBytes), null));
                    Enqueue(requestTask);
                    ThreadPool.QueueUserWorkItem(async (_) => {
                        try
                        {
                            await _connection.OpenAsync();
                            Debug.WriteLine("连接打开");
                            var writeTask = new Task<Task>(() => WriteAsync());
                            var readTask = new Task<Task>(() => ReadAsync());
                            _writeTask = writeTask.Unwrap();
                            _readTask = readTask.Unwrap();
                            writeTask.Start();
                            readTask.Start();
                        }
                        catch (Exception ex)
                        {
                            Close(-1, ex);
                        }
                    });
                }
            }
            public override Task<HttpResponse> SendAsync(HttpRequest request)
            {
                if (request == null)
                    throw new ArgumentNullException(nameof(request));

                var requestTask = new RequestTask()
                {
                    Status = RequestTask.WaitingToRun,
                    Request = request,
                    ResponseTcs = new TaskCompletionSource<HttpResponse>(TaskCreationOptions.RunContinuationsAsynchronously)
                };
                request.RegisterForDispose(requestTask);

                lock (this) //_client
                {
                    Connection connection = null;
                    lock (_requestQueue)
                    {
                        if (_requestQueue.Count > 0)
                        {
                            _requestQueue.Enqueue(requestTask);
                            return requestTask.ResponseTcs.Task;
                        }
                        _idle.TryDequeue(out connection);
                    }
                    while (connection != null)
                    {
                        if (connection.TryEnqueue(requestTask))
                        {
                            requestTask.Run();
                            return requestTask.ResponseTcs.Task;
                        }
                        lock (_requestQueue)
                        {
                            Debug.Assert(_requestQueue.Count == 0);
                            _idle.TryDequeue(out connection);
                        }
                    }
                    if (_activing != null)
                    {
                        lock (_requestQueue)
                        {
                            _requestQueue.Enqueue(requestTask);
                            return requestTask.ResponseTcs.Task;
                        }
                    }
                    if (_active != null)
                    {
                        var active = _active;
                        lock (active) 
                        {
                            _activing = active;
                            _active = active.Next;
                            requestTask.Run();
                            active.Active(requestTask);
                            return requestTask.ResponseTcs.Task;
                        }
                    }

                    lock (_requestQueue)
                    {
                        _requestQueue.Enqueue(requestTask);
                        return requestTask.ResponseTcs.Task;
                    }
                }
            }
        }
        private class Http2Connection : ClientConnection//NotSupportedHttp2Exception
        {
            public Http2Connection(ClientConnection connection)
            {
                _connection = connection;
            }

            private ClientConnection _connection;
            public override PropertyCollection<ClientConnection> Properties => _connection.Properties;
            public override bool Connected => _connection.Connected;
            public override ISecurity Security => _connection.Security;
            public override EndPoint LocalEndPoint => _connection.LocalEndPoint;
            public override EndPoint RemoteEndPoint => _connection.RemoteEndPoint;
            public override void Open()
            {
                _connection.Open();
                if (_connection.Security.ApplicationProtocol != SslApplicationProtocol.Http2)
                {
                    _connection.Close();
                    throw new NotSupportedException("h2");
                    //var alpn = _connection.Security.ApplicationProtocol.ToString();
                    //var ex = new NotSupportedException($"h2=>ALPN:{alpn}");
                    //ex.Data.Add("ALPN", alpn);
                    //throw ex;
                }
            }
            public override async Task OpenAsync()
            {
                await _connection.OpenAsync();
                if (_connection.Security.ApplicationProtocol != SslApplicationProtocol.Http2)
                {
                    _connection.Close();
                    throw new NotSupportedException("h2");
                }
            }
            public override int Receive(Span<byte> buffer) => _connection.Receive(buffer);
            public override int Receive(byte[] buffer, int offset, int count) => _connection.Receive(buffer, offset, count);
            public override ValueTask<int> ReceiveAsync(Memory<byte> buffer) => _connection.ReceiveAsync(buffer);
            public override ValueTask<int> ReceiveAsync(byte[] buffer, int offset, int count) => _connection.ReceiveAsync(buffer, offset, count);
            public override void Send(ReadOnlySpan<byte> buffer) => _connection.Send(buffer);
            public override void Send(byte[] buffer, int offset, int count) => _connection.Send(buffer, offset, count);
            public override Task SendAsync(ReadOnlyMemory<byte> buffer) => _connection.SendAsync(buffer);
            public override Task SendAsync(byte[] buffer, int offset, int count) => _connection.SendAsync(buffer, offset, count);
            public override void SendFile(string fileName) => _connection.SendFile(fileName);
            public override Task SendFileAsync(string fileName) => _connection.SendFileAsync(fileName);
            public override void Close() => _connection.Close();
        }
        private class CollectionHttpClient : HttpClient
        {
            public struct Authority : IEquatable<Authority>
            {
                private string _host;
                private int _port;
                public Authority(string host, int port)
                {
                    _host = host;
                    _port = port;
                }
                public string Host => _host;
                public int Port => _port;
                public override int GetHashCode() => _host.GetHashCode(StringComparison.OrdinalIgnoreCase) ^ _port;
                public override bool Equals(object obj) => obj != null && obj is Authority && Equals((Authority)obj);
                public bool Equals(Authority other) => _port == other._port && _host.Equals(other._host, StringComparison.OrdinalIgnoreCase);
            }
            public CollectionHttpClient(int httpConenctions, int httpsConenctions, int http2Conenctions, int http2Streams)
            {
                _httpConenctions = httpConenctions;
                _httpsConenctions = httpsConenctions;
                _http2Conenctions = http2Conenctions;
                _http2Streams = http2Streams;
                _http = new Dictionary<Authority, HttpClient>();
                _https = new Dictionary<Authority, HttpClient>();
                _http2 = new Dictionary<Authority, (bool, HttpClient)>();
            }

            private int _httpConenctions;
            private int _httpsConenctions;
            private int _http2Conenctions;
            private int _http2Streams;
            private readonly object _httpSync = new object();
            private readonly object _httpsSync = new object();
            private readonly object _http2Sync = new object();
            private Dictionary<Authority, HttpClient> _http;
            private Dictionary<Authority, HttpClient> _https;
            private Dictionary<Authority, (bool, HttpClient)> _http2;//supported,client
            private async Task<HttpResponse> SendHttp2Async(Authority key, HttpRequest request)
            {
                Debug.Assert(request.Version == HttpVersion.Version20);

                if (!_http2.TryGetValue(key, out var http2))
                {
                    lock (_http2Sync)
                    {
                        if (!_http2.TryGetValue(key, out http2))
                        {
                            http2 = (true, CreateHttp2(() => new Http2Connection(ClientConnection.Create(key.Host, key.Port).UseSsl(
                                new SslClientAuthenticationOptions
                                {
                                    ApplicationProtocols = new List<SslApplicationProtocol>() { SslApplicationProtocol.Http2 },
                                    EnabledSslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12 | SslProtocols.Tls13,
                                    TargetHost = key.Host
                                })), _http2Conenctions, _http2Streams));
                            var __http2 = new Dictionary<Authority, (bool, HttpClient)>(_http2);
                            __http2.Add(key, http2);
                            _http2 = __http2;
                        }
                    }
                }
                (var supported, var client) = http2;
                if (supported)
                {
                    Debug.Assert(client != null);
                    try
                    {
                        return await client.SendAsync(request);
                    }
                    catch (NotSupportedException ex)
                    {
                        if (ex.Message != "h2")
                            throw;
                    }
                    lock (_http2Sync)
                    {
                        if (_http2.TryGetValue(key, out http2))
                        {
                            (supported, client) = http2;
                            if (supported)
                            {
                                var __http2 = new Dictionary<Authority, (bool, HttpClient)>(_http2);
                                __http2[key] = (false, null);
                                _http2 = __http2;
                            }
                        }
                    }
                }
                request.Version = HttpVersion.Version11;
                if (!_https.TryGetValue(key, out var http1))
                {
                    lock (_httpsSync)
                    {
                        if (!_https.TryGetValue(key, out http1))
                        {
                            http1 = CreateHttp(() => ClientConnection.Create(key.Host, key.Port).UseSsl(
                                new SslClientAuthenticationOptions
                                {
                                    EnabledSslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12,
                                    TargetHost = key.Host
                                }), _httpsConenctions);
                            var https = new Dictionary<Authority, HttpClient>(_https);
                            https.Add(key, http1);
                            _https = https;
                        }
                    }
                }
                return await http1.SendAsync(request);
            }
            public override Task<HttpResponse> SendAsync(HttpRequest request)
            {
                var url = request.Url;
                if (url.Scheme.EqualsIgnoreCase(Url.SchemeHttp))
                {
                    var key = new Authority(url.Host, url.Port ?? 80);
                    if (!_http.TryGetValue(key, out var client))
                    {
                        lock (_httpSync)
                        {
                            if (!_http.TryGetValue(key, out client))
                            {
                                client = CreateHttp(() => ClientConnection.Create(key.Host, key.Port), _httpConenctions);
                                var http = new Dictionary<Authority, HttpClient>(_http);
                                http.Add(key, client);
                                _http = http;
                            }
                        }
                    }
                    return client.SendAsync(request);
                }
                else if (url.Scheme.EqualsIgnoreCase(Url.SchemeHttps))
                {
                    var key = new Authority(url.Host, url.Port ?? 443);
                    if (request.Version == HttpVersion.Version20)
                        return SendHttp2Async(key, request);//Task.

                    if (!_https.TryGetValue(key, out var client))
                    {
                        lock (_httpsSync)
                        {
                            if (!_https.TryGetValue(key, out client))
                            {
                                client = CreateHttp(() => ClientConnection.Create(key.Host, key.Port).UseSsl(
                                   new SslClientAuthenticationOptions
                                   {
                                       EnabledSslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12 | SslProtocols.Tls13,
                                       TargetHost = key.Host
                                   }), _httpsConenctions);
                                var https = new Dictionary<Authority, HttpClient>(_https);
                                https.Add(key, client);
                                _https = https;
                            }
                        }
                    }
                    return client.SendAsync(request);
                }
                else
                {
                    throw new NotSupportedException(request.Url.Scheme);
                }
            }
        }
        private class ProxyConnection : ClientConnection
        {
            public ProxyConnection(ClientConnection connection, string host, int port)
            {
                _connection = connection;
                _host = host;
                _port = port;
            }

            private ClientConnection _connection;
            private string _host;
            private int _port;
            public override PropertyCollection<ClientConnection> Properties => _connection.Properties;
            public override bool Connected => _connection.Connected;
            public override ISecurity Security => _connection.Security;
            public override EndPoint LocalEndPoint => _connection.LocalEndPoint;
            public override EndPoint RemoteEndPoint => _connection.RemoteEndPoint;
            public override void Open()
            {
                var request = new HttpRequest();
                request.Method = HttpMethod.Connect;
                request.Url.Host = _host;
                request.Url.Port = _port;
                request.Version = HttpVersion.Version11;
                var response = default(HttpResponse);
                _connection.Open();
                try
                {
                    response = _connection.SendAsync(request).Result;
                    if (response.StatusCode == 200)
                    {
                        response.Content.DrainAsync().Wait();
                    }
                    else
                    {
                        throw new NotSupportedException($"{response.StatusCode}:{response.ReasonPhrase}");
                    }
                }
                finally
                {
                    request.Dispose();
                    response.Dispose();
                }
            }
            public override async Task OpenAsync()
            {
                var request = new HttpRequest();
                request.Method = HttpMethod.Connect;
                request.Url.Host = _host;
                request.Url.Port = _port;
                request.Version = HttpVersion.Version11;
                //Headers
                var response = default(HttpResponse);
                await _connection.OpenAsync();
                try
                {
                    response = await _connection.SendAsync(request);
                    if (response.StatusCode == 200)
                    {
                        await response.Content.DrainAsync();
                    }
                    else
                    {
                        throw new NotSupportedException($"{response.StatusCode}:{response.ReasonPhrase}");
                    }
                }
                finally
                {
                    request.Dispose();
                    response.Dispose();
                }
            }
            public override int Receive(Span<byte> buffer) => _connection.Receive(buffer);
            public override int Receive(byte[] buffer, int offset, int count) => _connection.Receive(buffer, offset, count);
            public override ValueTask<int> ReceiveAsync(Memory<byte> buffer) => _connection.ReceiveAsync(buffer);
            public override ValueTask<int> ReceiveAsync(byte[] buffer, int offset, int count) => _connection.ReceiveAsync(buffer, offset, count);
            public override void Send(ReadOnlySpan<byte> buffer) => _connection.Send(buffer);
            public override void Send(byte[] buffer, int offset, int count) => _connection.Send(buffer, offset, count);
            public override Task SendAsync(ReadOnlyMemory<byte> buffer) => _connection.SendAsync(buffer);
            public override Task SendAsync(byte[] buffer, int offset, int count) => _connection.SendAsync(buffer, offset, count);
            public override void SendFile(string fileName) => _connection.SendFile(fileName);
            public override Task SendFileAsync(string fileName) => _connection.SendFileAsync(fileName);
            public override void Close() => _connection.Close();
        }
        private class ProxyClient : HttpClient
        {
            public struct Authority : IEquatable<Authority>
            {
                private string _host;
                private int _port;
                public Authority(string host, int port)
                {
                    _host = host;
                    _port = port;
                }
                public string Host => _host;
                public int Port => _port;
                public override int GetHashCode() => _host.GetHashCode(StringComparison.OrdinalIgnoreCase) ^ _port;
                public override bool Equals(object obj) => obj != null && obj is Authority && Equals((Authority)obj);
                public bool Equals(Authority other) => _port == other._port && _host.Equals(other._host, StringComparison.OrdinalIgnoreCase);
            }
            public ProxyClient(string proxyHost, int proxyPort, int maxConenctions)
            {
                _proxyHost = proxyHost;
                _proxyPort = proxyPort;
                _maxConenctions = maxConenctions;
                _proxy = new Dictionary<Authority, HttpClient>();
            }

            private string _proxyHost;
            private int _proxyPort;
            private int _maxConenctions;
            private readonly object _proxySync = new object();
            private Dictionary<Authority, HttpClient> _proxy;
            public override Task<HttpResponse> SendAsync(HttpRequest request) 
            {
                var url = request.Url;
                var key = new Authority(url.Host, url.Port ?? 443);
                if (!_proxy.TryGetValue(key, out var client))
                {
                    lock (_proxySync)
                    {
                        if (!_proxy.TryGetValue(key, out client))
                        {
                            client = CreateHttpProxy(() =>
                                new ProxyConnection(ClientConnection.Create(_proxyHost, _proxyPort), key.Host, key.Port)
                                .UseSsl(
                                new SslClientAuthenticationOptions
                                {
                                    EnabledSslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12 | SslProtocols.Tls13,
                                    TargetHost = key.Host
                                }), _maxConenctions);

                            var proxy = new Dictionary<Authority, HttpClient>(_proxy);
                            proxy.Add(key, client);
                            _proxy = proxy;
                        }
                    }
                }
                return client.SendAsync(request);
            }
        }
        #endregion

        //public static HttpClient CreateHttp2(string host, int port,  SslClientAuthenticationOptions options, int maxStreams)
        //{
        //    if (host == null)
        //        throw new ArgumentNullException(nameof(host));
        //    if (port > IPEndPoint.MaxPort || port < IPEndPoint.MinPort)
        //        throw new ArgumentOutOfRangeException(nameof(port));
        //    if (options == null)
        //        throw new ArgumentNullException(nameof(options));
        //    if (maxStreams <= 0)
        //        throw new ArgumentOutOfRangeException(nameof(maxStreams));

        //    return CreateHttp2(new Http2ClientConnection(host, port,
        //        new SslClientAuthenticationOptions()
        //        {
        //            AllowRenegotiation = options.AllowRenegotiation,
        //            ApplicationProtocols = new List<SslApplicationProtocol>() { SslApplicationProtocol.Http2 },//ignore options.ApplicationProtocols
        //            CertificateRevocationCheckMode = options.CertificateRevocationCheckMode,
        //            CipherSuitesPolicy = options.CipherSuitesPolicy,
        //            ClientCertificates = options.ClientCertificates,
        //            EnabledSslProtocols = options.EnabledSslProtocols == SslProtocols.None ? SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12 : options.EnabledSslProtocols,
        //            EncryptionPolicy = options.EncryptionPolicy,
        //            LocalCertificateSelectionCallback = options.LocalCertificateSelectionCallback,
        //            RemoteCertificateValidationCallback = options.RemoteCertificateValidationCallback,
        //            TargetHost = string.IsNullOrEmpty(options.TargetHost) ? host : options.TargetHost
        //        }), maxStreams);
        //}
        //太长了 不要了
        //public static HttpClient Create(Func<string, int, ClientConnection> httpFactory, int httpConenctions, Func<string, int, ClientConnection> httpsFactory, int httpsConenctions, Func<string, int, ClientConnection> http2Factory, int http2Streams)
        //{
        //    //这个不要代码太垃圾
        //    return null;
        //}


        //#region SslClientAuthenticationOptions Register
        //private static SslClientAuthenticationOptions _Options;
        //public static void SslClientAuthenticationOptions(SslClientAuthenticationOptions options)
        //{
        //    _Options = options;
        //}
        //private static SslClientAuthenticationOptions CreateHttpsOptions(string host)
        //{
        //    var options = _Options;
        //    return options == null ?
        //         new SslClientAuthenticationOptions
        //         {
        //             EnabledSslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12,
        //             TargetHost = host
        //         } :
        //         new SslClientAuthenticationOptions
        //         {
        //             AllowRenegotiation = options.AllowRenegotiation,
        //             CertificateRevocationCheckMode = options.CertificateRevocationCheckMode,
        //             CipherSuitesPolicy = options.CipherSuitesPolicy,
        //             ClientCertificates = options.ClientCertificates,
        //             EnabledSslProtocols = options.EnabledSslProtocols == SslProtocols.None ? SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12 : options.EnabledSslProtocols,
        //             EncryptionPolicy = options.EncryptionPolicy,
        //             LocalCertificateSelectionCallback = options.LocalCertificateSelectionCallback,
        //             RemoteCertificateValidationCallback = options.RemoteCertificateValidationCallback,
        //             TargetHost = host
        //         };
        //}
        //private static SslClientAuthenticationOptions CreateHttp2Options(string host)
        //{
        //    var options = _Options;
        //    return options == null ?
        //         new SslClientAuthenticationOptions
        //         {
        //             ApplicationProtocols = new List<SslApplicationProtocol>() { SslApplicationProtocol.Http2 },
        //             EnabledSslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12,
        //             TargetHost = host
        //         } :
        //         new SslClientAuthenticationOptions
        //         {
        //             ApplicationProtocols = new List<SslApplicationProtocol>() { SslApplicationProtocol.Http2 },
        //             CertificateRevocationCheckMode = options.CertificateRevocationCheckMode,
        //             CipherSuitesPolicy = options.CipherSuitesPolicy,
        //             ClientCertificates = options.ClientCertificates,
        //             EnabledSslProtocols = options.EnabledSslProtocols == SslProtocols.None ? SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12 : options.EnabledSslProtocols,
        //             EncryptionPolicy = options.EncryptionPolicy,
        //             LocalCertificateSelectionCallback = options.LocalCertificateSelectionCallback,
        //             RemoteCertificateValidationCallback = options.RemoteCertificateValidationCallback,
        //             TargetHost = host
        //         };
        //}
        //#endregion
    }
}