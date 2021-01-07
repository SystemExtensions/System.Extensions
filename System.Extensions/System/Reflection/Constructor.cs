
namespace System.Reflection
{
    using System.Linq.Expressions;
    using System.Collections.Generic;
    public static class Constructor
    {
        private static readonly object _Sync = new object();
        private static Stack<object> _Handlers;
        private static Dictionary<Type, Func<object>> _ObjHandlers;
        private static class Handler<T>
        {
            static Handler()
            {
                Register(typeof(T), out var expression, out var @delegate);
                if (expression != null)
                {
                    Value = @delegate == null
                        ? Expression.Lambda<Func<T>>(expression).Compile()
                        : (Func<T>)@delegate;
                }
            }

            public static Func<T> Value;
        }
        static Constructor()
        {
            _Handlers = new Stack<object>();
            _ObjHandlers = new Dictionary<Type, Func<object>>();

            //Empty,HasDefaultValue
            Register((type) => {
                if (type.IsValueType)
                    return Expression.Default(type);

                var ctors = type.GetConstructors();
                foreach (var ctor in ctors)
                {
                    var parameters = ctor.GetParameters();
                    var ctorParams = new List<Expression>();
                    foreach (var parameter in parameters)
                    {
                        if (parameter.HasDefaultValue)
                        {
                            ctorParams.Add(Expression.Constant(parameter.DefaultValue, parameter.ParameterType));
                        }
                    }
                    if (parameters.Length == ctorParams.Count)
                    {
                        return Expression.New(ctor, ctorParams);
                    }
                }
                return null;
            });
            //TODO Interface?
            //IList<>
            Register((type) => {
                if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(IList<>))
                    return null;

                Register(typeof(List<>).MakeGenericType(type.GetGenericArguments()), out var expression, out _);
                return expression;
            });
            //IReadOnlyList<>
            Register((type) => {
                if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(IReadOnlyList<>))
                    return null;

                Register(typeof(List<>).MakeGenericType(type.GetGenericArguments()), out var expression, out _);
                return expression;
            });
            //ISet<>
            Register((type) => {
                if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(ISet<>))
                    return null;

                Register(typeof(HashSet<>).MakeGenericType(type.GetGenericArguments()), out var expression, out _);
                return expression;
            });
            //IDictionary<,>
            Register((type) => {
                if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(IDictionary<,>))
                    return null;

                Register(typeof(Dictionary<,>).MakeGenericType(type.GetGenericArguments()), out var expression, out _);
                return expression;
            });
            //IDictionary<,>
            Register((type) => {
                if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(IReadOnlyDictionary<,>))
                    return null;

                Register(typeof(Dictionary<,>).MakeGenericType(type.GetGenericArguments()), out var expression, out _);
                return expression;
            });
        }
        public static void Register<T>(Func<T> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            lock (_Sync)
            {
                _Handlers.Push(new Tuple<Type, Delegate>(typeof(T), handler));
            }
        }
        public static void Register(Type type, Func<Expression> handler)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            Register((_type) => {
                if (_type != type)
                    return null;

                return handler();
            });
        }
        public static void Register(Func<Type, Expression> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            lock (_Sync)
            {
                _Handlers.Push(handler);
            }
        }
        public static void Register(Type type, out Expression expression, out Delegate @delegate)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            expression = null;
            @delegate = null;
            lock (_Sync)
            {
                foreach (var handler in _Handlers)
                {
                    if (handler is Func<Type, Expression> exprHandler)
                    {
                        expression = exprHandler.Invoke(type);
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
                            expression = Expression.Invoke(Expression.Constant(_delegate));
                            @delegate = _delegate;
                            return;
                        }
                    }
                }
            }
        }
        public static Func<T> Get<T>()
        {
            return Handler<T>.Value;
        }
        public static Func<object> Get(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            if (!_ObjHandlers.TryGetValue(type, out var constructor))
            {
                lock (_Sync)
                {
                    if (!_ObjHandlers.TryGetValue(type, out constructor))
                    {
                        Register(type, out var expression, out _);
                        if (expression != null)
                        {
                            constructor = Expression.Lambda<Func<object>>(Expression.Convert(expression, typeof(object))).Compile();
                        }
                        var objHandlers = new Dictionary<Type, Func<object>>(_ObjHandlers);
                        objHandlers.Add(type, constructor);
                        _ObjHandlers = objHandlers;
                    }
                }
            }
            return constructor;
        }
        public static T Invoke<T>()
        {
            var constructor = Get<T>();
            if (constructor == null)
                throw new NotSupportedException($"Constructor:{typeof(T)}");

            return constructor();
        }
        public static object Invoke(Type type)
        {
            var constructor = Get(type);
            if (constructor == null)
                throw new NotSupportedException($"Constructor:{type}");

            return constructor();
        }
    }
}
