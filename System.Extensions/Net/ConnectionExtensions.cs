
namespace System.Extensions.Net
{
    using System.IO;
    using System.Net;
    using System.Net.Security;
    using System.Net.Sockets;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;
    public static class ConnectionExtensions
    {
        static ConnectionExtensions()
        {
            //TODO .NET5 GC.AllocateUninitializedArray
            _Provider = Provider<Memory<byte>>.CreateFromProcessor(() => new UnmanagedMemory<byte>(8192).Memory, 1024);//reset?
            _UseSsl = (client, options) => new SslClient(client, options);
            _UseSslAsync = async (connection, options) =>
            {
                var ssl = new SslConnection(connection);
                try
                {
                    await ssl.AuthenticateAsync(options);
                }
                catch
                {
                    ssl.Close();
                    throw;
                }
                return ssl;
            };
        }

        private static Provider<Memory<byte>> _Provider;
        public static void Register(Provider<Memory<byte>> provider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            _Provider = provider;
        }
        public static void Register(out Provider<Memory<byte>> provider)
        {
            provider = _Provider;
        }
        public static Memory<byte> GetBytes(out IDisposable disposable)
        {
            if (_Provider.TryGetValue(out var bytes, out disposable))
            {
                //Debug.Assert(bytes.Length >= 32);
                return bytes;
            }
            var unmanagedBytes = new UnmanagedMemory<byte>(8192);
            disposable = unmanagedBytes;
            return unmanagedBytes.Memory;
        }

