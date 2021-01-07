using System;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Extensions.Http;
using System.Runtime.Serialization;
using System.Extensions.Net;
using System.Linq.Expressions;

namespace BasicSample
{
    public class HandlerCompilerSample
    {
        public static void Run()
        {
            var compiler = new HandlerCompiler();
            compiler.Register<Object1>(new Object1());//Singleton
            compiler.Register<Object2>(() => new Object2());
            compiler.Register<IHttp2Pusher>(req => req.Pusher());
            compiler.RegisterAsync<AsyncObject>(async (req) =>
            {
                await Task.Delay(1000);
                return new AsyncObject();
            });
            //泛型
            //Generic
            compiler.Register((type, req) =>
            {
                if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(GenericObject<>))
                    return null;

                var ctor = type.GetConstructor(Type.EmptyTypes);
                return Expression.New(ctor);
            });

            compiler.RegisterParameterAsync<string>(p => p.Name == "test", async (req) =>
            {
                await Task.CompletedTask;
                return "this is string1";
            });

            compiler.RegisterProperty<string>(p => p.Name == "TestString", _ => "this is string2");

            //compiler.RegisterParameter();
            //compiler.RegisterProperty();

            compiler.RegisterReturn<string>((value, req, resp) =>
            {
                resp.Headers.Add(HttpHeaders.ContentType, "text/plain");
                resp.Content = StringContent.Create(value);
            });
            compiler.RegisterReturnAsync<byte[]>(async (value, req, resp) =>
            {
                await Task.CompletedTask;
                resp.Headers[HttpHeaders.ContentType] = "application/octet-stream";
                resp.Content = MemoryContent.Create(value);
            });


            var h1 = compiler.Compile(typeof(TestService).GetMethod("Test1"));
            var pathParams1 = new PathParams()
            {
                { "pathParam1","ZhangHe"},
                { "pathParam2","9999"}
            };
            var req1 = new HttpRequest().PathParams(pathParams1);
            var resp1 = h1.HandleAsync(req1).Result;

            Console.WriteLine();
            Console.WriteLine();
            var h2 = compiler.Compile(typeof(TestService).GetMethod("Test2"));
            var resp2 = h2.HandleAsync(new HttpRequest()).Result;
            Console.WriteLine($"Content:{resp2.Content.ReadStringAsync().Result}");

            Console.WriteLine();
            Console.WriteLine();
            var h3 = compiler.Compile(typeof(TestService).GetMethod("Test3"));
            var resp3 = h3.HandleAsync(new HttpRequest()).Result;
            Console.WriteLine($"Content:{resp3.Content.ReadStringAsync().Result}");

            Console.WriteLine();
            Console.WriteLine();
            var h4 = compiler.Compile(typeof(TestService).GetMethod("Test4"));
            var resp4 = h4.HandleAsync(new HttpRequest()).Result;
            Console.WriteLine($"Content:{resp4.Content.ReadStringAsync().Result}");

            Console.WriteLine();
            Console.WriteLine();
            var h5 = compiler.Compile(typeof(TestService2).GetMethod("Test"));
            var resp5 = h1.HandleAsync(new HttpRequest()).Result;
            foreach (var item in resp5.Headers)
            {
                Console.WriteLine($"{item.Key}={item.Value}");
            }


            //------------------------------------------------------------------------

            var compiler1 = new HandlerCompiler();
            QueryParamAttribute.Register(compiler1);
            QueryParamsAttribute.Register(compiler1);
            QueryAndFormAttribute.Register(compiler1);

            Console.WriteLine();
            Console.WriteLine();
            var h6 = compiler1.Compile(typeof(TestService).GetMethod("Test5"));
            var queryParams6 = new QueryParams()
            {
                { "Name","ZhangSan"},
                { "Age","100"}
            };
            var req6 = new HttpRequest().QueryParams(queryParams6);
            var resp6 = h6.HandleAsync(req6).Result;

