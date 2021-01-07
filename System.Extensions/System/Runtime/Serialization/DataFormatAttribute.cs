
namespace System.Runtime.Serialization
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class DataFormatAttribute:Attribute
    {
        public DataFormatAttribute()
        { }
        public DataFormatAttribute(string format)
        {
            if (format == null)
                throw new ArgumentNullException(nameof(format));

            _format = format;
        }
        private string _format;
        public string Format => _format;
    }
}
