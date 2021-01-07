
namespace WebSample
{
    using System;
    public class ValidateAttribute:Attribute
    {
        public ValidateAttribute(string method)
        {
            if (method == null)
                throw new ArgumentNullException(nameof(method));

            _method = method;
        }
        private string _method;
        public string Method => _method;
    }
}
