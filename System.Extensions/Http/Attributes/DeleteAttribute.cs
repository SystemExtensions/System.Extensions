
namespace System.Extensions.Http
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class DeleteAttribute : Attribute
    {
        public DeleteAttribute(string template)
        {
            if (template == null)
                throw new ArgumentNullException(nameof(template));

            _template = template;
        }

        private string _template;
        public string Template => _template;
    }
}
