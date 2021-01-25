using System;
using System.IO;
using System.Data;
using System.Text;
using System.Dynamic;
using System.Buffers;
using System.Runtime.Loader;
using System.Reflection;
using System.Diagnostics;
using System.Extensions.Net;
using System.Extensions.Http;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Data.Sqlite;
using System.Text.Encodings.Web;
using System.Linq.Expressions;
using WebSample.Models;

namespace WebSample
{
    class Program
    {
        static void Main(string[] args)
        {
            //Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));

            //Config
            //dynamic config= JsonReader.FromJson5<DynamicObject>(new StreamReader("config.json"));
            //var port = (int)config.Port;
            //var connectionString = (string)config.ConnectionString;

            //XSS
            JsonWriter.RegisterProperty((property, value, writer) => {
                if (property.DeclaringType.Namespace != "WebSample.Models")//only WebSample.Models.class
                    return null;
                if (property.PropertyType != typeof(string))
                    return null;
                if (property.IsDefined(typeof(RawStringAttribute)))
                    return null;
                if (property.DeclaringType.IsDefined(typeof(RawStringAttribute)))
                    return null;
                //if (value == null) { writer.WriteNull(); } else { writer.WriteString(HtmlEncoder.Default.Encode(value)); }
                var writeNull = typeof(JsonWriter).GetMethod("WriteNull", Type.EmptyTypes);
                var writeString = typeof(JsonWriter).GetMethod("WriteString", new[] { typeof(string) });
                var encode = typeof(HtmlEncoder).GetMethod("Encode", new[] { typeof(string) });
                return Expression.IfThenElse(
                        Expression.Equal(value, Expression.Constant(null)),
                        Expression.Call(writer, writeNull),
                        Expression.Call(writer, writeString, Expression.Call(Expression.Property(null, typeof(HtmlEncoder).GetProperty("Default")), encode, value))
                    );
            });
            //Exception
            //FeaturesExtensions.UseException((request,response,ex)=> {
            //    //request.Method,request.Version maybe null > be careful
            //    //response.UseRedirect("");
            //    return Task.CompletedTask;
            //});

            //Buffer
            //ConnectionExtensions.Register(Provider<Memory<byte>>.CreateFromProcessor(() => new UnmanagedMemory<byte>(8192).Memory, 2048));
            //StringContent.Register(Provider<Buffer<char>>.CreateFromProcessor(() => Buffer<char>.Create(4096, ArrayPool<char>.Shared, 4096, out var _), 2048, (buff) => buff.Clear());
            //MemoryContent.Register(Provider<Buffer<byte>>.CreateFromProcessor(() => Buffer<byte>.Create(8192, ArrayPool<byte>.Shared, 8192, out var _), 2048, (buff) => buff.Clear());

            var httpSvr = new TcpServer(9999);
            var http = httpSvr.UseHttp((options, router) => {
                //options.KeepAliveTimeout = 0;
                //options.ReceiveTimeout = 0;
                //options.SendTimeout = 0;

                router.MapFiles("/{*path}", "StaticFiles", null);//null for Test(use maxAge)
                //OR
                //router.MapFile("/favicon.ico", "StaticFiles/favicon.ico", null);
                //router.MapFiles("/Js/{*path}", "StaticFiles/Js/", null);
                //router.MapFiles("/Upload/{*path}", "StaticFiles/Upload/", null);

                var compiler = new HandlerCompiler();
                compiler.Register<SqlDb>(SqlDb.Create<SqliteConnection>("Data Source=data.db", cmd => Debug.WriteLine(cmd.CommandText)));
                compiler.Register<IHttp2Pusher>(req => req.Pusher());
                compiler.Register<Passport>(req => req.GetPassport());

                //if Assembly not load
                //AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(nameof(YourAssemblyName)));
                router.MapAttribute(compiler);//AssemblyLoadContext.Default.Assemblies

                //router.MapSlash();

                Console.WriteLine("GET:");
                foreach (var item in router.GetTree)
                {
                    Console.WriteLine(item.Key);
                }
                Console.WriteLine("POST:");
                foreach (var item in router.PostTree)
                {
                    Console.WriteLine(item.Key);
                }
            }).Use(new SharedModule());

            //Start(int maxConnections, int backlog)
            //Environment.ProcessorCount * 1024, 65535
            //ip,request limit better
            httpSvr.Start();

            var h2Svr = new TcpServer(9899);
            h2Svr.UseHttp2((options) => {
                options.Certificate = new X509Certificate2("server.pfx", "123456");
            }, http.Handler);
            h2Svr.Start();


            Console.WriteLine("http://localhost:9999");
            Console.WriteLine("https://localhost:9899");//Chrome --ignore-certificate-errors
            Console.WriteLine("Login Name: admin");
            Console.WriteLine("Login Password: 123456");
            Console.ReadLine();
        }

