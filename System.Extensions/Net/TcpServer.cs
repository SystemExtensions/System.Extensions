
namespace System.Extensions.Net
{
    using System.Diagnostics;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Runtime.InteropServices;
    public class TcpServer
    {
        #region private
        private bool _listening;
        private Socket _socket;
        private EndPoint _localEndPoint;
        private Connection[] _connections;
        #endregion
        private class TcpConnection : IConnection
        {
            private PropertyCollection<IConnection> _properties;
            private Connection _connection;
            public TcpConnection(Connection connection)
            {
                _properties = new PropertyCollection<IConnection>();
                _connection = connection;
            }
            public PropertyCollection<IConnection> Properties => _properties;
            public bool Connected
            {
                get
                {
                    var connection = _connection;
                    if (connection == null)
                        return false;
                    return connection.Connected;
                }
            }
            public ISecurity Security => null;
            public EndPoint LocalEndPoint
            {
                get
                {
                    var connection = _connection;
                    if (connection == null)
                        return null;
                    return connection.LocalEndPoint;
                }
            }
            public EndPoint RemoteEndPoint
            {
                get
                {
                    var connection = _connection;
                    if (connection == null)
                        return null;
                    return connection.RemoteEndPoint;
                }
            }
            public int Receive(Span<byte> buffer)
            {
                var connection = _connection;
                if (connection == null)
                    throw new InvalidOperationException($"{nameof(TcpConnection)}.{nameof(Receive)}");

                return connection.Receive(buffer);
            }
            public int Receive(byte[] buffer, int offset, int count)
            {
                var connection = _connection;
                if (connection == null)
                    throw new InvalidOperationException($"{nameof(TcpConnection)}.{nameof(Receive)}");

                return connection.Receive(buffer, offset, count);
            }
            public ValueTask<int> ReceiveAsync(Memory<byte> buffer)
            {
                var connection = _connection;
                if (connection == null)
                    throw new InvalidOperationException($"{nameof(TcpConnection)}.{nameof(ReceiveAsync)}");

                return connection.ReceiveAsync(buffer);
            }
            public ValueTask<int> ReceiveAsync(byte[] buffer, int offset, int count)
            {
                var connection = _connection;
                if (connection == null)
                    throw new InvalidOperationException($"{nameof(TcpConnection)}.{nameof(ReceiveAsync)}");

                return connection.ReceiveAsync(buffer, offset, count);
            }
            public void Send(ReadOnlySpan<byte> buffer)
            {
                var connection = _connection;
                if (connection == null)
                    throw new InvalidOperationException($"{nameof(TcpConnection)}.{nameof(Send)}");

                connection.Send(buffer);
            }
            public void Send(byte[] buffer, int offset, int count)
            {
                var connection = _connection;
                if (connection == null)
                    throw new InvalidOperationException($"{nameof(TcpConnection)}.{nameof(Send)}");

                connection.Send(buffer, offset, count);
            }
            public Task SendAsync(ReadOnlyMemory<byte> buffer)
            {
                var connection = _connection;
                if (connection == null)
                    throw new InvalidOperationException($"{nameof(TcpConnection)}.{nameof(SendAsync)}");

                return connection.SendAsync(buffer);
            }
            public Task SendAsync(byte[] buffer, int offset, int count)
            {
                var connection = _connection;
                if (connection == null)
                    throw new InvalidOperationException($"{nameof(TcpConnection)}.{nameof(SendAsync)}");

                return connection.SendAsync(buffer, offset, count);
            }
            public void SendFile(string fileName)
            {
                var connection = _connection;
                if (connection == null)
                    throw new InvalidOperationException($"{nameof(TcpConnection)}.{nameof(SendFile)}");

                connection.SendFile(fileName);
            }
            public Task SendFileAsync(string fileName)
            {
                var connection = _connection;
                if (connection == null)
                    throw new InvalidOperationException($"{nameof(TcpConnection)}.{nameof(SendFileAsync)}");

                return connection.SendFileAsync(fileName);
            }
            public void Close()
            {
                if (_connection != null)
                {
                    var connection = Interlocked.Exchange(ref _connection, null);
                    if (connection != null)
                    {
                        connection.Close();
                    }
                }
            }
        }
        private class Connection : IThreadPoolWorkItem, IDisposable
        {
            public Connection(TcpServer server)
            {
                _server = server;
                _acceptArgs = new SocketAsyncEventArgs();
                _acceptArgs.Completed += AcceptCompleted;
                _receiveArgs = new SocketAsyncEventArgs();
                _receiveArgs.Completed += ReceiveCompleted;
                _sendArgs = new SocketAsyncEventArgs();
                _sendArgs.Completed += SendCompleted;
                _sendFileCallback = SendFileCompleted;
            }

