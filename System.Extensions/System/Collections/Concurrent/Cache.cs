
namespace System.Collections.Concurrent
{
    using System.Diagnostics;
    using System.Collections.Generic;
    using System.Threading;
    [DebuggerDisplay("Count = {Count}")]
    [DebuggerTypeProxy(typeof(CacheDebugView<,>))]
    public class Cache<TKey, TValue>
    {
        public Cache()
            : this(Environment.ProcessorCount * 8, 60, EqualityComparer<TKey>.Default)
        { }
        public Cache(int concurrencyLevel, int capacity)
           : this(concurrencyLevel, capacity, EqualityComparer<TKey>.Default)
        { }
        public Cache(int concurrencyLevel, int capacity, IEqualityComparer<TKey> comparer)
        {
            if (concurrencyLevel <= 0)
                throw new ArgumentOutOfRangeException(nameof(concurrencyLevel));
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            if (comparer == null)
                throw new ArgumentNullException(nameof(comparer));

            var count = concurrencyLevel - 1;
            count |= count >> 1;
            count |= count >> 2;
            count |= count >> 4;
            count |= count >> 8;
            count |= count >> 16;
            count += 1;
            _mask = count - 1;
            _locks = new SpinLock[count];
            _storage = new Storage[count];
            _comparer = comparer;
            for (int i = 0; i < count; i++)
            {
                _locks[i] = new SpinLock();
                _storage[i] = new Storage(capacity, comparer);
            }
        }
        private class Storage
        {
            private static readonly int[] _Capacities = {
                13, 17, 28, 37, 60, 79, 123, 163, 249, 331, 505, 673, 1021, 1361, 2047, 2729,4104, 5471,
                8212, 10949, 16434, 21911, 32890, 43853, 65790, 87719, 131586, 175447,263175, 350899,
                526365, 701819,1052731, 1403641, 2105478, 2807303, 4210993, 5614657, 8421999, 11229331,
                16844004, 22458671, 33688036, 44917381, 67376083, 89834777, 134752168, 179669557, 269504379,
                359339171, 539008777, 718678369, 1078017556, 1437356741, 1610612736, 2147483647 };
            public struct Entry
            {
                public int Next;
                public int HashCode;//-1=empty
                public TKey Key;
                public TValue Value;
                public DateTimeOffset Expire;
            }
            public Storage(int capacity, IEqualityComparer<TKey> comparer)
            {
                var bucketSize = 0;
                var entrySize = 0;
                for (int i = 0; i < _Capacities.Length; i += 2)
                {
                    if (_Capacities[i] > capacity)
                    {
                        entrySize = _Capacities[i];
                        bucketSize = _Capacities[i + 1];
                        break;
                    }
                }
                if (bucketSize == 0)
                    throw new OverflowException(nameof(capacity));

                _free = 1;
                _buckets = new int[bucketSize];
                _entries = new Entry[entrySize];//[0]Empty
                _comparer = comparer;
            }
            private int _free;
            private int _count;
            private int[] _buckets;
            private Entry[] _entries;
            private IEqualityComparer<TKey> _comparer;
            private void Resize(TKey key, int hashCode, TValue value, DateTimeOffset expire)
            {
                Debug.Assert(_free >= _entries.Length);
                var entrySize = _entries.Length;
                var bucketSize = 0;
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

                var buckets = new int[bucketSize];
                var entries = new Entry[entrySize];
                entries[_free].Key = key;
                entries[_free].HashCode = hashCode;
                entries[_free].Value = value;
                entries[_free].Expire = expire;
                buckets[hashCode % bucketSize] = _free;
                Array.Copy(_entries, 1, entries, 1, _count);
                //TODO? Collect
                for (int i = _count; i > 0; i--)
                {
                    Debug.Assert(_entries[i].HashCode >= 0);
                    ref var bucket = ref buckets[_entries[i].HashCode % bucketSize];
                    entries[i].Next = bucket;
                    bucket = i;
                }
                _buckets = buckets;
                _entries = entries;
                _free += 1;
                _count += 1;
            }
            private int Add(TKey key, int hashCode, TValue value, DateTimeOffset expire)
            {
                Debug.Assert(_free < _entries.Length);
                var index = _free;
                ref var entry = ref _entries[index];
                if (entry.Next == 0)
                {
                    _free += 1;
                    _count += 1;
                }
                else
                {
                    _free = entry.Next;
                    entry.Next = 0;
                }
                entry.Key = key;
                entry.HashCode = hashCode;
                entry.Value = value;
                entry.Expire = expire;
                return index;
            }
            public bool TryAdd(TKey key, int hashCode, TValue value, DateTimeOffset expire)
            {
                ref var bucket = ref _buckets[hashCode % _buckets.Length];
                if (bucket == 0)
                {
                    if (_free == _entries.Length)
                        Resize(key, hashCode, value, expire);
                    else
                        bucket = Add(key, hashCode, value, expire);
                    return true;
                }
                else
                {
                    for (int i = bucket; ;)
                    {
                        ref var entry = ref _entries[i];
                        if (entry.HashCode == hashCode && _comparer.Equals(entry.Key, key))
                        {
                            if (entry.Expire < DateTimeOffset.Now)
                            {
                                entry.Value = value;
                                entry.Expire = expire;
                                return true;
                            }
                            return false;
                        }
                        if (entry.Next == 0)
                        {
                            if (_free == _entries.Length)
                                Resize(key, hashCode, value, expire);
                            else
                                entry.Next = Add(key, hashCode, value, expire);
                            return true;
                        }
                        else
                        {
                            i = entry.Next;
                        }
                    }
                }
            }
            public bool TryAdd(TKey key, int hashCode, Func<TValue> valueFactory, DateTimeOffset expire)
            {
                ref var bucket = ref _buckets[hashCode % _buckets.Length];
                if (bucket == 0)
                {
                    var value = valueFactory();
                    if (_free == _entries.Length)
                        Resize(key, hashCode, value, expire);
                    else
                        bucket = Add(key, hashCode, value, expire);
                    return true;
                }
                else
                {
                    for (int i = bucket; ;)
                    {
                        ref var entry = ref _entries[i];
                        if (entry.HashCode == hashCode && _comparer.Equals(entry.Key, key))
                        {
                            if (entry.Expire < DateTimeOffset.Now)
                            {
                                var value = valueFactory();
                                entry.Value = value;
                                entry.Expire = expire;
                                return true;
                            }
                            return false;
                        }
                        if (entry.Next == 0)
                        {
                            var value = valueFactory();
                            if (_free == _entries.Length)
                                Resize(key, hashCode, value, expire);
                            else
                                entry.Next = Add(key, hashCode, value, expire);
                            return true;
                        }
                        else
                        {
                            i = entry.Next;
                        }
                    }
                }
            }
            public bool TryAdd(TKey key, int hashCode, Func<(TValue, DateTimeOffset)> factory)
            {
                ref var bucket = ref _buckets[hashCode % _buckets.Length];
                if (bucket == 0)
                {
                    (var value, var expire) = factory();
                    if (_free == _entries.Length)
                        Resize(key, hashCode, value, expire);
                    else
                        bucket = Add(key, hashCode, value, expire);
                    return true;
                }
                else
                {
                    for (int i = bucket; ;)
                    {
                        ref var entry = ref _entries[i];
                        if (entry.HashCode == hashCode && _comparer.Equals(entry.Key, key))
                        {
                            if (entry.Expire < DateTimeOffset.Now)
                            {
                                (var value, var expire) = factory();
                                entry.Value = value;
                                entry.Expire = expire;
                                return true;
                            }
                            return false;
                        }
                        if (entry.Next == 0)
                        {
                            (var value, var expire) = factory();
                            if (_free == _entries.Length)
                                Resize(key, hashCode, value, expire);
                            else
                                entry.Next = Add(key, hashCode, value, expire);
                            return true;
                        }
                        else
                        {
                            i = entry.Next;
                        }
                    }
                }
            }
            public bool TryUpdate(TKey key, int hashCode, TValue newValue, out TValue value)
            {
                ref var bucket = ref _buckets[hashCode % _buckets.Length];
                for (int i = bucket, previous = 0; i > 0;)
                {
                    ref var entry = ref _entries[i];
                    if (entry.HashCode == hashCode && _comparer.Equals(entry.Key, key))
                    {
                        if (entry.Expire < DateTimeOffset.Now)
                        {
                            if (previous == 0)
                            {
                                Debug.Assert(i == bucket);
                                bucket = entry.Next;
                            }
                            else
                            {
                                _entries[previous].Next = entry.Next;
                            }
                            entry.Key = default;
                            entry.HashCode = -1;
                            entry.Value = default;
                            entry.Next = _free;
                            _free = i;
                            value = default;
                            return false;
                        }
                        value = entry.Value;
                        entry.Value = newValue;
                        return true;
                    }
                    previous = i;
                    i = entry.Next;
                }
                value = default;
                return false;
            }
            public bool TryUpdate(TKey key, int hashCode, Func<TValue, TValue> newValueFactory, out TValue value)
            {
                ref var bucket = ref _buckets[hashCode % _buckets.Length];
                for (int i = bucket, previous = 0; i > 0;)
                {
                    ref var entry = ref _entries[i];
                    if (entry.HashCode == hashCode && _comparer.Equals(entry.Key, key))
                    {
                        if (entry.Expire < DateTimeOffset.Now)
                        {
                            if (previous == 0)
                            {
                                Debug.Assert(i == bucket);
                                bucket = entry.Next;
                            }
                            else
                            {
                                _entries[previous].Next = entry.Next;
                            }
                            entry.Key = default;
                            entry.HashCode = -1;
                            entry.Value = default;
                            entry.Next = _free;
                            _free = i;
                            value = default;
                            return false;
                        }
                        value = entry.Value;
                        entry.Value = newValueFactory(value);
                        return true;
                    }
                    previous = i;
                    i = entry.Next;
                }
                value = default;
                return false;
            }
            public bool TryUpdate(TKey key, int hashCode, DateTimeOffset expire, out TValue value)
            {
                ref var bucket = ref _buckets[hashCode % _buckets.Length];
                for (int i = bucket, previous = 0; i > 0;)
                {
                    ref var entry = ref _entries[i];
                    if (entry.HashCode == hashCode && _comparer.Equals(entry.Key, key))
                    {
                        if (entry.Expire < DateTimeOffset.Now)
                        {
                            if (previous == 0)
                            {
                                Debug.Assert(i == bucket);
                                bucket = entry.Next;
                            }
                            else
                            {
                                _entries[previous].Next = entry.Next;
                            }
                            entry.Key = default;
                            entry.HashCode = -1;
                            entry.Value = default;
                            entry.Next = _free;
                            _free = i;
                            value = default;
                            return false;
                        }
                        value = entry.Value;
                        entry.Expire = expire;
                        return true;
                    }
                    previous = i;
                    i = entry.Next;
                }
                value = default;
                return false;
            }
            public bool TryUpdate(TKey key, int hashCode, TValue newValue, DateTimeOffset expire, out TValue value)
            {
                ref var bucket = ref _buckets[hashCode % _buckets.Length];
                for (int i = bucket, previous = 0; i > 0;)
                {
                    ref var entry = ref _entries[i];
                    if (entry.HashCode == hashCode && _comparer.Equals(entry.Key, key))
                    {
                        if (entry.Expire < DateTimeOffset.Now)
                        {
                            if (previous == 0)
                            {
                                Debug.Assert(i == bucket);
                                bucket = entry.Next;
                            }
                            else
                            {
                                _entries[previous].Next = entry.Next;
                            }
                            entry.Key = default;
                            entry.HashCode = -1;
                            entry.Value = default;
                            entry.Next = _free;
                            _free = i;
                            value = default;
                            return false;
                        }
                        value = entry.Value;
                        entry.Value = newValue;
                        entry.Expire = expire;
                        return true;
                    }
                    previous = i;
                    i = entry.Next;
                }
                value = default;
                return false;
            }
            public bool TryUpdate(TKey key, int hashCode, Func<TValue, TValue> newValueFactory, DateTimeOffset expire, out TValue value)
            {
                ref var bucket = ref _buckets[hashCode % _buckets.Length];
                for (int i = bucket, previous = 0; i > 0;)
                {
                    ref var entry = ref _entries[i];
                    if (entry.HashCode == hashCode && _comparer.Equals(entry.Key, key))
                    {
                        if (entry.Expire < DateTimeOffset.Now)
                        {
                            if (previous == 0)
                            {
                                Debug.Assert(i == bucket);
                                bucket = entry.Next;
                            }
                            else
                            {
                                _entries[previous].Next = entry.Next;
                            }
                            entry.Key = default;
                            entry.HashCode = -1;
                            entry.Value = default;
                            entry.Next = _free;
                            _free = i;
                            value = default;
                            return false;
                        }
                        value = entry.Value;
                        entry.Value = newValueFactory(value);
                        entry.Expire = expire;
                        return true;
                    }
                    previous = i;
                    i = entry.Next;
                }
                value = default;
                return false;
            }
            public bool TryUpdate(TKey key, int hashCode, Func<TValue, DateTimeOffset, (TValue, DateTimeOffset)> newFactory, out TValue value)
            {
                ref var bucket = ref _buckets[hashCode % _buckets.Length];
                for (int i = bucket, previous = 0; i > 0;)
                {
                    ref var entry = ref _entries[i];
                    if (entry.HashCode == hashCode && _comparer.Equals(entry.Key, key))
                    {
                        if (entry.Expire < DateTimeOffset.Now)
                        {
                            if (previous == 0)
                            {
                                Debug.Assert(i == bucket);
                                bucket = entry.Next;
                            }
                            else
                            {
                                _entries[previous].Next = entry.Next;
                            }
                            entry.Key = default;
                            entry.HashCode = -1;
                            entry.Value = default;
                            entry.Next = _free;
                            _free = i;
                            value = default;
                            return false;
                        }
                        value = entry.Value;
                        (var newValue, var expire) = newFactory(value, entry.Expire);
                        entry.Value = newValue;
                        entry.Expire = expire;
                        return true;
                    }
                    previous = i;
                    i = entry.Next;
                }
                value = default;
                return false;
            }
            public bool TryRemove(TKey key, int hashCode, out TValue value)
            {
                ref var bucket = ref _buckets[hashCode % _buckets.Length];
                for (int i = bucket, previous = 0; i > 0;)
                {
                    ref var entry = ref _entries[i];
                    if (entry.HashCode == hashCode && _comparer.Equals(entry.Key, key))
                    {
                        if (previous == 0)
                        {
                            Debug.Assert(i == bucket);
                            bucket = entry.Next;
                        }
                        else
                        {
                            _entries[previous].Next = entry.Next;
                        }
                        if (entry.Expire < DateTimeOffset.Now)
                        {
                            value = default;
                            entry.Key = default;
                            entry.HashCode = -1;
                            entry.Value = default;
                            entry.Next = _free;
                            _free = i;
                            return false;
                        }
                        else
                        {
                            value = entry.Value;
                            entry.Key = default;
                            entry.HashCode = -1;
                            entry.Value = default;
                            entry.Next = _free;
                            _free = i;
                            return true;
                        }
                    }
                    previous = i;
                    i = entry.Next;
                }
                value = default;
                return false;
            }
            public bool TryGetValue(TKey key, int hashCode, out TValue value)
            {
                ref var bucket = ref _buckets[hashCode % _buckets.Length];
                for (int i = bucket, previous = 0; i > 0;)
                {
                    ref var entry = ref _entries[i];
                    if (entry.HashCode == hashCode && _comparer.Equals(entry.Key, key))
                    {
                        if (entry.Expire < DateTimeOffset.Now)
                        {
                            if (previous == 0)
                            {
                                Debug.Assert(i == bucket);
                                bucket = entry.Next;
                            }
                            else
                            {
                                _entries[previous].Next = entry.Next;
                            }
                            entry.Key = default;
                            entry.HashCode = -1;
                            entry.Value = default;
                            entry.Next = _free;
                            _free = i;
                            value = default;
                            return false;
                        }
                        value = entry.Value;
                        return true;
                    }
                    previous = i;
                    i = entry.Next;
                }
                value = default;
                return false;
            }
            public TValue AddOrUpdate(TKey key, int hashCode, TValue value, DateTimeOffset expire)
            {
                ref var bucket = ref _buckets[hashCode % _buckets.Length];
                if (bucket == 0)
                {
                    if (_free == _entries.Length)
                        Resize(key, hashCode, value, expire);
                    else
                        bucket = Add(key, hashCode, value, expire);
                    return value;
                }
                else
                {
                    for (int i = bucket; ;)
                    {
                        ref var entry = ref _entries[i];
                        if (entry.HashCode == hashCode && _comparer.Equals(entry.Key, key))
                        {
                            entry.Value = value;
                            entry.Expire = expire;
                            return value;
                        }
                        if (entry.Next == 0)
                        {
                            if (_free == _entries.Length)
                                Resize(key, hashCode, value, expire);
                            else
                                entry.Next = Add(key, hashCode, value, expire);
                            return value;
                        }
                        else
                        {
                            i = entry.Next;
                        }
                    }
                }
            }
            public TValue AddOrUpdate(TKey key, int hashCode, TValue value, DateTimeOffset expire, Func<TValue, TValue> newValueFactory)
            {
                ref var bucket = ref _buckets[hashCode % _buckets.Length];
                if (bucket == 0)
                {
                    if (_free == _entries.Length)
                        Resize(key, hashCode, value, expire);
                    else
                        bucket = Add(key, hashCode, value, expire);
                    return value;
                }
                else
                {
                    for (int i = bucket; ;)
                    {
                        ref var entry = ref _entries[i];
                        if (entry.HashCode == hashCode && _comparer.Equals(entry.Key, key))
                        {
                            if (entry.Expire < DateTimeOffset.Now)
                            {
                                entry.Value = value;
                                entry.Expire = expire;
                                return value;
                            }
                            else
                            {
                                var newValue = newValueFactory(entry.Value);
                                entry.Value = newValue;
                                //entry.Expire = expire;
                                return newValue;
                            }
                        }
                        if (entry.Next == 0)
                        {
                            if (_free == _entries.Length)
                                Resize(key, hashCode, value, expire);
                            else
                                entry.Next = Add(key, hashCode, value, expire);
                            return value;
                        }
                        else
                        {
                            i = entry.Next;
                        }
                    }
                }
            }
            public TValue AddOrUpdate(TKey key, int hashCode, Func<(TValue, DateTimeOffset)> factory, Func<TValue, DateTimeOffset, (TValue, DateTimeOffset)> newFactory)
            {
                ref var bucket = ref _buckets[hashCode % _buckets.Length];
                if (bucket == 0)
                {
                    (var value, var expire) = factory();
                    if (_free == _entries.Length)
                        Resize(key, hashCode, value, expire);
                    else
                        bucket = Add(key, hashCode, value, expire);
                    return value;
                }
                else
                {
                    for (int i = bucket; ;)
                    {
                        ref var entry = ref _entries[i];
                        if (entry.HashCode == hashCode && _comparer.Equals(entry.Key, key))
                        {
                            if (entry.Expire < DateTimeOffset.Now)
                            {
                                (var value, var expire) = factory();
                                entry.Value = value;
                                entry.Expire = expire;
                                return value;
                            }
                            else
                            {
                                (var value, var expire) = newFactory(entry.Value, entry.Expire);
                                entry.Value = value;
                                entry.Expire = expire;
                                return value;
                            }
                        }
                        if (entry.Next == 0)
                        {
                            (var value, var expire) = factory();
                            if (_free == _entries.Length)
                                Resize(key, hashCode, value, expire);
                            else
                                entry.Next = Add(key, hashCode, value, expire);
                            return value;
                        }
                        else
                        {
                            i = entry.Next;
                        }
                    }
                }
            }
            public TValue GetOrAdd(TKey key, int hashCode, TValue value, DateTimeOffset expire)
            {
                ref var bucket = ref _buckets[hashCode % _buckets.Length];
                if (bucket == 0)
                {
                    if (_free == _entries.Length)
                        Resize(key, hashCode, value, expire);
                    else
                        bucket = Add(key, hashCode, value, expire);
                    return value;
                }
                else
                {
                    for (int i = bucket; ;)
                    {
                        ref var entry = ref _entries[i];
                        if (entry.HashCode == hashCode && _comparer.Equals(entry.Key, key))
                        {
                            if (entry.Expire < DateTimeOffset.Now)
                            {
                                entry.Value = value;
                                entry.Expire = expire;
                                return value;
                            }
                            return entry.Value;
                        }
                        if (entry.Next == 0)
                        {
                            if (_free == _entries.Length)
                                Resize(key, hashCode, value, expire);
                            else
                                entry.Next = Add(key, hashCode, value, expire);
                            return value;
                        }
                        else
                        {
                            i = entry.Next;
                        }
                    }
                }
            }
            public TValue GetOrAdd(TKey key, int hashCode, Func<TValue> valueFactory, DateTimeOffset expire)
            {
                ref var bucket = ref _buckets[hashCode % _buckets.Length];
                if (bucket == 0)
                {
                    var value = valueFactory();
                    if (_free == _entries.Length)
                        Resize(key, hashCode, value, expire);
                    else
                        bucket = Add(key, hashCode, value, expire);
                    return value;
                }
                else
                {
                    for (int i = bucket; ;)
                    {
                        ref var entry = ref _entries[i];
                        if (entry.HashCode == hashCode && _comparer.Equals(entry.Key, key))
                        {
                            if (entry.Expire < DateTimeOffset.Now)
                            {
                                var value = valueFactory();
                                entry.Value = value;
                                entry.Expire = expire;
                                return value;
                            }
                            return entry.Value;
                        }
                        if (entry.Next == 0)
                        {
                            var value = valueFactory();
                            if (_free == _entries.Length)
                                Resize(key, hashCode, value, expire);
                            else
                                entry.Next = Add(key, hashCode, value, expire);
                            return value;
                        }
                        else
                        {
                            i = entry.Next;
                        }
                    }
                }
            }
            public TValue GetOrAdd(TKey key, int hashCode, Func<(TValue, DateTimeOffset)> factory)
            {
                ref var bucket = ref _buckets[hashCode % _buckets.Length];
                if (bucket == 0)
                {
                    (var value, var expire) = factory();
                    if (_free == _entries.Length)
                        Resize(key, hashCode, value, expire);
                    else
                        bucket = Add(key, hashCode, value, expire);
                    return value;
                }
                else
                {
                    for (int i = bucket; ;)
                    {
                        ref var entry = ref _entries[i];
                        if (entry.HashCode == hashCode && _comparer.Equals(entry.Key, key))
                        {
                            if (entry.Expire < DateTimeOffset.Now)
                            {
                                (var value, var expire) = factory();
                                entry.Value = value;
                                entry.Expire = expire;
                                return value;
                            }
                            return entry.Value;
                        }
                        if (entry.Next == 0)
                        {
                            (var value, var expire) = factory();
                            if (_free == _entries.Length)
                                Resize(key, hashCode, value, expire);
                            else
                                entry.Next = Add(key, hashCode, value, expire);
                            return value;
                        }
                        else
                        {
                            i = entry.Next;
                        }
                    }
                }
            }
            public void ForEach(Action<TKey, TValue, DateTimeOffset> action)
            {
                var now = DateTimeOffset.Now;
                for (int i = 1; i <= _count; i++)
                {
                    ref var entry = ref _entries[i];
                    if (entry.HashCode == -1 || entry.Expire < now)
                        continue;

                    action(entry.Key, entry.Value, entry.Expire);
                }
            }
            public void Clear()
            {
                Array.Clear(_buckets, 0, _buckets.Length);
                Array.Clear(_entries, 1, _count);
                _free = 1;
                _count = 0;
            }
            public void Collect()
            {
                var now = DateTimeOffset.Now;
                for (int index = 0; index < _buckets.Length; index++)
                {
                    ref var bucket = ref _buckets[index];
                    if (bucket == 0)
                        continue;

                    for (int i = bucket, previous = 0; i > 0;)
                    {
                        ref var entry = ref _entries[i];
                        if (entry.Expire < now)
                        {
                            if (previous == 0)
                            {
                                Debug.Assert(i == bucket);
                                bucket = entry.Next;
                            }
                            else
                            {
                                _entries[previous].Next = entry.Next;
                            }
                            var next = entry.Next;
                            entry.Key = default;
                            entry.HashCode = -1;
                            entry.Value = default;
                            entry.Next = _free;
                            _free = i;
                            i = next;
                        }
                        else
                        {
                            previous = i;
                            i = entry.Next;
                        }
                    }
                }
            }
        }
        private int _mask;
        private SpinLock[] _locks;//TODO? [(padding),locks,(padding)]
        private IEqualityComparer<TKey> _comparer;
        private Storage[] _storage;
        private int Count => this.Count();
        //TODO?
        //public event Action<TKey, TValue, DateTimeOffset> OnAdd;
        //public event Action<TKey, TValue, DateTimeOffset> OnCollect;
        public bool TryAdd(TKey key, TValue value, DateTimeOffset expire)
        {
            var hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;
            var index = hashCode & _mask;
            var lockTaken = false;
            try
            {
                _locks[index].Enter(ref lockTaken);
                return _storage[index].TryAdd(key, hashCode, value, expire);
            }
            finally
            {
                Debug.Assert(lockTaken);
                _locks[index].Exit(false);
            }
        }
        public bool TryAdd(TKey key, Func<TValue> valueFactory, DateTimeOffset expire)
        {
            var hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;
            var index = hashCode & _mask;
            var lockTaken = false;
            try
            {
                _locks[index].Enter(ref lockTaken);
                return _storage[index].TryAdd(key, hashCode, valueFactory, expire);
            }
            finally
            {
                Debug.Assert(lockTaken);
                _locks[index].Exit(false);
            }
        }
        public bool TryAdd(TKey key, Func<(TValue, DateTimeOffset)> factory)
        {
            var hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;
            var index = hashCode & _mask;
            var lockTaken = false;
            try
            {
                _locks[index].Enter(ref lockTaken);
                return _storage[index].TryAdd(key, hashCode, factory);
            }
            finally
            {
                Debug.Assert(lockTaken);
                _locks[index].Exit(false);
            }
        }
        public bool TryUpdate(TKey key, TValue newValue, out TValue value)
        {
            var hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;
            var index = hashCode & _mask;
            var lockTaken = false;
            try
            {
                _locks[index].Enter(ref lockTaken);
                return _storage[index].TryUpdate(key, hashCode, newValue, out value);
            }
            finally
            {
                Debug.Assert(lockTaken);
                _locks[index].Exit(false);
            }
        }
        public bool TryUpdate(TKey key, Func<TValue, TValue> newValueFactory, out TValue value)
        {
            var hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;
            var index = hashCode & _mask;
            var lockTaken = false;
            try
            {
                _locks[index].Enter(ref lockTaken);
                return _storage[index].TryUpdate(key, hashCode, newValueFactory, out value);
            }
            finally
            {
                Debug.Assert(lockTaken);
                _locks[index].Exit(false);
            }
        }
        public bool TryUpdate(TKey key, DateTimeOffset expire, out TValue value)
        {
            var hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;
            var index = hashCode & _mask;
            var lockTaken = false;
            try
            {
                _locks[index].Enter(ref lockTaken);
                return _storage[index].TryUpdate(key, hashCode, expire, out value);
            }
            finally
            {
                Debug.Assert(lockTaken);
                _locks[index].Exit(false);
            }
        }
        public bool TryUpdate(TKey key, TValue newValue, DateTimeOffset expire, out TValue value)
        {
            var hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;
            var index = hashCode & _mask;
            var lockTaken = false;
            try
            {
                _locks[index].Enter(ref lockTaken);
                return _storage[index].TryUpdate(key, hashCode, newValue, expire, out value);
            }
            finally
            {
                Debug.Assert(lockTaken);
                _locks[index].Exit(false);
            }
        }
        public bool TryUpdate(TKey key, Func<TValue, TValue> newValueFactory, DateTimeOffset expire, out TValue value)
        {
            var hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;
            var index = hashCode & _mask;
            var lockTaken = false;
            try
            {
                _locks[index].Enter(ref lockTaken);
                return _storage[index].TryUpdate(key, hashCode, newValueFactory, expire, out value);
            }
            finally
            {
                Debug.Assert(lockTaken);
                _locks[index].Exit(false);
            }
        }
        public bool TryUpdate(TKey key, Func<TValue, DateTimeOffset, (TValue, DateTimeOffset)> newFactory, out TValue value)
        {
            var hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;
            var index = hashCode & _mask;
            var lockTaken = false;
            try
            {
                _locks[index].Enter(ref lockTaken);
                return _storage[index].TryUpdate(key, hashCode, newFactory, out value);
            }
            finally
            {
                Debug.Assert(lockTaken);
                _locks[index].Exit(false);
            }
        }
        public bool TryRemove(TKey key, out TValue value)
        {
            var hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;
            var index = hashCode & _mask;
            var lockTaken = false;
            try
            {
                _locks[index].Enter(ref lockTaken);
                return _storage[index].TryRemove(key, hashCode, out value);
            }
            finally
            {
                Debug.Assert(lockTaken);
                _locks[index].Exit(false);
            }
        }
        public bool TryGetValue(TKey key, out TValue value)
        {
            var hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;
            var index = hashCode & _mask;
            var lockTaken = false;
            try
            {
                _locks[index].Enter(ref lockTaken);
                return _storage[index].TryGetValue(key, hashCode, out value);
            }
            finally
            {
                Debug.Assert(lockTaken);
                _locks[index].Exit(false);
            }
        }
        public TValue AddOrUpdate(TKey key, TValue value, DateTimeOffset expire)
        {
            var hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;
            var index = hashCode & _mask;
            var lockTaken = false;
            try
            {
                _locks[index].Enter(ref lockTaken);
                return _storage[index].AddOrUpdate(key, hashCode, value, expire);
            }
            finally
            {
                Debug.Assert(lockTaken);
                _locks[index].Exit(false);
            }
        }
        public TValue AddOrUpdate(TKey key, TValue value, DateTimeOffset expire, Func<TValue, TValue> newValueFactory)
        {
            var hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;
            var index = hashCode & _mask;
            var lockTaken = false;
            try
            {
                _locks[index].Enter(ref lockTaken);
                return _storage[index].AddOrUpdate(key, hashCode, value, expire, newValueFactory);
            }
            finally
            {
                Debug.Assert(lockTaken);
                _locks[index].Exit(false);
            }
        }
        public TValue AddOrUpdate(TKey key, Func<(TValue, DateTimeOffset)> factory, Func<TValue, DateTimeOffset, (TValue, DateTimeOffset)> newFactory)
        {
            var hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;
            var index = hashCode & _mask;
            var lockTaken = false;
            try
            {
                _locks[index].Enter(ref lockTaken);
                return _storage[index].AddOrUpdate(key, hashCode, factory, newFactory);
            }
            finally
            {
                Debug.Assert(lockTaken);
                _locks[index].Exit(false);
            }
        }
        public TValue GetOrAdd(TKey key, TValue value, DateTimeOffset expire)
        {
            var hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;
            var index = hashCode & _mask;
            var lockTaken = false;
            try
            {
                _locks[index].Enter(ref lockTaken);
                return _storage[index].GetOrAdd(key, hashCode, value, expire);
            }
            finally
            {
                Debug.Assert(lockTaken);
                _locks[index].Exit(false);
            }
        }
        public TValue GetOrAdd(TKey key, Func<TValue> valueFactory, DateTimeOffset expire)
        {
            var hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;
            var index = hashCode & _mask;
            var lockTaken = false;
            try
            {
                _locks[index].Enter(ref lockTaken);
                return _storage[index].GetOrAdd(key, hashCode, valueFactory, expire);
            }
            finally
            {
                Debug.Assert(lockTaken);
                _locks[index].Exit(false);
            }
        }
        public TValue GetOrAdd(TKey key, Func<(TValue, DateTimeOffset)> factory)
        {
            var hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;
            var index = hashCode & _mask;
            var lockTaken = false;
            try
            {
                _locks[index].Enter(ref lockTaken);
                return _storage[index].GetOrAdd(key, hashCode, factory);
            }
            finally
            {
                Debug.Assert(lockTaken);
                _locks[index].Exit(false);
            }
        }
        public void ForEach(Action<TKey, TValue, DateTimeOffset> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            var lockCount = 0;
            try
            {
                for (; lockCount < _locks.Length;)
                {
                    var lockTaken = false;
                    _locks[lockCount].Enter(ref lockTaken);
                    Debug.Assert(lockTaken);
                    _storage[lockCount++].ForEach(action);
                }
            }
            finally
            {
                for (int i = 0; i < lockCount; i++)
                {
                    _locks[i].Exit(false);
                }
            }
        }
        public void Clear()
        {
            var lockCount = 0;
            try
            {
                for (; lockCount < _locks.Length;)
                {
                    var lockTaken = false;
                    _locks[lockCount].Enter(ref lockTaken);
                    Debug.Assert(lockTaken);
                    _storage[lockCount++].Clear();
                }
            }
            finally
            {
                for (int i = 0; i < lockCount; i++)
                {
                    _locks[i].Exit(false);
                }
            }
        }
        public void Collect()
        {
            for (int i = 0; i < _locks.Length; i++)
            {
                var lockTaken = false;
                try
                {
                    _locks[i].Enter(ref lockTaken);
                    _storage[i].Collect();
                }
                finally
                {
                    if (lockTaken)
                    {
                        _locks[i].Exit(false);
                    }
                }
            }
        }

