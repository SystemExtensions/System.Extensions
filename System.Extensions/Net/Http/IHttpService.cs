
namespace System.Extensions.Net
{
    using System.Extensions.Http;
    public interface IHttpService : IConnectionHandler
    {
        IHttpHandler Handler { get; set; }
    }
}
