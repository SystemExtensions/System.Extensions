using System;
using System.Collections.Generic;

namespace BasicSample
{
   public  class PropertyCollectionSample
    {
        public static void Run()
        {
            var p1 = new Person()
            {
                Name = "ZhangHe",
                Age = int.MaxValue
            };

            p1.Height("1.11");
            var height = p1.Height();
            Console.WriteLine(height);

            p1.Items().Add("StringKey", "123456");
            Console.WriteLine(p1.Items()["StringKey"]);
        }
    }
    public static class PersonExtensions
    {
        private static Property<Person> _Height = new Property<Person>("Height");
        public static string Height(this Person @this)
        {
            return (string)@this.Properties[_Height];
        }
        public static Person Height(this Person @this, string height)
        {
            @this.Properties[_Height] = height;
            return @this;
        }
        //传统的
        //tradition Properties
        private static Property<Person> _Items = new Property<Person>("Items");
        public static IDictionary<string, object> Items(this Person @this)
        {
            var items = (IDictionary<string, object>)@this.Properties[_Items];
            if (items == null)
            {
                items = new Dictionary<string, object>();
                @this.Properties[_Items] = items;
            }
            return items;
        }
    }
    public class Person
    {
        public PropertyCollection<Person> Properties { get; } = new PropertyCollection<Person>();
        public string Name { get; set; }
        public int Age { get; set; }
    }
}