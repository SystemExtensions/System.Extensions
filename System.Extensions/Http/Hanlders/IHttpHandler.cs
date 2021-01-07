
namespace System.Extensions.Http
{
    using System.Threading.Tasks;
    public interface IHttpHandler
    {
        Task<HttpResponse> HandleAsync(HttpRequest request);
    }
}
