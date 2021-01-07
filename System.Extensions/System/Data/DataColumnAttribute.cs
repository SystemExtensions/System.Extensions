
namespace System.Data
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class DataColumnAttribute : Attribute
    {
        public string Name { get; set; }

        //TODO? type length...
    }
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class IgnoreDataColumnAttribute : Attribute
    {

    }
}
