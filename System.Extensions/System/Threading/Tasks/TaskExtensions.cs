

namespace System.Threading.Tasks
{
    public static class TaskExtensions
    {
        #region private
        private class TimeoutTask//TODO? :TaskCompletionSource<object>
        {
            private static TimerCallback _TimerCallback = (state) =>
            {
                var @this = (TimeoutTask)state;
                if (@this._tcs.TrySetException(new TimeoutException()))
                {
                    @this._timer.Dispose();
                    if (@this._continuationAction != null)
                    {
                        @this._task.ContinueWith(@this._continuationAction);
                    }
                }
            };
            private static Action<Task, object> _ContinueWith = (task, state) =>
            {
                var @this = (TimeoutTask)state;

                if (task.Exception != null)
                {
                    if (@this._tcs.TrySetException(task.Exception.InnerExceptions))
                    {
                        @this._timer.Dispose();
                    }
                }
                else if (@this._tcs.TrySetResult(null))
                {
                    @this._timer.Dispose();
                }
            };
            public TimeoutTask(Task task, TimeSpan timout, Action<Task> continuationAction)
            {
                _task = task;
                _tcs = new TaskCompletionSource<object>(this, TaskCreationOptions.None);
                _continuationAction = continuationAction;
                _task.ContinueWith(_ContinueWith, this);
                _timer = new Timer(_TimerCallback, this, timout, TimeSpan.Zero);
            }
            private Task _task;
            private Timer _timer;
            private TaskCompletionSource<object> _tcs;
            private Action<Task> _continuationAction;
            public Task Task => _tcs.Task;
        }
        private class TimeoutTask<TResult>
        {
            private static TimerCallback _TimerCallback = (state) =>
            {
                var @this = (TimeoutTask<TResult>)state;
                if (@this._tcs.TrySetException(new TimeoutException()))
                {
                    @this._timer.Dispose();
                    if (@this._continuationAction != null)
                    {
                        @this._task.ContinueWith(@this._continuationAction);
                    }
                }
            };
            private static Action<Task<TResult>, object> _ContinueWith = (task, state) =>
            {
                var @this = (TimeoutTask<TResult>)state;

                if (task.Exception != null)
                {
                    if (@this._tcs.TrySetException(task.Exception.InnerExceptions))
                    {
                        @this._timer.Dispose();
                    }
                }
                else if (@this._tcs.TrySetResult(task.Result))
                {
                    @this._timer.Dispose();
                }
            };
            public TimeoutTask(Task<TResult> task, TimeSpan timout, Action<Task<TResult>> continuationAction)
            {
                _task = task;
                _tcs = new TaskCompletionSource<TResult>(this, TaskCreationOptions.None);
                _continuationAction = continuationAction;
                _task.ContinueWith(_ContinueWith, this);
                _timer = new Timer(_TimerCallback, this, timout, TimeSpan.Zero);
            }
            private Task<TResult> _task;
            private Timer _timer;
            private TaskCompletionSource<TResult> _tcs;
            private Action<Task<TResult>> _continuationAction;
            public Task<TResult> Task => _tcs.Task;
        }
        #endregion
        public static Task Timeout(this Task @this, int millisecondsTimeout)
        {
            if (@this == null || @this.IsCompleted)
                return @this;

            return new TimeoutTask(@this, TimeSpan.FromMilliseconds(millisecondsTimeout), null).Task;
        }
        public static Task Timeout(this Task @this, TimeSpan timeout)
        {
            if (@this == null || @this.IsCompleted)
                return @this;

            return new TimeoutTask(@this, timeout, null).Task;
        }
        public static ValueTask Timeout(this ValueTask @this, int millisecondsTimeout)
        {
            if (@this == null || @this.IsCompleted)
                return @this;

            return new ValueTask(new TimeoutTask(@this.AsTask(), TimeSpan.FromMilliseconds(millisecondsTimeout), null).Task);
        }
        public static ValueTask Timeout(this ValueTask @this, TimeSpan timeout)
        {
            if (@this == null || @this.IsCompleted)
                return @this;

            return new ValueTask(new TimeoutTask(@this.AsTask(), timeout, null).Task);
        }
        public static Task Timeout(this Task @this, int millisecondsTimeout, Action<Task> continuationAction)
        {
            if (@this == null || @this.IsCompleted)
                return @this;

            return new TimeoutTask(@this, TimeSpan.FromMilliseconds(millisecondsTimeout), continuationAction).Task;
        }
        public static Task Timeout(this Task @this, TimeSpan timeout, Action<Task> continuationAction)
        {
            if (@this == null || @this.IsCompleted)
                return @this;

            return new TimeoutTask(@this, timeout, continuationAction).Task;
        }
        public static ValueTask Timeout(this ValueTask @this, int millisecondsTimeout, Action<Task> continuationAction)
        {
            if (@this == null || @this.IsCompleted)
                return @this;

            return new ValueTask(new TimeoutTask(@this.AsTask(), TimeSpan.FromMilliseconds(millisecondsTimeout), continuationAction).Task);
        }
        public static ValueTask Timeout(this ValueTask @this, TimeSpan timeout, Action<Task> continuationAction)
        {
            if (@this == null || @this.IsCompleted)
                return @this;

            return new ValueTask(new TimeoutTask(@this.AsTask(), timeout, continuationAction).Task);
        }
        public static Task<TResult> Timeout<TResult>(this Task<TResult> @this, int millisecondsTimeout)
        {
            if (@this == null || @this.IsCompleted)
                return @this;

            return new TimeoutTask<TResult>(@this, TimeSpan.FromMilliseconds(millisecondsTimeout), null).Task;
        }
        public static Task<TResult> Timeout<TResult>(this Task<TResult> @this, TimeSpan timeout)
        {
            if (@this == null || @this.IsCompleted)
                return @this;

            return new TimeoutTask<TResult>(@this, timeout, null).Task;
        }
        public static ValueTask<TResult> Timeout<TResult>(this ValueTask<TResult> @this, int millisecondsTimeout)
        {
            if (@this == null || @this.IsCompleted)
                return @this;

            return new ValueTask<TResult>(new TimeoutTask<TResult>(@this.AsTask(), TimeSpan.FromMilliseconds(millisecondsTimeout), null).Task);
        }
        public static ValueTask<TResult> Timeout<TResult>(this ValueTask<TResult> @this, TimeSpan timeout)
        {
            if (@this == null || @this.IsCompleted)
                return @this;

            return new ValueTask<TResult>(new TimeoutTask<TResult>(@this.AsTask(), timeout, null).Task);
        }
        public static Task<TResult> Timeout<TResult>(this Task<TResult> @this, int millisecondsTimeout, Action<Task<TResult>> continuationAction)
        {
            if (@this == null || @this.IsCompleted)
                return @this;

            return new TimeoutTask<TResult>(@this, TimeSpan.FromMilliseconds(millisecondsTimeout), continuationAction).Task;
        }
        public static Task<TResult> Timeout<TResult>(this Task<TResult> @this, TimeSpan timeout, Action<Task<TResult>> continuationAction)
        {
            if (@this == null || @this.IsCompleted)
                return @this;

            return new TimeoutTask<TResult>(@this, timeout, continuationAction).Task;
        }
        public static ValueTask<TResult> Timeout<TResult>(this ValueTask<TResult> @this, int millisecondsTimeout, Action<Task<TResult>> continuationAction)
        {
            if (@this == null || @this.IsCompleted)
                return @this;

            return new ValueTask<TResult>(new TimeoutTask<TResult>(@this.AsTask(), TimeSpan.FromMilliseconds(millisecondsTimeout), continuationAction).Task);
        }
        public static ValueTask<TResult> Timeout<TResult>(this ValueTask<TResult> @this, TimeSpan timeout, Action<Task<TResult>> continuationAction)
        {
            if (@this == null || @this.IsCompleted)
                return @this;

            return new ValueTask<TResult>(new TimeoutTask<TResult>(@this.AsTask(), timeout, continuationAction).Task);
        }
        public static Task Timeout(this Task @this, TaskTimeoutQueue timeoutQueue)
        {
            if (@this == null || @this.IsCompleted || timeoutQueue == null)
                return @this;

            return timeoutQueue.Add(@this);
        }
        public static ValueTask Timeout(this ValueTask @this, TaskTimeoutQueue timeoutQueue)
        {
            if (@this == null || @this.IsCompleted || timeoutQueue == null)
                return @this;

            return new ValueTask(timeoutQueue.Add(@this.AsTask()));
        }
        public static Task Timeout(this Task @this, TaskTimeoutQueue timeoutQueue, Action<Task> continuationAction)
        {
            if (@this == null || @this.IsCompleted || timeoutQueue == null)
                return @this;

            return timeoutQueue.Add(@this, continuationAction);
        }
        public static ValueTask Timeout(this ValueTask @this, TaskTimeoutQueue timeoutQueue, Action<Task> continuationAction)
        {
            if (@this == null || @this.IsCompleted || timeoutQueue == null)
                return @this;

            return new ValueTask(timeoutQueue.Add(@this.AsTask(), continuationAction));
        }
        public static Task<TResult> Timeout<TResult>(this Task<TResult> @this, TaskTimeoutQueue<TResult> timeoutQueue)
        {
            if (@this == null || @this.IsCompleted || timeoutQueue == null)
                return @this;

            return timeoutQueue.Add(@this);
        }
        public static ValueTask<TResult> Timeout<TResult>(this ValueTask<TResult> @this, TaskTimeoutQueue<TResult> timeoutQueue)
        {
            if (@this == null || @this.IsCompleted || timeoutQueue == null)
                return @this;

            return new ValueTask<TResult>(timeoutQueue.Add(@this.AsTask()));
        }
        public static Task<TResult> Timeout<TResult>(this Task<TResult> @this, TaskTimeoutQueue<TResult> timeoutQueue, Action<Task<TResult>> continuationAction)
        {
            if (@this == null || @this.IsCompleted || timeoutQueue == null)
                return @this;

            return timeoutQueue.Add(@this, continuationAction);
        }
        public static ValueTask<TResult> Timeout<TResult>(this ValueTask<TResult> @this, TaskTimeoutQueue<TResult> timeoutQueue, Action<Task<TResult>> continuationAction)
        {
            if (@this == null || @this.IsCompleted || timeoutQueue == null)
                return @this;

            return new ValueTask<TResult>(timeoutQueue.Add(@this.AsTask(), continuationAction));
        }
    }
}
