
namespace System.Extensions.Http
{
    using System.Diagnostics;
    using System.Collections;
    using System.Collections.Generic;
    [DebuggerDisplay("Count = {Count}")]
    [DebuggerTypeProxy(typeof(DebugView))]
    public class FormParams : IFormParams
    {
        private static int _Capacity = 12;
        private static int _MaxCapacity = 1000;
        private KeyValueCollection<string,string> _formCollection;
        public FormParams()
        {
            _formCollection = new KeyValueCollection<string, string>(_Capacity, StringComparer.Ordinal);
        }
        public FormParams(int capacity)
        {
            _formCollection = new KeyValueCollection<string, string>(capacity, StringComparer.Ordinal);
        }
        public KeyValuePair<string, string> this[int index]
        {
            get => _formCollection[index];
            set 
            {
                if (value.Key == null || value.Value == null)
                    throw new ArgumentException(nameof(value));

                _formCollection[index] = value;
            }
        }
        public string this[string name]
        {
            get => _formCollection[name];
            set 
            {
                if (name == null)
                    throw new ArgumentNullException(nameof(name));
                if (value == null)
                    throw new ArgumentNullException(nameof(value));

                _formCollection[name] = value;

                if (_formCollection.Count > _MaxCapacity)
                    throw new InvalidOperationException(nameof(_MaxCapacity));
            }
        }
        public void Add(string name, string value)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            _formCollection.Add(name, value);

            if (_formCollection.Count > _MaxCapacity)
                throw new InvalidOperationException(nameof(_MaxCapacity));
        }
        public int Remove(string name)
        {
            return _formCollection.Remove(name);
        }
        public void Clear()=> _formCollection.Clear();
        public int Count => _formCollection.Count;
        public bool Contains(string name)
        {
            return _formCollection.ContainsKey(name);
        }
        public bool TryGetValue(string name, out string value)
        {
            return _formCollection.TryGetValue(name, out value);
        }
        public string[] GetValues(string name)
        {
            return _formCollection.GetValues(name);
        }
        public override string ToString()
        {
            if (_formCollection.Count == 0)
                return string.Empty;

            var sb = StringExtensions.ThreadRent(out var disposable);
            try
            {
                sb.Write(_formCollection[0].Key);
                sb.Write('=');
                sb.Write(_formCollection[0].Value);
                for (int i = 1; i < _formCollection.Count; i++)
                {
                    var item = _formCollection[i];
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
        public Enumerator GetEnumerator() => new Enumerator(_formCollection);
        public struct Enumerator
        {
            internal Enumerator(KeyValueCollection<string, string> formCollection)
            {
                _formCollection = formCollection;
                _index = 0;
                _current = default;
            }

            private int _index;
            private KeyValuePair<string, string> _current;
            private KeyValueCollection<string, string> _formCollection;
            public KeyValuePair<string, string> Current => _current;
            public bool MoveNext()
            {
                if (_index < _formCollection.Count)
                {
                    _current = _formCollection[_index];
                    _index++;
                    return true;
                }
                return false;
            }
        }
        #region IEnumerable
        IEnumerator<KeyValuePair<string, string>> IEnumerable<KeyValuePair<string, string>>.GetEnumerator()
        {
            return _formCollection.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return _formCollection.GetEnumerator();
        }
        #endregion
        private class DebugView
        {
            public DebugView(FormParams formParams)
            {
                _formParams = formParams;
            }
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private FormParams _formParams;
            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            internal KeyValuePair<string, string>[] Items
            {
                get
                {
                    var items = new KeyValuePair<string, string>[_formParams.Count];
                    for (int i = 0; i < items.Length; i++)
                    {
                        items[i] = _formParams[i];
                    }
                    return items;
                }
            }
        }
    }
}
