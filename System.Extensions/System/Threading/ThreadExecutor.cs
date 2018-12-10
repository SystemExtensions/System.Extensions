
namespace System.Threading
{
    using System.Diagnostics;
    public class ThreadExecutor:IExecutor
    {
        [ThreadStatic]
        private static ThreadExecutor _current = null;
        public static ThreadExecutor Current => _current;

        private const int _Free = 0;
        private const int _ToWait = 1;
        private const int _Wait = 2;
        private const int _ToReset = 3; 
        private const int _Size = 16;//255
        private const int _Mask = 15;//254
        private static readonly Action<object> _Action 
            = (state) => { ((Action)state).Invoke(); };
        public ThreadExecutor()
            : this(64, 0)
        { }
        public ThreadExecutor(int workQueue)
           : this(workQueue, 0)
        {
            //1024==50ms
        }
        public ThreadExecutor(int workQueue, int maxStackSize)
        {
            if (workQueue < 4)//最低4个
                throw new ArgumentOutOfRangeException(nameof(workQueue));

            var queue1 = new WorkQueue(0);
            var queue2 = new WorkQueue(0);
            var queue3 = new WorkQueue(0);
            var queue4 = new WorkQueue(0);
            queue1.Next = queue2;
            queue2.Next = queue3;
            queue3.Next = queue4;
            queue4.Next = queue1;
            _workQueue = queue1;
            _available = workQueue - 4;
            _context = new Context(this);
            _workThread = new Thread(Start, maxStackSize);
            _workThread.TrySetApartmentState(ApartmentState.MTA);
            _workThread.IsBackground = true;
            _workThread.Start(queue1);
        }

        private int _available;//-1就回收
        private bool _shutdownRequested;
        private bool _abortRequested;
        private Context _context;
        private Thread _workThread;
        private volatile WorkQueue _workQueue;//执行状态 空闲、繁忙
        public event Action<Exception> OnException;//线程执行异常
        public Thread WorkThread => _workThread;
        public SynchronizationContext SynchronizationContext => _context;
        public void Run(IRunnable runnable)
        {
            if (ReferenceEquals(_current, this))
                runnable.Run();//同步执行
            else
                Queue(runnable);
        }
        public void Run(Action action)
        {
            if (ReferenceEquals(_current, this))
            {
                action();
                return;
            }
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            Queue(_Action, action);
        }
        public void Run(Action<object> action, object state)
        {
            if (ReferenceEquals(_current, this))
                action(state);
            else
                Queue(action, state);
        }
        public void Queue(IRunnable runnable)
        {
            if (runnable == null)
                throw new ArgumentNullException(nameof(runnable));
            if (_shutdownRequested)
                throw new InvalidOperationException(nameof(_shutdownRequested));
            if (_abortRequested)
                throw new InvalidOperationException(nameof(_abortRequested));

            var spinWait = new SpinWait();
            for (; ; )
            {
                var workQueue = _workQueue;
                var index = workQueue.Index;
                if (Interlocked.CompareExchange(ref workQueue.WorkItems[index].Status, _ToWait, _Free) == _Free)
                {
                    if (index == _Mask)
                    {
                        #region 最后一个
                        var next = workQueue.Next;
                        var nextStatus = next.WorkItems[_Mask].Status;
                        if (nextStatus == _Wait)//没有执行完 扩容
                        {
                            WorkQueue queue;
                            if (_available > 0)
                            {
                                queue = new WorkQueue(0);
                                _available -= 1;
                            }
                            else
                            {
                                queue = new WorkQueue(1);
                            }
                            queue.Next = next;
                            workQueue.Next = queue;
                            _workQueue = queue;
                            workQueue.Index = 0;
                        }
                        else if (next.Flag == 0)
                        {
                            Debug.Assert(nextStatus != _ToWait);
                            for (int i = 0; i < _Size; i++)
                            {
                                next.WorkItems[i].Status = _Free;
                            }
                            _workQueue = next;
                            workQueue.Index = 0;
                        }
                        else if (next.Flag == 1)
                        {
                            Debug.Assert(_available == 0 || _available == -1);
                            if (_available == 0)//还没有回收标志
                            {
                                if (next.Next.WorkItems[_Mask].Status != _Wait)
                                {
                                    next.Flag = 2;//标志下次回收
                                    _available = -1;
                                }
                            }
                            for (int i = 0; i < _Size; i++)
                            {
                                next.WorkItems[i].Status = _Free;
                            }
                            _workQueue = next;
                            workQueue.Index = 0;
                        }
                        else if (next.Flag == 2)
                        {
                            Debug.Assert(_available == -1);
                            var tempNext = next.Next;
                            if (tempNext.WorkItems[_Mask].Status != _Wait)
                            {
                                Console.WriteLine("回收");
                                _available = 0;
                                next.Next = null;
                                workQueue.Next = tempNext;
                                for (int i = 0; i < _Size; i++)
                                {
                                    tempNext.WorkItems[i].Status = _Free;
                                }
                                _workQueue = tempNext;
                                workQueue.Index = 0;
                            }
                            else
                            {
                                for (int i = 0; i < _Size; i++)
                                {
                                    next.WorkItems[i].Status = _Free;
                                }
                                _workQueue = next;
                                workQueue.Index = 0;
                            }
                        }
                        #endregion
                    }
                    else
                    {
                        workQueue.Index = index + 1;
                    }
                    workQueue.WorkItems[index].Runnable = runnable;
                    Volatile.Write(ref workQueue.WorkItems[index].Status, _Wait);
                    return;
                }
                spinWait.SpinOnce();
            }
        }
        public void Queue(Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            Queue(_Action, action);
        }
        public void Queue(Action<object> action, object state)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            if (_shutdownRequested)
                throw new InvalidOperationException(nameof(_shutdownRequested));
            if (_abortRequested)
                throw new InvalidOperationException(nameof(_abortRequested));

