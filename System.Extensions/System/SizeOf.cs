
namespace System
{
    using System.Reflection.Emit;
    public static class SizeOf<T> where T : struct
    {
        static SizeOf()
        {
            var sizeOfType = new DynamicMethod("SizeOfType", typeof(int), new Type[] { });
            ILGenerator il = sizeOfType.GetILGenerator();
            il.Emit(OpCodes.Sizeof, typeof(T));
            il.Emit(OpCodes.Ret);
            Value = (int)sizeOfType.Invoke(null, null);
        }

        public readonly static int Value;
    }
}
