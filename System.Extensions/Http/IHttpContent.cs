
namespace System.Extensions.Http
{
    using System.Threading.Tasks;
    public interface IHttpContent
    {
        long Available { get; }
        long Length { get; }
        bool Rewind();
        long ComputeLength();
        int Read(Span<byte> buffer);
        int Read(byte[] buffer, int offset, int count);
        ValueTask<int> ReadAsync(Memory<byte> buffer);
        ValueTask<int> ReadAsync(byte[] buffer, int offset, int count);
    }
}
