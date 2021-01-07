
namespace System.Extensions.Http
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class GetAttribute : Attribute
    {
        public GetAttribute(string template)
        {
            if (template == null)
                throw new ArgumentNullException(nameof(template));

            _template = template;
        }

        private string _template;
        public string Template => _template;
    }
}
