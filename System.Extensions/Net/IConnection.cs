
namespace System.Extensions.Net
{
    using System.Net;
    using System.Threading.Tasks;
    public interface IConnection
    {
        PropertyCollection<IConnection> Properties { get; }
        bool Connected { get; }
        ISecurity Security { get; }
        EndPoint LocalEndPoint { get; }
        EndPoint RemoteEndPoint { get; }
        int Receive(Span<byte> buffer);
        int Receive(byte[] buffer, int offset, int count);
        ValueTask<int> ReceiveAsync(Memory<byte> buffer);
        ValueTask<int> ReceiveAsync(byte[] buffer, int offset, int count);
        void Send(ReadOnlySpan<byte> buffer);
        void Send(byte[] buffer, int offset, int count);
        Task SendAsync(ReadOnlyMemory<byte> buffer);//TODO?? ValueTask
        Task SendAsync(byte[] buffer, int offset, int count);
        void SendFile(string fileName);
        Task SendFileAsync(string fileName);
        void Close();
    }
}
