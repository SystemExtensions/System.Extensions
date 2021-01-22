
namespace System.IO.Compression
{
    using System.Diagnostics;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Runtime.InteropServices;
    public class DeflateEncoder : IDisposable
    {
        static DeflateEncoder()
        {
            //https://github.com/dotnet/runtime/blob/master/src/libraries/Common/src/Interop/Interop.zlib.cs
            var zlib = typeof(GZipStream).Assembly.GetType("Interop+zlib");
            var compressionLevelType = typeof(GZipStream).Assembly.GetType("System.IO.Compression.ZLibNative+CompressionLevel");
            var compressionMethodType = typeof(GZipStream).Assembly.GetType("System.IO.Compression.ZLibNative+CompressionMethod");
            var compressionStrategyType = typeof(GZipStream).Assembly.GetType("System.IO.Compression.ZLibNative+CompressionStrategy");
            var flushCodeType = typeof(GZipStream).Assembly.GetType("System.IO.Compression.ZLibNative+FlushCode");
            var zStreamType = typeof(GZipStream).Assembly.GetType("System.IO.Compression.ZLibNative+ZStream");
            var nextIn = zStreamType.GetField("nextIn", BindingFlags.Instance | BindingFlags.NonPublic);
            var nextOut = zStreamType.GetField("nextOut", BindingFlags.Instance | BindingFlags.NonPublic);
            var msg = zStreamType.GetField("msg", BindingFlags.Instance | BindingFlags.NonPublic);
            var internalState = zStreamType.GetField("internalState", BindingFlags.Instance | BindingFlags.NonPublic);
            var availIn = zStreamType.GetField("availIn", BindingFlags.Instance | BindingFlags.NonPublic);
            var availOut = zStreamType.GetField("availOut", BindingFlags.Instance | BindingFlags.NonPublic);
            var nextIn_ = typeof(DeflateEncoder).GetField("_nextIn", BindingFlags.Instance | BindingFlags.NonPublic);
            var nextOut_ = typeof(DeflateEncoder).GetField("_nextOut", BindingFlags.Instance | BindingFlags.NonPublic);
            var msg_ = typeof(DeflateEncoder).GetField("_msg", BindingFlags.Instance | BindingFlags.NonPublic);
            var internalState_ = typeof(DeflateEncoder).GetField("_internalState", BindingFlags.Instance | BindingFlags.NonPublic);
            var availIn_ = typeof(DeflateEncoder).GetField("_availIn", BindingFlags.Instance | BindingFlags.NonPublic);
            var availOut_ = typeof(DeflateEncoder).GetField("_availOut", BindingFlags.Instance | BindingFlags.NonPublic);

            //_Init
            {
                var encoder = Expression.Parameter(typeof(DeflateEncoder), "encoder");
                var level = Expression.Parameter(typeof(int), "level");
                var windowBits = Expression.Parameter(typeof(int), "windowBits");
                var stream = Expression.Variable(zStreamType, "stream");
                var errorCode = Expression.Variable(typeof(int), "errorCode");
                var expr = Expression.Block(new[] { stream, errorCode },
                    Expression.Assign(stream, Expression.Default(zStreamType)),
                    Expression.Assign(errorCode,
                    Expression.Convert(
                        Expression.Call(null, zlib.GetMethod("DeflateInit2_", BindingFlags.Static | BindingFlags.NonPublic),
                        stream,
                        Expression.Convert(level, compressionLevelType),
                        Expression.Convert(Expression.Constant(8), compressionMethodType),
                        windowBits,
                        Expression.Constant(8),
                        Expression.Convert(Expression.Constant(0), compressionStrategyType)), typeof(int))),
                    Expression.Assign(Expression.Field(encoder, nextIn_), Expression.Field(stream, nextIn)),
                    Expression.Assign(Expression.Field(encoder, nextOut_), Expression.Field(stream, nextOut)),
                    Expression.Assign(Expression.Field(encoder, msg_), Expression.Field(stream, msg)),
                    Expression.Assign(Expression.Field(encoder, internalState_), Expression.Field(stream, internalState)),
                    Expression.Assign(Expression.Field(encoder, availIn_), Expression.Field(stream, availIn)),
                    Expression.Assign(Expression.Field(encoder, availOut_), Expression.Field(stream, availOut)),
                    errorCode
                    );
                _Init = Expression.Lambda<Func<DeflateEncoder, int, int, int>>(expr, new[] { encoder, level, windowBits }).Compile();
            }

            //_Deflate
            {
                var encoder = Expression.Parameter(typeof(DeflateEncoder), "encoder");
                var flushCode = Expression.Parameter(typeof(int), "flushCode");
                var stream = Expression.Variable(zStreamType, "stream");
                var errorCode = Expression.Variable(typeof(int), "errorCode");
                var expr = Expression.Block(new[] { stream, errorCode },
                    Expression.Assign(stream, Expression.Default(zStreamType)),
                    Expression.Assign(Expression.Field(stream, nextIn), Expression.Field(encoder, nextIn_)),
                    Expression.Assign(Expression.Field(stream, nextOut), Expression.Field(encoder, nextOut_)),
                    Expression.Assign(Expression.Field(stream, msg), Expression.Field(encoder, msg_)),
                    Expression.Assign(Expression.Field(stream, internalState), Expression.Field(encoder, internalState_)),
                    Expression.Assign(Expression.Field(stream, availIn), Expression.Field(encoder, availIn_)),
                    Expression.Assign(Expression.Field(stream, availOut), Expression.Field(encoder, availOut_)),
                    Expression.Assign(errorCode,
                    Expression.Convert(
                        Expression.Call(null, zlib.GetMethod("Deflate", BindingFlags.Static | BindingFlags.NonPublic),
                        stream,
                        Expression.Convert(flushCode, flushCodeType)), typeof(int))),
                    Expression.Assign(Expression.Field(encoder, nextIn_), Expression.Field(stream, nextIn)),
                    Expression.Assign(Expression.Field(encoder, nextOut_), Expression.Field(stream, nextOut)),
                    Expression.Assign(Expression.Field(encoder, msg_), Expression.Field(stream, msg)),
                    Expression.Assign(Expression.Field(encoder, internalState_), Expression.Field(stream, internalState)),
                    Expression.Assign(Expression.Field(encoder, availIn_), Expression.Field(stream, availIn)),
                    Expression.Assign(Expression.Field(encoder, availOut_), Expression.Field(stream, availOut)),
                    errorCode
                    );
                _Deflate = Expression.Lambda<Func<DeflateEncoder, int, int>>(expr, new[] { encoder, flushCode }).Compile();
            }

            //_DeflateEnd
            {
                var encoder = Expression.Parameter(typeof(DeflateEncoder), "encoder");
                var stream = Expression.Variable(zStreamType, "stream");
                var expr = Expression.Block(new[] { stream },
                    Expression.Assign(stream, Expression.Default(zStreamType)),
                    Expression.Assign(Expression.Field(stream, nextIn), Expression.Field(encoder, nextIn_)),
                    Expression.Assign(Expression.Field(stream, nextOut), Expression.Field(encoder, nextOut_)),
                    Expression.Assign(Expression.Field(stream, msg), Expression.Field(encoder, msg_)),
                    Expression.Assign(Expression.Field(stream, internalState), Expression.Field(encoder, internalState_)),
                    Expression.Assign(Expression.Field(stream, availIn), Expression.Field(encoder, availIn_)),
                    Expression.Assign(Expression.Field(stream, availOut), Expression.Field(encoder, availOut_)),
                    Expression.Convert(
                        Expression.Call(null, zlib.GetMethod("DeflateEnd", BindingFlags.Static | BindingFlags.NonPublic),
                        stream), typeof(int)));
                _DeflateEnd = Expression.Lambda<Func<DeflateEncoder, int>>(expr, new[] { encoder }).Compile();
            }

        }
        private static Func<DeflateEncoder, int, int, int> _Init;
        private static Func<DeflateEncoder, int, int> _Deflate;
        private static Func<DeflateEncoder, int> _DeflateEnd;
        public DeflateEncoder(int level, int windowBits)
        {
            if (level < 0 || level > 9)
                throw new ArgumentOutOfRangeException(nameof(level));

            var errorCode = _Init(this, level, windowBits);
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
        public void Compress(Span<byte> src, Span<byte> dest, bool flush, out int bytesConsumed,  out int bytesWritten, out bool completed)
        {
            unsafe
            {
                fixed (byte* pSrc = src)
                fixed (byte* pDest = dest)
                {
                    Compress(pSrc, src.Length, pDest, dest.Length, flush, out bytesConsumed, out bytesWritten, out completed);
                }
            }
        }
        public unsafe void Compress(byte* src, int srcBytes, byte* dest, int destBytes, bool flush, out int bytesConsumed, out int bytesWritten, out bool completed)
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
            var errorCode = _Deflate(this, flush ? 4 : 0);
            bytesConsumed = srcBytes - (int)_availIn;
            bytesWritten = destBytes - (int)_availOut;
            if (errorCode == 1)//StreamEnd 
            {
                Debug.Assert(flush);
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

            _DeflateEnd(this);
            _internalState = IntPtr.Zero;
            _nextIn = IntPtr.Zero;
            _nextOut = IntPtr.Zero;
            _msg = IntPtr.Zero;
            _availIn = 0;
            _availOut = 0;
            GC.SuppressFinalize(this);
        }
        ~DeflateEncoder()
        {
            Dispose();
        }
    }
}
