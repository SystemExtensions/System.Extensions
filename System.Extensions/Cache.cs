
namespace System.Extensions
{
    using System.Diagnostics;
    using System.Threading;
    public abstract class Cache<T>
    {
        public abstract bool TryGetValue(out T value, out IDisposable disposable);

        #region static provider 
        private class LinkedCache : Cache<T>
        {
            //int init 初始化个数
            public LinkedCache(Func<T> valueFactory, int max,Action<T> reset)
            {
                var cacheds = new Cached[max];
                Cached temp = null;
                for (int i = 0; i < max; i++)
                {
                    Cached cached = null;
                    cached = new Cached(this, valueFactory, reset);
                    cacheds[i] = cached;
                    if (temp != null)
                        temp.Next = cached;
                    temp = cached;
                }
                Head = cacheds[0];//设置为第一个
            }
            public class Cached : IDisposable
            {
                internal volatile Cached Next;

                public Cached(LinkedCache cache, Func<T> valueFactory, Action<T> reset)
                {
                    _cache = cache;
                    _valueFactory = valueFactory;
                    _reset = reset;
                }

                private LinkedCache _cache;
                private T _value;
                private Func<T> _valueFactory;
                private Action<T> _reset;
                [DebuggerBrowsable(DebuggerBrowsableState.Never)]
                public T Value
                {
                    get
                    {
                        if (_valueFactory != null)
                        {
                            _value = _valueFactory.Invoke();
                            _valueFactory = null;

                        }
                        return _value;
                    }
                }
                public void Dispose()
                {
                    this.Next = _cache.Head;//尝试往头上接
                    var node = this;
                    //有问题
                    if (Interlocked.CompareExchange(ref _cache.Head, node, node.Next) == this.Next)
                    {
                        return;
                    }

                    var spinWait = default(SpinWait);
                    while (true)
                    {
                        this.Next = _cache.Head;
                        node = this;
                        if (Interlocked.CompareExchange(ref _cache.Head, node, node.Next) == this.Next)
                        {
                            return;
                        }
                        spinWait.SpinOnce();
                        Console.WriteLine("进行一次空旋");
                    }
                }
            }
            //数据填充
            internal volatile Cached Head;
            public override bool TryGetValue(out T value, out IDisposable disposable)
            {
                var head = Head;
                if (head == null)
                {
                    value = default;
                    disposable = null;
                    return false;
                }
                //是否出现竞争就直接返回 还是加一个次数
                if (Interlocked.CompareExchange(ref Head, head.Next, head) == head)
                {
                    head.Next = null;
                    value = head.Value;
                    disposable = head;
                    return true;
                }
                var spinWait = default(SpinWait);
                while (true)
                {
                    head = Head;
                    if (head == null)
                    {
                        value = default;
                        disposable = null;
                        return false;
                    }
                    if (Interlocked.CompareExchange(ref Head, head.Next, head) == head)
                    {
                        head.Next = null;
                        value = head.Value;
                        disposable = head;
                        return true;
                    }
                    spinWait.SpinOnce();
                    Console.WriteLine("进行一次空旋");
                }
            }
        }
        private class ProcessorCache : Cache<T>
        {
            public ProcessorCache(Func<T> valueFactory, int max, Action<T> reset)
            {
                var count = Environment.ProcessorCount - 1;
                count |= count >> 1;
                count |= count >> 2;
                count |= count >> 4;
                count |= count >> 8;
                count |= count >> 16;
                count += 1;
                _mask = count - 1;
                _caches = new LinkedCache[count];
                for (int i = 0; i < count; i++)
                {
                    _caches[i] = new LinkedCache(valueFactory, max, reset);
                }
            }

            private LinkedCache[] _caches;
            private int _mask;
            public override bool TryGetValue(out T value, out IDisposable disposable)
            {
                return _caches[Thread.GetCurrentProcessorId() & _mask].TryGetValue(out value, out disposable);
            }
        }
        public static Cache<T> Create(Func<T> valueFactory,int max)
        {
            if (valueFactory == null)
                throw new ArgumentNullException(nameof(valueFactory));
            if (max <= 0)
                throw new ArgumentOutOfRangeException(nameof(max));

            return new LinkedCache(valueFactory,max,null);
        }
        public static Cache<T> Create(Func<T> valueFactory, int max,Action<T> reset)
        {
            if (valueFactory == null)
                throw new ArgumentNullException(nameof(valueFactory));
            if (max <= 0)
                throw new ArgumentOutOfRangeException(nameof(max));

            return new LinkedCache(valueFactory, max, reset);
        }
        public static Cache<T> CreateFromProcessor(Func<T> valueFactory, int max)
        {
            if (valueFactory == null)
                throw new ArgumentNullException(nameof(valueFactory));
            if (max <= 0)
                throw new ArgumentOutOfRangeException(nameof(max));

            return new ProcessorCache(valueFactory,max,null);
        }
        public static Cache<T> CreateFromProcessor(Func<T> valueFactory, int max, Action<T> reset)
        {
            if (valueFactory == null)
                throw new ArgumentNullException(nameof(valueFactory));
            if (max <= 0)
                throw new ArgumentOutOfRangeException(nameof(max));

            return new ProcessorCache(valueFactory, max, reset);
        }

        //public static Cache<T> Create(int count, Func<int> selector, Func<T> valueFactory, int max)
        //{
        //    return new LinkedCache(valueFactory, max, null);
        //}
        //public static Cache<T> Create(int count, Func<int> selector, Func<T> valueFactory, int max, Action<T> reset)
        //{
        //    return new LinkedCache(valueFactory, max, reset);
        //}
        #endregion
    }
}
