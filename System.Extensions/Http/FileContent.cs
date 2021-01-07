
namespace System.Extensions.Http
{
    using System.IO;
    using System.Diagnostics;
    using System.Threading.Tasks;
    [DebuggerDisplay("{_file.FullName}")]
    public class FileContent : IHttpContent, IDisposable
    {
        private FileInfo _file;
        private FileStream _fs;
        public FileContent(string fileName)
           : this(new FileInfo(fileName))
        { }
        public FileContent(FileInfo file)
        {
            if (file == null)
                throw new ArgumentNullException(nameof(file));
            if (!file.Exists)
                throw new FileNotFoundException(file.FullName);

            _file = file;
        }
        public bool Rewind()
        {
            if (_file == null)
                throw new ObjectDisposedException(nameof(FileContent));

            if (_fs != null)
                _fs.Position = 0;
            return true;
        }
        public long ComputeLength() => Length;
        public FileInfo File
        {
            get
            {
                if (_file == null)
                    throw new ObjectDisposedException(nameof(FileContent));

                return _file;
            }
        }
        public long Available
        {
            get
            {
                if (_file == null)
                    throw new ObjectDisposedException(nameof(FileContent));

                if (_fs == null)
                    return _file.Length;

                return _fs.Length - _fs.Position;
            }
        }
        public long Length
        {
            get
            {
                if (_file == null)
                    throw new ObjectDisposedException(nameof(FileContent));

                return _file.Length;
            }
        }
        public int Read(Span<byte> buffer)
        {
            if (_file == null)
                throw new ObjectDisposedException(nameof(FileContent));

            if (_fs == null)
            {
                _fs = new FileStream(_file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.None);
                if (_fs.Length != _file.Length)
                {
                    _fs.Close();
                    _fs = null;
                    throw new InvalidDataException(nameof(FileContent));
                }
            }
            return _fs.Read(buffer);
        }
        public int Read(byte[] buffer, int offset, int count)
        {
            if (_file == null)
                throw new ObjectDisposedException(nameof(FileContent));

            if (_fs == null)
            {
                _fs = new FileStream(_file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.None);
                if (_fs.Length != _file.Length)
                {
                    _fs.Close();
                    _fs = null;
                    throw new InvalidDataException(nameof(FileContent));
                }
            }
            return _fs.Read(buffer,offset,count);
        }
        public ValueTask<int> ReadAsync(Memory<byte> buffer)
        {
            if (_file == null)
                throw new ObjectDisposedException(nameof(FileContent));

            if (_fs == null)
            {
                _fs = new FileStream(_file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.Asynchronous);
                if (_fs.Length != _file.Length)
                {
                    _fs.Close();
                    _fs = null;
                    throw new InvalidDataException(nameof(FileContent));
                }
            }
            return _fs.ReadAsync(buffer);
        }
        public ValueTask<int> ReadAsync(byte[] buffer, int offset, int count)
        {
            if (_file == null)
                throw new ObjectDisposedException(nameof(FileContent));

            if (_fs == null)
            {
                _fs = new FileStream(_file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.Asynchronous);
                if (_fs.Length != _file.Length)
                {
                    _fs.Close();
                    _fs = null;
                    throw new InvalidDataException(nameof(FileContent));
                }
            }
            return new ValueTask<int>(_fs.ReadAsync(buffer, offset, count));
        }
        public void Dispose()
        {
            if (_file == null)
                return;

            _file = null;

            if (_fs != null)
            {
                _fs.Dispose();
                _fs = null;
            }
        }
    }
}
