
namespace System.Extensions.Http
{
    using System.Diagnostics;
    using System.Collections;
    using System.Collections.Generic;
    [DebuggerDisplay("Count = {Count}")]
    [DebuggerTypeProxy(typeof(DebugView))]
    public class QueryParams: IQueryParams
    {
        private static int _Capacity = 12;
        private static int _MaxCapacity = 1000;
        private KeyValueCollection<string, string> _queryCollection;
        public QueryParams()
        {
            _queryCollection = new KeyValueCollection<string, string>(_Capacity, StringComparer.Ordinal);
        }
        public QueryParams(int capacity)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            _queryCollection = new KeyValueCollection<string, string>(capacity, StringComparer.Ordinal);
        }
        public KeyValuePair<string, string> this[int index]
        {
            get => _queryCollection[index];
            set 
            {
                if (value.Key == null || value.Value == null)
                    throw new ArgumentException(nameof(value));

                _queryCollection[index] = value;
            }
        }
        public string this[string name]
        {
            get => _queryCollection[name];
            set 
            {
                if (name == null)
                    throw new ArgumentNullException(nameof(name));
                if (value == null)
                    throw new ArgumentNullException(nameof(value));

                _queryCollection[name] = value;

                if (_queryCollection.Count > _MaxCapacity)
                    throw new InvalidOperationException(nameof(_MaxCapacity));
            }
        }
        public void Add(string name,string value)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            _queryCollection.Add(name, value);

            if (_queryCollection.Count > _MaxCapacity)
                throw new InvalidOperationException(nameof(_MaxCapacity));
        }
        public int Remove(string name)
        {
            return _queryCollection.Remove(name);
        }
        public int Count => _queryCollection.Count;
        public void Clear()=> _queryCollection.Clear();
        public bool Contains(string name)
        {
            return _queryCollection.ContainsKey(name);
        }
        public bool TryGetValue(string name, out string value)
        {
            return _queryCollection.TryGetValue(name, out value);
        }
        public string[] GetValues(string name)
        {
            return _queryCollection.GetValues(name);
        }
        public override string ToString()
        {
            if (_queryCollection.Count == 0)
                return string.Empty;

            var sb = StringExtensions.ThreadRent(out var disposable);
            try
            {
                sb.Write(_queryCollection[0].Key);
                sb.Write('=');
                sb.Write(_queryCollection[0].Value);
                for (int i = 1; i < _queryCollection.Count; i++)
                {
                    var item = _queryCollection[i];
                    sb.Write('&');
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
        public Enumerator GetEnumerator() => new Enumerator(_queryCollection);
        public struct Enumerator
        {
            internal Enumerator(KeyValueCollection<string, string> queryCollection)
            {
                _queryCollection = queryCollection;
                _index = 0;
                _current = default;
            }

            private int _index;
            private KeyValuePair<string, string> _current;
            private KeyValueCollection<string, string> _queryCollection;
            public KeyValuePair<string, string> Current => _current;
            public bool MoveNext()
            {
                if (_index < _queryCollection.Count)
                {
                    _current = _queryCollection[_index];
                    _index++;
                    return true;
                }
                return false;
            }
        }
        #region IEnumerable
        IEnumerator<KeyValuePair<string, string>> IEnumerable<KeyValuePair<string, string>>.GetEnumerator()
        {
            return _queryCollection.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return _queryCollection.GetEnumerator();
        }
        #endregion
        private class DebugView
        {
            public DebugView(QueryParams queryParams)
            {
                _queryParams = queryParams;
            }
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private QueryParams _queryParams;
            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            internal KeyValuePair<string, string>[] Items
            {
                get
                {
                    var items = new KeyValuePair<string, string>[_queryParams.Count];
                    for (int i = 0; i < items.Length; i++)
                    {
                        items[i] = _queryParams[i];
                    }
                    return items;
                }
            }
        }
    }
}
