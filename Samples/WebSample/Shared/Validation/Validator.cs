
namespace WebSample
{
    using System;
    using System.Reflection;
    using System.Linq.Expressions;
    using System.Collections.Generic;
    public static class Validator
    {
        private static readonly object _Sync = new object();
        private static Stack<Func<object, Type, ParameterExpression, Expression>> _Handlers;
        private static class Handler<T>
        {
            static Handler()
            {
                var value = Expression.Parameter(typeof(T), "value");
                Register(typeof(T), value, out var expression);
                if (expression != null)
                {
                    Value = Expression.Lambda<Func<T, string>>(expression, value).Compile();
                }
            }

            public static Func<T, string> Value;
        }
        static Validator() 
        {
            _Handlers = new Stack<Func<object, Type, ParameterExpression, Expression>>();

            //Nullable<>
            Register((attribute, type, value) => {
                if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(Nullable<>))
                    return null;

                var nullableType = type.GetGenericArguments()[0];
                var nullableValue = Expression.Variable(nullableType, "nullableValue");
                Register(attribute, nullableType, nullableValue, out var expression);
                if (expression == null)
                    return Expression.Empty();
                return Expression.Block(new[] { nullableValue },
                            Expression.Assign(nullableValue, Expression.Property(value, "Value")),
                            expression);
            });
        }
        public static void Register(Func<object, Type, ParameterExpression, Expression> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            lock (_Sync)
            {
                _Handlers.Push(handler);
            }
        }
        public static void Register<TAttribute, T>(Func<TAttribute, T, string> handler) where TAttribute : Attribute
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            Register((attribute, type, value) => {
                if (attribute.GetType() != typeof(TAttribute))
                    return null;
                if (type != typeof(T))
                    return null;

                return Expression.Invoke(Expression.Constant(handler), Expression.Constant((TAttribute)attribute), value);
            });
        }
        public static void Register(object attribute,Type type,ParameterExpression value,out Expression expression) 
        {
            if (attribute == null)
                throw new ArgumentNullException(nameof(attribute));
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            expression = null;
            lock (_Sync) 
            {
                foreach (var handler in _Handlers)
                {
                    expression = handler.Invoke(attribute, type, value);
                    if (expression != null)
                    {
                        if (expression.Type == typeof(void) && expression.NodeType == ExpressionType.Default)
                            expression = null;
                        return;
                    }
                }
            }
        }
        public static void Register(Type type, ParameterExpression value, out Expression expression) 
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            expression = null;
            lock (_Sync) 
            {
                var errorMessage = Expression.Variable(typeof(string), "errorMessage");
                var variables = new List<ParameterExpression>() { errorMessage };
                var exprs = new List<Expression>();
                var returnLabel = Expression.Label(typeof(string));
                var properties = type.GetGeneralProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (var property in properties)
                {
                    var propertyValue = Expression.Variable(property.PropertyType, $"value{property.Name}");
                    var propertyExprs = new List<Expression>();
                    var attributes = property.GetCustomAttributes();
                    foreach (var attribute in attributes)
                    {
                        if (attribute is ValidateAttribute validate) 
                        {
                            var method = type.GetMethod(validate.Method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                            propertyExprs.Add(Expression.Call(value, method));
                            continue;
                        }
                        Register(attribute, property.PropertyType, propertyValue, out var expr);
                        if (expr != null)
                            propertyExprs.Add(expr);
                    }
                    if (propertyExprs.Count == 0)
                        continue;
                    variables.Add(propertyValue);
                    exprs.Add(Expression.Assign(propertyValue, Expression.Property(value, property)));
                    foreach (var propertyExpr in propertyExprs)
                    {
                        exprs.Add(Expression.Assign(errorMessage,propertyExpr));
                        exprs.Add(Expression.IfThen(
                            Expression.NotEqual(errorMessage, Expression.Constant(null, typeof(string))),
                            Expression.Return(returnLabel, errorMessage)
                            ));
                    }
                }

                if (exprs.Count > 0) 
                {
                    exprs.Add(Expression.Label(returnLabel, Expression.Constant(null, typeof(string))));
                    expression = Expression.Block(variables, exprs);
                }
            }
        }
        public static string Validate<T>(T value)
        {
            var handler = Handler<T>.Value;
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            return handler(value);
        }
        public static bool Validate<T>(T value, out string errorMessage)
        {
            errorMessage = Validate(value);
            return errorMessage == null;
        }

        //Custom
    }
}