            Console.WriteLine();
            Console.WriteLine();
            var h7 = compiler1.Compile(typeof(TestService).GetMethod("Test6"));
            var queryParam7 = new QueryParams()
            {
                { "Name","ZhangSan"}
            };
            var formParams7 = new FormParams()
            {
                { "Age","100"}
            };
            var req7 = new HttpRequest().QueryParams(queryParam7).FormParams(formParams7);
            var resp7 = h7.HandleAsync(req7).Result;
        }
    }
    public class TestService
    {
        public void Test1(
            HttpRequest request,
            HttpResponse response,
            IPathParams pathParams,
            string pathParam1,
            int? pathParam2,
            IQueryParams queryParams,
            IFormParams formParams,
            IFormFileParams files,
            IFormFile uplaod,
            [ReadJson]
            JsonObject jsonObject
            )
        {
            Console.WriteLine($"HttpRequest:{request}");
            Console.WriteLine($"HttpResponse:{response}");
            Console.WriteLine($"IPathParams:{pathParams}");
            Console.WriteLine(pathParam1);
            Console.WriteLine(pathParam2);
            Console.WriteLine($"IQueryParams:{queryParams}");
            Console.WriteLine($"IFormParams:{formParams}");
            Console.WriteLine($"IFormFileParams:{files}");
            Console.WriteLine($"IFormFile:{uplaod}");
            Console.WriteLine($"JsonObject:{jsonObject}");

            response.UseCookie("CookieName", "CookieValue");
            response.UseRedirect("/");
        }
        public string Test2(Object1 o1, Object2 o2, IHttp2Pusher pusher, AsyncObject o3, GenericObject<string> o4)
        {
            Console.WriteLine($"Object1:{o1}");
            Console.WriteLine($"Object2:{o2}");
            Console.WriteLine($"IHttp2Pusher:{pusher}");
            Console.WriteLine($"AsyncObject:{o3}");
            Console.WriteLine($"GenericObject<string>:{o4}");
            return "String";
        }

        public JsonData1 Test3()
        {
            return new JsonData1()
            {
                Code = 1000,
                Message = "MMMMMMM"
            };
        }
        public JsonData2<Person1> Test4()
        {
            return new JsonData2<Person1>()
            {
                Code = 1000,
                Message = "MMMMMMM",
                Data = new Person1()
                {
                    Name = "ZhangHe",
                    Age = int.MaxValue
                }
            };
        }

        public void Test5([QueryParam("Name")]string name, [QueryParam]int Age, [QueryParams]Person1 person) 
        {
            Console.WriteLine("QueryParam:");
            Console.WriteLine(name);
            Console.WriteLine(Age);
            Console.WriteLine("QueryParams:");
            Console.WriteLine(person.Name);
            Console.WriteLine(person.Age);
        }

        public void Test6([QueryAndForm]Person1 person)
        {
            Console.WriteLine("QueryAndForm");
            Console.WriteLine(person.Name);
            Console.WriteLine(person.Age);
        }
    }
    public class TestService2
    {
        private IQueryParams _queryParams;
        private AsyncObject _asyncObject;
        public TestService2(IQueryParams queryParams, AsyncObject asyncObject)
        {
            _queryParams = queryParams;
            _asyncObject = asyncObject;
        }
        public HttpResponse Response { get; set; }
        public AsyncObject AsyncObject { get; set; }
        public IQueryParams QueryParams { get; set; }
        public string TestString { get; set; }
        public void Test(IQueryParams queryParams, AsyncObject asyncObject, string test)
        {
            Console.WriteLine(DateTime.Now);
            Console.WriteLine();
            Response.UseCookie("session", "123456");
        }
    }

    public class Object1
    {

    }
    public class Object2
    {

    }
    public class AsyncObject
    {

    }
    public class GenericObject<T>
    {
        public T Value { get; set; }
    }
    public class JsonObject
    {

    }

    public class JsonData1
    {
        public int Code { get; set; }
        public string Message { get; set; }
        [DataMember(EmitDefaultValue = false)]
        public object Data { get; set; }
        public void Invoke(HttpRequest request, HttpResponse response)
        {
            response.UseJson(this);
        }

    }
    public class JsonData2<T>
    {
        public int Code { get; set; }
        public string Message { get; set; }
        [DataMember(EmitDefaultValue = false)]
        public T Data { get; set; }
        public async Task Invoke(HttpRequest request, HttpResponse response)
        {
            await Task.CompletedTask;
            response.UseJson(this);
        }
    }
    public class Person1
    {
        public string Name { get; set; }
        public int Age { get; set; }
    }
    //I don't like this
    public class QueryParamAttribute : Attribute 
    {
        private string _name;
        public QueryParamAttribute() { }
        public QueryParamAttribute(string name)
        {
            _name = name;
        }
        public string Name => _name;
        public static void Register(HandlerCompiler compiler) 
        {
            compiler.RegisterParameter((type, parameter, request) => {
                var queryParamAttribute = parameter.GetCustomAttribute<QueryParamAttribute>();
                if (queryParamAttribute == null)
                    return null;

                var name = queryParamAttribute.Name ?? parameter.Name;
                //FeaturesExtensions.QueryParams(request).GetValue<T>(name);
                var queryParams = typeof(FeaturesExtensions).GetMethod("QueryParams", new[] { typeof(HttpRequest) });
                var getValue = typeof(FeaturesExtensions).GetMethod("GetValue",1, new[] { typeof(IQueryParams), typeof(string) }).MakeGenericMethod(parameter.ParameterType);
                return Expression.Call(getValue, Expression.Call(queryParams, request), Expression.Constant(name));
                //TODO
                //TryGetValue()=>parameter.DefaultValue
            });
        }
    }
    public class QueryParamsAttribute : Attribute
    {
        public static void Register(HandlerCompiler compiler)
        {
            compiler.RegisterParameter((type, parameter, request) => {
                var queryParamsAttribute = parameter.GetCustomAttribute<QueryParamsAttribute>();
                if (queryParamsAttribute == null)
                    return null;

                //FeaturesExtensions.QueryParams(request).GetValue<T>();
                var queryParams = typeof(FeaturesExtensions).GetMethod("QueryParams", new[] { typeof(HttpRequest) });
                var getValue = typeof(FeaturesExtensions).GetMethod("GetValue",1, new[] { typeof(IQueryParams) }).MakeGenericMethod(parameter.ParameterType);
                return Expression.Call(getValue, Expression.Call(queryParams, request));
            });
        }
    }
    public class QueryAndForm 
    {
        private IQueryParams _queryParams;
        private IFormParams _formParams;
        public QueryAndForm(IQueryParams queryParams,IFormParams formParams) 
        {
            _queryParams = queryParams;
            _formParams = formParams;
        }
        public int Count => _queryParams.Count + _formParams.Count;
        public KeyValuePair<string, string> this[int index] 
        {
            get 
            {
                if (index < _queryParams.Count)
                {
                    return _queryParams[index];
                }
                else 
                {

                    return _formParams[index - _queryParams.Count];
                }
            }
        }
    }
    public class QueryAndFormAttribute : Attribute
    {
        public static void Register(HandlerCompiler compiler)
        {
            compiler.RegisterParameter((type, parameter, request) => {
                var queryAndFormAttribute = parameter.GetCustomAttribute<QueryAndFormAttribute>();
                if (queryAndFormAttribute == null)
                    return null;

                //FeaturesExtensions.GetValue<QueryAndForm, T>()(new QueryAndForm(FeaturesExtensions.QueryParams(request), FeaturesExtensions.FormParams(request)));
                var handler = typeof(FeaturesExtensions).GetMethod("GetValue", 2, Type.EmptyTypes).MakeGenericMethod(typeof(QueryAndForm), parameter.ParameterType).Invoke(null, null);
                var queryParams = typeof(FeaturesExtensions).GetMethod("QueryParams", new[] { typeof(HttpRequest) });
                var formParams = typeof(FeaturesExtensions).GetMethod("FormParams", new[] { typeof(HttpRequest) });
                var ctor = typeof(QueryAndForm).GetConstructor(new[] { typeof(IQueryParams), typeof(IFormParams) });
                return Expression.Invoke(Expression.Constant(handler), Expression.New(ctor, Expression.Call(queryParams, request), Expression.Call(formParams, request)));
            });
        }
    }
}