
namespace System.Collections.Concurrent
{
    using System.Diagnostics;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    public static class CollectionExtensions
    {
        public static int Count<TKey, TValue>(this Cache<TKey, TValue> @this)
        {
            var count = 0;
            @this.ForEach((key, value, expire) => count += 1);
            return count;
        }
        public static bool TryRemove<TKey, TValue>(this Cache<TKey, TValue> @this, TKey key)
        {
            return @this.TryRemove(key, out _);
        }

        //TODO??
        //public static int Count<TKey, TValue>(this Cache<TKey, TValue> @this, bool useSync)
        //{
        //    var count = 0;
        //    @this.ForEach((key, value, expire) => count += 1, useSync);
        //    return count;
        //}

        #region TODO? Redesign
        private static readonly HashSet<object> _Collect = new HashSet<object>();
        private class GcNotify<TKey, TValue> : IDisposable
        {
            //TODO? multiple Obj
            private int _generation;
            private Cache<TKey, TValue> _cache;
            public class Obj
            {
                private GcNotify<TKey, TValue> _gcNotify;
                private int _generation;
                public Obj(GcNotify<TKey, TValue> gcNotify)
                {
                    _gcNotify = gcNotify;
                    _generation = 0;
                }
                ~Obj()
                {
                    var gcNotify = _gcNotify;
                    if (gcNotify == null)
                        return;
                    var generation = gcNotify._generation;
                    var cache = gcNotify._cache;
                    if (cache == null)
                        return;
                    if (_generation > generation)
                        return;
                    if (_generation == generation)
                    {
                        Debug.WriteLine("GcNotify:Collect");
                        ThreadPool.QueueUserWorkItem((_) => cache.Collect());
                        new Obj(gcNotify);
                        return;
                    }
                    GC.ReRegisterForFinalize(this);
                    _generation = GC.GetGeneration(this);
                }
            }
            public GcNotify(Cache<TKey, TValue> cache, int generation)
            {
                _cache = cache;
                _generation = generation;
                new Obj(this);
            }
            public void Dispose()
            {
                _cache = null;
                lock (_Collect)
                {
                    _Collect.Remove(this);
                }
            }
        }
        private class TimerObj : IDisposable
        {
            private Timer _timer;
            public TimerObj(Timer timer) 
            {
                _timer = timer;
            }
            public void Dispose()
            {
                if (_timer == null)
                    return;
                var timer = _timer;
                _timer = null;
                timer.Dispose();
                lock (_Collect) 
                {
                    _Collect.Remove(this);
                }
            }
        }
        #endregion
        public static Cache<TKey, TValue> Collect<TKey, TValue>(this Cache<TKey, TValue> @this, int gcGeneration, out IDisposable disposable)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (gcGeneration > GC.MaxGeneration)
                throw new ArgumentOutOfRangeException(nameof(gcGeneration));

            var gcNotify = new GcNotify<TKey, TValue>(@this, gcGeneration);
            lock (_Collect)
            {
                _Collect.Add(gcNotify);
            }
            disposable = gcNotify;
            return @this;
        }
        public static Cache<TKey, TValue> Collect<TKey, TValue>(this Cache<TKey, TValue> @this, TimeSpan intervalTime, out IDisposable disposable)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            var timer = new Timer(
                 (state) =>
                 {
                     Debug.WriteLine("Timer=>Collect");
                     var cache = (Cache<TKey, TValue>)state;
                     ThreadPool.QueueUserWorkItem((_) => cache.Collect());
                 },
                 @this, intervalTime, intervalTime);
            var timerObj = new TimerObj(timer);
            lock (_Collect)
            {
                _Collect.Add(timerObj);
            }
            disposable = timerObj;
            return @this;
        }
        public static Task<TValue> GetOrAddAsync<TKey, TValue>(this Cache<TKey, Task<TValue>> @this, TKey key, Func<Task<TValue>> valueFactory, DateTimeOffset expire)
        {
            return @this.GetOrAdd(key, () => Task.Run(valueFactory), expire);
        }
        public static Task<TValue> GetOrAddAsync<TKey, TValue>(this Cache<TKey, Task<TValue>> @this, TKey key, Func<TValue> valueFactory, DateTimeOffset expire)
        {
            return @this.GetOrAdd(key, () => Task.Run(valueFactory), expire);
        }
    }
}
