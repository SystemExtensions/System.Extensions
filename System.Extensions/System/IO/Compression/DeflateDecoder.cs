
namespace System.IO.Compression
{
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Runtime.InteropServices;
    public class DeflateDecoder : IDisposable
    {
        static DeflateDecoder()
        {
            //https://github.com/dotnet/runtime/blob/master/src/libraries/Common/src/Interop/Interop.zlib.cs
            var zlib = typeof(GZipStream).Assembly.GetType("Interop+zlib");
            var flushCodeType = typeof(GZipStream).Assembly.GetType("System.IO.Compression.ZLibNative+FlushCode");
            var zStreamType = typeof(GZipStream).Assembly.GetType("System.IO.Compression.ZLibNative+ZStream");
            var nextIn = zStreamType.GetField("nextIn", BindingFlags.Instance | BindingFlags.NonPublic);
            var nextOut = zStreamType.GetField("nextOut", BindingFlags.Instance | BindingFlags.NonPublic);
            var msg = zStreamType.GetField("msg", BindingFlags.Instance | BindingFlags.NonPublic);
            var internalState = zStreamType.GetField("internalState", BindingFlags.Instance | BindingFlags.NonPublic);
            var availIn = zStreamType.GetField("availIn", BindingFlags.Instance | BindingFlags.NonPublic);
            var availOut = zStreamType.GetField("availOut", BindingFlags.Instance | BindingFlags.NonPublic);
            var nextIn_ = typeof(DeflateDecoder).GetField("_nextIn", BindingFlags.Instance | BindingFlags.NonPublic);
            var nextOut_ = typeof(DeflateDecoder).GetField("_nextOut", BindingFlags.Instance | BindingFlags.NonPublic);
            var msg_ = typeof(DeflateDecoder).GetField("_msg", BindingFlags.Instance | BindingFlags.NonPublic);
            var internalState_ = typeof(DeflateDecoder).GetField("_internalState", BindingFlags.Instance | BindingFlags.NonPublic);
            var availIn_ = typeof(DeflateDecoder).GetField("_availIn", BindingFlags.Instance | BindingFlags.NonPublic);
            var availOut_ = typeof(DeflateDecoder).GetField("_availOut", BindingFlags.Instance | BindingFlags.NonPublic);

            //_Init
            {
                var decoder = Expression.Parameter(typeof(DeflateDecoder), "decoder");
                var windowBits = Expression.Parameter(typeof(int), "windowBits");
                var stream = Expression.Variable(zStreamType, "stream");
                var errorCode = Expression.Variable(typeof(int), "errorCode");
                var expr = Expression.Block(new[] { stream, errorCode },
                    Expression.Assign(stream, Expression.Default(zStreamType)),
                    Expression.Assign(errorCode,
                    Expression.Convert(
                        Expression.Call(null, zlib.GetMethod("InflateInit2_", BindingFlags.Static | BindingFlags.NonPublic),
                        stream,
                        windowBits), typeof(int))),
                    Expression.Assign(Expression.Field(decoder, nextIn_), Expression.Field(stream, nextIn)),
                    Expression.Assign(Expression.Field(decoder, nextOut_), Expression.Field(stream, nextOut)),
                    Expression.Assign(Expression.Field(decoder, msg_), Expression.Field(stream, msg)),
                    Expression.Assign(Expression.Field(decoder, internalState_), Expression.Field(stream, internalState)),
                    Expression.Assign(Expression.Field(decoder, availIn_), Expression.Field(stream, availIn)),
                    Expression.Assign(Expression.Field(decoder, availOut_), Expression.Field(stream, availOut)),
                    errorCode
                    );
                _Init = Expression.Lambda<Func<DeflateDecoder, int, int>>(expr, new[] { decoder, windowBits }).Compile();
            }

            //_Inflate
            {
                var decoder = Expression.Parameter(typeof(DeflateDecoder), "decoder");
                var flushCode = Expression.Parameter(typeof(int), "flushCode");
                var stream = Expression.Variable(zStreamType, "stream");
                var errorCode = Expression.Variable(typeof(int), "errorCode");
                var expr = Expression.Block(new[] { stream, errorCode },
                    Expression.Assign(stream, Expression.Default(zStreamType)),
                    Expression.Assign(Expression.Field(stream, nextIn), Expression.Field(decoder, nextIn_)),
                    Expression.Assign(Expression.Field(stream, nextOut), Expression.Field(decoder, nextOut_)),
                    Expression.Assign(Expression.Field(stream, msg), Expression.Field(decoder, msg_)),
                    Expression.Assign(Expression.Field(stream, internalState), Expression.Field(decoder, internalState_)),
                    Expression.Assign(Expression.Field(stream, availIn), Expression.Field(decoder, availIn_)),
                    Expression.Assign(Expression.Field(stream, availOut), Expression.Field(decoder, availOut_)),
                    Expression.Assign(errorCode,
                    Expression.Convert(
                        Expression.Call(null, zlib.GetMethod("Inflate", BindingFlags.Static | BindingFlags.NonPublic),
                        stream,
                        Expression.Convert(flushCode, flushCodeType)), typeof(int))),
                    Expression.Assign(Expression.Field(decoder, nextIn_), Expression.Field(stream, nextIn)),
                    Expression.Assign(Expression.Field(decoder, nextOut_), Expression.Field(stream, nextOut)),
                    Expression.Assign(Expression.Field(decoder, msg_), Expression.Field(stream, msg)),
                    Expression.Assign(Expression.Field(decoder, internalState_), Expression.Field(stream, internalState)),
                    Expression.Assign(Expression.Field(decoder, availIn_), Expression.Field(stream, availIn)),
                    Expression.Assign(Expression.Field(decoder, availOut_), Expression.Field(stream, availOut)),
                    errorCode
                    );
                _Inflate = Expression.Lambda<Func<DeflateDecoder, int, int>>(expr, new[] { decoder, flushCode }).Compile();
            }

            //_InflateEnd
            {
                var decoder = Expression.Parameter(typeof(DeflateDecoder), "decoder");
                var stream = Expression.Variable(zStreamType, "stream");
                var expr = Expression.Block(new[] { stream },
                    Expression.Assign(stream, Expression.Default(zStreamType)),
                    Expression.Assign(Expression.Field(stream, nextIn), Expression.Field(decoder, nextIn_)),
                    Expression.Assign(Expression.Field(stream, nextOut), Expression.Field(decoder, nextOut_)),
                    Expression.Assign(Expression.Field(stream, msg), Expression.Field(decoder, msg_)),
                    Expression.Assign(Expression.Field(stream, internalState), Expression.Field(decoder, internalState_)),
                    Expression.Assign(Expression.Field(stream, availIn), Expression.Field(decoder, availIn_)),
                    Expression.Assign(Expression.Field(stream, availOut), Expression.Field(decoder, availOut_)),
                    Expression.Convert(
                        Expression.Call(null, zlib.GetMethod("InflateEnd", BindingFlags.Static | BindingFlags.NonPublic),
                        stream), typeof(int)));
                _InflateEnd = Expression.Lambda<Func<DeflateDecoder, int>>(expr, new[] { decoder }).Compile();
            }

        }
        private static Func<DeflateDecoder, int, int> _Init;
        private static Func<DeflateDecoder, int, int> _Inflate;
        private static Func<DeflateDecoder, int> _InflateEnd;
        public DeflateDecoder(int windowBits)
        {
            var errorCode = _Init(this, windowBits);
            if (errorCode != 0)
            {
                var msg = _msg != IntPtr.Zero ? Marshal.PtrToStringAnsi(_msg)! : string.Empty;
                throw new InvalidOperationException($"{errorCode}:{msg}");
            }
        }

