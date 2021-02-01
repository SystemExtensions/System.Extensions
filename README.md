System.Extensions
============

### 轻量的 Http Https Http2(Server Client Proxy) Json Orm开发框架(无依赖) 可以代替AspNetCore开发
### 基础库的扩展和重写
- **[Url](System.Extensions/System/Url.cs)**, Domain支持,Encode Decode  ReadOnlySpan<char> ReadOnlySequence<char> 支持
- **[Provider<T>](System.Extensions/System/Provider.cs)**, 对象池,可根据CPU个数创建
- **[EncodingExtensions](System.Extensions/System/Text/EncodingExtensions.cs)**, 增加GetBytes GetChars GetByteCount GetString API 支持 ReadOnlySequence<char> ReadOnlySequence<byte>
- **[ExpressionExtensions](System.Extensions/System/Linq/Expressions/ExpressionExtensions.cs)**, 扩展Invoke() 进行表达式树计算
- **[ReflectionExtensions](System.Extensions/System/Reflection/ReflectionExtensions.cs)**, 提供将方法字段属性编译成委托,扩展GetGeneralProperties 获取属性保持声明顺序,接口类型
- **[DirectoryInfoExtensions](System.Extensions/System/IO/DirectoryInfoExtensions.cs)**, 方便防范目录遍历攻击,通过StartsWith方式有风险
- **[Buffer<T>](System.Extensions/System/Buffers/Buffer.cs)**, char byte的buffer读写更简单
- **[UnmanagedMemory<T>](System.Extensions/System/Buffers/UnmanagedMemory.cs)**, 堆内存、栈内存 包装成Memory<T>
- **[KeyValueCollection<TKey, TValue>](System.Extensions/System/Collections/Generic/KeyValueCollection.cs)**, 顺序的键值字典 允许相同键
- **[Cache<TKey, TValue>](System.Extensions/System/Collections/Concurrent/Cache.cs)**, 内存缓存字典 可以手动、定时、GC时回收
- **[Constructor](System.Extensions/System/Reflection/Constructor.cs)**, 类IOC容器 可以提供对象 也可以提供表达式树
- **[Synchronization<T>](System.Extensions/System/Threading/Synchronization.cs)**, 同步锁 根据IEqualityComparer<T>判断去锁对象(订单号,用户ID)
- **[TaskTimeoutQueue](System.Extensions/System/Threading/Tasks/TaskTimeoutQueue.cs)**, 基于队列的超时扩展
- **[...](System.Extensions/System)**

### Light Http Https Http2(Server Client Proxy) Json Orm framework(NO Dependencies) like AspNetCore
### Extension and rewriting of basic library
- **[Url](System.Extensions/System/Url.cs)**, Domain support,Encode Decode ReadOnlySpan<char> ReadOnlySequence<char> support
- **[Provider<T>](System.Extensions/System/Provider.cs)**, Object pool,From Processor support
- **[EncodingExtensions](System.Extensions/System/Text/EncodingExtensions.cs)**,  GetBytes GetChars GetByteCount GetString API for ReadOnlySequence<char> ReadOnlySequence<byte>
- **[ExpressionExtensions](System.Extensions/System/Linq/Expressions/ExpressionExtensions.cs)**, Invoke() for evaluate expression tree
- **[ReflectionExtensions](System.Extensions/System/Reflection/ReflectionExtensions.cs)**, Compile MethodInfo FieldInfo PropertyInfo to Delegate, GetGeneralProperties maintain declaration order, interface type
- **[DirectoryInfoExtensions](System.Extensions/System/IO/DirectoryInfoExtensions.cs)**, Prevent directory traversal attack. There are risks through Startswith
- **[Buffer<T>](System.Extensions/System/Buffers/Buffer.cs)**,  It's easier to char byte read and write
- **[UnmanagedMemory<T>](System.Extensions/System/Buffers/UnmanagedMemory.cs)**, Heap memory, stack memory wrap Memory<T>
- **[KeyValueCollection<TKey, TValue>](System.Extensions/System/Collections/Generic/KeyValueCollection.cs)**, Ordered key value dictionary,allow the same key
- **[Cache<TKey, TValue>](System.Extensions/System/Collections/Concurrent/Cache.cs)**, MemoryCache
- **[Constructor](System.Extensions/System/Reflection/Constructor.cs)**, Like IOC Container,provide object or expression tree
- **[Synchronization<T>](System.Extensions/System/Threading/Synchronization.cs)**, The synchronization lock through IEqualityComparer<T>(ordernumber,userid)
- **[TaskTimeoutQueue](System.Extensions/System/Threading/Tasks/TaskTimeoutQueue.cs)**, Timeout by Queue 
- **[...](System.Extensions/System)**

