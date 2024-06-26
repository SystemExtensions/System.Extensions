
namespace System.Extensions.Net
{
    using System.Text;
    using System.Buffers;
    using System.Net.Security;
    using System.Net.WebSockets;
    using System.Threading.Tasks;
    using System.Security.Cryptography;
    using System.Extensions.Http;
    public static class WebSocketExtensions
    {
        public static async Task<WebSocketMessageType> ReceiveAsync(this WebSocket @this, BufferWriter<byte> buffer)
        {
            for (; ; )
            {
                var memory = buffer.GetMemory();

                var result = await @this.ReceiveAsync(memory, default);

                buffer.Advance(result.Count);

                if (result.EndOfMessage)
                {
                    return result.MessageType;
                }
            }
        }

        //Text
        public static Task SendAsync(this WebSocket @this, string text)
        {
            return @this.SendAsync(text.AsMemory());
        }
        public static async Task SendAsync(this WebSocket @this, ReadOnlyMemory<char> text)
        {
            var buffer = Buffer<byte>.Create(ArrayPool<byte>.Shared, 8192, out var disposable);

            try
            {
                buffer.WriteChars(text.Span, Encoding.UTF8);

                await @this.SendAsync(buffer.Sequence, WebSocketMessageType.Text);
            }
            finally
            {
                disposable.Dispose();
            }
        }
        public static async Task SendAsync(this WebSocket @this, ReadOnlySequence<char> text)
        {
            var buffer = Buffer<byte>.Create(ArrayPool<byte>.Shared, 8192, out var disposable);

            try
            {
                buffer.WriteChars(text, Encoding.UTF8);

                await @this.SendAsync(buffer.Sequence, WebSocketMessageType.Text);
            }
            finally
            {
                disposable.Dispose();
            }
        }

        public static async Task SendAsync(this WebSocket @this, byte[] buffer, WebSocketMessageType messageType)
        {
            await @this.SendAsync(buffer, messageType, true, default);
        }
        public static async Task SendAsync(this WebSocket @this, ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType)
        {
            await @this.SendAsync(buffer, messageType, true, default);
        }
        public static async Task SendAsync(this WebSocket @this, ReadOnlySequence<byte> buffer, WebSocketMessageType messageType)
        {

            if (buffer.IsSingleSegment)
            {
                await @this.SendAsync(buffer.First, messageType, true, default);
            }
            else
            {
                //TODO 优化??
                foreach (var memory in buffer)
                {
                    await @this.SendAsync(memory, messageType, false, default);
                }

                await @this.SendAsync(ArraySegment<byte>.Empty, messageType, true, default);
            }
        }


        public const string Magic = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
        public static HttpResponse UseWebSocket(this HttpResponse @this, HttpRequest request, TimeSpan keepAliveInterval, Func<WebSocket, Task> webSocketHandler)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (webSocketHandler == null)
                throw new ArgumentNullException(nameof(webSocketHandler));

            var connection = request.Headers[HttpHeaders.Connection];
            if (!connection.Contains("Upgrade"))//connection.EqualsIgnoreCase("Upgrade") keepalive, Upgrade
                throw new InvalidOperationException("Upgrade");

            //TODO?
            //var connectionSpan = connection.AsSpan();
            //while (HttpHeaders.TryParse(ref connectionSpan, out var connectionName)) 
            //{

            //}

            var upgrade = request.Headers[HttpHeaders.Upgrade];
            if (!upgrade.EqualsIgnoreCase("websocket"))
                throw new InvalidOperationException("websocket");

            //TODO??
            //Sec-WebSocket-Version;

            var webSocketKey = request.Headers["Sec-WebSocket-Key"];
            if (webSocketKey == null)
                throw new InvalidOperationException("Sec-WebSocket-Key");

            //Sec-Websocket-Accept:
            var sha1 = SHA1.Create();
            var bytes = Encoding.ASCII.GetBytes(webSocketKey + Magic);
            var hash = sha1.ComputeHash(bytes);
            var websocketAccept = Convert.ToBase64String(hash);

            @this.StatusCode = 101;
            @this.Headers[HttpHeaders.Connection] = "Upgrade";
            @this.Headers[HttpHeaders.Upgrade] = "websocket";
            @this.Headers["Sec-Websocket-Accept"] = websocketAccept;