            var spinWait = new SpinWait();
            for (;;)
            {
                var workQueue = _workQueue;
                var index = workQueue.Index;
                if (Interlocked.CompareExchange(ref workQueue.WorkItems[index].Status, _ToWait, _Free) == _Free)
                {
                    if (index == _Mask)
                    {
                        #region 最后一个
                        var next = workQueue.Next;
                        var nextStatus = next.WorkItems[_Mask].Status;
                        if (nextStatus == _Wait)//没有执行完 扩容
                        {
                            WorkQueue queue;
                            if (_available > 0)
                            {
                                queue = new WorkQueue(0);
                                _available -= 1;
                            }
                            else
                            {
                                queue = new WorkQueue(1);
                            }
                            queue.Next = next;
                            workQueue.Next = queue;
                            _workQueue = queue;
                            workQueue.Index = 0;
                        }
                        else if (next.Flag == 0)
                        {
                            Debug.Assert(nextStatus != _ToWait);
                            for (int i = 0; i < _Size; i++)
                            {
                                next.WorkItems[i].Status = _Free;
                            }
                            _workQueue = next;
                            workQueue.Index = 0;
                        }
                        else if (next.Flag == 1)
                        {
                            Debug.Assert(_available == 0 || _available == -1);
                            if (_available == 0)//还没有回收标志
                            {
                                if (next.Next.WorkItems[_Mask].Status != _Wait)
                                {
                                    next.Flag = 2;//标志下次回收
                                    _available = -1;
                                }
                            }
                            for (int i = 0; i < _Size; i++)
                            {
                                next.WorkItems[i].Status = _Free;
                            }
                            _workQueue = next;
                            workQueue.Index = 0;
                        }
                        else if (next.Flag == 2)
                        {
                            Debug.Assert(_available == -1);
                            var tempNext = next.Next;
                            if (tempNext.WorkItems[_Mask].Status != _Wait)
                            {
                                Console.WriteLine("回收");
                                _available = 0;
                                next.Next = null;
                                workQueue.Next = tempNext;
                                for (int i = 0; i < _Size; i++)
                                {
                                    tempNext.WorkItems[i].Status = _Free;
                                }
                                _workQueue = tempNext;
                                workQueue.Index = 0;
                            }
                            else
                            {
                                for (int i = 0; i < _Size; i++)
                                {
                                    next.WorkItems[i].Status = _Free;
                                }
                                _workQueue = next;
                                workQueue.Index = 0;
                            }
                        }
                        #endregion
                    }
                    else
                    {
                        workQueue.Index = index + 1;
                    }
                    workQueue.WorkItems[index].Action = action;
                    workQueue.WorkItems[index].State = state;
                    Volatile.Write(ref workQueue.WorkItems[index].Status, _Wait);
                    return;
                }
                spinWait.SpinOnce();
            }
        }
        public void RequestShutdown()//不会进行阻塞
        {
            _shutdownRequested = true;
        }
        public void Shutdown()
        {
            _shutdownRequested = true;
            _workThread.Join();
        }
        public bool Shutdown(int millisecondsTimeout)
        {
            _shutdownRequested = true;
            if (!_workThread.Join(millisecondsTimeout))
            {
                Abort();
                return false;//超时了
            }
            return true;//没有超时
        }
        public bool Shutdown(TimeSpan timeout)
        {
            _shutdownRequested = true;
            if (_workThread.Join(timeout))
            {
                Abort();
                return false;//超时了
            }
            return true;//没有超时
        }
        public void Abort()
        {
            _abortRequested = true;
            _workThread.Abort();
        }
        private void Start(object workQueueObj)
        {
            _current = this;// 执行完 _current = null;
            SynchronizationContext.SetSynchronizationContext(_context);//设置同步上下文
            var spinWait = new SpinWait();
            var workQueue = (WorkQueue)workQueueObj;
            for (;;)
            {
                for (int i = 0; i < _Size; i++)
                {
                    if (workQueue.WorkItems[i].Status != _Wait)
                    {
                        spinWait.Reset();
                        do
                        {
                            if (_shutdownRequested)
                                return;
                            spinWait.SpinOnce();
                        } while (workQueue.WorkItems[i].Status != _Wait);
                    }
                    if (workQueue.WorkItems[i].Action != null)
                    {
                        try
                        {
                            workQueue.WorkItems[i].Action.Invoke(workQueue.WorkItems[i].State);
                        }
                        catch (ThreadAbortException)
                        {
                            if (_abortRequested || Environment.HasShutdownStarted)
                                return;
                            Thread.ResetAbort();
                        }
                        catch (Exception ex)
                        {
                            OnException?.Invoke(ex);//是否继续捕获ThreadAbortException
                        }
                        workQueue.WorkItems[i].Action = null;
                        workQueue.WorkItems[i].State = null;
                        workQueue.WorkItems[i].Status = _ToReset;
                    }
                    else if (workQueue.WorkItems[i].SendOrPostCallback != null)
                    {
                        try
                        {
                            workQueue.WorkItems[i].SendOrPostCallback.Invoke(workQueue.WorkItems[i].State);
                        }
                        catch (ThreadAbortException)
                        {
                            if (_abortRequested || Environment.HasShutdownStarted)
                                return;
                            Thread.ResetAbort();
                        }
                        catch (Exception ex)
                        {
                            OnException?.Invoke(ex);
                        }
                        workQueue.WorkItems[i].SendOrPostCallback = null;
                        workQueue.WorkItems[i].State = null;
                        workQueue.WorkItems[i].Status = _ToReset;
                    }
                    else if (workQueue.WorkItems[i].Runnable != null)
                    {
                        try
                        {
                            workQueue.WorkItems[i].Runnable.Run();
                        }
                        catch (ThreadAbortException)
                        {
                            if (_abortRequested || Environment.HasShutdownStarted)
                                return;
                            Thread.ResetAbort();
                        }
                        catch (Exception ex)
                        {
                            OnException?.Invoke(ex);
                        }
                        workQueue.WorkItems[i].Runnable = null;
                        workQueue.WorkItems[i].State = null;
                        workQueue.WorkItems[i].Status = _ToReset;
                    }
                    else
                    {
                        Debug.Fail(nameof(ThreadExecutor));
                    }
                }
                workQueue = workQueue.Next;
            }
            
            //var runIndex = 0;
            //_current = this;/// 执行完 _current = null;
            //SynchronizationContext.SetSynchronizationContext(new SyncContext(this));//设置同步上下文
            //while (true)
            //{
            //    try
            //    {
            //        Debug.WriteLine("SpinWait");
            //        SpinWait.SpinUntil(_spinWaitCondition);
            //    }
            //    catch (ThreadAbortException)
            //    {
            //        //TryFinish();
            //        if (_abortRequested || Environment.HasShutdownStarted)
            //            return;
            //        Debug.WriteLine("SpinWait=>ResetAbort(ThreadAbortException)");
            //        Thread.ResetAbort();
            //        continue;
            //    }
            //    if (_shutdownRequested)
            //        return;
            //    do
            //    {
            //        //Debug
            //        if (_running.Status != _Wait)
            //            throw new Exception("high exception");

            //        try
            //        {
            //            _running.Runnable.Run();
            //        }
            //        catch (Exception ex)
            //        {
            //            Debug.WriteLine("Run Exception");
            //            if (ex is ThreadAbortException)
            //            {
            //                if (_abortRequested || Environment.HasShutdownStarted)
            //                    return;
            //                TryThreadResetAbort();
            //            }
            //            try
            //            {
            //                OnException?.Invoke(ex);
            //            }
            //            catch (ThreadAbortException)
            //            {
            //                if (_abortRequested || Environment.HasShutdownStarted)
            //                    return;
            //                TryThreadResetAbort();
            //            }
            //            catch (Exception)
            //            {
            //                throw;
            //            }
            //        }
            //        var running = _running;
            //        _running.Runnable = null;
            //        _running.ActionRunnable.Action = null;
            //        _running.StateActionRunnable.Action = null;
            //        _running.StateActionRunnable.State = null;
            //        _running.SendOrPostCallbackRunnable.Action = null;
            //        _running.SendOrPostCallbackRunnable.State = null;
            //        _running = _running.Next;
            //        running.Status = _Free;//状态最后修改//ToFree
            //    } while (_running.Status == _Wait);
            //}
        }
        private struct WorkItem
        {
            public int Status;
            public Action<object> Action;
            public object State;
            public SendOrPostCallback SendOrPostCallback;
            public IRunnable Runnable;
        }
        private class WorkQueue
        {
            public WorkQueue(int flag)
            {
                WorkItems = new WorkItem[_Size];
                Flag = flag;
            }

