System.Extensions
============

.NET next generation programming

## Packages

Package                                   | NuGet
------------------------------------------|-----------------------------
`System.Extensions.Core` | [v3.1.0](https://www.nuget.org/packages/System.Extensions.Core)
`System.Extensions.RazorCompilation` | [v3.1.0](https://www.nuget.org/packages/System.Extensions.RazorCompilation)

## Get Started

```csharp
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
[View More](https://github.com/SystemExtensions/System.Extensions/tree/master/Samples)

## Get Support

zhanghe1024@qq.com

## License

System.Extensions is licensed under the [MIT](LICENSE) license.

## Donation

- [WeChat](https://share.weiyun.com/85mNdQnS)
- [Alipay](https://share.weiyun.com/nMqXbWHU)
- [PayPal](https://www.paypal.me/zhanghe1024)


return signed USB disk with two year code backup(send receiving address via email)

- 32G donate over 128RMB(20USD)
- 64G donate over 256RMB(40USD)
- 128G donate over 512RMB(80USD)
___extra payment of postage is required in non mainland China___