        //TODO??
        //public void ForEach(Action<TKey, TValue, DateTimeOffset> action, bool useSync)
        //{
        //    if (action == null)
        //        throw new ArgumentNullException(nameof(action));

        //    if (useSync)
        //    {
        //        var lockCount = 0;
        //        try
        //        {
        //            for (; lockCount < _locks.Length;)
        //            {
        //                var lockTaken = false;
        //                _locks[lockCount].Enter(ref lockTaken);
        //                Debug.Assert(lockTaken);
        //                _storage[lockCount++].ForEach(action);
        //            }
        //        }
        //        finally
        //        {
        //            for (int i = 0; i < lockCount; i++)
        //            {
        //                _locks[i].Exit(false);
        //            }
        //        }
        //    }
        //    else
        //    {
        //        for (int i = 0; i < _locks.Length; i++)
        //        {
        //            var lockTaken = false;
        //            try
        //            {
        //                _locks[i].Enter(ref lockTaken);
        //                _storage[i].ForEach(action);
        //            }
        //            finally
        //            {
        //                if (lockTaken)
        //                {
        //                    _locks[i].Exit(false);
        //                }
        //            }
        //        }
        //    }
        //}
    }
    internal class CacheDebugView<TKey, TValue>
    {
        public CacheDebugView(Cache<TKey, TValue> cache)
        {
            _cache = cache;
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Cache<TKey, TValue> _cache;
        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public (TKey, TValue, DateTimeOffset)[] Items
        {
            get
            {
                var items = new List<(TKey,TValue,DateTimeOffset)>();
                _cache.ForEach((key, value, expire) => {
                    items.Add((key, value, expire));
                });
                return items.ToArray();
            }
        }
    }
}
