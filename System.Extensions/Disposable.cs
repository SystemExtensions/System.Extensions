
namespace System.Extensions
{
    using System.Collections.Generic;
    public static class Disposable
    {
        private class _Disposable : IDisposable
        {
            public _Disposable(Action disposable)
            {
                _disposable = disposable;
            }

            private Action _disposable;
            public void Dispose()
            {
                _disposable.Invoke();
            }
        }
        public static IDisposable Create(Action disposable)
        {
            if (disposable == null)
                throw new ArgumentNullException(nameof(disposable));

            return new _Disposable(disposable);
        }

        //public static void Dispose<T>(this IList<T> @this) where T : IDisposable
        public static void Dispose(this IList<IDisposable> @this)
        {
            if (@this == null)
                return;

            for (int i = 0; i < @this.Count; i++)
            {
                @this[i]?.Dispose();//不忽略异常
            }
            @this.Clear();
        }
        public static void Add(this IList<IDisposable> @this, Action disposable)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (disposable == null)
                throw new ArgumentNullException(nameof(disposable));

            @this.Add(new _Disposable(disposable));
        }
    }
}
