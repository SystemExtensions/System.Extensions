using System;
using System.Collections.Generic;
using System.Text;
using System.Linq.Expressions;
using System.Dynamic;
using System.IO;
using System.Buffers;
using System.Runtime.Serialization;

namespace BasicSample
{
    public class JsonReaderSample
    {
        public static void Run() 
        {
            JsonReader.Register<long>((reader) =>
            {
                //if (reader.IsNumber)
                //    return reader.GetInt64();

                return long.Parse(reader.GetString());
            });
            //OR
            //JsonReader.Register(typeof(long), (reader) =>
            //{
            //    var getString = typeof(JsonReader).GetMethod("GetString", Type.EmptyTypes);
            //    var parse = typeof(long).GetMethod("Parse", new[] { typeof(string) });
            //    return Expression.Call(parse, Expression.Call(reader, getString));
            //});
            //OR
            //JsonReader.Register((type, reader) =>
            //{
            //    if (type != typeof(long))
            //        return null;

            //    var getString = typeof(JsonReader).GetMethod("GetString", Type.EmptyTypes);
            //    var parse = typeof(long).GetMethod("Parse", new[] { typeof(string) });
            //    return Expression.Call(parse, Expression.Call(reader, getString));
            //});


            //String=>
            Console.WriteLine(JsonReader.FromJson<int>("2147483647"));
            Console.WriteLine(JsonReader.FromJson<bool>("true"));
            Console.WriteLine(JsonReader.FromJson<string>("\"ZhangHe\""));
            Console.WriteLine(JsonReader.FromJson<long>("\"9223372036854775807\""));
            Console.WriteLine(JsonReader.FromJson(typeof(int), "2147483647"));
            Console.WriteLine(JsonReader.FromJson(typeof(bool), "true"));
            Console.WriteLine(JsonReader.FromJson(typeof(string), "\"ZhangHe\""));
            Console.WriteLine(JsonReader.FromJson(typeof(long), "\"9223372036854775807\""));


            //NOT JsonReader.FromJson<dynamic>
            dynamic dObj1 = JsonReader.FromJson<DynamicObject>("{\"Name\":\"ZhangHe\",\"Age\":30}");
            Console.WriteLine((string)dObj1.Name);
            Console.WriteLine((int)dObj1.Age);

            //FromJson5
            dynamic dObj2 = JsonReader.FromJson5<DynamicObject>(
                @"{
                //comment1
                Name:'ZhangHe',
                /*
                 *comment2
                 */
                Age:30}");
            Console.WriteLine((string)dObj2.Name);
            Console.WriteLine((int)dObj2.Age);

            //dynamic Method
            Console.WriteLine(dObj2.IsArray());
            Console.WriteLine(dObj2.IsObject());
            Console.WriteLine(dObj2.IsUndefined());
            Console.WriteLine(dObj2.IsNull());
            Console.WriteLine(dObj2.IsString());
            Console.WriteLine(dObj2.IsBoolean());
            Console.WriteLine(dObj2.IsNumber());
            Console.WriteLine(dObj2.Count());
            Console.WriteLine(dObj2.Properties());

            //object (List<object> Dictionary<string, object> decimal double string bool)
            var obj1 = JsonReader.FromJson<object>("[123,{\"Name\":\"ZhangHe\",\"Age\":30}]");
            var objArray1 = (List<object>)obj1;
            Console.WriteLine(objArray1[0]);
            var objDic1 = (Dictionary<string, object>)objArray1[1];
            Console.WriteLine(objDic1["Name"]);
            Console.WriteLine(objDic1["Age"]);


            //JsonReader.RegisterProperty((property) => StringExtensions.ToSnakeCase(property.Name));
            JsonReader.RegisterProperty<Guid>((format) => format == "My", (reader) =>{
                return Guid.ParseExact(reader.GetString(),"N");
            });

            JsonReader.Register(typeof(TestClass1), Expression.Parameter(typeof(JsonReader), "reader"), out var expression1, out _);
            Console.WriteLine(expression1);//Use Lib To See Code


            //Can Create FromJson<T>(T value,) by expression2
            JsonReader.Register(typeof(TestClass1), Expression.Parameter(typeof(TestClass1), "value"), Expression.Parameter(typeof(JsonReader), "reader"), out var expression2);
            Console.WriteLine(expression2);

            var jsonString1 = "{\"String1\":\"ZhangHe{}[]\\r\\n\",\"Int\":2147483647,\"Bool\":true,\"Double\":NaN,\"Long\":\"-9223372036854775808\",\"NullLong\":null,\"DateTime\":\"2020-10-29 09:21:09\",\"Guid\":\"cf01f9f22d2e4bb2b2181e27108790ab\",\"E1\":9223372036854775807,\"E2\":Infinity}";
            var jsonString2 = "[{\"String1\":\"ZhangHe{}[]\\r\\n\",\"Int\":2147483647,\"Bool\":true,\"Double\":NaN,\"Long\":\"-9223372036854775808\",\"NullLong\":null,\"DateTime\":\"2020-10-29 09:21:09\",\"Guid\":\"cf01f9f22d2e4bb2b2181e27108790ab\",\"E1\":9223372036854775807,\"E2\":Infinity}]";


            var c1 = JsonReader.FromJson<TestClass1>(jsonString1);
            Console.WriteLine(c1.Long);
            var c2 = JsonReader.FromJson<List<TestClass1>>(jsonString2);
            Console.WriteLine(c2[0].Long);


            var span1 = jsonString1.AsSpan();
            var c3 = JsonReader.FromJson<TestClass1>(span1);//ReadOnlySpan<char>
            Console.WriteLine(c3.Long);

            unsafe 
            {
                fixed (char* pData = jsonString1) 
                {
                    var c4 = JsonReader.FromJson<TestClass1>(pData,jsonString1.Length);//char*
                    Console.WriteLine(c4.Long);
                }
            }

            var s1 = Buffer<char>.Create(1);
            s1.Write(jsonString1);
            var c5 = JsonReader.FromJson<TestClass1>(s1.Sequence);//ReadOnlySequence<char>
            Console.WriteLine(c5.Long);


            var t1 = new StringReader(jsonString1);//new StreamReader(new FileStream())
            var c6 = JsonReader.FromJson<TestClass1>(t1);//TextReader
            Console.WriteLine(c6.Long);


            var jsonString3 = "[{\"Name\":\"ZhangHe\",\"Age\":30,\"Array\":[1,2,3,4]},{\"Name\":\"LiSi\",\"Age\":20,\"Obj\":{\"P1\":1,\"P2\":2}}]";
            var reader1 = JsonReader.Create(jsonString3);
            //JsonReader.CreateJson5();
            //ReadOnlySpan<char>,char*,ReadOnlySequence<char>,TextReader
            while (reader1.Read())
            {
                if (reader1.IsProperty)
                {
                    Console.WriteLine(reader1.GetProperty());
                    reader1.Read();
                    if (reader1.IsNull)
                        Console.WriteLine("Null");
                    else if (reader1.IsString)
                        Console.WriteLine(reader1.GetString());
                    else if (reader1.IsNumber)
                    {
                        reader1.GetNumber(out var number);//reader1.GetInt32();
                        //int.Parse(number);
                        Console.WriteLine(new string(number));
                    }
                    else if (reader1.IsBoolean)
                        Console.WriteLine(reader1.GetBoolean());
                    else if (reader1.IsStartArray) 
                    {
                        reader1.Skip();
                        Console.WriteLine("SKIP");
                        //OR
                        //continue;
                    }
                    else if (reader1.IsStartObject)
                        continue;
                    else
                        throw new FormatException("Bad JSON");
                }
            }

        }
        public class TestClass1
        {
            [DataMember(Name = "String1")]
            public string String { get; set; }
            public int Int { get; set; }
            public bool Bool { get; set; }
            public double Double { get; set; }
            [DataMember(IsRequired =true)]//no Long Property will throw exception
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
            public Dictionary<string, object> ExtensionData { get; set; }
        }
    }
}
