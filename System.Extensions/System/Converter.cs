
namespace System
{
    using System.Reflection;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    public static class Converter
    {
        private static readonly object _Sync = new object();
        private static Stack<object> _Handlers;
        private static Dictionary<(Type, Type), Func<object, object>> _ObjHandlers;
        static Converter()
        {
            _Handlers = new Stack<object>();
            _ObjHandlers = new Dictionary<(Type, Type), Func<object, object>>();

            //Nullable<>
            Register((tValue, tResult, value) => {
                if (tValue.IsGenericType && tValue.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    var get = typeof(Converter).GetMethod("Get", Type.EmptyTypes);
                    var gValue = tValue.GetGenericArguments()[0];
                    if (tResult.IsGenericType && tResult.GetGenericTypeDefinition() == typeof(Nullable<>))
                    {
                        var gResult = tResult.GetGenericArguments()[0];
                        var converter = get.MakeGenericMethod(gValue, gResult).Invoke(null, null);
                        if (converter != null)
                        {
                            return Expression.TryCatch(
                                    Expression.Convert(
                                    Expression.Invoke(
                                        Expression.Constant(converter),
                                        Expression.Convert(value, gValue)), tResult),
                                    Expression.Catch(typeof(Exception), Expression.Default(tResult))
                                );
                        }
                    }
                    else
                    {
                        var converter = get.MakeGenericMethod(gValue, tResult).Invoke(null, null);
                        if (converter != null)
                        {
                            return Expression.Invoke(
                                Expression.Constant(converter),
                                Expression.Convert(value, gValue));
                        }
                    }
                }
                else if (tResult.IsGenericType && tResult.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    var get = typeof(Converter).GetMethod("Get", Type.EmptyTypes);
                    var gResult = tResult.GetGenericArguments()[0];
                    var converter = get.MakeGenericMethod(tValue, gResult).Invoke(null, null);
                    if (converter != null)
                    {
                        return Expression.TryCatch(
                                Expression.Convert(
                                Expression.Invoke(Expression.Constant(converter), value), tResult),
                                Expression.Catch(typeof(Exception), Expression.Constant(null, tResult))
                            );
                    }
                }
                return null;
            });
            //Nullable<Enum> Enum.IsDefined TODO?? Remove
            Register((tValue, tResult, value) => {
                if (!tResult.IsGenericType || tResult.GetGenericTypeDefinition() != typeof(Nullable<>))
                    return null;
                var gResult = tResult.GetGenericArguments()[0];
                if (!gResult.IsEnum)
                    return null;

                Register(tValue, gResult, value, out var expression, out _);
                if (expression == null)
                    return null;
                var isDefined = typeof(Enum).GetMethod("IsDefined");
                var @enum = Expression.Variable(gResult, "@enum");
                return Expression.TryCatch(
                    Expression.Block(new[] { @enum },
                        Expression.Assign(@enum, expression),
                        Expression.Condition(
                            Expression.Call(isDefined, Expression.Constant(gResult), Expression.Convert(@enum, typeof(object))),
                            Expression.New(tResult.GetConstructor(new[] { gResult }), @enum),
                            Expression.Default(tResult))),
                        Expression.Catch(typeof(Exception), Expression.Default(tResult))
                        );
            });
            //Enum
            Register((tValue, tResult, value) => {
                if (!tResult.IsEnum)
                    return null;

                if (tValue == typeof(string))
                {
                    var tryParse = typeof(int).GetMethod("TryParse", new[] { typeof(string), typeof(int).MakeByRefType() });
                    var parse = typeof(Enum).GetMethod("Parse", new[] { typeof(string) });
                    var intValue = Expression.Variable(typeof(int), "intValue");
                    return Expression.Block(new[] { intValue },
                        Expression.Condition(
                            Expression.Call(tryParse, value, intValue),
                            Expression.Convert(intValue, tResult),
                            Expression.Call(parse.MakeGenericMethod(tResult), value)
                            )
                        );
                }
                if (tValue == typeof(int))
                    return Expression.Convert(value, tResult);

                Register(tValue, typeof(int), value, out var expression, out _);
                if (expression == null)
                    return null;

                return Expression.Convert(expression, tResult);
            });
            Register((tValue, tResult, value) => {
                if (!tValue.IsEnum || tResult != typeof(string))
                    return null;

                var toString = tValue.GetMethod("ToString", Type.EmptyTypes);
                return Expression.Call(value, toString);
            });
            //Parse [TryParse???],Convert
            Register((tValue, tResult, value) => {
                var parse = tResult.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, null, new[] { tValue }, null);
                if (parse != null && parse.ReturnType == tResult)
                {
                    return Expression.Call(parse, value);
                }
                var methods = typeof(Convert).GetMethods(BindingFlags.Public | BindingFlags.Static);
                foreach (var method in methods)
                {
                    if (method.ReturnType == tResult)
                    {
                        var @params = method.GetParameters();
                        if (@params.Length == 1 && @params[0].ParameterType == tValue)
                        {
                            return Expression.Call(method, value);
                        }
                    }
                }
                return null;
            });
            //ToString
            Register((tValue, tResult, value) =>
            {
                if (tResult != typeof(string) || tValue.IsNotPublic)//IsNotPublic
                    return null;
                var toString = tValue.GetMethod("ToString", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (toString == null)
                    return null;
                if (tValue.IsGenericType && tValue.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    var gValue = tValue.GetGenericArguments()[0];
                    var get = typeof(Converter).GetMethod("Get", Type.EmptyTypes);
                    var converter = get.MakeGenericMethod(gValue, typeof(string)).Invoke(null, null);
                    if (converter == null)
                        return null;
                    return Expression.Condition(
                        Expression.Equal(value, Expression.Constant(null)),
                        Expression.Constant(null, typeof(string)),
                        Expression.Invoke(Expression.Constant(converter), Expression.Convert(value, gValue))
                        );
                }
                //Or !=typeof(object)
                if (toString.DeclaringType != tValue)
                    return null;
                return Expression.Call(value, toString);
            });
            //op_Implicit op_Explicit
            Register((tValue, tResult, value) => {
                var methods = tValue.GetMethods(BindingFlags.Public | BindingFlags.Static);
                foreach (var method in methods)
                {
                    if (method.Name == "op_Implicit" || method.Name == "op_Explicit")
                    {
                        if (method.ReturnType == tResult && method.GetParameters()[0].ParameterType == tValue)
                        {
                            return Expression.Convert(value, tResult);
                        }
                    }
                }
                methods = tResult.GetMethods(BindingFlags.Public | BindingFlags.Static);
                foreach (var method in methods)
                {
                    if (method.Name == "op_Implicit" || method.Name == "op_Explicit")
                    {
                        if (method.ReturnType == tResult && method.GetParameters()[0].ParameterType == tValue)
                        {
                            return Expression.Convert(value, tResult);
                        }
                    }
                }
                return null;
            });
            //Number SByte=5,Decimal=15
            Register((tValue, tResult, value) => {
                var tValueCode = (int)Type.GetTypeCode(tValue);
                if (tValueCode < 5 || tValueCode > 15)
                    return null;
                var tResultCode = (int)Type.GetTypeCode(tResult);
                if (tResultCode < 5 || tResultCode > 15)
                    return null;
                //if (tResultCode > tValueCode)
                //    return null;
                return Expression.Convert(value, tResult);
            });
            //object
            Register((tValue, tResult, value) => {
                if (tValue != typeof(object))
                    return null;
                var convert = typeof(Converter).GetMethod("Convert", new[] { typeof(object), typeof(Type) });
                return Expression.Convert(
                    Expression.Call(convert, value, Expression.Constant(tResult)), tResult);
            });
            //Equals,IsAssignableFrom
            Register((tValue, tResult, value) => {
                if (tValue == tResult)
                    return value;
                if (!tResult.IsAssignableFrom(tValue))
                    return null;
                return Expression.Convert(value, tResult);
            });
            //Guid<=>string
            Register<Guid, string>((value) => value.ToString("N"));
            Register<string, Guid>((value) => Guid.Parse(value));
            //string=>bool EqualsIgnoreCase? yes? 
            Register<string, bool>((value) => value == "1" || value == "true" || value == "on");
        }
        private static class Handler<TValue, TResult>
        {
            static Handler()
            {
                var value = Expression.Parameter(typeof(TValue), "value");
                Register(typeof(TValue), typeof(TResult), value, out var expression, out var @delegate);
                if (expression != null)
                {
                    Value = @delegate == null
                            ? Expression.Lambda<Func<TValue, TResult>>(expression, value).Compile()
                            : (Func<TValue, TResult>)@delegate;
                }
            }

            public static Func<TValue, TResult> Value;
        }
        public static void Register<TValue, TResult>(Func<TValue, TResult> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            lock (_Sync)
            {
                _Handlers.Push(new Tuple<Type, Type, Delegate>(typeof(TValue), typeof(TResult), handler));
            }
        }
        public static void Register(Type tValue, Type tResult, Func<ParameterExpression, Expression> handler)
        {
            if (tValue == null)
                throw new ArgumentNullException(nameof(tValue));
            if (tResult == null)
                throw new ArgumentNullException(nameof(tResult));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            Register((_tValue, _tResult, value) => {
                if (_tValue == tValue && _tResult == tResult)
                    return handler(value);

                return null;
            });
        }
        public static void Register(Func<Type, Type, ParameterExpression, Expression> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            lock (_Sync)
            {
                _Handlers.Push(handler);
            }
        }
        public static void Register(Type tValue, Type tResult, ParameterExpression value, out Expression expression, out Delegate @delegate)
        {
            if (tValue == null)
                throw new ArgumentNullException(nameof(tValue));
            if (tResult == null)
                throw new ArgumentNullException(nameof(tResult));
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            expression = null;
            @delegate = null;
            lock (_Sync)
            {
                foreach (var handler in _Handlers)
                {
                    if (handler is Func<Type, Type, ParameterExpression, Expression> exprHandler)
                    {
                        expression = exprHandler.Invoke(tValue, tResult, value);
                        if (expression != null)
                        {
                            if (expression.Type == typeof(void) && expression.NodeType == ExpressionType.Default)
                                expression = null;
                            return;
                        }
                    }
                    else
                    {
                        (var _tValue, var _tResult, var _delegate) = (Tuple<Type, Type, Delegate>)handler;
                        if (_tValue == tValue && _tResult == tResult)
                        {
                            expression = Expression.Invoke(Expression.Constant(_delegate), value);
                            @delegate = _delegate;
                            return;
                        }
                    }
                }
            }
        }
        public static Func<TValue, TResult> Get<TValue, TResult>()
        {
            return Handler<TValue, TResult>.Value;
        }
        public static Func<object, object> Get(Type tValue, Type tResult)
        {
            if (tValue == null)
                throw new ArgumentNullException(nameof(tValue));
            if (tResult == null)
                throw new ArgumentNullException(nameof(tResult));

            if (!_ObjHandlers.TryGetValue((tValue, tResult), out var handler))
            {
                lock (_Sync)
                {
                    if (!_ObjHandlers.TryGetValue((tValue, tResult), out handler))
                    {
                        var objValue = Expression.Parameter(typeof(object), "objValue");
                        var value = Expression.Variable(tValue, "value");
                        Register(tValue, tResult, value, out var expression, out _);
                        if (expression != null)
                        {
                            var expr = Expression.Block(new [] { value },
                                Expression.Assign(value, Expression.Convert(objValue, tValue)),
                                Expression.Convert(expression, typeof(object)));
                            handler = Expression.Lambda<Func<object, object>>(expr, objValue).Compile();
                        }
                        var objHandlers = new Dictionary<(Type, Type), Func<object, object>>(_ObjHandlers);
                        objHandlers.Add((tValue, tResult), handler);
                        _ObjHandlers = objHandlers;
                    }
                }
            }
            return handler;
        }
        public static TResult Convert<TValue, TResult>(TValue value)
        {
            var converter = Handler<TValue, TResult>.Value;
            if (converter == null)
                throw new NotSupportedException($"Converter:{typeof(TValue)},{typeof(TResult)}");

            return converter(value);
        }
        public static object Convert(object value, Type tResult)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (tResult == null)
                throw new ArgumentNullException(nameof(tResult));

            var converter = Get(value.GetType(), tResult);
            if (converter == null)
                throw new NotSupportedException($"Converter:{value.GetType()},{tResult}");

            return converter(value);
        }
        public static bool TryConvert<TValue, TResult>(TValue value, out TResult result)
        {
            var converter = Handler<TValue, TResult>.Value;
            if (converter == null)
            {
                result = default;
                return false;
            }
            try
            {
                result = converter(value);
                return true;
            }
            catch
            {
                //TODO?? Trace.WriteLine(ex, "UnobservedException");
                result = default;
                return false;
            }
        }
        public static bool TryConvert(object value, Type tResult, out object result)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (tResult == null)
                throw new ArgumentNullException(nameof(tResult));

            var converter = Get(value.GetType(), tResult);
            if (converter == null)
            {
                result = default;
                return false;
            }
            try
            {
                result = converter(value);
                return true;
            }
            catch
            {
                result = default;
                return false;
            }
        }
    }
}
