
namespace System.Extensions.Net
{
    using System.Reflection;
    using System.Linq.Expressions;
    using System.Net.Sockets;
    public static class SocketExtensions
    {
        public static void SetKeepAlive(this Socket @this, int keepAliveTime, int keepAliveInterval)
        {
            SetKeepAlive(@this, keepAliveTime, keepAliveInterval, 5);
        }
        public static void SetKeepAlive(this Socket @this, int keepAliveTime, int keepAliveInterval, int maxDataRetries)
        {
            var inOptionValues = new byte[12];
            BitConverter.TryWriteBytes(inOptionValues.AsSpan(0, 4), maxDataRetries);
            BitConverter.TryWriteBytes(inOptionValues.AsSpan(4, 4), keepAliveTime);
            BitConverter.TryWriteBytes(inOptionValues.AsSpan(8, 4), keepAliveInterval);
            @this.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            @this.IOControl(IOControlCode.KeepAliveValues, inOptionValues, null);
        }

        //SocketAsyncEventArgs BUG???
        static SocketExtensions() 
        {
            var args = Expression.Parameter(typeof(SocketAsyncEventArgs),"args");
            //TODO?? Move To Completed
            //SetBuffer(null, 0, 0);
            var setBuffer= typeof(SocketAsyncEventArgs).GetMethod("SetBuffer", new[] { typeof(byte[]), typeof(int), typeof(int) });
            var setBufferExpr = Expression.Call(args, setBuffer, Expression.Constant(null, typeof(byte[])), Expression.Constant(0), Expression.Constant(0));
            var currentSocket = typeof(SocketAsyncEventArgs).GetField("_currentSocket", BindingFlags.NonPublic | BindingFlags.Instance);
            if (currentSocket != null)
            {
                _Clear = Expression.Lambda<Action<SocketAsyncEventArgs>>(
                    Expression.Block(setBufferExpr, Expression.Assign(Expression.Field(args, currentSocket), Expression.Constant(null, typeof(Socket)))), args).Compile();
            }
            else 
            {
                _Clear = Expression.Lambda<Action<SocketAsyncEventArgs>>(setBufferExpr, args).Compile();
            }
        }

        private static Action<SocketAsyncEventArgs> _Clear;
        public static void Clear(this SocketAsyncEventArgs @this) 
        {
            _Clear(@this);
        }
    }
}
