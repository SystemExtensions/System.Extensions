
namespace System.Collections.Concurrent
{
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Collections.Generic;
    public class ProducerConsumerQueue<T>//(for TEST) TODO(Ring? Resize?)
    {
        private SpinLock _sync;
        private Queue<T> _producer;
        private Queue<TaskCompletionSource<T>> _consumer;
        public ProducerConsumerQueue() 
        {
            _sync = new SpinLock();
            _producer = new Queue<T>();
            _consumer = new Queue<TaskCompletionSource<T>>();
        }
        public void Enqueue(T item) 
        {
            var lockTaken = false;
            try
            {
                _sync.Enter(ref lockTaken);
                if (_consumer.TryDequeue(out var tcs))
                {
                    Debug.Assert(_producer.Count == 0);
                    tcs.TrySetResult(item);
                }
                else
                {

                    _producer.Enqueue(item);
                }
            }
            finally
            {
                _sync.Exit(false);
            }
        }
        public bool TryDequeue(out T result) 
        {
            var lockTaken = false;
            try
            {
                _sync.Enter(ref lockTaken);
                return _producer.TryDequeue(out result);
            }
            finally
            {
                _sync.Exit(false);
            }
        }
        public Task<T> WaitAsync() 
        {
            var lockTaken = false;
            try
            {
                _sync.Enter(ref lockTaken);
                if (_producer.TryDequeue(out var result))
                {
                    return Task.FromResult(result);
                }
                else
                {
                    var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
                    _consumer.Enqueue(tcs);
                    return tcs.Task;
                }
            }
            finally
            {
                _sync.Exit(false);
            }
        }
    }
}
