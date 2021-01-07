using System;
using System.Text;
using System.IO.Compression;

namespace BasicSample
{
    public class CompressionSample
    {
        public static void RunGzip() 
        {
            var src1 = Encoding.UTF8.GetBytes(new string('A', 1000));
            var dest1 = new byte[1024];

            var gzipEncoder = new DeflateEncoder(9, 31);//level 0-9
            gzipEncoder.Compress(src1, dest1, true, out var bytesConsumed1, out var bytesWritten1, out var completed1);
            Console.WriteLine(bytesConsumed1);
            Console.WriteLine(bytesWritten1);
            Console.WriteLine(completed1);
            

            var src2 = dest1.AsSpan(0,bytesWritten1);
            var dest2 = new byte[1024];

            var gzipDecoder = new DeflateDecoder(31);
            gzipDecoder.Decompress(src2,dest2,true,out var bytesConsumed2,out var bytesWritten2,out var completed2);
            Console.WriteLine(bytesConsumed2);
            Console.WriteLine(bytesWritten2);
            Console.WriteLine(completed2);
            Console.WriteLine(Encoding.UTF8.GetString(dest2.AsSpan(0,bytesWritten2)));


            gzipEncoder.Dispose();
            gzipDecoder.Dispose();
        }

        public static void RunDeflate() 
        {
            var src1 = Encoding.UTF8.GetBytes(new string('A', 1000));
            var dest1 = new byte[1024];

            var deflateEncoder = new DeflateEncoder(9, 15);//level 0-9 ,zlib header
            deflateEncoder.Compress(src1, dest1, true, out var bytesConsumed1, out var bytesWritten1, out var completed1);
            Console.WriteLine(bytesConsumed1);
            Console.WriteLine(bytesWritten1);
            Console.WriteLine(completed1);


            var src2 = dest1.AsSpan(0, bytesWritten1);
            var dest2 = new byte[1024];

            var deflateDecoder = new DeflateDecoder(15);
            deflateDecoder.Decompress(src2, dest2, true, out var bytesConsumed2, out var bytesWritten2, out var completed2);
            Console.WriteLine(bytesConsumed2);
            Console.WriteLine(bytesWritten2);
            Console.WriteLine(completed2);
            Console.WriteLine(Encoding.UTF8.GetString(dest2.AsSpan(0, bytesWritten2)));


            deflateEncoder.Dispose();
            deflateDecoder.Dispose();
        }
    }
}
