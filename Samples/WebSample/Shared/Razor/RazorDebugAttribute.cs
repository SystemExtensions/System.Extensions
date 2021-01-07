namespace Microsoft.AspNetCore.Razor.Hosting
{
    using System;
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    public sealed class RazorDebugAttribute : Attribute
    {
        public RazorDebugAttribute(string @namespace, string baseType, string razorPath, Type type)
        {
            if (@namespace == null)
                throw new ArgumentNullException(nameof(@namespace));
            if (baseType == null)
                throw new ArgumentNullException(nameof(baseType));
            if (razorPath == null)
                throw new ArgumentNullException(nameof(razorPath));
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            Namespace = @namespace;
            BaseType = baseType;
            RazorPath = razorPath;
            Type = type;
        }
        public string Namespace { get; }
        public string BaseType { get; }
        public string RazorPath { get; }
        public Type Type { get; }
    }
}