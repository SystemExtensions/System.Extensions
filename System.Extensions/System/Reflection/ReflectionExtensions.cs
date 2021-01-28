
namespace System.Reflection
{
    using System.Collections.Generic;
    using System.Reflection.Emit;
    public static class ReflectionExtensions
    {
        static void EmitCast(Type typeFrom, Type typeTo, ILGenerator il)
        {
            if (typeFrom.IsByRef)//TODO
                return;

            if (typeFrom == typeTo)
                return;

            if (typeTo == typeof(void))
            {
                il.Emit(OpCodes.Pop);
                return;
            }

            if (typeFrom.IsValueType && typeTo.IsValueType)
                throw new NotSupportedException(nameof(EmitCast));

            if (typeFrom.IsValueType)
            {
                il.Emit(OpCodes.Box, typeFrom);
            }
            else if (typeTo.IsValueType)
            {
                il.Emit(OpCodes.Unbox_Any, typeTo);
            }
            else if (!typeTo.IsAssignableFrom(typeFrom))
            {
                il.Emit(OpCodes.Castclass, typeTo);
            }
        }
        public static TDelegate CreateDelegate<TDelegate>(this FieldInfo @this) where TDelegate : Delegate
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            var delegateMethod = typeof(TDelegate).GetMethod("Invoke");
            var delegateParameters = delegateMethod.GetParameters();
            var parameterTypes = new Type[delegateParameters.Length];
            for (int i = 0; i < delegateParameters.Length; i++)
            {
                parameterTypes[i] = delegateParameters[i].ParameterType;
            }
            if (delegateMethod.ReturnType == typeof(void))
            {
                if (@this.IsStatic)
                {
                    if (parameterTypes.Length != 1)
                        throw new ArgumentException(nameof(TDelegate));
                    var setValue = new DynamicMethod("_SetValue", typeof(void), parameterTypes);
                    var il = setValue.GetILGenerator();
                    il.Emit(OpCodes.Ldarg_0);
                    EmitCast(parameterTypes[0], @this.FieldType, il);
                    il.Emit(OpCodes.Stsfld, @this);
                    il.Emit(OpCodes.Ret);
                    return setValue.CreateDelegate(typeof(TDelegate)) as TDelegate;
                }
                else
                {
                    if (parameterTypes.Length != 2)
                        throw new ArgumentException(nameof(TDelegate));
                    var setValue = new DynamicMethod("_SetValue", typeof(void), parameterTypes);
                    var il = setValue.GetILGenerator();
                    il.Emit(OpCodes.Ldarg_0);
                    EmitCast(parameterTypes[0], @this.ReflectedType, il);
                    il.Emit(OpCodes.Ldarg_1);
                    EmitCast(parameterTypes[1], @this.FieldType, il);
                    il.Emit(OpCodes.Stfld, @this);
                    il.Emit(OpCodes.Ret);
                    return setValue.CreateDelegate(typeof(TDelegate)) as TDelegate;
                }
            }
            else
            {
                if (@this.IsStatic)
                {
                    if (parameterTypes.Length != 0)
                        throw new ArgumentException(nameof(TDelegate));
                    var getValue = new DynamicMethod("_GetValue", delegateMethod.ReturnType, parameterTypes);
                    var il = getValue.GetILGenerator();
                    il.Emit(OpCodes.Ldsfld, @this);
                    EmitCast(@this.FieldType, delegateMethod.ReturnType, il);
                    il.Emit(OpCodes.Ret);
                    return getValue.CreateDelegate(typeof(TDelegate)) as TDelegate;
                }
                else
                {
                    if (parameterTypes.Length != 1)
                        throw new ArgumentException(nameof(TDelegate));
                    var getValue = new DynamicMethod("_GetValue", delegateMethod.ReturnType, parameterTypes);
                    var il = getValue.GetILGenerator();
                    il.Emit(OpCodes.Ldarg_0);
                    EmitCast(parameterTypes[0], @this.ReflectedType, il);
                    il.Emit(OpCodes.Ldfld, @this);
                    EmitCast(@this.FieldType, delegateMethod.ReturnType, il);
                    il.Emit(OpCodes.Ret);
                    return getValue.CreateDelegate(typeof(TDelegate)) as TDelegate;
                }
            }
        }
        public static TDelegate CreateDelegate<TDelegate>(this PropertyInfo @this) where TDelegate : Delegate
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            var delegateMethod = typeof(TDelegate).GetMethod("Invoke");
            if (delegateMethod.ReturnType == typeof(void))
            {
                if (@this.SetMethod == null)
                    throw new ArgumentException(nameof(TDelegate));
                return CreateDelegate<TDelegate>(@this.SetMethod);
            }
            else
            {
                if (@this.GetMethod == null)
                    throw new ArgumentException(nameof(TDelegate));
                return CreateDelegate<TDelegate>(@this.GetMethod);
            }
        }
        public static TDelegate CreateDelegate<TDelegate>(this ConstructorInfo @this) where TDelegate : Delegate
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            var parameters = @this.GetParameters();
            var delegateMethod = typeof(TDelegate).GetMethod("Invoke");
            if (delegateMethod.ReturnType == typeof(void))
                throw new ArgumentException(nameof(TDelegate));
            var delegateParameters = delegateMethod.GetParameters();
            if (delegateParameters.Length != parameters.Length)
                throw new ArgumentException(nameof(TDelegate));
            var parameterTypes = new Type[delegateParameters.Length];
            for (int i = 0; i < delegateParameters.Length; i++)
            {
                parameterTypes[i] = delegateParameters[i].ParameterType;
            }
            var ctor = new DynamicMethod("_Ctor ", delegateMethod.ReturnType, parameterTypes);
            var il = ctor.GetILGenerator();
            if (parameterTypes.Length < 1)
                goto newObj;
            il.Emit(OpCodes.Ldarg_0);
            EmitCast(parameterTypes[0], parameters[0].ParameterType, il);
            if (parameterTypes.Length < 2)
                goto newObj;
            il.Emit(OpCodes.Ldarg_1);
            EmitCast(parameterTypes[1], parameters[1].ParameterType, il);
            if (parameterTypes.Length < 3)
                goto newObj;
            il.Emit(OpCodes.Ldarg_2);
            EmitCast(parameterTypes[2], parameters[2].ParameterType, il);
            if (parameterTypes.Length < 4)
                goto newObj;
            il.Emit(OpCodes.Ldarg_3);
            EmitCast(parameterTypes[3], parameters[3].ParameterType, il);
            for (int i = 4; i < parameters.Length; i++)
            {
                il.Emit(OpCodes.Ldarg_S, (byte)i);
                EmitCast(parameterTypes[i], parameters[i].ParameterType, il);
            }
        newObj:
            il.Emit(OpCodes.Newobj, @this);
            EmitCast(@this.ReflectedType, delegateMethod.ReturnType, il);
            il.Emit(OpCodes.Ret);
            return (TDelegate)ctor.CreateDelegate(typeof(TDelegate));
        }
        public static TDelegate CreateDelegate<TDelegate>(this MethodInfo @this) where TDelegate : Delegate
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            var delegateValue = Delegate.CreateDelegate(typeof(TDelegate), @this,false);
            if (delegateValue != null)
                return (TDelegate)delegateValue;

            var rawParameterTypes = default(Type[]);
            #region rawParameterTypes
            if (@this.IsStatic)
            {
                var parameters = @this.GetParameters();
                rawParameterTypes = new Type[parameters.Length];
                for (int i = 0; i < parameters.Length; i++)
                {
                    rawParameterTypes[i] = parameters[i].ParameterType;
                }
            }
            else
            {
                var parameters = @this.GetParameters();
                rawParameterTypes = new Type[parameters.Length + 1];
                rawParameterTypes[0] = @this.ReflectedType;
                for (int i = 0; i < parameters.Length; i++)
                {
                    rawParameterTypes[i + 1] = parameters[i].ParameterType;
                }
            }
            #endregion
            var delegateMethod = typeof(TDelegate).GetMethod("Invoke");
            var delegateParameters = delegateMethod.GetParameters();
            if (delegateParameters.Length != rawParameterTypes.Length)
                throw new ArgumentException(nameof(TDelegate));
            var parameterTypes = new Type[delegateParameters.Length];
            for (int i = 0; i < delegateParameters.Length; i++)
            {
                parameterTypes[i] = delegateParameters[i].ParameterType;
            }
            var invoker = new DynamicMethod("_Invoke ", delegateMethod.ReturnType, parameterTypes);
            var il = invoker.GetILGenerator();
            if (parameterTypes.Length < 1)
                goto methodCall;
            il.Emit(OpCodes.Ldarg_0);
            EmitCast(parameterTypes[0], rawParameterTypes[0],il);
            if (parameterTypes.Length < 2)
                goto methodCall;
            il.Emit(OpCodes.Ldarg_1);
            EmitCast(parameterTypes[1], rawParameterTypes[1], il);
            if (parameterTypes.Length < 3)
                goto methodCall;
            il.Emit(OpCodes.Ldarg_2);
            EmitCast(parameterTypes[2], rawParameterTypes[2], il);
            if (parameterTypes.Length < 4)
                goto methodCall;
            il.Emit(OpCodes.Ldarg_3);
            EmitCast(parameterTypes[3], rawParameterTypes[3], il);
            for (int i = 4; i < parameterTypes.Length; i++)
            {
                il.Emit(OpCodes.Ldarg_S, (byte)i);
                EmitCast(parameterTypes[i], rawParameterTypes[i], il);
            }
        methodCall:
            il.Emit(OpCodes.Call, @this);
            EmitCast(@this.ReturnType, delegateMethod.ReturnType, il);
            il.Emit(OpCodes.Ret);
            return (TDelegate)invoker.CreateDelegate(typeof(TDelegate));
        }
        //Sort Declare(MetadataToken)
        private static Comparison<PropertyInfo> _Comparison = (p1, p2) => p1.MetadataToken - p2.MetadataToken;
        public static PropertyInfo[] GetGeneralProperties(this Type @this, BindingFlags bindingAttr)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            var properties = @this.GetProperties(bindingAttr);
            Array.Sort(properties, _Comparison);
            if (@this.IsInterface && (bindingAttr & BindingFlags.DeclaredOnly) == 0)
            {
                var dictionary = new Dictionary<string, PropertyInfo>();
                foreach (var property in properties)
                {
                    if (property.PropertyType.IsByRef
                    || property.PropertyType.IsByRefLike
                    || property.PropertyType.IsPointer
                    || property.GetIndexParameters().Length > 0)
                        continue;

                    dictionary.TryAdd(property.Name, property);
                }
                var interfaces = @this.GetInterfaces();
                foreach (var @interface in interfaces)
                {
                    var interfaceProperties = @interface.GetProperties(bindingAttr);
                    Array.Sort(interfaceProperties, _Comparison);
                    foreach (var property in interfaceProperties)
                    {
                        if (property.PropertyType.IsByRef
                        || property.PropertyType.IsByRefLike
                        || property.PropertyType.IsPointer
                        || property.GetIndexParameters().Length > 0)
                            continue;

                        dictionary.TryAdd(property.Name, property);
                    }
                }
                properties = new PropertyInfo[dictionary.Count];
                dictionary.Values.CopyTo(properties, 0);
                return properties;
            }
            else
            {
                var index = 0;
                var length = properties.Length;
                for (; ; )
                {
                    if (index == length)
                        return properties;

                    var property = properties[index];
                    if (property.PropertyType.IsByRef
                        || property.PropertyType.IsByRefLike
                        || property.PropertyType.IsPointer
                        || property.GetIndexParameters().Length > 0)
                        break;
                    index++;
                }
                var list = new List<PropertyInfo>();
                for (int i = 0; i < index; i++)
                {
                    list.Add(properties[i]);
                }

                for (; ; )
                {
                    if (index == length)
                        return list.ToArray();

                    var property = properties[index++];
                    if (property.PropertyType.IsByRef
                        || property.PropertyType.IsByRefLike
                        || property.PropertyType.IsPointer
                        || property.GetIndexParameters().Length > 0)
                        continue;

                    list.Add(property);
                }
            }
        }
    }
}
