using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace BasicSample
{
    public class CacheSample
    {
        private static Cache<int, string> _Cache1 = new Cache<int, string>(Environment.ProcessorCount, 1024, -1);//_Cache1.Collect()
        private static Cache<int, string> _Cache2 = new Cache<int, string>();//GC (1)
        public static void Run() 
        {
            _Cache1.TryAdd(1, "value1", DateTimeOffset.Now.AddMinutes(1));
            _Cache1.TryAdd(2, () => "value2", DateTimeOffset.Now.AddMinutes(2));
            _Cache1.TryAdd(3, () => ("value3", DateTimeOffset.Now.AddDays(3)));

            _Cache1.TryAdd(101, "value101", TimeSpan.FromMinutes(1));
            _Cache1.TryAdd(102, "value102", TimeSpan.FromMinutes(2));
            _Cache1.TryAdd(103, "value103", TimeSpan.FromMinutes(3));


            _Cache1.TryUpdate(1, "newValue1", out var oldValue1);
            _Cache1.TryUpdate(2, value1 => "newValue2", out var oldValue2);
            _Cache1.TryUpdate(3, DateTimeOffset.Now.AddDays(1), out var value3);
            _Cache1.TryUpdate(4, "newValue4", DateTimeOffset.Now.AddDays(2), out var oldValue4);
            _Cache1.TryUpdate(5, (value5) => "newValue5", DateTimeOffset.Now.AddDays(3), out var oldValue5);
            _Cache1.TryUpdate(6, (value5, expire5) => ("newValue6", DateTimeOffset.Now.AddDays(4)), out var oldValue6);


            _Cache1.AddOrUpdate(1, "value1", DateTimeOffset.Now.AddYears(1));
            _Cache1.AddOrUpdate(2, "newValue2", DateTimeOffset.Now.AddYears(2), (value2) => "updateValue2");
            _Cache1.AddOrUpdate(3, () => ("newValue2", DateTimeOffset.Now.AddYears(3)), (value3, expire3) => ("updateValue3", DateTimeOffset.Now.AddYears(3)));


            _Cache1.GetOrAdd(4, "Value4", DateTimeOffset.Now.AddSeconds(1));
            _Cache1.GetOrAdd(5, () => "Value5", DateTimeOffset.Now.AddSeconds(2));
            _Cache1.GetOrAdd(6, () => ("Value6", DateTimeOffset.Now.AddSeconds(3)));


            _Cache1.TryGetValue(1, out var getValue1);
            _Cache1.TryGetValue(2, out var getValue2);
            _Cache1.TryGetValue(3, out var getValue3);
            _Cache1.TryGetValue(4, out var getValue4);
            _Cache1.TryGetValue(5, out var getValue5);
            _Cache1.TryGetValue(6, out var getValue6);
            Console.WriteLine(getValue1);
            Console.WriteLine(getValue2);
            Console.WriteLine(getValue3);
            Console.WriteLine(getValue4);
            Console.WriteLine(getValue5);
            Console.WriteLine(getValue6);


            _Cache1.ForEach((key, value, expire) => {
                Console.WriteLine($"{key}={value},{expire}");
            });

            _Cache1.TryRemove(1, out var oldValue11);

            _Cache1.Collect();
            //_Cache1.Clear();
        }

        private static Cache<string, Task<string>> _Cache4 = new Cache<string, Task<string>>();
        public static async Task RunDataAsync() 
        {
            var key = "USERLIST";
            var value1= await _Cache4.GetOrAddAsync(key,
                async () => {
                    return await GetDataAsync();
                }, DateTimeOffset.Now.AddMinutes(10));

            Console.WriteLine(value1);


            var value2 = await _Cache4.GetOrAddAsync(key,
               async () => {
                   return await GetDataAsync();
               }, DateTimeOffset.Now.AddMinutes(10));

            Console.WriteLine(value2);
        }
        private static async Task<string> GetDataAsync() 
        {
            Console.WriteLine("FROM DB");
            await Task.Delay(1000);

            return "Data";
        }
    }
}
