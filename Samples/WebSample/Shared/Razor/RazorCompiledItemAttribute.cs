namespace Microsoft.AspNetCore.Razor.Hosting
{
    using System;
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
    public sealed class RazorCompiledItemAttribute : Attribute
    {
        public RazorCompiledItemAttribute(Type type, string kind, string identifier)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (kind == null)
                throw new ArgumentNullException(nameof(kind));

            Type = type;
            Kind = kind;
            Identifier = identifier;
        }
        public string Kind { get; }
        public string Identifier { get; }
        public Type Type { get; }
    }
}