            private TcpServer _server;
            private TcpConnection _connection;
            private Socket _socket;
            private SocketAsyncEventArgs _acceptArgs;
            private SocketAsyncEventArgs _receiveArgs;
            private SocketAsyncEventArgs _sendArgs;
            private AsyncCallback _sendFileCallback;
            public bool Connected => _socket.Connected;
            public EndPoint LocalEndPoint => _socket.LocalEndPoint;
            public EndPoint RemoteEndPoint => _socket.RemoteEndPoint;
            public async void Execute()
            {
                Debug.Assert(_connection != null);
                if (!_server._listening)
                {
                    _connection.Close();
                    _connection = null;
                    _socket = null;
                    return;
                }

                try
                {
                    await _server.Handler.HandleAsync(_connection);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex, "UnobservedException");
                }
                finally
                {
                    _connection.Close();
                    _connection = null;
                    _socket = null;
                    if (_server._listening)
                    {
                        try
                        {
                            Accept();
                        }
                        catch (Exception ex)
                        {
                            Trace.WriteLine(ex, "UnobservedException");
                        }
                    }
                }
            }
            public void Accept()
            {
                if (!_server._socket.AcceptAsync(_acceptArgs))
                    AcceptCompleted(_server._socket, _acceptArgs);
            }
            public void AcceptCompleted(object sender, SocketAsyncEventArgs e)
            {
                if (e.AcceptSocket != null && e.SocketError == SocketError.Success)
                {
                    Debug.Assert(_connection == null);
                    Debug.Assert(_socket == null);
                    _socket = e.AcceptSocket;
                    e.AcceptSocket = null;
                    _connection = new TcpConnection(this);
                    _connection.Socket(_socket);
                    ThreadPool.UnsafeQueueUserWorkItem(this, false);
                }
                else
                {
                    if (_server._listening)
                    {
                        e.AcceptSocket = null;
                        try
                        {
                            if (_server._socket.AcceptAsync(e))
                                return;
                        }
                        catch (Exception ex)
                        {
                            Trace.WriteLine(ex, "UnobservedException");
                        }
                        AcceptCompleted(_server._socket, _acceptArgs);
                    }
                }
            }
            public int Receive(Span<byte> buffer)
            {
                return _socket.Receive(buffer);
            }
            public int Receive(byte[] buffer, int offset, int count)
            {
                return _socket.Receive(buffer, offset, count, SocketFlags.None);
            }
            public ValueTask<int> ReceiveAsync(Memory<byte> buffer)
            {
                var socket = _socket;
                if (socket.Available > 0)
                    return new ValueTask<int>(socket.Receive(buffer.Span));

                var tcs = new TaskCompletionSource<int>();
                var receiveArgs = _receiveArgs;
                if (receiveArgs.UserToken != null)
                {
                    Debug.WriteLine("Warn:ReceiveArgs");
                    receiveArgs = new SocketAsyncEventArgs();
                    receiveArgs.Completed += ReceiveCompleted;
                }
                receiveArgs.UserToken = tcs;
                receiveArgs.SetBuffer(buffer);
                try
                {
                    if (socket.ReceiveAsync(receiveArgs))
                        return new ValueTask<int>(tcs.Task);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                    receiveArgs.UserToken = null;
                    return new ValueTask<int>(tcs.Task);
                }
                if (receiveArgs.SocketError == SocketError.Success)
                    tcs.TrySetResult(receiveArgs.BytesTransferred);
                else
                    tcs.TrySetException(new SocketException((int)receiveArgs.SocketError));
                receiveArgs.UserToken = null;
                return new ValueTask<int>(tcs.Task);
            }
            public ValueTask<int> ReceiveAsync(byte[] buffer, int offset, int count)
            {
                var socket = _socket;
                if (socket.Available > 0)
                    return new ValueTask<int>(socket.Receive(buffer, offset, count, SocketFlags.None));

                var tcs = new TaskCompletionSource<int>();
                var receiveArgs = _receiveArgs;
                if (receiveArgs.UserToken != null)
                {
                    Debug.WriteLine("Warn:ReceiveArgs");
                    receiveArgs = new SocketAsyncEventArgs();
                    receiveArgs.Completed += ReceiveCompleted;
                }
                receiveArgs.UserToken = tcs;
                receiveArgs.SetBuffer(buffer, offset, count);
                try
                {
                    if (socket.ReceiveAsync(receiveArgs))
                        return new ValueTask<int>(tcs.Task);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                    receiveArgs.UserToken = null;
                    return new ValueTask<int>(tcs.Task);
                }
                if (receiveArgs.SocketError == SocketError.Success)
                    tcs.TrySetResult(receiveArgs.BytesTransferred);
                else
                    tcs.TrySetException(new SocketException((int)receiveArgs.SocketError));
                receiveArgs.UserToken = null;
                return new ValueTask<int>(tcs.Task);
            }
            public void Send(ReadOnlySpan<byte> buffer)
            {
                _socket.Send(buffer);
            }
            public void Send(byte[] buffer, int offset, int count)
            {
                _socket.Send(buffer, offset, count, SocketFlags.None);
            }
            public Task SendAsync(ReadOnlyMemory<byte> buffer)
            {
                var socket = _socket;
                var tcs = new TaskCompletionSource<object>();
                var sendArgs = _sendArgs;
                if (sendArgs.UserToken != null)
                {
                    Debug.WriteLine("Warn:SendArgs");
                    sendArgs = new SocketAsyncEventArgs();
                    sendArgs.Completed += SendCompleted;
                }
                sendArgs.UserToken = tcs;
                sendArgs.SetBuffer(MemoryMarshal.AsMemory(buffer));
                try
                {
                    if (socket.SendAsync(sendArgs))
                        return tcs.Task;
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                    sendArgs.UserToken = null;
                    return tcs.Task;
                }
                if (sendArgs.SocketError == SocketError.Success)
                    tcs.TrySetResult(null);
                else
                    tcs.TrySetException(new SocketException((int)sendArgs.SocketError));
                sendArgs.UserToken = null;
                return tcs.Task;
            }
            public Task SendAsync(byte[] buffer, int offset, int count)
            {
                var socket = _socket;
                var tcs = new TaskCompletionSource<object>();
                var sendArgs = _sendArgs;
                if (sendArgs.UserToken != null)
                {
                    Debug.WriteLine("Warn:SendArgs");
                    sendArgs = new SocketAsyncEventArgs();
                    sendArgs.Completed += SendCompleted;
                }
                sendArgs.UserToken = tcs;
                sendArgs.SetBuffer(buffer, offset, count);
                try
                {
                    if (socket.SendAsync(sendArgs))
                        return tcs.Task;
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                    sendArgs.UserToken = null;
                    return tcs.Task;
                }
                if (sendArgs.SocketError == SocketError.Success)
                    tcs.TrySetResult(null);
                else
                    tcs.TrySetException(new SocketException((int)sendArgs.SocketError));
                sendArgs.UserToken = null;
                return tcs.Task;
            }
            public void SendFile(string fileName)
            {
                _socket.SendFile(fileName);
            }
            public Task SendFileAsync(string fileName)//windows TransmitFile Max=2G
            {
                var socket = _socket;
                var tcs = new TaskCompletionSource<object>(socket);
                socket.BeginSendFile(fileName, _sendFileCallback, tcs);
                return tcs.Task;
            }
            public void Close()
            {
                try
                {
                    _socket.Shutdown(SocketShutdown.Both);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex, "UnobservedException");
                }
                finally
                {
                    _socket.Close();
                }
                _receiveArgs.Clear();
                _sendArgs.Clear();
            }
            public void Dispose()
            {
                _connection?.Close();
                _acceptArgs.Dispose();
                _receiveArgs.Dispose();
                _sendArgs.Dispose();
            }