        static Program() 
        {
            if (!File.Exists("server.pfx")) //Create Test Cert
            {
                using (var rsa = RSA.Create(2048))
                {
                    var certReq = new CertificateRequest($"CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                    certReq.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
                    certReq.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation | X509KeyUsageFlags.KeyEncipherment, true));
                    var altName = new SubjectAlternativeNameBuilder();
                    altName.AddDnsName("localhsot");
                    certReq.CertificateExtensions.Add(altName.Build(true));
                    var cert = certReq.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1));
                    File.WriteAllBytes("server.pfx", cert.Export(X509ContentType.Pfx, "123456"));
                }
            }

            if (!File.Exists("data.db")) //Create sqlite db
            {
                var db = SqlDb.Create<SqliteConnection>("Data Source=data.db");
                db.Execute("CREATE TABLE [Account]([Id] INTEGER PRIMARY KEY AUTOINCREMENT,[Name] TEXT NULL,[Password] TEXT NULL)");
                db.Execute("CREATE TABLE [BookCategory]([Id] INTEGER PRIMARY KEY AUTOINCREMENT,[Name] TEXT NULL)");                
                db.Execute("CREATE TABLE [Book]([Id] INTEGER PRIMARY KEY AUTOINCREMENT,[ISBN] TEXT NULL,[Name] TEXT NULL,[Author] TEXT NULL,[ImageUrl] TEXT NULL,[CategoryId] INTEGER NULL,[CreateTime] datetime NULL)");

                db.Insert<Account>(s => new Account() { Name = "admin", Password = "123456" });

                var category1 = db.InsertIdentity<BookCategory, int>(s => new BookCategory() { Name = "Category1" });
                var category2 = db.InsertIdentity<BookCategory, int>(s => new BookCategory() { Name = "Category2" });
                var category3 = db.InsertIdentity<BookCategory, int>(s => new BookCategory() { Name = "Category3" });

                for (int i = 0; i < 5; i++)
                {
                    db.Insert<Book>(s => new Book()
                    {
                        ISBN = "<script>alert(1)</script>",
                        Name = "<script>alert(2)</script>",
                        Author = "<script>alert(3)</script>",
                        ImageUrl = "/Upload/c064c87a45c342218a00cc6cc8babc9c.png",
                        CategoryId = category1,
                        CreateTime = DateTime.Now.AddSeconds(new Random().Next(1, 1000))
                    });
                    db.Insert<Book>(s => new Book()
                    {
                        ISBN = "9787020008735",
                        Name = "Journey to the West",
                        Author = "Wu Chengen",
                        ImageUrl = "/Upload/40fc7f1a9cf54eb4a0f7e32e6dbb41b8.jpg",
                        CategoryId = category1,
                        CreateTime = DateTime.Now.AddSeconds(new Random().Next(1, 1000))
                    });
                    db.Insert<Book>(s => new Book()
                    {
                        ISBN = "9787020008728",
                        Name = "Romance of the Three Kingdoms",
                        Author = "Luo Guanzhong",
                        ImageUrl = "/Upload/14011a95982144959a8390779a51e3cc.jpg",
                        CategoryId = category2,
                        CreateTime = DateTime.Now.AddSeconds(new Random().Next(1, 1000))
                    });
                    db.Insert<Book>(s => new Book()
                    {
                        ISBN = "9787020125579",
                        Name = "Water Margin",
                        Author = "Shi Naian",
                        ImageUrl = "/Upload/a92cd80a3c0d4ff0bdb3f8ae6429e0f7.jpg",
                        CategoryId = category2,
                        CreateTime = DateTime.Now.AddSeconds(new Random().Next(1, 1000))
                    });
                    db.Insert<Book>(s => new Book()
                    {
                        ISBN = "9787020002207",
                        Name = "The Dream of Red Mansion",
                        Author = "Cao Xueqin",
                        ImageUrl = "/Upload/d0b39355e15b4a26a2937aebfa75d63e.jpg",
                        CategoryId = category3,
                        CreateTime = DateTime.Now.AddSeconds(new Random().Next(1, 1000))
                    });
                }
            }
        }
    }
}
