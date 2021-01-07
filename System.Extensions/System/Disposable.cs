
namespace System
{
    public static class Disposable
    {
        public static IDisposable Empty { get; } = new _EmptyDisposable();
        private class _EmptyDisposable : IDisposable
        {
            public void Dispose() { }
        }
        private class _Disposable : IDisposable
        {
            public _Disposable(Action disposable)
            {
                _disposable = disposable;
            }

            private Action _disposable;
            public void Dispose()
            {
                _disposable();
            }
        }
        public static IDisposable Create(Action disposable)
        {
            if (disposable == null)
                throw new ArgumentNullException(nameof(disposable));//TODO? return Empty

            return new _Disposable(disposable);
        }
    }
}