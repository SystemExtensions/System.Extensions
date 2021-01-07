
namespace System.Extensions.Net
{
    using System.Extensions.Http;
    public interface IHttp2Pusher
    {
        //TODO?? void Push(HttpRequest request, HttpResponse response);
        //TODO? IHttp2Service? request.Http2().Push() request.Http2().PingAsync() request.Http2().GoAwayAsync()
        //string url
        void Push(string path, HttpResponse response);
    }
}