            public WorkItem[] WorkItems;
            public volatile int Index;
            public volatile WorkQueue Next;
            public int Flag;//0 永久、1临时、2回收
            public string DebugValue
            {
                get
                {
                    int num1 = 0;
                    int num2 = 0;
                    int num3 = 0;
                    if (this.Flag == 0)
                        num1 += 1;
                    else if (this.Flag == 1)
                        num2 += 1;
                    else if (this.Flag == 2)
                        num3 += 1;
                    var next = this.Next;
                    while (next != this)
                    {
                        Console.WriteLine(next.Flag);
                        if (next.Flag == 0)
                            num1 += 1;
                        else if (next.Flag == 1)
                            num2 += 1;
                        else if (next.Flag == 2)
                            num3 += 1;
                        next = next.Next;
                    }
                    return $"永久:{num1}、临时{num2}+{num3}";
                }
            }
        }
        private class Context : SynchronizationContext
        {
            public Context(ThreadExecutor executor)
            {
                if (executor == null)
                    throw new ArgumentNullException(nameof(executor));

                @this = executor;
            }

            private ThreadExecutor @this;
            public override void Post(SendOrPostCallback action, object state)
            {
                if (ReferenceEquals(_current, @this))
                {
                    action(state);
                    return;
                }
                
                if (action == null)
                    throw new ArgumentNullException(nameof(action));
                if (@this._shutdownRequested)
                    throw new InvalidOperationException(nameof(_shutdownRequested));
                if (@this._abortRequested)
                    throw new InvalidOperationException(nameof(_abortRequested));

                var spinWait = new SpinWait();
                for (;;)
                {
                    var workQueue = @this._workQueue;
                    var index = workQueue.Index;
                    var status = Volatile.Read(ref workQueue.WorkItems[index].Status);
                    if (Interlocked.CompareExchange(ref workQueue.WorkItems[index].Status, _ToWait, _Free) == status)
                    {
                        if (index == _Mask)
                        {
                            var next = workQueue.Next;
                            var nextStatus = next.WorkItems[_Mask].Status;
                            if (nextStatus == _Wait)//没有执行完 扩容
                            {
                                WorkQueue queue;
                                if (@this._available > 0)
                                {
                                    queue = new WorkQueue(0);
                                    @this._available -= 1;
                                }
                                else
                                {
                                    queue = new WorkQueue(1);
                                }
                                queue.Next = next;
                                workQueue.Next = queue;
                                @this._workQueue = queue;
                                workQueue.Index = 0;
                            }
                            else if (next.Flag == 0)
                            {
                                Debug.Assert(nextStatus != _ToWait);
                                if (nextStatus == _ToReset)
                                {
                                    for (int i = 0; i < _Size; i++)
                                    {
                                        next.WorkItems[i].Status = _Free;
                                    }
                                }
                                @this._workQueue = next;
                                workQueue.Index = 0;
                            }
                            else if (next.Flag == 1)
                            {
                                Debug.Assert(@this._available == 0 || @this._available == -1);
                                if (@this._available == 0)//还没有回收标志
                                {
                                    if (next.Next.WorkItems[_Mask].Status != _Wait)
                                    {
                                        next.Flag = 2;//标志下次回收
                                        @this._available = -1;
                                    }
                                }
                                @this._workQueue = next;
                                workQueue.Index = 0;
                            }
                            else if (next.Flag == 2)
                            {
                                Debug.Assert(@this._available == -1);
                                var tempNext = next.Next;
                                if (tempNext.WorkItems[_Mask].Status != _Wait)
                                {
                                    Console.WriteLine("回收");
                                    @this._available = 0;
                                    workQueue.Next = tempNext;
                                    @this._workQueue = tempNext;
                                    workQueue.Index = 0;
                                }
                                else
                                {
                                    @this._workQueue = next;
                                    workQueue.Index = 0;
                                }
                            }
                        }
                        else
                        {
                            workQueue.Index = index + 1;
                        }
                        workQueue.WorkItems[index].SendOrPostCallback = action;
                        workQueue.WorkItems[index].State = state;
                        Volatile.Write(ref workQueue.WorkItems[index].Status, _Wait);
                        return;
                    }
                    spinWait.SpinOnce();
                }
            }
        }
    }
}
