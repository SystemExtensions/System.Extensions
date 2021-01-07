using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Extensions.Http;
using System.Extensions.Net;

namespace BasicSample
{
    public class HttpServerClientSample
    {
        private static HttpClient _Client1 = HttpClient.Default.UseCompression().UseCookie().UseRedirect().UseTimeout().Use(async (req, client) => {
            req.Headers.Add(HttpHeaders.UserAgent, "My-Client");
            req.Headers.Add("My-Header-Name", "MyHeaderValue");
            var query = req.Url.Query;
            if (string.IsNullOrEmpty(query))
            {
                req.Url.Query = "?addName1=v1&addName2=v2";
            }
            else
            {
                req.Url.Query = query + "&addName1=v1&addName2=v2";
            }

            var resp = await client.SendAsync(req);

            if (resp.StatusCode != 200)
                throw new Exception("StatusCode");

            //resp.Headers.TryGetValue()
            Console.WriteLine(resp);
            return resp;
        });
        private static HttpClient _Client2 = HttpClient.CreateHttp("localhost", 9999, 2);
        private static HttpClient _Client3 = HttpClient.CreateHttp(() => ClientConnection.Create("localhost", 9999), 2);
        public static void RunHttp()
        {
            var httpSvr = new TcpServer(9999);
            httpSvr.UseHttp((options, router) => {
                MapRouter(router);
            });
            httpSvr.Start();

            Run(_Client1, "http://localhost:9999").Wait();

            Run(_Client2, "http://localhost:9999").Wait();

            Run(_Client3, "http://localhost:9999").Wait();
        }


        private static HttpClient _Client4 = HttpClient.Default.Use(async (req, client) => {
            try
            {
                var response = await client.SendAsync(req);
                return response;
            }
            catch (Exception)
            {
                if (req.Content != null && !req.Content.Rewind())
                    throw;
                Console.WriteLine("Retry");
                var response = await client.SendAsync(req);
                return response;
            }
        }).UseCookie().UseRedirect();
        private static HttpClient _Client5 = HttpClient.CreateHttps("localhost", 9899, 2).UseTimeout();
        private static HttpClient _Client6 = HttpClient.CreateHttp(() => ClientConnection.Create("localhost", 9899).UseSsl(
            new SslClientAuthenticationOptions()
            {
                TargetHost = "localhost",
                RemoteCertificateValidationCallback = (a, b, c, d) => true
            }), 2);
        public static void RunHttps()
        {
            //ignore cert validation  (global)
            ConnectionExtensions.UseSsl((conn, options) => {
                options.RemoteCertificateValidationCallback = (a, b, c, d) => true;
            });

            try
            {
                Run(_Client4, "https://localhost:9899").Wait();
            }
            catch { }

            var httpsSvr = new TcpServer(9899);
            httpsSvr.UseHttps((options, router) => {
                options.Certificate = new X509Certificate2("server.pfx", "123456");
                MapRouter(router);
            });
            httpsSvr.Start();

            Run(_Client4, "https://localhost:9899").Wait();

            Run(_Client5, "https://localhost:9899").Wait();

            Run(_Client6, "https://localhost:9899").Wait();
        }


        private static HttpClient _Client7 = HttpClient.CreateHttp("localhost", 9799, 2).UseCompression();//gzip,deflate,br
        private static HttpClient _Client8 = HttpClient.CreateHttps("localhost", 9798, 2).UseCompression("deflate", "br");//deflate,br
        public static void RunHttpAndHttps()
        {
            var httpSvr = new TcpServer(9799);
            var http = httpSvr.UseHttp((options, router) => {
                MapRouter(router);
            }).Use(async (req, handler) => {
                var resp = await handler.HandleAsync(req);
                resp.UseCompression(req, gzipLevel: 9, deflateLevel: -1, brLevel: 11);
                resp.Headers.TryGetValue(HttpHeaders.ContentEncoding, out var contentEncoding);
                Console.WriteLine($"ContentEncoding:{contentEncoding}");
                return resp;
            });
            httpSvr.Start();

            var httpsSvr = new TcpServer(9798);
            httpsSvr.UseHttps((options) => {
                options.Certificate = new X509Certificate2("server.pfx", "123456");
            }, http.Handler);
            httpsSvr.Start();


            Run(_Client7, "http://localhost:9799").Wait();

            ConnectionExtensions.UseSsl((conn, options) => {
                options.RemoteCertificateValidationCallback = (a, b, c, d) => true;
            });
            Run(_Client8, "https://localhost:9798").Wait();
        }


