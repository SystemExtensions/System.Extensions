
namespace System.Buffers
{
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    public class UnmanagedMemory<T> : IDisposable where T : unmanaged
    {
        private class UnmanagedMemoryManager : MemoryManager<T>
        {
            protected int _length;
            protected unsafe T* _dataPtr;
            public UnmanagedMemoryManager() { }
            public unsafe UnmanagedMemoryManager(T* dataPtr,int length)
            {
                Debug.Assert(length > 0);
                _length = length;
                _dataPtr = dataPtr;
            }
            public int Length => _length;
            public unsafe T* DataPtr => _dataPtr;
            public override Span<T> GetSpan()
            {
                if (_length == -1)
                    throw new ObjectDisposedException(nameof(UnmanagedMemoryManager));

                unsafe
                {
                    return new Span<T>(_dataPtr, _length);
                }
            }
            public override MemoryHandle Pin(int elementIndex = 0)
            {
                if (_length == -1)
                    throw new ObjectDisposedException(nameof(UnmanagedMemoryManager));

                unsafe
                {
                    return new MemoryHandle(_dataPtr + elementIndex);
                }
            }
            public override void Unpin()
            {

            }
            protected override void Dispose(bool disposing)
            {
                if (_length != -1) 
                {
                    _length = -1;
                    unsafe { _dataPtr = (T*)0; }
                }
            }
        }
        private class UnmanagedMemoryManagerAlloc : UnmanagedMemoryManager
        {
            public UnmanagedMemoryManagerAlloc(int length)
            {
                Debug.Assert(length > 0);
                _length = length;
                var dataPtr= Marshal.AllocHGlobal(length * BufferExtensions.SizeOf<T>());//the allocated memory is not zero-filled.
                unsafe { _dataPtr = (T*)dataPtr.ToPointer(); }
            }
            protected override void Dispose(bool disposing)
            {
                if (_length != -1)
                {
                    _length = -1;
                    unsafe 
                    {
                        var dataPtr = new IntPtr(_dataPtr);
                        unsafe { _dataPtr = (T*)0; }
                        Marshal.FreeHGlobal(dataPtr);
                    }
                }
            }
            ~UnmanagedMemoryManagerAlloc()
            {
                Dispose(false);
            }
        }
        public UnmanagedMemory(int length)
        {
            if (length <= 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            _memoryManager = new UnmanagedMemoryManagerAlloc(length);
        }
        public unsafe UnmanagedMemory(T* dataPtr, int length)
        {
            if (length <= 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            _memoryManager = new UnmanagedMemoryManager(dataPtr, length);
        }

        private UnmanagedMemoryManager _memoryManager;
        public Memory<T> Memory => _memoryManager.Memory;
        public Span<T> GetSpan() => _memoryManager.GetSpan();
        public int Length => _memoryManager.Length;
        public unsafe T* DataPtr => _memoryManager.DataPtr;
        public void Dispose()
        {
            ((IDisposable)_memoryManager).Dispose();
        }

        public static implicit operator Memory<T>(UnmanagedMemory<T> @this)
        {
            return @this.Memory;
        }

        public static implicit operator ReadOnlyMemory<T>(UnmanagedMemory<T> @this)
        {
            return @this.Memory;
        }
    }
}
