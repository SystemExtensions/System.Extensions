
namespace System.Collections.Concurrent
{
    using System.Threading.Tasks;
    public static class CollectionExtensions
    {
        public static bool TryAdd<TKey, TValue>(this Cache<TKey, TValue> @this, TKey key, TValue value, TimeSpan expire)
        {
            return @this.TryAdd(key, value, DateTimeOffset.Now.Add(expire));
        }
        public static bool TryAdd<TKey, TValue>(this Cache<TKey, TValue> @this, TKey key, Func<TValue> valueFactory, TimeSpan expire)
        {
            return @this.TryAdd(key, valueFactory, DateTimeOffset.Now.Add(expire));
        }
        public static bool TryUpdate<TKey, TValue>(this Cache<TKey, TValue> @this, TKey key, TimeSpan expire, out TValue value)
        {
            return @this.TryUpdate(key, DateTimeOffset.Now.Add(expire), out value);
        }
        public static bool TryUpdate<TKey, TValue>(this Cache<TKey, TValue> @this, TKey key, TValue newValue, TimeSpan expire, out TValue value)
        {
            return @this.TryUpdate(key, newValue, DateTimeOffset.Now.Add(expire), out value);
        }
        public static bool TryUpdate<TKey, TValue>(this Cache<TKey, TValue> @this, TKey key, Func<TValue, TValue> newValueFactory, TimeSpan expire, out TValue value)
        {
            return @this.TryUpdate(key, newValueFactory, DateTimeOffset.Now.Add(expire), out value);
        }
        public static TValue AddOrUpdate<TKey, TValue>(this Cache<TKey, TValue> @this, TKey key, TValue value, TimeSpan expire)
        {
            return @this.AddOrUpdate(key, value, DateTimeOffset.Now.Add(expire));
        }
        public static TValue AddOrUpdate<TKey, TValue>(this Cache<TKey, TValue> @this, TKey key, TValue value, TimeSpan expire, Func<TValue, TValue> newValueFactory)
        {
            return @this.AddOrUpdate(key, value, DateTimeOffset.Now.Add(expire), newValueFactory);
        }
        public static TValue GetOrAdd<TKey, TValue>(this Cache<TKey, TValue> @this, TKey key, TValue value, TimeSpan expire)
        {
            return @this.GetOrAdd(key, value, DateTimeOffset.Now.Add(expire));
        }
        public static TValue GetOrAdd<TKey, TValue>(this Cache<TKey, TValue> @this, TKey key, Func<TValue> valueFactory, TimeSpan expire)
        {
            return @this.GetOrAdd(key, valueFactory, DateTimeOffset.Now.Add(expire));
        }
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
        public static Task<TValue> GetOrAddAsync<TKey, TValue>(this Cache<TKey, Task<TValue>> @this, TKey key, Func<Task<TValue>> valueFactory, DateTimeOffset expire)
        {
            return @this.GetOrAdd(key, () => Task.Run(valueFactory), expire);
        }
        public static Task<TValue> GetOrAddAsync<TKey, TValue>(this Cache<TKey, Task<TValue>> @this, TKey key, Func<TValue> valueFactory, DateTimeOffset expire)
        {
            return @this.GetOrAdd(key, () => Task.Run(valueFactory), expire);
        }
        public static Task<TValue> GetOrAddAsync<TKey, TValue>(this Cache<TKey, Task<TValue>> @this, TKey key, Func<Task<TValue>> valueFactory, TimeSpan expire)
        {
            return @this.GetOrAdd(key, () => Task.Run(valueFactory), expire);
        }
        public static Task<TValue> GetOrAddAsync<TKey, TValue>(this Cache<TKey, Task<TValue>> @this, TKey key, Func<TValue> valueFactory, TimeSpan expire)
        {
            return @this.GetOrAdd(key, () => Task.Run(valueFactory), expire);
        }


        public static bool TryAdd<TValue>(this Cache<TValue> @this, TValue value, TimeSpan expire)
        {
            return @this.TryAdd(value, DateTimeOffset.Now.Add(expire));
        }
        public static bool TryAdd<TValue>(this Cache<TValue> @this, Func<TValue> valueFactory, TimeSpan expire)
        {
            return @this.TryAdd(valueFactory, DateTimeOffset.Now.Add(expire));
        }
        public static bool TryUpdate<TValue>(this Cache<TValue> @this, TimeSpan expire, out TValue value)
        {
            return @this.TryUpdate(DateTimeOffset.Now.Add(expire), out value);
        }
        public static bool TryUpdate<TValue>(this Cache<TValue> @this, TValue newValue, TimeSpan expire, out TValue value)
        {
            return @this.TryUpdate(newValue, DateTimeOffset.Now.Add(expire), out value);
        }
        public static bool TryUpdate<TValue>(this Cache<TValue> @this, Func<TValue, TValue> newValueFactory, TimeSpan expire, out TValue value)
        {
            return @this.TryUpdate(newValueFactory, DateTimeOffset.Now.Add(expire), out value);
        }
        public static TValue AddOrUpdate<TValue>(this Cache<TValue> @this, TValue value, TimeSpan expire)
        {
            return @this.AddOrUpdate(value, DateTimeOffset.Now.Add(expire));
        }
        public static TValue AddOrUpdate<TValue>(this Cache<TValue> @this, TValue value, TimeSpan expire, Func<TValue, TValue> newValueFactory)
        {
            return @this.AddOrUpdate(value, DateTimeOffset.Now.Add(expire), newValueFactory);
        }
        public static TValue GetOrAdd<TValue>(this Cache<TValue> @this, TValue value, TimeSpan expire)
        {
            return @this.GetOrAdd(value, DateTimeOffset.Now.Add(expire));
        }
        public static TValue GetOrAdd<TValue>(this Cache<TValue> @this, Func<TValue> valueFactory, TimeSpan expire)
        {
            return @this.GetOrAdd(valueFactory, DateTimeOffset.Now.Add(expire));
        }
        public static bool TryRemove<TValue>(this Cache<TValue> @this)
        {
            return @this.TryRemove(out _);
        }

        public static Task<TValue> GetOrAddAsync<TValue>(this Cache<Task<TValue>> @this, Func<Task<TValue>> valueFactory, DateTimeOffset expire)
        {
            return @this.GetOrAdd(() => Task.Run(valueFactory), expire);
        }
        public static Task<TValue> GetOrAddAsync<TValue>(this Cache<Task<TValue>> @this, Func<TValue> valueFactory, DateTimeOffset expire)
        {
            return @this.GetOrAdd(() => Task.Run(valueFactory), expire);
        }
        public static Task<TValue> GetOrAddAsync<TValue>(this Cache<Task<TValue>> @this, Func<Task<TValue>> valueFactory, TimeSpan expire)
        {
            return @this.GetOrAdd(() => Task.Run(valueFactory), expire);
        }
        public static Task<TValue> GetOrAddAsync<TValue>(this Cache<Task<TValue>> @this, Func<TValue> valueFactory, TimeSpan expire)
        {
            return @this.GetOrAdd(() => Task.Run(valueFactory), expire);
        }
    }
}
