
namespace System.Extensions.Http
{
    using System.Diagnostics;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    public static class HttpHandler
    {
        #region  private
        private class HandlerA : IHttpHandler
        {
            public HandlerA(Action<HttpRequest, HttpResponse> handler)
            {
                _handler = handler;
            }
            private Action<HttpRequest, HttpResponse> _handler;
            public Task<HttpResponse> HandleAsync(HttpRequest request)
            {
                Debug.Assert(_handler != null);
                var response = request.CreateResponse();
                _handler(request, response);
                return Task.FromResult(response);
            }
            public override string ToString() => "Action<HttpRequest, HttpResponse>";
        }
        private class HandlerB : IHttpHandler
        {
            public HandlerB(Func<HttpRequest, HttpResponse, Task> handler)
            {
                _handler = handler;
            }
            private Func<HttpRequest, HttpResponse, Task> _handler;
            public async Task<HttpResponse> HandleAsync(HttpRequest request)
            {
                Debug.Assert(_handler != null);
                var response = request.CreateResponse();
                await _handler(request, response);
                return response;
            }
            public override string ToString() => "Func<HttpRequest, HttpResponse, ValueTask>";
        }
        private class HandlerC : IHttpHandler
        {
            public HandlerC(Func<HttpRequest, Task<HttpResponse>> handler)
            {
                _handler = handler;
            }
            private Func<HttpRequest, Task<HttpResponse>> _handler;
            public Task<HttpResponse> HandleAsync(HttpRequest request)
            {
                Debug.Assert(_handler != null);
                return _handler(request);
            }
            public override string ToString() => "Func<HttpRequest, ValueTask<HttpResponse>>";
        }
        private class Handler2 : IHttpHandler
        {
            public Handler2(IHttpHandler handler1, IHttpHandler handler2)
            {
                _handler1 = handler1;
                _handler2 = handler2;
            }
            private IHttpHandler _handler1;
            private IHttpHandler _handler2;
            public async Task<HttpResponse> HandleAsync(HttpRequest request)
            {
                var response = await _handler1.HandleAsync(request);
                if (response != null)
                    return response;

                return await _handler2.HandleAsync(request);
            }
        }
        private class Handler3 : IHttpHandler
        {
            public Handler3(IHttpHandler handler1, IHttpHandler handler2, IHttpHandler handler3)
            {
                _handler1 = handler1;
                _handler2 = handler2;
                _handler3 = handler3;
            }
            private IHttpHandler _handler1;
            private IHttpHandler _handler2;
            private IHttpHandler _handler3;
            public async Task<HttpResponse> HandleAsync(HttpRequest request)
            {
                var response = await _handler1.HandleAsync(request);
                if (response != null)
                    return response;
                response = await _handler2.HandleAsync(request);
                if (response != null)
                    return response;
                return await _handler3.HandleAsync(request);
            }
        }
        private class Handler4 : IHttpHandler
        {
            public Handler4(IHttpHandler handler1, IHttpHandler handler2, IHttpHandler handler3, IHttpHandler handler4)
            {
                _handler1 = handler1;
                _handler2 = handler2;
                _handler3 = handler3;
                _handler4 = handler4;
            }
            private IHttpHandler _handler1;
            private IHttpHandler _handler2;
            private IHttpHandler _handler3;
            private IHttpHandler _handler4;
            public async Task<HttpResponse> HandleAsync(HttpRequest request)
            {
                var response = await _handler1.HandleAsync(request);
                if (response != null)
                    return response;
                response = await _handler2.HandleAsync(request);
                if (response != null)
                    return response;
                response = await _handler3.HandleAsync(request);
                if (response != null)
                    return response;

                return await _handler4.HandleAsync(request);
            }
        }
        private class Handler5 : IHttpHandler
        {
            public Handler5(IHttpHandler handler1, IHttpHandler handler2, IHttpHandler handler3, IHttpHandler handler4, IHttpHandler handler5)
            {
                _handler1 = handler1;
                _handler2 = handler2;
                _handler3 = handler3;
                _handler4 = handler4;
                _handler5 = handler5;
            }
            private IHttpHandler _handler1;
            private IHttpHandler _handler2;
            private IHttpHandler _handler3;
            private IHttpHandler _handler4;
            private IHttpHandler _handler5;
            public async Task<HttpResponse> HandleAsync(HttpRequest request)
            {
                var response = await _handler1.HandleAsync(request);
                if (response != null)
                    return response;
                response = await _handler2.HandleAsync(request);
                if (response != null)
                    return response;
                response = await _handler3.HandleAsync(request);
                if (response != null)
                    return response;
                response = await _handler4.HandleAsync(request);
                if (response != null)
                    return response;

                return await _handler5.HandleAsync(request);
            }
        }
        private class Handlers : IHttpHandler
        {
            private List<IHttpHandler> _collection;
            public Handlers()
            {
                _collection = new List<IHttpHandler>();
            }
            public int Count => _collection.Count;
            public void Add(IHttpHandler handler)
            {
                _collection.Add(handler);
            }
            public void Clear() => _collection.Clear();
            public bool TryGetHandler(out IHttpHandler handler)
            {
                var count = _collection.Count;
                if (count == 0)
                {
                    handler = null;
                    return true;
                }
                else if (count == 1)
                {
                    handler = _collection[0];
                    return true;
                }
                else if (count == 2)
                {
                    handler = new Handler2(_collection[0], _collection[1]);
                    return true;
                }
                else if (count == 3)
                {
                    handler = new Handler3(_collection[0], _collection[1], _collection[2]);
                    return true;
                }
                else if (count == 4)
                {
                    handler = new Handler4(_collection[0], _collection[1], _collection[2], _collection[3]);
                    return true;
                }
                else if (count == 5)
                {
                    handler = new Handler5(_collection[0], _collection[1], _collection[2], _collection[3], _collection[4]);
                    return true;
                }
                else
                {
                    handler = null;
                    return false;
                }
            }
            public async Task<HttpResponse> HandleAsync(HttpRequest request)
            {
                for (int i = 0; i < _collection.Count; i++)
                {
                    var response = await _collection[i].HandleAsync(request);
                    if (response != null)
                        return response;
                }
                return null;
            }
        }
        private class Module : IHttpModule
        {
            public Module(Func<HttpRequest, IHttpHandler, Task<HttpResponse>> module)
            {
                _module = module;
            }

            private Func<HttpRequest, IHttpHandler, Task<HttpResponse>> _module;
            public IHttpHandler Handler { get; set; }
            public Task<HttpResponse> HandleAsync(HttpRequest request)
            {
                return _module(request, Handler);
            }
            public override string ToString() => "Func<HttpRequest, IHttpHandler,ValueTask<HttpResponse>>";
        }
        #endregion
        public static IHttpHandler Create(Action<HttpRequest, HttpResponse> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            return new HandlerA(handler);
        }
        public static IHttpHandler Create(Func<HttpRequest, HttpResponse, Task> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            return new HandlerB(handler);
        }
        public static IHttpHandler Create(Func<HttpRequest, Task<HttpResponse>> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            return new HandlerC(handler);
        }
        public static IHttpModule CreateModule(Func<HttpRequest, IHttpHandler, Task<HttpResponse>> module)
        {
            if (module == null)
                throw new ArgumentNullException(nameof(module));

            return new Module(module);
        }
        //TODO?? only [multi IHttpModule]+[single IHttpHandler]
        public static IHttpHandler CreatePipeline(IList<IHttpHandler> handlers)
        {
            if (handlers == null)
                throw new ArgumentNullException(nameof(handlers));

            if (handlers.Count == 0)
                return null;
            if (handlers.Count == 1)
                return handlers[0];
            //TODO better
            var collection = new Handlers();
            IHttpModule module = null;
            IHttpHandler root = null;
            for (int i = 0; i < handlers.Count; i++)
            {
                var handler = handlers[i];
                if (handler == null)
                    continue;
                var tempModule = handler as IHttpModule;
                if (tempModule != null && tempModule.Handler == null)
                {
                    if (module == null)//root
                    {
                        if (collection.TryGetHandler(out var tempHandler))
                        {
                            root = tempHandler ?? handler;
                            collection.Clear();
                        }
                        else
                        {
                            root = collection;
                            collection = new Handlers();
                        }
                    }
                    else
                    {
                        if (collection.TryGetHandler(out var tempHandler))
                        {
                            module.Handler = tempHandler ?? handler;
                        }
                        else
                        {
                            module.Handler = collection;
                            collection = new Handlers();
                        }
                    }
                    module = tempModule;
                }
                else
                {
                    collection.Add(handler);
                }
            }

            if (root == null)
                return collection.TryGetHandler(out var tempHandler) ? tempHandler : collection;

            Debug.Assert(module != null);
            if (collection.Count > 0)
                module.Handler = collection.TryGetHandler(out var tempHandler) ? tempHandler : collection;
            return root;
        }

        // static async ValueTask<HttpResponse> Invoke(HttpRequest request, IHttpHandler handler)
        //public static IHttpModule CreateModule(Type moduleType)
        //{
        //    if (moduleType == null)
        //        throw new ArgumentNullException(nameof(moduleType));

        //    var method = moduleType.GetMethod("Invoke", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(HttpRequest), typeof(IHttpHandler) }, null);
        //    if (method == null)
        //        return null;
        //    var request = Expression.Parameter(typeof(HttpRequest), "request");
        //    var handler = Expression.Parameter(typeof(IHttpHandler), "handler");
        //    return CreateModule(Expression.Lambda<Func<HttpRequest, IHttpHandler, ValueTask<HttpResponse>>>
        //        (Expression.Call(null, method, request, handler), request, handler).Compile());
        //}
        //async ValueTask<HttpResponse> Invoke(HttpRequest request, IHttpHandler handler)
        //public static IHttpModule CreateModule<TModule>(TModule module)
        //{
        //    if (module == null)
        //        throw new ArgumentNullException(nameof(module));

        //    var moduleType = typeof(TModule);
        //    var method = moduleType.GetMethod("Invoke", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(HttpRequest), typeof(IHttpHandler) }, null);
        //    if (method == null)
        //        return null;
        //    var request = Expression.Parameter(typeof(HttpRequest), "request");
        //    var handler = Expression.Parameter(typeof(IHttpHandler), "handler");
        //    return CreateModule(Expression.Lambda<Func<HttpRequest, IHttpHandler, ValueTask<HttpResponse>>>
        //        (Expression.Call(Expression.Constant(module), method, request, handler), request, handler).Compile());
        //}
    }
}
