
namespace System.Extensions.Http
{
    public class HttpRequest
    {
        public HttpRequest()
        {
            _properties = new PropertyCollection<HttpRequest>();
            _url = new Url();
            _headers = new HttpHeaders();
        }
        public HttpRequest(string url)
        {
            if (url == null)
                throw new ArgumentNullException(nameof(url));

            _properties = new PropertyCollection<HttpRequest>();
            _url = new Url(url);
            _headers = new HttpHeaders();
        }
        //public HttpRequest(Url baseUri, string relativeUri)
        //{
        //    if (baseUri == null)
        //        throw new ArgumentNullException(nameof(baseUri));
        //    if (relativeUri == null)
        //        throw new ArgumentNullException(nameof(baseUri));

        //    _properties = new PropertyCollection<HttpRequest>();
        //    _url = new Url(baseUri, relativeUri);
        //    _headers = new HttpHeaders();
        //}
        public HttpRequest(Url url, IHttpHeaders headers)
        {
            if (url == null)
                throw new ArgumentNullException(nameof(url));
            if (headers == null)
                throw new ArgumentNullException(nameof(headers));

            _properties = new PropertyCollection<HttpRequest>();
            _url = url;
            _headers = headers;
        }

        private PropertyCollection<HttpRequest> _properties;
        private Url _url;
        private IHttpHeaders _headers;

        public PropertyCollection<HttpRequest> Properties => _properties;
        /// <summary>
        /// RequestLine
        /// </summary>
        public HttpMethod Method { get; set; }
        /// <summary>
        /// Url
        /// </summary>
        public Url Url => _url;
        /// <summary>
        /// Version
        /// </summary>
        public HttpVersion Version { get; set; }
        /// <summary>
        /// Headers
        /// </summary>
        public IHttpHeaders Headers => _headers;
        /// <summary>
        /// Content
        /// </summary>
        public IHttpContent Content { get; set; }

        /// <summary>
        /// ToString
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            var sb = StringExtensions.ThreadRent(out var disposable);
            try
            {
                sb.Write("Method: ");
                sb.Write(Method == null ? "<null>" : Method.ToString());
                sb.Write(", Url: '");
                sb.Write(Url == null ? "<null>" : Url.ToString());
                sb.Write("', Version: ");
                sb.Write(Version == null ? "<null>" : Version.ToString());
                sb.Write(", Headers: ");
                sb.Write(Headers.Count.ToString());
                sb.Write(", Content: ");
                sb.Write(Content == null ? "<null>" : Content.Length.ToString());
                return sb.ToString();
            }
            finally
            {
                disposable.Dispose();
            }
        }
    }
}