        private static HttpClient _Client9 = HttpClient.Default.UseCookie().UseRedirect().UseTimeout().Use((req, client) => {
            if (req.Version == null)
                req.Version = HttpVersion.Version20;
            return client.SendAsync(req);
        });
        private static HttpClient _Client10 = HttpClient.CreateHttps("localhost", 9699, 2);
        private static HttpClient _Client11 = HttpClient.CreateHttp2("localhost", 9699, 2, 64);
        public static void RunHttp2()//https h2
        {
            var h2Svr = new TcpServer(9699);
            h2Svr.UseHttp2((options, router) => {
                options.Certificate = new X509Certificate2("server.pfx", "123456");
                MapRouter(router);
                //Test Push
                router.MapGet("/Push", (req, resp) => {
                    resp.Headers.Add(HttpHeaders.ContentType, "text/html; charset=utf-8");
                    resp.Content = StringContent.Create(" <script src='/Test.js'></script><h1>h2 Push Test</h1>");
                    var pusher = req.Pusher();
                    if (pusher != null)
                    {
                        var pushResp = new HttpResponse();
                        pushResp.Headers.Add(HttpHeaders.ContentType, "text/javascript; charset=utf-8");
                        pushResp.Content = StringContent.Create("alert('Test.js Push')");
                        pusher.Push("/Test.js", pushResp);
                    }
                });
            });
            h2Svr.Start();

            ConnectionExtensions.UseSsl((conn, options) => {
                options.RemoteCertificateValidationCallback = (a, b, c, d) => true;
            });

            Run(_Client9, "https://localhost:9699").Wait();

            Run(_Client10, "https://localhost:9699").Wait();

            Run(_Client11, "https://localhost:9699").Wait();


            Console.WriteLine();
            Console.WriteLine("Use the browser: https://localhost:9699/Push");
            Console.WriteLine("press any key to continue");
            Console.ReadLine();
        }


        private static HttpClient _Client12 = HttpClient.CreateHttp("localhost", 9599, 2);
        private static HttpClient _Client13 = HttpClient.CreateHttps("localhost", 9598, 2);
        private static HttpClient _Client14 = HttpClient.CreateHttp2("localhost", 9598, 2, 64);
        public static void RunHttpAndH2()
        {
            var httpSvr = new TcpServer(9599);
            var http = httpSvr.UseHttp((options, router) => {
                MapRouter(router);
            });
            httpSvr.Start();

            var h2Svr = new TcpServer(9598);
            h2Svr.UseHttp2((options) => {
                options.Certificate = new X509Certificate2("server.pfx", "123456");
            }, http.Handler);
            h2Svr.Start();


            Run(_Client12, "http://localhost:9599").Wait();

            ConnectionExtensions.UseSsl((conn, options) => {
                options.RemoteCertificateValidationCallback = (a, b, c, d) => true;
            });

            Run(_Client13, "https://localhost:9598").Wait();

            Run(_Client14, "https://localhost:9598").Wait();
        }


        private static HttpClient _Client15 = HttpClient.CreateHttp2(() => ClientConnection.Create("localhost", 9499), 2, 64);
        public static void RunH2c()
        {
            var h2cSvr = new TcpServer(9499);
            var h2 = new Http2Service();
            var router = new HttpRouter();
            MapRouter(router);
            h2.Handler = router;
            h2cSvr.Handler = h2;
            h2cSvr.Start();


            Run(_Client15, "http://localhost:9499").Wait();
        }


