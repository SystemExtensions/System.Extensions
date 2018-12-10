
namespace System.Reflection
{
    using System.Linq.Expressions;
    public static class ReflectionExtensions
    {
        private const string _Invoke = "Invoke";
        public static TDelegate CompileGetter<TDelegate>(this FieldInfo @this)
        {
            var delegateInfo = typeof(TDelegate).GetMethod(_Invoke);
            if (delegateInfo == null)
                throw new ArgumentException(nameof(TDelegate));

            var returnParam = delegateInfo.ReturnParameter;
            if (returnParam.ParameterType == typeof(void))
                throw new ArgumentException(nameof(TDelegate));

            var delegateParams = delegateInfo.GetParameters();
            if (@this.IsStatic)
            {
                if (delegateParams.Length != 0)
                    throw new ArgumentException(nameof(TDelegate));

                var fieldAccess = Expression.Field(null, @this);

                Expression getFieldValue;
                if (returnParam.ParameterType== @this.FieldType)
                    getFieldValue = fieldAccess;
                else//类型不同就进行转换
                    getFieldValue = Expression.Convert(fieldAccess, returnParam.ParameterType);

                return Expression.Lambda<TDelegate>(getFieldValue).Compile();
            }
            else
            {
                if (delegateParams.Length != 1)
                    throw new ArgumentException(nameof(TDelegate));

                var instance = Expression.Parameter(delegateParams[0].ParameterType, "instance");
                Expression instanceCast;
                if (delegateParams[0].ParameterType == @this.ReflectedType)
                    instanceCast = instance;
                else
                    instanceCast = Expression.Convert(instance, @this.ReflectedType);

                var fieldAccess = Expression.Field(instanceCast, @this);
                Expression getFieldValue;
                if (returnParam.ParameterType == @this.FieldType)
                    getFieldValue = fieldAccess;
                else
                    getFieldValue = Expression.Convert(fieldAccess, returnParam.ParameterType);

                return Expression.Lambda<TDelegate>(getFieldValue, instance).Compile();
            }
        }
        public static TDelegate CompileSetter<TDelegate>(this FieldInfo @this)
        {
            var delegateInfo = typeof(TDelegate).GetMethod(_Invoke);
            if (delegateInfo == null)
                throw new ArgumentException(nameof(TDelegate));

            var returnParam = delegateInfo.ReturnParameter;
            if (returnParam.ParameterType != typeof(void))
                throw new ArgumentException(nameof(TDelegate));

            var delegateParams = delegateInfo.GetParameters();
            if (@this.IsStatic)
            {
                if (delegateParams.Length != 1)
                    throw new ArgumentException(nameof(TDelegate));

                var fieldValue = Expression.Parameter(delegateParams[0].ParameterType, "fieldValue");

                var fieldAccess = Expression.Field(null, @this);
                Expression setFieldValue;
                if (returnParam.ParameterType == @this.FieldType)
                    setFieldValue = fieldValue;
                else
                    setFieldValue = Expression.Convert(fieldValue, @this.FieldType);

                var setField = Expression.Assign(fieldAccess, setFieldValue);

                return Expression.Lambda<TDelegate>(setField, fieldValue).Compile();
            }
            else
            {
                if (delegateParams.Length != 2)
                    throw new ArgumentException(nameof(TDelegate));

                var instance = Expression.Parameter(delegateParams[0].ParameterType, "instance");
                Expression instanceCast;
                if (delegateParams[0].ParameterType == @this.ReflectedType)
                    instanceCast = instance;
                else
                    instanceCast = Expression.Convert(instance, @this.ReflectedType);

                var fieldValue = Expression.Parameter(delegateParams[1].ParameterType, "fieldValue");
                var fieldAccess = Expression.Field(instanceCast, @this);
                Expression setFieldValue;
                if (returnParam.ParameterType == @this.FieldType)
                    setFieldValue = fieldValue;
                else
                    setFieldValue = Expression.Convert(fieldValue, @this.FieldType);

                var setField = Expression.Assign(fieldAccess, setFieldValue);

                return Expression.Lambda<TDelegate>(setField, instance, fieldValue).Compile();
            }
        }

