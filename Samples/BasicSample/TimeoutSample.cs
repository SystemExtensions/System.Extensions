using System;
using System.Threading;
using System.Threading.Tasks;

namespace BasicSample
{
    public class TimeoutSample
    {
        private static async Task DoAsync()
        {
            Console.WriteLine("Start");
            await Task.Run(() =>
            {
                Thread.Sleep(new Random().Next(1000, 3000));
            });
            Console.WriteLine("End");
        }
        private static async Task<int> GetAsync()
        {
            Console.WriteLine("Start");
            await Task.Run(() =>
            {
                Thread.Sleep(new Random().Next(1000, 3000));
            });
            Console.WriteLine("End");
            return int.MaxValue;
        }

        public static async Task Run()
        {
            try
            {
                await DoAsync().Timeout(2000);
            }
            catch (TimeoutException)
            {
                Console.WriteLine("Timeout1");
            }
            try
            {
                await DoAsync().Timeout(2000, (task) =>
                {
                    Console.WriteLine("Continuation");
                });
            }
            catch (TimeoutException)
            {
                Console.WriteLine("Timeout2");
            }


            try
            {
                var value = await GetAsync().Timeout(2000);
            }
            catch (TimeoutException)
            {
                Console.WriteLine("Timeout3");
            }
            try
            {
                var value = await GetAsync().Timeout(2000, (task) =>
                 {
                     Console.WriteLine($"Continuation:{task.Result}");
                 });
            }
            catch (TimeoutException)
            {
                Console.WriteLine("Timeout4");
            }
        }


        private static TaskTimeoutQueue _Queue1 = new TaskTimeoutQueue(2000);
        private static TaskTimeoutQueue<int> _Queue2 = new TaskTimeoutQueue<int>(2000);
        public static async Task RunQueue()
        {
            try
            {
                await DoAsync().Timeout(_Queue1);
            }
            catch (TimeoutException)
            {
                Console.WriteLine("Timeout1");
            }
            try
            {
                await DoAsync().Timeout(_Queue1, (task) =>
                {
                    Console.WriteLine("Continuation");
                });
            }
            catch (TimeoutException)
            {
                Console.WriteLine("Timeout2");
            }


            try
            {
                var value = await GetAsync().Timeout(_Queue2);
            }
            catch (TimeoutException)
            {
                Console.WriteLine("Timeout3");
            }
            try
            {
                var value = await GetAsync().Timeout(_Queue2, (task) =>
                {
                    Console.WriteLine($"Continuation:{task.Result}");
                });
            }
            catch (TimeoutException)
            {
                Console.WriteLine("Timeout4");
            }
        }

        //timeout(2000-4000)
        private static TaskTimeoutQueue _LaxQueue1 = new TaskTimeoutQueue(-2000);
        private static TaskTimeoutQueue<int> _LaxQueue2 = new TaskTimeoutQueue<int>(-2000);
        public static async Task RunQueueLax()
        {
            try
            {
                await DoAsync().Timeout(_LaxQueue1);
            }
            catch (TimeoutException)
            {
                Console.WriteLine("Timeout1");
            }
            try
            {
                await DoAsync().Timeout(_LaxQueue1, (task) =>
                {
                    Console.WriteLine("Continuation");
                });
            }
            catch (TimeoutException)
            {
                Console.WriteLine("Timeout2");
            }


            try
            {
                var value = await GetAsync().Timeout(_LaxQueue2);
            }
            catch (TimeoutException)
            {
                Console.WriteLine("Timeout3");
            }
            try
            {
                var value = await GetAsync().Timeout(_LaxQueue2, (task) =>
                {
                    Console.WriteLine($"Continuation:{task.Result}");
                });
            }
            catch (TimeoutException)
            {
                Console.WriteLine("Timeout4");
            }
        }
    }
}