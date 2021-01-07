
namespace System.Threading
{
    using System.Diagnostics;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    public class Synchronization<T>
    {
        public Synchronization()
            : this(Environment.ProcessorCount * 8, 60, EqualityComparer<T>.Default)
        { }
        public Synchronization(int concurrencyLevel, int capacity)
             : this(concurrencyLevel, capacity, EqualityComparer<T>.Default)
        { }
        public Synchronization(int concurrencyLevel, int capacity, IEqualityComparer<T> comparer)
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
            public class TaskNode : TaskCompletionSource<object>
            {
                public TaskNode()
                    : base(TaskCreationOptions.RunContinuationsAsynchronously)
                { }
                public TaskNode Next;
            }
            public struct Entry
            {
                public int Next;
                public int HashCode;//-1=empty
                public T Value;
                public TaskNode Head;
                public TaskNode Tail;
            }
            public Storage(int capacity, IEqualityComparer<T> comparer)
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
            private IEqualityComparer<T> _comparer;
            private void Resize(T value, int hashCode)
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
                entries[_free].Value = value;
                entries[_free].HashCode = hashCode;
                buckets[hashCode % bucketSize] = _free;
                Array.Copy(_entries, 1, entries, 1, _count);
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
            private int Add(T value, int hashCode)
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
                entry.Value = value;
                entry.HashCode = hashCode;
                Debug.Assert(entry.Head == null);
                Debug.Assert(entry.Tail == null);
                return index;
            }
            public bool TryWait(T value, int hashCode)
            {
                ref var bucket = ref _buckets[hashCode % _buckets.Length];
                if (bucket == 0)
                {
                    if (_free == _entries.Length)
                        Resize(value, hashCode);
                    else
                        bucket = Add(value, hashCode);
                    return true;
                }
                else
                {
                    for (int i = bucket; ;)
                    {
                        ref var entry = ref _entries[i];
                        if (entry.HashCode == hashCode && _comparer.Equals(entry.Value, value))
                        {
                            return false;
                        }
                        if (entry.Next == 0)
                        {
                            if (_free == _entries.Length)
                                Resize(value, hashCode);
                            else
                                entry.Next = Add(value, hashCode);
                            return true;
                        }
                        else
                        {
                            i = entry.Next;
                        }
                    }
                }
            }
            public Task WaitAsync(T value, int hashCode)
            {
                ref var bucket = ref _buckets[hashCode % _buckets.Length];
                if (bucket == 0)
                {
                    if (_free == _entries.Length)
                        Resize(value, hashCode);
                    else
                        bucket = Add(value, hashCode);
                    return Task.CompletedTask;
                }
                else
                {
                    for (int i = bucket; ;)
                    {
                        ref var entry = ref _entries[i];
                        if (entry.HashCode == hashCode && _comparer.Equals(entry.Value, value))
                        {
                            var node = new TaskNode();
                            if (entry.Head == null)
                            {
                                Debug.Assert(entry.Tail == null);
                                entry.Head = node;
                                entry.Tail = node;
                            }
                            else
                            {
                                entry.Tail.Next = node;
                                entry.Tail = node;
                            }
                            return node.Task;
                        }
                        if (entry.Next == 0)
                        {
                            if (_free == _entries.Length)
                                Resize(value, hashCode);
                            else
                                entry.Next = Add(value, hashCode);
                            return Task.CompletedTask;
                        }
                        else
                        {
                            i = entry.Next;
                        }
                    }
                }
            }
            public bool Realese(T value, int hashCode)
            {
                ref var bucket = ref _buckets[hashCode % _buckets.Length];
                for (int i = bucket, previous = 0; i > 0;)
                {
                    ref var entry = ref _entries[i];
                    if (entry.HashCode == hashCode && _comparer.Equals(entry.Value, value))
                    {
                        if (entry.Head == null)
                        {
                            Debug.Assert(entry.Tail == null);
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
                            entry.Value = default;
                            entry.Next = _free;
                            _free = i;
                            return true;
                        }
                        else
                        {
                            entry.Head.TrySetResult(null);
                            entry.Head = entry.Head.Next;
                            if (entry.Head == null)
                                entry.Tail = null;
                            return true;
                        }
                    }
                    previous = i;
                    i = entry.Next;
                }
                return false;
            }
        }
        private int _mask;
        private SpinLock[] _locks;
        private IEqualityComparer<T> _comparer;
        private Storage[] _storage;
        public bool TryWait(T value)
        {
            var hashCode = _comparer.GetHashCode(value) & 0x7FFFFFFF;
            var index = hashCode & _mask;
            var lockTaken = false;
            try
            {
                _locks[index].Enter(ref lockTaken);
                return _storage[index].TryWait(value, hashCode);
            }
            finally
            {
                Debug.Assert(lockTaken);
                _locks[index].Exit(false);
            }
        }
        public Task WaitAsync(T value)
        {
            var hashCode = _comparer.GetHashCode(value) & 0x7FFFFFFF;
            var index = hashCode & _mask;
            var lockTaken = false;
            try
            {
                _locks[index].Enter(ref lockTaken);
                return _storage[index].WaitAsync(value, hashCode);
            }
            finally
            {
                Debug.Assert(lockTaken);
                _locks[index].Exit(false);
            }
        }
        public bool Realese(T value)
        {
            var hashCode = _comparer.GetHashCode(value) & 0x7FFFFFFF;
            var index = hashCode & _mask;
            var lockTaken = false;
            try
            {
                _locks[index].Enter(ref lockTaken);
                return _storage[index].Realese(value, hashCode);
            }
            finally
            {
                Debug.Assert(lockTaken);
                _locks[index].Exit(false);
            }
        }

        //TODO
        //public void Reales(Predicate<T> match)
        //{
        //    //Count
        //}
    }
}
