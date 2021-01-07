using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Net.Security;
using System.Extensions.Http;
using System.Extensions.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;

namespace BasicSample
{
    //ignore websocket(req.Headers.TryGetValue(HttpHeaders.Upgrade,out var upgrade) pipe)
    public class HttpProxySample
    {
        private static HttpClient _Proxy1 = HttpClient.CreateHttpProxy("localhost", 9999, 2);
        private static HttpClient _Proxy2 = HttpClient.CreateHttpProxy(() => ClientConnection.Create("localhost", 9999), 2);
        public static void RunHttp() 
        {
            var proxySvr = new TcpServer(9999);
            var httpClient = HttpClient.Default.UseTimeout();//HttpClient.Create()
            proxySvr.UseHttp(
                (options) => {
                    //options.KeepAliveTimeout = 0;
                },
                (req) => {
                    return httpClient.SendAsync(req);
                });
            proxySvr.Start();


            //TEST
            //Browser settings proxy (http)
            //localhost 9999
            //OR
            //Code
            var req1 = new HttpRequest("http://cnblogs.com");
            _Proxy1.SendAsync(req1, (resp) => {
                Console.WriteLine(resp.StatusCode);
                foreach (var item in resp.Headers)
                {
                    Console.WriteLine($"{item.Key}={item.Value}");
                }
            }).Wait();
            var req2 = new HttpRequest("http://cnblogs.com/");
            _Proxy2.SendAsync(req2, (resp) => {
                Console.WriteLine(resp.StatusCode);
                foreach (var item in resp.Headers)
                {
                    Console.WriteLine($"{item.Key}={item.Value}");
                }
            }).Wait();
        }


        private static HttpClient _Proxy3 = HttpClient.CreateHttpsProxy("localhost", 9899, 2);
        private static HttpClient _Proxy4 = HttpClient.CreateHttpsProxy("localhost", 9899, 2, "cnblogs.com", 443);
        public static void RunHttps()
        {
            //Tunnel
            var proxySvr = new TcpServer(9899);
            proxySvr.Use(async (conn, _) => {
                var req = new HttpRequest();
                await conn.ReceiveAsync(req);
                if (req.Method != HttpMethod.Connect)
                {
                    //ignore http
                    req.Dispose();
                    return;
                }
                await req.Content.DrainAsync();
                var resp = new HttpResponse()
                {
                    StatusCode = 200,
                    ReasonPhrase = "Connection Established"
                };
                await conn.SendAsync(resp, req);
                req.Dispose();
                resp.Dispose();
                var proxyClient = ClientConnection.Create(req.Url.Host, req.Url.Port ?? 443, out var proxyDisposable);
                await proxyClient.OpenAsync();
                await PipeAsync(proxyClient, conn);
                proxyDisposable.Dispose();//try finally
            });
            proxySvr.Start();


            //TEST
            //Browser settings proxy (https)
            //localhost 9899
            //OR
            //Code
            var req1 = new HttpRequest("https://cnblogs.com");
            _Proxy3.SendAsync(req1, (resp) => {
                Console.WriteLine(resp.StatusCode);
                foreach (var item in resp.Headers)
                {
                    Console.WriteLine($"{item.Key}={item.Value}");
                }
            }).Wait();
            var req2 = new HttpRequest("https://cnblogs.com/");
            _Proxy4.SendAsync(req2, (resp) => {
                Console.WriteLine(resp.StatusCode);
                foreach (var item in resp.Headers)
                {
                    Console.WriteLine($"{item.Key}={item.Value}");
                }
            }).Wait();
        }


