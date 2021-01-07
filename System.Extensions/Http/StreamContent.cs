
namespace System.Extensions.Http
{
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;
    [DebuggerDisplay("Length = {Length}, StreamContent")]
    public class StreamContent : IHttpContent
    {
        private long _available = -1;
        private long _length = -1;
        private Stream _stream;
        public StreamContent(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            _stream = stream;
            try
            {
                _length = stream.Length;
                _available = _length - stream.Position;
            }
            catch
            { }
        }
        public Stream Stream => _stream;
        public long Available => _available;
        public long Length => _length;
        public bool Rewind()
        {
            if (!_stream.CanSeek)
                return false;

            try
            {
                _stream.Position = 0;
                _available = _length;
                return true;
            }
            catch
            {
                return false;
            }
        }
        public long ComputeLength() => _length;
        public int Read(Span<byte> buffer)
        {
            if (_available == 0)
                return 0;
            if (buffer.IsEmpty)
                return 0;

            var result = _stream.Read(buffer);
            if (result == 0)
            {
                _available = 0;
                return 0;
            }

            if (_available > 0)
                _available -= result;

            return result;
        }
        public int Read(byte[] buffer, int offset, int count)
        {
            return Read(buffer.AsSpan(offset, count));
        }
        public async ValueTask<int> ReadAsync(Memory<byte> buffer)
        {
            if (_available == 0)
                return 0;
            if (buffer.IsEmpty)
                return 0;

            var result = await _stream.ReadAsync(buffer);
            if (result == 0)
            {
                _available = 0;
                return 0;
            }

            if (_available > 0)
                _available -= result;

            return result;
        }
        public ValueTask<int> ReadAsync(byte[] buffer, int offset, int count)
        {
            return ReadAsync(buffer.AsMemory(offset, count));
        }
    }
}
