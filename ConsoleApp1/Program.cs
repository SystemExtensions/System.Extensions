using System;
using System.Diagnostics;
using System.Buffers;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Runtime.InteropServices;
using System.Extensions;
using System.Net.Http;
using System.IO;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    class Program
    {
        static void Main(string[] args)
        {
            //Console.WriteLine(BitConverter.ToString(Encoding.Unicode.GetBytes("A BC")));

            var str = "ABCD%20%20%20%20+EFG+HI+JKLMN+%20%20%20";
            var sb = new StringBuffer();
            sb.Append(str);

            Console.WriteLine(Url.Decode(sb, Encoding.UTF8));

            //ArrayPool<byte>.Shared.Rent
            //var cache = new Cached<Memory<char>>();
            //cache.AsMemoryPool();

            //IMemoryOwner<byte>
            //var writer = new StringBuffer();
            //writer.Write(int.MaxValue);
            //writer.Write(new byte[123])
            //writer.Append("ABCDEFG");

            //sb.Append();
            //sb.Write();
            //ConnectionExtensions.SetBytes(MemoryPool<byte>.Shared);
            //ConnectionExtensions.SetWriter(new ObjectPool());//ObjectCache 
            //ConnectionExtensions.GetBytes();
            //ConnectionExtensions.GetWriter();
            //var str = ulong.MaxValue.ToString();

            //Console.WriteLine(str.TryConvert(out long? vInt));
            Console.Read();
        }
    }
}
