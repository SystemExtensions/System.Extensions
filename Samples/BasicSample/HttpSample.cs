using System;
using System.Text;
using System.Extensions.Http;
using System.IO;
using System.Threading.Tasks;
using System.Dynamic;

namespace BasicSample
{
    public class HttpSample
    {
        public static void Run()
        {
            //testFile
            var testFilePath = "testFile.txt";
            File.WriteAllText(testFilePath, "this is file content.BY 张贺", new UTF8Encoding(false));//No BOM


            var req1 = new HttpRequest("http://localhost:9999/GetJson?Query=Value#Fragment");
            Console.WriteLine(req1);

            var req2 = new HttpRequest("/GetJson?Query=Value#Fragment");
            req2.Url.Scheme = "http";
            req2.Url.Authority = "localhost:9999";

            var req3 = new HttpRequest();
            req3.Url.AbsoluteUri = "http://localhost:9999/GetJson?Query=Value#Fragment";

            var req4 = new HttpRequest();
            req4.Url.Scheme = "http";
            req4.Url.Authority = "localhost:9999";
            //OR
            //req4.Url.Host = "localhost";
            //req4.Url.Port = 9999;
            req4.Url.AbsolutePath = "/GetJson?Query=Value#Fragment";
            //OR
            //req4.Url.Path = "/GetJson";
            //req4.Url.Query = "?Query=Value";
            //req4.Url.Fragment = "#Fragment";

            //QueryParams
            req1.Method = HttpMethod.Get;
            req1.Version = HttpVersion.Version11;
            req1.Headers.Add(HttpHeaders.UserAgent, "My-Client");
            var queryParams1 = new QueryParams();
            queryParams1.Add("Name", "张贺");
            queryParams1.Add("Age", "10");
            queryParams1.Add("Sex", "");
            //OR
            //var queryParams1 = new QueryParams() 
            //{
            //    {"Name", "张贺"},
            //    {"Age", "10"},
            //    {"Sex", ""}
            //};
            queryParams1.Parse("TestKey1=TestValue1&TestKey2=TestValue2");
            queryParams1.Join(req1.Url);
            Console.WriteLine(req1);
            var queryParams2 = req1.QueryParams();
            foreach (var item in queryParams2)
            {
                Console.WriteLine($"{item.Key}={item.Value}");
            }
            var q1 = queryParams2.GetValue<string>("Name");
            var q2 = queryParams2.GetValue<int>("Age");
            var q3 = queryParams2.GetValue<int?>("Sex");
            var qModel1 = queryParams2.GetValue<Model>();
            var q4 = qModel1.Name;
            var q5 = qModel1.Age;
            var q6 = qModel1.Sex;
            dynamic qObj1 = queryParams2.GetValue<DynamicObject>();
            var q7 = (string)qObj1.Name;
            var q8 = (int)qObj1.Age;
            var q9 = (int?)qObj1.Sex;

            //FormParams
            req2.Method = HttpMethod.Post;
            req2.Version = HttpVersion.Version11;
            req2.Headers[HttpHeaders.UserAgent] = "My-Client";
            var formParams1 = new FormParams();
            formParams1.Add("Name", "张贺1");
            formParams1.Add("Age", "11");
            formParams1.Add("Sex", "");
            //OR
            //var formParams1 = new FormParams() 
            //{
            //    {"Name", "张贺1"},
            //    {"Age", "11"},
            //    {"Sex", ""}
            //};
            req2.UseForm(formParams1);
            //OR
            //req2.UseForm(formParams,Encoding.UTF8);
            Console.WriteLine(req2);
            //await
            FeaturesExtensions.ReadFormAsync(req2, 1024 * 1024, 10 * 1024 * 1024).Wait();//1M ,10M
            var formParams2 = req2.FormParams();
            foreach (var item in formParams2)
            {
                Console.WriteLine($"{item.Key}={item.Value}");
            }
            var f1 = formParams2.GetValue<string>("Name");
            var f2 = formParams2.GetValue<int>("Age");
            var f3 = formParams2.GetValue<int?>("Sex");
            var fModel1 = formParams2.GetValue<Model>();
            var f4 = fModel1.Name;
            var f5 = fModel1.Age;
            var f6 = fModel1.Sex;
            dynamic fObj1 = formParams2.GetValue<DynamicObject>();
            var f7 = (string)fObj1.Name;
            var f8 = (int)fObj1.Age;
            var f9 = (int?)fObj1.Sex;


            //FormParams FormFileParams
            var formParams3 = new FormParams();
            formParams3.Add("Key1", "Value1");
            formParams3.Add("姓名", "张贺");
            var fileParams1 = new FormFileParams();
            fileParams1.Add("Upload1", testFilePath);//MimeTypes.Default
            fileParams1.Add("Upload2", "myfile.html", "text/html", new FileInfo(testFilePath));
            fileParams1.Add("Upload3", new BytesFileParams(new byte[] { 1, 2, 3 }));//custom
            req3.UseFormData(formParams3, fileParams1);
            //OR
            //req3.UseFormData(formParams3, fileParams1, "my-random-boundary");
            //req3.UseFormData(formParams3, fileParams1, "my-random-boundary", Encoding.UTF8);
            Console.WriteLine(req3);
            //await
            FeaturesExtensions.ReadFormAsync(req3, 1024 * 1024, 10 * 1024 * 1024).Wait();//1M ,10M
            var formParams4 = req3.FormParams();
            foreach (var item in formParams4)
            {
                Console.WriteLine($"{item.Key}={item.Value}");
            }
            var fileParams2 = req3.FormFileParams();
            foreach (var item in fileParams2)
            {
                Console.WriteLine(item.Key);
                var upload = item.Value;
                Console.WriteLine(upload.Length);
                Console.WriteLine(upload.ContentType);
                Console.WriteLine(upload.FileName);
                upload.TryGetExtension(out var extName);
                Console.WriteLine(extName);
                //await upload.SaveAsync()
            }

            //Cookie
            req4.Headers.Add(HttpHeaders.Cookie, "cookieName1=cookieValue1; cookieName2=cookieValue2");
            var cookieParams1 = req4.CookieParams();
            foreach (var item in cookieParams1)
            {
                Console.WriteLine($"{item.Key}={item.Value}");
            }
            //cookieParams1.GetValue<>
            var sb1 = StringContent.Rent(out var disposable1);
            req4.RegisterForDispose(disposable1);
            sb1.Write("this ");
            sb1.Write(" is Request Content");
            req4.Content = StringContent.Create(sb1.Sequence);
            //OR MemoryContent.Rent()
            Console.WriteLine(req4.Content.ReadStringAsync().Result);//await

            var req5 = new HttpRequest();
            req5.UseJson(new { Data = "Request Json" });
            req5.Headers.TryGetValue(HttpHeaders.ContentType, out var contentType1);
            Console.WriteLine(contentType1);
            Console.WriteLine(req5.Content.ReadStringAsync().Result);//await

            req1.Dispose();
            req2.Dispose();
            req3.Dispose();
            req4.Dispose();
            req5.Dispose();


            var resp1 = new HttpResponse();
            resp1.Headers.Add(HttpHeaders.Server, "My-Web-Server");
            resp1.UseCookie("cookieName1","cookieValue1");
            resp1.UseCookie("cookieName2", "cookieValue2", domain: "localhost", maxAge: 1000, httpOnly: true, secure: true);
            var cookies1 = resp1.Headers.GetValues(HttpHeaders.SetCookie);
            foreach (var item in cookies1)
            {
                Console.WriteLine(item);
            }


            var resp2 = new HttpResponse();
            resp2.Headers[HttpHeaders.Server] = "My-Web-Server";
            resp2.UseRedirect("http://localhost:9999/Redirect");
            Console.WriteLine(resp2.StatusCode);//302
            resp2.Headers.TryGetValue(HttpHeaders.Location, out var location1);
            Console.WriteLine(location1);


            Console.WriteLine("UseFile");
            var resp3 = new HttpResponse();
            resp3.UseFile(testFilePath);
            Console.WriteLine(resp3.Content.Length);
            Console.WriteLine(resp3.Content.ReadStringAsync().Result);//await
            //UseFile(HttpRequest
            //ETag
            var resp4 = new HttpResponse();
            var etagReq = new HttpRequest();
            resp4.UseFile(etagReq,testFilePath);
            resp4.Headers.TryGetValue(HttpHeaders.AcceptRanges, out var acceptRanges);
            Console.WriteLine(acceptRanges);
            resp4.Headers.TryGetValue(HttpHeaders.ETag, out var etag1);
            Console.WriteLine(etag1);
            //Range
            var resp5 = new HttpResponse();
            var rangeReq = new HttpRequest();
            rangeReq.Headers.Add(HttpHeaders.Range, "bytes=0-3");
            resp5.UseFile(rangeReq, testFilePath);
            Console.WriteLine(resp5.StatusCode);//206 Partial Content
            Console.WriteLine(resp5.Content.Length);
            Console.WriteLine(resp5.Content.ReadStringAsync().Result);//await

            var resp6 = new HttpResponse();
            resp6.UseJson(new { Code = 0, Msg = "OK" });
            Console.WriteLine(resp6.Content.ReadStringAsync().Result);//await

            var resp7 = new HttpResponse();
            var sb2 = StringContent.Rent(out var disposable2);
            resp7.RegisterForDispose(disposable2);
            sb1.Write("this ");
            sb1.Write(" is Response Content");
            resp7.Content = StringContent.Create(sb1.Sequence);
            //OR MemoryContent.Rent()
            resp7.Headers.Add(HttpHeaders.ContentType, "text/html; charset=utf-8");
            Console.WriteLine(resp7.Content.ReadStringAsync().Result);//await

            //Compression
            //resp.UseCompression("gzip", 9);//0-9
            //resp.UseCompression("deflate", 9);//0-9
            //resp.UseCompression("br", 11);//0-11

            resp1.Dispose();
            resp2.Dispose();
            resp3.Dispose();
            resp4.Dispose();
            resp5.Dispose();
            resp6.Dispose();
            resp7.Dispose();

            //FeaturesExtensions.RegisterForDispose(req1, resp1);


            //Excpetion
            FeaturesExtensions.UseException((req, resp, ex) => {
                resp.StatusCode = ex.StatusCode() ?? 500;
                resp.Content = StringContent.Create("ERROR");
                return Task.CompletedTask;
            });

            //FeaturesExtensions.GetEncoding();
            //FeaturesExtensions.GetReasonPhrase();
            //FeaturesExtensions.GetValue();Register

            Console.WriteLine(HttpVersion.Version9);
            Console.WriteLine(HttpVersion.Version10);
            Console.WriteLine(HttpVersion.Version11);
            Console.WriteLine(HttpVersion.Version20);
            Console.WriteLine(HttpMethod.Get);
            Console.WriteLine(HttpMethod.Put);
            Console.WriteLine(HttpMethod.Post);
            Console.WriteLine(HttpMethod.Head);
            Console.WriteLine(HttpMethod.Trace);
            Console.WriteLine(HttpMethod.Patch);
            Console.WriteLine(HttpMethod.Delete);
            Console.WriteLine(HttpMethod.Options);
            Console.WriteLine(HttpMethod.Connect);
            Console.WriteLine(FeaturesExtensions.GetReasonPhrase(200));
            Console.WriteLine(FeaturesExtensions.GetReasonPhrase(404));

            var queryParam2 = new QueryParams();
            var url = new Url("http://localhost:9999/Test?q1=v1&q2=v2");
            queryParam2.Parse(url);
            queryParam2.Parse("q3=v3&q4=v4");
            foreach (var item in queryParam2)
            {
                Console.WriteLine($"{item.Key}={item.Value}");
            }


            //HttpHeaders
            Console.WriteLine(HttpHeaders.Accept);
            Console.WriteLine(HttpHeaders.AcceptCharset);
            Console.WriteLine(HttpHeaders.AcceptEncoding);
            Console.WriteLine(HttpHeaders.AcceptLanguage);
            Console.WriteLine(HttpHeaders.AcceptRanges);
            Console.WriteLine(HttpHeaders.AccessControlAllowOrigin);
            //....
            Console.WriteLine(HttpHeaders.Warning);
            Console.WriteLine(HttpHeaders.WwwAuthenticate);

            var contentType2 = "text/json; charset=utf-8; q=0.9; v=1.1";
            HttpHeaders.TryParse(contentType2, out var h1);
            Console.WriteLine(new string(h1));
            HttpHeaders.TryParse(contentType2, out var h2, "charset", out var hv2);
            Console.WriteLine(new string(h2));
            Console.WriteLine(new string(hv2));
            HttpHeaders.TryParse(contentType2, out var h3, "charset", out var hv3,"v",out var hvv3);
            Console.WriteLine(new string(h3));
            Console.WriteLine(new string(hv3));
            Console.WriteLine(new string(hvv3));

            var accept1 = "text/html;q=,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3";
            var acceptSpan1 = accept1.AsSpan();
            while (HttpHeaders.TryParse(ref acceptSpan1, out var a1, "q", out var aq1, "v", out var av1))
            {
                Console.WriteLine(new string(a1));
                if (aq1 != null)
                    Console.WriteLine("q=" + new string(aq1));
                if (av1 != null)
                    Console.WriteLine("v=" + new string(av1));
            }

            //IHttpContent
            IHttpContent testContent = StringContent.Create("{\"Name\":\"Zhang\",\"Age\":30}");

            Console.WriteLine(testContent.Length);
            Console.WriteLine(testContent.Available);
            var length1= testContent.ComputeLength();//TryComputeLength
            Console.WriteLine(testContent.Length);

            var b1= testContent.ReadAsync(new byte[1024]);
            var b2 = testContent.ReadAsync(new byte[1024]);
            Console.WriteLine(b1);
            Console.WriteLine(b2);

            Console.WriteLine(testContent.Available);
            testContent.Rewind();
            Console.WriteLine(testContent.Available);
            testContent.DrainAsync().Wait();
            Console.WriteLine(testContent.Available);

            //testContent.ReadJsonAsync<>()
            //testContent.ReadStringAsync();
            //testContent.ReadStreamAsync();
            //testContent.ReadFileAsync();
            //testContent.ReadFormAsync();
            //testContent.ReadFormDataAsync();


            //IHttpHandler
            var handler1 = HttpHandler.Create((req, resp) => {
                Console.WriteLine("handler1");
            });
            var handler2 = HttpHandler.Create(async (req, resp) => {
                await Task.CompletedTask;
                Console.WriteLine("handler2");
            });
            var handler3 = HttpHandler.Create((req) => {
                Console.WriteLine("handler3");
                return Task.FromResult(new HttpResponse());
            });
            var handler4 = HttpHandler.Create(async(req) => {
                await Task.CompletedTask;
                Console.WriteLine("handler4");
                return new HttpResponse();
            });
            handler1.HandleAsync(new HttpRequest()).Wait();
            handler2.HandleAsync(new HttpRequest()).Wait();
            handler3.HandleAsync(new HttpRequest()).Wait();
            handler4.HandleAsync(new HttpRequest()).Wait();


            var module1 = HttpHandler.CreateModule((req, handler) => {
                Console.WriteLine("module1");
                return handler.HandleAsync(req);
            });
            var module2 = HttpHandler.CreateModule(async(req, handler) => {
                var resp= await handler.HandleAsync(req);
                Console.WriteLine("module2");
                return resp;
            });
            var handler5 = HttpHandler.Create(async (req) => {
                await Task.CompletedTask;
                Console.WriteLine("handler5");
                return null;
            });
            var handler6 = HttpHandler.CreatePipeline(new[] { module1, module2, handler5, handler1 });
            handler6.HandleAsync(new HttpRequest()).Wait();
        }

        private class Model 
        {
            public string Name { get; set; }
            public int Age { get; set; }
            public int? Sex { get; set; }
        }
        private class BytesFileParams : IFormFile
        {
            public BytesFileParams(byte[] bytes) 
            {
                if (bytes == null)
                    throw new ArgumentNullException(nameof(bytes));

                _bytes = bytes;
            }
            private byte[] _bytes;
            public long Length => _bytes.Length;
            public string FileName => "";
            public string ContentType => "application/octet-stream";
            public Stream OpenRead()
            {
                return new MemoryStream(_bytes);
            }
            public Task SaveAsync(string filePath)
            {
                File.WriteAllBytes(filePath, _bytes);
                return Task.CompletedTask;
            }
        }
    }
}
