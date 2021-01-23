
namespace System.Extensions.Http
{
    using System.IO;
    using System.Net.Mime;
    using System.Diagnostics;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    [DebuggerDisplay("Count = {Count}")]
    [DebuggerTypeProxy(typeof(DebugView))]
    public class FormFileParams : IFormFileParams
    {
        private static int _Capacity = 6;
        private static int _MaxCapacity = 10;
        private KeyValueCollection<string, IFormFile> _fileCollection;
        public FormFileParams()
        {
            _fileCollection = new KeyValueCollection<string, IFormFile>(_Capacity, StringComparer.Ordinal);
        }
        public FormFileParams(int capacity)
        {
            _fileCollection = new KeyValueCollection<string, IFormFile>(capacity, StringComparer.Ordinal);
        }
        public KeyValuePair<string, IFormFile> this[int index]
        {
            get => _fileCollection[index];
            set 
            {
                if (value.Key == null || value.Value == null)
                    throw new ArgumentException(nameof(value));

                _fileCollection[index] = value;
            }
        }
        public IFormFile this[string name]
        {
            get => _fileCollection[name];
            set 
            {
                if (name == null)
                    throw new ArgumentNullException(nameof(name));
                if (value == null)
                    throw new ArgumentNullException(nameof(value));

                _fileCollection[name] = value;

                if (_fileCollection.Count > _MaxCapacity)
                    throw new InvalidOperationException(nameof(_MaxCapacity));
            }
        }
        public void Add(string name, string filePath)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));
            
            var file = new FileInfo(filePath);
            if (!file.Exists)
                throw new FileNotFoundException(nameof(filePath));

            var contentType = MimeTypes.Default.TryGetValue(file.Name, out var mimeType) ? mimeType : "application/octet-stream";

            _fileCollection.Add(name, new FormFile(file.Name, contentType, file));

            if (_fileCollection.Count > _MaxCapacity)
                throw new InvalidOperationException(nameof(_MaxCapacity));
        }
        public void Add(string name, string fileName, string contentType, FileInfo file)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (fileName == null)
                throw new ArgumentNullException(nameof(fileName));

            if (file != null && !file.Exists)
                throw new FileNotFoundException(file.FullName);

            _fileCollection.Add(name, new FormFile(fileName, contentType, file));

            if (_fileCollection.Count > _MaxCapacity)
                throw new InvalidOperationException(nameof(_MaxCapacity));
        }
        //TODO?? Stream byte[] ReadOnlySequence<byte>
        public void Add(string name, IFormFile file)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (file == null)
                throw new ArgumentNullException(nameof(file));

            _fileCollection.Add(name, file);

            if (_fileCollection.Count > _MaxCapacity)
                throw new InvalidOperationException(nameof(_MaxCapacity));
        }
        public int Remove(string name)
        {
            return _fileCollection.Remove(name);
        }
        public void Clear() => _fileCollection.Clear();
        public int Count => _fileCollection.Count;
        public bool Contains(string name)
        {
            return _fileCollection.ContainsKey(name);
        }
        public bool TryGetValue(string name, out IFormFile value)
        {
            return _fileCollection.TryGetValue(name, out value);
        }
        public IFormFile[] GetValues(string name)
        {
            return _fileCollection.GetValues(name);
        }
        public override string ToString()
        {
            if (_fileCollection.Count == 0)
                return string.Empty;

            var sb = StringExtensions.ThreadRent(out var disposable);
            try
            {
                sb.Write(_fileCollection[0].Key);
                sb.Write('=');
                sb.Write(_fileCollection[0].Value.FileName);
                for (int i = 1; i < _fileCollection.Count; i++)
                {
                    var item = _fileCollection[i];
                    sb.Write('&');
                    sb.Write(item.Key);
                    sb.Write('=');
                    sb.Write(item.Value.FileName);
                }
                return sb.ToString();
            }
            finally
            {
                disposable.Dispose();
            }
        }
        public Enumerator GetEnumerator() => new Enumerator(_fileCollection);
        public struct Enumerator
        {
            internal Enumerator(KeyValueCollection<string, IFormFile> fileCollection)
            {
                _fileCollection = fileCollection;
                _index = 0;
                _current = default;
            }

            private int _index;
            private KeyValuePair<string, IFormFile> _current;
            private KeyValueCollection<string, IFormFile> _fileCollection;
            public KeyValuePair<string, IFormFile> Current => _current;
            public bool MoveNext()
            {
                if (_index < _fileCollection.Count)
                {
                    _current = _fileCollection[_index];
                    _index++;
                    return true;
                }
                return false;
            }
        }
        #region IEnumerable
        IEnumerator<KeyValuePair<string, IFormFile>> IEnumerable<KeyValuePair<string, IFormFile>>.GetEnumerator()
        {
            return _fileCollection.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return _fileCollection.GetEnumerator();
        }
        #endregion
        private class FormFile : IFormFile
        {
            private FileInfo _file;
            private string _fileName;
            private string _contentType;
            public FormFile(string fileName, string contentType, FileInfo file)
            {
                _fileName = fileName;
                _contentType = contentType;
                _file = file;
            }
            public long Length => _file == null ? 0 : _file.Length;
            public string FileName => _fileName;
            public string ContentType => _contentType;
            public Task SaveAsync(string filePath)
            {
                if (_file == null)//TODO? Create 0 byte
                    throw new InvalidOperationException("No Data");

                File.Copy(_file.FullName, filePath, true);//overwrite
                return Task.CompletedTask;
            }
            public Stream OpenRead()
            {
                if (_file == null)
                    return Stream.Null;

                var fs = new FileStream(_file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.Asynchronous);
                if (fs.Length != _file.Length)
                    throw new InvalidOperationException("fs.Length != _file.Length");

                return fs;
            }
            public override string ToString() => FileName;
        }
        private class DebugView
        {
            public DebugView(FormFileParams formFileParams)
            {
                _formFileParams = formFileParams;
            }
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private FormFileParams _formFileParams;
            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            internal KeyValuePair<string, string>[] Items
            {
                get
                {
                    var items = new KeyValuePair<string, string>[_formFileParams.Count];
                    for (int i = 0; i < items.Length; i++)
                    {
                        var formFileParam = _formFileParams[i];
                        var formFile = formFileParam.Value;
                        items[i] = new KeyValuePair<string, string>(formFileParam.Key, "(" + formFile.FileName + "," + formFile.ContentType + "," + formFile.Length + ")");
                    }
                    return items;
                }
            }
        }
    }
}
