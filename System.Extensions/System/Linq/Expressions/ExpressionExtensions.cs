
namespace System.Linq.Expressions
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Reflection;
    public static class ExpressionExtensions
    {
        static ExpressionExtensions()
        {
            _Convert = new Dictionary<(Type, Type), Func<object, object>>();
            _Not = new Dictionary<Type, Func<object, object>>();
            _Equal = new Dictionary<(Type, Type), Func<object, object, object>>();
            _NotEqual = new Dictionary<(Type, Type), Func<object, object, object>>();
            _GreaterThan = new Dictionary<(Type, Type), Func<object, object, object>>();
            _GreaterThanOrEqual = new Dictionary<(Type, Type), Func<object, object, object>>();
            _LessThan = new Dictionary<(Type, Type), Func<object, object, object>>();
            _LessThanOrEqual = new Dictionary<(Type, Type), Func<object, object, object>>();
            _AndAlso = new Dictionary<(Type, Type), Func<object, object, object>>();
            _OrElse = new Dictionary<(Type, Type), Func<object, object, object>>();
            _And = new Dictionary<(Type, Type), Func<object, object, object>>();
            _Or = new Dictionary<(Type, Type), Func<object, object, object>>();
            _Add = new Dictionary<(Type, Type), Func<object, object, object>>();
            _Subtract = new Dictionary<(Type, Type), Func<object, object, object>>();
            _Multiply = new Dictionary<(Type, Type), Func<object, object, object>>();
            _Divide = new Dictionary<(Type, Type), Func<object, object, object>>();
            _Modulo = new Dictionary<(Type, Type), Func<object, object, object>>();
            _Coalesce = new Dictionary<(Type, Type), Func<object, object, object>>();
            _Conditional = new Dictionary<(Type, Type, Type), Func<object, object, object, object>>();
        }

        #region private
        private static object _Sync = new object();
        //private static object _ConvertSync = new object();
        private static Dictionary<(Type, Type), Func<object, object>> _Convert;
        private static Dictionary<Type, Func<object, object>> _Not;
        private static Dictionary<(Type, Type), Func<object, object, object>> _Equal;
        private static Dictionary<(Type, Type), Func<object, object, object>> _NotEqual;
        private static Dictionary<(Type, Type), Func<object, object, object>> _GreaterThan;
        private static Dictionary<(Type, Type), Func<object, object, object>> _GreaterThanOrEqual;
        private static Dictionary<(Type, Type), Func<object, object, object>> _LessThan;
        private static Dictionary<(Type, Type), Func<object, object, object>> _LessThanOrEqual;
        private static Dictionary<(Type, Type), Func<object, object, object>> _AndAlso;
        private static Dictionary<(Type, Type), Func<object, object, object>> _OrElse;
        private static Dictionary<(Type, Type), Func<object, object, object>> _And;
        private static Dictionary<(Type, Type), Func<object, object, object>> _Or;
        private static Dictionary<(Type, Type), Func<object, object, object>> _Add;
        private static Dictionary<(Type, Type), Func<object, object, object>> _Subtract;
        private static Dictionary<(Type, Type), Func<object, object, object>> _Multiply;
        private static Dictionary<(Type, Type), Func<object, object, object>> _Divide;
        private static Dictionary<(Type, Type), Func<object, object, object>> _Modulo;
        private static Dictionary<(Type, Type), Func<object, object, object>> _Coalesce;
        private static Dictionary<(Type, Type, Type), Func<object, object, object, object>> _Conditional;
        #endregion

        public static object Invoke(this LambdaExpression @this, params object[] parameters)
        {
            if (TryInvoke(@this, out var result, parameters))
                return result;
            throw new NotSupportedException(@this.Body.ToString());
        }
        public static object Invoke(this Expression @this)
        {
            if (TryInvoke(@this, null, out var result))
                return result;
            throw new NotSupportedException(@this.ToString());
        }
        public static bool TryInvoke(this LambdaExpression @this, out object result, params object[] parameters)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            var exprParameters = @this.Parameters;
            if (exprParameters.Count == 0)
                return TryInvoke(@this.Body, null, out result);
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));
            if (parameters.Length != exprParameters.Count)
                throw new ArgumentException(nameof(parameters));

            var invokeParameters = new Dictionary<ParameterExpression, object>();
            for (int i = 0; i < exprParameters.Count; i++)
            {
                invokeParameters.Add(exprParameters[i], parameters[i]);
            }
            return TryInvoke(@this.Body, invokeParameters, out result);
        }
        public static bool TryInvoke(this Expression @this, out object result)
        {
            return TryInvoke(@this, null, out result);
        }
        public static bool TryInvoke(this Expression @this, IDictionary<ParameterExpression, object> parameters, out object result)
        {
            if (@this == null)
            {
                result = null;
                return true;
            }

            switch (@this.NodeType)
            {
                case ExpressionType.Convert:
                    {
                        var operand = ((UnaryExpression)@this).Operand;
                        if (!_Convert.TryGetValue((@this.Type, operand.Type), out var convert))
                        {
                            lock (_Sync)
                            {
                                if (!_Convert.TryGetValue((@this.Type, operand.Type), out convert))
                                {
                                    var paramObj = Expression.Parameter(typeof(object), "paramObj");
                                    var expr = Expression.Convert(Expression.Convert(Expression.Convert(paramObj, operand.Type), @this.Type), typeof(object));
                                    convert = Expression.Lambda<Func<object, object>>(expr, paramObj).Compile();
                                    var Convert = new Dictionary<(Type, Type), Func<object, object>>(_Convert);
                                    Convert.Add((@this.Type, operand.Type), convert);
                                    _Convert = Convert;
                                }
                            }
                        }
                        if (TryInvoke(operand, parameters, out var value))
                        {
                            result = convert(value);
                            return true;
                        }
                        else
                        {
                            result = null;
                            return false;
                        }
                    }
                case ExpressionType.Not:
                    {
                        var operand = ((UnaryExpression)@this).Operand;
                        if (!_Not.TryGetValue(operand.Type, out var not))
                        {
                            lock (_Sync)
                            {
                                if (!_Not.TryGetValue(operand.Type, out not))
                                {
                                    var paramObj = Expression.Parameter(typeof(object), "paramObj");
                                    var expr = Expression.Convert(Expression.Not(Expression.Convert(paramObj, operand.Type)), typeof(object));
                                    not = Expression.Lambda<Func<object, object>>(expr, paramObj).Compile();
                                    var Not = new Dictionary<Type, Func<object, object>>(_Not);
                                    Not.Add(operand.Type, not);
                                    _Not = Not;
                                }
                            }
                        }
                        if (TryInvoke(operand, parameters, out var value))
                        {
                            result = not(value);
                            return true;
                        }
                        else
                        {
                            result = null;
                            return false;
                        }
                    }
                case ExpressionType.Equal:
                    {
                        var left = ((BinaryExpression)@this).Left;
                        var right = ((BinaryExpression)@this).Right;
                        if (!_Equal.TryGetValue((left.Type, right.Type), out var equal))
                        {
                            lock (_Sync)
                            {
                                if (!_Equal.TryGetValue((left.Type, right.Type), out equal))
                                {
                                    var leftObj = Expression.Parameter(typeof(object), "leftObj");
                                    var rightObj = Expression.Parameter(typeof(object), "rightObj");
                                    var expr = Expression.Convert(
                                        Expression.Equal(Expression.Convert(leftObj, left.Type), Expression.Convert(rightObj, right.Type)),
                                        typeof(object));
                                    equal = Expression.Lambda<Func<object, object, object>>(expr, leftObj, rightObj).Compile();
                                    var Equal = new Dictionary<(Type, Type), Func<object, object, object>>(_Equal);
                                    Equal.Add((left.Type, right.Type), equal);
                                    _Equal = Equal;
                                }
                            }
                        }
                        if (TryInvoke(left, parameters, out var valueLeft) && TryInvoke(right, parameters, out var valueRight))
                        {
                            result = equal(valueLeft, valueRight);
                            return true;
                        }
                        else
                        {
                            result = null;
                            return false;
                        }
                    }
                case ExpressionType.NotEqual:
                    {
                        var left = ((BinaryExpression)@this).Left;
                        var right = ((BinaryExpression)@this).Right;
                        if (!_NotEqual.TryGetValue((left.Type, right.Type), out var notEqual))
                        {
                            lock (_Sync)
                            {
                                if (!_NotEqual.TryGetValue((left.Type, right.Type), out notEqual))
                                {
                                    var leftObj = Expression.Parameter(typeof(object), "leftObj");
                                    var rightObj = Expression.Parameter(typeof(object), "rightObj");
                                    var expr = Expression.Convert(
                                        Expression.NotEqual(Expression.Convert(leftObj, left.Type), Expression.Convert(rightObj, right.Type)),
                                        typeof(object));
                                    notEqual = Expression.Lambda<Func<object, object, object>>(expr, leftObj, rightObj).Compile();
                                    var NotEqual = new Dictionary<(Type, Type), Func<object, object, object>>(_NotEqual);
                                    NotEqual.Add((left.Type, right.Type), notEqual);
                                    _NotEqual = NotEqual;
                                }
                            }
                        }
                        if (TryInvoke(left, parameters, out var valueLeft) && TryInvoke(right, parameters, out var valueRight))
                        {
                            result = notEqual(valueLeft, valueRight);
                            return true;
                        }
                        else
                        {
                            result = null;
                            return false;
                        }
                    }
                case ExpressionType.GreaterThan:
                    {
                        var left = ((BinaryExpression)@this).Left;
                        var right = ((BinaryExpression)@this).Right;
                        if (!_GreaterThan.TryGetValue((left.Type, right.Type), out var greaterThan))
                        {
                            lock (_Sync)
                            {
                                if (!_GreaterThan.TryGetValue((left.Type, right.Type), out greaterThan))
                                {
                                    var leftObj = Expression.Parameter(typeof(object), "leftObj");
                                    var rightObj = Expression.Parameter(typeof(object), "rightObj");
                                    var expr = Expression.Convert(
                                        Expression.GreaterThan(Expression.Convert(leftObj, left.Type), Expression.Convert(rightObj, right.Type)),
                                        typeof(object));
                                    greaterThan = Expression.Lambda<Func<object, object, object>>(expr, leftObj, rightObj).Compile();
                                    var GreaterThan = new Dictionary<(Type, Type), Func<object, object, object>>(_GreaterThan);
                                    GreaterThan.Add((left.Type, right.Type), greaterThan);
                                    _GreaterThan = GreaterThan;
                                }
                            }
                        }
                        if (TryInvoke(left, parameters, out var valueLeft) && TryInvoke(right, parameters, out var valueRight))
                        {
                            result = greaterThan(valueLeft, valueRight);
                            return true;
                        }
                        else
                        {
                            result = null;
                            return false;
                        }
                    }
                case ExpressionType.GreaterThanOrEqual:
                    {
                        var left = ((BinaryExpression)@this).Left;
                        var right = ((BinaryExpression)@this).Right;
                        if (!_GreaterThanOrEqual.TryGetValue((left.Type, right.Type), out var greaterThanOrEqual))
                        {
                            lock (_Sync)
                            {
                                if (!_GreaterThanOrEqual.TryGetValue((left.Type, right.Type), out greaterThanOrEqual))
                                {
                                    var leftObj = Expression.Parameter(typeof(object), "leftObj");
                                    var rightObj = Expression.Parameter(typeof(object), "rightObj");
                                    var expr = Expression.Convert(
                                        Expression.GreaterThanOrEqual(Expression.Convert(leftObj, left.Type), Expression.Convert(rightObj, right.Type)),
                                        typeof(object));
                                    greaterThanOrEqual = Expression.Lambda<Func<object, object, object>>(expr, leftObj, rightObj).Compile();
                                    var GreaterThanOrEqual = new Dictionary<(Type, Type), Func<object, object, object>>(_GreaterThanOrEqual);
                                    GreaterThanOrEqual.Add((left.Type, right.Type), greaterThanOrEqual);
                                    _GreaterThanOrEqual = GreaterThanOrEqual;
                                }
                            }
                        }
                        if (TryInvoke(left, parameters, out var valueLeft) && TryInvoke(right, parameters, out var valueRight))
                        {
                            result = greaterThanOrEqual(valueLeft, valueRight);
                            return true;
                        }
                        else
                        {
                            result = null;
                            return false;
                        }
                    }
                case ExpressionType.LessThan:
                    {
                        var left = ((BinaryExpression)@this).Left;
                        var right = ((BinaryExpression)@this).Right;
                        if (!_LessThan.TryGetValue((left.Type, right.Type), out var lessThan))
                        {
                            lock (_Sync)
                            {
                                if (!_LessThan.TryGetValue((left.Type, right.Type), out lessThan))
                                {
                                    var leftObj = Expression.Parameter(typeof(object), "leftObj");
                                    var rightObj = Expression.Parameter(typeof(object), "rightObj");
                                    var expr = Expression.Convert(
                                        Expression.LessThan(Expression.Convert(leftObj, left.Type), Expression.Convert(rightObj, right.Type)),
                                        typeof(object));
                                    lessThan = Expression.Lambda<Func<object, object, object>>(expr, leftObj, rightObj).Compile();
                                    var LessThan = new Dictionary<(Type, Type), Func<object, object, object>>(_LessThan);
                                    LessThan.Add((left.Type, right.Type), lessThan);
                                    _LessThan = LessThan;
                                }
                            }
                        }
                        if (TryInvoke(left, parameters, out var valueLeft) && TryInvoke(right, parameters, out var valueRight))
                        {
                            result = lessThan(valueLeft, valueRight);
                            return true;
                        }
                        else
                        {
                            result = null;
                            return false;
                        }
                    }
                case ExpressionType.LessThanOrEqual:
                    {
                        var left = ((BinaryExpression)@this).Left;
                        var right = ((BinaryExpression)@this).Right;
                        if (!_LessThanOrEqual.TryGetValue((left.Type, right.Type), out var lessThanOrEqual))
                        {
                            lock (_Sync)
                            {
                                if (!_LessThanOrEqual.TryGetValue((left.Type, right.Type), out lessThanOrEqual))
                                {
                                    var leftObj = Expression.Parameter(typeof(object), "leftObj");
                                    var rightObj = Expression.Parameter(typeof(object), "rightObj");
                                    var expr = Expression.Convert(
                                        Expression.LessThanOrEqual(Expression.Convert(leftObj, left.Type), Expression.Convert(rightObj, right.Type)),
                                        typeof(object));
                                    lessThanOrEqual = Expression.Lambda<Func<object, object, object>>(expr, leftObj, rightObj).Compile();
                                    var LessThanOrEqual = new Dictionary<(Type, Type), Func<object, object, object>>(_LessThanOrEqual);
                                    LessThanOrEqual.Add((left.Type, right.Type), lessThanOrEqual);
                                    _LessThanOrEqual = LessThanOrEqual;
                                }
                            }
                        }
                        if (TryInvoke(left, parameters, out var valueLeft) && TryInvoke(right, parameters, out var valueRight))
                        {
                            result = lessThanOrEqual(valueLeft, valueRight);
                            return true;
                        }
                        else
                        {
                            result = null;
                            return false;
                        }
                    }
                case ExpressionType.AndAlso:
                    {
                        var left = ((BinaryExpression)@this).Left;
                        var right = ((BinaryExpression)@this).Right;
                        if (!_AndAlso.TryGetValue((left.Type, right.Type), out var andAlso))
                        {
                            lock (_Sync)
                            {
                                if (!_AndAlso.TryGetValue((left.Type, right.Type), out andAlso))
                                {
                                    var leftObj = Expression.Parameter(typeof(object), "leftObj");
                                    var rightObj = Expression.Parameter(typeof(object), "rightObj");
                                    var expr = Expression.Convert(
                                        Expression.AndAlso(Expression.Convert(leftObj, left.Type), Expression.Convert(rightObj, right.Type)),
                                        typeof(object));
                                    andAlso = Expression.Lambda<Func<object, object, object>>(expr, leftObj, rightObj).Compile();
                                    var AndAlso = new Dictionary<(Type, Type), Func<object, object, object>>(_AndAlso);
                                    AndAlso.Add((left.Type, right.Type), andAlso);
                                    _AndAlso = AndAlso;
                                }
                            }
                        }
                        if (TryInvoke(left, parameters, out var valueLeft) && TryInvoke(right, parameters, out var valueRight))
                        {
                            result = andAlso(valueLeft, valueRight);
                            return true;
                        }
                        else
                        {
                            result = null;
                            return false;
                        }
                    }
                case ExpressionType.OrElse:
                    {
                        var left = ((BinaryExpression)@this).Left;
                        var right = ((BinaryExpression)@this).Right;
                        if (!_OrElse.TryGetValue((left.Type, right.Type), out var orElse))
                        {
                            lock (_Sync)
                            {
                                if (!_OrElse.TryGetValue((left.Type, right.Type), out orElse))
                                {
                                    var leftObj = Expression.Parameter(typeof(object), "leftObj");
                                    var rightObj = Expression.Parameter(typeof(object), "rightObj");
                                    var expr = Expression.Convert(
                                        Expression.OrElse(Expression.Convert(leftObj, left.Type), Expression.Convert(rightObj, right.Type)),
                                        typeof(object));
                                    orElse = Expression.Lambda<Func<object, object, object>>(expr, leftObj, rightObj).Compile();
                                    var OrElse = new Dictionary<(Type, Type), Func<object, object, object>>(_OrElse);
                                    OrElse.Add((left.Type, right.Type), orElse);
                                    _OrElse = OrElse;
                                }
                            }
                        }
                        if (TryInvoke(left, parameters, out var valueLeft) && TryInvoke(right, parameters, out var valueRight))
                        {
                            result = orElse(valueLeft, valueRight);
                            return true;
                        }
                        else
                        {
                            result = null;
                            return false;
                        }
                    }
                case ExpressionType.And:
                    {
                        var left = ((BinaryExpression)@this).Left;
                        var right = ((BinaryExpression)@this).Right;
                        if (!_And.TryGetValue((left.Type, right.Type), out var and))
                        {
                            lock (_Sync)
                            {
                                if (!_And.TryGetValue((left.Type, right.Type), out and))
                                {
                                    var leftObj = Expression.Parameter(typeof(object), "leftObj");
                                    var rightObj = Expression.Parameter(typeof(object), "rightObj");
                                    var expr = Expression.Convert(
                                        Expression.And(Expression.Convert(leftObj, left.Type), Expression.Convert(rightObj, right.Type)),
                                        typeof(object));
                                    and = Expression.Lambda<Func<object, object, object>>(expr, leftObj, rightObj).Compile();
                                    var And = new Dictionary<(Type, Type), Func<object, object, object>>(_And);
                                    And.Add((left.Type, right.Type), and);
                                    _And = And;
                                }
                            }
                        }
                        if (TryInvoke(left, parameters, out var valueLeft) && TryInvoke(right, parameters, out var valueRight))
                        {
                            result = and(valueLeft, valueRight);
                            return true;
                        }
                        else
                        {
                            result = null;
                            return false;
                        }
                    }
                case ExpressionType.Or:
                    {
                        var left = ((BinaryExpression)@this).Left;
                        var right = ((BinaryExpression)@this).Right;
                        if (!_Or.TryGetValue((left.Type, right.Type), out var or))
                        {
                            lock (_Sync)
                            {
                                if (!_Or.TryGetValue((left.Type, right.Type), out or))
                                {
                                    var leftObj = Expression.Parameter(typeof(object), "leftObj");
                                    var rightObj = Expression.Parameter(typeof(object), "rightObj");
                                    var expr = Expression.Convert(
                                        Expression.Or(Expression.Convert(leftObj, left.Type), Expression.Convert(rightObj, right.Type)),
                                        typeof(object));
                                    or = Expression.Lambda<Func<object, object, object>>(expr, leftObj, rightObj).Compile();
                                    var Or = new Dictionary<(Type, Type), Func<object, object, object>>(_Or);
                                    Or.Add((left.Type, right.Type), or);
                                    _Or = Or;
                                }
                            }
                        }
                        if (TryInvoke(left, parameters, out var valueLeft) && TryInvoke(right, parameters, out var valueRight))
                        {
                            result = or(valueLeft, valueRight);
                            return true;
                        }
                        else
                        {
                            result = null;
                            return false;
                        }
                    }
                case ExpressionType.Add:
                    {
                        var left = ((BinaryExpression)@this).Left;
                        var right = ((BinaryExpression)@this).Right;
                        if (!_Add.TryGetValue((left.Type, right.Type), out var add))
                        {
                            lock (_Sync)
                            {
                                if (!_Add.TryGetValue((left.Type, right.Type), out add))
                                {
                                    var leftObj = Expression.Parameter(typeof(object), "leftObj");
                                    var rightObj = Expression.Parameter(typeof(object), "rightObj");
                                    var expr = Expression.Convert(
                                        Expression.Add(Expression.Convert(leftObj, left.Type), Expression.Convert(rightObj, right.Type)),
                                        typeof(object));
                                    add = Expression.Lambda<Func<object, object, object>>(expr, leftObj, rightObj).Compile();
                                    var Add = new Dictionary<(Type, Type), Func<object, object, object>>(_Add);
                                    Add.Add((left.Type, right.Type), add);
                                    _Add = Add;
                                }
                            }
                        }
                        if (TryInvoke(left, parameters, out var valueLeft) && TryInvoke(right, parameters, out var valueRight))
                        {
                            result = add(valueLeft, valueRight);
                            return true;
                        }
                        else
                        {
                            result = null;
                            return false;
                        }
                    }
                case ExpressionType.Subtract:
                    {
                        var left = ((BinaryExpression)@this).Left;
                        var right = ((BinaryExpression)@this).Right;
                        if (!_Subtract.TryGetValue((left.Type, right.Type), out var subtract))
                        {
                            lock (_Sync)
                            {
                                if (!_Subtract.TryGetValue((left.Type, right.Type), out subtract))
                                {
                                    var leftObj = Expression.Parameter(typeof(object), "leftObj");
                                    var rightObj = Expression.Parameter(typeof(object), "rightObj");
                                    var expr = Expression.Convert(
                                        Expression.Subtract(Expression.Convert(leftObj, left.Type), Expression.Convert(rightObj, right.Type)),
                                        typeof(object));
                                    subtract = Expression.Lambda<Func<object, object, object>>(expr, leftObj, rightObj).Compile();
                                    var Subtract = new Dictionary<(Type, Type), Func<object, object, object>>(_Subtract);
                                    Subtract.Add((left.Type, right.Type), subtract);
                                    _Subtract = Subtract;
                                }
                            }
                        }
                        if (TryInvoke(left, parameters, out var valueLeft) && TryInvoke(right, parameters, out var valueRight))
                        {
                            result = subtract(valueLeft, valueRight);
                            return true;
                        }
                        else
                        {
                            result = null;
                            return false;
                        }
                    }
                case ExpressionType.Multiply:
                    {
                        var left = ((BinaryExpression)@this).Left;
                        var right = ((BinaryExpression)@this).Right;
                        if (!_Multiply.TryGetValue((left.Type, right.Type), out var multiply))
                        {
                            lock (_Sync)
                            {
                                if (!_Multiply.TryGetValue((left.Type, right.Type), out multiply))
                                {
                                    var leftObj = Expression.Parameter(typeof(object), "leftObj");
                                    var rightObj = Expression.Parameter(typeof(object), "rightObj");
                                    var expr = Expression.Convert(
                                        Expression.Multiply(Expression.Convert(leftObj, left.Type), Expression.Convert(rightObj, right.Type)),
                                        typeof(object));
                                    multiply = Expression.Lambda<Func<object, object, object>>(expr, leftObj, rightObj).Compile();
                                    var Multiply = new Dictionary<(Type, Type), Func<object, object, object>>(_Multiply);
                                    Multiply.Add((left.Type, right.Type), multiply);
                                    _Multiply = Multiply;
                                }
                            }
                        }
                        if (TryInvoke(left, parameters, out var valueLeft) && TryInvoke(right, parameters, out var valueRight))
                        {
                            result = multiply(valueLeft, valueRight);
                            return true;
                        }
                        else
                        {
                            result = null;
                            return false;
                        }
                    }
                case ExpressionType.Divide:
                    {
                        var left = ((BinaryExpression)@this).Left;
                        var right = ((BinaryExpression)@this).Right;
                        if (!_Divide.TryGetValue((left.Type, right.Type), out var divide))
                        {
                            lock (_Sync)
                            {
                                if (!_Divide.TryGetValue((left.Type, right.Type), out divide))
                                {
                                    var leftObj = Expression.Parameter(typeof(object), "leftObj");
                                    var rightObj = Expression.Parameter(typeof(object), "rightObj");
                                    var expr = Expression.Convert(
                                        Expression.Divide(Expression.Convert(leftObj, left.Type), Expression.Convert(rightObj, right.Type)),
                                        typeof(object));
                                    divide = Expression.Lambda<Func<object, object, object>>(expr, leftObj, rightObj).Compile();
                                    var Divide = new Dictionary<(Type, Type), Func<object, object, object>>(_Divide);
                                    Divide.Add((left.Type, right.Type), divide);
                                    _Divide = Divide;
                                }
                            }
                        }
                        if (TryInvoke(left, parameters, out var valueLeft) && TryInvoke(right, parameters, out var valueRight))
                        {
                            result = divide(valueLeft, valueRight);
                            return true;
                        }
                        else
                        {
                            result = null;
                            return false;
                        }
                    }
                case ExpressionType.Modulo:
                    {
                        var left = ((BinaryExpression)@this).Left;
                        var right = ((BinaryExpression)@this).Right;
                        if (!_Modulo.TryGetValue((left.Type, right.Type), out var modulo))
                        {
                            lock (_Sync)
                            {
                                if (!_Modulo.TryGetValue((left.Type, right.Type), out modulo))
                                {
                                    var leftObj = Expression.Parameter(typeof(object), "leftObj");
                                    var rightObj = Expression.Parameter(typeof(object), "rightObj");
                                    var expr = Expression.Convert(
                                        Expression.Modulo(Expression.Convert(leftObj, left.Type), Expression.Convert(rightObj, right.Type)),
                                        typeof(object));
                                    modulo = Expression.Lambda<Func<object, object, object>>(expr, leftObj, rightObj).Compile();
                                    var Modulo = new Dictionary<(Type, Type), Func<object, object, object>>(_Modulo);
                                    Modulo.Add((left.Type, right.Type), modulo);
                                    _Modulo = Modulo;
                                }
                            }
                        }
                        if (TryInvoke(left, parameters, out var valueLeft) && TryInvoke(right, parameters, out var valueRight))
                        {
                            result = modulo(valueLeft, valueRight);
                            return true;
                        }
                        else
                        {
                            result = null;
                            return false;
                        }
                    }
                case ExpressionType.Coalesce:
                    {
                        var left = ((BinaryExpression)@this).Left;
                        var right = ((BinaryExpression)@this).Right;
                        if (!_Coalesce.TryGetValue((left.Type, right.Type), out var coalesce))
                        {
                            lock (_Sync)
                            {
                                if (!_Coalesce.TryGetValue((left.Type, right.Type), out coalesce))
                                {
                                    var leftObj = Expression.Parameter(typeof(object), "leftObj");
                                    var rightObj = Expression.Parameter(typeof(object), "rightObj");
                                    var expr = Expression.Convert(
                                        Expression.Coalesce(Expression.Convert(leftObj, left.Type), Expression.Convert(rightObj, right.Type)),
                                        typeof(object));
                                    coalesce = Expression.Lambda<Func<object, object, object>>(expr, leftObj, rightObj).Compile();
                                    var Coalesce = new Dictionary<(Type, Type), Func<object, object, object>>(_Coalesce);
                                    Coalesce.Add((left.Type, right.Type), coalesce);
                                    _Coalesce = Coalesce;
                                }
                            }
                        }
                        if (TryInvoke(left, parameters, out var valueLeft) && TryInvoke(right, parameters, out var valueRight))
                        {
                            result = coalesce(valueLeft, valueRight);
                            return true;
                        }
                        else
                        {
                            result = null;
                            return false;
                        }
                    }
                case ExpressionType.Conditional:
                    {
                        var test = ((ConditionalExpression)@this).Test;
                        var ifTrue = ((ConditionalExpression)@this).IfTrue;
                        var ifFalse = ((ConditionalExpression)@this).IfFalse;
                        if (!_Conditional.TryGetValue((test.Type, ifTrue.Type, ifFalse.Type), out var conditional))
                        {
                            lock (_Sync)
                            {
                                if (!_Conditional.TryGetValue((test.Type, ifTrue.Type, ifFalse.Type), out conditional))
                                {
                                    var testObj = Expression.Parameter(typeof(object), "testObj");
                                    var ifTrueObj = Expression.Parameter(typeof(object), "ifTrueObj");
                                    var ifFalseObj = Expression.Parameter(typeof(object), "ifFalseObj");
                                    var expr = Expression.Convert(
                                        Expression.Condition(Expression.Convert(testObj, test.Type), Expression.Convert(ifTrueObj, ifTrue.Type), Expression.Convert(ifFalseObj, ifFalse.Type)),
                                        typeof(object));
                                    conditional = Expression.Lambda<Func<object, object, object, object>>(expr, testObj, ifTrueObj, ifFalseObj).Compile();
                                    var Conditional = new Dictionary<(Type, Type, Type), Func<object, object, object, object>>(_Conditional);
                                    Conditional.Add((test.Type, ifTrue.Type, ifFalse.Type), conditional);
                                    _Conditional = Conditional;
                                }
                            }
                        }
                        if (TryInvoke(test, parameters, out var valueTest) && TryInvoke(ifTrue, parameters, out var valueIfTrue) && TryInvoke(ifFalse, parameters, out var valueIfFalse))
                        {
                            result = conditional(valueTest, valueIfTrue, valueIfFalse);
                            return true;
                        }
                        else
                        {
                            result = null;
                            return false;
                        }
                    }
                case ExpressionType.Constant:
                    {
                        result = ((ConstantExpression)@this).Value;
                        return true;
                    }
                case ExpressionType.Quote:
                    {
                        result = ((UnaryExpression)@this).Operand;
                        return true;
                    }
                case ExpressionType.New:
                    {
                        var ctor = (NewExpression)@this;
                        if (ctor.Arguments.Count == 0)
                        {
                            result = ctor.Constructor.Invoke(null);
                            return true;
                        }
                        var args = new object[ctor.Arguments.Count];
                        for (int i = 0; i < args.Length; i++)
                        {
                            if (TryInvoke(ctor.Arguments[i], parameters, out var value))
                            {
                                args[i] = value;
                            }
                            else
                            {
                                result = null;
                                return false;
                            }
                        }
                        result = ctor.Constructor.Invoke(args);
                        return true;
                    }
                case ExpressionType.NewArrayBounds:
                    {
                        var newArray = (NewArrayExpression)@this;
                        if (TryInvoke(newArray.Expressions[0], parameters, out var count))
                        {
                            result = Array.CreateInstance(newArray.Type.GetElementType(), (int)count);
                            return true;
                        }
                        else
                        {
                            result = null;
                            return false;
                        }
                    }
                case ExpressionType.NewArrayInit:
                    {
                        var newArray = (NewArrayExpression)@this;
                        var eleExprs = newArray.Expressions;
                        var array = Array.CreateInstance(newArray.Type.GetElementType(), newArray.Expressions.Count);
                        for (int i = 0; i < eleExprs.Count; i++)
                        {
                            if (TryInvoke(eleExprs[i], parameters, out var value))
                            {
                                array.SetValue(value, i);
                            }
                            else
                            {
                                result = null;
                                return false;
                            }
                        }
                        result = array;
                        return true;
                    }
                case ExpressionType.ListInit:
                    {
                        var listInit = (ListInitExpression)@this;
                        if (TryInvoke(listInit.NewExpression, parameters, out var list))
                        {
                            foreach (var initializer in listInit.Initializers)
                            {
                                var args = new object[initializer.Arguments.Count];
                                for (int i = 0; i < args.Length; i++)
                                {
                                    if (TryInvoke(initializer.Arguments[i], parameters, out var value))
                                    {
                                        args[i] = value;
                                    }
                                    else
                                    {
                                        result = null;
                                        return false;
                                    }
                                }
                                initializer.AddMethod.Invoke(list, args);
                            }
                            result = list;
                            return true;
                        }
                        else
                        {
                            result = null;
                            return false;
                        }
                    }
                case ExpressionType.MemberInit:
                    {
                        var memberInit = (MemberInitExpression)@this;
                        if (TryInvoke(memberInit.NewExpression, parameters, out var obj))
                        {
                            foreach (var binding in memberInit.Bindings)
                            {
                                var memberAssignment = (MemberAssignment)binding;
                                if (TryInvoke(memberAssignment.Expression, parameters, out var value))
                                {
                                    if (memberAssignment.Member is FieldInfo field)
                                        field.SetValue(obj, value);
                                    else if (memberAssignment.Member is PropertyInfo property)
                                        property.SetValue(obj, value);
                                    else
                                    {
                                        result = null;
                                        return false;
                                    }
                                }
                                else
                                {
                                    result = null;
                                    return false;
                                }
                            }
                            result = obj;
                            return true;
                        }
                        else
                        {
                            result = null;
                            return false;
                        }
                    }
                case ExpressionType.MemberAccess:
                    {
                        var member = (MemberExpression)@this;
                        if (TryInvoke(member.Expression, parameters, out var value))
                        {
                            if (member.Member is FieldInfo field)
                            {
                                result = field.GetValue(value);
                                return true;
                            }
                            else if (member.Member is PropertyInfo property)
                            {
                                result = property.GetValue(value);
                                return true;
                            }
                            else
                            {
                                result = null;
                                return false;
                            }
                        }
                        else
                        {
                            result = null;
                            return false;
                        }
                    }
                case ExpressionType.Call:
                    {
                        var method = (MethodCallExpression)@this;
                        if (TryInvoke(method.Object, parameters, out var obj))
                        {
                            if (method.Arguments.Count == 0)
                            {
                                result = method.Method.Invoke(obj, null);
                                return true;
                            }
                            else
                            {
                                var args = new object[method.Arguments.Count];
                                for (int i = 0; i < args.Length; i++)
                                {
                                    if (TryInvoke(method.Arguments[i], parameters, out var value))
                                    {
                                        args[i] = value;
                                    }
                                    else
                                    {
                                        result = null;
                                        return false;
                                    }
                                }
                                result = method.Method.Invoke(obj, args);
                                return true;
                            }
                        }
                        else
                        {
                            result = null;
                            return false;
                        }
                    }
                case ExpressionType.ArrayIndex:
                    {
                        var arrayIndex = (BinaryExpression)@this;
                        if (TryInvoke(arrayIndex.Left, parameters, out var array) && TryInvoke(arrayIndex.Right, parameters, out var index))
                        {
                            result = ((Array)array).GetValue((int)index);
                            return true;
                        }
                        else
                        {
                            result = null;
                            return false;
                        }
                    }
                case ExpressionType.ArrayLength:
                    {
                        var arrayLength = (UnaryExpression)@this;
                        if (TryInvoke(arrayLength.Operand, parameters, out var array))
                        {
                            result = ((Array)array).Length;
                            return true;
                        }
                        else
                        {
                            result = null;
                            return false;
                        }
                    }
                case ExpressionType.Parameter:
                    {
                        var parameter = (ParameterExpression)@this;
                        if (parameters != null && parameters.TryGetValue(parameter, out var value))
                        {
                            result = value;
                            return true;
                        }
                        else
                        {
                            result = null;
                            return false;
                        }
                    }
                default:
                    {
                        result = null;
                        return false;
                    }
            }
        }
        public static bool IsEnumerable(this Type @this, out MethodInfo getEnumerator, out MethodInfo moveNext, out PropertyInfo current)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            getEnumerator = @this.GetMethod("GetEnumerator", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (getEnumerator != null)
            {
                var enumeratorType = getEnumerator.ReturnType;
                if (enumeratorType.IsGenericType && enumeratorType.GetGenericTypeDefinition() == typeof(IEnumerator<>))
                {
                    moveNext = typeof(IEnumerator).GetMethod("MoveNext");
                    current = enumeratorType.GetProperty("Current");
                    return true;
                }
                moveNext = enumeratorType.GetMethod("MoveNext");
                current = enumeratorType.GetProperty("Current");
                return true;
            }

            var interfaces = @this.GetInterfaces();
            //IEnumerable<>
            foreach (var @interface in interfaces)
            {
                if (@interface.IsGenericType && @interface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                {
                    getEnumerator = @interface.GetMethod("GetEnumerator");
                    moveNext = typeof(IEnumerator).GetMethod("MoveNext");
                    current = getEnumerator.ReturnType.GetProperty("Current");
                    return true;
                }
            }
            //IEnumerable
            foreach (var @interface in interfaces)
            {
                if (@this == typeof(IEnumerable))
                {
                    getEnumerator = typeof(IEnumerable).GetMethod("GetEnumerator");
                    moveNext = typeof(IEnumerator).GetMethod("MoveNext");
                    current = typeof(IEnumerator).GetProperty("Current");
                    return true;
                }
            }
            getEnumerator = null;
            moveNext = null;
            current = null;
            return false;
        }
        public static Expression Replace(this Expression @this, ParameterExpression oldParameter, ParameterExpression newParameter)
        {
            if (@this == null)
                return null;
            if (oldParameter == null)
                throw new ArgumentNullException(nameof(oldParameter));
            if (newParameter == null)
                throw new ArgumentNullException(nameof(newParameter));

            var visitor = new ParameterVisitor(oldParameter, newParameter);
            return visitor.Visit(@this);
        }
        public static Expression Replace(this Expression @this, ParameterExpression[] oldParameters, ParameterExpression[] newParameters)
        {
            if (@this == null)
                return null;
            if (oldParameters == null)
                throw new ArgumentNullException(nameof(oldParameters));
            if (newParameters == null)
                throw new ArgumentNullException(nameof(newParameters));
            if (newParameters.Length != oldParameters.Length)
                throw new ArgumentException(nameof(newParameters));

            var visitor = new ParametersVisitor(oldParameters, newParameters);
            return visitor.Visit(@this);
        }
        private class ParameterVisitor : ExpressionVisitor
        {
            private ParameterExpression _oldParameter;
            private ParameterExpression _newParameter;
            public ParameterVisitor(ParameterExpression oldParameter, ParameterExpression newParameter)
            {
                _oldParameter = oldParameter;
                _newParameter = newParameter;
            }
            protected override Expression VisitParameter(ParameterExpression parameter)
            {
                if (parameter == _oldParameter)
                    return _newParameter;

                return parameter;
            }
            public override Expression Visit(Expression node)
            {
                return base.Visit(node);
            }
        }
        private class ParametersVisitor : ExpressionVisitor
        {
            private ParameterExpression[] _oldParameters;
            private ParameterExpression[] _newParameters;
            public ParametersVisitor(ParameterExpression[] oldParameters, ParameterExpression[] newParameters)
            {
                _oldParameters = oldParameters;
                _newParameters = newParameters;
            }
            protected override Expression VisitParameter(ParameterExpression parameter)
            {
                for (int i = 0; i < _oldParameters.Length; i++)
                {
                    if (parameter == _oldParameters[i])
                        return _newParameters[i];
                }
                return parameter;
            }
            public override Expression Visit(Expression node)
            {
                return base.Visit(node);
            }
        }
    }
}
