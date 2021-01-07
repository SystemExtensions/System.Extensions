
namespace System.Collections.Concurrent
{
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Collections.Generic;
    public class DelayQueue<TKey, TValue>//(for TEST) TODO?
    {
        public DelayQueue(TimeSpan delayTime, Action<TKey, TValue> handler)
            : this(Environment.ProcessorCount * 4, 60, EqualityComparer<TKey>.Default, delayTime, handler)
        { }
        public DelayQueue(int concurrencyLevel, int capacity, TimeSpan delayTime, Action<TKey, TValue> handler)
           : this(concurrencyLevel, capacity, EqualityComparer<TKey>.Default, delayTime, handler)
        { }
        public DelayQueue(int concurrencyLevel, int capacity, IEqualityComparer<TKey> comparer, TimeSpan delayTime, Action<TKey, TValue> handler)
        {
            if (concurrencyLevel <= 0)
                throw new ArgumentOutOfRangeException(nameof(concurrencyLevel));
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            if (comparer == null)
                throw new ArgumentNullException(nameof(comparer));
            if (delayTime <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(delayTime));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

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
            _handler = (key, value) => {
                ThreadPool.QueueUserWorkItem((_) => {
                    try
                    {
                        handler(key, value);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"DelayQueue:{ex.Message}");
                    }
                });
            };
            _delayTime = delayTime;
            for (int i = 0; i < count; i++)
            {
                _locks[i] = new SpinLock();
                var timer = new Timer(TimerCallback, i, Timeout.Infinite, Timeout.Infinite);
                _storage[i] = new Storage(timer, capacity, comparer);
            }
        }
        public DelayQueue(TimeSpan delayTime, Func<TKey, TValue, Task> handler)
            : this(Environment.ProcessorCount * 4, 60, EqualityComparer<TKey>.Default, delayTime, handler)
        { }
        public DelayQueue(int concurrencyLevel, int capacity, TimeSpan delayTime, Func<TKey, TValue, Task> handler)
           : this(concurrencyLevel, capacity, EqualityComparer<TKey>.Default, delayTime, handler)
        { }
        public DelayQueue(int concurrencyLevel, int capacity, IEqualityComparer<TKey> comparer, TimeSpan delayTime, Func<TKey, TValue, Task> handler)
        {
            if (concurrencyLevel <= 0)
                throw new ArgumentOutOfRangeException(nameof(concurrencyLevel));
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            if (comparer == null)
                throw new ArgumentNullException(nameof(comparer));
            if (delayTime <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(delayTime));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

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
            _handler = (key, value) => {
                ThreadPool.QueueUserWorkItem(async (_) =>
                {
                    try
                    {
                        await handler(key, value);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"DelayQueue:{ex.Message}");
                    }
                });
            };
            _delayTime = delayTime;
            for (int i = 0; i < count; i++)
            {
                _locks[i] = new SpinLock();
                var timer = new Timer(TimerCallback, i, Timeout.Infinite, Timeout.Infinite);
                _storage[i] = new Storage(timer, capacity, comparer);
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
                public int NodePrevious;
                public int NodeNext;
            }
            public Storage(Timer timer, int capacity, IEqualityComparer<TKey> comparer)
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
                _timer = timer;
                _interval = TimeSpan.FromSeconds(1);
            }
            private int _free;
            private int _count;
            private int[] _buckets;
            private Entry[] _entries;
            private int _tail;
            private Timer _timer;
            private TimeSpan _interval;
            private IEqualityComparer<TKey> _comparer;
            private void Resize(TKey key, int hashCode, TValue value, DateTimeOffset expire)
            {
                Debug.Assert(_free >= _entries.Length);
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

                var buckets = new int[bucketSize];
                var entries = new Entry[entrySize];
                entries[_free].Key = key;
                entries[_free].HashCode = hashCode;
                entries[_free].Value = value;
                entries[_free].Expire = expire;
                entries[_free].NodePrevious = _tail;
                Debug.Assert(entries[_free].NodeNext == 0);
                _entries[_tail].NodeNext = _free;
                _tail = _free;
                buckets[hashCode % bucketSize] = _free;
                Array.Copy(_entries, 1, entries, 1, _count);
                entries[0].NodeNext = _entries[0].NodeNext;
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
                Debug.Assert(entry.NodePrevious == 0);
                Debug.Assert(entry.NodeNext == 0);
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
                entry.NodePrevious = _tail;
                _entries[_tail].NodeNext = index;
                _tail = index;
                return index;
            }
            public bool TryAdd(TKey key, int hashCode, TValue value, DateTimeOffset expire)
            {
                ref var bucket = ref _buckets[hashCode % _buckets.Length];
                if (bucket == 0)
                {
                    if (_tail == 0)
                    {
                        var now = DateTimeOffset.Now;
                        _timer.Change(expire > now ? expire - now : TimeSpan.Zero, Timeout.InfiniteTimeSpan);
                    }
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
                            return false;
                        }
                        if (entry.Next == 0)
                        {
                            if (_tail == 0)
                            {
                                var now = DateTimeOffset.Now;
                                _timer.Change(expire > now ? expire - now : TimeSpan.Zero, Timeout.InfiniteTimeSpan);
                            }
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
            public bool TryRemove(TKey key, int hashCode, out TKey _key, out TValue value, out DateTimeOffset expire)
            {
                ref var bucket = ref _buckets[hashCode % _buckets.Length];
                for (int i = bucket, previous = 0; i > 0;)
                {
                    ref var entry = ref _entries[i];
                    if (entry.HashCode == hashCode && _comparer.Equals(entry.Key, key))
                    {
                        _key = entry.Key;
                        value = entry.Value;
                        expire = entry.Expire;
                        if (previous == 0)
                        {
                            Debug.Assert(i == bucket);
                            bucket = entry.Next;
                        }
                        else
                        {
                            _entries[previous].Next = entry.Next;
                        }
                        entry.HashCode = -1;
                        entry.Key = default;
                        entry.Value = default;
                        entry.Next = _free;

                        if (entry.NodeNext == 0)
                            _tail = entry.NodePrevious;
                        else
                            _entries[entry.NodeNext].NodePrevious = entry.NodePrevious;
                        _entries[entry.NodePrevious].NodeNext = entry.NodeNext;
                        entry.NodePrevious = 0;
                        entry.NodeNext = 0;

                        _free = i;
                        return true;
                    }
                    previous = i;
                    i = entry.Next;
                }
                _key = default;
                value = default;
                expire = default;
                return false;
            }
            public bool TryRemove(DateTimeOffset now, out TKey key, out TValue value)
            {
                var index = _entries[0].NodeNext;
                if (index == 0)
                {
                    Debug.Assert(_tail == 0);
                    key = default;
                    value = default;
                    return false;
                }
                ref var entry = ref _entries[index];
                if (now < entry.Expire)
                {
                    var dueTime = entry.Expire - now;
                    _timer.Change(dueTime > _interval ? dueTime : _interval, Timeout.InfiniteTimeSpan);
                    key = default;
                    value = default;
                    return false;
                }
                key = entry.Key;
                value = entry.Value;
                ref var bucket = ref _buckets[entry.HashCode % _buckets.Length];
                for (int i = bucket, previous = 0; i > 0;)
                {
                    if (i == index)
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
                        entry.HashCode = -1;
                        entry.Key = default;
                        entry.Value = default;
                        entry.Next = _free;

                        if (entry.NodeNext == 0)
                            _tail = entry.NodePrevious;
                        else
                            _entries[entry.NodeNext].NodePrevious = entry.NodePrevious;
                        _entries[entry.NodePrevious].NodeNext = entry.NodeNext;
                        entry.NodePrevious = 0;
                        entry.NodeNext = 0;

                        _free = i;
                        return true;
                    }
                    previous = i;
                    i = _entries[i].Next;
                }
                throw new InvalidOperationException(nameof(TryRemove));
            }
        }
        private int _mask;
        private SpinLock[] _locks;
        private IEqualityComparer<TKey> _comparer;
        private Action<TKey, TValue> _handler;
        private TimeSpan _delayTime;
        private Storage[] _storage;
        private void TimerCallback(object state)
        {
            Debug.WriteLine($"DelayQueue:{nameof(TimerCallback)}");
            var index = (int)state;
            for (; ; )
            {
                var now = DateTimeOffset.Now;
                var lockTaken = false;
                try
                {
                    _locks[index].Enter(ref lockTaken);
                    if (_storage[index].TryRemove(now, out var key, out var value))
                    {
                        _handler(key, value);
                        continue;
                    }
                    return;
                }
                finally
                {
                    Debug.Assert(lockTaken);
                    _locks[index].Exit(false);
                    //_locks[index].Exit();??
                }
            }

        }
        public bool TryAdd(TKey key, TValue value)
        {
            return TryAdd(key, value, DateTimeOffset.Now);
        }
        public bool TryAdd(TKey key, TValue value, DateTimeOffset now)
        {
            var hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;
            var index = hashCode & _mask;
            var lockTaken = false;
            try
            {
                var delayTime = now.Add(_delayTime);
                _locks[index].Enter(ref lockTaken);
                return _storage[index].TryAdd(key, hashCode, value, delayTime);
            }
            finally
            {
                Debug.Assert(lockTaken);
                _locks[index].Exit(false);
            }
        }
        public bool TryRemove(TKey key)
        {
            var hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;
            var index = hashCode & _mask;
            var lockTaken = false;
            var now = DateTimeOffset.Now;
            var result = false;
            TKey _key; TValue value; DateTimeOffset expire;
            try
            {
                _locks[index].Enter(ref lockTaken);
                result = _storage[index].TryRemove(key, hashCode, out _key, out value, out expire);
            }
            finally
            {
                Debug.Assert(lockTaken);
                _locks[index].Exit(false);
            }
            if (result)
            {
                if (expire < now)
                {
                    _handler(_key, value);
                    return false;
                }
                return true;
            }
            return false;
        }
        public bool TryRemove(TKey key, out TValue value, out DateTimeOffset expire)
        {
            var hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;
            var index = hashCode & _mask;
            var lockTaken = false;
            try
            {
                _locks[index].Enter(ref lockTaken);
                return _storage[index].TryRemove(key, hashCode, out _, out value, out expire);
            }
            finally
            {
                Debug.Assert(lockTaken);
                _locks[index].Exit(false);
            }
        }
    }
}
