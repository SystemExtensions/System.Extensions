
namespace System
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    public static class Executor
    {
        //TODO?? T5 T6 T7....
        public static void Retry(int maxRetry, Action action, Action<AggregateException> handler = null)
        {
            if (maxRetry < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetry));
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            List<Exception> innerExceptions;
            try
            {
                action();
                return;
            }
            catch (Exception ex)
            {
                innerExceptions = new List<Exception>() { ex };
                if (handler != null)
                {
                    handler.Invoke(new AggregateException(innerExceptions));
                }
            }

            for (int i = 1; i <= maxRetry; i++)
            {
                try
                {
                    action();
                    return;
                }
                catch (Exception ex)
                {
                    innerExceptions.Add(ex);
                    if (handler != null)
                    {
                        handler.Invoke(new AggregateException(innerExceptions));
                    }
                }
            }
            throw new AggregateException("MaxRetry", innerExceptions);
        }
        public static void Retry<T>(int maxRetry, Action<T> action, T arg, Action<AggregateException> handler = null)
        {
            if (maxRetry < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetry));
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            List<Exception> innerExceptions;
            try
            {
                action(arg);
                return;
            }
            catch (Exception ex)
            {
                innerExceptions = new List<Exception>() { ex };
                if (handler != null)
                {
                    handler.Invoke(new AggregateException(innerExceptions));
                }
            }

            for (int i = 1; i <= maxRetry; i++)
            {
                try
                {
                    action(arg);
                    return;
                }
                catch (Exception ex)
                {
                    innerExceptions.Add(ex);
                    if (handler != null)
                    {
                        handler.Invoke(new AggregateException(innerExceptions));
                    }
                }
            }
            throw new AggregateException("MaxRetry", innerExceptions);
        }
        public static void Retry<T1, T2>(int maxRetry, Action<T1, T2> action, T1 arg1, T2 arg2, Action<AggregateException> handler = null)
        {
            if (maxRetry < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetry));
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            List<Exception> innerExceptions;
            try
            {
                action(arg1, arg2);
                return;
            }
            catch (Exception ex)
            {
                innerExceptions = new List<Exception>() { ex };
                if (handler != null)
                {
                    handler.Invoke(new AggregateException(innerExceptions));
                }
            }

            for (int i = 1; i <= maxRetry; i++)
            {
                try
                {
                    action(arg1, arg2);
                    return;
                }
                catch (Exception ex)
                {
                    innerExceptions.Add(ex);
                    if (handler != null)
                    {
                        handler.Invoke(new AggregateException(innerExceptions));
                    }
                }
            }
            throw new AggregateException("MaxRetry", innerExceptions);
        }
        public static void Retry<T1, T2, T3>(int maxRetry, Action<T1, T2, T3> action, T1 arg1, T2 arg2, T3 arg3, Action<AggregateException> handler = null)
        {
            if (maxRetry < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetry));
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            List<Exception> innerExceptions;
            try
            {
                action(arg1, arg2, arg3);
                return;
            }
            catch (Exception ex)
            {
                innerExceptions = new List<Exception>() { ex };
                if (handler != null)
                {
                    handler.Invoke(new AggregateException(innerExceptions));
                }
            }

            for (int i = 1; i <= maxRetry; i++)
            {
                try
                {
                    action(arg1, arg2, arg3);
                    return;
                }
                catch (Exception ex)
                {
                    innerExceptions.Add(ex);
                    if (handler != null)
                    {
                        handler.Invoke(new AggregateException(innerExceptions));
                    }
                }
            }
            throw new AggregateException("MaxRetry", innerExceptions);
        }
        public static void Retry<T1, T2, T3, T4>(int maxRetry, Action<T1, T2, T3, T4> action, T1 arg1, T2 arg2, T3 arg3, T4 arg4, Action<AggregateException> handler = null)
        {
            if (maxRetry < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetry));
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            List<Exception> innerExceptions;
            try
            {
                action(arg1, arg2, arg3, arg4);
                return;
            }
            catch (Exception ex)
            {
                innerExceptions = new List<Exception>() { ex };
                if (handler != null)
                {
                    handler.Invoke(new AggregateException(innerExceptions));
                }
            }

            for (int i = 1; i <= maxRetry; i++)
            {
                try
                {
                    action(arg1, arg2, arg3, arg4);
                    return;
                }
                catch (Exception ex)
                {
                    innerExceptions.Add(ex);
                    if (handler != null)
                    {
                        handler.Invoke(new AggregateException(innerExceptions));
                    }
                }
            }
            throw new AggregateException("MaxRetry", innerExceptions);
        }
        public static async Task Retry(int maxRetry, Action action, Func<AggregateException, Task> handler)
        {
            if (maxRetry < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetry));
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            List<Exception> innerExceptions;
            try
            {
                action();
                return;
            }
            catch (Exception ex)
            {
                innerExceptions = new List<Exception>() { ex };
                if (handler != null)
                {
                    await handler.Invoke(new AggregateException(innerExceptions));
                }
            }

            for (int i = 1; i <= maxRetry; i++)
            {
                try
                {
                    action();
                    return;
                }
                catch (Exception ex)
                {
                    innerExceptions.Add(ex);
                    if (handler != null)
                    {
                        await handler.Invoke(new AggregateException(innerExceptions));
                    }
                }
            }
            throw new AggregateException("MaxRetry", innerExceptions);
        }
        public static async Task Retry<T>(int maxRetry, Action<T> action, T arg, Func<AggregateException, Task> handler)
        {
            if (maxRetry < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetry));
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            List<Exception> innerExceptions;
            try
            {
                action(arg);
                return;
            }
            catch (Exception ex)
            {
                innerExceptions = new List<Exception>() { ex };
                if (handler != null)
                {
                    await handler.Invoke(new AggregateException(innerExceptions));
                }
            }

            for (int i = 1; i <= maxRetry; i++)
            {
                try
                {
                    action(arg);
                    return;
                }
                catch (Exception ex)
                {
                    innerExceptions.Add(ex);
                    if (handler != null)
                    {
                        await handler.Invoke(new AggregateException(innerExceptions));
                    }
                }
            }
            throw new AggregateException("MaxRetry", innerExceptions);
        }
        public static async Task Retry<T1, T2>(int maxRetry, Action<T1, T2> action, T1 arg1, T2 arg2, Func<AggregateException, Task> handler)
        {
            if (maxRetry < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetry));
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            List<Exception> innerExceptions;
            try
            {
                action(arg1, arg2);
                return;
            }
            catch (Exception ex)
            {
                innerExceptions = new List<Exception>() { ex };
                if (handler != null)
                {
                    await handler.Invoke(new AggregateException(innerExceptions));
                }
            }

            for (int i = 1; i <= maxRetry; i++)
            {
                try
                {
                    action(arg1, arg2);
                    return;
                }
                catch (Exception ex)
                {
                    innerExceptions.Add(ex);
                    if (handler != null)
                    {
                        await handler.Invoke(new AggregateException(innerExceptions));
                    }
                }
            }
            throw new AggregateException("MaxRetry", innerExceptions);
        }
        public static async Task Retry<T1, T2, T3>(int maxRetry, Action<T1, T2, T3> action, T1 arg1, T2 arg2, T3 arg3, Func<AggregateException, Task> handler)
        {
            if (maxRetry < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetry));
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            List<Exception> innerExceptions;
            try
            {
                action(arg1, arg2, arg3);
                return;
            }
            catch (Exception ex)
            {
                innerExceptions = new List<Exception>() { ex };
                if (handler != null)
                {
                    await handler.Invoke(new AggregateException(innerExceptions));
                }
            }

            for (int i = 1; i <= maxRetry; i++)
            {
                try
                {
                    action(arg1, arg2, arg3);
                    return;
                }
                catch (Exception ex)
                {
                    innerExceptions.Add(ex);
                    if (handler != null)
                    {
                        await handler.Invoke(new AggregateException(innerExceptions));
                    }
                }
            }
            throw new AggregateException("MaxRetry", innerExceptions);
        }
        public static async Task Retry<T1, T2, T3, T4>(int maxRetry, Action<T1, T2, T3, T4> action, T1 arg1, T2 arg2, T3 arg3, T4 arg4, Func<AggregateException, Task> handler)
        {
            if (maxRetry < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetry));
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            List<Exception> innerExceptions;
            try
            {
                action(arg1, arg2, arg3, arg4);
                return;
            }
            catch (Exception ex)
            {
                innerExceptions = new List<Exception>() { ex };
                if (handler != null)
                {
                    await handler.Invoke(new AggregateException(innerExceptions));
                }
            }

            for (int i = 1; i <= maxRetry; i++)
            {
                try
                {
                    action(arg1, arg2, arg3, arg4);
                    return;
                }
                catch (Exception ex)
                {
                    innerExceptions.Add(ex);
                    if (handler != null)
                    {
                        await handler.Invoke(new AggregateException(innerExceptions));
                    }
                }
            }
            throw new AggregateException("MaxRetry", innerExceptions);
        }

        //???? GetAwaiter
        public static TResult Retry<TResult>(int maxRetry, Func<TResult> func, Action<AggregateException> handler = null)
        {
            if (maxRetry < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetry));
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            List<Exception> innerExceptions;
            try
            {
                return func();
            }
            catch (Exception ex)
            {
                innerExceptions = new List<Exception>() { ex };
                if (handler != null)
                {
                    handler.Invoke(new AggregateException(innerExceptions));
                }
            }

            for (int i = 1; i <= maxRetry; i++)
            {
                try
                {
                    return func();
                }
                catch (Exception ex)
                {
                    innerExceptions.Add(ex);
                    if (handler != null)
                    {
                        handler.Invoke(new AggregateException(innerExceptions));
                    }
                }
            }
            throw new AggregateException("MaxRetry", innerExceptions);
        }
        public static TResult Retry<T, TResult>(int maxRetry, Func<T, TResult> func, T arg, Action<AggregateException> handler = null)
        {
            if (maxRetry < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetry));
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            List<Exception> innerExceptions;
            try
            {
                return func(arg);
            }
            catch (Exception ex)
            {
                innerExceptions = new List<Exception>() { ex };
                if (handler != null)
                {
                    handler.Invoke(new AggregateException(innerExceptions));
                }
            }

            for (int i = 1; i <= maxRetry; i++)
            {
                try
                {
                    return func(arg);
                }
                catch (Exception ex)
                {
                    innerExceptions.Add(ex);
                    if (handler != null)
                    {
                        handler.Invoke(new AggregateException(innerExceptions));
                    }
                }
            }
            throw new AggregateException("MaxRetry", innerExceptions);
        }
        public static TResult Retry<T1, T2, TResult>(int maxRetry, Func<T1, T2, TResult> func, T1 arg1, T2 arg2, Action<AggregateException> handler = null)
        {
            if (maxRetry < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetry));
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            List<Exception> innerExceptions;
            try
            {
                return func(arg1, arg2);
            }
            catch (Exception ex)
            {
                innerExceptions = new List<Exception>() { ex };
                if (handler != null)
                {
                    handler.Invoke(new AggregateException(innerExceptions));
                }
            }

            for (int i = 1; i <= maxRetry; i++)
            {
                try
                {
                    return func(arg1, arg2);
                }
                catch (Exception ex)
                {
                    innerExceptions.Add(ex);
                    if (handler != null)
                    {
                        handler.Invoke(new AggregateException(innerExceptions));
                    }
                }
            }
            throw new AggregateException("MaxRetry", innerExceptions);
        }
        public static TResult Retry<T1, T2, T3, TResult>(int maxRetry, Func<T1, T2, T3, TResult> func, T1 arg1, T2 arg2, T3 arg3, Action<AggregateException> handler = null)
        {
            if (maxRetry < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetry));
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            List<Exception> innerExceptions;
            try
            {
                return func(arg1, arg2, arg3);
            }
            catch (Exception ex)
            {
                innerExceptions = new List<Exception>() { ex };
                if (handler != null)
                {
                    handler.Invoke(new AggregateException(innerExceptions));
                }
            }

            for (int i = 1; i <= maxRetry; i++)
            {
                try
                {
                    return func(arg1, arg2, arg3);
                }
                catch (Exception ex)
                {
                    innerExceptions.Add(ex);
                    if (handler != null)
                    {
                        handler.Invoke(new AggregateException(innerExceptions));
                    }
                }
            }
            throw new AggregateException("MaxRetry", innerExceptions);
        }
        public static TResult Retry<T1, T2, T3, T4, TResult>(int maxRetry, Func<T1, T2, T3, T4, TResult> func, T1 arg1, T2 arg2, T3 arg3, T4 arg4, Action<AggregateException> handler = null)
        {
            if (maxRetry < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetry));
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            List<Exception> innerExceptions;
            try
            {
                return func(arg1, arg2, arg3, arg4);
            }
            catch (Exception ex)
            {
                innerExceptions = new List<Exception>() { ex };
                if (handler != null)
                {
                    handler.Invoke(new AggregateException(innerExceptions));
                }
            }

            for (int i = 1; i <= maxRetry; i++)
            {
                try
                {
                    return func(arg1, arg2, arg3, arg4);
                }
                catch (Exception ex)
                {
                    innerExceptions.Add(ex);
                    if (handler != null)
                    {
                        handler.Invoke(new AggregateException(innerExceptions));
                    }
                }
            }
            throw new AggregateException("MaxRetry", innerExceptions);
        }

        public static async Task<TResult> Retry<TResult>(int maxRetry, Func<TResult> func, Func<AggregateException, Task> handler)
        {
            if (maxRetry < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetry));
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            List<Exception> innerExceptions;
            try
            {
                return func();
            }
            catch (Exception ex)
            {
                innerExceptions = new List<Exception>() { ex };
                if (handler != null)
                {
                    await handler.Invoke(new AggregateException(innerExceptions));
                }
            }

            for (int i = 1; i <= maxRetry; i++)
            {
                try
                {
                    return func();
                }
                catch (Exception ex)
                {
                    innerExceptions.Add(ex);
                    if (handler != null)
                    {
                        await handler.Invoke(new AggregateException(innerExceptions));
                    }
                }
            }
            throw new AggregateException("MaxRetry", innerExceptions);
        }
        public static async Task<TResult> Retry<T, TResult>(int maxRetry, Func<T, TResult> func, T arg, Func<AggregateException, Task> handler)
        {
            if (maxRetry < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetry));
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            List<Exception> innerExceptions;
            try
            {
                return func(arg);
            }
            catch (Exception ex)
            {
                innerExceptions = new List<Exception>() { ex };
                if (handler != null)
                {
                    await handler.Invoke(new AggregateException(innerExceptions));
                }
            }

            for (int i = 1; i <= maxRetry; i++)
            {
                try
                {
                    return func(arg);
                }
                catch (Exception ex)
                {
                    innerExceptions.Add(ex);
                    if (handler != null)
                    {
                        await handler.Invoke(new AggregateException(innerExceptions));
                    }
                }
            }
            throw new AggregateException("MaxRetry", innerExceptions);
        }
        public static async Task<TResult> Retry<T1, T2, TResult>(int maxRetry, Func<T1, T2, TResult> func, T1 arg1, T2 arg2, Func<AggregateException, Task> handler)
        {
            if (maxRetry < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetry));
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            List<Exception> innerExceptions;
            try
            {
                return func(arg1, arg2);
            }
            catch (Exception ex)
            {
                innerExceptions = new List<Exception>() { ex };
                if (handler != null)
                {
                    await handler.Invoke(new AggregateException(innerExceptions));
                }
            }

            for (int i = 1; i <= maxRetry; i++)
            {
                try
                {
                    return func(arg1, arg2);
                }
                catch (Exception ex)
                {
                    innerExceptions.Add(ex);
                    if (handler != null)
                    {
                        await handler.Invoke(new AggregateException(innerExceptions));
                    }
                }
            }
            throw new AggregateException("MaxRetry", innerExceptions);
        }
        public static async Task<TResult> Retry<T1, T2, T3, TResult>(int maxRetry, Func<T1, T2, T3, TResult> func, T1 arg1, T2 arg2, T3 arg3, Func<AggregateException, Task> handler)
        {
            if (maxRetry < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetry));
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            List<Exception> innerExceptions;
            try
            {
                return func(arg1, arg2, arg3);
            }
            catch (Exception ex)
            {
                innerExceptions = new List<Exception>() { ex };
                if (handler != null)
                {
                    await handler.Invoke(new AggregateException(innerExceptions));
                }
            }

            for (int i = 1; i <= maxRetry; i++)
            {
                try
                {
                    return func(arg1, arg2, arg3);
                }
                catch (Exception ex)
                {
                    innerExceptions.Add(ex);
                    if (handler != null)
                    {
                        await handler.Invoke(new AggregateException(innerExceptions));
                    }
                }
            }
            throw new AggregateException("MaxRetry", innerExceptions);
        }
        public static async Task<TResult> Retry<T1, T2, T3, T4, TResult>(int maxRetry, Func<T1, T2, T3, T4, TResult> func, T1 arg1, T2 arg2, T3 arg3, T4 arg4, Func<AggregateException, Task> handler)
        {
            if (maxRetry < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetry));
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            List<Exception> innerExceptions;
            try
            {
                return func(arg1, arg2, arg3, arg4);
            }
            catch (Exception ex)
            {
                innerExceptions = new List<Exception>() { ex };
                if (handler != null)
                {
                    await handler.Invoke(new AggregateException(innerExceptions));
                }
            }

            for (int i = 1; i <= maxRetry; i++)
            {
                try
                {
                    return func(arg1, arg2, arg3, arg4);
                }
                catch (Exception ex)
                {
                    innerExceptions.Add(ex);
                    if (handler != null)
                    {
                        await handler.Invoke(new AggregateException(innerExceptions));
                    }
                }
            }
            throw new AggregateException("MaxRetry", innerExceptions);
        }

        //Task
        public static async Task<TResult> Retry<TResult>(int maxRetry, Func<Task<TResult>> func, Action<AggregateException> handler = null)
        {
            if (maxRetry < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetry));
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            List<Exception> innerExceptions;
            try
            {
                return await func();
            }
            catch (Exception ex)
            {
                innerExceptions = new List<Exception>() { ex };
                if (handler != null)
                {
                    handler.Invoke(new AggregateException(innerExceptions));
                }
            }

            for (int i = 1; i <= maxRetry; i++)
            {
                try
                {
                    return await func();
                }
                catch (Exception ex)
                {
                    innerExceptions.Add(ex);
                    if (handler != null)
                    {
                        handler.Invoke(new AggregateException(innerExceptions));
                    }
                }
            }
            throw new AggregateException("MaxRetry", innerExceptions);
        }
        public static async Task<TResult> Retry<T, TResult>(int maxRetry, Func<T, Task<TResult>> func, T arg, Action<AggregateException> handler = null)
        {
            if (maxRetry < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetry));
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            List<Exception> innerExceptions;
            try
            {
                return await func(arg);
            }
            catch (Exception ex)
            {
                innerExceptions = new List<Exception>() { ex };
                if (handler != null)
                {
                    handler.Invoke(new AggregateException(innerExceptions));
                }
            }

            for (int i = 1; i <= maxRetry; i++)
            {
                try
                {
                    return await func(arg);
                }
                catch (Exception ex)
                {
                    innerExceptions.Add(ex);
                    if (handler != null)
                    {
                        handler.Invoke(new AggregateException(innerExceptions));
                    }
                }
            }
            throw new AggregateException("MaxRetry", innerExceptions);
        }
        public static async Task<TResult> Retry<T1, T2, TResult>(int maxRetry, Func<T1, T2, Task<TResult>> func, T1 arg1, T2 arg2, Action<AggregateException> handler = null)
        {
            if (maxRetry < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetry));
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            List<Exception> innerExceptions;
            try
            {
                return await func(arg1, arg2);
            }
            catch (Exception ex)
            {
                innerExceptions = new List<Exception>() { ex };
                if (handler != null)
                {
                    handler.Invoke(new AggregateException(innerExceptions));
                }
            }

            for (int i = 1; i <= maxRetry; i++)
            {
                try
                {
                    return await func(arg1, arg2);
                }
                catch (Exception ex)
                {
                    innerExceptions.Add(ex);
                    if (handler != null)
                    {
                        handler.Invoke(new AggregateException(innerExceptions));
                    }
                }
            }
            throw new AggregateException("MaxRetry", innerExceptions);
        }
        public static async Task<TResult> Retry<T1, T2, T3, TResult>(int maxRetry, Func<T1, T2, T3, Task<TResult>> func, T1 arg1, T2 arg2, T3 arg3, Action<AggregateException> handler = null)
        {
            if (maxRetry < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetry));
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            List<Exception> innerExceptions;
            try
            {
                return await func(arg1, arg2, arg3);
            }
            catch (Exception ex)
            {
                innerExceptions = new List<Exception>() { ex };
                if (handler != null)
                {
                    handler.Invoke(new AggregateException(innerExceptions));
                }
            }

            for (int i = 1; i <= maxRetry; i++)
            {
                try
                {
                    return await func(arg1, arg2, arg3);
                }
                catch (Exception ex)
                {
                    innerExceptions.Add(ex);
                    if (handler != null)
                    {
                        handler.Invoke(new AggregateException(innerExceptions));
                    }
                }
            }
            throw new AggregateException("MaxRetry", innerExceptions);
        }
        public static async Task<TResult> Retry<T1, T2, T3, T4, TResult>(int maxRetry, Func<T1, T2, T3, T4, Task<TResult>> func, T1 arg1, T2 arg2, T3 arg3, T4 arg4, Action<AggregateException> handler = null)
        {
            if (maxRetry < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetry));
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            List<Exception> innerExceptions;
            try
            {
                return await func(arg1, arg2, arg3, arg4);
            }
            catch (Exception ex)
            {
                innerExceptions = new List<Exception>() { ex };
                if (handler != null)
                {
                    handler.Invoke(new AggregateException(innerExceptions));
                }
            }

            for (int i = 1; i <= maxRetry; i++)
            {
                try
                {
                    return await func(arg1, arg2, arg3, arg4);
                }
                catch (Exception ex)
                {
                    innerExceptions.Add(ex);
                    if (handler != null)
                    {
                        handler.Invoke(new AggregateException(innerExceptions));
                    }
                }
            }
            throw new AggregateException("MaxRetry", innerExceptions);
        }

        public static async Task<TResult> Retry<TResult>(int maxRetry, Func<Task<TResult>> func, Func<AggregateException, Task> handler)
        {
            if (maxRetry < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetry));
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            List<Exception> innerExceptions;
            try
            {
                return await func();
            }
            catch (Exception ex)
            {
                innerExceptions = new List<Exception>() { ex };
                if (handler != null)
                {
                    await handler.Invoke(new AggregateException(innerExceptions));
                }
            }

            for (int i = 1; i <= maxRetry; i++)
            {
                try
                {
                    return await func();
                }
                catch (Exception ex)
                {
                    innerExceptions.Add(ex);
                    if (handler != null)
                    {
                        await handler.Invoke(new AggregateException(innerExceptions));
                    }
                }
            }
            throw new AggregateException("MaxRetry", innerExceptions);
        }
        public static async Task<TResult> Retry<T, TResult>(int maxRetry, Func<T, Task<TResult>> func, T arg, Func<AggregateException, Task> handler)
        {
            if (maxRetry < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetry));
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            List<Exception> innerExceptions;
            try
            {
                return await func(arg);
            }
            catch (Exception ex)
            {
                innerExceptions = new List<Exception>() { ex };
                if (handler != null)
                {
                    await handler.Invoke(new AggregateException(innerExceptions));
                }
            }

            for (int i = 1; i <= maxRetry; i++)
            {
                try
                {
                    return await func(arg);
                }
                catch (Exception ex)
                {
                    innerExceptions.Add(ex);
                    if (handler != null)
                    {
                        await handler.Invoke(new AggregateException(innerExceptions));
                    }
                }
            }
            throw new AggregateException("MaxRetry", innerExceptions);
        }
        public static async Task<TResult> Retry<T1, T2, TResult>(int maxRetry, Func<T1, T2, Task<TResult>> func, T1 arg1, T2 arg2, Func<AggregateException, Task> handler)
        {
            if (maxRetry < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetry));
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            List<Exception> innerExceptions;
            try
            {
                return await func(arg1, arg2);
            }
            catch (Exception ex)
            {
                innerExceptions = new List<Exception>() { ex };
                if (handler != null)
                {
                    await handler.Invoke(new AggregateException(innerExceptions));
                }
            }

            for (int i = 1; i <= maxRetry; i++)
            {
                try
                {
                    return await func(arg1, arg2);
                }
                catch (Exception ex)
                {
                    innerExceptions.Add(ex);
                    if (handler != null)
                    {
                        await handler.Invoke(new AggregateException(innerExceptions));
                    }
                }
            }
            throw new AggregateException("MaxRetry", innerExceptions);
        }
        public static async Task<TResult> Retry<T1, T2, T3, TResult>(int maxRetry, Func<T1, T2, T3, Task<TResult>> func, T1 arg1, T2 arg2, T3 arg3, Func<AggregateException, Task> handler)
        {
            if (maxRetry < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetry));
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            List<Exception> innerExceptions;
            try
            {
                return await func(arg1, arg2, arg3);
            }
            catch (Exception ex)
            {
                innerExceptions = new List<Exception>() { ex };
                if (handler != null)
                {
                    await handler.Invoke(new AggregateException(innerExceptions));
                }
            }

            for (int i = 1; i <= maxRetry; i++)
            {
                try
                {
                    return await func(arg1, arg2, arg3);
                }
                catch (Exception ex)
                {
                    innerExceptions.Add(ex);
                    if (handler != null)
                    {
                        await handler.Invoke(new AggregateException(innerExceptions));
                    }
                }
            }
            throw new AggregateException("MaxRetry", innerExceptions);
        }
        public static async Task<TResult> Retry<T1, T2, T3, T4, TResult>(int maxRetry, Func<T1, T2, T3, T4, Task<TResult>> func, T1 arg1, T2 arg2, T3 arg3, T4 arg4, Func<AggregateException, Task> handler)
        {
            if (maxRetry < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetry));
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            List<Exception> innerExceptions;
            try
            {
                return await func(arg1, arg2, arg3, arg4);
            }
            catch (Exception ex)
            {
                innerExceptions = new List<Exception>() { ex };
                if (handler != null)
                {
                    await handler.Invoke(new AggregateException(innerExceptions));
                }
            }

            for (int i = 1; i <= maxRetry; i++)
            {
                try
                {
                    return await func(arg1, arg2, arg3, arg4);
                }
                catch (Exception ex)
                {
                    innerExceptions.Add(ex);
                    if (handler != null)
                    {
                        await handler.Invoke(new AggregateException(innerExceptions));
                    }
                }
            }
            throw new AggregateException("MaxRetry", innerExceptions);
        }

        //ValueTask
        public static async Task<TResult> Retry<TResult>(int maxRetry, Func<ValueTask<TResult>> func, Action<AggregateException> handler = null)
        {
            if (maxRetry < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetry));
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            List<Exception> innerExceptions;
            try
            {
                return await func();
            }
            catch (Exception ex)
            {
                innerExceptions = new List<Exception>() { ex };
                if (handler != null)
                {
                    handler.Invoke(new AggregateException(innerExceptions));
                }
            }

            for (int i = 1; i <= maxRetry; i++)
            {
                try
                {
                    return await func();
                }
                catch (Exception ex)
                {
                    innerExceptions.Add(ex);
                    if (handler != null)
                    {
                        handler.Invoke(new AggregateException(innerExceptions));
                    }
                }
            }
            throw new AggregateException("MaxRetry", innerExceptions);
        }
        public static async Task<TResult> Retry<T, TResult>(int maxRetry, Func<T, ValueTask<TResult>> func, T arg, Action<AggregateException> handler = null)
        {
            if (maxRetry < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetry));
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            List<Exception> innerExceptions;
            try
            {
                return await func(arg);
            }
            catch (Exception ex)
            {
                innerExceptions = new List<Exception>() { ex };
                if (handler != null)
                {
                    handler.Invoke(new AggregateException(innerExceptions));
                }
            }

            for (int i = 1; i <= maxRetry; i++)
            {
                try
                {
                    return await func(arg);
                }
                catch (Exception ex)
                {
                    innerExceptions.Add(ex);
                    if (handler != null)
                    {
                        handler.Invoke(new AggregateException(innerExceptions));
                    }
                }
            }
            throw new AggregateException("MaxRetry", innerExceptions);
        }
        public static async Task<TResult> Retry<T1, T2, TResult>(int maxRetry, Func<T1, T2, ValueTask<TResult>> func, T1 arg1, T2 arg2, Action<AggregateException> handler = null)
        {
            if (maxRetry < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetry));
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            List<Exception> innerExceptions;
            try
            {
                return await func(arg1, arg2);
            }
            catch (Exception ex)
            {
                innerExceptions = new List<Exception>() { ex };
                if (handler != null)
                {
                    handler.Invoke(new AggregateException(innerExceptions));
                }
            }

            for (int i = 1; i <= maxRetry; i++)
            {
                try
                {
                    return await func(arg1, arg2);
                }
                catch (Exception ex)
                {
                    innerExceptions.Add(ex);
                    if (handler != null)
                    {
                        handler.Invoke(new AggregateException(innerExceptions));
                    }
                }
            }
            throw new AggregateException("MaxRetry", innerExceptions);
        }
        public static async Task<TResult> Retry<T1, T2, T3, TResult>(int maxRetry, Func<T1, T2, T3, ValueTask<TResult>> func, T1 arg1, T2 arg2, T3 arg3, Action<AggregateException> handler = null)
        {
            if (maxRetry < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetry));
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            List<Exception> innerExceptions;
            try
            {
                return await func(arg1, arg2, arg3);
            }
            catch (Exception ex)
            {
                innerExceptions = new List<Exception>() { ex };
                if (handler != null)
                {
                    handler.Invoke(new AggregateException(innerExceptions));
                }
            }

            for (int i = 1; i <= maxRetry; i++)
            {
                try
                {
                    return await func(arg1, arg2, arg3);
                }
                catch (Exception ex)
                {
                    innerExceptions.Add(ex);
                    if (handler != null)
                    {
                        handler.Invoke(new AggregateException(innerExceptions));
                    }
                }
            }
            throw new AggregateException("MaxRetry", innerExceptions);
        }
        public static async Task<TResult> Retry<T1, T2, T3, T4, TResult>(int maxRetry, Func<T1, T2, T3, T4, ValueTask<TResult>> func, T1 arg1, T2 arg2, T3 arg3, T4 arg4, Action<AggregateException> handler = null)
        {
            if (maxRetry < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetry));
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            List<Exception> innerExceptions;
            try
            {
                return await func(arg1, arg2, arg3, arg4);
            }
            catch (Exception ex)
            {
                innerExceptions = new List<Exception>() { ex };
                if (handler != null)
                {
                    handler.Invoke(new AggregateException(innerExceptions));
                }
            }

            for (int i = 1; i <= maxRetry; i++)
            {
                try
                {
                    return await func(arg1, arg2, arg3, arg4);
                }
                catch (Exception ex)
                {
                    innerExceptions.Add(ex);
                    if (handler != null)
                    {
                        handler.Invoke(new AggregateException(innerExceptions));
                    }
                }
            }
            throw new AggregateException("MaxRetry", innerExceptions);
        }

        public static async Task<TResult> Retry<TResult>(int maxRetry, Func<ValueTask<TResult>> func, Func<AggregateException, Task> handler)
        {
            if (maxRetry < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetry));
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            List<Exception> innerExceptions;
            try
            {
                return await func();
            }
            catch (Exception ex)
            {
                innerExceptions = new List<Exception>() { ex };
                if (handler != null)
                {
                    await handler.Invoke(new AggregateException(innerExceptions));
                }
            }

            for (int i = 1; i <= maxRetry; i++)
            {
                try
                {
                    return await func();
                }
                catch (Exception ex)
                {
                    innerExceptions.Add(ex);
                    if (handler != null)
                    {
                        await handler.Invoke(new AggregateException(innerExceptions));
                    }
                }
            }
            throw new AggregateException("MaxRetry", innerExceptions);
        }
        public static async Task<TResult> Retry<T, TResult>(int maxRetry, Func<T, ValueTask<TResult>> func, T arg, Func<AggregateException, Task> handler)
        {
            if (maxRetry < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetry));
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            List<Exception> innerExceptions;
            try
            {
                return await func(arg);
            }
            catch (Exception ex)
            {
                innerExceptions = new List<Exception>() { ex };
                if (handler != null)
                {
                    await handler.Invoke(new AggregateException(innerExceptions));
                }
            }

            for (int i = 1; i <= maxRetry; i++)
            {
                try
                {
                    return await func(arg);
                }
                catch (Exception ex)
                {
                    innerExceptions.Add(ex);
                    if (handler != null)
                    {
                        await handler.Invoke(new AggregateException(innerExceptions));
                    }
                }
            }
            throw new AggregateException("MaxRetry", innerExceptions);
        }
        public static async Task<TResult> Retry<T1, T2, TResult>(int maxRetry, Func<T1, T2, ValueTask<TResult>> func, T1 arg1, T2 arg2, Func<AggregateException, Task> handler)
        {
            if (maxRetry < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetry));
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            List<Exception> innerExceptions;
            try
            {
                return await func(arg1, arg2);
            }
            catch (Exception ex)
            {
                innerExceptions = new List<Exception>() { ex };
                if (handler != null)
                {
                    await handler.Invoke(new AggregateException(innerExceptions));
                }
            }

            for (int i = 1; i <= maxRetry; i++)
            {
                try
                {
                    return await func(arg1, arg2);
                }
                catch (Exception ex)
                {
                    innerExceptions.Add(ex);
                    if (handler != null)
                    {
                        await handler.Invoke(new AggregateException(innerExceptions));
                    }
                }
            }
            throw new AggregateException("MaxRetry", innerExceptions);
        }
        public static async Task<TResult> Retry<T1, T2, T3, TResult>(int maxRetry, Func<T1, T2, T3, ValueTask<TResult>> func, T1 arg1, T2 arg2, T3 arg3, Func<AggregateException, Task> handler)
        {
            if (maxRetry < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetry));
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            List<Exception> innerExceptions;
            try
            {
                return await func(arg1, arg2, arg3);
            }
            catch (Exception ex)
            {
                innerExceptions = new List<Exception>() { ex };
                if (handler != null)
                {
                    await handler.Invoke(new AggregateException(innerExceptions));
                }
            }

            for (int i = 1; i <= maxRetry; i++)
            {
                try
                {
                    return await func(arg1, arg2, arg3);
                }
                catch (Exception ex)
                {
                    innerExceptions.Add(ex);
                    if (handler != null)
                    {
                        await handler.Invoke(new AggregateException(innerExceptions));
                    }
                }
            }
            throw new AggregateException("MaxRetry", innerExceptions);
        }
        public static async Task<TResult> Retry<T1, T2, T3, T4, TResult>(int maxRetry, Func<T1, T2, T3, T4, ValueTask<TResult>> func, T1 arg1, T2 arg2, T3 arg3, T4 arg4, Func<AggregateException, Task> handler)
        {
            if (maxRetry < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetry));
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            List<Exception> innerExceptions;
            try
            {
                return await func(arg1, arg2, arg3, arg4);
            }
            catch (Exception ex)
            {
                innerExceptions = new List<Exception>() { ex };
                if (handler != null)
                {
                    await handler.Invoke(new AggregateException(innerExceptions));
                }
            }

            for (int i = 1; i <= maxRetry; i++)
            {
                try
                {
                    return await func(arg1, arg2, arg3, arg4);
                }
                catch (Exception ex)
                {
                    innerExceptions.Add(ex);
                    if (handler != null)
                    {
                        await handler.Invoke(new AggregateException(innerExceptions));
                    }
                }
            }
            throw new AggregateException("MaxRetry", innerExceptions);
        }

        //TODO? Try
    }
}
