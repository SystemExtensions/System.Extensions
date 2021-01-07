
namespace System.Extensions.Http
{
    public sealed class HttpVersion : IEquatable<HttpVersion>, IComparable<HttpVersion>
    {
        public static readonly HttpVersion Version9 = new HttpVersion(0, 9);
        public static readonly HttpVersion Version10 = new HttpVersion(1, 0);
        public static readonly HttpVersion Version11 = new HttpVersion(1, 1);
        public static readonly HttpVersion Version20 = new HttpVersion(2, 0);
        #region HttpVersion
        private HttpVersion(int major, int minor)
        {
            _major = major;
            _minor = minor;
            _version = $"HTTP/{major}.{minor}";
            _hashcode = _version.GetHashCode();
        }
        private int _major;
        private int _minor;
        private int _hashcode;
        private string _version;
        public int Major => _major;
        public int Minor => _minor;
        public bool Equals(HttpVersion other) => ReferenceEquals(this, other);
        public override bool Equals(object obj) => Equals(obj as HttpVersion);
        public override int GetHashCode() => _hashcode;
        public override string ToString()=> _version;
        public static bool operator ==(HttpVersion left, HttpVersion right) => ReferenceEquals(left, right);
        public static bool operator !=(HttpVersion left, HttpVersion right) => !(left == right);
        public int CompareTo(HttpVersion other)
        {
            if (other == null)
                return 1;

            if (_major != other._major)
                return _major > other._major ? 1 : -1;

            if (_minor != other._minor)
                return _minor > other._minor ? 1 : -1;

            return 0;
        }
        public static bool operator <(HttpVersion left, HttpVersion right)
        {
            if (left == null)
                throw new ArgumentNullException(nameof(left));

            return left.CompareTo(right) < 0;
        }
        public static bool operator <=(HttpVersion left, HttpVersion right)
        {
            if (left == null)
                throw new ArgumentNullException(nameof(left));

            return left.CompareTo(right) <= 0;
        }
        public static bool operator >(HttpVersion left, HttpVersion right)
        {
            return right < left;
        }
        public static bool operator >=(HttpVersion left, HttpVersion right)
        {
            return right <= left;
        }
        #endregion
    }
}