            #region Completed
            static void ReceiveCompleted(object sender, SocketAsyncEventArgs e)
            {
                var tcs = (TaskCompletionSource<int>)e.UserToken;
                Debug.Assert(tcs != null);
                var socketError = e.SocketError;
                var bytesTransferred = e.BytesTransferred;
                e.UserToken = null;
                if (socketError == SocketError.Success)
                {
                    tcs.TrySetResult(bytesTransferred);
                }
                else
                {
                    tcs.TrySetException(new SocketException((int)socketError));
                }
            }
            static void SendCompleted(object sender, SocketAsyncEventArgs e)
            {
                var tcs = (TaskCompletionSource<object>)e.UserToken;
                Debug.Assert(tcs != null);
                var socketError = e.SocketError;
                e.UserToken = null;
                if (socketError == SocketError.Success)
                {
                    tcs.TrySetResult(null);
                }
                else
                {
                    tcs.TrySetException(new SocketException((int)socketError));
                }
            }
            static void SendFileCompleted(IAsyncResult ar)
            {
                var tcs = (TaskCompletionSource<object>)ar.AsyncState;
                Debug.Assert(tcs != null);
                var socket = (Socket)tcs.Task.AsyncState;
                try
                {
                    socket.EndSendFile(ar);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                    return;
                }
                tcs.TrySetResult(null);
            }
            #endregion
        }
        public TcpServer(int port) :
            this(new IPEndPoint(IPAddress.Any, port))
        { }
        public TcpServer(string ipAddress, int port) :
            this(new IPEndPoint(IPAddress.Parse(ipAddress), port))
        { }
        public TcpServer(EndPoint localEndPoint)
        {
            if (localEndPoint == null)
                throw new ArgumentNullException(nameof(localEndPoint));

            _localEndPoint = localEndPoint;
            _socket = localEndPoint is UnixDomainSocketEndPoint
                    ? new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified)
                    : new Socket(localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        }
        public Socket Socket => _socket;
        public EndPoint LocalEndPoint => _localEndPoint;
        public IConnectionHandler Handler { get; set; }
        public void Start()
        {
            Start(Environment.ProcessorCount * 1024, 65535);
        }
        public void Start(int maxConnections, int backlog)
        {
            if (_listening)
                throw new InvalidOperationException(nameof(Start));
            if (maxConnections <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxConnections));
            if (backlog <= 0)
                throw new ArgumentOutOfRangeException(nameof(backlog));

            _socket.Bind(_localEndPoint);
            _connections = new Connection[maxConnections];
            for (int i = 0; i < maxConnections; i++)
            {
                _connections[i] = new Connection(this);
            }
            _socket.Listen(backlog);
            _listening = true;
            for (int i = 0; i < _connections.Length; i++)
            {
                _connections[i].Accept();
            }
        }
        public void Stop()
        {
            if (!_listening)
                return;

            _listening = false;
            //TODO
            _socket.Close();
            for (int i = 0; i < _connections.Length; i++)
            {
                _connections[i].Dispose();
            }
        }
    }
}
