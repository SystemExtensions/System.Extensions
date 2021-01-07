
namespace System.Extensions.Http
{
    public sealed class HttpMethod : IEquatable<HttpMethod>
    {
        public static readonly HttpMethod Get = new HttpMethod("GET");
        public static readonly HttpMethod Put = new HttpMethod("PUT");
        public static readonly HttpMethod Post = new HttpMethod("POST");
        public static readonly HttpMethod Head = new HttpMethod("HEAD");
        public static readonly HttpMethod Trace = new HttpMethod("TRACE");
        public static readonly HttpMethod Patch = new HttpMethod("PATCH");
        public static readonly HttpMethod Delete = new HttpMethod("DELETE");
        public static readonly HttpMethod Options = new HttpMethod("OPTIONS");
        public static readonly HttpMethod Connect = new HttpMethod("CONNECT");
        #region HttpMethod
        private HttpMethod(string method)
        {
            _method = method;
            _hashcode = method.GetHashCode();
        }
        private readonly int _hashcode;
        private readonly string _method;
        public bool Equals(HttpMethod other) => ReferenceEquals(this, other);
        public override bool Equals(object obj) => Equals(obj as HttpMethod);
        public override int GetHashCode() => _hashcode;
        public override string ToString() => _method;
        public static bool operator ==(HttpMethod left, HttpMethod right) => ReferenceEquals(left, right);
        public static bool operator !=(HttpMethod left, HttpMethod right)=> !(left == right);
        #endregion
    }
}
