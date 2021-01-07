
namespace System.Threading.Tasks
{
    using System.Diagnostics;
    public class TaskTimeoutQueue
    {
        #region const
        private const int _Free = 0;
        private const int _ToWrite = 1;
        private const int _Write = 2;
        #endregion
        private class Delayed : TaskCompletionSource<object>
        {
            public Delayed(object state)
                : base(state, TaskCreationOptions.RunContinuationsAsynchronously)
            { }

            public volatile Delayed Previous;
            public volatile Delayed Next;
            public volatile int Flag;
            public DateTimeOffset DelayTime;
            public Action<Task> ContinuationAction;
        }
        private class CachedNow
        {
            public CachedNow(DateTimeOffset value)
            {
                Value = value;
            }

            public readonly DateTimeOffset Value;
        }
        public TaskTimeoutQueue(int millisecondsTimeout)
            : this(TimeSpan.FromMilliseconds(millisecondsTimeout))
        { }
        public TaskTimeoutQueue(TimeSpan timeout)
        {
            var ticks = timeout.Ticks;
            if (ticks == 0)
                throw new ArgumentOutOfRangeException(nameof(timeout));

            if (ticks > 0)
            {
                _timeout = timeout;
            }
            else
            {
                _timeout = new TimeSpan(Math.Abs(ticks));
                _cachedNow = new CachedNow(DateTimeOffset.Now.Add(_timeout));
            }
            _head = new Delayed(null) { Flag = _ToWrite };
            _tail = _head;
            _timer = new Timer(TimerCallback);
            _interval = TimeSpan.FromSeconds(1);
            _TaskCompleted = TaskCompleted;
            _timerIsRunning = false;
        }
        private CachedNow _cachedNow;
        private TimeSpan _timeout;
        private TimeSpan _interval;
        private Timer _timer;
        private volatile bool _timerIsRunning;
        private Delayed _head;
        private volatile Delayed _tail;
        private Action<Task, object> _TaskCompleted;
        private void TimerCallback(object state)
        {
            if (_cachedNow != null)
                _cachedNow = new CachedNow(DateTimeOffset.Now.Add(_timeout));
            var head = _head;
            var spinWait = new SpinWait();
            for (; ; )
            {
                if (Interlocked.CompareExchange(ref head.Flag, _Write, _ToWrite) == _ToWrite)
                {
                    var delayed = head.Next;
                    if (delayed == null)
                    {
                        head.Flag = _ToWrite;
                        _timerIsRunning = false;//lock _timer
                        return;
                    }
                    var now = DateTimeOffset.Now;
                    if (now < delayed.DelayTime)
                    {
                        //1S的误差 影响不大
                        head.Flag = _ToWrite;
                        var dueTime = delayed.DelayTime - now;
                        _timer.Change(dueTime > _interval ? dueTime : _interval, Timeout.InfiniteTimeSpan);
                        return;
                    }
                    Delayed next;
                    for (; ; )
                    {
                        if (Interlocked.CompareExchange(ref delayed.Flag, _Write, _ToWrite) == _ToWrite)
                        {
                            next = delayed.Next;
                            break;
                        }
                        spinWait.SpinOnce();
                    }
                    if (next == null)
                        _tail = head;
                    else
                        next.Previous = head;
                    delayed.Next = null;
                    delayed.Previous = null;
                    delayed.Flag = _Free;
                    head.Next = next;
                    head.Flag = _ToWrite;
                    try
                    {
                        if (delayed.TrySetException(new TimeoutException()))
                        {
                            if (delayed.ContinuationAction != null)
                            {
                                var task = (Task)delayed.Task.AsyncState;
                                task.ContinueWith(delayed.ContinuationAction);
                            }
                        }
                    }
                    catch { }
                    continue;
                }
                spinWait.SpinOnce();
            }
        }
        private void TaskCompleted(Task task, object state)
        {
            var delayed = (Delayed)state;
            Debug.Assert(delayed != null);
            Debug.Assert(ReferenceEquals(task, delayed.Task.AsyncState));
            if (delayed.Flag == _Free)
                return;

            if (TryRemove(delayed))
            {
                if (task.Exception != null)
                    delayed.TrySetException(task.Exception.InnerExceptions);
                else
                    delayed.TrySetResult(null);
            }
        }
        private bool TryRemove(Delayed delayed)
        {
            Debug.Assert(delayed != null);
            var spinWait = new SpinWait();
            for (; ; )
            {
                if (delayed.Flag == _Free)
                    return false;
                var previous = delayed.Previous;
                if (previous == null)
                    return false;
                if (Interlocked.CompareExchange(ref previous.Flag, _Write, _ToWrite) == _ToWrite)
                {
                    if (previous.Next != delayed)//ABA
                    {
                        previous.Flag = _ToWrite;
                        continue;
                    }
                    Delayed next;
                    for (; ; )
                    {
                        if (Interlocked.CompareExchange(ref delayed.Flag, _Write, _ToWrite) == _ToWrite)
                        {
                            //aba
                            next = delayed.Next;
                            break;
                        }
                        spinWait.SpinOnce();
                    }
                    if (next == null)
                        _tail = previous;
                    else
                        next.Previous = previous;
                    delayed.Next = null;
                    delayed.Previous = null;
                    delayed.Flag = _Free;
                    previous.Next = next;
                    previous.Flag = _ToWrite;
                    return true;
                }
                spinWait.SpinOnce();
            }
        }
        public Task Add(Task task) 
        {
            return Add(task, null);
        }
        public Task Add(Task task, Action<Task> continuationAction)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));
            if (task.IsCompleted)
                return task;
            
            if (!_timerIsRunning)
            {
                lock (_timer)
                {
                    if (!_timerIsRunning)
                    {
                        _timer.Change(_timeout, Timeout.InfiniteTimeSpan);
                        if (_cachedNow != null)
                            _cachedNow = new CachedNow(DateTimeOffset.Now.Add(_timeout));
                        _timerIsRunning = true;
                    }
                }
            }
            var delayed = new Delayed(task);
            delayed.ContinuationAction = continuationAction;
            var now = _cachedNow == null ? DateTimeOffset.Now : _cachedNow.Value;
            delayed.DelayTime = now.Add(_timeout);
            var spinWait = new SpinWait();
            for (; ; )
            {
                var tail = _tail;//ABA
                Debug.Assert(tail != null);
                if (Interlocked.CompareExchange(ref tail.Flag, _Write, _ToWrite) == _ToWrite)
                {
                    if (tail.Next != null)
                    {
                        tail.Flag = _ToWrite;
                        continue;
                    }
                    tail.Next = delayed;
                    delayed.Previous = tail;
                    delayed.Flag = _ToWrite;
                    _tail = delayed;
                    tail.Flag = _ToWrite;
                    break;
                }
                spinWait.SpinOnce();
            }
            task.ContinueWith(_TaskCompleted, delayed);
            return delayed.Task;
        }
    }
    public class TaskTimeoutQueue<T>
    {
        #region const
        private const int _Free = 0;
        private const int _ToWrite = 1;
        private const int _Write = 2;
        #endregion
        private class Delayed : TaskCompletionSource<T>
        {
            public Delayed(object state)
                : base(state, TaskCreationOptions.RunContinuationsAsynchronously)
            { }

            public volatile Delayed Previous;
            public volatile Delayed Next;
            public volatile int Flag;
            public DateTimeOffset DelayTime;
            public Action<Task<T>> ContinuationAction;
        }
        private class CachedNow
        {
            public CachedNow(DateTimeOffset value)
            {
                Value = value;
            }

            public readonly DateTimeOffset Value;
        }
        public TaskTimeoutQueue(int millisecondsTimeout)
            : this(TimeSpan.FromMilliseconds(millisecondsTimeout))
        { }
        public TaskTimeoutQueue(TimeSpan timeout)
        {
            var ticks = timeout.Ticks;
            if (ticks == 0)
                throw new ArgumentOutOfRangeException(nameof(timeout));

            if (ticks > 0)
            {
                _timeout = timeout;
            }
            else
            {
                _timeout = new TimeSpan(Math.Abs(ticks));
                _cachedNow = new CachedNow(DateTimeOffset.Now.Add(_timeout));
            }
            _head = new Delayed(null) { Flag = _ToWrite };
            _tail = _head;
            _timer = new Timer(TimerCallback);
            _interval = TimeSpan.FromSeconds(1);
            _TaskCompleted = TaskCompleted;
            _timerIsRunning = false;
        }
        private CachedNow _cachedNow;
        private TimeSpan _timeout;
        private TimeSpan _interval;
        private Timer _timer;
        private volatile bool _timerIsRunning;
        private Delayed _head;
        private volatile Delayed _tail;
        private Action<Task<T>, object> _TaskCompleted;
        private void TimerCallback(object state)
        {
            if (_cachedNow != null)
                _cachedNow = new CachedNow(DateTimeOffset.Now.Add(_timeout));
            var head = _head;
            var spinWait = new SpinWait();
            for (; ; )
            {
                if (Interlocked.CompareExchange(ref head.Flag, _Write, _ToWrite) == _ToWrite)
                {
                    var delayed = head.Next;
                    if (delayed == null)
                    {
                        head.Flag = _ToWrite;
                        _timerIsRunning = false;
                        return;
                    }
                    var now = DateTimeOffset.Now;
                    if (now < delayed.DelayTime)
                    {
                        //1S的误差 影响不大
                        head.Flag = _ToWrite;
                        var dueTime = delayed.DelayTime - now;
                        _timer.Change(dueTime > _interval ? dueTime : _interval, Timeout.InfiniteTimeSpan);
                        return;
                    }
                    Delayed next;
                    for (; ; )
                    {
                        if (Interlocked.CompareExchange(ref delayed.Flag, _Write, _ToWrite) == _ToWrite)
                        {
                            next = delayed.Next;
                            break;
                        }
                        spinWait.SpinOnce();
                    }
                    if (next == null)
                        _tail = head;
                    else
                        next.Previous = head;
                    delayed.Next = null;
                    delayed.Previous = null;
                    delayed.Flag = _Free;
                    head.Next = next;
                    head.Flag = _ToWrite;
                    try
                    {
                        if (delayed.TrySetException(new TimeoutException())) 
                        {
                            if (delayed.ContinuationAction != null)
                            {
                                var task = (Task<T>)delayed.Task.AsyncState;
                                task.ContinueWith(delayed.ContinuationAction);
                            }
                        }
                    }
                    catch { }
                    continue;
                }
                spinWait.SpinOnce();
            }
        }
        private void TaskCompleted(Task<T> task, object state)
        {
            var delayed = (Delayed)state;
            Debug.Assert(delayed != null);
            Debug.Assert(ReferenceEquals(task, delayed.Task.AsyncState));
            if (TryRemove(delayed))
            {
                if (task.Exception != null)
                    delayed.TrySetException(task.Exception.InnerExceptions);
                else
                    delayed.TrySetResult(task.Result);
            }
        }
        private bool TryRemove(Delayed delayed)
        {
            Debug.Assert(delayed != null);
            var spinWait = new SpinWait();
            for (; ; )
            {
                if (delayed.Flag == _Free)
                    return false;
                var previous = delayed.Previous;
                if (previous == null)
                    return false;
                if (Interlocked.CompareExchange(ref previous.Flag, _Write, _ToWrite) == _ToWrite)
                {
                    if (previous.Next != delayed)
                    {
                        previous.Flag = _ToWrite;
                        continue;
                    }
                    Delayed next;
                    for (; ; )
                    {
                        if (Interlocked.CompareExchange(ref delayed.Flag, _Write, _ToWrite) == _ToWrite)
                        {
                            next = delayed.Next;
                            break;
                        }
                        spinWait.SpinOnce();
                    }
                    if (next == null)
                        _tail = previous;
                    else
                        next.Previous = previous;
                    delayed.Next = null;
                    delayed.Previous = null;
                    delayed.Flag = _Free;
                    previous.Next = next;
                    previous.Flag = _ToWrite;
                    return true;
                }
                spinWait.SpinOnce();
            }
        }
        public Task<T> Add(Task<T> task) 
        {
            return Add(task, null);
        }
        public Task<T> Add(Task<T> task, Action<Task<T>> continuationAction)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));
            if (task.IsCompleted)
                return task;
            
            if (!_timerIsRunning)
            {
                lock (_timer)
                {
                    if (!_timerIsRunning)
                    {
                        _timer.Change(_timeout, Timeout.InfiniteTimeSpan);
                        if (_cachedNow != null)
                            _cachedNow = new CachedNow(DateTimeOffset.Now.Add(_timeout));
                        _timerIsRunning = true;
                    }
                }
            }
            var delayed = new Delayed(task);
            delayed.ContinuationAction = continuationAction;
            var now = _cachedNow == null ? DateTimeOffset.Now : _cachedNow.Value;
            delayed.DelayTime = now.Add(_timeout);
            var spinWait = new SpinWait();
            for (; ; )
            {
                var tail = _tail;
                if (Interlocked.CompareExchange(ref tail.Flag, _Write, _ToWrite) == _ToWrite)
                {
                    if (tail.Next != null)
                    {
                        tail.Flag = _ToWrite;
                        continue;
                    }
                    tail.Next = delayed;
                    delayed.Previous = tail;
                    delayed.Flag = _ToWrite;
                    _tail = delayed;
                    tail.Flag = _ToWrite;
                    break;
                }
                spinWait.SpinOnce();
            }
            task.ContinueWith(_TaskCompleted, delayed);
            return delayed.Task;
        }
    }
}
