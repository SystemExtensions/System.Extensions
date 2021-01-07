
namespace System.Extensions.Http
{
    public class HttpResponse
    {
        public HttpResponse()
        {
            _properties = new PropertyCollection<HttpResponse>();
            _headers = new HttpHeaders();
        }
        public HttpResponse(IHttpHeaders headers)
        {
            if (headers == null)
                throw new ArgumentNullException(nameof(headers));

            _properties = new PropertyCollection<HttpResponse>();
            _headers = headers;
        }

        private PropertyCollection<HttpResponse> _properties;
        private IHttpHeaders _headers;

        public PropertyCollection<HttpResponse> Properties => _properties;

        //virtual?
        public HttpVersion Version { get; set; }

        /// <summary>
        /// StatusCode
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// ReasonPhrase
        /// </summary>
        public string ReasonPhrase { get; set; }

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
                sb.Write("Version: ");
                sb.Write(Version == null ? "<null>" : Version.ToString());
                sb.Write(", StatusCode: ");
                sb.Write(StatusCode.ToString());
                sb.Write(", ReasonPhrase: '");
                sb.Write(ReasonPhrase);
                sb.Write("', Headers: ");
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
