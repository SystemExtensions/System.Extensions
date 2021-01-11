

namespace System.Extensions.Http
{
    using System.Reflection;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using System.Threading.Tasks;
    public class HandlerCompiler//TODO???? static
    {
        #region private
        private static Property<HttpRequest> _Response = new Property<HttpRequest>("HandlerCompiler.Response");
        private static Property<HttpRequest> _AsyncParameters = new Property<HttpRequest>("HandlerCompiler.AsyncParameters");
        //TODO? lock()
        private Stack<Func<Type, ParameterExpression, Expression>> _handlers;
        private Stack<Func<Type, ParameterInfo, ParameterExpression, Expression>> _parameterHandlers;//clash
        private Stack<Func<Type, PropertyInfo, ParameterExpression, Expression>> _propertyHandlers;
        private Stack<object> _returnHandlers;//Func<Type, ParameterExpression, ParameterExpression, ParameterExpression, Expression>
        private class VoidReturnHandler : IHttpHandler
        {
            private MethodInfo _method;
            private Func<HttpRequest, Task<object>>[] _asyncParameters;
            private Action<HttpRequest> _handler;
            public VoidReturnHandler(MethodInfo method, List<Func<HttpRequest, Task<object>>> asyncParameters, Action<HttpRequest> handler)
            {
                _method = method;
                _handler = handler;
                if (asyncParameters.Count > 0)
                    _asyncParameters = asyncParameters.ToArray();
            }
            public async Task<HttpResponse> HandleAsync(HttpRequest request)
            {
                var response = request.CreateResponse();
                request.Properties[_Response] = response;
                if (_asyncParameters != null)
                {
                    var asyncParameters = new object[_asyncParameters.Length];
                    for (int i = 0; i < _asyncParameters.Length; i++)
                    {
                        //TODO? try catch asyncParameters[i] = null;
                        asyncParameters[i] = await _asyncParameters[i](request);
                    }
                    request.Properties[_AsyncParameters] = asyncParameters;
                }

                _handler(request);
                return response;
            }
            public override string ToString() => _method.ReflectedType + "." + _method.Name;
        }
        private class TaskReturnHandler : IHttpHandler
        {
            private MethodInfo _method;
            private Func<HttpRequest, Task<object>>[] _asyncParameters;
            private Func<HttpRequest, Task> _handler;
            public TaskReturnHandler(MethodInfo method, List<Func<HttpRequest, Task<object>>> asyncParameters, Func<HttpRequest, Task> handler)
            {
                _method = method;
                _handler = handler;
                if (asyncParameters.Count > 0)
                    _asyncParameters = asyncParameters.ToArray();
            }
            public async Task<HttpResponse> HandleAsync(HttpRequest request)
            {
                var response = request.CreateResponse();
                request.Properties[_Response] = response;
                if (_asyncParameters != null)
                {
                    var asyncParameters = new object[_asyncParameters.Length];
                    for (int i = 0; i < _asyncParameters.Length; i++)
                    {
                        asyncParameters[i] = await _asyncParameters[i](request);
                    }
                    request.Properties[_AsyncParameters] = asyncParameters;
                }
                await _handler(request);
                return response;
            }
            public override string ToString() => _method.ReflectedType + "." + _method.Name;
        }
        private class ValueTaskReturnHandler : IHttpHandler
        {
            private MethodInfo _method;
            private Func<HttpRequest, Task<object>>[] _asyncParameters;
            private Func<HttpRequest, ValueTask> _handler;
            public ValueTaskReturnHandler(MethodInfo method, List<Func<HttpRequest, Task<object>>> asyncParameters, Func<HttpRequest, ValueTask> handler)
            {
                _method = method;
                _handler = handler;
                if (asyncParameters.Count > 0)
                    _asyncParameters = asyncParameters.ToArray();
            }
            public async Task<HttpResponse> HandleAsync(HttpRequest request)
            {
                var response = request.CreateResponse();
                request.Properties[_Response] = response;
                if (_asyncParameters != null)
                {
                    var asyncParameters = new object[_asyncParameters.Length];
                    for (int i = 0; i < _asyncParameters.Length; i++)
                    {
                        asyncParameters[i] = await _asyncParameters[i](request);
                    }
                    request.Properties[_AsyncParameters] = asyncParameters;
                }
                await _handler(request);
                return response;
            }
            public override string ToString() => _method.ReflectedType + "." + _method.Name;
        }
        private class ReturnHandler<T> : IHttpHandler
        {
            private MethodInfo _method;
            private Func<HttpRequest, Task<object>>[] _asyncParameters;
            private Func<HttpRequest, T> _handler;
            private Action<T, HttpRequest, HttpResponse> _valueHandler;
            public ReturnHandler(MethodInfo method, List<Func<HttpRequest, Task<object>>> asyncParameters, Func<HttpRequest, T> handler, Action<T, HttpRequest, HttpResponse> valueHandler)
            {
                _method = method;
                _handler = handler;
                _valueHandler = valueHandler;
                if (asyncParameters.Count > 0)
                    _asyncParameters = asyncParameters.ToArray();
            }
            public async Task<HttpResponse> HandleAsync(HttpRequest request)
            {
                var response = request.CreateResponse();
                request.Properties[_Response] = response;
                if (_asyncParameters != null)
                {
                    var asyncParameters = new object[_asyncParameters.Length];
                    for (int i = 0; i < _asyncParameters.Length; i++)
                    {
                        asyncParameters[i] = await _asyncParameters[i](request);
                    }
                    request.Properties[_AsyncParameters] = asyncParameters;
                }
                var value = _handler(request);
                _valueHandler(value, request, response);
                return response;
            }
            public override string ToString() => _method.ReflectedType + "." + _method.Name;
        }
        private class ReturnAsyncHandler<T> : IHttpHandler
        {
            private MethodInfo _method;
            private Func<HttpRequest, Task<object>>[] _asyncParameters;
            private Func<HttpRequest, T> _handler;
            private Func<T, HttpRequest, HttpResponse, ValueTask> _valueHandler;
            public ReturnAsyncHandler(MethodInfo method, List<Func<HttpRequest, Task<object>>> asyncParameters, Func<HttpRequest, T> handler, Func<T, HttpRequest, HttpResponse,ValueTask> valueHandler)
            {
                _method = method;
                _handler = handler;
                _valueHandler = valueHandler;
                if (asyncParameters.Count > 0)
                    _asyncParameters = asyncParameters.ToArray();
            }
            public async Task<HttpResponse> HandleAsync(HttpRequest request)
            {
                var response = request.CreateResponse();
                request.Properties[_Response] = response;
                if (_asyncParameters != null)
                {
                    var asyncParameters = new object[_asyncParameters.Length];
                    for (int i = 0; i < _asyncParameters.Length; i++)
                    {
                        asyncParameters[i] = await _asyncParameters[i](request);
                    }
                    request.Properties[_AsyncParameters] = asyncParameters;
                }
                var value = _handler(request);
                await _valueHandler(value, request, response);
                return response;
            }
            public override string ToString() => _method.ReflectedType + "." + _method.Name;
        }
        private class TaskReturnHandler<T> : IHttpHandler
        {
            private MethodInfo _method;
            private Func<HttpRequest, Task<object>>[] _asyncParameters;
            private Func<HttpRequest, Task<T>> _handler;
            private Action<T, HttpRequest, HttpResponse> _valueHandler;
            public TaskReturnHandler(MethodInfo method, List<Func<HttpRequest, Task<object>>> asyncParameters, Func<HttpRequest, Task<T>> handler, Action<T, HttpRequest, HttpResponse> valueHandler)
            {
                _method = method;
                _handler = handler;
                _valueHandler = valueHandler;
                if (asyncParameters.Count > 0)
                    _asyncParameters = asyncParameters.ToArray();
            }
            public async Task<HttpResponse> HandleAsync(HttpRequest request)
            {
                var response = request.CreateResponse();
                request.Properties[_Response] = response;
                if (_asyncParameters != null)
                {
                    var asyncParameters = new object[_asyncParameters.Length];
                    for (int i = 0; i < _asyncParameters.Length; i++)
                    {
                        asyncParameters[i] = await _asyncParameters[i](request);
                    }
                    request.Properties[_AsyncParameters] = asyncParameters;
                }
                var value = await _handler(request);
                _valueHandler(value, request, response);
                return response;
            }
            public override string ToString() => _method.ReflectedType + "." + _method.Name;
        }
        private class TaskReturnAsyncHandler<T> : IHttpHandler
        {
            private MethodInfo _method;
            private Func<HttpRequest, Task<object>>[] _asyncParameters;
            private Func<HttpRequest, Task<T>> _handler;
            private Func<T, HttpRequest, HttpResponse, ValueTask> _valueHandler;
            public TaskReturnAsyncHandler(MethodInfo method, List<Func<HttpRequest, Task<object>>> asyncParameters, Func<HttpRequest, Task<T>> handler, Func<T, HttpRequest, HttpResponse,ValueTask> valueHandler)
            {
                _method = method;
                _handler = handler;
                _valueHandler = valueHandler;
                if (asyncParameters.Count > 0)
                    _asyncParameters = asyncParameters.ToArray();
            }
            public async Task<HttpResponse> HandleAsync(HttpRequest request)
            {
                var response = request.CreateResponse();
                request.Properties[_Response] = response;
                if (_asyncParameters != null)
                {
                    var asyncParameters = new object[_asyncParameters.Length];
                    for (int i = 0; i < _asyncParameters.Length; i++)
                    {
                        asyncParameters[i] = await _asyncParameters[i](request);
                    }
                    request.Properties[_AsyncParameters] = asyncParameters;
                }
                var value = await _handler(request);
                await _valueHandler(value, request, response);
                return response;
            }
            public override string ToString() => _method.ReflectedType + "." + _method.Name;
        }
        private class ValueTaskReturnHandler<T> : IHttpHandler
        {
            private MethodInfo _method;
            private Func<HttpRequest, Task<object>>[] _asyncParameters;
            private Func<HttpRequest, ValueTask<T>> _handler;
            private Action<T, HttpRequest, HttpResponse> _valueHandler;
            public ValueTaskReturnHandler(MethodInfo method, List<Func<HttpRequest, Task<object>>> asyncParameters, Func<HttpRequest, ValueTask<T>> handler, Action<T, HttpRequest, HttpResponse> valueHandler)
            {
                _method = method;
                _handler = handler;
                _valueHandler = valueHandler;
                if (asyncParameters.Count > 0)
                    _asyncParameters = asyncParameters.ToArray();
            }
            public async Task<HttpResponse> HandleAsync(HttpRequest request)
            {
                var response = request.CreateResponse();
                request.Properties[_Response] = response;
                if (_asyncParameters != null)
                {
                    var asyncParameters = new object[_asyncParameters.Length];
                    for (int i = 0; i < _asyncParameters.Length; i++)
                    {
                        asyncParameters[i] = await _asyncParameters[i](request);
                    }
                    request.Properties[_AsyncParameters] = asyncParameters;
                }
                var value = await _handler(request);
                _valueHandler(value, request, response);
                return response;
            }
            public override string ToString() => _method.ReflectedType + "." + _method.Name;
        }
        private class ValueTaskReturnAsyncHandler<T> : IHttpHandler
        {
            private MethodInfo _method;
            private Func<HttpRequest, Task<object>>[] _asyncParameters;
            private Func<HttpRequest, ValueTask<T>> _handler;
            private Func<T, HttpRequest, HttpResponse, ValueTask> _valueHandler;
            public ValueTaskReturnAsyncHandler(MethodInfo method, List<Func<HttpRequest, Task<object>>> asyncParameters, Func<HttpRequest, ValueTask<T>> handler, Func<T, HttpRequest, HttpResponse,ValueTask> valueHandler)
            {
                _method = method;
                _handler = handler;
                _valueHandler = valueHandler;
                if (asyncParameters.Count > 0)
                    _asyncParameters = asyncParameters.ToArray();
            }
            public async Task<HttpResponse> HandleAsync(HttpRequest request)
            {
                var response = request.CreateResponse();
                request.Properties[_Response] = response;
                if (_asyncParameters != null)
                {
                    var asyncParameters = new object[_asyncParameters.Length];
                    for (int i = 0; i < _asyncParameters.Length; i++)
                    {
                        asyncParameters[i] = await _asyncParameters[i](request);
                    }
                    request.Properties[_AsyncParameters] = asyncParameters;
                }
                var value = await _handler(request);
                await _valueHandler(value, request, response);
                return response;
            }
            public override string ToString() => _method.ReflectedType + "." + _method.Name;
        }
        #endregion
        public HandlerCompiler()
        {
            _handlers = new Stack<Func<Type, ParameterExpression, Expression>>();
            _parameterHandlers = new Stack<Func<Type, ParameterInfo, ParameterExpression, Expression>>();
            _propertyHandlers = new Stack<Func<Type, PropertyInfo, ParameterExpression, Expression>>();
            _returnHandlers = new Stack<object>();

            Register(typeof(HttpRequest), (req) => req);
            Register(typeof(IHttpHeaders), (req) => Expression.Property(req, typeof(HttpRequest).GetProperty("Headers")));
            Register(typeof(IQueryParams), (req) => Expression.Call(null, typeof(FeaturesExtensions).GetMethod("QueryParams", new[] { typeof(HttpRequest) }), req));
            Register(typeof(IPathParams), (req) => Expression.Call(null, typeof(FeaturesExtensions).GetMethod("PathParams", new[] { typeof(HttpRequest) }), req));
            Register(typeof(ICookieParams), (req) => Expression.Call(null, typeof(FeaturesExtensions).GetMethod("CookieParams", new[] { typeof(HttpRequest) }), req));
            //Register(typeof(IFormParams), (req) => Expression.Call(null, typeof(FeaturesExtensions).GetMethod("FormParams", new[] { typeof(HttpRequest) }), req));
            //Register(typeof(IFormFileParams), (req) => Expression.Call(null, typeof(FeaturesExtensions).GetMethod("FormFileParams", new[] { typeof(HttpRequest) }), req));
            Register((req) => (HttpResponse)req.Properties[_Response]);

            //Form
            RegisterAsync<IFormParams>(async (req) => {
                await FeaturesExtensions.ReadFormAsync(req, 1 << 20, 32 << 20);//1M,32M
                return req.FormParams();
            });
            RegisterAsync<IFormFileParams>(async (req) => {
                await FeaturesExtensions.ReadFormAsync(req, 1 << 20, 32 << 20);//1M,32M
                return req.FormFileParams();
            });
            RegisterParameterAsync<IFormFile>(async (parameter, req) => {
                await FeaturesExtensions.ReadFormAsync(req, 1 << 20, 32 << 20);
                req.FormFileParams().TryGetValue(parameter.Name, out var file);
                return file;
            });
            RegisterPropertyAsync<IFormFile>(async (property, req) => {
                await FeaturesExtensions.ReadFormAsync(req, 1 << 20, 32 << 20);
                req.FormFileParams().TryGetValue(property.Name, out var file);
                return file;
            });

            //Json
            RegisterParameter((type, parameter, req) => {
                var jsonAttribute = parameter.GetCustomAttribute<ReadJsonAttribute>();
                if (jsonAttribute == null)
                    return null;

                var readAsync = typeof(ReadJsonAttribute).GetMethod("ReadAsync", new[] { typeof(HttpRequest) });
                return Expression.Call(Expression.Constant(jsonAttribute), readAsync.MakeGenericMethod(parameter.ParameterType), req);
            });
            RegisterProperty((type, property, req) => {
                var jsonAttribute = property.GetCustomAttribute<ReadJsonAttribute>();
                if (jsonAttribute == null)
                    return null;

                var readAsync = typeof(ReadJsonAttribute).GetMethod("ReadAsync", new[] { typeof(HttpRequest) });
                return Expression.Call(Expression.Constant(jsonAttribute), readAsync.MakeGenericMethod(property.PropertyType), req);
            });

            //IPathParams 
            RegisterParameter((type, parameter, req) =>
            {
                FeaturesExtensions.GetValue(typeof(string), parameter.ParameterType, Expression.Variable(typeof(string)), out var expression, out _);
                if (expression == null)
                    return null;
                //string=>parameter.ParameterType
                var pathParams = Expression.Call(null, typeof(FeaturesExtensions).GetMethod("PathParams", new[] { typeof(HttpRequest) }), req);
                var getValue = typeof(FeaturesExtensions).GetMethod("GetValue", 1, new[] { typeof(IPathParams), typeof(string) });
                return Expression.Call(null, getValue.MakeGenericMethod(parameter.ParameterType), pathParams, Expression.Constant(parameter.Name));
            });

            //(void Task ValueTask) Invoke(HttpRequest,HttpResponse)
            RegisterReturn((type, value, request, response) => {
                var invoke = type.GetMethod("Invoke", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(HttpRequest), typeof(HttpResponse) }, null);
                if (invoke == null)
                    return null;

                return Expression.Call(value, invoke, request, response);
            });
        }
        public void Register<T>(T value)
        {
            Register((type, request) => {
                if (type != typeof(T))
                    return null;

                return Expression.Constant(value, typeof(T));
            });
        }
        public void Register<T>(Func<T> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            Register((type, request) => {
                if (type != typeof(T))
                    return null;

                return Expression.Invoke(Expression.Constant(handler));
            });
        }
        public void Register<T>(Func<HttpRequest, T> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            Register((type, request) => {
                if (type != typeof(T))
                    return null;

                return Expression.Invoke(Expression.Constant(handler), request);
            });
        }
        public void RegisterAsync<T>(Func<HttpRequest, Task<object>> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            Register((type, request) => {
                if (type != typeof(T))
                    return null;

                return Expression.Invoke(Expression.Constant(handler), request);
            });
        }
        public void Register(Type type, Func<Expression> handler)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            Register((_type, request) => {
                if (_type != type)
                    return null;

                return handler();
            });
        }
        public void Register(Type type, Func<ParameterExpression, Expression> handler)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            Register((_type, request) => {
                if (_type != type)
                    return null;

                return handler(request);
            });
        }
        public void Register(Func<Type, ParameterExpression, Expression> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            _handlers.Push(handler);
        }
        public void Register(Type type, ParameterExpression request, out Expression expression)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            
            expression = null;
            foreach (var handler in _handlers)
            {
                expression = handler.Invoke(type, request);
                if (expression != null)
                {
                    if (expression.Type == typeof(void) && expression.NodeType == ExpressionType.Default)
                        expression = null;
                    return;
                }
            }
        }
        public void RegisterParameter<T>(Predicate<ParameterInfo> predicate, T value)
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            RegisterParameter((type, parameter, request) => {
                if (type != typeof(T) || !predicate(parameter))
                    return null;

                return Expression.Constant(value);
            });
        }
        public void RegisterParameter<T>(Predicate<ParameterInfo> predicate, Func<HttpRequest, T> handler)
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            RegisterParameter((type, parameter, request) => {
                if (type != typeof(T) || !predicate(parameter))
                    return null;

                return Expression.Invoke(Expression.Constant(handler), request);
            });
        }
        public void RegisterParameterAsync<T>(Predicate<ParameterInfo> predicate, Func<HttpRequest, Task<object>> handler)
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            RegisterParameter((type, parameter, request) => {
                if (type != typeof(T) || !predicate(parameter))
                    return null;

                return Expression.Invoke(Expression.Constant(handler), request);
            });
        }
        public void RegisterParameter<T>(Func<ParameterInfo, HttpRequest, T> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            RegisterParameter((type, parameter, request) => {
                if (type != typeof(T))
                    return null;

                return Expression.Invoke(Expression.Constant(handler), Expression.Constant(parameter), request);
            });
        }
        public void RegisterParameterAsync<T>(Func<ParameterInfo, HttpRequest, Task<object>> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            RegisterParameter((type, parameter, request) => {
                if (type != typeof(T))
                    return null;

                return Expression.Invoke(Expression.Constant(handler), Expression.Constant(parameter), request);
            });
        }
        public void RegisterParameter(Func<Type, ParameterInfo, ParameterExpression, Expression> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            _parameterHandlers.Push(handler);
        }
        public void RegisterParameter(ParameterInfo parameter, ParameterExpression request, out Expression expression)
        {
            if (parameter == null)
                throw new ArgumentNullException(nameof(parameter));
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            expression = null;
            foreach (var handler in _parameterHandlers)
            {
                expression = handler.Invoke(parameter.ParameterType, parameter, request);
                if (expression != null)
                {
                    if (expression.Type == typeof(void) && expression.NodeType == ExpressionType.Default)
                        expression = null;
                    return;
                }
            }
        }
        public void RegisterProperty<T>(Predicate<PropertyInfo> predicate, T value)
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            RegisterProperty((type, property, request) => {
                if (type != typeof(T) || !predicate(property))
                    return null;

                return Expression.Constant(value);
            });
        }
        public void RegisterProperty<T>(Predicate<PropertyInfo> predicate, Func<HttpRequest, T> handler)
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            RegisterProperty((type, property, request) => {
                if (type != typeof(T) || !predicate(property))
                    return null;

                return Expression.Invoke(Expression.Constant(handler), request);
            });
        }
        public void RegisterPropertyAsync<T>(Predicate<PropertyInfo> predicate, Func<HttpRequest, Task<object>> handler)
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            RegisterProperty((type, property, request) => {
                if (type != typeof(T) || !predicate(property))
                    return null;

                return Expression.Invoke(Expression.Constant(handler), request);
            });
        }
        public void RegisterProperty<T>(Func<PropertyInfo, HttpRequest, T> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            RegisterProperty((type, property, request) => {
                if (type != typeof(T))
                    return null;

                return Expression.Invoke(Expression.Constant(handler), Expression.Constant(property), request);
            });
        }
        public void RegisterPropertyAsync<T>(Func<PropertyInfo, HttpRequest, Task<object>> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            RegisterProperty((type, property, request) => {
                if (type != typeof(T))
                    return null;

                return Expression.Invoke(Expression.Constant(handler), Expression.Constant(property), request);
            });
        }
        public void RegisterProperty(Func<Type, PropertyInfo, ParameterExpression, Expression> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            _propertyHandlers.Push(handler);
        }
        public void RegisterProperty(PropertyInfo property, ParameterExpression request, out Expression expression)
        {
            if (property == null)
                throw new ArgumentNullException(nameof(property));
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            expression = null;
            foreach (var handler in _propertyHandlers)
            {
                expression = handler.Invoke(property.PropertyType, property, request);
                if (expression != null) 
                {
                    if (expression.Type == typeof(void) && expression.NodeType == ExpressionType.Default)
                        expression = null;
                    return;
                }
            }
        }
        public void RegisterReturn<T>(Action<T, HttpRequest, HttpResponse> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            _returnHandlers.Push(new Tuple<Type, Delegate>(typeof(T), handler));
        }
        public void RegisterReturnAsync<T>(Func<T, HttpRequest, HttpResponse, ValueTask> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            _returnHandlers.Push(new Tuple<Type, Delegate>(typeof(T), handler));
        }
        public void RegisterReturn(Func<Type, ParameterExpression, ParameterExpression, ParameterExpression, Expression> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            _returnHandlers.Push(handler);
        }
        public void RegisterReturn(Type type, ParameterExpression value, ParameterExpression request, ParameterExpression response, out Expression expression, out Delegate @delegate)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (response == null)
                throw new ArgumentNullException(nameof(response));

            expression = null;
            @delegate = null;
            foreach (var handler in _returnHandlers)
            {
                if (handler is Func<Type, ParameterExpression, ParameterExpression, ParameterExpression, Expression> expHandler)
                {
                    expression = expHandler.Invoke(type, value, request, response);
                    if (expression != null) 
                    {
                        if (expression.Type == typeof(void) && expression.NodeType == ExpressionType.Default)
                            expression = null;
                        return;
                    }
                }
                else
                {
                    (var _type, var _delegate) = (Tuple<Type, Delegate>)handler;
                    if (_type == type)
                    {
                        expression = Expression.Invoke(Expression.Constant(_delegate), value, request, response);
                        @delegate = _delegate;
                        return;
                    }
                }
            }
        }
        public IHttpHandler Compile(MethodInfo method)
        {
            if (method == null)
                throw new ArgumentNullException(nameof(method));

            Expression GetParameter(ParameterInfo _parameter, ParameterExpression _request)
            {
                RegisterParameter(_parameter, _request, out var expression);
                if (expression != null)
                    return expression;
                Register(_parameter.ParameterType, _request, out expression);
                if (expression != null)
                    return expression;
                if (_parameter.HasDefaultValue)
                    return Expression.Constant(_parameter.DefaultValue, _parameter.ParameterType);
                return null;
            }
            Expression GetProperty(PropertyInfo _property, ParameterExpression _request)
            {
                RegisterProperty(_property, _request, out var expression);
                if (expression != null)
                    return expression;
                Register(_property.PropertyType, _request, out expression);
                return expression;
            }
            Delegate GetReturn(Type _type, ParameterExpression _request)
            {
                var value = Expression.Parameter(_type, "value");
                var response = Expression.Parameter(typeof(HttpResponse), "response");
                RegisterReturn(_type, value, _request, response, out var expression, out var @delegate);
                if (@delegate != null)
                    return @delegate;
                if (expression == null)
                    return null;
                if (expression.Type == typeof(void) || expression.Type == typeof(ValueTask))
                    return Expression.Lambda(expression, value, _request, response).Compile();
                else if (typeof(Task).IsAssignableFrom(expression.Type))//Task Task<TResult>
                    return Expression.Lambda(Expression.New(typeof(ValueTask).GetConstructor(new[] { typeof(Task) }), expression), value, _request, response).Compile();
                else if (expression.Type.IsGenericType && expression.Type.GetGenericTypeDefinition() == typeof(ValueTask<>))
                    return Expression.Lambda(
                        Expression.New(typeof(ValueTask).GetConstructor(new[] { typeof(Task) }), Expression.Call(expression, expression.Type.GetMethod("AsTask"))
                        ), value, _request, response).Compile();
                else
                    return Expression.Lambda(Expression.Block(typeof(void), expression), value, _request, response).Compile();
            }

            var request = Expression.Parameter(typeof(HttpRequest), "request");
            var methodParams = method.GetParameters();
            var parameters = new Expression[methodParams.Length];
            var asyncParameters = new List<Func<HttpRequest, Task<object>>>();
            for (int i = 0; i < methodParams.Length; i++)
            {
                var expression = GetParameter(methodParams[i], request) ?? Expression.Default(methodParams[i].ParameterType);
                if (expression.Type == typeof(Task<object>) && methodParams[i].ParameterType != typeof(Task<object>))
                {
                    parameters[i] = Expression.Convert(Expression.ArrayIndex(
                        Expression.Convert(
                        Expression.Property(
                            Expression.Property(request, typeof(HttpRequest).GetProperty("Properties")),
                            typeof(PropertyCollection<HttpRequest>).GetProperty("Item"), Expression.Constant(_AsyncParameters)), typeof(object[])),
                        Expression.Constant(asyncParameters.Count)),
                        methodParams[i].ParameterType);
                    asyncParameters.Add(Expression.Lambda<Func<HttpRequest, Task<object>>>(expression, request).Compile());
                }
                else
                {
                    parameters[i] = expression;
                }
            }
            Expression methodCall = null;
            if (method.IsStatic)
            {
                methodCall = Expression.Call(null, method, parameters);
            }
            else
            {
                //TODO
                //Register(method.ReflectedType, request, out var instance);
                //?? use Constructor.Register
                var instance = Expression.Variable(method.ReflectedType, "instance");
                var ctors = method.ReflectedType.GetConstructors();
                foreach (var ctor in ctors)
                {
                    var exprs = new List<Expression>();
                    var ctorParameters = ctor.GetParameters();
                    var ctorParams = new List<Expression>();
                    foreach (var parameter in ctorParameters)
                    {
                        var expression = GetParameter(parameter, request);
                        if (expression == null)
                            continue;

                        if (expression.Type == typeof(Task<object>) && parameter.ParameterType != typeof(Task<object>))
                        {
                            ctorParams.Add(Expression.Convert(Expression.ArrayIndex(
                                Expression.Convert(
                                Expression.Property(
                                    Expression.Property(request, typeof(HttpRequest).GetProperty("Properties")),
                                    typeof(PropertyCollection<HttpRequest>).GetProperty("Item"), Expression.Constant(_AsyncParameters)), typeof(object[])),
                                Expression.Constant(asyncParameters.Count)),
                                parameter.ParameterType));

                            asyncParameters.Add(Expression.Lambda<Func<HttpRequest, Task<object>>>(expression, request).Compile());
                        }
                        else
                        {
                            ctorParams.Add(expression);
                        }   
                    }
                    if (ctorParameters.Length == ctorParams.Count)
                    {
                        exprs.Add(Expression.Assign(instance, Expression.New(ctor, ctorParams)));
                        var properties = method.ReflectedType.GetGeneralProperties(BindingFlags.Public | BindingFlags.Instance);
                        foreach (var property in properties)
                        {
                            if (!property.CanWrite)
                                continue;
                            var expression = GetProperty(property, request);
                            if (expression == null)
                                continue;

                            if (expression.Type == typeof(Task<object>) && property.PropertyType != typeof(Task<object>))
                            {
                                exprs.Add(Expression.Assign(
                                    Expression.Property(instance, property),
                                    Expression.Convert(Expression.ArrayIndex(
                                        Expression.Convert(
                                        Expression.Property(
                                            Expression.Property(request, typeof(HttpRequest).GetProperty("Properties")),
                                            typeof(PropertyCollection<HttpRequest>).GetProperty("Item"), Expression.Constant(_AsyncParameters)), typeof(object[])),
                                        Expression.Constant(asyncParameters.Count)),
                                        property.PropertyType)));
                                asyncParameters.Add(Expression.Lambda<Func<HttpRequest, Task<object>>>(expression, request).Compile());
                            }
                            else
                            {
                                exprs.Add(Expression.Assign(Expression.Property(instance, property), expression));
                            }
                        }
                        exprs.Add(Expression.Call(instance, method, parameters));
                        methodCall = Expression.Block(new[] { instance }, exprs);
                        break;
                    }
                }
                if (methodCall == null)
                    return null;
            }

            if (methodCall.Type == typeof(void))
            {
                var handler = Expression.Lambda<Action<HttpRequest>>(methodCall, request).Compile();
                return new VoidReturnHandler(method, asyncParameters, handler);
            }
            else if (methodCall.Type == typeof(Task))
            {
                var handler = Expression.Lambda<Func<HttpRequest, Task>>(methodCall, request).Compile();
                return new TaskReturnHandler(method, asyncParameters, handler);
            }
            else if (methodCall.Type == typeof(ValueTask))
            {
                var handler = Expression.Lambda<Func<HttpRequest, ValueTask>>(methodCall, request).Compile();
                return new ValueTaskReturnHandler(method, asyncParameters, handler);
            }
            else if (methodCall.Type.IsGenericType)
            {
                if (methodCall.Type.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    var typeArg = methodCall.Type.GenericTypeArguments[0];
                    var valueHandler = GetReturn(typeArg, request);
                    if (valueHandler == null)
                    {
                        var handler = Expression.Lambda<Func<HttpRequest, Task>>(methodCall, request).Compile();
                        return new TaskReturnHandler(method, asyncParameters, handler);
                    }
                    else if (valueHandler.Method.ReturnType == typeof(void))
                    {
                        var handler = Expression.Lambda(methodCall, request).Compile();
                        return (IHttpHandler)Activator.CreateInstance(typeof(TaskReturnHandler<>).MakeGenericType(typeArg), method, asyncParameters, handler, valueHandler);
                    }
                    else//ValueTask
                    {
                        var handler = Expression.Lambda(methodCall, request).Compile();
                        return (IHttpHandler)Activator.CreateInstance(typeof(TaskReturnAsyncHandler<>).MakeGenericType(typeArg), method, asyncParameters, handler, valueHandler);
                    }
                }
                else if (methodCall.Type.GetGenericTypeDefinition() == typeof(ValueTask<>))
                {
                    var typeArg = methodCall.Type.GenericTypeArguments[0];
                    var valueHandler = GetReturn(typeArg, request);
                    if (valueHandler == null)
                    {
                        var handler = Expression.Lambda<Func<HttpRequest, Task>>(Expression.Call(methodCall, methodCall.Type.GetMethod("AsTask")), request).Compile();
                        return new TaskReturnHandler(method, asyncParameters, handler);
                    }
                    else if (valueHandler.Method.ReturnType == typeof(void))
                    {
                        var handler = Expression.Lambda(methodCall, request).Compile();
                        return (IHttpHandler)Activator.CreateInstance(typeof(ValueTaskReturnHandler<>).MakeGenericType(typeArg), method, asyncParameters, handler, valueHandler);
                    }
                    else
                    {
                        var handler = Expression.Lambda(methodCall, request).Compile();
                        return (IHttpHandler)Activator.CreateInstance(typeof(ValueTaskReturnAsyncHandler<>).MakeGenericType(typeArg), method, asyncParameters, handler, valueHandler);
                    }
                }
            }
            {
                var valueHandler = GetReturn(methodCall.Type, request);
                if (valueHandler == null)
                {
                    var handler = Expression.Lambda<Action<HttpRequest>>(Expression.Block(methodCall, Expression.Empty()), request).Compile();
                    return new VoidReturnHandler(method, asyncParameters, handler);
                }
                else if (valueHandler.Method.ReturnType == typeof(void))
                {
                    var handler = Expression.Lambda(methodCall, request).Compile();
                    return (IHttpHandler)Activator.CreateInstance(typeof(ReturnHandler<>).MakeGenericType(methodCall.Type), method, asyncParameters, handler, valueHandler);
                }
                else
                {
                    var handler = Expression.Lambda(methodCall, request).Compile();
                    return (IHttpHandler)Activator.CreateInstance(typeof(ReturnAsyncHandler<>).MakeGenericType(methodCall.Type), method, asyncParameters, handler, valueHandler);
                }
            }
        }
    }
}
