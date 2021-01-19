using System;
using System.Extensions.Net;
using System.Extensions.Http;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace WebSample
{
    public class SharedModule : IHttpModule
    {
        public SharedModule() 
        {
            //(for TEST)
            Task.Run(async () =>
            {
                for (; ; )
                {
                    if (_logQueue.TryDequeue(out var message))
                    {
                        Console.WriteLine(message);
                        continue;
                    }
                    message = await _logQueue.WaitAsync();
                    Console.WriteLine(message);
                }
            });
        }
        public IHttpHandler Handler { get; set; }
        private ProducerConsumerQueue<string> _logQueue = new ProducerConsumerQueue<string>();
        public async Task<HttpResponse> HandleAsync(HttpRequest request)
        {
            //use your log lib
            _logQueue.Enqueue($"LOG:{DateTime.Now},{request.Connection().RemoteEndPoint},{request.Method}:{request.Url}");//for test

            //Host filter
            //if (!request.Url.Host.EqualsIgnoreCase("localhost")) 
            //{
            //}
            //CSRF Referer
            //if (request.Method == HttpMethod.Post)
            //{
            //    //request.Headers.TryGetValue(HttpHeaders.Referer, out var referer)
            //}
            var response = await Handler.HandleAsync(request);
            if (response == null)
                return null;

            //http StringContent=>Compression
            //OR request.Url.Scheme.EqualsIgnoreCase("http")
            if (request.Connection().Security == null && response.Content is StringContent)
            {
                response.UseCompression(request, 9, 9, 11);
            }
            response.Headers.Add(HttpHeaders.Server, "MY-SERVER");
            return response;
        }
    }
}
