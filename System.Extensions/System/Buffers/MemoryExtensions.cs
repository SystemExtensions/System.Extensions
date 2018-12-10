
namespace System.Buffers
{
    using System.Runtime.InteropServices;
    public static class MemoryExtensions
    {
        public static Memory<byte> AsBytes<T>(this Memory<T> @this) where T : struct
        {
            if (@this.IsEmpty)
                return Memory<byte>.Empty;

            return new _MemoryManager<T>(@this).Memory;
        }
        public static ReadOnlyMemory<byte> AsBytes<T>(this ReadOnlyMemory<T> @this) where T:struct
        {
            if (@this.IsEmpty)
                return ReadOnlyMemory<byte>.Empty;

            return new _MemoryManager<T>(MemoryMarshal.AsMemory(@this)).Memory;
        }
        private class _MemoryManager<T> : MemoryManager<byte> where T : struct
        {
            public _MemoryManager(Memory<T> memory)
            {
                _memory = memory;
            }

            private Memory<T> _memory;
            private MemoryHandle _memoryHandle;
            public override Span<byte> GetSpan()
            {
                return MemoryMarshal.AsBytes(_memory.Span);
            }
            public override MemoryHandle Pin(int elementIndex = 0)
            {
                Console.WriteLine(nameof(Pin));
                _memoryHandle = _memory.Pin();
                unsafe
                {
                    return new MemoryHandle((byte*)_memoryHandle.Pointer + (elementIndex * SizeOf<T>.Value));
                }
            }
            public override void Unpin()
            {
                Console.WriteLine(nameof(Unpin));
                _memoryHandle.Dispose();
            }
            protected override void Dispose(bool disposing)
            {
                throw new NotImplementedException();
            }
        }
    }
}
