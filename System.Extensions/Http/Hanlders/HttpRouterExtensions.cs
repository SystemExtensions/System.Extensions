
namespace System.Extensions.Http
{
    using System.IO;
    using System.Reflection;
    using System.Net.Mime;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Runtime.Loader;
    public static class HttpRouterExtensions
    {
        public static HttpRouter.Tree MapSlash(this HttpRouter.Tree @this)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            var tempTree = new Dictionary<string, IHttpHandler>();
            foreach (var item in @this)
            {
                tempTree.Add(item.Key, item.Value);
            }
            var tempRouter = new List<KeyValuePair<string, IHttpHandler>>();
            foreach (var item in tempTree)
            {
                var template = item.Key;
                var index = template.LastIndexOf('/');
                if (index < template.Length - 1)
                {
                    if (!template.Substring(index + 1).StartsWith("{*"))
                    {
                        var newTemplate = item.Key + '/';
                        if (!tempTree.ContainsKey(newTemplate))
                        {
                            tempRouter.Add(new KeyValuePair<string, IHttpHandler>(newTemplate, item.Value));
                        }
                    }
                }
            }
            foreach (var item in tempRouter)
            {
                @this.Map(item.Key, item.Value);
            }
            return @this;
        }
        public static HttpRouter.Tree MapTree(this HttpRouter.Tree @this, HttpRouter.Tree tree)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (tree == null)
                throw new ArgumentNullException(nameof(tree));

