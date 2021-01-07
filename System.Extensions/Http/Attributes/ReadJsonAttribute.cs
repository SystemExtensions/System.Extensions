
namespace System.Extensions.Http
{
    using System.Text;
    using System.Threading.Tasks;
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class ReadJsonAttribute : Attribute
    {
        #region private
        private static int _MaxLength = 1 << 20;//1M
        private int _maxLength;
        #endregion
        public ReadJsonAttribute() 
        {
            _maxLength = _MaxLength;
        }
        public ReadJsonAttribute(int maxLength) 
        {
            _maxLength = maxLength;
        }
        public async Task<object> ReadAsync<T>(HttpRequest request)
        {
            if (request == null)
                return null;

            //TODO? ContentType
            var encoding = FeaturesExtensions.GetEncoding(request) ?? Encoding.UTF8;
            var content = request.Content.AsBounded(_maxLength);
            //try
            //{

            //}
            //catch
            //{
            //    return null;
            //}
            return await content.ReadJsonAsync<T>(encoding);
        }
    }
}
