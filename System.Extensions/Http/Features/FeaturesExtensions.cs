
namespace System.Extensions.Http
{
    using System.Diagnostics;
    using System.Text;
    using System.Buffers;
    using System.Dynamic;
    using System.IO;
    using System.IO.Compression;
    using System.Collections;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Linq.Expressions;
    using System.Net.Mime;
    using System.Threading;
    using System.Threading.Tasks;
    public static class FeaturesExtensions
    {
        private static Property<HttpRequest> _QueryParams = new Property<HttpRequest>("Features.QueryParams");
        public static IQueryParams QueryParams(this HttpRequest @this)
        {
            var queryParams = (IQueryParams)@this.Properties[_QueryParams];
            if (queryParams != null)
                return queryParams;

            var query = @this.Url.Query;
            if (string.IsNullOrEmpty(query))
            {
                @this.Properties[_QueryParams] = _EmptyQueryParams;
                return _EmptyQueryParams;
            }
            else 
            {
                var _queryParams = new QueryParams();
                _queryParams.Parse(query.AsSpan(1));
                @this.Properties[_QueryParams] = _queryParams;
                return _queryParams;
            }
        }
        public static HttpRequest QueryParams(this HttpRequest @this, IQueryParams queryParams)
        {
            @this.Properties[_QueryParams] = queryParams;
            return @this;
        }
        public static void QueryParams(this HttpRequest @this, out IQueryParams queryParams)
        {
            queryParams = (IQueryParams)@this.Properties[_QueryParams];
        }

        private static Property<HttpRequest> _PathParams = new Property<HttpRequest>("Features.PathParams");
        public static IPathParams PathParams(this HttpRequest @this)
        {
            var pathParams = (IPathParams)@this.Properties[_PathParams];
            return pathParams ?? _EmptyPathParams;
        }
        public static HttpRequest PathParams(this HttpRequest @this, IPathParams pathParams)
        {
            @this.Properties[_PathParams] = pathParams;
            return @this;
        }
        public static void PathParams(this HttpRequest @this, out IPathParams pathParams)
        {
            pathParams = (IPathParams)@this.Properties[_PathParams];
        }

        private static Property<HttpRequest> _FormParams = new Property<HttpRequest>("Features.FormParams");
        public static IFormParams FormParams(this HttpRequest @this)
        {
            var formParams = (IFormParams)@this.Properties[_FormParams];
            return formParams ?? _EmptyFormParams;
        }
        public static HttpRequest FormParams(this HttpRequest @this, IFormParams formParams)
        {
            @this.Properties[_FormParams] = formParams;
            return @this;
        }
        public static void FormParams(this HttpRequest @this, out IFormParams formParams)
        {
            formParams = (IFormParams)@this.Properties[_FormParams];
        }

        private static Property<HttpRequest> _CookieParams = new Property<HttpRequest>("Features.CookieParams");
        public static ICookieParams CookieParams(this HttpRequest @this)
        {
            var cookieParams = (ICookieParams)@this.Properties[_CookieParams];
            if (cookieParams != null)
                return cookieParams;

            var cookies = @this.Headers.GetValues(HttpHeaders.Cookie);
            if (cookies.Length == 0)
            {
                @this.Properties[_CookieParams] = _EmptyCookieParams;
                return _EmptyCookieParams;
            }
            else
            {
                var _cookieParams = new CookieParams();
                for (int i = 0; i < cookies.Length; i++)
                {
                    _cookieParams.Parse(cookies[i]);
                }
                @this.Properties[_CookieParams] = _cookieParams;
                return _cookieParams;
            }
        }
        public static HttpRequest CookieParams(this HttpRequest @this, ICookieParams cookieParams)
        {
            @this.Properties[_CookieParams] = cookieParams;
            return @this;
        }
        public static void CookieParams(this HttpRequest @this, out ICookieParams cookieParams)
        {
            cookieParams = (ICookieParams)@this.Properties[_CookieParams];
        }

        private static Property<HttpRequest> _FormFileParams = new Property<HttpRequest>("Features.FormFileParams");
        public static IFormFileParams FormFileParams(this HttpRequest @this)
        {
            var fileParams = (IFormFileParams)@this.Properties[_FormFileParams];
            return fileParams ?? _EmptyFormFileParams;
        }
        public static HttpRequest FormFileParams(this HttpRequest @this, IFormFileParams fileParams)
        {
            @this.Properties[_FormFileParams] = fileParams;
            return @this;
        }
        public static void FormFileParams(this HttpRequest @this, out IFormFileParams fileParams)
        {
            fileParams = (IFormFileParams)@this.Properties[_FormFileParams];
        }