延伸出的基于表达式树的约束编程模式
Extended expression tree constraint programming
https://dotnetfiddle.net/fDqsMc
https://dotnetfiddle.net/uH2dpF
https://dotnetfiddle.net/46M5mi

## Packages

Package                                   | NuGet
------------------------------------------|-----------------------------
`SystemExtensions.Core` | [v3.1.1](https://www.nuget.org/packages/SystemExtensions.Core)
`SystemExtensions.RazorCompilation` | [v3.1.1](https://www.nuget.org/packages/SystemExtensions.RazorCompilation)

## Get Started

```csharp
//HttpServer
var tcpSvr = new TcpServer(9999);
tcpSvr.UseHttp((options, router) => {
	router.MapGet("/say", (req, resp) => {
		resp.Headers.Add(HttpHeaders.ContentType, "text/plain");
		resp.Content = StringContent.Create("hello world");
	});
});
tcpSvr.Start();
```
```csharp
//HttpClient
var req = new HttpRequest("http://localhost:9999/say");
try
{
	var resp = await HttpClient.Default.SendAsync(req);
	var @string = await resp.Content.ReadStringAsync();
	Console.WriteLine(@string);
}
finally
{
	req.Dispose();
}
```
```csharp
//Http2Push
var tcpSvr = new TcpServer(9999);
tcpSvr.UseHttp2((options, router) => {
	options.Certificate = new X509Certificate2(fileName, password);
	router.MapGet("/", (req, resp) => {
		resp.Headers.Add(HttpHeaders.ContentType, "text/html; charset=utf-8");
		resp.Content = StringContent.Create("<script src='/push.js'></script>");
		var pusher = req.Pusher();
		if (pusher != null)
		{
			var pushResp = new HttpResponse();
			pushResp.Headers.Add(HttpHeaders.ContentType, "text/javascript; charset=utf-8");
			pushResp.Content = StringContent.Create("alert('hello world')");
			pusher.Push("/push.js", pushResp);
		}
	});
});
tcpSvr.Start();
```
```csharp
//Json
class Person
{
	[DataMember(Name = "PersonName")]
	public string Name { get; set; }
	public int Age { get; set; }
	[DataFormat("yyyy-MM-dd HH:mm:ss")]
	public DateTime Birthday { get; set; }
}
var json = JsonWriter.ToJson<Person>(new Person()
{
	Name = "Zhang",
	Age = int.MaxValue,
	Birthday = DateTime.Now
});
//{"PersonName":"Zhang","Age":2147483647,"Birthday":"2021-01-26 12:27:15"}
var person = JsonReader.FromJson<Person>(json);
```
```csharp
//Orm
class Book
{
	public int Id { get; set; }
	[DataColumn(Name = "BookName")]
	public string Name { get; set; }
	public int CategoryId { get; set; }
	public BookCategory Category { get; set; }
	public DateTime CreateTime { get; set; }
}
[DataTable(Name = "BookCategory")]
class BookCategory
{
	public int Id { get; set; }
	public string Name { get; set; }
}
var db = SqlDb.Create<SqliteConnection>("Data Source=data.db");
var book = await db.SelectSingleAsync<Book>((b, s) => s.Navigate(b), 100/*BookId*/);
(var books, var count) = await db.SelectPagedAsync<Book>(20/*offset*/, 100/*fetch*/,
	(b, s) => s.Navigate(b), //select
	(b, s) => b.Category.Id == 1/*CategoryId*/, //where
	(b, s) => s.Desc(b.CreateTime));//orderBy
```
[View More](Samples)

## Get Support

zhanghe1024@qq.com

## License

System.Extensions is licensed under the [MIT](LICENSE) license.

## Donation

- [WeChat](https://share.weiyun.com/85mNdQnS)
- [Alipay](https://share.weiyun.com/nMqXbWHU)
- [PayPal](https://www.paypal.me/zhanghe1024)