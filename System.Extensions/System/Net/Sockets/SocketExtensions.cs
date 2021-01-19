
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
            try
            {
                var saea = Expression.Parameter(typeof(SocketAsyncEventArgs), "saea");
                //TODO?? SetBuffer(null, 0, 0)Move To Completed
                //var setBuffer = typeof(SocketAsyncEventArgs).GetMethod("SetBuffer", new[] { typeof(byte[]), typeof(int), typeof(int) });
                //HandleCompletionPortCallbackError
                //_operating(Free = 0)TODO? SpinWait
                //https://github.com/dotnet/runtime/blob/master/src/libraries/System.Net.Sockets/src/System/Net/Sockets/SocketAsyncEventArgs.cs
                var operating = typeof(SocketAsyncEventArgs).GetField("_operating", BindingFlags.NonPublic | BindingFlags.Instance);
                var currentSocket = typeof(SocketAsyncEventArgs).GetField("_currentSocket", BindingFlags.NonPublic | BindingFlags.Instance);
                _CleanUp = Expression.Lambda<Action<SocketAsyncEventArgs>>(
                    Expression.IfThen(
                        Expression.Equal(Expression.Field(saea, operating), Expression.Constant(0)),
                        Expression.Assign(Expression.Field(saea, currentSocket), Expression.Constant(null, typeof(Socket)))
                        )
                    , saea).Compile();
            }
            catch
            {
                Console.WriteLine(nameof(_CleanUp));
                _CleanUp=(saea)=> { };
            }
        }

        private static Action<SocketAsyncEventArgs> _CleanUp;
        public static void CleanUp(this SocketAsyncEventArgs @this) 
        {
            _CleanUp(@this);
        }
    }
}
