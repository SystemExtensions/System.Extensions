
namespace System.Extensions.Net
{
    using System.Diagnostics;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    public class TcpServer : IConnectionService, IDisposable
    {
        public class Connection : IConnection
        {
            internal IExecutor Executor;//可以反射
            internal SocketAsyncEventArgs AcceptArgs;
            public PropertyCollection<IConnection> Properties => throw new NotImplementedException();

            public bool IsSecure => throw new NotImplementedException();

            public EndPoint LocalEndPoint => throw new NotImplementedException();

            public EndPoint RemoteEndPoint => throw new NotImplementedException();

            public int Receive(Span<byte> buffer)
            {
                throw new NotImplementedException();
            }

            public int Receive(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }

            public ValueTask<int> ReceiveAsync(Memory<byte> buffer)
            {
                throw new NotImplementedException();
            }

            public ValueTask<int> ReceiveAsync(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }

            public void Send(ReadOnlySpan<byte> buffer)
            {
                throw new NotImplementedException();
            }

            public void Send(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }

            public Task SendAsync(ReadOnlyMemory<byte> buffer)
            {
                throw new NotImplementedException();
            }

            public Task SendAsync(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }

            public void SendFile(string fileName)
            {
                throw new NotImplementedException();
            }

            public Task SendFileAsync(string fileName)
            {
                throw new NotImplementedException();
            }
        }

        private bool _active=false;
        private Socket _socket;
        private IPEndPoint _localEndPoint;
        private Connection[] _connections;

        public event Action OnStart;
        public event Action OnStop;
        public event Action<Connection> OnConnected;
        public event Action<Connection> OnDisconnected;
        public event Action<Connection, Exception> OnException;

        public TcpServer(int port) :
            this(new IPEndPoint(IPAddress.Any, port))
        { }
        public TcpServer(IPAddress localIP, int port)
            : this(new IPEndPoint(localIP, port))
        { }
        public TcpServer(IPEndPoint localEndPoint)
        {
            if (localEndPoint == null)
                throw new ArgumentNullException(nameof(localEndPoint));

            _handleAsync = HandleAsync;
            _handleWaitCallback = HandleAsync;
            _localEndPoint = localEndPoint;
            _socket = new Socket(_localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        }

        protected bool Active => _active;//这个不要
        public Socket Socket => _socket;
        public IPEndPoint LocalEndPoint => _localEndPoint;
        public Connection[] Connections => _connections;
        public IConnectionHandler Handler { get; set; }

        public void Start()
        {
            Start(8192, int.MaxValue);
        }//connExecutor
        public void Start(int maxConnections, int backlog)
        {
            if (_active)
                return;
            if (backlog <= 0)
                throw new ArgumentOutOfRangeException(nameof(backlog));

            _socket.Bind(_localEndPoint);
            _connections = new Connection[maxConnections];
            for (int i = 0; i < maxConnections; i++)
            {
                _connections[i] = new Connection();
                _connections[i].Executor = null;//使用线程池执行
                _connections[i].AcceptArgs = new SocketAsyncEventArgs();
                _connections[i].AcceptArgs.Completed += AcceptCompleted;
                _connections[i].AcceptArgs.UserToken = _connections[i];
            }
            OnStart?.Invoke();
            _socket.Listen(backlog);
            for (int i = 0; i < _connections.Length; i++)
            {
                _socket.AcceptAsync(_connections[i].AcceptArgs);
            }
            Trace.TraceInformation($"Server:{_localEndPoint} is start");
            _active = true;
        }
        public void Start(Func<int, IExecutor> executorDelegate, int maxConnections, int backlog)
        {
            if (_active)
                return;
            if (executorDelegate == null)
                throw new ArgumentNullException(nameof(executorDelegate));
            if (maxConnections <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxConnections));
            if (backlog <= 0)
                throw new ArgumentOutOfRangeException(nameof(backlog));

            _socket.Bind(_localEndPoint);
            _connections = new Connection[maxConnections];
            for (int i = 0; i < _connections.Length; i++)
            {
                _connections[i] = new Connection();
                _connections[i].Executor = executorDelegate(i);
                _connections[i].AcceptArgs = new SocketAsyncEventArgs();
                _connections[i].AcceptArgs.Completed += AcceptCompleted;
                _connections[i].AcceptArgs.UserToken = _connections[i];
            }
            OnStart?.Invoke();
            _socket.Listen(backlog);
            for (int i = 0; i < _connections.Length; i++)
            {
                _socket.AcceptAsync(_connections[i].AcceptArgs);
            }
            Trace.TraceInformation($"Server:{_localEndPoint} is start");
            _active = true;
        }
        public void Stop()
        {
            if (!_active)
                return;
            _active = false;//避免继续接收到连接
            //向所有连接发送关闭请求
            if (_socket != null)
            {
                _socket.Close();
                _socket = null;
            }
            OnStop?.Invoke();
            foreach (var connection in _connections)
            {
                //connection.Dispose();
            }
            _connections = null;
            _socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        }
        private void AcceptCompleted(object sender, SocketAsyncEventArgs acceptArgs)
        {
            var connection = (Connection)acceptArgs.UserToken;
            if (acceptArgs.AcceptSocket != null && acceptArgs.SocketError == SocketError.Success)
            {
                //connection.Open(acceptArgs.AcceptSocket);
                acceptArgs.AcceptSocket = null;
                if (connection.Executor == null)
                {
                    if (!ThreadPool.UnsafeQueueUserWorkItem(_handleWaitCallback, connection))
                        throw new InvalidOperationException(nameof(ThreadPool.UnsafeQueueUserWorkItem));
                }
                else
                {
                    connection.Executor.Run(_handleAsync, connection);
                }
            }
            else//Accept失败
            {
                //OnAcceptError?.Invoke(acceptEventArgs.SocketError);
                Trace.TraceWarning("accept error:" + acceptArgs.SocketError);
                if (_active)//不是激活状态就不响应了
                {
                    acceptArgs.AcceptSocket = null;
                    _socket.AcceptAsync(connection.AcceptArgs);
                }
            }
        }

        private Action<object> _handleAsync;
        private WaitCallback _handleWaitCallback;
        private async void HandleAsync(object state)
        {
            var connection = (Connection)state;
            try
            {
                OnConnected?.Invoke(connection);//触发事件
                if (Handler != null)
                    await Handler.HandleAsync(connection);
            }
            catch (AggregateException ex)
            {
                Console.WriteLine("复合错误" + ex.InnerExceptions.Count);
                Console.WriteLine(ex.GetBaseException().Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }//忽略异常
            finally
            {
                //await connection.Disposables().DisposeAsync();
                ((IConnection)connection).Properties.Clear(_PropertyMatch);

                //if (!connection.IsClosed)
                //   connection.Close();

                OnDisconnected?.Invoke(connection);
                _socket.AcceptAsync(connection.AcceptArgs);
            }
        }

        //移除所有!#的属性
        private static Predicate<Property<IConnection>> _PropertyMatch
            = (pDesc) => { return pDesc.Name[0] != '#'; };

        private bool disposed = false;
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                Stop();
                if (_socket != null)
                {
                    _socket.Dispose();
                    _socket = null;
                }
                //清理池
            }
            disposed = true;
        }
    }
}
