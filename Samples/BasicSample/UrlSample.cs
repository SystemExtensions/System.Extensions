using System;
using System.Text;
using System.Buffers;
using System.Globalization;

namespace BasicSample
{
    public class UrlSample
    {
        public static void Run()
        {
            var url1 = new Url();
            url1.Scheme = "https";
            url1.UserInfo = null;
            url1.Host = "www.cnblogs.com";
            url1.Port = 443;
            //url1.Authority = "www.cnblogs.com:443";
            url1.Path = "/test1/test2/";
            url1.Query = "?name=zhanghe";
            url1.Fragment = "#link";
            Console.WriteLine($"url1:{url1}");
            Console.WriteLine();

            var url2 = new Url("https://www.cnblogs.com/test1/test2/?name=zhanghe#link");
            Console.WriteLine("url2");
            Console.WriteLine($"Scheme:{url2.Scheme}");
            Console.WriteLine($"UserInfo:{url2.UserInfo}");
            Console.WriteLine($"Host:{url2.Host}");
            //默认端口 需要自己根据协议判断
            //default port,You need to judge by scheme
            Console.WriteLine($"Port:{url2.Port}");
            Console.WriteLine($"HostNameType:{url2.HostNameType}");
            Console.WriteLine($"Authority:{url2.Authority}");
            Console.WriteLine($"Domain:{url2.Domain}");
            Console.WriteLine($"Path:{url2.Path}");
            Console.WriteLine($"Query:{url2.Query}");
            Console.WriteLine($"Fragment:{url2.Fragment}");
            Console.WriteLine($"AbsolutePath:{url2.AbsolutePath}");
            Console.WriteLine($"AbsoluteUri:{url2.AbsoluteUri}");
            Console.WriteLine();

            //IPv4
            var url3 = new Url("https://121.40.43.188:443/test1/test2/?name=zhanghe#link");
            Console.WriteLine("url3");
            Console.WriteLine($"Host:{url3.Host}");
            Console.WriteLine($"HostNameType:{url3.HostNameType}");
            Console.WriteLine($"Authority:{url3.Authority}");
            Console.WriteLine($"Domain:{url3.Domain}");
            Console.WriteLine();

            //IPv6
            var url4 = new Url("https://[::FFFF:7928:2BBC]:443/test1/test2/?name=zhanghe#link");
            Console.WriteLine("url4");
            Console.WriteLine($"Host:{url4.Host}");
            Console.WriteLine($"HostNameType:{url4.HostNameType}");
            Console.WriteLine($"Authority:{url4.Authority}");
            Console.WriteLine($"Domain:{url4.Domain}");
            Console.WriteLine();

            //Domain
            var url5 = new Url("http://cnblogs.com");
            var url6 = new Url("http://cnblogs.com.cn");//co,com,net,org,gov + ccTLDs(国家顶级域名)
            var url7 = new Url("http://cnblogs.biz.cn");
            var url8 = new Url("http://cnblogs.com.xyz");
            Console.WriteLine($"Domain:{url5.Domain}   {url5}");
            Console.WriteLine($"Domain:{url6.Domain}   {url6}");
            Console.WriteLine($"Domain:{url7.Domain}   {url7}");
            Console.WriteLine($"Domain:{url8.Domain}   {url8}");
            //(ipv4,ipv6,not .) Domain is null not return Host
            var url9 = new Url("http://121.40.43.188/");
            var url10 = new Url("http://[::FFFF:7928:2BBC]/");
            var url11 = new Url("http://localhost/");
            Console.WriteLine($"Domain:{url9.Domain}   {url9}");
            Console.WriteLine($"Domain:{url10.Domain}   {url10}");
            Console.WriteLine($"Domain:{url11.Domain}   {url11}");
            //url.Domain??url.Host
            Console.WriteLine($"Domain:{url9.Domain ?? url9.Host}   {url9}");
            Console.WriteLine($"Domain:{url10.Domain ?? url10.Host}   {url10}");
            Console.WriteLine($"Domain:{url11.Domain ?? url11.Host}   {url11}");
            Console.WriteLine();

            //Uri=>Url
            //Unicode(Host(Idn) Path Fragment)
            var uri = new Uri("http://博客园.com/路径1/路径2?查询#片段");
            var url13 = new Url(uri);
            Console.WriteLine($"url13:{url13}");
            Console.WriteLine($"Host:{new IdnMapping().GetUnicode(url13.Host)}");
            Console.WriteLine($"Path:{Url.Decode(url13.Path)}");
            Console.WriteLine($"Query:{Url.Decode(url13.Query)}");
            Console.WriteLine($"Fragment:{Url.Decode(url13.Fragment)}");
            Console.WriteLine();
        }
        public static void RunEncoding()
        {
            var str1 = "My name is 张贺";

            //string
            Console.WriteLine("string");
            var e1 = Url.Encode(str1);
            var d1 = Url.Decode(e1);
            Console.WriteLine($"UTF8:{e1}");
            Console.WriteLine($"UTF8:{d1}");
            var e2 = Url.Encode(str1, Encoding.Unicode);
            var d2 = Url.Decode(e2, Encoding.Unicode);
            Console.WriteLine($"Unicode:{e2}");
            Console.WriteLine($"Unicode:{d2}");

            //ReadOnlySpan<char>
            var span1 = str1.AsSpan();
            Console.WriteLine();
            Console.WriteLine("ReadOnlySpan<char>");
            var e3 = Url.Encode(span1);
            var d3 = Url.Decode(e3.AsSpan());
            Console.WriteLine($"UTF8:{e3}");
            Console.WriteLine($"UTF8:{d3}");
            var e4 = Url.Encode(span1, Encoding.Unicode);
            var d4 = Url.Decode(e4.AsSpan(), Encoding.Unicode);
            Console.WriteLine($"Unicode:{e4}");
            Console.WriteLine($"Unicode:{d4}");

            //ReadOnlySequence<char>
            ReadOnlySequence<char> CreateSeq(string value)
            {
                var sb = Buffer<char>.Create(1);
                for (int i = 0; i < value.Length; i++)
                {
                    sb.Write(value[i]);
                }
                return sb.Sequence;
            }
            Console.WriteLine();
            Console.WriteLine("ReadOnlySequence<char>");
            var seq1 = CreateSeq(str1);
            var e5 = Url.Encode(seq1);
            var d5 = Url.Decode(CreateSeq(e5));
            Console.WriteLine($"UTF8:{e5}");
            Console.WriteLine($"UTF8:{d5}");
            var e6 = Url.Encode(seq1, Encoding.Unicode);
            var d6 = Url.Decode(CreateSeq(e6), Encoding.Unicode);
            Console.WriteLine($"Unicode:{e6}");
            Console.WriteLine($"Unicode:{d6}");

            //BufferWriter<char>
            Console.WriteLine();
            Console.WriteLine("BufferWriter<char>");
            var writer1 = Buffer<char>.Create(1);
            Url.Encode(str1, Encoding.UTF8, writer1);
            Console.WriteLine($"UTF8:{writer1.ToString()}");
            var writer2 = Buffer<char>.Create(1);
            Url.Encode(str1, Encoding.Unicode, writer2);
            Console.WriteLine($"Unicode:{writer2.ToString()}");
        }
    }
}