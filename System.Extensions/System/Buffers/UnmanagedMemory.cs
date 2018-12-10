
namespace System.Buffers
{
    using System.Runtime.InteropServices;
    public class UnmanagedMemory<T> : IDisposable where T : struct
    {
        private class UnmanagedMemoryManager : MemoryManager<T>
        {
            public UnmanagedMemoryManager(int length)
            {
                _length = length;
                _memoryPtr = Marshal.AllocHGlobal(length * SizeOf<T>.Value);
            }

            private int _length;
            private IntPtr _memoryPtr;
            public override Span<T> GetSpan()
            {
                unsafe
                {
                    return new Span<T>(_memoryPtr.ToPointer(), _length);
                }
            }
            public override MemoryHandle Pin(int elementIndex = 0)
            {
                unsafe
                {
                    return new MemoryHandle((byte*)_memoryPtr.ToPointer() + elementIndex * SizeOf<T>.Value);
                }
            }
            public override void Unpin()
            {
               
            }
            protected override void Dispose(bool disposing)
            {
                Free();
            }
            public void Free()
            {
                if (_memoryPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(_memoryPtr);
                }
                _memoryPtr = IntPtr.Zero;
            }
            ~UnmanagedMemoryManager()
            {
                Free();
            }
        }
        public UnmanagedMemory(int length)
        {
            if (length <= 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            _memoryManager = new UnmanagedMemoryManager(length);
        }

        private UnmanagedMemoryManager _memoryManager;
        public Memory<T> Memory => _memoryManager.Memory;
        public Span<T> GetSpan() => _memoryManager.GetSpan();
        public void Dispose()
        {
            _memoryManager.Free();
        }
    }
}
