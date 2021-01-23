
namespace System.Collections.Generic
{
    using System.Diagnostics;
    [DebuggerDisplay("Count = {Count}")]
    [DebuggerTypeProxy(typeof(KeyValueCollectionDebugView<,>))]
    public class KeyValueCollection<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    {
        private static readonly int[] _Capacities = {
            6, 8, 12, 16, 24, 32, 48, 64, 96, 128, 192, 256, 384, 512, 768, 1024, 1536, 2048, 3072, 4096,
            6144, 8192, 12288, 16384, 24576, 32768, 49152, 65536, 98304, 131072, 196608, 262144, 393216, 524288,
            786432, 1048576, 1572864, 2097152, 3145728, 4194304, 6291456, 8388608, 12582912, 16777216, 25165824,
            33554432, 50331648, 67108864, 100663296, 134217728, 201326592, 268435456, 402653184, 536870912, 805306368, 1073741824 };
        private struct Entry
        {
            public int Next;
            public int HashCode;
            public TKey Key;
            public TValue Value;
        }

        private int _mask;
        private int[] _buckets;
        private Entry[] _entries;
        private int _count;
        private IEqualityComparer<TKey> _comparer;
        private void Resize(TKey key, int hashCode, TValue value)
        {
            Debug.Assert(_count >= _entries.Length);
            var bucketSize = 0;
            var entrySize = _entries.Length;
            for (int i = 0; i < _Capacities.Length; i += 2)
            {
                if (_Capacities[i] > entrySize)
                {
                    entrySize = _Capacities[i];
                    bucketSize = _Capacities[i + 1];
                    break;
                }
            }
            if (bucketSize == 0)
                throw new OverflowException(nameof(Resize));

            var mask = bucketSize - 1;
            var buckets = new int[bucketSize];
            var entries = new Entry[entrySize];
            Array.Copy(_entries, 0, entries, 0, _count);
            entries[_count].Key = key;
            entries[_count].Value = value;
            entries[_count].HashCode = hashCode;
            entries[_count].Next = -1;
            for (int i = _count; i >= 0; i--)
            {
                ref var bucket = ref buckets[entries[i].HashCode & mask];
                entries[i].Next = bucket - 1;
                bucket = i + 1;
            }
            _mask = mask;
            _buckets = buckets;
            _entries = entries;
            _count += 1;
        }
        public KeyValueCollection()
            : this(6, EqualityComparer<TKey>.Default)
        { }
        public KeyValueCollection(int capacity)
            : this(capacity, EqualityComparer<TKey>.Default)
        { }
        public KeyValueCollection(int capacity, IEqualityComparer<TKey> comparer)
        {
            if (capacity <= 0)//TODO? 0=Array.Empty
                throw new ArgumentOutOfRangeException(nameof(capacity));
            if (comparer == null)
                throw new ArgumentNullException(nameof(comparer));

            var bucketSize = 0;
            var entrySize = 0;
            for (int i = 0; i < _Capacities.Length; i += 2)
            {
                if (_Capacities[i] >= capacity)
                {
                    entrySize = _Capacities[i];
                    bucketSize = _Capacities[i + 1];
                    break;
                }
            }
            if (bucketSize == 0)
                throw new OverflowException(nameof(capacity));

            _mask = bucketSize - 1;
            _buckets = new int[bucketSize];
            _entries = new Entry[entrySize];
            _comparer = comparer;
        }
        public KeyValuePair<TKey, TValue> this[int index]
        {
            get
            {
                if (index < 0 || index >= _count)
                    throw new IndexOutOfRangeException(nameof(index));

                return new KeyValuePair<TKey, TValue>(_entries[index].Key, _entries[index].Value);
            }
            set
            {
                if (index < 0 || index >= _count)
                    throw new IndexOutOfRangeException(nameof(index));

                ref var rawBucket = ref _buckets[_entries[index].HashCode & _mask];
                var hashCode = _comparer.GetHashCode(value.Key) & 0x7FFFFFFF;
                ref var bucket = ref _buckets[hashCode & _mask];
                if (bucket == rawBucket)
                {
                    _entries[index].HashCode = hashCode;
                    _entries[index].Key = value.Key;
                    _entries[index].Value = value.Value;
                }
                else
                {
                    var temp = rawBucket - 1;
                    if (temp == index)
                    {
                        rawBucket = _entries[index].Next + 1;
                    }
                    else
                    {
                        while (_entries[temp].Next != index)
                        {
                            temp = _entries[temp].Next;
                        }
                        _entries[temp].Next = _entries[index].Next;
                    }

                    temp = bucket - 1;
                    if (temp == -1)
                    {
                        bucket = index + 1;
                        _entries[index].HashCode = hashCode;
                        _entries[index].Next = -1;
                        _entries[index].Key = value.Key;
                        _entries[index].Value = value.Value;
                    }
                    else
                    {
                        while (_entries[temp].Next != -1 && _entries[temp].Next < index)
                        {
                            temp = _entries[temp].Next;
                        }
                        _entries[index].Next = _entries[temp].Next;
                        _entries[index].HashCode = hashCode;
                        _entries[index].Key = value.Key;
                        _entries[index].Value = value.Value;
                        _entries[temp].Next = index;
                    }
                }
            }
        }
        public TValue this[TKey key]
        {
            get
            {
                if (TryGetValue(key, out var value))
                    return value;
                throw new KeyNotFoundException(key.ToString());
            }
            set
            {
                var hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;
                ref var bucket = ref _buckets[hashCode & _mask];
                var index = -1;
                if (bucket != 0)
                {
                    var i = bucket - 1;
                    for (; ; )
                    {
                        if (_entries[i].HashCode == hashCode && _comparer.Equals(_entries[i].Key, key))
                        {
                            _entries[i].Value = value;
                            i = _entries[i].Next;
                            while (i != -1)
                            {
                                var next = _entries[i].Next;
                                if (_entries[i].HashCode == hashCode && _comparer.Equals(_entries[i].Key, key))
                                {
                                    RemoveAt(i);
                                    i = next == -1 ? -1 : next - 1;
                                    continue;
                                }
                                i = next;
                            }
                            return;
                        }
                        if (_entries[i].Next == -1)
                        {
                            index = i;
                            break;
                        }
                        else
                        {
                            i = _entries[i].Next;
                        }
                    }
                }

                if (_count < _entries.Length)
                {
                    _entries[_count].Key = key;
                    _entries[_count].Value = value;
                    _entries[_count].HashCode = hashCode;
                    _entries[_count].Next = -1;
                    if (index == -1)
                    {
                        Debug.Assert(bucket == 0);
                        _count += 1;
                        bucket = _count;
                        return;
                    }
                    else
                    {
                        _entries[index].Next = _count;
                        _count += 1;
                        return;
                    }
                }
                else
                {
                    Resize(key, hashCode, value);
                }
            }
        }
        public IEqualityComparer<TKey> Comparer => _comparer;
        public int Count => _count;
        public bool ContainsKey(TKey key)
        {
            var hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;
            var i = _buckets[hashCode & _mask] - 1;
            while (i != -1)
            {
                ref var entry = ref _entries[i];
                if (entry.HashCode == hashCode && _comparer.Equals(entry.Key, key))
                {
                    return true;
                }
                i = entry.Next;
            }
            return false;
        }
        public void Add(TKey key, TValue value)
        {
            var hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;
            if (_count == _entries.Length)
            {
                Resize(key, hashCode, value);
                return;
            }
            _entries[_count].Key = key;
            _entries[_count].Value = value;
            _entries[_count].HashCode = hashCode;
            _entries[_count].Next = -1;
            ref var bucket = ref _buckets[hashCode & _mask];
            if (bucket == 0)
            {
                _count += 1;
                bucket = _count;
                return;
            }
            else
            {
                var i = bucket - 1;
                while (_entries[i].Next != -1)
                {
                    i = _entries[i].Next;
                }
                _entries[i].Next = _count;
                _count += 1;
                return;
            }
        }
        public bool TryGetValue(TKey key, out TValue value)
        {
            var hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;
            var i = _buckets[hashCode & _mask] - 1;
            while (i != -1)
            {
                ref var entry = ref _entries[i];
                if (entry.HashCode == hashCode && _comparer.Equals(entry.Key, key))
                {
                    value = entry.Value;
                    return true;
                }
                i = entry.Next;
            }
            value = default;
            return false;
        }
        public TValue[] GetValues(TKey key)
        {
            var hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;
            var i = _buckets[hashCode & _mask] - 1;
            Span<int> indexs = stackalloc int[6];
            var count = 0;
            while (i != -1)
            {
                if (_entries[i].HashCode == hashCode && _comparer.Equals(_entries[i].Key, key))
                {
                    if (count == indexs.Length)
                    {
                        var newIndexs = new int[indexs.Length * 2];
                        indexs.CopyTo(newIndexs);
                        indexs = newIndexs;
                    }
                    indexs[count++] = i;
                }
                i = _entries[i].Next;
            }
            if (count == 0)
                return Array.Empty<TValue>();

            var values = new TValue[count];
            for (int j = 0; j < count; j++)
            {
                values[j] = _entries[indexs[j]].Value;
            }
            return values;
        }
        public bool RemoveAt(int index)
        {
            if (index < 0 || index >= _count)
                return false;

            //修正移除项索引
            {
                var key = _entries[index].Key;
                var hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;
                ref var bucket = ref _buckets[hashCode & _mask];
                var head = bucket - 1;
                Debug.Assert(head != -1);
                if (head == index)
                {
                    bucket = _entries[index].Next + 1;
                    _entries[index] = default;
                }
                else
                {
                    while (_entries[head].Next != index)
                    {
                        head = _entries[head].Next;
                    }
                    _entries[head].Next = _entries[index].Next;
                    _entries[index] = default;
                }
            }
            //剩余元素前移
            for (int i = index + 1; i < _count; i++)
            {
                var key = _entries[i].Key;
                var hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;
                ref var bucket = ref _buckets[hashCode & _mask];
                var head = bucket - 1;
                Debug.Assert(head != -1);
                if (head == i)
                {
                    bucket = i;
                    _entries[i - 1] = _entries[i];
                }
                else
                {
                    while (_entries[head].Next != i)
                    {
                        head = _entries[head].Next;
                    }
                    _entries[head].Next = i - 1;
                    _entries[i - 1] = _entries[i];
                }
            }
            _count -= 1;
            _entries[_count] = default;
            return true;
        }
        public int Remove(TKey key)
        {
            var result = 0;
            var hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;
            var i = _buckets[hashCode & _mask] - 1;
            while (i != -1)
            {
                var next = _entries[i].Next;
                if (_entries[i].HashCode == hashCode && _comparer.Equals(_entries[i].Key, key))
                {
                    RemoveAt(i);
                    result += 1;
                    i = next == -1 ? -1 : next - 1;
                    continue;
                }
                i = next;
            }
            return result;
        }
        public void Clear()
        {
            Array.Clear(_buckets, 0, _buckets.Length);
            Array.Clear(_entries, 0, _count);
            _count = 0;
        }
        public Enumerator GetEnumerator() => new Enumerator(this);
        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
        {
            return GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>
        {
            private KeyValueCollection<TKey, TValue> _collection;
            private int _index;
            private KeyValuePair<TKey, TValue> _current;
            internal Enumerator(KeyValueCollection<TKey, TValue> collection)
            {
                _collection = collection;
                _index = 0;
                _current = default;
            }
            public KeyValuePair<TKey, TValue> Current => _current;
            object IEnumerator.Current => _current;
            public bool MoveNext()
            {
                if (_index == _collection._count)
                {
                    _current = default;
                    return false;
                }
                _current = new KeyValuePair<TKey, TValue>(_collection._entries[_index].Key, _collection._entries[_index].Value);
                _index += 1;
                return true;
            }
            public void Reset()
            {
                _index = 0;
                _current = default;
            }
            public void Dispose()
            {
                _collection = null;
            }
        }
    }
    internal class KeyValueCollectionDebugView<TKey, TValue>
    {
        public KeyValueCollectionDebugView(KeyValueCollection<TKey, TValue> collection)
        {
            _collection = collection;
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private KeyValueCollection<TKey, TValue> _collection;
        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public KeyValuePair<TKey, TValue>[] Items
        {
            get
            {
                var items = new KeyValuePair<TKey, TValue>[_collection.Count];
                for (int i = 0; i < items.Length; i++)
                {
                    items[i] = _collection[i];
                }
                return items;
            }
        }
    }
}
