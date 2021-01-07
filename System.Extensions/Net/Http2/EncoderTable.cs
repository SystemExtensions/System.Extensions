
namespace System.Extensions.Net
{
    using System.Collections.Generic;
    using System.Extensions.Http;
    public class EncoderTable
    {
        //https://httpwg.org/specs/rfc7541.html#static.table.definition
        //15	accept-charset	
        //16	accept-encoding gzip, deflate
        //17	accept-language	
        //18	accept-ranges	
        //19	accept	
        //20	access-control-allow-origin	
        //21	age	
        //22	allow	
        //23	authorization	
        //24	cache-control	
        //25	content-disposition	
        //26	content-encoding	
        //27	content-language	
        //28	content-length	
        //29	content-location	
        //30	content-range	
        //31	content-type	
        //32	cookie	
        //33	date	
        //34	etag	
        //35	expect	
        //36	expires	
        //37	from	
        //38	host	
        //39	if-match	
        //40	if-modified-since	
        //41	if-none-match	
        //42	if-range	
        //43	if-unmodified-since	
        //44	last-modified	
        //45	link	
        //46	location	
        //47	max-forwards	
        //48	proxy-authenticate	
        //49	proxy-authorization	
        //50	range	
        //51	referer	
        //52	refresh	
        //53	retry-after	
        //54	server	
        //55	set-cookie	
        //56	strict-transport-security	
        //57	transfer-encoding	
        //58	user-agent	
        //59	vary	
        //60	via	
        //61	www-authenticate
        private static Dictionary<string, int> _StaticTable =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            //{ HttpHeaders.ProxyConnection, 0 },
            { HttpHeaders.Upgrade, 0 },
            { HttpHeaders.Connection, 0 },//不允许出现的头
            { HttpHeaders.AcceptCharset, 15 },
            { HttpHeaders.AcceptEncoding, 16 },
            { HttpHeaders.AcceptLanguage, 17 },
            { HttpHeaders.AcceptRanges, 18 },
            { HttpHeaders.Accept, 19 },
            { HttpHeaders.AccessControlAllowOrigin , 20 },
            { HttpHeaders.Age , 21 },
            { HttpHeaders.Allow , 22 },
            { HttpHeaders.Authorization , 23 },
            { HttpHeaders.CacheControl , 24 },
            { HttpHeaders.ContentDisposition , 25 },
            { HttpHeaders.ContentEncoding , 26 },
            { HttpHeaders.ContentLanguage , 27 },
            { HttpHeaders.ContentLength , 28 },
            { HttpHeaders.ContentLocation , 29 },
            { HttpHeaders.ContentRange , 30 },
            { HttpHeaders.ContentType , 31 },
            { HttpHeaders.Cookie , 32 },
            { HttpHeaders.Date , 33 },
            { HttpHeaders.ETag , 34 },
            { HttpHeaders.Expect , 35 },
            { HttpHeaders.Expires , 36 },
            { HttpHeaders.From , 37 },
            { HttpHeaders.Host , 38 },//这个不要了
            { HttpHeaders.IfMatch , 39 },
            { HttpHeaders.IfModifiedSince , 40 },
            { HttpHeaders.IfNoneMatch , 41 },
            { HttpHeaders.IfRange , 42 },
            { HttpHeaders.IfUnmodifiedSince , 43 },
            { HttpHeaders.LastModified , 44 },
            { HttpHeaders.Link , 45 },
            { HttpHeaders.Location , 46 },
            { HttpHeaders.MaxForwards , 47 },
            { HttpHeaders.ProxyAuthenticate , 48 },
            { HttpHeaders.ProxyAuthorization , 49 },
            { HttpHeaders.Range , 50 },
            { HttpHeaders.Referer , 51 },
            { HttpHeaders.Refresh , 52 },
            { HttpHeaders.RetryAfter , 53 },
            { HttpHeaders.Server , 54 },
            { HttpHeaders.SetCookie , 55 },
            { HttpHeaders.StrictTransportSecurity , 56 },
            { HttpHeaders.TransferEncoding , 57 },//不要
            { HttpHeaders.UserAgent , 58 },
            { HttpHeaders.Vary , 59 },
            { HttpHeaders.Via , 60 },
            { HttpHeaders.WwwAuthenticate , 61 }
        };

        //TODO? 先使用静态表 以后扩展
        public EncoderTable(int maxSize)
        {
            if (maxSize < 0)
                throw new ArgumentOutOfRangeException(nameof(maxSize));

            //var headers = new Http2Headers();
            //headers.Add("Server", "MyServer", true);是否加入索引
        }

        public bool TryGetIndex(string name, out int index)
        {
            return _StaticTable.TryGetValue(name, out index);
        }
    }
}
