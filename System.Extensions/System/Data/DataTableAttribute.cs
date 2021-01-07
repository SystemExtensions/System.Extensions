
namespace System.Data
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class DataTableAttribute : Attribute
    {
        public string Name { get; set; }//TableName

        //public DataTableAttribute() 
        //{ }

        //public DataTableAttribute(string name)
        //{
        //    Name = name;
        //}
        //public string Key { get; set; }//PrimaryKey
    }
}
