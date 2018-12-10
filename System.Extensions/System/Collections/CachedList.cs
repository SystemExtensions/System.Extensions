
namespace System.Collections
{
    using System.Reflection;
    using System.Collections.Generic;
    public class CachedList<T> : IList<T>
    {
        private static readonly object _Sync = new object();
        private static Action<List<T>, T[]> _setArray;
        private static Func<List<T>, T[]> _getArray;
        private T[] _array;
        private List<T> _list;
        public CachedList(int cachedLength)
        {
            if (cachedLength <= 0)
                throw new ArgumentOutOfRangeException(nameof(cachedLength));

            _list = new List<T>(cachedLength);
            if (_getArray == null)
            {
                lock (_Sync)
                {
                    if (_getArray == null)
                    {
                        var arrayField = typeof(List<T>).GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance);
                        _getArray = arrayField.CompileGetter<Func<List<T>, T[]>>();
                        _setArray = arrayField.CompileSetter<Action<List<T>, T[]>>();
                    }
                }
            }
            _array = _getArray(_list);
        }
        public T this[int index]
        {
            get
            {
                return _list[index];
            }
            set
            {
                _list[index] = value;
            }
        }
        public int Count => _list.Count;
        public void Clear()
        {
            _list.Clear();

            var array = _getArray(_list);
            if (ReferenceEquals(array, _array))
                return;

            _setArray(_list, _array);
            Array.Clear(_array, 0, _array.Length);
        }
        public bool Contains(T item) => _list.Contains(item);
        public void Add(T item) => _list.Add(item);
        public int IndexOf(T item) => _list.IndexOf(item);
        public void Insert(int index, T item) => _list.Insert(index,item);
        public bool Remove(T item) => _list.Remove(item);
        public void RemoveAt(int index) => _list.RemoveAt(index);
        public int RemoveAll(Predicate<T> match) => _list.RemoveAll(match);
        public void CopyTo(T[] array, int arrayIndex) => _list.CopyTo(array, arrayIndex);
        public List<T>.Enumerator GetEnumerator() => _list.GetEnumerator();
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => _list.GetEnumerator();
        bool ICollection<T>.IsReadOnly => false;
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public static implicit operator List<T>(CachedList<T> @this) => @this._list;
    }
}
