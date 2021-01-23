
namespace System.Extensions.Http
{
    using System.Diagnostics;
    using System.Collections;
    using System.Collections.Generic;
    [DebuggerDisplay("Count = {Count}")]
    [DebuggerTypeProxy(typeof(DebugView))]
    public class CookieParams : ICookieParams
    {
        private static int _Capacity = 12;
        private static int _MaxCapacity = 512;
        private KeyValueCollection<string, string> _cookieCollection;
        public CookieParams()
        {
            _cookieCollection = new KeyValueCollection<string, string>(_Capacity, StringComparer.Ordinal);
        }
        public CookieParams(int capacity)
        {
            _cookieCollection = new KeyValueCollection<string, string>(capacity, StringComparer.Ordinal);
        }
        public KeyValuePair<string, string> this[int index]
        {
            get => _cookieCollection[index];
            set 
            {
                if (value.Key == null || value.Value == null)
                    throw new ArgumentException(nameof(value));

                _cookieCollection[index] = value;
            }
        }
        public string this[string name]
        {
            get => _cookieCollection[name];
            set 
            {
                if (name == null)
                    throw new ArgumentNullException(nameof(name));
                if (value == null)
                    throw new ArgumentNullException(nameof(value));

                _cookieCollection[name] = value;

                if (_cookieCollection.Count > _MaxCapacity)
                    throw new InvalidOperationException(nameof(_MaxCapacity));
            }
        }
        public void Add(string name, string value)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            _cookieCollection.Add(name, value);

            if (_cookieCollection.Count > _MaxCapacity)
                throw new InvalidOperationException(nameof(_MaxCapacity));
        }
        public int Remove(string name)
        {
            return _cookieCollection.Remove(name);
        }
        public void Clear()=> _cookieCollection.Clear();
        public int Count => _cookieCollection.Count;
        public bool Contains(string name)
        {
            return _cookieCollection.ContainsKey(name);
        }
        public bool TryGetValue(string name, out string value)
        {
            return _cookieCollection.TryGetValue(name, out value);
        }
        public string[] GetValues(string name)
        {
            return _cookieCollection.GetValues(name);
        }
        public override string ToString()
        {
            if (_cookieCollection.Count == 0)
                return string.Empty;

            var sb = StringExtensions.ThreadRent(out var disposable);
            try
            {
                sb.Write(_cookieCollection[0].Key);
                sb.Write('=');
                sb.Write(_cookieCollection[0].Value);
                for (int i = 1; i < _cookieCollection.Count; i++)
                {
                    var item = _cookieCollection[i];
                    sb.Write("; ");
                    sb.Write(item.Key);
                    sb.Write('=');
                    sb.Write(item.Value);
                }
                return sb.ToString();
            }
            finally
            {
                disposable.Dispose();
            }
        }
        public Enumerator GetEnumerator() => new Enumerator(_cookieCollection);
        public struct Enumerator
        {
            internal Enumerator(KeyValueCollection<string,string> cookieCollection)
            {
                _cookieCollection = cookieCollection;
                _index = 0;
                _current = default;
            }

            private int _index;
            private KeyValuePair<string, string> _current;
            private KeyValueCollection<string, string> _cookieCollection;
            public KeyValuePair<string, string> Current => _current;
            public bool MoveNext()
            {
                if (_index < _cookieCollection.Count)
                {
                    _current = _cookieCollection[_index];
                    _index++;
                    return true;
                }
                return false;
            }
        }
        #region IEnumerable
        IEnumerator<KeyValuePair<string, string>> IEnumerable<KeyValuePair<string, string>>.GetEnumerator()
        {
            return _cookieCollection.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return _cookieCollection.GetEnumerator();
        }
        #endregion
        private class DebugView
        {
            public DebugView(CookieParams cookieParams)
            {
                _cookieParams = cookieParams;
            }
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private CookieParams _cookieParams;
            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            internal KeyValuePair<string, string>[] Items
            {
                get
                {
                    var items = new KeyValuePair<string, string>[_cookieParams.Count];
                    for (int i = 0; i < items.Length; i++)
                    {
                        items[i] = _cookieParams[i];
                    }
                    return items;
                }
            }
        }
    }
}