            var connectionHandler = new _WebSocketHandler(null, keepAliveInterval, webSocketHandler);
            @this.ConnectionHandler(connectionHandler);

            return @this;
        }
        private class _WebSocketHandler : IConnectionHandler
        {
            private string _subProtocol;
            private TimeSpan _keepAliveInterval;
            private Func<WebSocket, Task> _webSocketHandler;
            public _WebSocketHandler(string subProtocol, TimeSpan keepAliveInterval, Func<WebSocket, Task> webSocketHandler)
            {
                _subProtocol = subProtocol;
                _keepAliveInterval = keepAliveInterval;
                _webSocketHandler = webSocketHandler;
            }
            public async Task HandleAsync(IConnection connection)
            {
                var stream = connection.AsStream();

                var webSocket = WebSocket.CreateFromStream(stream, true, _subProtocol, _keepAliveInterval);

                try
                {
                    await _webSocketHandler.Invoke(webSocket);
                }
                finally
                {
                    webSocket.Dispose();
                }
            }
        }

        //客户端
        public static Task<WebSocket> ConnectAsync(string url)
        {
            return ConnectAsync(new Url(url), null, null, TimeSpan.FromSeconds(30));
        }
        public static Task<WebSocket> ConnectAsync(string url, TimeSpan keepAliveInterval)
        {
            return ConnectAsync(new Url(url), null, null, keepAliveInterval);
        }
        public static Task<WebSocket> ConnectAsync(string url, Action<HttpRequest> onRequest, Action<HttpResponse> onResponse, TimeSpan keepAliveInterval)
        {
            return ConnectAsync(new Url(url), onRequest, onResponse, keepAliveInterval);
        }
        public static Task<WebSocket> ConnectAsync(Url url, Action<HttpRequest> onRequest, Action<HttpResponse> onResponse, TimeSpan keepAliveInterval)
        {
            var client = url.Scheme.EqualsIgnoreCase("ws") ? ClientConnection.Create(url.Host, url.Port ?? 80) :
                url.Scheme.EqualsIgnoreCase("wss") ? ClientConnection.Create(url.Host, url.Port ?? 443).UseSsl(new SslClientAuthenticationOptions()
                {
                    TargetHost = url.Host,
                    RemoteCertificateValidationCallback = (a, b, c, d) => true,
                }) :
                throw new NotSupportedException("Scheme:ws,wss");

            return ConnectAsync(url, client, onRequest, onResponse, keepAliveInterval);
        }
        public static async Task<WebSocket> ConnectAsync(Url url, ClientConnection client, Action<HttpRequest> onRequest, Action<HttpResponse> onResponse, TimeSpan keepAliveInterval)
        {
            if (url == null)
                throw new ArgumentNullException(nameof(url));
            if (client == null)
                throw new ArgumentNullException(nameof(client));

            var request = new HttpRequest(url);

            request.Method = HttpMethod.Get;
            request.Headers[HttpHeaders.Connection] = "Upgrade";
            request.Headers[HttpHeaders.Upgrade] = "websocket";
            request.Headers["Sec-Websocket-Version"] = "13";
            var webSocketKey = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
            request.Headers["Sec-Websocket-Key"] = webSocketKey;
            onRequest?.Invoke(request);

            if (!client.Connected)
            {
                await client.OpenAsync();
            }

            var response = await client.SendAsync(request);
            onResponse?.Invoke(response);

            if (response.StatusCode != 101)
                throw new NotSupportedException("StatusCode");

            if (!response.Headers[HttpHeaders.Connection].EqualsIgnoreCase("Upgrade"))
                throw new NotSupportedException("Connection");

            if (!response.Headers[HttpHeaders.Upgrade].EqualsIgnoreCase("websocket"))
                throw new NotSupportedException("Upgrade");

            //Sec-Websocket-Accept:
            var sha1 = SHA1.Create();
            var bytes = Encoding.ASCII.GetBytes(webSocketKey + Magic);
            var hash = sha1.ComputeHash(bytes);
            var websocketAccept = Convert.ToBase64String(hash);

            if (response.Headers["Sec-Websocket-Accept"] != websocketAccept)
                throw new NotSupportedException("Sec-Websocket-Accept");

            return WebSocket.CreateFromStream(client.AsStream(), false, null, keepAliveInterval);
        }
    }
}