            foreach (var item in tree)
            {
                @this.Map(item.Key, item.Value);
            }
            return @this;
        }
        public static HttpRouter MapSlash(this HttpRouter @this) 
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            @this.GetTree.MapSlash();
            @this.PostTree.MapSlash();
            @this.PutTree.MapSlash();
            @this.DeleteTree.MapSlash();
            @this.HeadTree.MapSlash();
            return @this;
        }
        public static HttpRouter MapGet(this HttpRouter @this, string template, Action<HttpRequest, HttpResponse> handler)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (template == null)
                throw new ArgumentNullException(nameof(template));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            @this.GetTree.Map(template, HttpHandler.Create(handler));
            return @this;
        }
        public static HttpRouter MapGet(this HttpRouter @this, string template, Func<HttpRequest, HttpResponse, Task> handler)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (template == null)
                throw new ArgumentNullException(nameof(template));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            @this.GetTree.Map(template, HttpHandler.Create(handler));
            return @this;
        }
        public static HttpRouter MapPost(this HttpRouter @this, string template, Action<HttpRequest, HttpResponse> handler)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (template == null)
                throw new ArgumentNullException(nameof(template));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            @this.PostTree.Map(template, HttpHandler.Create(handler));
            return @this;
        }
        public static HttpRouter MapPost(this HttpRouter @this, string template, Func<HttpRequest, HttpResponse, Task> handler)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (template == null)
                throw new ArgumentNullException(nameof(template));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            @this.PostTree.Map(template, HttpHandler.Create(handler));
            return @this;
        }
        public static HttpRouter MapPut(this HttpRouter @this, string template, Action<HttpRequest, HttpResponse> handler)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (template == null)
                throw new ArgumentNullException(nameof(template));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            @this.PutTree.Map(template, HttpHandler.Create(handler));
            return @this;
        }
        public static HttpRouter MapPut(this HttpRouter @this, string template, Func<HttpRequest, HttpResponse, Task> handler)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (template == null)
                throw new ArgumentNullException(nameof(template));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            @this.PutTree.Map(template, HttpHandler.Create(handler));
            return @this;
        }
        public static HttpRouter MapDelete(this HttpRouter @this, string template, Action<HttpRequest, HttpResponse> handler)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (template == null)
                throw new ArgumentNullException(nameof(template));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            @this.DeleteTree.Map(template, HttpHandler.Create(handler));
            return @this;
        }
        public static HttpRouter MapDelete(this HttpRouter @this, string template, Func<HttpRequest, HttpResponse, Task> handler)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (template == null)
                throw new ArgumentNullException(nameof(template));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            @this.DeleteTree.Map(template, HttpHandler.Create(handler));
            return @this;
        }
        public static HttpRouter MapHead(this HttpRouter @this, string template, Action<HttpRequest, HttpResponse> handler)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (template == null)
                throw new ArgumentNullException(nameof(template));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            @this.HeadTree.Map(template, HttpHandler.Create(handler));
            return @this;
        }
        public static HttpRouter MapHead(this HttpRouter @this, string template, Func<HttpRequest, HttpResponse, Task> handler)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (template == null)
                throw new ArgumentNullException(nameof(template));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            @this.HeadTree.Map(template, HttpHandler.Create(handler));
            return @this;
        }
        public static HttpRouter MapAttribute(this HttpRouter @this, HandlerCompiler compiler)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (compiler == null)
                throw new ArgumentNullException(nameof(compiler));

            var currentAssembly = typeof(HttpRouterExtensions).Assembly.GetName();
            var assemblies = AssemblyLoadContext.Default.Assemblies;
            foreach (var assembly in assemblies)
            {
                var referencedAssemblies = assembly.GetReferencedAssemblies();
                foreach (var referencedAssembly in referencedAssemblies)
                {
                    if (AssemblyName.ReferenceMatchesDefinition(currentAssembly, referencedAssembly))
                    {
                        MapAttribute(@this, assembly.GetExportedTypes(), compiler);
                        break;
                    }
                }
            }
            return @this;
        }
        public static HttpRouter MapAttribute(this HttpRouter @this, HandlerCompiler compiler, Func<MethodInfo, IHttpModule> moduleDelegate)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (compiler == null)
                throw new ArgumentNullException(nameof(compiler));

            var currentAssembly = typeof(HttpRouterExtensions).Assembly.GetName();
            var assemblies = AssemblyLoadContext.Default.Assemblies;
            foreach (var assembly in assemblies)
            {
                var referencedAssemblies = assembly.GetReferencedAssemblies();
                foreach (var referencedAssembly in referencedAssemblies)
                {
                    if (AssemblyName.ReferenceMatchesDefinition(currentAssembly, referencedAssembly))
                    {
                        MapAttribute(@this, assembly.GetExportedTypes(), compiler, moduleDelegate);
                        break;
                    }
                }
            }
            return @this;
        }
        public static HttpRouter MapAttribute(this HttpRouter @this, HandlerCompiler compiler, Func<MethodInfo, IHttpHandler[], IHttpHandler[], IHttpHandler, IHttpHandler> handlerDelegate)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (compiler == null)
                throw new ArgumentNullException(nameof(compiler));

            var currentAssembly = typeof(HttpRouterExtensions).Assembly.GetName();
            var assemblies = AssemblyLoadContext.Default.Assemblies;
            foreach (var assembly in assemblies)
            {
                var referencedAssemblies = assembly.GetReferencedAssemblies();
                foreach (var referencedAssembly in referencedAssemblies)
                {
                    if (AssemblyName.ReferenceMatchesDefinition(currentAssembly, referencedAssembly))
                    {
                        MapAttribute(@this, assembly.GetExportedTypes(), compiler, handlerDelegate);
                        break;
                    }
                }
            }
            return @this;
        }
        public static HttpRouter MapAttribute(this HttpRouter @this, Type[] types, HandlerCompiler compiler) 
        {
            return MapAttribute(@this, types, compiler,
                (method, typeHandlers, methodHandlers, handler) => {
                    var handlers = new List<IHttpHandler>();
                    handlers.AddRange(typeHandlers);
                    handlers.AddRange(methodHandlers);
                    handlers.Add(handler);
                    return HttpHandler.CreatePipeline(handlers);
                });
        }
        public static HttpRouter MapAttribute(this HttpRouter @this, Type[] types, HandlerCompiler compiler, Func<MethodInfo, IHttpModule> moduleDelegate)
        {
            return MapAttribute(@this, types, compiler,
                (method, typeHandlers, methodHandlers, handler) => {
                    var handlers = new List<IHttpHandler>();
                    handlers.AddRange(typeHandlers);
                    handlers.AddRange(methodHandlers);
                    handlers.Add(handler);
                    var module = moduleDelegate?.Invoke(method);
                    if (module == null)
                        return HttpHandler.CreatePipeline(handlers);
                    module.Handler = HttpHandler.CreatePipeline(handlers);
                    return module;
                });
        }
        public static HttpRouter MapAttribute(this HttpRouter @this, Type[] types, HandlerCompiler compiler, Func<MethodInfo, IHttpHandler[], IHttpHandler[], IHttpHandler, IHttpHandler> handlerDelegate)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (types == null)
                throw new ArgumentNullException(nameof(types));
            if (compiler == null)
                throw new ArgumentNullException(nameof(compiler));
            if (handlerDelegate == null)
                throw new ArgumentNullException(nameof(handlerDelegate));

            static IHttpHandler[] TypeHandlers(Type type, MethodInfo method)
            {
                var attributes = type.GetCustomAttributes(true);
                var handlers = new List<IHttpHandler>();
                foreach (var attribute in attributes)
                {
                    var handler = attribute.GetType().GetMethod("Invoke", new[] { typeof(MethodInfo) });
                    if (handler == null)
                    {
                        handler = attribute.GetType().GetMethod("Invoke", Type.EmptyTypes);
                        if (handler != null)
                        {
                            if (handler.IsStatic)
                            {
                                handlers.Add((IHttpHandler)handler.Invoke(null, null));
                            }
                            else
                            {
                                handlers.Add((IHttpHandler)handler.Invoke(attribute, null));
                            }
                        }
                    }
                    else if (handler.IsStatic)
                    {
                        handlers.Add((IHttpHandler)handler.Invoke(null, new[] { method }));
                    }
                    else
                    {
                        handlers.Add((IHttpHandler)handler.Invoke(attribute, new[] { method }));
                    }
                }
                return handlers.ToArray();
            }
            static IHttpHandler[] MethodHandlers(MethodInfo method)
            {
                var attributes = method.GetCustomAttributes(true);
                var handlers = new List<IHttpHandler>();
                foreach (var attribute in attributes)
                {
                    var handler = attribute.GetType().GetMethod("Invoke", new[] { typeof(MethodInfo) });
                    if (handler == null)
                    {
                        handler = attribute.GetType().GetMethod("Invoke", Type.EmptyTypes);
                        if (handler != null)
                        {
                            if (handler.IsStatic)
                            {
                                handlers.Add((IHttpHandler)handler.Invoke(null, null));
                            }
                            else
                            {
                                handlers.Add((IHttpHandler)handler.Invoke(attribute, null));
                            }
                        }
                    }
                    else if (handler.IsStatic)
                    {
                        handlers.Add((IHttpHandler)handler.Invoke(null, new[] { method }));
                    }
                    else
                    {
                        handlers.Add((IHttpHandler)handler.Invoke(attribute, new[] { method }));
                    }
                }
                return handlers.ToArray();
            }
            foreach (var type in types)
            {
                if (type == null)
                    continue;
                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                foreach (var method in methods)
                {
                    var attributes = method.GetCustomAttributes(false);
                    foreach (var attribute in attributes)
                    {
                        if (attribute is GetAttribute getAttribute)
                        {
                            var handler = compiler.Compile(method);
                            if (handler != null)
                                @this.GetTree.Map(getAttribute.Template, handlerDelegate(method, TypeHandlers(type, method), MethodHandlers(method), handler));
                        }
                        else if (attribute is PostAttribute postAttribute)
                        {
                            var handler = compiler.Compile(method);
                            if (handler != null)
                                @this.PostTree.Map(postAttribute.Template, handlerDelegate(method, TypeHandlers(type, method), MethodHandlers(method), handler));
                        }
                        else if (attribute is PutAttribute putAttribute)
                        {
                            var handler = compiler.Compile(method);
                            if (handler != null)
                                @this.PutTree.Map(putAttribute.Template, handlerDelegate(method, TypeHandlers(type, method), MethodHandlers(method), handler));
                        }
                        else if (attribute is DeleteAttribute deleteAttribute)
                        {
                            var handler = compiler.Compile(method);
                            if (handler != null)
                                @this.DeleteTree.Map(deleteAttribute.Template, handlerDelegate(method, TypeHandlers(type, method), MethodHandlers(method), handler));
                        }
                    }
                }
            }
            return @this;
        }
        public static HttpRouter MapFile(this HttpRouter @this, string template, string fileName, TimeSpan? maxAge)
        {
            if (!MimeTypes.Default.TryGetValue(fileName, out var mimeType))
                mimeType = "application/octet-stream";

            return MapFile(@this, template, fileName, mimeType, maxAge);
        }
        public static HttpRouter MapFile(this HttpRouter @this, string template, string fileName, string contentType, TimeSpan? maxAge)
        {
            var file = new FileInfo(fileName);
            if (!file.Exists)
                throw new FileNotFoundException(file.FullName);
            if (contentType == null)
                throw new ArgumentNullException(nameof(contentType));

            @this.GetTree.Map(template, new FileHandler(file, contentType, maxAge));
            @this.HeadTree.Map(template, new FileHandler(file, contentType, maxAge));
            return @this;
        }
        public static HttpRouter MapFiles(this HttpRouter @this, string template, string path, TimeSpan? maxAge)
        {
            return MapFiles(@this, template, path, null, maxAge, null);
        }
        public static HttpRouter MapFiles(this HttpRouter @this, string template, string path, MimeTypes mimeTypes, TimeSpan? maxAge, string subPathParam)
        {
            if (Path.EndsInDirectorySeparator(path))
                path += Path.DirectorySeparatorChar;

            var root = new DirectoryInfo(path);
            if (!root.Exists)
                throw new DirectoryNotFoundException(path);

            if (mimeTypes == null)
                mimeTypes = MimeTypes.Default;
            if (subPathParam == null)
                subPathParam = "path";

            @this.GetTree.Map(template, new FilesHandler(root, mimeTypes, maxAge, subPathParam));
            @this.HeadTree.Map(template, new FilesHandler(root, mimeTypes, maxAge, subPathParam));
            return @this;
        }

        #region private
        private class FileHandler : IHttpHandler
        {
            private FileInfo _file;
            private string _contentType;
            private string _maxAge;
            public FileHandler(FileInfo file, string contentType, TimeSpan? maxAge)
            {
                _file = file;
                _contentType = contentType;
                if (maxAge != null)
                    _maxAge = $"max-age={(long)maxAge.Value.TotalSeconds}";
            }
            public Task<HttpResponse> HandleAsync(HttpRequest request)
            {
                var response = request.CreateResponse();
                response.UseFile(request, _file, _contentType);
                if (_maxAge != null)
                    response.Headers.Add(HttpHeaders.CacheControl, _maxAge);
                return Task.FromResult(response);
            }
            public override string ToString() => _file.FullName;
        }
        private class FilesHandler : IHttpHandler
        {
            private DirectoryInfo _root;
            private string _subPathParam;
            private MimeTypes _mimeTypes;
            private string _maxAge;
            public FilesHandler(DirectoryInfo root, MimeTypes mimeTypes, TimeSpan? maxAge, string subPathParam)
            {
                _root = root;
                _mimeTypes = mimeTypes;
                _subPathParam = subPathParam;
                if (maxAge != null)
                    _maxAge = $"max-age={(long)maxAge.Value.TotalSeconds}";
            }
            public Task<HttpResponse> HandleAsync(HttpRequest request)
            {
                if (!request.PathParams().TryGetValue(_subPathParam,out var subPath))
                    return Task.FromResult<HttpResponse>(null);
                //TODO? prohibit RelativeSegments
                var file = _root.GetFile(subPath);
                if (!file.Exists)
                    return Task.FromResult<HttpResponse>(null);
                //if (!file.FullName.StartsWith(_path))//../  risk? Exhaustion Path
                //    return Task.FromResult<HttpResponse>(null);
                if (!_mimeTypes.TryGetValue(file.FullName, out var mimeType))
                    return Task.FromResult<HttpResponse>(null);

                var response = request.CreateResponse();
                response.UseFile(request, file, mimeType);
                if (_maxAge != null)
                    response.Headers.Add(HttpHeaders.CacheControl, _maxAge);
                return Task.FromResult(response);
            }
            public override string ToString() => _root.FullName;
        }
        #endregion

        //public static HttpRouter MapGet(this HttpRouter @this, string template, Action<HttpRequest, HttpResponse> handler)
        //{
        //    return @this;
        //}
        //public static HttpRouter MapGet(this HttpRouter @this, string template, Func<HttpRequest, HttpResponse, ValueTask> handler)
        //{
        //    return @this;
        //}
        //public static HttpRouter MapGet(this HttpRouter @this, string template, Func<HttpRequest, ValueTask<HttpResponse>> handler)
        //{
        //    return @this;
        //}
        //public static HttpRouter MapAttribute(this HttpRouter @this, Action<HandlerCompiler> compilerDelegate)
        //{
        //    var compiler = new HandlerCompiler();
        //    compilerDelegate.Invoke(compiler);
        //    return MapAttribute(@this, compiler);
        //}
        //public static HttpRouter MapAttribute(this HttpRouter @this, Action<HandlerCompiler> compilerDelegate, Func<IHttpHandler[], IHttpHandler[], IHttpHandler, IHttpHandler> handlerDelegate)
        //{
        //    var compiler = new HandlerCompiler();
        //    compilerDelegate.Invoke(compiler);
        //    return MapAttribute(@this, compiler, handlerDelegate);
        //}
    }
}
