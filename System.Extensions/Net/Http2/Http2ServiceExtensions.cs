
namespace System.Extensions.Net
{
    using System.Diagnostics;
    using System.Collections.Generic;
    using System.Net.Security;
    using System.Threading.Tasks;
    using System.Security.Cryptography.X509Certificates;
    using System.Security.Authentication;
    using System.Extensions.Http;
    public static class Http2ServiceExtensions
    {
        private static Property<HttpRequest> _Pusher = new Property<HttpRequest>("Http2ServiceExtensions.Pusher");
        public static IHttp2Pusher Pusher(this HttpRequest @this)
        {
            if (@this == null)
                return null;

            return (IHttp2Pusher)@this.Properties[_Pusher];
        }
        public static void Pusher(this HttpRequest @this, IHttp2Pusher pusher)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            @this.Properties[_Pusher] = pusher;
        }
        public class Http2Options
        {
            public int Http1KeepAliveTimeout = -60000;//60s-120s
            public int Http1ReceiveTimeout = -10000;//10s-20s
            public int Http1SendTimeout = -10000;//10s-20s
            public int Http1MaxHeaderSize = 40 << 10;//40K(<Large GC) MaxHeaderSize
            public int KeepAliveTimeout = -60000;//60s-120s
            public int ReceiveTimeout = -20000;//20s-40s
            public int SendTimeout = -10000;//10s-20s
            public int MaxConcurrentStreams = 64;
            public int InitialWindowSize = 1024 * 1024;//==ReceiveWindow(default 1M)
            public int MaxHeaderListSize = 40 * 1024;//after Encode
            public int MaxSettings = 1;//Settings Frame Count
            public X509Certificate Certificate;//
            public int HandShakeTimeout = -5000;//5s-10s
        }
        public static IHttpService UseHttp2(this IConnectionService @this, Action<Http2Options> options, IHttpHandler handler)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            var optionsValue = new Http2Options();
            options?.Invoke(optionsValue);
            if (optionsValue.Certificate == null)
                throw new ArgumentNullException(nameof(optionsValue.Certificate));
            var http1 = new HttpService(
                optionsValue.Http1KeepAliveTimeout,
                optionsValue.Http1ReceiveTimeout,
                optionsValue.Http1SendTimeout,
                optionsValue.Http1MaxHeaderSize
                );
            http1.Handler = handler;
            var http2 = new Http2Service(
                optionsValue.KeepAliveTimeout,
                optionsValue.ReceiveTimeout,
                optionsValue.SendTimeout,
                optionsValue.MaxConcurrentStreams,
                optionsValue.InitialWindowSize,
                optionsValue.MaxHeaderListSize,
                optionsValue.MaxSettings
                );
            http2.Handler = handler;
            var service = new Service(http1, http2, optionsValue.Certificate, optionsValue.HandShakeTimeout);
            @this.Handler = service;
            return service;
        }
        public static IHttpService UseHttp2(this IConnectionService @this, Action<Http2Options, HttpRouter> handler)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var optionsValue = new Http2Options();
            var router = new HttpRouter();
            handler.Invoke(optionsValue, router);
            if (optionsValue.Certificate == null)
                throw new ArgumentNullException(nameof(optionsValue.Certificate));
            var http1 = new HttpService(
                optionsValue.Http1KeepAliveTimeout,
                optionsValue.Http1ReceiveTimeout,
                optionsValue.Http1SendTimeout,
                optionsValue.Http1MaxHeaderSize
                );
            http1.Handler = router;
            var http2 = new Http2Service(
                optionsValue.KeepAliveTimeout,
                optionsValue.ReceiveTimeout,
                optionsValue.SendTimeout,
                optionsValue.MaxConcurrentStreams,
                optionsValue.InitialWindowSize,
                optionsValue.MaxHeaderListSize,
                optionsValue.MaxSettings
                );
            http2.Handler = router;
            var service = new Service(http1, http2, optionsValue.Certificate, optionsValue.HandShakeTimeout);
            @this.Handler = service;
            return service;
        }
        public static IHttpService UseHttp2(this IConnectionService @this, Action<Http2Options> options, Func<HttpRequest, Task<HttpResponse>> handler)
        {
            return UseHttp2(@this, options, HttpHandler.Create(handler));
        }
        #region private
        private class Service : IHttpService
        {
            public Service(HttpService http1, Http2Service http2, X509Certificate cert, int handShakeTimeout)
            {
                Debug.Assert(http1 != null);
                Debug.Assert(http2 != null);
                Debug.Assert(cert != null);
                _http1 = http1;
                _http2 = http2;
                _sslOptions = new SslServerAuthenticationOptions()
                {
                    ServerCertificate = cert,
                    EnabledSslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12 | SslProtocols.Tls13,
                    AllowRenegotiation = false,
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                    ClientCertificateRequired = false,
                    ApplicationProtocols = new List<SslApplicationProtocol>() { SslApplicationProtocol.Http2, SslApplicationProtocol.Http11 },
                };
                if (handShakeTimeout != 0)
                    _handShakeQueue = new TaskTimeoutQueue<IConnection>(handShakeTimeout);
            }

            private SslServerAuthenticationOptions _sslOptions;
            private HttpService _http1;
            private Http2Service _http2;
            private TaskTimeoutQueue<IConnection> _handShakeQueue;
            public IHttpHandler Handler
            {
                get => _http2.Handler;
                set
                {
                    _http1.Handler = value;
                    _http2.Handler = value;
                }
            }
            public async Task HandleAsync(IConnection connection)
            {
                var ssl = await connection.UseSslAsync(_sslOptions).Timeout(_handShakeQueue);
                try
                {
                    var protocol = ssl.Security.ApplicationProtocol;
                    if (protocol == SslApplicationProtocol.Http2)
                    {
                        await _http2.HandleAsync(ssl);
                        return;
                    }
                    else if (protocol == SslApplicationProtocol.Http11 || protocol.Protocol.IsEmpty)
                    {
                        await _http1.HandleAsync(ssl);
                        return;
                    }
                    else
                    {
                        throw new NotSupportedException($"ALPN:{protocol}");
                    }
                }
                finally
                {
                    ssl.Close();
                }
            }
        }
        #endregion
    }
}