        private IntPtr _nextIn;
        private IntPtr _nextOut;
        private IntPtr _msg;
        private IntPtr _internalState;
        private uint _availIn;
        private uint _availOut;
        public void Decompress(ReadOnlySpan<byte> src, Span<byte> dest, bool flush, out int bytesConsumed, out int bytesWritten, out bool completed)
        {
            unsafe
            {
                fixed (byte* pSrc = src)
                fixed (byte* pDest = dest)
                {
                    Decompress(pSrc, src.Length, pDest, dest.Length, flush, out bytesConsumed, out bytesWritten, out completed);
                }
            }
        }
        public unsafe void Decompress(byte* src, int srcBytes, byte* dest, int destBytes, bool flush, out int bytesConsumed, out int bytesWritten, out bool completed)
        {
            if (_internalState == IntPtr.Zero)
                throw new ObjectDisposedException(nameof(DeflateEncoder));
            if (srcBytes < 0)
                throw new ArgumentOutOfRangeException(nameof(srcBytes));
            if (destBytes < 0)
                throw new ArgumentOutOfRangeException(nameof(destBytes));

            _nextIn = (IntPtr)src;
            _availIn = (uint)srcBytes;
            _nextOut = (IntPtr)dest;
            _availOut = (uint)destBytes;
            var errorCode = _Inflate(this, flush ? 4 : 0);
            bytesConsumed = srcBytes - (int)_availIn;
            bytesWritten = destBytes - (int)_availOut;
            if (errorCode == 1)//StreamEnd 
            {
                completed = true;
                return;
            }
            if (errorCode == 0)
            {
                completed = false;
                return;
            }
            var msg = _msg != IntPtr.Zero ? Marshal.PtrToStringAnsi(_msg)! : string.Empty;
            throw new InvalidOperationException($"{errorCode}:{msg}");
        }
        public void Dispose()
        {
            if (_internalState == IntPtr.Zero)
                return;

            _InflateEnd(this);
            _internalState = IntPtr.Zero;
            _nextIn = IntPtr.Zero;
            _nextOut = IntPtr.Zero;
            _msg = IntPtr.Zero;
            _availIn = 0;
            _availOut = 0;
            GC.SuppressFinalize(this);
        }
        ~DeflateDecoder()
        {
            Dispose();
        }
    }
}