        private static HttpClient _Proxy5 = HttpClient.CreateHttpProxy("localhost", 9799, 2);
        private static HttpClient _Proxy6 = HttpClient.CreateHttpProxy(() => ClientConnection.Create("localhost", 9799), 2);
        private static HttpClient _Proxy7 = HttpClient.CreateHttpsProxy("localhost", 9799, 2);
        private static HttpClient _Proxy8 = HttpClient.CreateHttpsProxy("localhost", 9799, 2, "cnblogs.com", 443);
        public static void RunHttpAndHttps()
        {
            var proxySvr = new TcpServer(9799);
            var httpClient = HttpClient.Default.UseTimeout();//HttpClient.Create()
            proxySvr.UseHttp(
                (options) => {
                    
                },
                (req) => {
                    return httpClient.SendAsync(req);
                });
            proxySvr.Use(async (conn, handler) => {
                var req = new HttpRequest();
                await conn.ReceiveAsync(req);
                if (req.Method == HttpMethod.Connect)
                {
                    await req.Content.DrainAsync();
                    var resp = new HttpResponse()
                    {
                        StatusCode = 200,
                        ReasonPhrase = "Connection Established"
                    };
                    await conn.SendAsync(resp, req);
                    req.Dispose();
                    resp.Dispose();
                    var proxyClient = ClientConnection.Create(req.Url.Host, req.Url.Port ?? 443, out var proxyDisposable);
                    await proxyClient.OpenAsync();
                    await PipeAsync(proxyClient, conn);
                    proxyDisposable.Dispose(); //try finally
                }
                else
                {
                    var resp = await httpClient.SendAsync(req);
                    await conn.SendAsync(resp, req);
                    req.Dispose();
                    resp.Dispose();
                    await handler.HandleAsync(conn);
                }
            });
            proxySvr.Start();


            //TEST
            //Browser settings proxy (http,https)
            //localhost 9799
            //OR
            //Code
            var req1 = new HttpRequest("http://cnblogs.com");
            _Proxy5.SendAsync(req1, (resp) => {
                Console.WriteLine(resp.StatusCode);
                foreach (var item in resp.Headers)
                {
                    Console.WriteLine($"{item.Key}={item.Value}");
                }
            }).Wait();
            var req2 = new HttpRequest("http://cnblogs.com/");
            _Proxy6.SendAsync(req2, (resp) => {
                Console.WriteLine(resp.StatusCode);
                foreach (var item in resp.Headers)
                {
                    Console.WriteLine($"{item.Key}={item.Value}");
                }
            }).Wait();
            var req3 = new HttpRequest("https://cnblogs.com");
            _Proxy7.SendAsync(req3, (resp) => {
                Console.WriteLine(resp.StatusCode);
                foreach (var item in resp.Headers)
                {
                    Console.WriteLine($"{item.Key}={item.Value}");
                }
            }).Wait();
            var req4 = new HttpRequest("https://cnblogs.com/");
            _Proxy8.SendAsync(req4, (resp) => {
                Console.WriteLine(resp.StatusCode);
                foreach (var item in resp.Headers)
                {
                    Console.WriteLine($"{item.Key}={item.Value}");
                }
            }).Wait();
        }


        private static HttpClient _Proxy9 = HttpClient.CreateHttpsProxy("localhost", 9699, 2);
        private static HttpClient _Proxy10 = HttpClient.CreateHttpsProxy("localhost", 9699, 2, "cnblogs.com", 443);
        public static void RunHttpsFiddle() 
        {
            var fiddleSvr = new TcpServer(8699);
            var httpClient = HttpClient.Default.UseTimeout();//HttpClient.Create()
            fiddleSvr.UseHttp(
                 (options) => {
                     options.KeepAliveTimeout = 0;
                     options.ReceiveTimeout = 0;
                     options.SendTimeout = 0;
                 },
                 (req) => {
                     Console.WriteLine($"Fiddle:{req.Url}");
                     return httpClient.SendAsync(req);
                 });
            var hosts = new ConcurrentDictionary<int, string>();
            var certs = new ConcurrentDictionary<string, X509Certificate>();
            fiddleSvr.Use(async(conn, handler) => {
                var remotePort = ((System.Net.IPEndPoint)conn.RemoteEndPoint).Port;
                string host;
                var spinWait = new SpinWait();
                do
                {
                    spinWait.SpinOnce();
                } while (!hosts.TryGetValue(remotePort, out host));
                hosts.TryRemove(remotePort, out _);

                if (!certs.TryGetValue(host, out var cert))
                {
                    lock (certs)//OR no lock
                    {
                        if (!certs.TryGetValue(host, out cert))
                        {
                            var rootCert = new X509Certificate2(@"MyCA-Root.pfx", "123456");//install
                            using (var rsa = RSA.Create(2048))
                            {
                                var certReq = new CertificateRequest($"CN={host}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                                certReq.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
                                certReq.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation | X509KeyUsageFlags.KeyEncipherment, true));
                                var altName = new SubjectAlternativeNameBuilder();
                                altName.AddDnsName(host);
                                certReq.CertificateExtensions.Add(altName.Build(true));
                                var serialBuf = new byte[16];
                                RandomNumberGenerator.Fill(serialBuf.AsSpan(1));
                                var signCert = certReq.Create(rootCert, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1), serialBuf);//notAfter<rootCert
                                cert = signCert.CopyWithPrivateKey(rsa);
                                cert = new X509Certificate2(cert.Export(X509ContentType.Pfx, "123456"), "123456");
                                certs.TryAdd(host, cert);
                            }
                        }
                    }
                }
                var sslOptions = new SslServerAuthenticationOptions()
                {
                    ServerCertificate = cert,//cert
                    EnabledSslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12,
                    AllowRenegotiation = false,
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                    ClientCertificateRequired = false
                };
                var ssl = await conn.UseSslAsync(sslOptions);//handShakeTimeout
                await handler.HandleAsync(ssl);
            });
            fiddleSvr.Start();

