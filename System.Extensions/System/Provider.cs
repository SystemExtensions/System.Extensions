
namespace System
{
    using System.Diagnostics;//ObjectPool<T>
    using System.Threading;
    public abstract class Provider<T>
    {
        public abstract bool TryGetValue(out T value, out IDisposable disposable);

        #region static provider 
        private class StackProvider : Provider<T>//TODO? IDisposable
        {
            public StackProvider(Func<T> valueFactory, int max,Action<T> reset)
            {
                Node temp = null;
                for (int i = 0; i < max; i++)
                {
                    var node = new Node(valueFactory, reset);
                    node.Next = temp;
                    temp = node;
                }
                Head = temp;
            }
            public class Node : IDisposable
            {
                public Node Next;
                public StackProvider Provider;
                public Node(Node node) 
                {
                    Debug.Assert(node != null);
                    _value = node._value;
                    _valueFactory = node._valueFactory;
                    _reset = node._reset;
                }
                public Node(Func<T> valueFactory, Action<T> reset)
                {
                    _valueFactory = valueFactory;
                    _reset = reset;
                }

                private T _value;
                private Func<T> _valueFactory;
                private Action<T> _reset;
                [DebuggerBrowsable(DebuggerBrowsableState.Never)]
                public T Value
                {
                    get
                    {
                        Debug.Assert(Provider != null);
                        if (_valueFactory != null)
                        {
                            _value = _valueFactory();
                            _valueFactory = null;
                        }
                        return _value;
                    }
                }
                public void Dispose()
                {
                    var provider = Interlocked.Exchange(ref Provider, null);
                    if (provider == null)
                        return;

                    if (_valueFactory == null && _reset != null)
                        _reset.Invoke(_value);

                    var @this = new Node(this);
                    @this.Next = provider.Head;
                    if (Interlocked.CompareExchange(ref provider.Head, @this, @this.Next) == @this.Next)
                        return;

                    var spinWait = new SpinWait();
                    for (; ; )
                    {
                        spinWait.SpinOnce();
                        @this.Next = provider.Head;
                        if (Interlocked.CompareExchange(ref provider.Head, @this, @this.Next) == @this.Next)
                            return;
                    }
                }
            }
            public volatile Node Head;//TODO?? Padding  SpinLock??
            public override bool TryGetValue(out T value, out IDisposable disposable)
            {
                var head = Head;
                if (head == null)
                {
                    value = default;
                    disposable = null;
                    return false;
                }
                if (Interlocked.CompareExchange(ref Head, head.Next, head) == head)
                {
                    Debug.Assert(head.Provider == null);
                    head.Provider = this;
                    value = head.Value;
                    disposable = head;
                    return true;
                }
                var spinWait = new SpinWait();
                for (; ; )
                {
                    spinWait.SpinOnce();
                    head = Head;
                    if (head == null)
                    {
                        value = default;
                        disposable = null;
                        return false;
                    }
                    if (Interlocked.CompareExchange(ref Head, head.Next, head) == head)
                    {
                        Debug.Assert(head.Provider == null);
                        head.Provider = this;
                        value = head.Value;
                        disposable = head;
                        return true;
                    }
                }
            }
        }
        private class ProcessorProvider : Provider<T>
        {
            public ProcessorProvider(Func<T> valueFactory, int max, Action<T> reset)
            {
                var count = Environment.ProcessorCount - 1;
                count |= count >> 1;
                count |= count >> 2;
                count |= count >> 4;
                count |= count >> 8;
                count |= count >> 16;
                count += 1;
                _mask = count - 1;
                _providers = new StackProvider[count];
                for (int i = 0; i < count; i++)
                {
                    _providers[i] = new StackProvider(valueFactory, max, reset);
                }
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            private StackProvider[] _providers;
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private int _mask;
            public override bool TryGetValue(out T value, out IDisposable disposable)
            {
                return _providers[Thread.GetCurrentProcessorId() & _mask].TryGetValue(out value, out disposable);
            }
        }
        public static Provider<T> Create(Func<T> valueFactory, int max)
        {
            if (valueFactory == null)
                throw new ArgumentNullException(nameof(valueFactory));
            if (max <= 0)
                throw new ArgumentOutOfRangeException(nameof(max));

            return new StackProvider(valueFactory, max, null);
        }
        public static Provider<T> Create(Func<T> valueFactory, int max, Action<T> reset)
        {
            if (valueFactory == null)
                throw new ArgumentNullException(nameof(valueFactory));
            if (max <= 0)
                throw new ArgumentOutOfRangeException(nameof(max));

            return new StackProvider(valueFactory, max, reset);
        }
        public static Provider<T> CreateFromProcessor(Func<T> valueFactory, int max)
        {
            if (valueFactory == null)
                throw new ArgumentNullException(nameof(valueFactory));
            if (max <= 0)
                throw new ArgumentOutOfRangeException(nameof(max));

            return new ProcessorProvider(valueFactory, max, null);
        }
        public static Provider<T> CreateFromProcessor(Func<T> valueFactory, int max, Action<T> reset)
        {
            if (valueFactory == null)
                throw new ArgumentNullException(nameof(valueFactory));
            if (max <= 0)
                throw new ArgumentOutOfRangeException(nameof(max));

            return new ProcessorProvider(valueFactory, max, reset);
        }
        //TODO??? CreateFromThread
        #endregion
    }
}
