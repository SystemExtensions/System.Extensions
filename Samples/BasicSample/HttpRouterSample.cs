using System;
using System.Text;
using System.IO;
using System.Extensions.Http;
using System.Net.Mime;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BasicSample
{
    public class HttpRouterSample
    {
        public static void Run()
        {
            var router = new HttpRouter();
            //var getTree= router.GetTree;
            //var postTree = router.PostTree;
            //var headTree = router.HeadTree;
            //var putTree = router.PutTree;
            //var deleteTree = router.DeleteTree;

            router.MapGet("/get/index", (req, resp) => Console.WriteLine("/get/index"));
            //不支持参数约束,前缀,后缀 繁琐而且用处不大
            //Not support parameter constraints,prefix,suffix tedious and useless
            router.MapGet("/get/{param1}/{param2}", (req, resp) => Console.WriteLine("/get/{param1}/{param2}"));
            router.MapGet("/get/{*catchAll}", (req, resp) => Console.WriteLine("/get/{*catchAll}"));
            Console.WriteLine("MapGet");
            foreach (var item in router.GetTree)
            {
                Console.WriteLine(item.Key);
            }

            //MapAttribute
            var compiler = new HandlerCompiler();//See HandlerCompilerSample
            //compiler.Register()
            router.MapAttribute(new[] { typeof(TestService) }, compiler);
            //customize
            router.MapAttribute(new[] { typeof(TestService) }, compiler,
                (method, typeHandlers, methodHandlers, handler) =>
                {
                    var handlers = new List<IHttpHandler>();
                    handlers.Add(HttpHandler.CreateModule((req, handler) =>
                    {
                        Console.WriteLine("Before typeHandlers");
                        return handler.HandleAsync(req);
                    }));
                    handlers.AddRange(typeHandlers);
                    handlers.Add(HttpHandler.CreateModule((req, handler) =>
                    {
                        Console.WriteLine("Before methodHandlers");
                        return handler.HandleAsync(req);
                    }));
                    handlers.AddRange(methodHandlers);
                    handlers.Add(handler);
                    return HttpHandler.CreatePipeline(handlers);
                });
            //router.MapAttribute(compiler);
            //router.MapAttribute(handlerDelegate)
            Console.WriteLine();
            Console.WriteLine("MapAttribute");
            foreach (var item in router.GetTree)
            {
                Console.WriteLine(item.Key);
            }


            Directory.CreateDirectory("Static");
            File.WriteAllText("Static/testFile.txt", "this is file content.BY 张贺", new UTF8Encoding(false));
            File.WriteAllText("Static/testHtml1.html", "<h1>testHtml1<h1>", new UTF8Encoding(false));
            File.WriteAllText("Static/testHtml2.html", "<h2>testHtml2<h2>", new UTF8Encoding(false));
            //MapFile
            router.MapFile("/testFile1", "Static/testFile.txt", 86400);//CacheControl 
            router.MapFile("/testFile2", "Static/testFile.txt", "text/html; charset=utf-8", 86400);
            //MapFiles
            router.MapFiles("/static1/{*path}", "Static", 86400);
            var customMimeTypes = new MimeTypes();
            //var customMimeTypes = new MimeTypes(MimeTypes.Default);
            customMimeTypes.Add(".html", "text/html; charset=utf-8");
            router.MapFiles("/static2/{*customName}", "Static", customMimeTypes, 86400, "customName");
            //router.MapFiles("/static2/{*customName}", "Static", MimeTypes.Default, TimeSpan.FromDays(1), "customName");


            //MapSlash
            //尾部/
            router.GetTree.MapSlash();
            //router.MapSlash();
            Console.WriteLine();
            Console.WriteLine("MapSlash");
            foreach (var item in router.GetTree)
            {
                Console.WriteLine(item.Key);
            }

            //动态路由
            //Dynamic change router
            //CopyOnWrite(Safe)
            var newGetTree = new HttpRouter.Tree();
            newGetTree.Map("/new/index", HttpHandler.Create((req, resp) => { Console.WriteLine("/new/index"); }));
            newGetTree.Map("/new/{param1}/{param2}", HttpHandler.Create((req, resp) => { Console.WriteLine("/new/{param1}/{param2}"); }));
            newGetTree.Map("/new/{*catchAll}", HttpHandler.Create((req, resp) => { Console.WriteLine("/new/{*catchAll}"); }));
            newGetTree.MapSlash();
            newGetTree.MapTree(router.GetTree);
            router.GetTree = newGetTree;
            Console.WriteLine();
            Console.WriteLine("NewGetTree");
            foreach (var item in router.GetTree)
            {
                Console.WriteLine(item.Key);
            }

            //Match
            Console.WriteLine();
            Console.WriteLine("Match");
            var params1 = new PathParams();
            var h1 = router.GetTree.Match("/attribute/index", params1);
            Console.WriteLine(params1.Count);
            var params2 = new PathParams();
            var h2 = router.GetTree.Match("/attribute/p1/x/y", params2);
            Console.WriteLine(params2.Count);
            var params3 = new PathParams();
            var h3 = router.GetTree.Match("/attribute/catchAll/x/y/z//", params3);
            Console.WriteLine(params3.Count);

            //HandleAsync
            Console.WriteLine();
            Console.WriteLine("HandleAsync");
            var req1 = new HttpRequest("/attribute/index") { Method = HttpMethod.Get };
            var resp1 = router.HandleAsync(req1).Result;
            var req2 = new HttpRequest("/attribute/p1/x/y") { Method = HttpMethod.Get };
            var resp2 = router.HandleAsync(req2).Result;
            var req3 = new HttpRequest("/attribute/catchAll/x/y/z//") { Method = HttpMethod.Get };
            var resp3 = router.HandleAsync(req3).Result;

            var req4 = new HttpRequest("/testFile1") { Method = HttpMethod.Get };
            var resp4 = router.HandleAsync(req4).Result;
            Console.WriteLine(resp4.Content.ReadStringAsync().Result);
            var req5 = new HttpRequest("/testFile2") { Method = HttpMethod.Head };
            var resp5 = router.HandleAsync(req5).Result;
            Console.WriteLine(resp5.Content.ReadStringAsync().Result);
            var req6 = new HttpRequest("/static1/testHtml1.html") { Method = HttpMethod.Get };
            var resp6 = router.HandleAsync(req6).Result;
            Console.WriteLine(resp6.Content.ReadStringAsync().Result);
            var req7 = new HttpRequest("/static2/testHtml2.html") { Method = HttpMethod.Get };
            var resp7 = router.HandleAsync(req7).Result;
            Console.WriteLine(resp7.Content.ReadStringAsync().Result);


            //------------------------------------------------------------------------
            //router chain(HttpRouter is IHttpHandler)
            var router1 = new HttpRouter();
            var router2 = new HttpRouter();//var tree1 = new HttpRouter.Tree();
            router2.MapGet("/{*path}", (req, resp) => {
                Console.WriteLine(nameof(router2));
                Console.WriteLine(req.PathParams().GetValue<string>("path"));
            });
            router1.GetTree.Map("/Images/{*img}", router2);
            router1.GetTree.Map("/Js/{*js}", HttpHandler.Create(
                (req) => {
                    Console.WriteLine("Js");
                    return router2.HandleAsync(req);
                }));

            Console.WriteLine();
            var req8 = new HttpRequest("/Images/123456.png") { Method = HttpMethod.Get };
            var resp8 = router1.HandleAsync(req8).Result;
            var req9 = new HttpRequest("/Js/jq.js") { Method = HttpMethod.Get };
            var resp9 = router1.HandleAsync(req9).Result;

            //------------------------------------------------------------------------
            //special
            // /path1/{param1} Match /path1/  (if not Map /path1/)
            var router3 = new HttpRouter();
            router3.MapGet("/", (req, resp) => { Console.WriteLine("/"); });
            router3.MapGet("/{param1}", (req, resp) => { Console.WriteLine("/{param1}"); });
            var req10 = new HttpRequest("/") { Method = HttpMethod.Get };
            var resp10 = router3.HandleAsync(req10).Result;

            var router4 = new HttpRouter();
            //router4.MapGet("/", (req, resp) => { Console.WriteLine("/"); });
            router4.MapGet("/{param1}", (req, resp) => { Console.WriteLine("/{param1}"); });
            var req11 = new HttpRequest("/") { Method = HttpMethod.Get };
            var resp11 = router4.HandleAsync(req11).Result;

            // multiple /
            var router5 = new HttpRouter();
            router5.MapGet("////", (req, resp) => { Console.WriteLine("////"); });
            var req12 = new HttpRequest("////") { Method = HttpMethod.Get };
            var resp12 = router5.HandleAsync(req12).Result;
            router5.MapGet("/Path1/{param1}/{param2}/", (req, resp) => { Console.WriteLine("/Path1/{param1}/{param2}/"); });
            //OR /Path1/{param1}/{param2}/{param3}
            var req13 = new HttpRequest("/Path1///") { Method = HttpMethod.Get };
            var resp13 = router5.HandleAsync(req13).Result;

        }

        [MyLog]
        public class TestService
        {
            [Get("/attribute/index")]
            [MyLog]
            [MyModule]
            public void Index()
            {
                Console.WriteLine("Index");
            }
            [Get("/attribute/p1/{param1}/{param2}")]
            public void Param1(IPathParams pathParams)
            {
                var param1 = pathParams.GetValue<string>("param1");
                var param2 = pathParams.GetValue<string>("param2");
                Console.WriteLine($"Param1:{param1},{param2}");
            }
            [Get("/attribute/p2/{param1}/{param2}")]
            public void Param2(string param1, string param2)
            {
                Console.WriteLine($"Param2:{param1},{param2}");
            }
            [Get("/attribute/catchAll/{*path}")]
            public void CatchAll(string path)
            {
                Console.WriteLine($"CatchAll:{path}");
            }
        }
        public class MyLogAttribute : Attribute
        {
            //OR  public static IHttpModule Invoke()
            //OR  public IHttpModule Invoke()
            //OR  public IHttpModule Invoke(MethodInfo method)
            public static IHttpModule Invoke(MethodInfo method)
            {
                return HttpHandler.CreateModule((req, handler) => {
                    Console.WriteLine($"Log:{method.Name}");
                    return handler.HandleAsync(req);
                });
            }
        }
        public class MyModuleAttribute : Attribute
        {
            public IHttpModule Invoke()
            {
                return HttpHandler.CreateModule(async (request, handler) => {
                    Console.WriteLine("Do Request");
                    var response = await handler.HandleAsync(request);
                    Console.WriteLine("Do Response");
                    return response;
                });
            }
        }
    }
}