            var proxySvr = new TcpServer(9699);
            proxySvr.UseHttp(
                (options) => {
                    //options.KeepAliveTimeout = 0;
                },
                (req) => {
                    return httpClient.SendAsync(req);
                });
            proxySvr.Use(async (conn, handler) => {
                var req = new HttpRequest();
                await conn.ReceiveAsync(req);
                if (req.Method == HttpMethod.Connect)
                {
                    await req.Content.DrainAsync();
                    var resp = new HttpResponse()
                    {
                        StatusCode = 200,
                        ReasonPhrase = "Connection Established"
                    };
                    await conn.SendAsync(resp, req);
                    req.Dispose();
                    resp.Dispose();
                    var proxyClient = ClientConnection.Create("localhost", 8699, out var proxyDisposable);//fiddleSvr
                    await proxyClient.OpenAsync();
                    var localPort= ((System.Net.IPEndPoint)proxyClient.LocalEndPoint).Port;
                    hosts.TryAdd(localPort, req.Url.Host);
                    await PipeAsync(proxyClient, conn);
                    proxyDisposable.Dispose(); //try finally
                }
                else
                {
                    var resp = await httpClient.SendAsync(req);
                    await conn.SendAsync(resp, req);
                    req.Dispose();
                    resp.Dispose();
                    await handler.HandleAsync(conn);
                }
            });
            proxySvr.Start();


