
namespace System.Buffers
{
    public interface BufferWriter<T> where T : unmanaged
    {
        Memory<T> GetMemory(int sizeHint = 0);
        Span<T> GetSpan(int sizeHint = 0);
        void Advance(int count);
        void Write(T value);
        void Write(ReadOnlySpan<T> value);
        unsafe void Write(T* pValue, int count);//TODO? Remove
    }
}