        private static Property<IConnection> _Items = new Property<IConnection>("ConnectionExtensions.Items");
        public static IDictionary<string, object> Items(this IConnection @this)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            var items = (IDictionary<string, object>)@this.Properties[_Items];
            if (items == null)
            {
                items = new Dictionary<string, object>();
                @this.Properties[_Items] = items;
            }
            return items;
        }
        public static void Items(this IConnection @this, IDictionary<string, object> items)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            @this.Properties[_Items] = items;
        }
        public static void Items(this IConnection @this, out IDictionary<string, object> items)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            items = (IDictionary<string, object>)@this.Properties[_Items];
        }

        private static Func<ClientConnection, SslClientAuthenticationOptions, ClientConnection> _UseSsl;
        public static void UseSsl(Action<ClientConnection, SslClientAuthenticationOptions> useSsl)
        {
            if (useSsl == null)
                throw new ArgumentNullException(nameof(useSsl));

            var _useSsl = _UseSsl;
            _UseSsl = (client, options) => {
                useSsl(client, options);
                return _useSsl(client, options);
            };
        }
        public static void UseSsl(Func<ClientConnection, SslClientAuthenticationOptions, ClientConnection> useSsl)
        {
            if (useSsl == null)
                useSsl = (client, options) => new SslClient(client, options);

            _UseSsl = useSsl;
        }
        public static void UseSsl(out Func<ClientConnection, SslClientAuthenticationOptions, ClientConnection> useSsl)
        {
            useSsl = _UseSsl;
        }
        private class SslClient : ClientConnection, ISecurity //IDisposable
        {
            public SslClient(ClientConnection client, SslClientAuthenticationOptions options)
            {
                _client = new ClientStream(client);
                _options = options;
            }
            private class ClientStream : Stream
            {
                private ClientConnection _client;
                public ClientStream(ClientConnection client)
                {
                    _client = client;
                }
                public PropertyCollection<ClientConnection> Properties => _client.Properties;
                public bool Connected => _client.Connected;
                public EndPoint LocalEndPoint => _client.LocalEndPoint;
                public EndPoint RemoteEndPoint => _client.RemoteEndPoint;
                public void Open() => _client.Open();
                public Task OpenAsync() => _client.OpenAsync();
                public override bool CanRead => true;
                public override bool CanWrite => true;
                public override int Read(Span<byte> buffer)
                {
                    return _client.Receive(buffer);
                }
                public override int Read(byte[] buffer, int offset, int count)
                {
                    return _client.Receive(buffer, offset, count);
                }
                public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
                {
                    return _client.ReceiveAsync(buffer);
                }
                public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
                {
                    return _client.ReceiveAsync(buffer, offset, count).AsTask();
                }
                public override void Write(ReadOnlySpan<byte> buffer)
                {
                    _client.Send(buffer);
                }
                public override void Write(byte[] buffer, int offset, int count)
                {
                    _client.Send(buffer, offset, count);
                }
                public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
                {
                    return new ValueTask(_client.SendAsync(buffer));
                }
                public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
                {
                    return _client.SendAsync(buffer, offset, count);
                }
                public override void Close() => _client.Close();
                public override bool CanSeek => false;
                public override long Length => throw new NotSupportedException(nameof(Length));
                public override long Position { get => throw new NotSupportedException(nameof(Position)); set => throw new NotSupportedException(nameof(Position)); }
                public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(nameof(Seek));
                public override void SetLength(long value) => throw new NotSupportedException(nameof(SetLength));
                public override void Flush()
                {

                }
            }

            private ClientStream _client;
            private SslStream _sslStream;
            private SslClientAuthenticationOptions _options;
            public override PropertyCollection<ClientConnection> Properties => _client.Properties;
            public override bool Connected => _client.Connected;
            public override ISecurity Security => this;
            public override EndPoint LocalEndPoint => _client.LocalEndPoint;
            public override EndPoint RemoteEndPoint => _client.RemoteEndPoint;
            public override void Open()
            {
                if (_sslStream != null)
                    _sslStream.Close();

                _client.Open();
                _sslStream = new SslStream(_client);
                _sslStream.AuthenticateAsClientAsync(_options, CancellationToken.None).Wait();
            }
            public override async Task OpenAsync()
            {
                if (_sslStream != null)
                    _sslStream.Close();

                await _client.OpenAsync();
                _sslStream = new SslStream(_client);
                await _sslStream.AuthenticateAsClientAsync(_options, CancellationToken.None);
            }
            public override int Receive(Span<byte> buffer)
            {
                var sslStream = _sslStream;
                if (sslStream == null)
                    throw new InvalidOperationException($"{nameof(SslClient)}.{nameof(Receive)}");

                return sslStream.Read(buffer);
            }
            public override int Receive(byte[] buffer, int offset, int count)
            {
                var sslStream = _sslStream;
                if (sslStream == null)
                    throw new InvalidOperationException($"{nameof(SslClient)}.{nameof(Receive)}");

                return sslStream.Read(buffer, offset, count);
            }
            public override ValueTask<int> ReceiveAsync(Memory<byte> buffer)
            {
                var sslStream = _sslStream;
                if (sslStream == null)
                    throw new InvalidOperationException($"{nameof(SslClient)}.{nameof(ReceiveAsync)}");

                return sslStream.ReadAsync(buffer);
            }
            public override ValueTask<int> ReceiveAsync(byte[] buffer, int offset, int count)
            {
                var sslStream = _sslStream;
                if (sslStream == null)
                    throw new InvalidOperationException($"{nameof(SslClient)}.{nameof(ReceiveAsync)}");

                return new ValueTask<int>(sslStream.ReadAsync(buffer, offset, count));
            }
            public override void Send(ReadOnlySpan<byte> buffer)
            {
                var sslStream = _sslStream;
                if (sslStream == null)
                    throw new InvalidOperationException($"{nameof(SslClient)}.{nameof(Send)}");

                sslStream.Write(buffer);
            }
            public override void Send(byte[] buffer, int offset, int count)
            {
                var sslStream = _sslStream;
                if (sslStream == null)
                    throw new InvalidOperationException($"{nameof(SslClient)}.{nameof(Send)}");

                sslStream.Write(buffer, offset, count);
            }
            public override Task SendAsync(ReadOnlyMemory<byte> buffer)
            {
                var sslStream = _sslStream;
                if (sslStream == null)
                    throw new InvalidOperationException($"{nameof(SslClient)}.{nameof(SendAsync)}");

                return sslStream.WriteAsync(buffer).AsTask();
            }
            public override Task SendAsync(byte[] buffer, int offset, int count)
            {
                var sslStream = _sslStream;
                if (sslStream == null)
                    throw new InvalidOperationException($"{nameof(SslClient)}.{nameof(SendAsync)}");

                return sslStream.WriteAsync(buffer, offset, count);
            }
            public override void SendFile(string fileName)
            {
                var sslStream = _sslStream;
                if (sslStream == null)
                    throw new InvalidOperationException($"{nameof(SslClient)}.{nameof(SendFile)}");

                var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.None);
                var bytes = GetBytes(out var disposable);
                try
                {
                    var span = bytes.Span;
                    for (; ; )
                    {
                        var result = fs.Read(span);
                        if (result == 0)
                            return;

                        sslStream.Write(span.Slice(0, result));
                    }
                }
                finally
                {
                    fs.Close();
                    disposable.Dispose();
                }
            }
            public override async Task SendFileAsync(string fileName)
            {
                var sslStream = _sslStream;
                if (sslStream == null)
                    throw new InvalidOperationException($"{nameof(SslClient)}.{nameof(SendFileAsync)}");

                var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.Asynchronous);
                var bytes = GetBytes(out var disposable);
                try
                {
                    for (; ; )
                    {
                        var result = await fs.ReadAsync(bytes);
                        if (result == 0)
                            return;

                        await sslStream.WriteAsync(bytes.Slice(0, result));
                    }
                }
                finally
                {
                    fs.Close();
                    disposable.Dispose();
                }
            }
            public override void Close()
            {
                if (_sslStream != null) 
                {
                    var sslStream = Interlocked.Exchange(ref _sslStream, null);
                    if (sslStream != null) 
                    {
                        sslStream.Close();
                    }
                }
            }
            #region Security
            public X509Certificate LocalCertificate => _sslStream.LocalCertificate;
            public X509Certificate RemoteCertificate => _sslStream.RemoteCertificate;
            public SslApplicationProtocol ApplicationProtocol => _sslStream.NegotiatedApplicationProtocol;
            public SslProtocols Protocol => _sslStream.SslProtocol;
            public CipherAlgorithmType CipherAlgorithm => _sslStream.CipherAlgorithm;
            public int CipherStrength => _sslStream.CipherStrength;
            public HashAlgorithmType HashAlgorithm => _sslStream.HashAlgorithm;
            public int HashStrength => _sslStream.HashStrength;
            public ExchangeAlgorithmType KeyExchangeAlgorithm => _sslStream.KeyExchangeAlgorithm;
            public int KeyExchangeStrength => _sslStream.KeyExchangeStrength;
            #endregion
        }
        public static ClientConnection UseSsl(this ClientConnection @this, SslClientAuthenticationOptions options)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            return _UseSsl(@this, options);
        }

        private static Func<IConnection, SslServerAuthenticationOptions, Task<IConnection>> _UseSslAsync;
        public static void UseSslAsync(Action<IConnection, SslServerAuthenticationOptions> useSslAsync)
        {
            if (useSslAsync == null)
                throw new ArgumentNullException(nameof(useSslAsync));

            var _useSslAsync = _UseSslAsync;
            _UseSslAsync = (connection, options) => {
                useSslAsync(connection, options);
                return _useSslAsync(connection, options);
            };
        }
        public static void UseSslAsync(Func<IConnection, SslServerAuthenticationOptions, Task<IConnection>> useSslAsync)
        {
            if (useSslAsync == null) 
            {
                useSslAsync = async (connection, options) =>
                {
                    var ssl = new SslConnection(connection);
                    try
                    {
                        await ssl.AuthenticateAsync(options);
                    }
                    catch
                    {
                        ssl.Close();
                        throw;
                    }
                    return ssl;
                };
            }
            _UseSslAsync = useSslAsync;
        }
        public static void UseSslAsync(out Func<IConnection, SslServerAuthenticationOptions, Task<IConnection>> useSslAsync)
        {
            useSslAsync = _UseSslAsync;
        }
        private class SslConnection : Stream, IConnection, ISecurity
        {
            private SslStream _sslStream;
            private IConnection _connection;
            public SslConnection(IConnection connection)
            {
                _connection = connection;
                _sslStream = new SslStream(this);
            }
            public PropertyCollection<IConnection> Properties => _connection.Properties;
            public bool Connected => _connection.Connected;
            public EndPoint LocalEndPoint => _connection.LocalEndPoint;
            public EndPoint RemoteEndPoint => _connection.RemoteEndPoint;

            #region Stream
            public override bool CanRead => true;
            public override bool CanWrite => true;
            public override int Read(Span<byte> buffer)
            {
                return _connection.Receive(buffer);
            }
            public override int Read(byte[] buffer, int offset, int count)
            {
                return _connection.Receive(buffer, offset, count);
            }
            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                return _connection.ReceiveAsync(buffer);
            }
            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return _connection.ReceiveAsync(buffer, offset, count).AsTask();
            }
            public override void Write(ReadOnlySpan<byte> buffer)
            {
                _connection.Send(buffer);
            }
            public override void Write(byte[] buffer, int offset, int count)
            {
                _connection.Send(buffer, offset, count);
            }
            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            {
                return new ValueTask(_connection.SendAsync(buffer));
            }
            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return _connection.SendAsync(buffer, offset, count);
            }
            public override void Close() => _connection.Close();
            public override bool CanSeek => false;
            public override long Length => throw new NotSupportedException(nameof(Length));
            public override long Position { get => throw new NotSupportedException(nameof(Position)); set => throw new NotSupportedException(nameof(Position)); }
            public override long Seek(long offset, SeekOrigin origin)=> throw new NotSupportedException(nameof(Seek));
            public override void SetLength(long value) => throw new NotSupportedException(nameof(SetLength));
            public override void Flush()
            {

            }
            #endregion
            public Task AuthenticateAsync(SslServerAuthenticationOptions sslServerAuthenticationOptions)
            {
                return _sslStream.AuthenticateAsServerAsync(sslServerAuthenticationOptions, CancellationToken.None);
            }
            public ISecurity Security => this;
            #region Security
            public X509Certificate LocalCertificate => _sslStream.LocalCertificate;
            public X509Certificate RemoteCertificate => _sslStream.RemoteCertificate;
            public SslApplicationProtocol ApplicationProtocol => _sslStream.NegotiatedApplicationProtocol;
            public SslProtocols Protocol => _sslStream.SslProtocol;
            public CipherAlgorithmType CipherAlgorithm => _sslStream.CipherAlgorithm;
            public int CipherStrength => _sslStream.CipherStrength;
            public HashAlgorithmType HashAlgorithm => _sslStream.HashAlgorithm;
            public int HashStrength => _sslStream.HashStrength;
            public ExchangeAlgorithmType KeyExchangeAlgorithm => _sslStream.KeyExchangeAlgorithm;
            public int KeyExchangeStrength => _sslStream.KeyExchangeStrength;
            #endregion
            public int Receive(Span<byte> buffer)
            {
                var sslStream = _sslStream;
                if (sslStream == null)
                    throw new InvalidOperationException($"{nameof(SslConnection)}.{nameof(Receive)}");

                return sslStream.Read(buffer);
            }
            public int Receive(byte[] buffer, int offset, int count)
            {
                var sslStream = _sslStream;
                if (sslStream == null)
                    throw new InvalidOperationException($"{nameof(SslConnection)}.{nameof(Receive)}");

                return sslStream.Read(buffer, offset, count);
            }
            public ValueTask<int> ReceiveAsync(Memory<byte> buffer)
            {
                var sslStream = _sslStream;
                if (sslStream == null)
                    throw new InvalidOperationException($"{nameof(SslConnection)}.{nameof(ReceiveAsync)}");

                return sslStream.ReadAsync(buffer);
            }
            public ValueTask<int> ReceiveAsync(byte[] buffer, int offset, int count)
            {
                var sslStream = _sslStream;
                if (sslStream == null)
                    throw new InvalidOperationException($"{nameof(SslConnection)}.{nameof(ReceiveAsync)}");

                return sslStream.ReadAsync(buffer.AsMemory(offset, count));
            }
            public void Send(ReadOnlySpan<byte> buffer)
            {
                var sslStream = _sslStream;
                if (sslStream == null)
                    throw new InvalidOperationException($"{nameof(SslConnection)}.{nameof(Send)}");

                sslStream.Write(buffer);
            }
            public void Send(byte[] buffer, int offset, int count)
            {
                var sslStream = _sslStream;
                if (sslStream == null)
                    throw new InvalidOperationException($"{nameof(SslConnection)}.{nameof(Send)}");

                sslStream.Write(buffer, offset, count);
            }
            public Task SendAsync(ReadOnlyMemory<byte> buffer)
            {
                var sslStream = _sslStream;
                if (sslStream == null)
                    throw new InvalidOperationException($"{nameof(SslConnection)}.{nameof(SendAsync)}");

                return sslStream.WriteAsync(buffer).AsTask();
            }
            public Task SendAsync(byte[] buffer, int offset, int count)
            {
                var sslStream = _sslStream;
                if (sslStream == null)
                    throw new InvalidOperationException($"{nameof(SslConnection)}.{nameof(SendAsync)}");

                return sslStream.ReadAsync(buffer, offset, count);
            }
            public void SendFile(string fileName)
            {
                var sslStream = _sslStream;
                if (sslStream == null)
                    throw new InvalidOperationException($"{nameof(SslConnection)}.{nameof(SendFile)}");

                var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.None);
                var bytes = GetBytes(out var disposable);
                try
                {
                    var span = bytes.Span;
                    for (; ; )
                    {
                        var result = fs.Read(span);
                        if (result == 0)
                            return;

                        sslStream.Write(span.Slice(0, result));
                    }
                }
                finally
                {
                    fs.Close();
                    disposable.Dispose();
                }
            }
            public async Task SendFileAsync(string fileName)
            {
                var sslStream = _sslStream;
                if (sslStream == null)
                    throw new InvalidOperationException($"{nameof(SslConnection)}.{nameof(SendFileAsync)}");

                var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.Asynchronous);
                var bytes = GetBytes(out var disposable);
                try
                {
                    for (; ; )
                    {
                        var result = await fs.ReadAsync(bytes);
                        if (result == 0)
                            return;

                        await sslStream.WriteAsync(bytes.Slice(0, result));
                    }
                }
                finally
                {
                    fs.Close();
                    disposable.Dispose();
                }
            }
            void IConnection.Close()
            {
                if (_sslStream != null)
                {
                    var sslStream = Interlocked.Exchange(ref _sslStream, null);
                    if (sslStream != null)
                    {
                        sslStream.Close();
                    }
                }
            }
        }
        public static Task<IConnection> UseSslAsync(this IConnection @this, SslServerAuthenticationOptions options)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            return _UseSslAsync(@this, options);
        }
        public static IConnectionService Use(this IConnectionService @this, Func<IConnection, IConnectionHandler, Task> module)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (module == null)
                throw new ArgumentNullException(nameof(module));

            @this.Handler = new ConnectionModule(@this.Handler, module);
            return @this;
        }
        public static Stream AsStream(this ClientConnection @this)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            return new ClientStream(@this);
        }
        public static Stream AsStream(this IConnection @this)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            return new ConnectionStream(@this);
        }

        //private static Property<ClientConnection> _SocketProperty = new Property<ClientConnection>("ConnectionExtensions.ClientConnection.Socket");
        //public static Socket Socket(this ClientConnection @this)
        //{
        //    if (@this == null)
        //        return null;
        //    return (Socket)@this.Properties[_SocketProperty];
        //}
        //public static void Socket(this IConnection @this, Socket socket)
        //{
        //    if (@this == null)
        //        throw new ArgumentNullException(nameof(@this));

        //    @this.Properties[_SocketProperty] = socket;
        //}

        private static Property<IConnection> _Socket = new Property<IConnection>("ConnectionExtensions.Socket");
        public static Socket Socket(this IConnection @this) 
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            return (Socket)@this.Properties[_Socket];
        }
        public static void Socket(this IConnection @this,Socket socket) 
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            @this.Properties[_Socket] = socket;
        }
        private class ClientStream : Stream
        {
            private ClientConnection _connection;
            public ClientStream(ClientConnection client)
            {
                _connection = client;
            }
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotSupportedException(nameof(Length));
            public override long Position { get => throw new NotSupportedException(nameof(Position)); set => throw new NotSupportedException(nameof(Position)); }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(nameof(Seek));
            public override void SetLength(long value) => throw new NotSupportedException(nameof(SetLength));
            public override int Read(Span<byte> buffer)
            {
                return _connection.Receive(buffer);
            }
            public override int Read(byte[] buffer, int offset, int count)
            {
                return _connection.Receive(buffer, offset, count);
            }
            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                return _connection.ReceiveAsync(buffer);
            }
            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return _connection.ReceiveAsync(buffer, offset, count).AsTask();
            }
            public override void Write(ReadOnlySpan<byte> buffer)
            {
                _connection.Send(buffer);
            }
            public override void Write(byte[] buffer, int offset, int count)
            {
                _connection.Send(buffer, offset, count);
            }
            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            {
                return new ValueTask(_connection.SendAsync(buffer));
            }
            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return _connection.SendAsync(buffer, offset, count);
            }
            public override void Flush()
            {

            }
        }
        private class ConnectionStream : Stream
        {
            private IConnection _connection;
            public ConnectionStream(IConnection connection)
            {
                _connection = connection;
            }
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotSupportedException(nameof(Length));
            public override long Position { get => throw new NotSupportedException(nameof(Position)); set => throw new NotSupportedException(nameof(Position)); }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(nameof(Seek));
            public override void SetLength(long value) => throw new NotSupportedException(nameof(SetLength));
            public override int Read(Span<byte> buffer)
            {
                return _connection.Receive(buffer);
            }
            public override int Read(byte[] buffer, int offset, int count)
            {
                return _connection.Receive(buffer, offset, count);
            }
            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                return _connection.ReceiveAsync(buffer);
            }
            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return _connection.ReceiveAsync(buffer, offset, count).AsTask();
            }
            public override void Write(ReadOnlySpan<byte> buffer)
            {
                _connection.Send(buffer);
            }
            public override void Write(byte[] buffer, int offset, int count)
            {
                _connection.Send(buffer, offset, count);
            }
            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            {
                return new ValueTask(_connection.SendAsync(buffer));
            }
            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return _connection.SendAsync(buffer, offset, count);
            }
            public override void Flush()
            {

            }
        }
        private class ConnectionModule : IConnectionHandler
        {
            private IConnectionHandler _handler;
            private Func<IConnection, IConnectionHandler, Task> _module;
            public ConnectionModule(IConnectionHandler handler, Func<IConnection, IConnectionHandler, Task> module)
            {
                _handler = handler;
                _module = module;
            }
            public Task HandleAsync(IConnection connection)
            {
                return _module(connection, _handler);
            }
        }

        #region Func<IConnection, Task> 意义不大
        //public static TcpServer Use(this TcpServer @this, Func<IConnection, Task> handler)
        //{
        //    if (@this == null)
        //        throw new ArgumentNullException(nameof(@this));
        //    if (handler == null)
        //        throw new ArgumentNullException(nameof(handler));

        //    @this.Handler = new ConnectionHandler(handler);
        //    return @this;
        //}
        //private class ConnectionHandler : IConnectionHandler
        //{
        //    private Func<IConnection, Task> _handler;
        //    public ConnectionHandler(Func<IConnection, Task> handler)
        //    {
        //        _handler = handler;
        //    }
        //    public Task HandleAsync(IConnection connection)
        //    {
        //        return _handler(connection);
        //    }
        //}
        #endregion
    }
}