        public static TDelegate CompileGetter<TDelegate>(this PropertyInfo @this)
        {
            if (!@this.CanRead)//是否有Get属性
                throw new ArgumentException(nameof(PropertyInfo));
            var delegateInfo = typeof(TDelegate).GetMethod(_Invoke);
            if (delegateInfo == null)
                throw new ArgumentException(nameof(TDelegate));

            var returnParam = delegateInfo.ReturnParameter;
            if (returnParam.ParameterType == typeof(void))
                throw new ArgumentException(nameof(TDelegate));

            var delegateParams = delegateInfo.GetParameters();
            if (@this.GetMethod.IsStatic)
            {
                if (delegateParams.Length != 0)
                    throw new ArgumentException(nameof(TDelegate));

                var propertyAccess = Expression.Property(null, @this);

                Expression getPropertyValue;
                if (returnParam.ParameterType == @this.PropertyType)
                    getPropertyValue = propertyAccess;
                else
                    getPropertyValue = Expression.Convert(propertyAccess, returnParam.ParameterType);

                return Expression.Lambda<TDelegate>(getPropertyValue).Compile();
            }
            else
            {
                if (delegateParams.Length != 1)
                    throw new ArgumentException(nameof(TDelegate));

                var instance = Expression.Parameter(delegateParams[0].ParameterType, "instance");
                Expression instanceCast;
                if (delegateParams[0].ParameterType == @this.ReflectedType)
                    instanceCast = instance;
                else
                    instanceCast = Expression.Convert(instance, @this.ReflectedType);

                var propertyAccess = Expression.Property(instanceCast, @this);
                Expression getPropertyValue;
                if (returnParam.ParameterType == @this.PropertyType)
                    getPropertyValue = propertyAccess;
                else
                    getPropertyValue = Expression.Convert(propertyAccess, returnParam.ParameterType);

                return Expression.Lambda<TDelegate>(getPropertyValue, instance).Compile();
            }
        }
        public static TDelegate CompileSetter<TDelegate>(this PropertyInfo @this)
        {
            if (!@this.CanWrite)//属性是否Set
                throw new ArgumentException(nameof(PropertyInfo));
            var delegateInfo = typeof(TDelegate).GetMethod(_Invoke);
            if (delegateInfo == null)
                throw new ArgumentException(nameof(TDelegate));

            var returnParam = delegateInfo.ReturnParameter;
            if (returnParam.ParameterType != typeof(void))
                throw new ArgumentException(nameof(TDelegate));

            var delegateParams = delegateInfo.GetParameters();
            if (@this.SetMethod.IsStatic)
            {
                if (delegateParams.Length != 1)
                    throw new ArgumentException(nameof(TDelegate));

                var propertyValue = Expression.Parameter(delegateParams[0].ParameterType, "propertyValue");

                var propertyAccess = Expression.Property(null, @this);
                Expression setPropertyValue;
                if (returnParam.ParameterType == @this.PropertyType)
                    setPropertyValue = propertyValue;
                else
                    setPropertyValue = Expression.Convert(propertyValue, @this.PropertyType);

                var setProperty = Expression.Assign(propertyAccess, setPropertyValue);
                return Expression.Lambda<TDelegate>(setProperty, propertyValue).Compile();
            }
            else
            {
                if (delegateParams.Length != 2)
                    throw new ArgumentException(nameof(TDelegate));

                var instance = Expression.Parameter(delegateParams[0].ParameterType, "instance");
                Expression instanceCast;
                if (delegateParams[0].ParameterType == @this.ReflectedType)
                    instanceCast = instance;
                else
                    instanceCast = Expression.Convert(instance, @this.ReflectedType);

                var propertyValue = Expression.Parameter(delegateParams[1].ParameterType, "propertyValue");
                var propertyAccess = Expression.Property(instanceCast, @this);
                Expression setPropertyValue;
                if (returnParam.ParameterType == @this.PropertyType)
                    setPropertyValue = propertyValue;
                else
                    setPropertyValue = Expression.Convert(propertyValue, @this.PropertyType);

                var setProperty = Expression.Assign(propertyAccess, setPropertyValue);
                return Expression.Lambda<TDelegate>(setProperty, instance, propertyValue).Compile();
            }
        }

