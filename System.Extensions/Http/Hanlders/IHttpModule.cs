
namespace System.Extensions.Http
{
    public interface IHttpModule : IHttpHandler
    {
        IHttpHandler Handler { get; set; }
    }
}
