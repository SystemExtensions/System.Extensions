using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Buffers;
using System.Runtime.Serialization;
using System.Linq.Expressions;
using System.Collections;

namespace BasicSample
{
    public class JsonWriterSample
    {
        public static void Run() 
        {
            JsonWriter.Register<long>((value, writer) =>
            {
                writer.WriteString(value.ToString());
            });
            //OR
            //JsonWriter.Register(typeof(long), (value, writer) => {
            //    var writeString = typeof(JsonWriter).GetMethod("WriteString", new[] { typeof(string) });
            //    var toString = typeof(long).GetMethod("ToString", Type.EmptyTypes);
            //    return Expression.Call(writer, writeString, Expression.Call(value, toString));
            //});
            //OR
            //JsonWriter.Register((type, value, writer) =>
            //{
            //    if (type != typeof(long))
            //        return null;

            //    var writeString = typeof(JsonWriter).GetMethod("WriteString", new[] { typeof(string) });
            //    var toString = typeof(long).GetMethod("ToString", Type.EmptyTypes);
            //    return Expression.Call(writer, writeString, Expression.Call(value, toString));
            //});


            //=>String
            Console.WriteLine(JsonWriter.ToJson<int>(int.MaxValue));
            Console.WriteLine(JsonWriter.ToJson<bool>(true));
            Console.WriteLine(JsonWriter.ToJson<string>("ZhangHe"));
            Console.WriteLine(JsonWriter.ToJson<long>(long.MaxValue));
            Console.WriteLine(JsonWriter.ToJson(typeof(int), int.MaxValue));
            Console.WriteLine(JsonWriter.ToJson(typeof(bool), true));
            Console.WriteLine(JsonWriter.ToJson(typeof(string), "ZhangHe"));
            Console.WriteLine(JsonWriter.ToJson(typeof(long), long.MaxValue));
            Console.WriteLine(JsonWriter.ToJson<object>(int.MaxValue));
            Console.WriteLine(JsonWriter.ToJson<object>(true));
            Console.WriteLine(JsonWriter.ToJson<object>("ZhangHe"));
            Console.WriteLine(JsonWriter.ToJson<object>(long.MaxValue));


            //JsonWriter.RegisterProperty((property) => StringExtensions.ToSnakeCase(property.Name));
            JsonWriter.RegisterProperty<Guid>((format) => format == "My", (value, writer) => {
                writer.WriteString(value.ToString("N"));
            });
            var c1 = new TestClass1()
            {
                String = "ZhangHe{}[]\r\n",
                Int = int.MaxValue,
                Bool = true,
                Double = double.NaN,
                Long = long.MinValue,
                DateTime=DateTime.Now,
                Guid=Guid.NewGuid(),
                ExtensionData = new Dictionary<string, object>()
                {
                    { "E1" ,long.MaxValue},
                    { "E2",double.PositiveInfinity}
                }
            };
            JsonWriter.Register(typeof(TestClass1), Expression.Parameter(typeof(TestClass1), "value"), Expression.Parameter(typeof(JsonWriter), "writer"), out var expression, out _);
            Console.WriteLine(expression);//Use Lib To See Code



            Console.WriteLine(JsonWriter.ToJson<TestClass1>(c1));
            Console.WriteLine(JsonWriter.ToJson<List<TestClass1>>(new List<TestClass1>() { c1 ,c1}));
            Console.WriteLine(JsonWriter.ToJsonIndent<TestClass1>(c1));
            Console.WriteLine(JsonWriter.ToJsonIndent<List<TestClass1>>(new List<TestClass1>() { c1 ,c1}));


            var s1 = Buffer<char>.Create(1);
            JsonWriter.ToJson<TestClass1>(c1, s1);//BufferWriter<char>
            Console.WriteLine(s1);

            var t1 = new StringWriter();//new StreamWriter(new FileStream())
            JsonWriter.ToJson<TestClass1>(c1, t1);//TextWriter
            Console.WriteLine(t1);



            //var t2 = new StringWriter();
            //var writer1 = JsonWriter.Create(s1);
            //var writer1 = JsonWriter.CreateIndent(s1, "\t", Environment.NewLine);
            var s2 = Buffer<char>.Create(1);
            var writer1 = JsonWriter.Create(s2);
            //var writer1 = JsonWriter.CreateIndent(s2, "\t", Environment.NewLine);
            writer1.WriteStartArray();
            writer1.WriteNull();
            writer1.WriteBoolean(true);
            writer1.WriteString("ABCDEF");
            writer1.WriteNumber(int.MaxValue);
            writer1.WriteNumber(decimal.MaxValue);
            writer1.WriteStartObject();
            writer1.WriteProperty("Name");
            writer1.WriteString("ZhangHe".AsSpan());
            writer1.WriteProperty("Double");
            writer1.WriteNumber(1.22);
            writer1.WriteEndObject();
            writer1.WriteEndArray();
            Console.WriteLine(s2.ToString());


            //------------------------------------------------------------------------
            Console.WriteLine(JsonWriter.ToJsonIndent(new TestClass2() { 
                Code=0,
                Message="ok",
                Data=new[] {"A","B","C" },
                ExtensionData=new PageData() 
                {
                    PageSize=10,
                    PagetNumber=1,
                    Records=100
                }
            }));
        }
        public class TestClass1
        {
            [DataMember(Name ="String1")]
            public string String { get; set; }
            public int Int { get; set; }
            public bool Bool { get; set; }
            public double Double { get; set; }
            [DataMember(Order = 100)]//bigger forward (default 0)
            public long Long { get; set; }
            public long? NullLong { get; set; }
            [DataFormat("yyyy-MM-dd HH:mm:ss")]
            public DateTime DateTime { get; set; }
            [DataFormat("My")]
            public Guid Guid { get; set; }
            [DataMember(EmitDefaultValue = false)]
            public object NullObject { get; set; }
            //如果是普通属性加上[DataMember]
            //If it is a normal property add [DataMember]
            //能遍历KeyValuePair<TKey, TValue>的类型都是支持的
            //Type that can Foreach KeyValuePair<TKey, TValue> is supported
            public Dictionary<string, object> ExtensionData { get; set; }
        }
        public class TestClass2 
        {
            public int Code { get; set; }
            public string Message { get; set; }
            public object Data { get; set; }
            public PageData ExtensionData { get; set; }
        }
        public class PageData//Or :IEnumerable<KeyValuePair<>>
        {
            public int Records { get; set; }
            public int PageSize { get; set; }
            public int PagetNumber { get; set; }
            public Enumerator GetEnumerator() => new Enumerator(this);
            public struct Enumerator
            {
                public Enumerator(PageData pageData)
                {
                    _pageData = pageData;
                    _state = 0;
                    _current = default;
                }
                private PageData _pageData;
                private int _state;
                private KeyValuePair<string, int> _current;
                public KeyValuePair<string, int> Current => _current;
                public bool MoveNext()
                {
                    switch (_state)
                    {
                        case 0:
                            _current = new KeyValuePair<string, int>("Records", _pageData.Records);
                            break;
                        case 1:
                            _current = new KeyValuePair<string, int>("PageSize", _pageData.PageSize);
                            break;
                        case 2:
                            _current = new KeyValuePair<string, int>("PagetNumber", _pageData.PagetNumber);
                            break;
                        default:
                            return false;
                    }
                    _state += 1;
                    return true;
                }

            }
        }
    }
}