        private static volatile int _Index = -1;
        private static HttpClient[] _Clients = new[]
        {
            HttpClient.CreateHttp("localhost",9301,2),
            HttpClient.CreateHttp("localhost",9302,2),
            HttpClient.CreateHttp("localhost",9303,2)
        };
        public static void RunGateway()
        {
            var gatewaySvr = new TcpServer(9399);
            gatewaySvr.UseHttp(
                (options) =>
                {

                }, (req) => {
                    //OR new HttpRequest()
                    var ip = ((System.Net.IPEndPoint)req.Connection().RemoteEndPoint).Address.ToString();
                    req.Headers["X-Real-IP"] = ip;
                    var index = Interlocked.Increment(ref _Index) & 0x7FFFFFFF;
                    var client = _Clients[index % _Clients.Length];//OR & mask
                    return client.SendAsync(req);
                });
            gatewaySvr.Start();

            var httpSvr1 = new TcpServer(9301);
            httpSvr1.UseHttp((options, router) => {
                router.MapGet("/{*path}", (req, resp) => {
                    req.Headers.TryGetValue("X-Real-IP", out var ip);
                    resp.Content = StringContent.Create("FROM:9301");
                });
            });
            httpSvr1.Start();
            var httpSvr2 = new TcpServer(9302);
            httpSvr2.UseHttp((options, router) => {
                router.MapGet("/{*path}", (req, resp) => {
                    req.Headers.TryGetValue("X-Real-IP", out var ip);
                    resp.Content = StringContent.Create("FROM:9302");
                });
            });
            httpSvr2.Start();
            var httpSvr3 = new TcpServer(9303);
            httpSvr3.UseHttp((options, router) => {
                router.MapGet("/{*path}", (req, resp) => {
                    req.Headers.TryGetValue("X-Real-IP", out var ip);
                    resp.Content = StringContent.Create("FROM:9303");
                });
            });
            httpSvr3.Start();

            var string1 = HttpClient.Default.GetStringAsync("http://localhost:9399/").Result;
            var string2 = HttpClient.Default.GetStringAsync("http://localhost:9399/").Result;
            var string3 = HttpClient.Default.GetStringAsync("http://localhost:9399/").Result;
            var string4 = HttpClient.Default.GetStringAsync("http://localhost:9399/").Result;
            var string5 = HttpClient.Default.GetStringAsync("http://localhost:9399/").Result;
            var string6 = HttpClient.Default.GetStringAsync("http://localhost:9399/").Result;
            Console.WriteLine(string1);
            Console.WriteLine(string2);
            Console.WriteLine(string3);
            Console.WriteLine(string4);
            Console.WriteLine(string5);
            Console.WriteLine(string6);
        }


        private static HttpClient _Client16 = HttpClient.CreateHttp(() => ClientConnection.Create(new UnixDomainSocketEndPoint("UnixDomainSocket")), 2);
        public static void RunUnixDomainSocket()
        {
            try { File.Delete("UnixDomainSocket"); } catch { }

            var localEndPoint = new UnixDomainSocketEndPoint("UnixDomainSocket");
            var unixDomainSvr = new TcpServer(localEndPoint);
            unixDomainSvr.UseHttp((options, router) => {
                MapRouter(router);
            });
            unixDomainSvr.Start();


            Run(_Client16, "http://localhost").Wait();
        }


        //5 connections
        private static HttpClient _Client17 = HttpClient.CreateHttp("localhost", 9299, 5);//1 request Queue
        private static HttpClient _Client18 = new ParallelClient(() => HttpClient.CreateHttp("localhost", 9299, 1), 5);//5 request Queue(Less lock competition, slightly better performance)
        public static void RunParallelClient() 
        {
            var testSvr = new TcpServer(9299);
            testSvr.UseHttp((options, router) => {
                MapRouter(router);
            });
            testSvr.Start();

            Run(_Client17, "http://localhost:9299").Wait();


            Run(_Client18, "http://localhost:9299").Wait();
        }