            //TEST
            //Browser settings proxy (http https)
            //localhost 9699
            //OR
            //Code
            ConnectionExtensions.UseSsl((conn, options) => {
                options.RemoteCertificateValidationCallback += (a, b, c, d) => true;
            });
            var req1 = new HttpRequest("https://cnblogs.com");
            _Proxy9.SendAsync(req1, (resp) => {
                Console.WriteLine(resp.StatusCode);
                foreach (var item in resp.Headers)
                {
                    Console.WriteLine($"{item.Key}={item.Value}");
                }
            }).Wait();
            var req2 = new HttpRequest("https://cnblogs.com/");
            _Proxy10.SendAsync(req2, (resp) => {
                Console.WriteLine(resp.StatusCode);
                foreach (var item in resp.Headers)
                {
                    Console.WriteLine($"{item.Key}={item.Value}");
                }
            }).Wait();
        }


        private static HttpClient _Proxy11 = HttpClient.CreateHttpsProxy("localhost", 9599, 2);
        private static HttpClient _Proxy12 = HttpClient.CreateHttpsProxy("localhost", 9599, 2, "cnblogs.com", 443);
        public static void RunLocalRemote() 
        {
            var safeHost = "www.baidu.com";
            var useProxys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            {
                "cnblogs.com",
                "google.com"
            };

            var localProxySvr = new TcpServer(9599);
            localProxySvr.Use(async (conn, _) => {
                var req = new HttpRequest();
                await conn.ReceiveAsync(req);
                if (req.Method != HttpMethod.Connect) 
                {
                    //ignore http
                    req.Dispose();
                    return;
                }
                var host = req.Url.Host;
                var port = req.Url.Port ?? 443;
                if (useProxys.Contains(req.Url.Domain)) //req.Url.Host
                {
                    req.Headers["Proxy-Authority"] = req.Url.Authority;
                    req.Url.Host = safeHost;
                    req.Headers[HttpHeaders.Host] = req.Url.Authority;
                    //RemoteProxy
                    host = "127.0.0.1";
                    port = 9499;
                }
                var proxyClient = ClientConnection.Create(host, port, out var proxyDisposable);
                await proxyClient.OpenAsync();
                var resp = await proxyClient.SendAsync(req);
                await conn.SendAsync(resp, req);
                req.Dispose();
                resp.Dispose();
                await PipeAsync(proxyClient, conn);
                proxyDisposable.Dispose(); //try finally
            });
            localProxySvr.Start();


            //远程服务器代理
            var remoteProxySvr = new TcpServer(9499);
            var httpClient = HttpClient.Default.UseTimeout();//HttpClient.Create()
            remoteProxySvr.Use(async (conn, _) => {
                var req = new HttpRequest();
                await conn.ReceiveAsync(req);
                if (req.Method != HttpMethod.Connect) 
                {
                    //ignore http
                    req.Dispose();
                    return;
                }

                await req.Content.DrainAsync();
                var resp = new HttpResponse()
                {
                    StatusCode = 200,
                    ReasonPhrase = "Connection Established"
                };
                await conn.SendAsync(resp, req);
                req.Dispose();
                resp.Dispose();
                if (req.Headers.TryGetValue("Proxy-Authority", out var authority)) 
                {
                    req.Url.Authority = authority;
                }
                var proxyClient = ClientConnection.Create(req.Url.Host, req.Url.Port ?? 443, out var proxyDisposable);
                await proxyClient.OpenAsync();
                await PipeAsync(proxyClient, conn);
                proxyDisposable.Dispose(); //try finally
            });
            remoteProxySvr.Start();


            //TEST
            //Browser settings proxy (https)
            //localhost 9599
            //OR
            //Code
            var req1 = new HttpRequest("https://cnblogs.com");
            _Proxy11.SendAsync(req1, (resp) => {
                Console.WriteLine(resp.StatusCode);
                foreach (var item in resp.Headers)
                {
                    Console.WriteLine($"{item.Key}={item.Value}");
                }
            }).Wait();
            var req2 = new HttpRequest("https://cnblogs.com/");
            _Proxy12.SendAsync(req2, (resp) => {
                Console.WriteLine(resp.StatusCode);
                foreach (var item in resp.Headers)
                {
                    Console.WriteLine($"{item.Key}={item.Value}");
                }
            }).Wait();
        }


        private static async Task PipeAsync(ClientConnection client, IConnection conn) 
        {
            var readBytes = ConnectionExtensions.GetBytes(out var readDisposable);
            var writeBytes = ConnectionExtensions.GetBytes(out var writeDisposable);
            var readTask = Task.Run(async () =>
            {
                try
                {
                    for (; ; )
                    {
                        var result = await client.ReceiveAsync(readBytes);
                        if (result == 0)
                            return;//FIN
                        await conn.SendAsync(readBytes.Slice(0, result));
                    }
                }
                finally
                {
                    conn.Close();
                }
            });
            var writeTask = Task.Run(async () =>
            {
                try
                {
                    for (; ; )
                    {
                        var result = await conn.ReceiveAsync(writeBytes);
                        if (result == 0)
                            return;//FIN
                        await client.SendAsync(writeBytes.Slice(0, result));
                    }
                }
                finally
                {
                    client.Close();
                }
            });
            try
            {
                await Task.WhenAll(readTask, writeTask);
            }
            finally
            {
                readDisposable.Dispose();
                writeDisposable.Dispose();
            }
        }
        static HttpProxySample() 
        {
            if (!File.Exists("MyCA-Root.pfx"))  //Create Test Root Cert
            {
                using (var rsa = RSA.Create(2048))
                {
                    var request = new CertificateRequest("CN=MyCA-Root", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                    request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
                    request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.CrlSign | X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.DigitalSignature, false));
                    var rootCert = request.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(2));
                    File.WriteAllBytes(@"MyCA-Root.pfx", rootCert.Export(X509ContentType.Pfx, "123456"));
                }
            }
        }
    }
}