        #region GetValue
        public static bool TryGetValue<T>(this IQueryParams @this, string name, out T value)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            var strConverter = GetValue<string, T>();
            if (strConverter != null)
            {
                if (@this.TryGetValue(name, out var strValue))
                {
                    try
                    {
                        value = strConverter(strValue);
                        return true;
                    }
                    catch
                    {
                        value = default;
                        return false;
                    }
                }
            }
            else
            {
                var strsConverter = GetValue<string[], T>();
                if (strsConverter != null)
                {
                    var strsValue = @this.GetValues(name);
                    try
                    {
                        value = strsConverter(strsValue);
                        return true;
                    }
                    catch
                    {
                        value = default;
                        return false;
                    }
                }
            }
            value = default;
            return false;
        }
        public static bool TryGetValue<T>(this IPathParams @this, string name, out T value)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            var strConverter = GetValue<string, T>();
            if (strConverter != null)
            {
                if (@this.TryGetValue(name, out var strValue))
                {
                    try
                    {
                        value = strConverter(strValue);
                        return true;
                    }
                    catch
                    {
                        value = default;
                        return false;
                    }
                }
            }
            else
            {
                var strsConverter = GetValue<string[], T>();
                if (strsConverter != null)
                {
                    var strsValue = @this.GetValues(name);
                    try
                    {
                        value = strsConverter(strsValue);
                        return true;
                    }
                    catch
                    {
                        value = default;
                        return false;
                    }
                }
            }
            value = default;
            return false;
        }
        public static bool TryGetValue<T>(this ICookieParams @this, string name, out T value)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            var strConverter = GetValue<string, T>();
            if (strConverter != null)
            {
                if (@this.TryGetValue(name, out var strValue))
                {
                    try
                    {
                        value = strConverter(strValue);
                        return true;
                    }
                    catch
                    {
                        value = default;
                        return false;
                    }
                }
            }
            else
            {
                var strsConverter = GetValue<string[], T>();
                if (strsConverter != null)
                {
                    var strsValue = @this.GetValues(name);
                    try
                    {
                        value = strsConverter(strsValue);
                        return true;
                    }
                    catch
                    {
                        value = default;
                        return false;
                    }
                }
            }
            value = default;
            return false;
        }
        public static bool TryGetValue<T>(this IFormParams @this, string name, out T value)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            var strConverter = GetValue<string, T>();
            if (strConverter != null)
            {
                if (@this.TryGetValue(name, out var strValue))
                {
                    try
                    {
                        value = strConverter(strValue);
                        return true;
                    }
                    catch
                    {
                        value = default;
                        return false;
                    }
                }
            }
            else
            {
                var strsConverter = GetValue<string[], T>();
                if (strsConverter != null)
                {
                    var strsValue = @this.GetValues(name);
                    try
                    {
                        value = strsConverter(strsValue);
                        return true;
                    }
                    catch
                    {
                        value = default;
                        return false;
                    }
                }
            }
            value = default;
            return false;
        }
        public static T GetValue<T>(this IQueryParams @this, string name)
        {
            TryGetValue<T>(@this, name, out var value);
            return value;
        }
        public static T GetValue<T>(this IPathParams @this, string name)
        {
            TryGetValue<T>(@this, name, out var value);
            return value;
        }
        public static T GetValue<T>(this ICookieParams @this, string name)
        {
            TryGetValue<T>(@this, name, out var value);
            return value;
        }
        public static T GetValue<T>(this IFormParams @this, string name)
        {
            TryGetValue<T>(@this, name, out var value);
            return value;
        }
        public static bool TryGetValue<T>(this IQueryParams @this, out T value)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            var converter = GetValue<IQueryParams, T>();
            if (converter == null)
            {
                value = default;
                return false;
            }
            try
            {
                value = converter(@this);
                return true;
            }
            catch
            {
                value = default;
                return false;
            }
        }
        public static bool TryGetValue<T>(this IFormParams @this, out T value)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            var converter = GetValue<IFormParams, T>();
            if (converter == null)
            {
                value = default;
                return false;
            }
            try
            {
                value = converter(@this);
                return true;
            }
            catch
            {
                value = default;
                return false;
            }
        }
        public static T GetValue<T>(this IQueryParams @this)
        {
            TryGetValue<T>(@this, out var value);
            return value;
        }
        public static T GetValue<T>(this IFormParams @this)
        {
            TryGetValue<T>(@this, out var value);
            return value;
        }
        private static class Converter
        {
            static Converter()
            {
                _Handlers = new Stack<object>();
                _PropertyResolver = (property) => property.SetMethod.IsPrivate ? null : property.Name;
                _ObjHandlers = new Dictionary<(Type, Type), Func<object, object>>();

                #region  Add
                var separator = new[] { '[', ']', '.' };//
                Action<IDictionary<string, object>, KeyValuePair<string, string>> add =
                    (@object, item) =>
                    {
                        var names = item.Key.Split(separator, StringSplitOptions.RemoveEmptyEntries);
                        if (names.Length > 0)
                        {
                            var temp = @object;
                            for (var index = 0; ;)
                            {
                                var name = names[index++];
                                if (index == names.Length)
                                {
                                    if (temp.TryGetValue(name, out var value))
                                    {
                                        if (value is string @string)
                                        {
                                            var list = new List<string>() { @string, item.Value };
                                            temp[name] = list;
                                        }
                                        else if (value is List<string> list)
                                        {
                                            list.Add(item.Value);
                                        }
                                    }
                                    else
                                    {
                                        temp.Add(name, item.Value);
                                    }
                                    break;
                                }
                                else
                                {
                                    if (temp.TryGetValue(name, out var value))
                                    {
                                        if (value is Dictionary<string, object> dictionary)
                                        {
                                            temp = dictionary;
                                        }
                                        else
                                        {
                                            break;
                                        }
                                    }
                                    else
                                    {
                                        var dictionary = new Dictionary<string, object>();
                                        temp.Add(name, dictionary);
                                        temp = dictionary;
                                    }
                                }
                            }
                        }
                    };
                #endregion
                //default
                Register((tValue, tResult, value) => {
                    var @object = Expression.Variable(typeof(Dictionary<string, object>), "@object");
                    Register(typeof(Dictionary<string, object>), tResult, @object, out var resultExpr, out _);
                    if (resultExpr == null)
                        return null;
                    Register(tValue, typeof(Dictionary<string, object>), value, out var objectExpr, out _);
                    if (objectExpr == null)
                        return null;
                    return Expression.Block(new[] { @object },
                        Expression.Assign(@object, objectExpr),
                        resultExpr
                        );
                });
                //=>Dictionary<string, object>
                Register((tValue, tResult, value) => {
                    if (tResult != typeof(Dictionary<string, object>))
                        return null;

                    var count = tValue.GetProperty("Count", typeof(int), Type.EmptyTypes);
                    var item = tValue.GetProperty("Item", typeof(KeyValuePair<string, string>), new[] { typeof(int) });
                    if (count == null || item == null)
                        return null;
                    var @object = Expression.Variable(typeof(Dictionary<string, object>), "@object");
                    var iArg = Expression.Variable(typeof(int), "i");
                    var countArg = Expression.Variable(typeof(int), "count");
                    var breakLabel = Expression.Label();
                    return Expression.Block(new[] { @object, iArg, countArg },
                        Expression.Assign(@object, Expression.New(typeof(Dictionary<string, object>).GetConstructor(Type.EmptyTypes))),
                        Expression.Assign(iArg, Expression.Constant(0)),
                        Expression.Assign(countArg, Expression.Property(value, count)),
                        Expression.Loop(
                            Expression.IfThenElse(
                                Expression.LessThan(iArg, countArg),
                                Expression.Invoke(Expression.Constant(add), @object, Expression.Property(value, item, Expression.PostIncrementAssign(iArg))),
                                Expression.Break(breakLabel)
                                ),
                            breakLabel),
                        @object
                        );
                });
                //string=>
                Register((tValue, tResult, value) => {
                    if (tValue != typeof(string))
                        return null;

                    System.Converter.Register(tValue, tResult, value, out var expression, out _);
                    if (expression == null)
                        return Expression.Empty();
                    return expression;
                });
                //List<string>[0]=>
                Register((tValue, tResult, value) => {
                    if (tValue != typeof(List<string>))
                        return null;

                    var @string = Expression.Variable(typeof(string), "@string");
                    Register(typeof(string), tResult, @string, out var expression, out _);
                    if (expression == null)
                        return Expression.Empty();
                    var getItem = typeof(List<string>).GetMethod("get_Item");
                    return Expression.Block(new[] { @string },
                        Expression.Assign(@string, Expression.Call(value, getItem, Expression.Constant(0))),
                        expression
                        );
                });
                //Dictionary<string, object>=>
                Register((tValue, tResult, value) => {
                    if (tValue != typeof(Dictionary<string, object>))
                        return null;
                    Constructor.Register(tResult, out var constructor, out _);
                    if (constructor == null)
                        return Expression.Empty();
                    var get = typeof(Converter).GetMethod("Get", Type.EmptyTypes);
                    var tryGetValue = tValue.GetMethod("TryGetValue");
                    var result = Expression.Variable(tResult, "result");
                    var variables = new List<ParameterExpression>() { result };
                    var exprs = new List<Expression>() { Expression.Assign(result, constructor) };
                    var properties = tResult.GetGeneralProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    foreach (var property in properties)
                    {
                        if (!property.CanWrite)
                            continue;
                        var propertyName = _PropertyResolver(property);
                        if (propertyName == null)
                            continue;
                        var propertyObject = Expression.Variable(typeof(object), $"object{property.Name}");
                        variables.Add(propertyObject);
                        exprs.Add(Expression.IfThen(
                            Expression.Call(value, tryGetValue, Expression.Constant(propertyName), propertyObject),
                            Expression.Assign(Expression.Property(result, property),
                            Expression.Condition(
                                Expression.TypeIs(propertyObject, typeof(string)),
                                Expression.Invoke(Expression.Call(get.MakeGenericMethod(typeof(string), property.PropertyType)), Expression.Convert(propertyObject, typeof(string))),
                                Expression.Condition(
                                    Expression.TypeIs(propertyObject, typeof(List<string>)),
                                    Expression.Invoke(Expression.Call(get.MakeGenericMethod(typeof(List<string>), property.PropertyType)), Expression.Convert(propertyObject, typeof(List<string>))),
                                    Expression.Condition(
                                        Expression.TypeIs(propertyObject, typeof(Dictionary<string, object>)),
                                        Expression.Invoke(Expression.Call(get.MakeGenericMethod(typeof(Dictionary<string, object>), property.PropertyType)), Expression.Convert(propertyObject, typeof(Dictionary<string, object>))),
                                        Expression.Default(property.PropertyType)
                                    ))))));
                    }
                    exprs.Add(result);
                    return Expression.Block(variables, exprs);
                });
                //List<string>=>Array
                Register((tValue, tResult, value) => {
                    if (tValue != typeof(List<string>))
                        return null;
                    if (!tResult.IsArray)
                        return null;

                    var eleType = tResult.GetElementType();
                    var listType = typeof(List<>).MakeGenericType(eleType);
                    var toArray = listType.GetMethod("ToArray", Type.EmptyTypes);
                    Register(tValue, listType, value, out var expression, out _);
                    if (expression == null)
                        return Expression.Empty();
                    return Expression.Call(expression, toArray);
                });
                //List<string>=>List<>
                Register((tValue, tResult, value) => {
                    if (tValue != typeof(List<string>))
                        return null;
                    if (!tResult.IsGenericType || tResult.GetGenericTypeDefinition() != typeof(List<>))
                        return null;

                    var eleType = tResult.GetGenericArguments()[0];
                    var item = Expression.Variable(typeof(string), "item");
                    Register(typeof(string), eleType, item, out var expression, out _);
                    if (expression == null)
                        return Expression.Empty();
                    var list = Expression.Variable(tResult, "list");
                    var i = Expression.Variable(typeof(int), "i");
                    var count = Expression.Variable(typeof(int), "count");
                    var getItem = tValue.GetMethod("get_Item");
                    var add = tResult.GetMethod("Add", BindingFlags.Instance | BindingFlags.Public, null, new[] { eleType }, null);
                    var breakLabel = Expression.Label();
                    return Expression.Block(new[] { list, i, count },
                        Expression.Assign(i, Expression.Constant(0)),
                        Expression.Assign(count, Expression.Property(value, "Count")),
                        Expression.Assign(list, Expression.New(tResult.GetConstructor(Type.EmptyTypes))),
                        Expression.Loop(
                            Expression.Block(new[] { item },
                                Expression.IfThen(
                                    Expression.GreaterThanOrEqual(i, count),
                                    Expression.Break(breakLabel)
                                    ),
                                Expression.Assign(item, Expression.Call(value, getItem, i)),
                                Expression.PostIncrementAssign(i),
                                Expression.Call(list, add, expression)
                        ), breakLabel),
                        list
                        );
                });
                //string[]=>Array
                Register((tValue, tResult, value) => {
                    if (tValue != typeof(string[]))
                        return null;
                    if (!tResult.IsArray)
                        return null;

                    var eleType = tResult.GetElementType();
                    var listType = typeof(List<>).MakeGenericType(eleType);
                    var toArray = listType.GetMethod("ToArray", Type.EmptyTypes);
                    Register(tValue, listType, value, out var expression, out _);
                    if (expression == null)
                        return Expression.Empty();
                    return Expression.Call(expression, toArray);
                });
                //string[]=>List<>
                Register((tValue, tResult, value) => {
                    if (tValue != typeof(string[]))
                        return null;
                    if (!tResult.IsGenericType || tResult.GetGenericTypeDefinition() != typeof(List<>))
                        return null;

                    var eleType = tResult.GetGenericArguments()[0];
                    var item = Expression.Variable(typeof(string), "item");
                    Register(typeof(string), eleType, item, out var expression, out _);
                    if (expression == null)
                        return Expression.Empty();
                    var list = Expression.Variable(tResult, "list");
                    var i = Expression.Variable(typeof(int), "i");
                    var length = Expression.Variable(typeof(int), "length");
                    var add = tResult.GetMethod("Add", BindingFlags.Instance | BindingFlags.Public, null, new[] { eleType }, null);
                    var breakLabel = Expression.Label();
                    return Expression.Block(new[] { list, i, length },
                        Expression.Assign(i, Expression.Constant(0)),
                        Expression.Assign(length, Expression.ArrayLength(value)),
                        Expression.Assign(list, Expression.New(tResult.GetConstructor(Type.EmptyTypes))),
                        Expression.Loop(
                            Expression.Block(new[] { item },
                                Expression.IfThen(
                                    Expression.GreaterThanOrEqual(i, length),
                                    Expression.Break(breakLabel)
                                    ),
                                Expression.Assign(item, Expression.ArrayAccess(value, i)),
                                Expression.PostIncrementAssign(i),
                                Expression.Call(list, add, expression)
                        ), breakLabel),
                        list
                        );
                });
                //Dynamic
                Register<Dictionary<string, object>, DynamicObject>((value) => new DynamicValue(value));
            }
            private static readonly object _Sync = new object();
            private static Stack<object> _Handlers;
            private static Func<PropertyInfo, string> _PropertyResolver;
            private static Dictionary<(Type, Type), Func<object, object>> _ObjHandlers;
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
            #region Dynamic
            private class DynamicValue : DynamicObject
            {
                public static DynamicValue Undefined = new DynamicValue(null);
                private object _value;
                public DynamicValue(object value)
                    : base()
                {
                    _value = value;
                }
                public override IEnumerable<string> GetDynamicMemberNames()
                {
                    if (_value is Dictionary<string, object> @object)
                        return @object.Keys;

                    return Array.Empty<string>();
                }
                public override bool TryGetMember(GetMemberBinder binder, out object result)
                {
                    if (_value is Dictionary<string, object> @object)
                    {
                        if (@object.TryGetValue(binder.Name, out var item))
                        {
                            result = new DynamicValue(item);
                            return true;
                        }
                    }
                    result = Undefined;
                    return true;
                }
                public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
                {
                    if (indexes.Length == 1)
                    {
                        var index = indexes[0];
                        if (index is int @int)
                        {
                            if (_value is string)
                            {
                                if (@int == 0)
                                {
                                    result = this;
                                    return true;
                                }
                            }
                            else if (_value is List<string> list)
                            {
                                if (@int >= 0 && @int < list.Count)
                                {
                                    result = new DynamicValue(list[@int]);
                                    return true;
                                }
                            }
                        }
                        else if (index is string @string)
                        {
                            if (_value is Dictionary<string, object> @object)
                            {
                                if (@object.TryGetValue(@string, out var item))
                                {
                                    result = new DynamicValue(item);
                                    return true;
                                }
                            }
                        }
                    }
                    result = Undefined;
                    return true;
                }
                public override bool TryConvert(ConvertBinder binder, out object result)
                {
                    var type = binder.Type;
                    if (_value != null)
                    {
                        var converter = Get(_value.GetType(), type);
                        if (converter != null)
                        {
                            try
                            {
                                result = converter(_value);
                                return true;
                            }
                            catch
                            { }
                        }
                    }
                    result = type.IsValueType ? Activator.CreateInstance(type) : null;
                    return true;
                }
                public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
                {
                    switch (binder.Name)
                    {
                        case "IsUndefined":
                            result = _value == null ? true : false;
                            return true;
                        case "Value":
                            result = _value;
                            return true;
                    }
                    return base.TryInvokeMember(binder, args, out result);
                }
            }
            #endregion
            public static void RegisterProperty(Func<PropertyInfo, string> propertyResolver)
            {
                if (propertyResolver == null)
                    throw new ArgumentNullException(nameof(propertyResolver));

                lock (_Sync)
                {
                    _PropertyResolver = propertyResolver;
                }
            }
            public static void Register<TValue, TResult>(Func<TValue, TResult> handler)
            {
                lock (_Sync)
                {
                    _Handlers.Push(new Tuple<Type, Type, Delegate>(typeof(TValue), typeof(TResult), handler));
                }
            }
            public static void Register(Type tValue, Type tResult, Func<ParameterExpression, Expression> handler)
            {
                Register((_tValue, _tResult, value) => {
                    if (_tValue == tValue && _tResult == tResult)
                        return handler(value);

                    return null;
                });
            }
            public static void Register(Func<Type, Type, ParameterExpression, Expression> handler)
            {
                lock (_Sync)
                {
                    _Handlers.Push(handler);
                }
            }
            public static void Register(Type tValue, Type tResult, ParameterExpression value, out Expression expression, out Delegate @delegate)
            {
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
                                var expr = Expression.Block(new[] { value },
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
        }
        public static void GetValue(Func<PropertyInfo, string> propertyResolver)
        {
            if (propertyResolver == null)
                throw new ArgumentNullException(nameof(propertyResolver));

            Converter.RegisterProperty(propertyResolver);
        }
        public static void GetValue<TValue, T>(Func<TValue, T> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            Converter.Register<TValue, T>(handler);
        }
        public static void GetValue(Type tValue, Type tResult, Func<ParameterExpression, Expression> handler)
        {
            if (tValue == null)
                throw new ArgumentNullException(nameof(tValue));
            if (tResult == null)
                throw new ArgumentNullException(nameof(tResult));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            Converter.Register(tValue, tResult, handler);
        }
        public static void GetValue(Func<Type, Type, ParameterExpression, Expression> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            Converter.Register(handler);
        }
        public static void GetValue(Type tValue, Type tResult, ParameterExpression value, out Expression expression, out Delegate @delegate)
        {
            if (tValue == null)
                throw new ArgumentNullException(nameof(tValue));
            if (tResult == null)
                throw new ArgumentNullException(nameof(tResult));
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            Converter.Register(tValue, tResult, value, out expression, out @delegate);
        }
        public static Func<TValue, T> GetValue<TValue, T>()
        {
            return Converter.Get<TValue, T>();
        }
        public static Func<object, object> GetValue(Type tValue, Type tResult)
        {
            return Converter.Get(tValue, tResult);
        }
        #endregion

        private static Property<HttpRequest> _RequestDisposables = new Property<HttpRequest>("Features.RequestDisposables");
        public static void RegisterForDispose(this HttpRequest @this, IDisposable disposable)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (disposable == null)
                return;

            lock (@this) 
            {
                var disposables = (IList<IDisposable>)@this.Properties[_RequestDisposables];
                if (disposables == null)
                {
                    disposables = new List<IDisposable>();
                    @this.Properties[_RequestDisposables] = disposables;
                }
                disposables.Add(disposable);
            }
        }

        private static Property<HttpRequest> _RequestDisposableResponses = new Property<HttpRequest>("Features.RequestDisposableResponses");
        public static void RegisterForDispose(HttpRequest request, HttpResponse response)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (response == null)
                return;

            lock (request) 
            {
                var responses = (HttpResponse[])request.Properties[_RequestDisposableResponses];
                if (responses == null)
                {
                    responses = new[] { response };
                    request.Properties[_RequestDisposableResponses] = responses;
                }
                else
                {
                    var length = responses.Length;
                    Array.Resize(ref responses, length + 1);
                    responses[length] = response;
                    request.Properties[_RequestDisposableResponses] = responses;
                }
            }
        }
        public static void Dispose(this HttpRequest @this)
        {
            if (@this == null)
                return;

            lock (@this) 
            {
                var responses = (HttpResponse[])@this.Properties[_RequestDisposableResponses];
                if (responses != null)
                {
                    @this.Properties[_RequestDisposableResponses] = null;
                    for (int i = 0; i < responses.Length; i++)
                    {
                        try
                        {
                            responses[i].Dispose();
                        }
                        catch (Exception ex)
                        {
                            Trace.WriteLine(ex, "UnobservedException");
                        }
                    }
                }
                var disposables = (IList<IDisposable>)@this.Properties[_RequestDisposables];
                if (disposables != null)
                {
                    @this.Properties[_RequestDisposables] = null;
                    for (int i = disposables.Count - 1; i >= 0; i--)
                    {
                        try
                        {
                            disposables[i].Dispose();
                        }
                        catch (Exception ex)
                        {
                            Trace.WriteLine(ex, "UnobservedException");
                        }
                    }
                }
            }
        }

        private static Property<HttpResponse> _ResponseDisposables = new Property<HttpResponse>("Features.ResponseDisposables");
        public static void RegisterForDispose(this HttpResponse @this, IDisposable disposable)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (disposable == null)
                return;

            lock (@this) 
            {
                var disposables = (IList<IDisposable>)@this.Properties[_ResponseDisposables];
                if (disposables == null)
                {
                    disposables = new List<IDisposable>();
                    @this.Properties[_ResponseDisposables] = disposables;
                }
                disposables.Add(disposable);
            }
        }
        public static void Dispose(this HttpResponse @this)
        {
            if (@this == null)
                return;

            lock (@this) 
            {
                var disposables = (IList<IDisposable>)@this.Properties[_ResponseDisposables];
                if (disposables != null)
                {
                    @this.Properties[_ResponseDisposables] = null;
                    for (int i = disposables.Count - 1; i >= 0; i--)
                    {
                        try
                        {
                            disposables[i].Dispose();
                        }
                        catch (Exception ex)
                        {
                            Trace.WriteLine(ex, "UnobservedException");
                        }
                    }
                }
            }
        }
        //TODO Remove?
        //wait extend syntax(new HttpResponse(request))
        public static HttpResponse CreateResponse(this HttpRequest @this)
        {
            var response = new HttpResponse();
            RegisterForDispose(@this, response);
            return response;
        }

        private static Property<HttpRequest> _RequestItems = new Property<HttpRequest>("Features.RequestItems");
        public static IDictionary<string, object> Items(this HttpRequest @this)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            var items = (IDictionary<string, object>)@this.Properties[_RequestItems];
            if (items == null)
            {
                items = new Dictionary<string, object>();
                @this.Properties[_RequestItems] = items;
            }
            return items;
        }
        public static void Items(this HttpRequest @this, IDictionary<string, object> items)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            @this.Properties[_RequestItems] = items;
        }
        public static void Items(this HttpRequest @this, out IDictionary<string, object> items)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            items = (IDictionary<string, object>)@this.Properties[_RequestItems];
        }

        private static Property<HttpResponse> _ResponseItems = new Property<HttpResponse>("Features.ResponseItems");
        public static IDictionary<string, object> Items(this HttpResponse @this)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            var items = (IDictionary<string, object>)@this.Properties[_ResponseItems];
            if (items == null)
            {
                items = new Dictionary<string, object>();
                @this.Properties[_ResponseItems] = items;
            }
            return items;
        }
        public static void Items(this HttpResponse @this, IDictionary<string, object> items)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            @this.Properties[_ResponseItems] = items;
        }
        public static void Items(this HttpResponse @this, out IDictionary<string, object> items)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            items = (IDictionary<string, object>)@this.Properties[_ResponseItems];
        }

        public static Task DrainAsync(this IHttpContent @this)
        {
            return DrainAsync(@this, 0);
        }
        public static async Task DrainAsync(this IHttpContent @this, long maxDrain)
        {
            if (@this == null)
                return;

            var bytes = ArrayPool<byte>.Shared.Rent(8192);
            try
            {
                if (maxDrain <= 0)
                {
                    for (; ; )
                    {
                        var result = await @this.ReadAsync(bytes);
                        if (result == 0)
                            return;
                    }
                }
                else
                {
                    var drained = 0;
                    for (; ; )
                    {
                        var result = await @this.ReadAsync(bytes);
                        if (result == 0)
                            return;
                        drained += result;
                        if (drained > maxDrain)
                            throw new InvalidDataException(nameof(maxDrain));
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(bytes);
            }
        }
        public static Task<string> ReadStringAsync(this IHttpContent @this)
        {
            return ReadStringAsync(@this, Encoding.UTF8);
        }
        public static async Task<string> ReadStringAsync(this IHttpContent @this, Encoding encoding)
        {
            if (@this == null)
                return null;
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            var bytes = ArrayPool<byte>.Shared.Rent(8192);
            var sb = StringContent.Rent(out var disposable);
            var decoder = encoding.GetDecoder();
            try
            {
                for (var offset = 0; ;)//BOM OR ([0]=='\uFEFF' sb.ToString(1))
                {
                    var result = await @this.ReadAsync(bytes, offset, bytes.Length - offset);
                    if (result == 0)
                    {
                        sb.WriteBytes(encoding.GetPreamble(bytes.AsSpan(0, offset)), true, decoder);
                        return sb.ToString();
                    }
                    offset += result;
                    if (offset > 4)
                    {
                        sb.WriteBytes(encoding.GetPreamble(bytes.AsSpan(0, offset)), false, decoder);
                        break;
                    }
                }

                for (; ; )
                {
                    var result = await @this.ReadAsync(bytes);
                    if (result == 0)
                    {
                        sb.WriteBytes(ReadOnlySpan<byte>.Empty, true, decoder);
                        return sb.ToString();
                    }

                    sb.WriteBytes(bytes.AsSpan(0, result), false, decoder);
                }
            }
            finally
            {
                disposable.Dispose();
                ArrayPool<byte>.Shared.Return(bytes);
            }
        }
        public static Task ReadStringAsync(this IHttpContent @this, BufferWriter<char> writer)
        {
            return ReadStringAsync(@this, writer, Encoding.UTF8);
        }
        public static async Task ReadStringAsync(this IHttpContent @this, BufferWriter<char> writer, Encoding encoding)
        {
            if (@this == null)
                return;
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            var bytes = ArrayPool<byte>.Shared.Rent(8192);
            var decoder = encoding.GetDecoder();
            try
            {
                for (var offset = 0; ;)//BOM
                {
                    var result = await @this.ReadAsync(bytes, offset, bytes.Length - offset);
                    if (result == 0)
                    {
                        writer.WriteBytes(encoding.GetPreamble(bytes.AsSpan(0, offset)), true, decoder);
                        return;
                    }
                    offset += result;
                    if (offset > 4)
                    {
                        writer.WriteBytes(encoding.GetPreamble(bytes.AsSpan(0, offset)), false, decoder);
                        break;
                    }
                }

                for (; ; )
                {
                    var result = await @this.ReadAsync(bytes);
                    if (result == 0) 
                    {
                        writer.WriteBytes(ReadOnlySpan<byte>.Empty, true, decoder);
                        return;
                    }

                    writer.WriteBytes(bytes.AsSpan(0, result), false, decoder);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(bytes);
            }
        }
        public static Task<T> ReadJsonAsync<T>(this IHttpContent @this)
        {
            return ReadJsonAsync<T>(@this, Encoding.UTF8);
        }
        public static async Task<T> ReadJsonAsync<T>(this IHttpContent @this, Encoding encoding)
        {
            if (@this == null)
                return default;
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            var bytes = ArrayPool<byte>.Shared.Rent(8192);
            var sb = StringContent.Rent(out var disposable);
            var decoder = encoding.GetDecoder();
            try
            {
                //TODO? Bom Unnecessary
                for (; ; )
                {
                    var result = await @this.ReadAsync(bytes);
                    if (result == 0) 
                    {
                        sb.WriteBytes(ReadOnlySpan<byte>.Empty, true, decoder);
                        return JsonReader.FromJson<T>(sb.Sequence);
                    }

                    sb.WriteBytes(bytes.AsSpan(0, result), false, decoder);
                }
            }
            finally
            {
                disposable.Dispose();
                ArrayPool<byte>.Shared.Return(bytes);
            }
        }
        public static Task<Stream> ReadStreamAsync(this IHttpContent @this)
        {
            return ReadStreamAsync(@this, 64 * 1024);//64K
        }
        public static async Task<Stream> ReadStreamAsync(this IHttpContent @this, int maxBufferSize)
        {
            if (@this == null)
                return Stream.Null;

            if (maxBufferSize <= 0)
            {
                var bytes = ArrayPool<byte>.Shared.Rent(8192);
                try
                {
                    var fs = new FileStream(Path.GetTempFileName(), FileMode.Open, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
                    for (; ; )
                    {
                        var result = await @this.ReadAsync(bytes);
                        if (result == 0)
                        {
                            fs.Position = 0;
                            return fs;
                        }

                        await fs.WriteAsync(bytes.AsMemory(0, result));
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(bytes);
                }
            }
            else
            {
                var buffer = MemoryContent.Rent(out var disposable);
                try
                {
                    var bufferSize = 0;
                    for (; ; )
                    {
                        var result = await @this.ReadAsync(buffer.GetMemory());
                        if (result == 0)
                            return new BufferedContentStream(buffer, disposable);

                        buffer.Advance(result);
                        bufferSize += result;
                        if (bufferSize >= maxBufferSize)
                            break;
                    }

                    var bytes = ArrayPool<byte>.Shared.Rent(8192);
                    try
                    {
                        var fs = new FileStream(Path.GetTempFileName(), FileMode.Open, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
                        for (; ; )
                        {
                            var result = await @this.ReadAsync(bytes);
                            if (result == 0)
                            {
                                fs.Position = 0;
                                return new BufferedContentStream(buffer, disposable, fs);
                            }

                            await fs.WriteAsync(bytes.AsMemory(0, result));
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(bytes);
                    }
                }
                catch
                {
                    disposable.Dispose();
                    throw;
                }
            }
        }
        //TODO?? Unfriendly
        //public static async Task<Stream> ReadStreamAsync(this IHttpContent @this, Buffer<byte> buffer, IDisposable disposable, int maxBufferSize)
        //{
        //    if (@this == null)
        //        throw new ArgumentNullException(nameof(@this));
        //    if (maxBufferSize <= 0)
        //        throw new ArgumentNullException(nameof(maxBufferSize));

        //    var bufferSize = 0;
        //    for (; ; )
        //    {
        //        var result = await @this.ReadAsync(buffer.GetMemory());
        //        if (result == 0)
        //            return new _BufferedStream(buffer, disposable);

        //        buffer.Advance(result);
        //        bufferSize += result;
        //        if (bufferSize >= maxBufferSize)
        //            break;
        //    }

        //    var bytes = ArrayPool<byte>.Shared.Rent(8192);
        //    try
        //    {
        //        var fs = new FileStream(Path.GetTempFileName(), FileMode.Open, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        //        for (; ; )
        //        {
        //            var result = await @this.ReadAsync(bytes);
        //            if (result == 0)
        //            {
        //                fs.Position = 0;
        //                return new _BufferedStream(buffer, disposable, fs);
        //            }

        //            await fs.WriteAsync(bytes.AsMemory(0, result));
        //        }
        //    }
        //    finally
        //    {
        //        ArrayPool<byte>.Shared.Return(bytes);
        //    }
        //}
        public static Task ReadFormAsync(this IHttpContent @this, FormParams formParams)
        {
            return ReadFormAsync(@this, formParams, Encoding.UTF8);
        }
        public static async Task ReadFormAsync(this IHttpContent @this, FormParams formParams, Encoding encoding)
        {
            if (@this == null)
                return;
            if (formParams == null)
                throw new ArgumentNullException(nameof(formParams));
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            var bytes = ArrayPool<byte>.Shared.Rent(8192);
            var sb = StringExtensions.Rent(out var disposable);
            try
            {
                string formName = null;
                for (; ; )
                {
                    var result = await @this.ReadAsync(bytes);
                    if (result == 0)
                    {
                        if (formName == null)
                            formParams.Add(Url.Decode(sb.Sequence, encoding), string.Empty);
                        else
                            formParams.Add(formName, Url.Decode(sb.Sequence, encoding));
                        return;
                    }
                    for (int i = 0; i < result; i++)
                    {
                        var tempByte = bytes[i];
                        if (tempByte == '=' && formName == null)
                        {
                            formName = Url.Decode(sb.Sequence, encoding);
                            sb.Clear();
                        }
                        else if (tempByte == '&')
                        {
                            if (formName == null)
                                formParams.Add(Url.Decode(sb.Sequence, encoding), string.Empty);
                            else
                                formParams.Add(formName, Url.Decode(sb.Sequence, encoding));
                            sb.Clear();
                            formName = null;
                        }
                        else
                        {
                            sb.Write((char)tempByte);
                        }
                    }
                }
            }
            finally
            {
                disposable.Dispose();
                ArrayPool<byte>.Shared.Return(bytes);
            }
        }
        public static Task<IDisposable> ReadFormDataAsync(this IHttpContent @this, FormParams formParams, FormFileParams formFileParams, string boundary)
        {
            return ReadFormDataAsync(@this, formParams, Encoding.UTF8, int.MaxValue, formFileParams, boundary);
        }
        public static async Task<IDisposable> ReadFormDataAsync(this IHttpContent @this, FormParams formParams, Encoding encoding, int maxForm, FormFileParams formFileParams, string boundary)
        {
            if (@this == null)
                return null;
            if (boundary == null)
                throw new ArgumentNullException(nameof(boundary));
            if (boundary.Length == 0 || boundary.Length > 250)//OR (skip int[])
                throw new ArgumentOutOfRangeException(nameof(boundary));
            if (maxForm < 0)
                maxForm = int.MaxValue;

            const int MaxHeader = 4096;
            const byte DASHByte = (byte)'-', SPByte = (byte)' ', CRByte = (byte)'\r', LFByte = (byte)'\n', COLONByte = (byte)':';
            var matchBytesLength = boundary.Length + 4;
            #region matchBytes Skip
            var matchBytes = new byte[matchBytesLength];
            matchBytes[0] = CRByte;
            matchBytes[1] = LFByte;
            matchBytes[2] = DASHByte;
            matchBytes[3] = DASHByte;
            for (int i = 0; i < boundary.Length; i++)
            {
                matchBytes[i + 4] = (byte)boundary[i];
            }
            var skip = new byte[256];
            for (var i = 0; i < skip.Length; ++i)
            {
                skip[i] = (byte)matchBytesLength;
            }
            for (var i = 0; i < matchBytesLength; ++i)
            {
                skip[matchBytes[i]] = (byte)Math.Max(1, matchBytesLength - 1 - i);//0=error
            }
            #endregion
            var matchBytesLengthMinusOne = matchBytesLength - 1;
            var matchBytesLastByte = matchBytes[matchBytesLengthMinusOne];

            var formSize = 0;
            var bytes = ArrayPool<byte>.Shared.Rent(8192);
            var sb = StringExtensions.Rent(out var disposable);
            var tempFiles = new List<string>();
            var decoder = encoding.GetDecoder();
            try
            {
                if (bytes.Length < matchBytesLength)
                    throw new ArgumentOutOfRangeException(nameof(matchBytes));
                int start = 0, end = 0;
                #region Begin --{boundary}
                do
                {
                    var result = await @this.ReadAsync(bytes.AsMemory(end));
                    if (result == 0)
                        throw new FormatException("EOF");

                    end += result;
                } while (end < matchBytesLength);
                for (int i = 0; i < matchBytesLength - 2; i++)
                {
                    if (bytes[start++] != matchBytes[i + 2])
                        throw new FormatException();
                }
                #endregion
                var state = State.Boundary;
                for (; ; )
                {
                    #region Section Header
                    Debug.WriteLine("Parse Section Header");
                    var headerFlag = 0;//0没有 1=ContentDisposition 2=ContentType 3=其他
                    string name = null, fileName = null, contentType = null;
                    int _HeaderFlag(Buffer<char> _sb)
                    {
                        var seq = _sb.Sequence;
                        var span = seq.IsSingleSegment ? seq.First.Span : seq.ToArray();
                        if (span.IsEmpty)
                            throw new InvalidDataException("headerName length must >0");
                        if (span.EqualsIgnoreCase(HttpHeaders.ContentType))
                            return 2;
                        else if (span.EqualsIgnoreCase(HttpHeaders.ContentDisposition))
                            return 1;
                        else
                            return 3;
                    }
                    void _Parse(Buffer<char> _sb, ref string _name, ref string _fileName)
                    {
                        if (_name != null || _fileName != null)
                            return;

                        var seq = _sb.Sequence;
                        var span = seq.IsSingleSegment ? seq.First.Span : seq.ToArray();
                        if (HttpHeaders.TryParse(span, out var formData, "name", out var nameSpan, "filename", out var fileNameSpan))
                        {
                            if (formData.EqualsIgnoreCase("form-data"))
                            {
                                //_name = Url.Decode(nameSpan, encoding);
                                //if (!fileNameSpan.IsEmpty)
                                //    _fileName = Url.Decode(fileNameSpan, encoding);
                                _name = encoding.GetString(nameSpan);
                                if (fileNameSpan != null)
                                    _fileName = encoding.GetString(fileNameSpan);
                            }
                        }
                    }
                    for (var length = 0; ;)
                    {
                        for (; start < end;)
                        {
                            var tempByte = bytes[start++];
                            switch (state)
                            {
                                case State.Boundary:
                                    if (tempByte == CRByte)
                                    {
                                        state = State.Cr;
                                        continue;
                                    }
                                    else if (tempByte == DASHByte)
                                    {
                                        state = State.Dash;
                                        continue;
                                    }
                                    throw new InvalidDataException(@"{boundary} cr or -");
                                case State.Dash:
                                    if (tempByte != DASHByte)
                                        throw new InvalidDataException(@"--");
                                    state = State.DashDash;
                                    continue;
                                case State.DashDash:
                                    if (tempByte != CRByte)
                                        throw new InvalidDataException(@"--\r");
                                    state = State.DashCr;
                                    continue;
                                case State.DashCr:
                                    if (tempByte != LFByte)
                                        throw new InvalidDataException(@"--\r\n");
                                    if (start != end || @this.Available != 0)
                                        throw new InvalidDataException("EOF");
                                    return Disposable.Create(() => {
                                        foreach (var path in tempFiles)
                                        {
                                            try { File.Delete(path); } catch { }
                                        }
                                    });
                                case State.Cr:
                                    if (tempByte != LFByte)
                                        throw new InvalidDataException(@"\r\n");
                                    state = State.Lf;
                                    continue;
                                case State.Lf:
                                    if (tempByte == CRByte)
                                    {
                                        state = State.LfCr;
                                        continue;
                                    }
                                    for (; ; )
                                    {
                                        if (tempByte == COLONByte)
                                        {
                                            state = State.Colon;
                                            headerFlag = _HeaderFlag(sb);
                                            sb.Clear();
                                            break;
                                        }
                                        else
                                        {
                                            if (length++ == MaxHeader)
                                                throw new InvalidDataException(nameof(MaxHeader));
                                            //if (tempByte < SPByte || tempByte >= 127)
                                            //    throw new InvalidDataException("bad character");
                                            sb.Write((char)tempByte);
                                        }
                                        if (start >= end)
                                        {
                                            state = State.Name;
                                            break;
                                        }
                                        tempByte = bytes[start++];
                                    }
                                    continue;
                                case State.LfCr:
                                    if (tempByte != LFByte)
                                        throw new InvalidDataException(@"\r\n\r\n");
                                    goto SectionContent;
                                case State.Name:
                                    for (; ; )
                                    {
                                        if (tempByte == COLONByte)
                                        {
                                            state = State.Colon;
                                            headerFlag = _HeaderFlag(sb);
                                            sb.Clear();
                                            break;
                                        }
                                        else
                                        {
                                            if (length++ == MaxHeader)
                                                throw new InvalidDataException(nameof(MaxHeader));
                                            //if (tempByte < SPByte || tempByte >= 127)
                                            //    throw new InvalidDataException("bad character");
                                            sb.Write((char)tempByte);
                                        }
                                        if (start >= end)
                                            break;
                                        tempByte = bytes[start++];
                                    }
                                    continue;
                                case State.Colon:
                                    if (tempByte != SPByte)
                                        throw new InvalidDataException(@": ");
                                    state = State.Value;
                                    continue;
                                case State.Value:
                                    for (; ; )
                                    {
                                        if (tempByte == CRByte)
                                        {
                                            state = State.Cr;
                                            if (headerFlag == 1)
                                                _Parse(sb, ref name, ref fileName);
                                            else if (headerFlag == 2)
                                                contentType = sb.ToString();
                                            sb.Clear();
                                            headerFlag = 0;
                                            break;
                                        }
                                        else
                                        {
                                            if (length++ == MaxHeader)
                                                throw new InvalidDataException(nameof(MaxHeader));
                                            //if (tempByte < SPByte || tempByte >= 127)
                                            //    throw new InvalidDataException("bad character");
                                            sb.Write((char)tempByte);
                                        }
                                        if (start >= end)
                                            break;
                                        tempByte = bytes[start++];
                                    }
                                    continue;
                            }
                        }
                        var result = await @this.ReadAsync(bytes);
                        if (result == 0)
                            throw new FormatException("EOF");
                        start = 0;
                        end = result;
                    }
                    #endregion

                    #region Section Content
                    SectionContent:
                    Debug.WriteLine("Parse Section Content");
                    FileStream tempFile = null;
                    try
                    {
                        for (; ; )
                        {
                            var matchOffset = 0;
                            var matchCount = 0;
                            // case 1: does segment1 fully contain matchBytes?
                            {
                                var segmentEndMinusMatchBytesLength = end - matchBytesLength;
                                matchOffset = start;
                                while (matchOffset < segmentEndMinusMatchBytesLength)
                                {
                                    var lookaheadTailChar = bytes[matchOffset + matchBytesLengthMinusOne];
                                    if (lookaheadTailChar == matchBytesLastByte)
                                    {
                                        var offset1 = matchOffset;
                                        var offset2 = 0;
                                        var count = matchBytesLengthMinusOne;
                                        for (; count-- > 0; offset1++, offset2++)
                                        {
                                            if (bytes[offset1] != matchBytes[offset2])
                                                goto continueMatch;
                                        }
                                        matchCount = matchBytesLength;
                                        goto matchComplete;
                                    }
                                continueMatch:
                                    matchOffset += skip[lookaheadTailChar];
                                }
                            }
                            // case 2: does segment1 end with the start of matchBytes?
                            matchCount = 0;
                            for (; matchOffset < end; matchOffset++)
                            {
                                var countLimit = end - matchOffset;
                                for (matchCount = 0; matchCount < matchBytesLength && matchCount < countLimit; matchCount++)
                                {
                                    if (matchBytes[matchCount] != bytes[matchOffset + matchCount])
                                    {
                                        matchCount = 0;
                                        break;
                                    }
                                }
                                if (matchCount > 0)
                                {
                                    break;
                                }
                            }
                        matchComplete:
                            Debug.WriteLine("matchComplete");
                            if (fileName == null)
                            {
                                formSize += matchOffset - start;
                                if (formSize > maxForm)
                                    throw new InvalidDataException(nameof(maxForm));
                                sb.WriteBytes(bytes.AsSpan(start, matchOffset - start), false, decoder);
                                if (matchCount == matchBytesLength)
                                {
                                    formParams.Add(name, sb.ToString());
                                    sb.Clear();
                                    decoder.Reset();
                                    start = matchOffset + matchCount;
                                    state = State.Boundary;
                                    break;
                                }
                                start = matchOffset;
                            }
                            else
                            {
                                if (matchOffset != start)
                                {
                                    if (tempFile == null)
                                    {
                                        var tempFilePath = Path.GetTempFileName();//tempFile
                                        tempFiles.Add(tempFilePath);
                                        tempFile = new FileStream(tempFilePath, FileMode.Open, FileAccess.Write, FileShare.None, 1, FileOptions.Asynchronous);
                                    }

                                    await tempFile.WriteAsync(bytes, start, matchOffset - start);
                                }
                                if (matchCount == matchBytesLength)
                                {
                                    if (tempFile == null)
                                    {
                                        formFileParams.Add(name, fileName, contentType, Stream.Null);//?
                                    }
                                    else
                                    {
                                        var tempFilePath = tempFile.Name;
                                        tempFile.Dispose();
                                        tempFile = null;
                                        formFileParams.Add(name, fileName, contentType, new FileInfo(tempFilePath));
                                    }
                                    start = matchOffset + matchCount;
                                    state = State.Boundary;
                                    break;
                                }
                                start = matchOffset;
                            }

                            #region ReadAsync
                            var available = end - start;
                            Debug.Assert(available < matchBytesLength);
                            if (available == 0)
                            {
                                start = 0;
                                end = 0;
                            }
                            else
                            {
                                if (start > bytes.Length - matchBytesLength)
                                {
                                    bytes.AsSpan(start, available).CopyTo(bytes);
                                    start = 0;
                                    end = available;
                                }
                            }
                            do
                            {
                                var result = await @this.ReadAsync(bytes.AsMemory(end));
                                if (result == 0)
                                    throw new FormatException("EOF");
                                end += result;
                            } while (end - start < matchBytesLength);
                            #endregion
                        }
                    }
                    catch 
                    {
                        if (tempFile != null)
                            tempFile.Dispose();
                        throw;
                    }
                    #endregion
                }
            }
            catch
            {
                #region delete tempFiles
                foreach (var path in tempFiles)
                {
                    try { File.Delete(path); } catch { }
                }
                throw;
                #endregion
            }
            finally
            {
                disposable.Dispose();
                ArrayPool<byte>.Shared.Return(bytes);
            }
        }
        public static async Task<long> ReadFileAsync(this IHttpContent @this, string path)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            var bytes = ArrayPool<byte>.Shared.Rent(8192);
            //0byte yes create,not use FileMode.OpenOrCreate
            var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 1, FileOptions.Asynchronous);
            try
            {
                for (; ; )
                {
                    var result = await @this.ReadAsync(bytes);
                    if (result == 0)
                        return fs.Length;

                    await fs.WriteAsync(bytes, 0, result);
                }
            }
            finally
            {
                fs.Close();
                ArrayPool<byte>.Shared.Return(bytes);
            }
        }
        public static async Task ReadFormAsync(HttpRequest request, int maxForm, int maxFormData)
        {
            static bool TryParseForm(IHttpHeaders headers, out Encoding _encoding, out string _boundary)
            {
                _encoding = null; _boundary = null;
                if (!headers.TryGetValue(HttpHeaders.ContentType, out var contentType))
                    return false;

                if (!HttpHeaders.TryParse(contentType, out var contentTypeSpan, "charset", out var charsetSpan, "boundary", out var boundarySpan))
                    return false;

                if (contentTypeSpan.EqualsIgnoreCase("application/x-www-form-urlencoded"))
                {
                    if (charsetSpan == null || charsetSpan.IsEmpty || charsetSpan.EqualsIgnoreCase("utf-8"))
                    {
                        _encoding = Encoding.UTF8;
                        return true;
                    }
                    try
                    {
                        _encoding = Encoding.GetEncoding(new string(charsetSpan));
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                }
                else if (contentTypeSpan.EqualsIgnoreCase("multipart/form-data"))
                {
                    if (charsetSpan == null || charsetSpan.IsEmpty || charsetSpan.EqualsIgnoreCase("utf-8"))
                    {
                        _encoding = Encoding.UTF8;
                    }
                    else
                    {
                        try
                        {
                            _encoding = Encoding.GetEncoding(new string(charsetSpan));
                        }
                        catch
                        {
                            return false;
                        }
                    }

                    if (boundarySpan.Length == 0)
                        return false;

                    _boundary = new string(boundarySpan);
                    return true;
                }
                else
                {
                    return false;
                }
            }

            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (request.Properties[_FormParams] != null)
                return;

            if (!TryParseForm(request.Headers, out var encoding, out var boundary))
                return;

            if (request.Content == null)
            {
                request.FormParams(_EmptyFormParams);
                request.FormFileParams(_EmptyFormFileParams);
                return;
            }

            if (boundary == null)//form
            {
                if (maxForm <= 0)
                {
                    request.FormParams(_EmptyFormParams);
                    request.FormFileParams(_EmptyFormFileParams);
                    return;
                }
                var formParams = new FormParams();
                try
                {
                    var content = request.Content.AsBounded(maxForm);
                    await content.ReadFormAsync(formParams, encoding);
                }
                finally
                {
                    request.FormParams(formParams);
                    request.FormFileParams(_EmptyFormFileParams);
                }
            }
            else//formData
            {
                if (maxFormData <= 0)
                {
                    request.FormParams(_EmptyFormParams);
                    request.FormFileParams(_EmptyFormFileParams);
                    return;
                }
                var formParams = new FormParams();
                var formFileParams = new FormFileParams();
                try
                {
                    var content = request.Content.AsBounded(maxFormData);
                    var disposable = await content.ReadFormDataAsync(formParams, encoding, maxForm, formFileParams, boundary);
                    request.RegisterForDispose(disposable);
                }
                finally
                {
                    request.FormParams(formParams);
                    request.FormFileParams(formFileParams);
                }
            }
        }

        public static bool TryGetExtension(this IFormFile @this, out string extension)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            extension = string.Empty;
            var fileName = @this.FileName;
            if (string.IsNullOrEmpty(fileName))
                return false;

            var index = fileName.Length;
            while (--index >= 0)
            {
                var c = fileName[index];
                if (c == '.')
                {
                    extension = fileName.Substring(index);
                    return true;
                }
                if (c == Path.DirectorySeparatorChar
                    || c == Path.AltDirectorySeparatorChar
                    || c == Path.VolumeSeparatorChar)//?Path.PathSeparator
                    return false;
            }
            return false;
        }
        public static bool TryGetExtension(this IFormFile @this, out string extension, string ext1)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (ext1 == null)
                throw new ArgumentNullException(nameof(ext1));

            extension = string.Empty;
            var fileName = @this.FileName;
            if (string.IsNullOrEmpty(fileName))
                return false;

            var index = fileName.Length;
            while (--index >= 0)
            {
                var c = fileName[index];
                if (c == '.')
                {
                    var extSpan = fileName.AsSpan(index);
                    if (ext1.EqualsIgnoreCase(extSpan))
                    {
                        extension = ext1;
                        return true;
                    }
                    return false;
                }
                if (c == Path.DirectorySeparatorChar
                    || c == Path.AltDirectorySeparatorChar
                    || c == Path.VolumeSeparatorChar)//?Path.PathSeparator
                    return false;
            }
            return false;
        }
        public static bool TryGetExtension(this IFormFile @this, out string extension, string ext1, string ext2)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (ext1 == null)
                throw new ArgumentNullException(nameof(ext1));
            if (ext2 == null)
                throw new ArgumentNullException(nameof(ext2));

            extension = string.Empty;
            var fileName = @this.FileName;
            if (string.IsNullOrEmpty(fileName))
                return false;

            var index = fileName.Length;
            while (--index >= 0)
            {
                var c = fileName[index];
                if (c == '.')
                {
                    var extSpan = fileName.AsSpan(index);
                    if (ext1.EqualsIgnoreCase(extSpan))
                    {
                        extension = ext1;
                        return true;
                    }
                    else if (ext2.EqualsIgnoreCase(extSpan))
                    {
                        extension = ext2;
                        return true;
                    }
                    return false;
                }
                if (c == Path.DirectorySeparatorChar
                    || c == Path.AltDirectorySeparatorChar
                    || c == Path.VolumeSeparatorChar)//?Path.PathSeparator
                    return false;
            }
            return false;
        }
        public static bool TryGetExtension(this IFormFile @this, out string extension, string ext1, string ext2, string ext3)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (ext1 == null)
                throw new ArgumentNullException(nameof(ext1));
            if (ext2 == null)
                throw new ArgumentNullException(nameof(ext2));
            if (ext3 == null)
                throw new ArgumentNullException(nameof(ext3));

            extension = string.Empty;
            var fileName = @this.FileName;
            if (string.IsNullOrEmpty(fileName))
                return false;

            var index = fileName.Length;
            while (--index >= 0)
            {
                var c = fileName[index];
                if (c == '.')
                {
                    var extSpan = fileName.AsSpan(index);
                    if (ext1.EqualsIgnoreCase(extSpan))
                    {
                        extension = ext1;
                        return true;
                    }
                    else if (ext2.EqualsIgnoreCase(extSpan))
                    {
                        extension = ext2;
                        return true;
                    }
                    else if (ext3.EqualsIgnoreCase(extSpan))
                    {
                        extension = ext3;
                        return true;
                    }
                    return false;
                }
                if (c == Path.DirectorySeparatorChar
                    || c == Path.AltDirectorySeparatorChar
                    || c == Path.VolumeSeparatorChar)//?Path.PathSeparator
                    return false;
            }
            return false;
        }
        public static bool TryGetExtension(this IFormFile @this, out string extension, params string[] extensions)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (extensions == null)
                throw new ArgumentNullException(nameof(extensions));

            extension = string.Empty;
            var fileName = @this.FileName;
            if (string.IsNullOrEmpty(fileName))
                return false;

            var index = fileName.Length;
            while (--index >= 0)
            {
                var c = fileName[index];
                if (c == '.')
                {
                    var extSpan = fileName.AsSpan(index);
                    foreach (var ext in extensions)
                    {
                        if (ext.EqualsIgnoreCase(extSpan))
                        {
                            extension = ext;
                            return true;
                        }
                    }
                    return false;
                }
                if (c == Path.DirectorySeparatorChar
                    || c == Path.AltDirectorySeparatorChar
                    || c == Path.VolumeSeparatorChar)//?Path.PathSeparator
                    return false;
            }
            return false;
        }

        //TODO?? Property<HttpRequest> _RequestEncoding, request.Encoding()
        public static Encoding GetEncoding(HttpRequest request)
        {
            if (request.Headers.TryGetValue(HttpHeaders.ContentType, out var contentType)
                && HttpHeaders.TryParse(contentType, out _, "charset", out var charset))
            {
                if (charset.IsEmpty)
                    return null;
                if (charset.EqualsIgnoreCase("utf-8"))
                    return Encoding.UTF8;
                try
                {
                    return Encoding.GetEncoding(new string(charset));
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex, "UnobservedException");
                    return null;
                }
            }

            return null;
        }
        public static Encoding GetEncoding(HttpResponse response)
        {
            if (response.Headers.TryGetValue(HttpHeaders.ContentType, out var contentType)
                && HttpHeaders.TryParse(contentType, out _, "charset",  out var charset))
            {
                if (charset.IsEmpty)
                    return null;
                if (charset.EqualsIgnoreCase("utf-8"))
                    return Encoding.UTF8;
                try
                {
                    return Encoding.GetEncoding(new string(charset));
                }
                catch(Exception ex)
                {
                    Trace.WriteLine(ex, "UnobservedException");
                    return null;
                }
            }

            return null;
        }

        #region ReasonPhrases 
        private static readonly string _Status100 = "Continue";
        private static readonly string _Status101 = "Switching Protocols";
        private static readonly string _Status102 = "Processing";
        private static readonly string _Status200 = "OK";
        private static readonly string _Status201 = "Created";
        private static readonly string _Status202 = "Accepted";
        private static readonly string _Status203 = "Non-Authoritative Information";
        private static readonly string _Status204 = "No Content";
        private static readonly string _Status205 = "Reset Content";
        private static readonly string _Status206 = "Partial Content";
        private static readonly string _Status207 = "Multi-Status";
        private static readonly string _Status226 = "IM Used";
        private static readonly string _Status300 = "Multiple Choices";
        private static readonly string _Status301 = "Moved Permanently";
        private static readonly string _Status302 = "Moved Temporarily";
        private static readonly string _Status303 = "See Other";
        private static readonly string _Status304 = "Not Modified";
        private static readonly string _Status305 = "Use Proxy";
        private static readonly string _Status307 = "Temporary Redirect";
        private static readonly string _Status308 = "Permanent Redirect";
        private static readonly string _Status400 = "Bad Request";
        private static readonly string _Status401 = "Unauthorized";
        private static readonly string _Status402 = "Payment Required";
        private static readonly string _Status403 = "Forbidden";
        private static readonly string _Status404 = "Not Found";
        private static readonly string _Status405 = "Method Not Allowed";
        private static readonly string _Status406 = "Not Acceptable";
        private static readonly string _Status407 = "Proxy Authentication Required";
        private static readonly string _Status408 = "Request Timeout";
        private static readonly string _Status409 = "Conflict";
        private static readonly string _Status410 = "Gone";
        private static readonly string _Status411 = "Length Required";
        private static readonly string _Status412 = "Precondition Failed";
        private static readonly string _Status413 = "Request Entity Too Large";
        private static readonly string _Status414 = "URI Too Long";
        private static readonly string _Status415 = "Unsupported Media Type";
        private static readonly string _Status416 = "Range Not Satisfiable";
        private static readonly string _Status417 = "Expectation Failed";
        private static readonly string _Status426 = "Upgrade Required";
        private static readonly string _Status500 = "Internal Server Error";
        private static readonly string _Status501 = "Not Implemented";
        private static readonly string _Status502 = "Bad Gateway";
        private static readonly string _Status503 = "Service Unavailable";
        private static readonly string _Status504 = "Gateway Timeout";
        private static readonly string _Status505 = "HTTP Version Not Supported";
        private static readonly string _Status510 = "Not Extended";
        public static string GetReasonPhrase(int statusCode)
        {
            switch (statusCode)
            {
                case 100:
                    return _Status100;
                case 101:
                    return _Status101;
                case 102:
                    return _Status102;
                case 200:
                    return _Status200;
                case 201:
                    return _Status201;
                case 202:
                    return _Status202;
                case 203:
                    return _Status203;
                case 204:
                    return _Status204;
                case 205:
                    return _Status205;
                case 206:
                    return _Status206;
                case 207:
                    return _Status207;
                case 226:
                    return _Status226;
                case 300:
                    return _Status300;
                case 301:
                    return _Status301;
                case 302:
                    return _Status302;
                case 303:
                    return _Status303;
                case 304:
                    return _Status304;
                case 305:
                    return _Status305;
                case 307:
                    return _Status307;
                case 308:
                    return _Status308;
                case 400:
                    return _Status400;
                case 401:
                    return _Status401;
                case 402:
                    return _Status402;
                case 403:
                    return _Status403;
                case 404:
                    return _Status404;
                case 405:
                    return _Status405;
                case 406:
                    return _Status406;
                case 407:
                    return _Status407;
                case 408:
                    return _Status408;
                case 409:
                    return _Status409;
                case 410:
                    return _Status410;
                case 411:
                    return _Status411;
                case 412:
                    return _Status412;
                case 413:
                    return _Status413;
                case 414:
                    return _Status414;
                case 415:
                    return _Status415;
                case 416:
                    return _Status416;
                case 417:
                    return _Status417;
                case 426:
                    return _Status426;
                case 500:
                    return _Status500;
                case 501:
                    return _Status501;
                case 502:
                    return _Status502;
                case 503:
                    return _Status503;
                case 504:
                    return _Status504;
                case 505:
                    return _Status505;
                case 510:
                    return _Status510;
                default:
                    return null;
            }
        }
        #endregion

        #region Exception
        private static object _ExceptionStatusCode = new object();
        private static Func<HttpRequest, HttpResponse, Exception, Task> _ExceptionHandler;
        public static void UseException(Func<HttpRequest, HttpResponse, Exception, Task> handler)
        {
            _ExceptionHandler = handler;
        }
        public static Task UseException(HttpRequest request, HttpResponse response, Exception ex)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (response == null)
                throw new ArgumentNullException(nameof(response));
            if (ex == null)
                throw new ArgumentNullException(nameof(ex));

            var handler = _ExceptionHandler;
            if (handler != null)
            {
                return handler(request, response, ex);
            }
            else
            {
                response.StatusCode = ex.StatusCode() ?? 500;
                var sb = StringContent.Rent(out var disposable);
                response.RegisterForDispose(disposable);
                //TODO? NewLine use <br/> compatible
                sb.Write("Method: ");
                sb.Write(request.Method == null ? "null" : request.Method.ToString());
                sb.Write("<br/>Url: ");
                sb.Write(request.Url == null ? "null" : request.Url.ToString());
                sb.Write("<br/>Version: ");
                sb.Write(request.Version == null ? "null" : request.Version.ToString());
                sb.Write("<br/>Content: ");
                sb.Write(request.Content == null ? "null" : request.Content.Length.ToString());
                sb.Write("<br/><br/>Headers: ");
                for (int i = 0; i < request.Headers.Count; i++)
                {
                    var header = request.Headers[i];
                    sb.Write("<br/>");
                    sb.Write(header.Key);
                    sb.Write(": ");
                    sb.Write(header.Value);
                }
                sb.Write("<br/><br/>Status: ");
                sb.Write(response.StatusCode);
                sb.Write(" ");
                sb.Write(response.ReasonPhrase ?? GetReasonPhrase(response.StatusCode));
                if (ex is AggregateException aggregateEx)
                {
                    var innerExceptions = aggregateEx.InnerExceptions;
                    foreach (var innerException in innerExceptions)
                    {
                        sb.Write("<br/>Type: ");
                        sb.Write(innerException.GetType().ToString());
                        sb.Write("<br/>Message: ");
                        sb.Write(innerException.Message);
                        sb.Write("<br/>StackTrace: ");
                        sb.Write(innerException.StackTrace);
                    }
                }
                else 
                {
                    sb.Write("<br/>Type: ");
                    sb.Write(ex.GetType().ToString());
                    sb.Write("<br/>Message: ");
                    sb.Write(ex.Message);
                    sb.Write("<br/>StackTrace: ");
                    sb.Write(ex.StackTrace);
                }

                response.Content = StringContent.Create(sb.Sequence);
                response.Headers.Add(HttpHeaders.ContentType, "text/html; charset=utf-8");
                return Task.CompletedTask;
            }
        }
        public static TException StatusCode<TException>(this TException @this, int statusCode) where TException : Exception
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            @this.Data[_ExceptionStatusCode] = statusCode;
            return @this;
        }
        public static int? StatusCode<TException>(this TException @this) where TException : Exception 
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            //TODO? typeof(Exception).GetField("_data")
            var statusCode = @this.Data[_ExceptionStatusCode];
            if (statusCode == null)
                return null;
            return (int)statusCode;
        }
        #endregion

        //TODO? Add(HttpHeaders.ContentType)=>[HttpHeaders.ContentType]=
        public static HttpResponse UseRedirect(this HttpResponse @this, string location)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (location == null)
                throw new ArgumentNullException(nameof(location));

            @this.StatusCode = 302;//Move Temporarily
            @this.Headers[HttpHeaders.Location] = location;
            return @this;
        }
        public static HttpResponse UseCookie(this HttpResponse @this, string name, string value)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            var sb = StringExtensions.ThreadRent(out var disposable);
            try
            {
                Url.Encode(name, Encoding.UTF8, sb);
                sb.Write('=');
                Url.Encode(value, Encoding.UTF8, sb);
                sb.Write("; Path=/");
                @this.Headers.Add(HttpHeaders.SetCookie, sb.ToString());
            }
            finally
            {
                disposable.Dispose();
            }
            return @this;
        }
        public static HttpResponse UseCookie(this HttpResponse @this, string name, string value, string domain = null, int? maxAge = null, string path = "/", bool httpOnly = false, bool secure = false, string sameSite = null)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            var sb = StringExtensions.ThreadRent(out var disposable);
            try
            {
                Url.Encode(name, Encoding.UTF8, sb);
                sb.Write('=');
                Url.Encode(value, Encoding.UTF8, sb);
                sb.Write("; Path=");
                sb.Write(path);
                if (!string.IsNullOrEmpty(domain))
                {
                    sb.Write("; Domain=");
                    sb.Write(domain);
                }
                if (maxAge != null) 
                {
                    sb.Write("; Max-Age=");
                    sb.Write(maxAge.Value);
                }
                if (secure)
                    sb.Write("; Secure");
                if (httpOnly)
                    sb.Write("; HttpOnly");

                //SameSite Strict Lax None
                if (sameSite != null)
                {
                    sb.Write("; SameSite=");
                    sb.Write(sameSite);
                }

                @this.Headers.Add(HttpHeaders.SetCookie, sb.ToString());
            }
            finally
            {
                disposable.Dispose();
            }
            return @this;
        }
        public static HttpResponse UseJson<T>(this HttpResponse @this, T value)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            var buffer = StringContent.Rent(out var disposable);
            @this.RegisterForDispose(disposable);
            JsonWriter.ToJson(value, buffer);
            @this.Content = StringContent.Create(buffer.Sequence, Encoding.UTF8);
            @this.Headers.Add(HttpHeaders.ContentType, "application/json; charset=utf-8");
            return @this;
        }
        public static HttpResponse UseJson<T>(this HttpResponse @this, T value, Encoding encoding)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            if (encoding == Encoding.UTF8)
                return UseJson(@this, value);

            var buffer = StringContent.Rent(out var disposable);
            @this.RegisterForDispose(disposable);
            JsonWriter.ToJson(value, buffer);
            @this.Content = StringContent.Create(buffer.Sequence, encoding);
            @this.Headers.Add(HttpHeaders.ContentType, "application/json; charset=" + encoding.WebName);
            return @this;
        }
        public static HttpResponse UseFile(this HttpResponse @this, string fileName)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            var fileContent = new FileContent(fileName);
            @this.RegisterForDispose(fileContent);
            @this.Content = fileContent;

            if (MimeTypes.Default.TryGetValue(fileContent.File.FullName, out var mimeType))
                @this.Headers.Add(HttpHeaders.ContentType, mimeType);
            else
                @this.Headers.Add(HttpHeaders.ContentType, "application/octet-stream");

            return @this;
        }
        public static HttpResponse UseFile(this HttpResponse @this, string fileName, string contentType)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            var fileContent = new FileContent(fileName);
            @this.RegisterForDispose(fileContent);
            @this.Content = fileContent;

            if (!string.IsNullOrEmpty(contentType))
                @this.Headers.Add(HttpHeaders.ContentType, contentType);
            else if (MimeTypes.Default.TryGetValue(fileContent.File.FullName, out var mimeType))
                @this.Headers.Add(HttpHeaders.ContentType, mimeType);
            else
                @this.Headers.Add(HttpHeaders.ContentType, "application/octet-stream");

            return @this;
        }
        public static HttpResponse UseFile(this HttpResponse @this, HttpRequest request, string fileName)
        {
            return UseFile(@this, request, new FileInfo(fileName), null);
        }
        public static HttpResponse UseFile(this HttpResponse @this, HttpRequest request, string fileName, string contentType)
        {
            return UseFile(@this, request, new FileInfo(fileName), contentType);
        }
        public static HttpResponse UseFile(this HttpResponse @this, HttpRequest request, FileInfo file, string contentType)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (file == null)
                throw new ArgumentNullException(nameof(file));
            if (!file.Exists)
                throw new FileNotFoundException(file.FullName);

            var etagHash = file.LastWriteTimeUtc.ToFileTime() ^ file.Length;
            var headers = request.Headers;
            if (!headers.TryGetValue(HttpHeaders.IfRange, out var ifRange) || etagHash == GetEtagHash(ifRange))
            {
                if (headers.TryGetValue(HttpHeaders.Range, out var range))
                {
                    var segmContent = SegmentContent.Create(file, range);
                    if (segmContent != null)
                    {
                        @this.StatusCode = 206;
                        var sb = StringExtensions.ThreadRent(out var disposable);
                        try
                        {
                            sb.Write("bytes ");
                            sb.Write(segmContent.Offset);
                            sb.Write('-');
                            sb.Write(segmContent.Offset + segmContent.Length - 1);
                            sb.Write('/');
                            sb.Write(file.Length);
                            @this.Headers.Add(HttpHeaders.ContentRange, sb.ToString());
                            @this.Content = segmContent;
                            @this.RegisterForDispose(segmContent);
                            return @this;
                        }
                        finally
                        {
                            disposable.Dispose();
                        }
                    }
                }
            }
            if (headers.TryGetValue(HttpHeaders.IfNoneMatch, out var ifNoneMatch))
            {
                if (etagHash == GetEtagHash(ifNoneMatch))
                {
                    @this.StatusCode = 304;
                    @this.Content = null;
                    return @this;
                }
            }
            @this.StatusCode = 200;
            var fileContent = new FileContent(file);
            @this.RegisterForDispose(fileContent);
            @this.Content = fileContent;
            @this.Headers.Add(HttpHeaders.AcceptRanges, "bytes");
            @this.Headers.Add(HttpHeaders.ETag, GetETag(etagHash));

            if (!string.IsNullOrEmpty(contentType))
                @this.Headers.Add(HttpHeaders.ContentType, contentType);
            else if (MimeTypes.Default.TryGetValue(file.FullName, out var mimeType))
                @this.Headers.Add(HttpHeaders.ContentType, mimeType);
            else
                @this.Headers.Add(HttpHeaders.ContentType, "application/octet-stream");

            return @this;
        }
        #region UseFile
        private class SegmentContent : IHttpContent, IDisposable
        {
            private FileInfo _file;
            private FileStream _fs;
            private long _offset;
            private long _length;
            private long _position;
            public SegmentContent(FileInfo file, long offset, long length)
            {
                _file = file;
                _offset = offset;
                _length = length;
            }
            public long Available => _length - _position;
            public long Offset => _offset;
            public long Length => _length;
            public bool Rewind()
            {
                _position = 0;
                _fs.Position = _offset;
                return true;
            }
            public long ComputeLength() => Length;
            public int Read(Span<byte> buffer)
            {
                if (_file == null)
                    return 0;
                if (_position == _length)
                    return 0;
                var count = buffer.Length;
                if (count == 0)
                    throw new ArgumentException(nameof(buffer));
                if (_fs == null)
                {
                    _fs = new FileStream(_file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.None);
                    if (_fs.Length != _file.Length)
                    {
                        _fs.Dispose();
                        _fs = null;
                        throw new InvalidDataException(nameof(SegmentContent));
                    }
                    _fs.Position = _offset;
                }
                var toRead = _length - _position;
                if (toRead > count)
                    toRead = count;
                var result = _fs.Read(buffer.Slice(0, (int)toRead));
                _position += result;
                return result;
            }
            public int Read(byte[] buffer, int offset, int count)
            {
                return Read(buffer.AsSpan(offset, count));
            }
            public async ValueTask<int> ReadAsync(Memory<byte> buffer)
            {
                if (_file == null)
                    return 0;
                if (_position == _length)
                    return 0;
                var count = buffer.Length;
                if (count == 0)
                    throw new ArgumentException(nameof(buffer));
                if (_fs == null)
                {
                    _fs = new FileStream(_file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.Asynchronous);
                    if (_fs.Length != _file.Length)
                    {
                        _fs.Dispose();
                        _fs = null;
                        throw new InvalidDataException(nameof(SegmentContent));
                    }
                    _fs.Position = _offset;
                }
                var toRead = _length - _position;
                if (toRead > count)
                    toRead = count;
                var result = await _fs.ReadAsync(buffer.Slice(0, (int)toRead));
                _position += result;
                return result;
            }
            public ValueTask<int> ReadAsync(byte[] buffer, int offset, int count)
            {
                return ReadAsync(buffer.AsMemory(offset, count));
            }
            public void Dispose()
            {
                if (_file == null)
                    return;

                _file = null;
                if (_fs != null)
                {
                    _fs.Dispose();
                    _fs = null;
                }
            }
            public static SegmentContent Create(FileInfo file, string range)
            {
                if (!range.StartsWith("bytes="))//Unit bytes
                    return null;

                var sep = range.IndexOf('-', 6);//not support * ,multi
                if (sep == -1)
                    return null;

                var length = file.Length;
                if (sep == 6)// -to
                {
                    if (long.TryParse(range.AsSpan(7), out var to))
                    {
                        if (to > 0 && to <= length)
                            return new SegmentContent(file, length - to, to);
                    }
                }
                else if (sep + 1 == range.Length)//from-
                {
                    if (long.TryParse(range.AsSpan(6, sep - 6), out var from))
                    {
                        if (from >= 0 && from < length)
                            return new SegmentContent(file, from, length - from);
                    }
                }
                else//from-to
                {
                    if (long.TryParse(range.AsSpan(6, sep - 6), out var from) && long.TryParse(range.AsSpan(sep + 1), out var to))
                    {
                        if (from >= 0 && from < length && to >= from && to < length)
                            return new SegmentContent(file, from, to - from + 1);
                    }
                }
                return null;
            }
        }
        private static string _Hex = "0123456789abcdef";//JAVA long.toHexString
        private static string GetETag(long etagHash)
        {
            Debug.Assert(etagHash >= 0);//? &0x0f..
            unsafe
            {
                var chars = stackalloc char[66];
                chars[65] = '\"';
                int charPos = 65;
                do
                {
                    chars[--charPos] = _Hex[(int)(etagHash & 15)];
                    etagHash >>= 4;//? >>>=
                } while (etagHash != 0);
                chars[--charPos] = '\"';
                return new string(chars, charPos, 66 - charPos);
            }
        }
        private static long GetEtagHash(string etagValue)
        {
            if (etagValue.Length < 3)
                return -1;
            if (etagValue[0] == '\"')
            {
                if (etagValue[etagValue.Length - 1] != '\"')
                    return -1;
                var end = etagValue.Length - 1;
                if (end <= 1)
                    return -1;
                long hash = 0;
                for (int i = 1; i < end; i++)
                {
                    var temp = etagValue[i];
                    if (temp >= 'a' && temp <= 'f')
                        hash = 16 * hash + (10 + temp - 'a');
                    else if (temp >= '0' && temp <= '9')
                        hash = 16 * hash + (temp - '0');
                    else
                        return -1;
                }
                return hash;
            }
            if (etagValue.Length < 5)
                return -1;
            if (etagValue.StartsWith("W/\"", StringComparison.OrdinalIgnoreCase))
            {
                if (etagValue[etagValue.Length - 1] != '\"')
                    return -1;
                var end = etagValue.Length - 1;
                if (end <= 1)
                    return -1;
                long hash = 0;
                for (int i = 3; i < end; i++)
                {
                    var temp = etagValue[i];
                    if (temp >= 'a' && temp <= 'f')
                        hash = 16 * hash + (10 + temp - 'a');
                    else if (temp >= '0' && temp <= '9')
                        hash = 16 * hash + (temp - '0');
                    else
                        return -1;
                }
                return hash;
            }
            return -1;
        }
        #endregion

        public static HttpResponse UseCompression(this HttpResponse @this, HttpRequest request, int gzipLevel, int deflateLevel, int brLevel)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (@this.Content == null)
                return @this;
            //if (@this.Headers.Contains(HttpHeaders.ContentEncoding))//not repeat Compress
            //    return @this;

            if (request.Headers.TryGetValue(HttpHeaders.AcceptEncoding, out var acceptEncoding))
            {
                var acceptEncodingSpan = acceptEncoding.AsSpan();
                while (HttpHeaders.TryParse(ref acceptEncodingSpan, out var encoderName))
                {
                    if (gzipLevel > 0 && encoderName.EqualsIgnoreCase("gzip"))
                    {
                        var content = new DeflateEncoderContent(@this.Content, new DeflateEncoder(gzipLevel, 31));
                        @this.RegisterForDispose(content);
                        @this.Content = content;
                        @this.Headers.Add(HttpHeaders.ContentEncoding, new string(encoderName));
                        return @this;
                    }
                    else if (deflateLevel > 0 && encoderName.EqualsIgnoreCase("deflate"))
                    {
                        var content = new DeflateEncoderContent(@this.Content, new DeflateEncoder(deflateLevel, 15));
                        @this.RegisterForDispose(content);
                        @this.Content = content;
                        @this.Headers.Add(HttpHeaders.ContentEncoding, new string(encoderName));
                        return @this;
                    }
                    else if (brLevel > 0 && encoderName.EqualsIgnoreCase("br"))
                    {
                        var content = new BrotliEncoderContent(@this.Content, new BrotliEncoder(brLevel, 18));//512k
                        @this.RegisterForDispose(content);
                        @this.Content = content;
                        @this.Headers.Add(HttpHeaders.ContentEncoding, new string(encoderName));
                        return @this;
                    }
                }
            }
            return @this;
        }
        public static HttpResponse UseCompression(this HttpResponse @this, DeflateEncoder encoder, string encoderName)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (encoder == null)
                throw new ArgumentNullException(nameof(encoder));

            if (@this.Content == null)
                return @this;
            var content = new DeflateEncoderContent(@this.Content, encoder);
            @this.RegisterForDispose(content);
            @this.Content = content;
            if (!string.IsNullOrEmpty(encoderName))
                @this.Headers[HttpHeaders.ContentEncoding] = encoderName;
            return @this;
        }
        public static HttpResponse UseCompression(this HttpResponse @this, BrotliEncoder encoder, string encoderName)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            if (@this.Content == null)
                return @this;
            var content = new BrotliEncoderContent(@this.Content, encoder);
            @this.RegisterForDispose(content);
            @this.Content = content;
            if (!string.IsNullOrEmpty(encoderName))
                @this.Headers[HttpHeaders.ContentEncoding] = encoderName;

            return @this;
        }
        public static void Join(this IQueryParams @this, Url url)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (url == null)
                throw new ArgumentNullException(nameof(url));
            if (@this.Count == 0)
                return;

            var sb = StringExtensions.ThreadRent(out var disposable);
            try
            {
                var query = url.Query;
                if (string.IsNullOrEmpty(query) || query.Length == 1)
                    sb.Write('?');
                //else if (query[query.Length - 1] == '&')
                //    sb.Write(query);
                else
                {
                    sb.Write(query);
                    sb.Write('&');
                }
                var queryItem = @this[0];
                Url.Encode(queryItem.Key, Encoding.UTF8, sb);
                sb.Write('=');
                Url.Encode(queryItem.Value, Encoding.UTF8, sb);
                for (int i = 1; i < @this.Count; i++)
                {
                    queryItem = @this[i];
                    sb.Write('&');
                    Url.Encode(queryItem.Key, Encoding.UTF8, sb);
                    sb.Write('=');
                    Url.Encode(queryItem.Value, Encoding.UTF8, sb);
                }
                url.Query = sb.ToString();
            }
            finally
            {
                disposable.Dispose();
            }
        }
        public static void Join(this IQueryParams @this, Url url, Encoding encoding)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (url == null)
                throw new ArgumentNullException(nameof(url));
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));
            if (@this.Count == 0)
                return;

            var sb = StringExtensions.ThreadRent(out var disposable);
            try
            {
                var query = url.Query;
                if (string.IsNullOrEmpty(query) || query.Length == 1)
                    sb.Write('?');
                //else if (query[query.Length - 1] == '&')
                //    sb.Write(query);
                else
                {
                    sb.Write(query);
                    sb.Write('&');
                }
                var queryItem = @this[0];
                Url.Encode(queryItem.Key, encoding, sb);
                sb.Write('=');
                Url.Encode(queryItem.Value, encoding, sb);
                for (int i = 1; i < @this.Count; i++)
                {
                    queryItem = @this[i];
                    sb.Write('&');
                    Url.Encode(queryItem.Key, encoding, sb);
                    sb.Write('=');
                    Url.Encode(queryItem.Value, encoding, sb);
                }
                url.Query = sb.ToString();
            }
            finally
            {
                disposable.Dispose();
            }
        }

        //TODO??? AddRange

        //TODO?? ToString Sort OR Sort
        //public static string ToString(this IQueryParams @this, IComparer<string> comparer)
        //{
        //    if (@this == null)
        //        throw new ArgumentNullException(nameof(@this));
        //    if (comparer == null)
        //        throw new ArgumentNullException(nameof(comparer));
        //    if (@this.Count == 0)
        //        return string.Empty;

        //    var count = @this.Count;
        //    var keys = new string[count];
        //    var values = new string[count];
        //    for (int i = 0; i < count; i++)
        //    {
        //        var item = @this[i];
        //        keys[i] = item.Key;
        //        values[i] = item.Value;
        //    }
        //    Array.Sort(keys, values, comparer);
        //    var sb = StringExtensions.ThreadRent(out var disposable);
        //    try
        //    {
        //        sb.Write(keys[0]);
        //        sb.Write('=');
        //        sb.Write(values[0]);
        //        for (int i = 1; i < count; i++)
        //        {
        //            sb.Write('&');
        //            sb.Write(keys[i]);
        //            sb.Write('=');
        //            sb.Write(values[i]);
        //        }
        //        return sb.ToString();
        //    }
        //    finally
        //    {
        //        disposable.Dispose();
        //    }
        //}
        //public static string ToString(this IFormParams @this, IComparer<string> comparer)
        //{
        //    if (@this == null)
        //        throw new ArgumentNullException(nameof(@this));
        //    if (comparer == null)
        //        throw new ArgumentNullException(nameof(comparer));
        //    if (@this.Count == 0)
        //        return string.Empty;

        //    var count = @this.Count;
        //    var keys = new string[count];
        //    var values = new string[count];
        //    for (int i = 0; i < count; i++)
        //    {
        //        var item = @this[i];
        //        keys[i] = item.Key;
        //        values[i] = item.Value;
        //    }
        //    Array.Sort(keys, values, comparer);
        //    var sb = StringExtensions.ThreadRent(out var disposable);
        //    try
        //    {
        //        sb.Write(keys[0]);
        //        sb.Write('=');
        //        sb.Write(values[0]);
        //        for (int i = 1; i < count; i++)
        //        {
        //            sb.Write('&');
        //            sb.Write(keys[i]);
        //            sb.Write('=');
        //            sb.Write(values[i]);
        //        }
        //        return sb.ToString();
        //    }
        //    finally
        //    {
        //        disposable.Dispose();
        //    }
        //}

        public static string Unescape(string value)//兼容\u编码
        {
            if (value == null)
                return string.Empty;

            var dataString = Uri.UnescapeDataString(value);
            var sb = new StringBuilder();
            var pos = 0;
            for (; pos < dataString.Length;)
            {
                var temp = dataString[pos];
                if (temp == '%')
                {
                    if (pos + 5 < dataString.Length)
                    {
                        if (dataString[pos + 1] == 'u')
                        {
                            try
                            {
                                var u = Convert.ToInt32(dataString.Substring(pos + 2, 4), 16);

                                sb.Append((char)u);
                                pos += 6;
                                continue;
                            }
                            catch (Exception)
                            {
                            }
                        }
                    }
                }
                sb.Append(temp);
                pos += 1;
            }

            return sb.ToString();
        }
        public static string Unescape(ReadOnlySpan<char> charsToDecode, Encoding encoding)
        {
            try
            {
                return Url.Decode(charsToDecode, encoding);
            }
            catch
            {
                return Unescape(new string(charsToDecode));
            }
        }
        public static void Parse(this QueryParams @this, ReadOnlySpan<char> query)
        {
            Parse(@this, query, Encoding.UTF8);
        }
        public static void Parse(this QueryParams @this, ReadOnlySpan<char> query, Encoding encoding)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            var length = query.Length;
            if (length == 0)
                return;

            var tempOffset = 0;
            string queryName = null;
            for (int i = 0; i < length; i++)
            {
                var temp = query[i];
                if (temp == '=')
                {
                    if (queryName == null)
                    {
                        queryName = Unescape(query.Slice(tempOffset, i - tempOffset), encoding);
                        tempOffset = i + 1;
                    }
                }
                else if (temp == '&')
                {
                    if (queryName == null)
                    {
                        queryName = Unescape(query.Slice(tempOffset, i - tempOffset), encoding);
                        @this.Add(queryName, string.Empty);
                    }
                    else
                    {
                        string queryValue = Unescape(query.Slice(tempOffset, i - tempOffset), encoding);
                        @this.Add(queryName, queryValue);
                    }
                    tempOffset = i + 1;
                    queryName = null;
                }
            }

            if (queryName == null)
            {
                queryName = Url.Decode(query.Slice(tempOffset, length - tempOffset), encoding);
                @this.Add(queryName, string.Empty);
            }
            else
            {
                string queryValue = Url.Decode(query.Slice(tempOffset, length - tempOffset), encoding);
                @this.Add(queryName, queryValue);
            }
        }
        public static void Parse(this QueryParams @this, Url url)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (url == null)
                throw new ArgumentNullException(nameof(url));

            var query = url.Query;
            if (query == null || query.Length == 0 || query.Length == 1)
                return;
            Parse(@this, query.AsSpan(1), Encoding.UTF8);
        }
        public static void Parse(this CookieParams @this, ReadOnlySpan<char> cookie)
        {
            Parse(@this, cookie, Encoding.UTF8);
        }
        public static void Parse(this CookieParams @this, ReadOnlySpan<char> cookie, Encoding encoding)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            var length = cookie.Length;
            if (length == 0)
                return;

            var tempOffset = 0;
            string cookieName = null;
            for (int i = 0; i < length; i++)
            {
                var temp = cookie[i];
                if (temp == '=')
                {
                    if (cookieName == null)
                    {
                        cookieName = Unescape(cookie.Slice(tempOffset, i - tempOffset), encoding);
                        tempOffset = i + 1;
                    }
                }
                else if (temp == ';')
                {
                    if (cookieName == null)
                    {
                        cookieName = Unescape(cookie.Slice(tempOffset, i - tempOffset), encoding);
                        @this.Add(cookieName, string.Empty);
                    }
                    else
                    {
                        string cookieValue = Unescape(cookie.Slice(tempOffset, i - tempOffset), encoding);
                        @this.Add(cookieName, cookieValue);
                    }
                    cookieName = null;
                    tempOffset = i + 1;
                    if (tempOffset < length && cookie[tempOffset] == ' ')
                    {
                        tempOffset += 1;
                    }
                }
            }

            if (cookieName == null)
            {
                cookieName = Unescape(cookie.Slice(tempOffset), encoding);
                @this.Add(cookieName, string.Empty);
            }
            else
            {
                string cookieValue = Unescape(cookie.Slice(tempOffset), encoding);
                @this.Add(cookieName, cookieValue);
            }
        }
        public static IHttpContent AsBounded(this IHttpContent @this, long capacity)
        {
            if (@this == null)
                return @this;

            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            var length = @this.Length;
            if (length == -1)
                return new BoundedContent(@this, capacity);
            if (length > capacity)
                throw new InvalidDataException(nameof(capacity));

            return @this;
        }
        public static Stream AsStream(this IHttpContent @this)
        {
            if (@this == null)
                return Stream.Null;

            return new ContentStream(@this);
        }

        #region private
        private enum State { Cr, Lf, LfCr, Name, Colon, Value, Boundary, Dash, DashDash, DashCr };
        private class BufferedContentStream : Stream
        {
            private long _length;
            private long _position;
            private ReadOnlySequence<byte> _bytes;
            private IDisposable _disposable;
            private FileStream _tempFile;
            public BufferedContentStream(Buffer<byte> buffer, IDisposable disposable)
            {
                _bytes = buffer.Sequence;
                _length = _bytes.Length;
                _disposable = disposable;
            }
            public BufferedContentStream(Buffer<byte> buffer, IDisposable disposable, FileStream tempFile)
            {
                _bytes = buffer.Sequence;
                _tempFile = tempFile;
                _length = _bytes.Length + tempFile.Length;
                _disposable = disposable;
            }
            public override bool CanRead => true;
            public override bool CanSeek => true;
            public override bool CanWrite => false;
            public override long Length => _length;
            public override long Position
            {
                get => _position;
                set
                {

                    if (value < 0 || value > _length)
                        throw new ArgumentOutOfRangeException(nameof(Position));

                    _position = value;
                    if (_tempFile != null)
                    {
                        var filePos = _position - _bytes.Length;
                        _tempFile.Position = filePos >= 0 ? filePos : 0;
                    }
                }
            }
            public override long Seek(long offset, SeekOrigin origin)
            {
                switch (origin)
                {
                    case SeekOrigin.Begin:
                        Position = offset;
                        break;
                    case SeekOrigin.Current:
                        Position = _position + offset;
                        break;
                    case SeekOrigin.End:
                        Position = _length + offset;
                        break;
                }
                return _position;
            }
            public override int Read(Span<byte> buffer)
            {
                if (_position == _length)
                    return 0;

                var length = buffer.Length;
                if (length == 0)
                    return 0;

                if (_position < _bytes.Length)
                {
                    var seq = _bytes.Slice(_position);
                    var bytesSum = 0;
                    foreach (var segm in seq)
                    {
                        var toCopy = length - bytesSum;
                        if (toCopy > segm.Length)
                        {
                            toCopy = segm.Length;
                            segm.Span.CopyTo(buffer.Slice(bytesSum));
                        }
                        else
                        {
                            segm.Span.Slice(0, toCopy).CopyTo(buffer.Slice(bytesSum));
                            Debug.Assert(bytesSum + toCopy == length);
                        }
                        bytesSum += toCopy;
                        if (bytesSum == length)
                            break;
                    }
                    _position += bytesSum;
                    return bytesSum;
                }
                else
                {
                    var result = _tempFile.Read(buffer);
                    _position += result;
                    return result;
                }
            }
            public override int Read(byte[] buffer, int offset, int count)
            {
                return Read(buffer.AsSpan(offset, count));
            }
            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                if (_position == _length)
                    return 0;

                var length = buffer.Length;
                if (length == 0)
                    return 0;

                if (_position < _bytes.Length)
                {
                    var seq = _bytes.Slice(_position);
                    var bytesSum = 0;
                    foreach (var segm in seq)
                    {
                        var toCopy = length - bytesSum;
                        if (toCopy > segm.Length)
                        {
                            toCopy = segm.Length;
                            segm.Span.CopyTo(buffer.Span.Slice(bytesSum));
                        }
                        else
                        {
                            segm.Span.Slice(0, toCopy).CopyTo(buffer.Span.Slice(bytesSum));
                            Debug.Assert(bytesSum + toCopy == length);
                        }
                        bytesSum += toCopy;
                        if (bytesSum == length)
                            break;
                    }
                    _position += bytesSum;
                    return bytesSum;
                }
                else
                {
                    var result = await _tempFile.ReadAsync(buffer);
                    _position += result;
                    return result;
                }
            }
            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return ReadAsync(buffer.AsMemory(offset, count)).AsTask();
            }
            protected override void Dispose(bool disposing)
            {
                if (_disposable != null)
                {
                    _disposable.Dispose();
                    _bytes = ReadOnlySequence<byte>.Empty;
                    _disposable = null;
                }
                if (_tempFile != null)
                {
                    _tempFile.Dispose();
                    _tempFile = null;
                }
            }
            #region NotSupported
            public override void SetLength(long value)
            {
                throw new NotSupportedException(nameof(SetLength));
            }
            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException(nameof(Write));
            }
            public override void Flush()
            {

            }
            #endregion
        }
        private class BoundedContent : IHttpContent
        {
            private long _read;
            private long _capacity;
            private IHttpContent _content;
            public BoundedContent(IHttpContent content, long capacity)
            {
                _content = content;
                _capacity = capacity;
            }
            public long Available => _content.Available;
            public long Length => _content.Length;
            public long ComputeLength() => _content.ComputeLength();
            public bool Rewind()
            {
                if (_content.Rewind())
                {
                    _read = 0;
                    return true;
                }
                return false;
            }
            public int Read(Span<byte> buffer)
            {
                var result = _content.Read(buffer);
                _read += result;
                if (_read > _capacity)
                    throw new InvalidDataException("capacity");
                return result;
            }
            public int Read(byte[] buffer, int offset, int count)
            {
                var result = _content.Read(buffer, offset, count);
                _read += result;
                if (_read > _capacity)
                    throw new InvalidDataException("capacity");
                return result;
            }
            public async ValueTask<int> ReadAsync(Memory<byte> buffer)
            {
                var result = await _content.ReadAsync(buffer);
                _read += result;
                if (_read > _capacity)
                    throw new InvalidDataException("capacity");
                return result;
            }
            public async ValueTask<int> ReadAsync(byte[] buffer, int offset, int count)
            {
                var result = await _content.ReadAsync(buffer, offset, count);
                _read += result;
                if (_read > _capacity)
                    throw new InvalidDataException("capacity");
                return result;
            }
        }
        private class ContentStream : Stream
        {
            private IHttpContent _content;
            public ContentStream(IHttpContent content)
            {
                _content = content;
            }
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => _content.Length;
            public override long Position
            {
                get => _content.Length == -1 ? -1 : _content.Length - _content.Available;
                set
                {
                    if (value == 0 && _content.Rewind())
                        return;

                    throw new NotSupportedException(nameof(Position));
                }
            }
            public override int Read(Span<byte> buffer)
            {
                return _content.Read(buffer);
            }
            public override int Read(byte[] buffer, int offset, int count)
            {
                return _content.Read(buffer, offset, count);
            }
            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                return _content.ReadAsync(buffer);
            }
            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return _content.ReadAsync(buffer, offset, count).AsTask();
            }

            #region NotSupported
            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException(nameof(Seek));
            }
            public override void SetLength(long value)
            {
                throw new NotSupportedException(nameof(SetLength));
            }
            public override void Flush()
            {
               
            }
            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException(nameof(Write));
            }
            #endregion
        }
        private class DeflateEncoderContent : IHttpContent, IDisposable
        {
            private DeflateEncoder _encoder;
            private IHttpContent _content;
            private int _offset;
            private int _length;
            private byte[] _buffer;
            public DeflateEncoderContent(IHttpContent content, DeflateEncoder encoder)
            {
                _content = content;
                _encoder = encoder;
                _buffer = ArrayPool<byte>.Shared.Rent(8192);
            }
            public long Available => _content == null ? 0 : -1;
            public long Length => -1;
            public bool Rewind() => false;
            public long ComputeLength() => -1;
            public int Read(Span<byte> buffer)
            {
                if (buffer.IsEmpty)
                    return 0;
                if (_content == null)
                    return 0;
                if (_buffer == null)
                {
                    _encoder.Compress(Array.Empty<byte>(), buffer, true, out var bytesConsumed, out var bytesWritten, out var completed);
                    Debug.Assert(bytesConsumed == 0);
                    Debug.Assert(bytesWritten != 0);
                    if (completed)
                    {
                        _content = null;
                        _encoder.Dispose();
                        _encoder = null;
                    }
                    return bytesWritten;
                }
                else
                {
                    if (_length == 0)
                    {
                        _length = _content.Read(_buffer);
                        if (_length == 0)
                        {
                            ArrayPool<byte>.Shared.Return(_buffer);
                            _buffer = null;
                            _encoder.Compress(Array.Empty<byte>(), buffer, true, out var bytesConsumed, out var bytesWritten, out var completed);
                            Debug.Assert(bytesConsumed == 0);
                            Debug.Assert(bytesWritten != 0);
                            if (completed)
                            {
                                _content = null;
                                _encoder.Dispose();
                                _encoder = null;
                            }
                            return bytesWritten;
                        }
                        else
                        {
                            _offset = 0;
                            _encoder.Compress(_buffer.AsSpan(_offset, _length), buffer, false, out var bytesConsumed, out var bytesWritten, out var completed);
                            _offset += bytesConsumed;
                            _length -= bytesConsumed;
                            if (completed)
                            {
                                ArrayPool<byte>.Shared.Return(_buffer);
                                _buffer = null;
                                _content = null;
                                _encoder.Dispose();
                                _encoder = null;
                                return bytesWritten;
                            }
                            if (bytesWritten == 0)
                                return Read(buffer);
                            return bytesWritten;
                        }
                    }
                    else
                    {
                        _encoder.Compress(_buffer.AsSpan(_offset, _length), buffer, false, out var bytesConsumed, out var bytesWritten, out var completed);
                        _offset += bytesConsumed;
                        _length -= bytesConsumed;
                        if (completed)
                        {
                            ArrayPool<byte>.Shared.Return(_buffer);
                            _buffer = null;
                            _content = null;
                            _encoder.Dispose();
                            _encoder = null;
                        }
                        if (bytesWritten == 0)
                            return Read(buffer);
                        return bytesWritten;
                    }
                }
            }
            public int Read(byte[] buffer, int offset, int count)
            {
                return Read(buffer.AsSpan(offset, count));
            }
            public async ValueTask<int> ReadAsync(Memory<byte> buffer)
            {
                if (buffer.IsEmpty)
                    return 0;
                if (_content == null)
                    return 0;
                if (_buffer == null)
                {
                    _encoder.Compress(Array.Empty<byte>(), buffer.Span, true, out var bytesConsumed, out var bytesWritten, out var completed);
                    Debug.Assert(bytesConsumed == 0);
                    Debug.Assert(bytesWritten != 0);
                    if (completed)
                    {
                        _content = null;
                        _encoder.Dispose();
                        _encoder = null;
                    }
                    return bytesWritten;
                }
                else
                {
                    if (_length == 0)
                    {
                        _length = await _content.ReadAsync(_buffer);
                        if (_length == 0)
                        {
                            ArrayPool<byte>.Shared.Return(_buffer);
                            _buffer = null;
                            _encoder.Compress(Array.Empty<byte>(), buffer.Span, true, out var bytesConsumed, out var bytesWritten, out var completed);
                            Debug.Assert(bytesConsumed == 0);
                            Debug.Assert(bytesWritten != 0);
                            if (completed)
                            {
                                _content = null;
                                _encoder.Dispose();
                                _encoder = null;
                            }
                            return bytesWritten;
                        }
                        else
                        {
                            _offset = 0;
                            _encoder.Compress(_buffer.AsSpan(_offset, _length), buffer.Span, false, out var bytesConsumed, out var bytesWritten, out var completed);
                            _offset += bytesConsumed;
                            _length -= bytesConsumed;
                            if (completed)
                            {
                                ArrayPool<byte>.Shared.Return(_buffer);
                                _buffer = null;
                                _content = null;
                                _encoder.Dispose();
                                _encoder = null;
                                return bytesWritten;
                            }
                            if (bytesWritten == 0)
                                return await ReadAsync(buffer);
                            return bytesWritten;
                        }
                    }
                    else
                    {
                        _encoder.Compress(_buffer.AsSpan(_offset, _length), buffer.Span, false, out var bytesConsumed, out var bytesWritten, out var completed);
                        _offset += bytesConsumed;
                        _length -= bytesConsumed;
                        if (completed)
                        {
                            ArrayPool<byte>.Shared.Return(_buffer);
                            _buffer = null;
                            _content = null;
                            _encoder.Dispose();
                            _encoder = null;
                        }
                        if (bytesWritten == 0)
                            return await ReadAsync(buffer);
                        return bytesWritten;
                    }
                }
            }
            public ValueTask<int> ReadAsync(byte[] buffer, int offset, int count)
            {
                return ReadAsync(buffer.AsMemory(offset, count));
            }
            public void Dispose()//TODO Sync
            {
                var content = _content;
                _content = null;
                if (content != null) 
                {
                    Debug.Assert(_encoder != null);
                    _encoder.Dispose();
                    _encoder = null;
                }
                var buffer = _buffer;
                _buffer = null;
                if (buffer != null)
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }
        private class BrotliEncoderContent : IHttpContent, IDisposable
        {
            private BrotliEncoder _encoder;
            private IHttpContent _content;
            private int _offset;
            private int _length;
            private byte[] _buffer;
            public BrotliEncoderContent(IHttpContent content, BrotliEncoder encoder)
            {
                _content = content;
                _encoder = encoder;
                _buffer = ArrayPool<byte>.Shared.Rent(8192);
            }
            public long Available => _content == null ? 0 : -1;
            public long Length => -1;
            public bool Rewind() => false;
            public long ComputeLength() => -1;
            public int Read(Span<byte> buffer)
            {
                if (buffer.IsEmpty)
                    return 0;
                if (_content == null)
                    return 0;
                if (_buffer == null)
                {
                    var status = _encoder.Flush(buffer, out var bytesWritten);
                    if (status == OperationStatus.Done)
                    {
                        _content = null;
                        _encoder.Dispose();
                    }
                    return bytesWritten;
                }
                else
                {
                    if (_length == 0)
                    {
                        _length = _content.Read(_buffer);
                        if (_length == 0)
                        {
                            ArrayPool<byte>.Shared.Return(_buffer);
                            _buffer = null;
                            var status = _encoder.Flush(buffer, out var bytesWritten);
                            if (status == OperationStatus.Done)
                            {
                                _content = null;
                                _encoder.Dispose();
                            }
                            return bytesWritten;
                        }
                        else 
                        {
                            _offset = 0;
                            var status = _encoder.Compress(_buffer.AsSpan(_offset, _length), buffer, out var bytesConsumed, out var bytesWritten, false);
                            _offset += bytesConsumed;
                            _length -= bytesConsumed;
                            if (status != OperationStatus.Done)
                                throw new InvalidDataException($"OperationStatus:{status}");
                            if (bytesWritten == 0)
                                return Read(buffer);
                            return bytesWritten;
                        }
                    }
                    else
                    {
                        var status = _encoder.Compress(_buffer.AsSpan(_offset, _length), buffer, out var bytesConsumed, out var bytesWritten, false);
                        _offset += bytesConsumed;
                        _length -= bytesConsumed;
                        if (status != OperationStatus.Done)
                            throw new InvalidDataException($"OperationStatus:{status}");
                        if (bytesWritten == 0)
                            return Read(buffer);
                        return bytesWritten;
                    }
                }
            }
            public int Read(byte[] buffer, int offset, int count)
            {
                return Read(buffer.AsSpan(offset, count));
            }
            public async ValueTask<int> ReadAsync(Memory<byte> buffer)
            {
                if (buffer.IsEmpty)
                    return 0;
                if (_content == null)
                    return 0;
                if (_buffer == null)
                {
                    var status = _encoder.Flush(buffer.Span, out var bytesWritten);
                    if (status == OperationStatus.Done)
                    {
                        _content = null;
                        _encoder.Dispose();
                    }
                    return bytesWritten;
                }
                else
                {
                    if (_length == 0)
                    {
                        _length = await _content.ReadAsync(_buffer);
                        if (_length == 0)
                        {
                            ArrayPool<byte>.Shared.Return(_buffer);
                            _buffer = null;
                            var status = _encoder.Flush(buffer.Span, out var bytesWritten);
                            if (status == OperationStatus.Done)
                            {
                                _content = null;
                                _encoder.Dispose();
                            }
                            return bytesWritten;
                        }
                        else
                        {
                            _offset = 0;
                            var status = _encoder.Compress(_buffer.AsSpan(_offset, _length), buffer.Span, out var bytesConsumed, out var bytesWritten, false);
                            _offset += bytesConsumed;
                            _length -= bytesConsumed;
                            if (status != OperationStatus.Done)
                                throw new InvalidDataException($"OperationStatus:{status}");
                            if (bytesWritten == 0)
                                return await ReadAsync(buffer);
                            return bytesWritten;
                        }
                    }
                    else
                    {
                        var status = _encoder.Compress(_buffer.AsSpan(_offset, _length), buffer.Span, out var bytesConsumed, out var bytesWritten, false);
                        _offset += bytesConsumed;
                        _length -= bytesConsumed;
                        if (status != OperationStatus.Done)
                            throw new InvalidDataException($"OperationStatus:{status}");
                        if (bytesWritten == 0)
                            return await ReadAsync(buffer);
                        return bytesWritten;
                    }
                }
            }
            public ValueTask<int> ReadAsync(byte[] buffer, int offset, int count)
            {
                return ReadAsync(buffer.AsMemory(offset, count));
            }
            public void Dispose()//TODO Sync
            {
                var content = _content;
                _content = null;
                if (content != null)
                {
                    _encoder.Dispose();
                }
                var buffer = _buffer;
                _buffer = null;
                if (buffer != null)
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }
        private class EmptyEnumerator<TValue> : IEnumerator<KeyValuePair<string, TValue>>
        {
            public static EmptyEnumerator<TValue> Value { get; } = new EmptyEnumerator<TValue>();
            KeyValuePair<string, TValue> IEnumerator<KeyValuePair<string, TValue>>.Current => default;
            object IEnumerator.Current => default;
            bool IEnumerator.MoveNext() => false;
            void IEnumerator.Reset() { }
            void IDisposable.Dispose() { }
        }
        private class EmptyQueryParams : IQueryParams
        {
            public KeyValuePair<string, string> this[int index] => throw new ArgumentOutOfRangeException(nameof(index));
            public int Count => 0;
            public bool Contains(string name) => false;
            public bool TryGetValue(string name, out string value)
            {
                value = null;
                return false;
            }
            public string[] GetValues(string name)
            {
                return Array.Empty<string>();
            }
            public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
            {
                return EmptyEnumerator<string>.Value;
            }
            IEnumerator IEnumerable.GetEnumerator()
            {
                return EmptyEnumerator<string>.Value;
            }
        }
        private class EmptyCookieParams : ICookieParams
        {
            public KeyValuePair<string, string> this[int index] => throw new ArgumentOutOfRangeException(nameof(index));
            public int Count => 0;
            public bool Contains(string name) => false;
            public bool TryGetValue(string name, out string value)
            {
                value = null;
                return false;
            }
            public string[] GetValues(string name)
            {
                return Array.Empty<string>();
            }
            public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
            {
                return EmptyEnumerator<string>.Value;
            }
            IEnumerator IEnumerable.GetEnumerator()
            {
                return EmptyEnumerator<string>.Value;
            }
        }
        private class EmptyFormParams : IFormParams
        {
            public KeyValuePair<string, string> this[int index] => throw new ArgumentOutOfRangeException(nameof(index));
            public int Count => 0;
            public bool Contains(string name) => false;
            public bool TryGetValue(string name, out string value)
            {
                value = null;
                return false;
            }
            public string[] GetValues(string name)
            {
                return Array.Empty<string>();
            }
            public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
            {
                return EmptyEnumerator<string>.Value;
            }
            IEnumerator IEnumerable.GetEnumerator()
            {
                return EmptyEnumerator<string>.Value;
            }
        }
        private class EmptyPathParams : IPathParams
        {
            public KeyValuePair<string, string> this[int index] => throw new ArgumentOutOfRangeException(nameof(index));
            public int Count => 0;
            public bool Contains(string name) => false;
            public bool TryGetValue(string name, out string value)
            {
                value = null;
                return false;
            }
            public string[] GetValues(string name)
            {
                return Array.Empty<string>();
            }
            public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
            {
                return EmptyEnumerator<string>.Value;
            }
            IEnumerator IEnumerable.GetEnumerator()
            {
                return EmptyEnumerator<string>.Value;
            }
        }
        private class EmptyFormFileParams : IFormFileParams
        {
            public KeyValuePair<string, IFormFile> this[int index] => throw new ArgumentOutOfRangeException(nameof(index));
            public int Count => 0;
            public bool Contains(string name) => false;
            public bool TryGetValue(string name, out IFormFile file)
            {
                file = null;
                return false;
            }
            public IFormFile[] GetValues(string name)
            {
                return Array.Empty<IFormFile>();
            }
            public IEnumerator<KeyValuePair<string, IFormFile>> GetEnumerator()
            {
                return EmptyEnumerator<IFormFile>.Value;
            }
            IEnumerator IEnumerable.GetEnumerator()
            {
                return EmptyEnumerator<IFormFile>.Value;
            }
        }

        private static EmptyQueryParams _EmptyQueryParams = new EmptyQueryParams();
        private static EmptyCookieParams _EmptyCookieParams = new EmptyCookieParams();
        private static EmptyFormParams _EmptyFormParams = new EmptyFormParams();
        private static EmptyPathParams _EmptyPathParams = new EmptyPathParams();
        private static EmptyFormFileParams _EmptyFormFileParams = new EmptyFormFileParams();
        #endregion
    }
}
