
namespace System.Extensions.Net
{
    using System.Diagnostics;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Runtime.InteropServices;
    public abstract class ClientConnection
    {
        #region abstract
        public abstract PropertyCollection<ClientConnection> Properties { get; }
        public abstract bool Connected { get; }
        public abstract ISecurity Security { get; }
        public abstract EndPoint LocalEndPoint { get; }
        public abstract EndPoint RemoteEndPoint { get; }
        public abstract void Open();
        public abstract Task OpenAsync();
        public abstract int Receive(Span<byte> buffer);
        public abstract int Receive(byte[] buffer, int offset, int count);
        public abstract ValueTask<int> ReceiveAsync(Memory<byte> buffer);
        public abstract ValueTask<int> ReceiveAsync(byte[] buffer, int offset, int count);
        public abstract void Send(ReadOnlySpan<byte> buffer);
        public abstract void Send(byte[] buffer, int offset, int count);
        public abstract Task SendAsync(ReadOnlyMemory<byte> buffer);
        public abstract Task SendAsync(byte[] buffer, int offset, int count);
        public abstract void SendFile(string fileName);
        public abstract Task SendFileAsync(string fileName);
        public abstract void Close();
        #endregion

        #region private
        private class TcpClient : ClientConnection, IDisposable
        {
            public TcpClient(EndPoint remoteEndPoint)
            {
                _remoteEndPoint = remoteEndPoint;
                _properties = new PropertyCollection<ClientConnection>();
                _connectArgs = new SocketAsyncEventArgs();
                _connectArgs.Completed += ConnectCompleted;
                _receiveArgs = new SocketAsyncEventArgs();
                _receiveArgs.Completed += ReceiveCompleted;
                _sendArgs = new SocketAsyncEventArgs();
                _sendArgs.Completed += SendCompleted;
                _sendFileCompleted = SendFileCompleted;
            }

            #region private
            private EndPoint _remoteEndPoint;
            private PropertyCollection<ClientConnection> _properties;
            private Socket _socket;
            private SocketAsyncEventArgs _connectArgs, _receiveArgs, _sendArgs;
            private AsyncCallback _sendFileCompleted;
            #endregion
            public override PropertyCollection<ClientConnection> Properties => _properties;
            public override bool Connected 
            {
                get
                {
                    var socket = _socket;
                    if (socket == null)
                        return false;
                    return socket.Connected;
                }
            }
            public override EndPoint LocalEndPoint
            {
                get
                {
                    var socket = _socket;
                    if (socket == null)
                        return null;
                    return socket.LocalEndPoint;
                }
            }
            public override EndPoint RemoteEndPoint
            {
                get
                {
                    var socket = _socket;
                    if (socket == null)
                        return null;
                    return socket.RemoteEndPoint;
                }
            }
            public override ISecurity Security => null;
            public override void Open()
            {
                if (_socket != null)
                    _socket.Close();

                _socket = _remoteEndPoint is UnixDomainSocketEndPoint
                    ? new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified)
                    : new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                _socket.Connect(_remoteEndPoint);
            }
            public override Task OpenAsync()
            {
                if (_socket != null)
                    _socket.Close();

                //TODO ConnectAsync(SocketAsyncEventArgs) UnixDomainSocketEndPoint BUG
                //_socket = _remoteEndPoint is UnixDomainSocketEndPoint
                //    ? new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified)
                //    : new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                if (_remoteEndPoint is UnixDomainSocketEndPoint)
                {
                    _socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                    return _socket.ConnectAsync(_remoteEndPoint);
                }
                _socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };

                var tcs = new TaskCompletionSource<object>();
                _connectArgs.RemoteEndPoint = _remoteEndPoint;
                Debug.Assert(_connectArgs.UserToken == null);
                _connectArgs.UserToken = tcs;
                try
                {
                    if (_socket.ConnectAsync(_connectArgs))
                        return tcs.Task;
                }
                catch (Exception ex)
                {
                    _connectArgs.UserToken = null;
                    tcs.TrySetException(ex);
                    return tcs.Task;
                }
                _connectArgs.UserToken = null;
                if (_connectArgs.SocketError == SocketError.Success)
                {
                    tcs.TrySetResult(null);
                }
                else
                {
                    tcs.TrySetException(new SocketException((int)_connectArgs.SocketError));
                }
                return tcs.Task;
            }
            public override int Receive(Span<byte> buffer)
            {
                var socket = _socket;
                if (socket == null)
                    throw new InvalidOperationException($"{nameof(TcpClient)}.{nameof(Receive)}");

                return socket.Receive(buffer);
            }
            public override int Receive(byte[] buffer, int offset, int count)
            {
                var socket = _socket;
                if (socket == null)
                    throw new InvalidOperationException($"{nameof(TcpClient)}.{nameof(Receive)}");

                return socket.Receive(buffer, offset, count, SocketFlags.None);
            }
            public override ValueTask<int> ReceiveAsync(Memory<byte> buffer)
            {
                var socket = _socket;
                if (socket == null)
                    throw new InvalidOperationException($"{nameof(TcpClient)}.{nameof(ReceiveAsync)}");

                if (socket.Available > 0)
                    return new ValueTask<int>(socket.Receive(buffer.Span));

                var tcs = new TaskCompletionSource<int>();
                var receiveArgs = _receiveArgs;
                if (receiveArgs.UserToken != null)
                {
                    Debug.WriteLine("Warn:_receiveArgs");
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
            public override ValueTask<int> ReceiveAsync(byte[] buffer, int offset, int count)
            {
                var socket = _socket;
                if (socket == null)
                    throw new InvalidOperationException($"{nameof(TcpClient)}.{nameof(ReceiveAsync)}");

                if (socket.Available > 0)
                    return new ValueTask<int>(socket.Receive(buffer, offset, count, SocketFlags.None));

                var tcs = new TaskCompletionSource<int>();
                var receiveArgs = _receiveArgs;
                if (receiveArgs.UserToken != null) 
                {
                    Debug.WriteLine("Warn:_receiveArgs");
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
            public override void Send(ReadOnlySpan<byte> buffer)
            {
                var socket = _socket;
                if (socket == null)
                    throw new InvalidOperationException($"{nameof(TcpClient)}.{nameof(Send)}");

                socket.Send(buffer);
            }
            public override void Send(byte[] buffer, int offset, int count)
            {
                var socket = _socket;
                if (socket == null)
                    throw new InvalidOperationException($"{nameof(TcpClient)}.{nameof(Send)}");

                socket.Send(buffer, offset, count, SocketFlags.None);
            }
            public override Task SendAsync(ReadOnlyMemory<byte> buffer)
            {
                var socket = _socket;
                if (socket == null)
                    throw new InvalidOperationException($"{nameof(TcpClient)}.{nameof(SendAsync)}");

                var tcs = new TaskCompletionSource<object>();
                var sendArgs = _sendArgs;
                if (sendArgs.UserToken != null)
                {
                    Debug.WriteLine("Warn:_sendArgs");
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
            public override Task SendAsync(byte[] buffer, int offset, int count)
            {
                var socket = _socket;
                if (socket == null)
                    throw new InvalidOperationException($"{nameof(TcpClient)}.{nameof(SendAsync)}");

                var tcs = new TaskCompletionSource<object>();
                var sendArgs = _sendArgs;
                if (sendArgs.UserToken != null)
                {
                    Debug.WriteLine("Warn:_sendArgs");
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
            public override void SendFile(string fileName)
            {
                var socket = _socket;
                if (socket == null)
                    throw new InvalidOperationException($"{nameof(TcpClient)}.{nameof(SendFile)}");

                socket.SendFile(fileName);
            }
            public override Task SendFileAsync(string fileName)
            {
                var socket = _socket;
                if (socket == null)
                    throw new InvalidOperationException($"{nameof(TcpClient)}.{nameof(SendFileAsync)}");

                var tcs = new TaskCompletionSource<object>(socket);
                socket.BeginSendFile(fileName, _sendFileCompleted, tcs);
                return tcs.Task;
            }
            public override void Close()
            {
                if (_socket != null)
                {
                    var socket = Interlocked.Exchange(ref _socket, null);
                    if (socket != null)
                    {
                        try
                        {
                            socket.Shutdown(SocketShutdown.Both);
                        }
                        catch (Exception ex)
                        {
                            Trace.WriteLine(ex, "UnobservedException");
                        }
                        finally
                        {
                            socket.Close();
                        }
                        _receiveArgs.Clear();
                        _sendArgs.Clear();
                    }
                }
            }
            public void Dispose() 
            {
                _socket?.Dispose();
                _connectArgs.Dispose();
                _receiveArgs.Dispose();
                _sendArgs.Dispose();
            }

            #region Completed
            static void ConnectCompleted(object sender, SocketAsyncEventArgs e)
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
        #endregion
        public static ClientConnection Create(EndPoint remoteEndPoint)
        {
            if (remoteEndPoint == null)
                throw new ArgumentNullException(nameof(remoteEndPoint));

            return new TcpClient(remoteEndPoint);
        }
        public static ClientConnection Create(string host, int port)
        {
            if (host == null)
                throw new ArgumentNullException(nameof(host));
            if (port > IPEndPoint.MaxPort || port < IPEndPoint.MinPort)
                throw new ArgumentOutOfRangeException(nameof(port));

            if (IPAddress.TryParse(host, out var ipAddress))
            {
                return new TcpClient(new IPEndPoint(ipAddress, port));
            }
            else 
            {
                return new TcpClient(new DnsEndPoint(host, port));
            }
        }
        public static ClientConnection Create(EndPoint remoteEndPoint, out IDisposable disposable)
        {
            if (remoteEndPoint == null)
                throw new ArgumentNullException(nameof(remoteEndPoint));

            var client = new TcpClient(remoteEndPoint);
            disposable = client;
            return client;
        }
        public static ClientConnection Create(string host, int port, out IDisposable disposable)
        {
            if (host == null)
                throw new ArgumentNullException(nameof(host));
            if (port > IPEndPoint.MaxPort || port < IPEndPoint.MinPort)
                throw new ArgumentOutOfRangeException(nameof(port));

            if (IPAddress.TryParse(host, out var ipAddress))
            {
                var client = new TcpClient(new IPEndPoint(ipAddress, port));
                disposable = client;
                return client;
            }
            else
            {
                var client = new TcpClient(new DnsEndPoint(host, port));
                disposable = client;
                return client;
            }
        }

        //TODO??
        //Action<Socket> onConnecting onConnected
        //Func<EndPoint,Socket> socketOptions
        //Func<Socket> socketOptions
    }
}
