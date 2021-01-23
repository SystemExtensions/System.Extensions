
namespace System.Extensions.Http
{
    using System.Diagnostics;
    using System.Collections;
    using System.Collections.Generic;
    [DebuggerDisplay("Count = {Count}")]
    [DebuggerTypeProxy(typeof(DebugView))]
    public class PathParams:IPathParams
    {
        private static int _Capacity = 6;
        private KeyValueCollection<string, string> _pathCollection;
        public PathParams()
        {
            _pathCollection = new KeyValueCollection<string, string>(_Capacity, StringComparer.Ordinal);
        }
        public PathParams(int capacity)
        {
            _pathCollection = new KeyValueCollection<string, string>(capacity, StringComparer.Ordinal);
        }
        public KeyValuePair<string, string> this[int index]
        {
            get => _pathCollection[index];
            set 
            {
                if (value.Key == null || value.Value == null)
                    throw new ArgumentException(nameof(value));

                _pathCollection[index] = value;
            }
        }
        public string this[string name]
        {
            get => _pathCollection[name];
            set 
            {
                if (name == null)
                    throw new ArgumentNullException(nameof(name));
                if (value == null)
                    throw new ArgumentNullException(nameof(value));

                _pathCollection[name] = value;
            }
        }
        public void Add(string name, string value)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            _pathCollection.Add(name, value);
        }
        public int Remove(string name)
        {
            return _pathCollection.Remove(name);
        }
        public void Clear() => _pathCollection.Clear();
        public int Count => _pathCollection.Count;
        public bool Contains(string name)
        {
            return _pathCollection.ContainsKey(name);
        }
        public bool TryGetValue(string name, out string value)
        {
            return _pathCollection.TryGetValue(name, out value);
        }
        public string[] GetValues(string name)
        {
            return _pathCollection.GetValues(name);
        }
        public override string ToString()
        {
            if (_pathCollection.Count == 0)
                return string.Empty;

            var sb = StringExtensions.ThreadRent(out var disposable);
            try
            {
                sb.Write(_pathCollection[0].Key);
                sb.Write(':');
                sb.Write(_pathCollection[0].Value);
                for (int i = 1; i < _pathCollection.Count; i++)
                {
                    var item = _pathCollection[i];
                    sb.Write(',');
                    sb.Write(item.Key);
                    sb.Write(':');
                    sb.Write(item.Value);
                }
                return sb.ToString();
            }
            finally
            {
                disposable.Dispose();
            }
        }
        public Enumerator GetEnumerator() => new Enumerator(_pathCollection);
        public struct Enumerator
        {
            internal Enumerator(KeyValueCollection<string, string> pathCollection)
            {
                _pathCollection = pathCollection;
                _index = 0;
                _current = default;
            }

            private int _index;
            private KeyValuePair<string, string> _current;
            private KeyValueCollection<string, string> _pathCollection;
            public KeyValuePair<string, string> Current => _current;
            public bool MoveNext()
            {
                if (_index < _pathCollection.Count)
                {
                    _current = _pathCollection[_index];
                    _index++;
                    return true;
                }
                return false;
            }
        }
        #region IEnumerable
        IEnumerator<KeyValuePair<string, string>> IEnumerable<KeyValuePair<string, string>>.GetEnumerator()
        {
            return _pathCollection.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return _pathCollection.GetEnumerator();
        }
        #endregion
        private class DebugView
        {
            public DebugView(PathParams pathParams)
            {
                _pathParams = pathParams;
            }
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private PathParams _pathParams;
            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            internal KeyValuePair<string, string>[] Items
            {
                get
                {
                    var items = new KeyValuePair<string, string>[_pathParams.Count];
                    for (int i = 0; i < items.Length; i++)
                    {
                        items[i] = _pathParams[i];
                    }
                    return items;
                }
            }
        }
    }
}