        public static TDelegate Compile<TDelegate>(this MethodInfo @this)
        {
            var delegateInfo = typeof(TDelegate).GetMethod(_Invoke);
            if (delegateInfo == null)
                throw new ArgumentException(nameof(TDelegate));

            var returnParam = delegateInfo.ReturnParameter;
            var delegateParams = delegateInfo.GetParameters();
            var methodReturnParam = @this.ReturnParameter;
            var methodParams = @this.GetParameters();

            if (@this.IsStatic)
            {
                if (delegateParams.Length != methodParams.Length)
                    throw new ArgumentException(nameof(TDelegate));

                //定义参数
                var parameters = new ParameterExpression[delegateParams.Length];
                for (int i = 0; i < delegateParams.Length; i++)
                {
                    parameters[i] = Expression.Parameter(delegateParams[i].ParameterType, "arg" + i);
                }
                //方法调用
                var callParameters = new Expression[methodParams.Length];
                for (int i = 0; i < methodParams.Length; i++)
                {
                    if (methodParams[i].ParameterType == delegateParams[i].ParameterType)
                        callParameters[i] = parameters[i];
                    else
                        callParameters[i] = Expression.Convert(parameters[i], methodParams[i].ParameterType);
                }

                var methodAccess = Expression.Call(null, @this, callParameters);

                Expression callMethod;
                if (returnParam.ParameterType == methodReturnParam.ParameterType)
                    callMethod = methodAccess;
                else
                    callMethod = Expression.Convert(methodAccess, returnParam.ParameterType);

                return Expression.Lambda<TDelegate>(callMethod, parameters).Compile();
            }
            else
            {
                if (delegateParams.Length != methodParams.Length + 1)//实例方法
                    throw new ArgumentException(nameof(TDelegate));

                //定义参数
                var parameters = new ParameterExpression[delegateParams.Length];
                var instance = Expression.Parameter(delegateParams[0].ParameterType, "instance");
                parameters[0] = instance;
                for (int i = 1; i < delegateParams.Length; i++)
                {
                    parameters[i] = Expression.Parameter(delegateParams[i].ParameterType, "arg" + (i - 1));
                }
                //方法调用
                var callParameters = new Expression[methodParams.Length];
                for (int i = 0; i < methodParams.Length; i++)
                {
                    if (methodParams[i].ParameterType == delegateParams[i + 1].ParameterType)
                        callParameters[i] = parameters[i + 1];
                    else
                        callParameters[i] = Expression.Convert(parameters[i + 1], methodParams[i].ParameterType);
                }

                var methodAccess = Expression.Call(instance, @this, callParameters);

                Expression callMethod;
                if (returnParam.ParameterType == methodReturnParam.ParameterType)
                    callMethod = methodAccess;
                else
                    callMethod = Expression.Convert(methodAccess, returnParam.ParameterType);

                return Expression.Lambda<TDelegate>(callMethod, parameters).Compile();
            }
        }
        public static TDelegate Compile<TDelegate>(this ConstructorInfo @this)
        {
            var delegateInfo = typeof(TDelegate).GetMethod(_Invoke);
            if (delegateInfo == null)
                throw new ArgumentException(nameof(TDelegate));

            var returnParam = delegateInfo.ReturnParameter;
            if (returnParam.ParameterType == typeof(void))
                throw new ArgumentException(nameof(TDelegate));

            var delegateParams = delegateInfo.GetParameters();
            var constructorParams = @this.GetParameters();

            if (delegateParams.Length != constructorParams.Length)
                throw new ArgumentException(nameof(TDelegate));

            var parameters = new ParameterExpression[delegateParams.Length];
            for (int i = 0; i < delegateParams.Length; i++)
            {
                parameters[i] = Expression.Parameter(delegateParams[i].ParameterType, "arg" + i);
            }

            //方法调用
            var callParameters = new Expression[constructorParams.Length];
            for (int i = 0; i < constructorParams.Length; i++)
            {
                if (constructorParams[i].ParameterType == delegateParams[i].ParameterType)
                    callParameters[i] = parameters[i];
                else
                    callParameters[i] = Expression.Convert(parameters[i], constructorParams[i].ParameterType);
            }
            var constructorAccess= Expression.New(@this, callParameters);


            Expression callConstructor;
            if (returnParam.ParameterType == @this.ReflectedType)
                callConstructor = constructorAccess;
            else
                callConstructor = Expression.Convert(constructorAccess, returnParam.ParameterType);

            return Expression.Lambda<TDelegate>(callConstructor, parameters).Compile();
        }
    }
}
