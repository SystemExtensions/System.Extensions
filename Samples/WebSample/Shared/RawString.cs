
namespace WebSample
{
    using System;
    using System.Text;
    public class RawString
    {
        private string _value;
        public RawString() 
        {
        
        }
        public RawString(string value)
        {
            _value = value;
        }
        public string Value => _value;
        public void Write(JsonWriter writer) 
        {
            writer.WriteString(_value);
        }
        public void Read(JsonReader reader) 
        {
            _value = reader.GetString();
        }

        public static implicit operator string(RawString @this)=> @this?._value;

        public static implicit operator RawString(string value)=> new RawString(value);
        static RawString() 
        {
            //using System.Data;
            //using Microsoft.Data.Sqlite;
            //SqlDbExtensions.RegisterDbReader<SqliteDataReader, RawString>((reader, i) => new RawString(reader.GetString(i)));
            //SqlDbExtensions.RegisterDbParameter<SqliteConnection, RawString>((value) => ((RawString)value).Value);
        }
    }
    public class RawStringAttribute : Attribute 
    {
    
    }
}
