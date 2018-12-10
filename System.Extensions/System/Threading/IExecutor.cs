
namespace System.Threading
{
    public interface IExecutor
    {
        void Run(IRunnable runnable);
        void Run(Action action);
        void Run(Action<object> action, object state);
    }
}
