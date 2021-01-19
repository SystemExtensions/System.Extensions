using System;
using System.IO;
using System.Text;
using System.Buffers;
using System.Threading.Tasks;
using System.Extensions.Http;

namespace BasicSample
{
    public class BufferSample
    {
        public static void Run()
        {
            //Buffer<char>
            Console.WriteLine("Buffer<char>");
            var c1 = Buffer<char>.Create(1);
            //Buffer<char>.Create(ArrayPool<char>.Shared, 1024, out var disposable);
            //StringExtensions.ThreadRent(out var disposable); ThreadStatic
            //StringExtensions.Rent(out var disposable);
            //StringContent.Rent(out var disposable) http

            c1.Write('A');
            c1.Write("This is a String!!!");
            c1.Write(byte.MaxValue);
            c1.Write(short.MaxValue);
            c1.Write(int.MaxValue);
            c1.Write(long.MaxValue);
            c1.Write(DateTime.Now, "yyyy-MM-dd HH:mm:ss");

            var span1 = c1.GetSpan();
            span1[0] = 'X';
            c1.Advance(1);
            var span2 = c1.GetSpan(32);
            Guid.NewGuid().TryFormat(span2, out var charsWritten, "N");
            c1.Advance(charsWritten);


            var bytes1 = Encoding.UTF8.GetBytes("My name is 张贺");
            c1.WriteBytes(bytes1, Encoding.UTF8);
            //c1.WriteBytes(ReadOnlySequence<byte>, Encoding.UTF8)

            var decoder = Encoding.UTF8.GetDecoder();
            c1.WriteBytes(bytes1.AsSpan(0, 2), false, decoder);
            c1.WriteBytes(bytes1.AsSpan(2, 3), false, decoder);
            c1.WriteBytes(bytes1.AsSpan(5), true, decoder);
            //c1.WriteBytes(byte*, bool, decoder)

            var tempSeq1 = c1.Sequence;//ReadOnlySequence<char>
            var tempSpan1 = tempSeq1.IsSingleSegment ? tempSeq1.First.Span : tempSeq1.ToArray();

            Console.WriteLine(Encoding.UTF8.GetByteCount(tempSeq1));
            byte[] byteArray = Encoding.UTF8.GetBytes(tempSeq1);
            Console.WriteLine(c1.ToString());

            //------------------------------------------------------------------------

            //Buffer<byte>
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("Buffer<byte>");
            var b1 = Buffer<byte>.Create(1);
            //Buffer<byte>.Create(ArrayPool<byte>.Shared, 1024, out var disposable);
            //MemoryContent.Rent(out var disposable) http

            b1.Write((byte)'A');
            b1.Write(Encoding.UTF8.GetBytes("This is a String!!!"));

            var span3 = b1.GetSpan();
            span3[0] = (byte)'X';
            b1.Advance(1);
            //var result = (stream,content...).ReadAsync(b1.GetSpan());
            //b1.Advance(result);
            //var result = await (stream, content...).ReadAsync(b1.GetMemory());
            //b1.Advance(result);


            b1.WriteChars("My name is 张贺", Encoding.UTF8);
            //b1.WriteChars(ReadOnlySequence<char>, Encoding.UTF8)

            var encoder = Encoding.UTF8.GetEncoder();
            b1.WriteChars("My", false, encoder);
            b1.WriteChars(" name is ", false, encoder);
            b1.WriteChars("张贺", true, encoder);
            //b1.WriteChars(char*, bool, encoder)


            var tempSeq2 = b1.Sequence;//ReadOnlySequence<byte>
            var tempSpan2 = tempSeq2.IsSingleSegment ? tempSeq2.First.Span : tempSeq2.ToArray();

            Console.WriteLine(Encoding.UTF8.GetCharCount(tempSeq2));
            Console.WriteLine(Encoding.UTF8.GetString(tempSeq2));

            //------------------------------------------------------------------------

            //BufferExtensions.SizeOf<T>()
            Console.WriteLine($"{sizeof(byte)}={BufferExtensions.SizeOf<byte>()}");
            Console.WriteLine($"{sizeof(sbyte)}={BufferExtensions.SizeOf<sbyte>()}");
            Console.WriteLine($"{sizeof(short)}={BufferExtensions.SizeOf<short>()}");
            Console.WriteLine($"{sizeof(ushort)}={BufferExtensions.SizeOf<ushort>()}");
            Console.WriteLine($"{sizeof(int)}={BufferExtensions.SizeOf<int>()}");
            Console.WriteLine($"{sizeof(uint)}={BufferExtensions.SizeOf<uint>()}");
            Console.WriteLine($"{sizeof(long)}={BufferExtensions.SizeOf<long>()}");
            Console.WriteLine($"{sizeof(ulong)}={BufferExtensions.SizeOf<ulong>()}");
            Console.WriteLine($"{sizeof(float)}={BufferExtensions.SizeOf<float>()}");
            Console.WriteLine($"{sizeof(double)}={BufferExtensions.SizeOf<double>()}");
            Console.WriteLine($"{sizeof(decimal)}={BufferExtensions.SizeOf<decimal>()}");
            unsafe
            {
                Console.WriteLine($"{sizeof(DateTime)}={BufferExtensions.SizeOf<DateTime>()}");
                Console.WriteLine($"{sizeof(DateTimeOffset)}={BufferExtensions.SizeOf<DateTimeOffset>()}");
            }

            //------------------------------------------------------------------------

            //AsWriter()
            var sb = Buffer<char>.Create(1);
            var writer = sb.AsWriter(); //TextWriter
            writer.Write("ABC");
            writer.Write(int.MaxValue);
            writer.Write(long.MaxValue);
            writer.Write("EFG");
            Console.WriteLine(sb.ToString());

            //------------------------------------------------------------------------
            //UnmanagedMemory<T>

            //Marshal.AllocHGlobal
            var um1 = new UnmanagedMemory<char>(10);
            unsafe
            {
                char* dataPtr = um1.DataPtr;
                Console.WriteLine(new IntPtr(dataPtr));
            }
            var um1Span = um1.GetSpan();
            var um1Length = um1.Length;
            for (int i = 0; i < um1Length; i++)
            {
                um1Span[i] = 'A';
            }
            Console.WriteLine(new string(um1.GetSpan()));
            um1.Dispose();

            var um2 = new UnmanagedMemory<byte>(9);
            //um2.GetSpan().Clear();
            //um2.GetSpan().Fill(0);
            for (int i = 0; i < um2.Length; i++)//not zero
            {
                Console.WriteLine(um2.GetSpan()[i]);
            }
            Encoding.ASCII.GetBytes("123456789", um2.GetSpan());
            Console.WriteLine(Encoding.ASCII.GetString(um2.GetSpan()));
            um2.Dispose();

            //pointer
            UnmanagedMemory<byte> um3;
            unsafe
            {
                var stackPtr1 = stackalloc byte[10];//byte*
                um3 = new UnmanagedMemory<byte>(stackPtr1, 10);
            }
            Task.Run(async () =>
            {
                var testStream = Stream.Null;
                await testStream.ReadAsync(um3.Memory);
            }).Wait();
            um3.Dispose();

        }
    }
}