        private static void MapRouter(HttpRouter router)
        {
            router.MapGet("/", (req, resp) => {
                resp.Headers.Add(HttpHeaders.ContentType, "text/html; charset=utf-8");
                resp.Content = StringContent.Create("This is Index");
            });


            File.WriteAllText("testFile.txt", "this is file content.BY 张贺", new UTF8Encoding(false));//No BOM
            router.MapGet("/File", (req, resp) => {
                resp.UseFile(req, "testFile.txt");
            });

            router.MapPost("/Json", async (req, resp) => {
                resp.UseCookie("cookieName1", "cookieValue1");
                resp.UseCookie("cookieName2", "cookieValue2");
                var model = await req.Content.ReadJsonAsync<ReqModel>();
                Console.WriteLine(model);
                resp.UseJson(new RespModel()
                {
                    Code = 0,
                    Message = "ok"
                });
            });
            router.MapPost("/Form", async (req, resp) => {
                await FeaturesExtensions.ReadFormAsync(req, maxForm: int.MaxValue, maxFormData: int.MaxValue);
                var form = req.FormParams();
                Console.WriteLine(form.Count);
                resp.UseRedirect("/");
            });
            router.MapPost("/FormData", async (req, resp) => {
                await FeaturesExtensions.ReadFormAsync(req, maxForm: int.MaxValue, maxFormData: int.MaxValue);
                var form = req.FormParams();
                Console.WriteLine(form.Count);
                var files = req.FormFileParams();
                Console.WriteLine(files.Count);
                resp.UseRedirect("/");
            });
        }
        private static async Task Run(HttpClient client, string url)
        {

            var string1 = await client.GetStringAsync($"{url}/");
            Console.WriteLine(string1);


            var string2 = await client.PostStringAsync($"{url}/Form", new FormParams() {
                { "formName1","formValue1"},
                { "formName2","formValue2"},
                { "formName3","formValue3"}
            });
            Console.WriteLine(string2);


            //client.GetFileAsync()
            var stream1 = await client.GetStreamAsync($"{url}/File");
            Console.WriteLine(stream1.Length);
            stream1.Close();


            //_Client1.GetJsonAsync()
            var model1 = await client.PostJsonAsync<ReqModel, RespModel>($"{url}/Json", new ReqModel()
            {
                Name = "Zhang",
                Age = 30
            });
            Console.WriteLine(model1.Code);
            Console.WriteLine(model1.Message);


            var req1 = new HttpRequest($"{url}/");
            //req1.Method = HttpMethod.Get;
            await client.SendAsync(req1, async (resp) => {
                var @string = await resp.Content.ReadStringAsync();
                Console.WriteLine(@string);
            });


            var req2 = new HttpRequest($"{url}/");
            try
            {
                var resp2 = await client.SendAsync(req2);
                var @string = await resp2.Content.ReadStringAsync();
                Console.WriteLine(@string);
            }
            finally
            {
                req2.Dispose();
            }


            var req3 = new HttpRequest($"{url}/Form");
            req3.UseForm(new FormParams()
            {
                { "formName1","formValue1"},
                { "formName2","formValue2"}
            });
            await client.SendAsync(req3, async (resp) => {
                if (resp.Headers.TryGetValue(HttpHeaders.Location, out var location))
                {
                    Console.WriteLine(location);
                }
                else
                {
                    //Redirect /
                    var @string = await resp.Content.ReadStringAsync();
                    Console.WriteLine(@string);
                }
            });


            var req4 = new HttpRequest($"{url}/FormData");
            var formParam4 = new FormParams()
            {
                { "formName1","formValue1"},
                { "formName2","formValue2"}
            };
            var files4 = new FormFileParams();
            files4.Add("file1", "testFile.txt");
            files4.Add("file2", "testFile.txt");
            req4.UseFormData(formParam4, files4);
            await client.SendAsync(req4, async (resp) => {
                if (resp.Headers.TryGetValue(HttpHeaders.Location, out var location))
                {
                    Console.WriteLine(location);
                }
                else
                {
                    //Redirect /
                    var @string = await resp.Content.ReadStringAsync();
                    Console.WriteLine(@string);
                }
            });


            var req5 = new HttpRequest($"{url}/Json");
            req5.UseJson(new ReqModel
            {
                Name = "Name1",
                Age = int.MaxValue
            });
            await client.SendAsync(req5, async (resp) => {
                var model = await resp.Content.ReadJsonAsync<RespModel>();
                Console.WriteLine(model.Code);
                Console.WriteLine(model.Message);
            });
        }

        public class ReqModel
        {
            public string Name { get; set; }
            public int Age { get; set; }
        }
        public class RespModel
        {
            public int Code { get; set; }
            public string Message { get; set; }
        }
        public class ParallelClient : HttpClient
        {
            private volatile int _index = -1;
            private HttpClient[] _clients;//OR use weight([1,2,3,1,2,1,1])
            public ParallelClient(Func<HttpClient> factory, int count) 
            {
                _clients = new HttpClient[count];

                for (int i = 0; i < count; i++)
                {
                    _clients[i] = factory();
                }
            }
            public override Task<HttpResponse> SendAsync(HttpRequest request)
            {
                var index = Interlocked.Increment(ref _index) & 0x7FFFFFFF;
                Console.WriteLine($"Client:{index}");
                var client = _clients[index % _clients.Length];//OR 2 Power & mask
                return client.SendAsync(request);
            }
        }

        static HttpServerClientSample() 
        {
            if (!File.Exists("server.pfx")) //Create Test Cert
            {
                using (var rsa = RSA.Create(2048))
                {
                    var certReq = new CertificateRequest($"CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                    certReq.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
                    certReq.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation | X509KeyUsageFlags.KeyEncipherment, true));
                    var altName = new SubjectAlternativeNameBuilder();
                    altName.AddDnsName("localhsot");
                    certReq.CertificateExtensions.Add(altName.Build(true));
                    var cert = certReq.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1));
                    File.WriteAllBytes("server.pfx", cert.Export(X509ContentType.Pfx, "123456"));
                }
            }
        }
    }
}
