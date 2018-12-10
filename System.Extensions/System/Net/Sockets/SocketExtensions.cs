
namespace System.Extensions.Net
{
    using System.Net.Sockets;
    public static class SocketExtensions
    {
        /// <summary>
        /// 设置TCP keepAlive参数
        /// SetTcpKeepAlive 这个方法名
        /// </summary>
        /// <param name="this"></param>
        /// <param name="keepAliveTime"></param>
        /// <param name="keepAliveInterval"></param>
        public static void SetKeepAlive(this Socket @this, TimeSpan keepAliveTime, TimeSpan keepAliveInterval)
        {
            byte[] inOptionValues = new byte[12];
            BitConverter.GetBytes((uint)1).CopyTo(inOptionValues, 0);
            BitConverter.GetBytes((uint)keepAliveTime.Milliseconds).CopyTo(inOptionValues, 4);
            BitConverter.GetBytes((uint)keepAliveInterval.Milliseconds).CopyTo(inOptionValues, 8);

            @this.IOControl(IOControlCode.KeepAliveValues, inOptionValues, null);
        }
    }
}
