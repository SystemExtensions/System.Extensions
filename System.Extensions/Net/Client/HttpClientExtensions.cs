
namespace System.Extensions.Http
{
    using System.IO;
    using System.Text;
    using System.Buffers;
    using System.Diagnostics;
    using System.Collections.Generic;
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using System.IO.Compression;
    public static class HttpClientExtensions
    {
        //TODO ReadJsonAsync(HttpRequest)
        public static async Task SendAsync(this HttpClient @this, HttpRequest request, Action<HttpResponse> handler)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            try
            {
                var response = await @this.SendAsync(request);
                handler(response);
            }
            finally
            {
                request.Dispose();
            }
        }
        public static async Task SendAsync(this HttpClient @this, HttpRequest request, Func<HttpResponse, Task> handler)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            try
            {
                var response = await @this.SendAsync(request);
                await handler(response);//TODO? ValueTask
            }
            finally
            {
                request.Dispose();
            }
        }
        public static async Task<T> ReadJsonAsync<T>(this HttpClient @this, HttpRequest request) 
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            try
            {
                var response = await @this.SendAsync(request);
                var encoding = FeaturesExtensions.GetEncoding(response) ?? Encoding.UTF8;
                return await response.Content.ReadJsonAsync<T>(encoding);
            }
            finally
            {
                request.Dispose();
            }
        }
        public static async Task<string> ReadStringAsync(this HttpClient @this, HttpRequest request)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            try
            {
                var response = await @this.SendAsync(request);
                var encoding = FeaturesExtensions.GetEncoding(response) ?? Encoding.UTF8;
                return await response.Content.ReadStringAsync(encoding);
            }
            finally
            {
                request.Dispose();
            }
        }
        public static async Task<Stream> ReadStreamAsync(this HttpClient @this, HttpRequest request)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            try
            {
                var response = await @this.SendAsync(request);
                return await response.Content.ReadStreamAsync();
            }
            finally
            {
                request.Dispose();
            }
        }
        public static async Task<long> ReadFileAsync(this HttpClient @this, HttpRequest request, string path)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            try
            {
                var response = await @this.SendAsync(request);
                return await response.Content.ReadFileAsync(path);
            }
            finally
            {
                request.Dispose();
            }
        }
        public static async Task<T> GetJsonAsync<T>(this HttpClient @this, string url)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (url == null)
                throw new ArgumentNullException(nameof(url));

            var request = new HttpRequest(url) { Method = HttpMethod.Get };
            try
            {
                var response = await @this.SendAsync(request);
                var encoding = FeaturesExtensions.GetEncoding(response) ?? Encoding.UTF8;
                return await response.Content.ReadJsonAsync<T>(encoding);
            }
            finally
            {
                request.Dispose();
            }
        }
        public static async Task<T> GetJsonAsync<T>(this HttpClient @this, string url, IQueryParams queryParams)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (url == null)
                throw new ArgumentNullException(nameof(url));

            var request = new HttpRequest(url) { Method = HttpMethod.Get };
            try
            {
                queryParams.Join(request.Url);
                var response = await @this.SendAsync(request);
                var encoding = FeaturesExtensions.GetEncoding(response) ?? Encoding.UTF8;
                return await response.Content.ReadJsonAsync<T>(encoding);
            }
            finally
            {
                request.Dispose();
            }
        }
        public static async Task<string> GetStringAsync(this HttpClient @this, string url)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (url == null)
                throw new ArgumentNullException(nameof(url));

            var request = new HttpRequest(url) { Method = HttpMethod.Get };
            try
            {
                var response = await @this.SendAsync(request);
                var encoding = FeaturesExtensions.GetEncoding(response) ?? Encoding.UTF8;
                return await response.Content.ReadStringAsync(encoding);
            }
            finally
            {
                request.Dispose();
            }
        }
        public static async Task<string> GetStringAsync(this HttpClient @this, string url, IQueryParams queryParams)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (url == null)
                throw new ArgumentNullException(nameof(url));

            var request = new HttpRequest(url) { Method = HttpMethod.Get };
            try
            {
                queryParams.Join(request.Url);
                var response = await @this.SendAsync(request);
                var encoding = FeaturesExtensions.GetEncoding(response) ?? Encoding.UTF8;
                return await response.Content.ReadStringAsync(encoding);
            }
            finally
            {
                request.Dispose();
            }
        }
        public static async Task<Stream> GetStreamAsync(this HttpClient @this, string url)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (url == null)
                throw new ArgumentNullException(nameof(url));

            var request = new HttpRequest(url) { Method = HttpMethod.Get };
            try
            {
                var response = await @this.SendAsync(request);
                return await response.Content.ReadStreamAsync();
            }
            finally
            {
                request.Dispose();
            }
        }
        public static async Task<Stream> GetStreamAsync(this HttpClient @this, string url, IQueryParams queryParams)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (url == null)
                throw new ArgumentNullException(nameof(url));

            var request = new HttpRequest(url) { Method = HttpMethod.Get };
            try
            {
                queryParams.Join(request.Url);
                var response = await @this.SendAsync(request);
                return await response.Content.ReadStreamAsync();
            }
            finally
            {
                request.Dispose();
            }
        }
        public static async Task<long> GetFileAsync(this HttpClient @this, string url, string path)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (url == null)
                throw new ArgumentNullException(nameof(url));
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            var request = new HttpRequest(url) { Method = HttpMethod.Get };
            try
            {
                var response = await @this.SendAsync(request);
                return await response.Content.ReadFileAsync(path);
            }
            finally
            {
                request.Dispose();
            }
        }
        public static async Task<long> GetFileAsync(this HttpClient @this, string url, IQueryParams queryParams, string path)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (url == null)
                throw new ArgumentNullException(nameof(url));
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            var request = new HttpRequest(url) { Method = HttpMethod.Get };
            try
            {
                queryParams.Join(request.Url);
                var response = await @this.SendAsync(request);
                return await response.Content.ReadFileAsync(path);
            }
            finally
            {
                request.Dispose();
            }
        }
        public static async Task<T> PostJsonAsync<T>(this HttpClient @this, string url, IFormParams formParams)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (url == null)
                throw new ArgumentNullException(nameof(url));
            if (formParams == null)
                throw new ArgumentNullException(nameof(formParams));

            var request = new HttpRequest(url);
            try
            {
                request.UseForm(formParams);
                var response = await @this.SendAsync(request);
                var encoding = FeaturesExtensions.GetEncoding(response) ?? Encoding.UTF8;
                return await response.Content.ReadJsonAsync<T>(encoding);
            }
            finally
            {
                request.Dispose();
            }
        }
        public static async Task<T> PostJsonAsync<T>(this HttpClient @this, string url, IFormParams formParams, IFormFileParams formFileParams)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (url == null)
                throw new ArgumentNullException(nameof(url));

            var request = new HttpRequest(url) { Method = HttpMethod.Post };
            try
            {
                request.UseFormData(formParams, formFileParams);
                var response = await @this.SendAsync(request);
                var encoding = FeaturesExtensions.GetEncoding(response) ?? Encoding.UTF8;
                return await response.Content.ReadJsonAsync<T>(encoding);
            }
            finally
            {
                request.Dispose();
            }
        }
        public static async Task<T> PostJsonAsync<T>(this HttpClient @this, string url, IQueryParams queryParams, IFormParams formParams)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (url == null)
                throw new ArgumentNullException(nameof(url));
            if (formParams == null)
                throw new ArgumentNullException(nameof(formParams));

            var request = new HttpRequest(url) { Method = HttpMethod.Post };
            try
            {
                queryParams.Join(request.Url);
                request.UseForm(formParams);
                var response = await @this.SendAsync(request);
                var encoding = FeaturesExtensions.GetEncoding(response) ?? Encoding.UTF8;
                return await response.Content.ReadJsonAsync<T>(encoding);
            }
            finally
            {
                request.Dispose();
            }
        }
        public static async Task<T> PostJsonAsync<T>(this HttpClient @this, string url, IQueryParams queryParams, IFormParams formParams, IFormFileParams formFileParams)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (url == null)
                throw new ArgumentNullException(nameof(url));

            var request = new HttpRequest(url) { Method = HttpMethod.Post };
            try
            {
                queryParams.Join(request.Url);
                request.UseFormData(formParams, formFileParams);
                var response = await @this.SendAsync(request);
                var encoding = FeaturesExtensions.GetEncoding(response) ?? Encoding.UTF8;
                return await response.Content.ReadJsonAsync<T>(encoding);
            }
            finally
            {
                request.Dispose();
            }
        }
        public static async Task<T> PostJsonAsync<TRequest, T>(this HttpClient @this, string url, TRequest value)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (url == null)
                throw new ArgumentNullException(nameof(url));

            var request = new HttpRequest(url) { Method = HttpMethod.Post };
            try
            {
                request.UseJson(value);
                var response = await @this.SendAsync(request);
                var encoding = FeaturesExtensions.GetEncoding(response) ?? Encoding.UTF8;
                return await response.Content.ReadJsonAsync<T>(encoding);
            }
            finally
            {
                request.Dispose();
            }
        }
        public static async Task<T> PostJsonAsync<TRequest, T>(this HttpClient @this, string url, IQueryParams queryParams, TRequest value)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (url == null)
                throw new ArgumentNullException(nameof(url));

            var request = new HttpRequest(url) { Method = HttpMethod.Post };
            try
            {
                queryParams.Join(request.Url);
                request.UseJson(value);
                var response = await @this.SendAsync(request);
                var encoding = FeaturesExtensions.GetEncoding(response) ?? Encoding.UTF8;
                return await response.Content.ReadJsonAsync<T>(encoding);
            }
            finally
            {
                request.Dispose();
            }
        }
        public static async Task<string> PostStringAsync(this HttpClient @this, string url, IFormParams formParams)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (url == null)
                throw new ArgumentNullException(nameof(url));
            if (formParams == null)
                throw new ArgumentNullException(nameof(formParams));

            var request = new HttpRequest(url) { Method = HttpMethod.Post };
            try
            {
                request.UseForm(formParams);
                var response = await @this.SendAsync(request);
                var encoding = FeaturesExtensions.GetEncoding(response) ?? Encoding.UTF8;
                return await response.Content.ReadStringAsync(encoding);
            }
            finally
            {
                request.Dispose();
            }
        }
        public static async Task<string> PostStringAsync(this HttpClient @this, string url, IFormParams formParams, IFormFileParams formFileParams)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (url == null)
                throw new ArgumentNullException(nameof(url));

            var request = new HttpRequest(url) { Method = HttpMethod.Post };
            try
            {
                request.UseFormData(formParams, formFileParams);
                var response = await @this.SendAsync(request);
                var encoding = FeaturesExtensions.GetEncoding(response) ?? Encoding.UTF8;
                return await response.Content.ReadStringAsync(encoding);
            }
            finally
            {
                request.Dispose();
            }
        }
        public static async Task<string> PostStringAsync(this HttpClient @this, string url, IQueryParams queryParams, IFormParams formParams)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (url == null)
                throw new ArgumentNullException(nameof(url));
            if (formParams == null)
                throw new ArgumentNullException(nameof(formParams));

            var request = new HttpRequest(url) { Method = HttpMethod.Post };
            try
            {
                queryParams.Join(request.Url);
                request.UseForm(formParams);
                var response = await @this.SendAsync(request);
                var encoding = FeaturesExtensions.GetEncoding(response) ?? Encoding.UTF8;
                return await response.Content.ReadStringAsync(encoding);
            }
            finally
            {
                request.Dispose();
            }
        }
        public static async Task<string> PostStringAsync(this HttpClient @this, string url, IQueryParams queryParams, IFormParams formParams, IFormFileParams formFileParams)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (url == null)
                throw new ArgumentNullException(nameof(url));

            var request = new HttpRequest(url) { Method = HttpMethod.Post };
            try
            {
                queryParams.Join(request.Url);
                request.UseFormData(formParams, formFileParams);
                var response = await @this.SendAsync(request);
                var encoding = FeaturesExtensions.GetEncoding(response) ?? Encoding.UTF8;
                return await response.Content.ReadStringAsync(encoding);
            }
            finally
            {
                request.Dispose();
            }
        }
        public static HttpRequest UseJson<T>(this HttpRequest @this, T value)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            var buffer = StringContent.Rent(out var disposable);
            @this.RegisterForDispose(disposable);
            JsonWriter.ToJson(value, buffer);
            @this.Content = StringContent.Create(buffer.Sequence, Encoding.UTF8);
            @this.Headers.Add(HttpHeaders.ContentType, "application/json; charset=utf-8");
            return @this;
        }
        public static HttpRequest UseJson<T>(this HttpRequest @this, T value, Encoding encoding)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            if (encoding == Encoding.UTF8)
                return UseJson(@this, value);

            var buffer = StringContent.Rent(out var disposable);
            @this.RegisterForDispose(disposable);
            JsonWriter.ToJson(value, buffer);
            @this.Content = StringContent.Create(buffer.Sequence, encoding);
            @this.Headers.Add(HttpHeaders.ContentType, "application/json; charset=" + encoding.WebName);
            return @this;
        }
        //public static HttpRequest UseJsonIndent<T>(this HttpRequest @this, T value)
        //{
        //    if (@this == null)
        //        throw new ArgumentNullException(nameof(@this));
        //    if (value == null)
        //        throw new ArgumentNullException(nameof(value));

        //    @this.Headers.Add(HttpHeaders.ContentType, "application/json; charset=utf-8");
        //    var buffer = StringContent.Rent(out var disposable);
        //    try
        //    {
        //        JsonWriter.ToJsonIndent(value, buffer);
        //        @this.Content = StringContent.Create(buffer.Sequence, Encoding.UTF8);
        //    }
        //    catch
        //    {
        //        disposable.Dispose();
        //        throw;
        //    }
        //    @this.RegisterForDispose(disposable);
        //    return @this;
        //}
        //public static HttpRequest UseJsonIndent<T>(this HttpRequest @this, T value, Encoding encoding)
        //{
        //    if (@this == null)
        //        throw new ArgumentNullException(nameof(@this));
        //    if (value == null)
        //        throw new ArgumentNullException(nameof(value));
        //    if (encoding == null)
        //        throw new ArgumentNullException(nameof(encoding));

        //    if (encoding == Encoding.UTF8)
        //        return UseJsonIndent(@this, value);

        //    @this.Headers.Add(HttpHeaders.ContentType, "application/json; charset=" + encoding.WebName);
        //    var buffer = StringContent.Rent(out var disposable);
        //    try
        //    {
        //        JsonWriter.ToJsonIndent(value, buffer);
        //        @this.Content = StringContent.Create(buffer.Sequence, encoding);
        //    }
        //    catch
        //    {
        //        disposable.Dispose();
        //        throw;
        //    }
        //    @this.RegisterForDispose(disposable);
        //    return @this;
        //}
        public static HttpRequest UseForm(this HttpRequest @this, IFormParams formParams)
        {
            return UseForm(@this, formParams, Encoding.UTF8);
        }
        public static HttpRequest UseForm(this HttpRequest @this, IFormParams formParams, Encoding encoding)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (formParams == null)
                throw new ArgumentNullException(nameof(formParams));
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            var count = formParams.Count;
            if (count == 0)
                return @this;
            var sb = StringContent.Rent(out var disposable);
            @this.RegisterForDispose(disposable);
            Url.Encode(formParams[0].Key, encoding, sb);
            sb.Write('=');
            Url.Encode(formParams[0].Value, encoding, sb);
            for (int i = 1; i < count; i++)
            {
                sb.Write('&');
                Url.Encode(formParams[i].Key, encoding, sb);
                sb.Write('=');
                Url.Encode(formParams[i].Value, encoding, sb);
            }
            @this.Content = StringContent.Create(sb.Sequence);
            var contentType = encoding == Encoding.UTF8
                ? "application/x-www-form-urlencoded; charset=utf-8"
                : $"application/x-www-form-urlencoded; charset={encoding.WebName}";

            @this.Headers.Add(HttpHeaders.ContentType, contentType);
            return @this;
        }
        public static HttpRequest UseFormData(this HttpRequest @this, IFormParams formParams, IFormFileParams formFileParams)
        {
            return UseFormData(@this, formParams, formFileParams, null, Encoding.UTF8);
        }
        public static HttpRequest UseFormData(this HttpRequest @this, IFormParams formParams, IFormFileParams formFileParams, string boundary)
        {
            return UseFormData(@this, formParams, formFileParams, boundary, Encoding.UTF8);
        }
        public static HttpRequest UseFormData(this HttpRequest @this, IFormParams formParams, IFormFileParams formFileParams, string boundary, Encoding encoding)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            if (string.IsNullOrEmpty(boundary))
            {
                Span<char> boundarySpan = stackalloc char[32];
                Guid.NewGuid().TryFormat(boundarySpan, out var charsWritten, "N");
                Span<byte> boundaryBytes = stackalloc byte[38];
                boundaryBytes[0] = (byte)'-';
                boundaryBytes[1] = (byte)'-';
                boundaryBytes[2] = (byte)'-';
                boundaryBytes[3] = (byte)'-';
                boundaryBytes[4] = (byte)'-';
                boundaryBytes[5] = (byte)'-';
                Encoding.ASCII.GetBytes(boundarySpan, boundaryBytes.Slice(6));
                var content = new FormDataContent(formParams, formFileParams, boundaryBytes, encoding);
                @this.Content = content;
                @this.RegisterForDispose(content);
                var contentType = StringExtensions.Concat("multipart/form-data; boundary=----", boundarySpan,
                    encoding == Encoding.UTF8 ? "; charset=utf-8" : "; charset=" + encoding.WebName);
                @this.Headers.Add(HttpHeaders.ContentType, contentType);
            }
            else
            {
                if (boundary.Length > 250)
                    throw new ArgumentOutOfRangeException(nameof(boundary));

                Span<byte> boundaryBytes = stackalloc byte[boundary.Length + 2];
                boundaryBytes[0] = (byte)'-';
                boundaryBytes[1] = (byte)'-';
                Encoding.ASCII.GetBytes(boundary, boundaryBytes.Slice(2));
                var content = new FormDataContent(formParams, formFileParams, boundaryBytes, encoding);
                @this.Content = content;
                @this.RegisterForDispose(content);
                var contentType = StringExtensions.Concat("multipart/form-data; boundary=", boundary,
                    encoding == Encoding.UTF8 ? "; charset=utf-8" : "; charset=" + encoding.WebName);
                @this.Headers.Add(HttpHeaders.ContentType, contentType);
            }
            return @this;
        }
        //TODO??? Use(options=>options.UseCookie().UseRedirect().UseTimeout())
        public static HttpClient Use(this HttpClient @this, Func<HttpRequest, HttpClient, Task<HttpResponse>> handler)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            return new DelegatingClient(@this, handler);
        }
        //public static HttpClient UseBaseUrl(this HttpClient @this, string url)
        //{
        //    if (@this == null)
        //        throw new ArgumentNullException(nameof(@this));
        //    if (url == null)
        //        throw new ArgumentNullException(nameof(url));

        //    var baseUrl = new Url(url);

        //    //baseUrl.AbsoluteUri = "";//自动拼接上去
        //    //允许相对url 
        //    //if (string.IsNullOrEmpty(baseUrl.Scheme))
        //    //    throw new ArgumentException("must is AbsoluteUri");

        //    if (baseUrl.Scheme == Url.SchemeHttp)
        //        baseUrl.Scheme = Url.SchemeHttp;
        //    else if (baseUrl.Scheme == Url.SchemeHttps)
        //        baseUrl.Scheme = Url.SchemeHttps;

        //    return new BaseUrlClient(@this, baseUrl);
        //}
        public static HttpClient UseCookie(this HttpClient @this)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            return new CookieClient(@this, null);
        }
        public static HttpClient UseCookie(this HttpClient @this, IList<string> setCookies)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (setCookies == null)
                throw new ArgumentNullException(nameof(setCookies));

            return new CookieClient(@this, setCookies);
        }
        public static HttpClient UseRedirect(this HttpClient @this)
        {
            return UseRedirect(@this, 4);
        }
        public static HttpClient UseRedirect(this HttpClient @this, int maxRedirections)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            if (maxRedirections <= 0)
                return @this;

            return new RedirectClient(@this, maxRedirections);
        }
        public static HttpClient UseCompression(this HttpClient @this) 
        {
            return @this.UseCompression("gzip", "deflate", "br");
        }
        public static HttpClient UseCompression(this HttpClient @this, params string[] acceptEncodings)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            if (acceptEncodings == null || acceptEncodings.Length == 0)
                return @this;

            var acceptEncoding = string.Join(", ", acceptEncodings);
            return @this.Use(async (request, client) =>
            {
                if (request.Headers.Contains(HttpHeaders.AcceptEncoding))
                {
                    var response = await client.SendAsync(request);
                    return response;
                }
                else
                {
                    request.Headers.Add(HttpHeaders.AcceptEncoding, acceptEncoding);
                    var response = await client.SendAsync(request);
                    if (response != null&& response.Content != null)
                    {
                        if (response.Headers.TryGetValue(HttpHeaders.ContentEncoding, out var contentEncoding))
                        {
                            //gzip&& deflate && br &&
                            if ("gzip".EqualsIgnoreCase(contentEncoding))
                            {
                                var content = new DeflateDecoderContent(response.Content, new DeflateDecoder(31));
                                response.RegisterForDispose(content);
                                response.Content = content;
                            }
                            else if ("deflate".EqualsIgnoreCase(contentEncoding))
                            {
                                var content = new DeflateDecoderContent(response.Content, new DeflateDecoder(15));
                                response.RegisterForDispose(content);
                                response.Content = content;
                            }
                            else if ("br".EqualsIgnoreCase(contentEncoding))
                            {
                                var content = new BrotliDecoderContent(response.Content, new BrotliDecoder());
                                response.RegisterForDispose(content);
                                response.Content = content;
                            }
                        }
                    }
                    return response;
                }
            });
        }
        public static HttpClient UseTimeout(this HttpClient @this)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            return new TimeoutQueueClient(@this, new TaskTimeoutQueue<HttpResponse>(20000), new TaskTimeoutQueue<int>(5000));
        }
        public static HttpClient UseTimeout(this HttpClient @this, int timeout, int readTimeout)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            return new TimeoutQueueClient(@this,
                timeout == 0 ? null : new TaskTimeoutQueue<HttpResponse>(timeout),
                readTimeout == 0 ? null : new TaskTimeoutQueue<int>(readTimeout));
        }
        public static HttpClient UseTimeout(this HttpClient @this, TaskTimeoutQueue<HttpResponse> timeout, TaskTimeoutQueue<int> readTimeout)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            return new TimeoutQueueClient(@this, timeout, readTimeout);
        }

        //UseAutoRedirect

        //先不支持HttpClient 释放
        //public static Property<HttpClient> DisposablesProperty = new Property<HttpClient>("#Sys.Disposables");
        //public static IList<IDisposable> Disposables(this HttpClient @this)
        //{
        //    //var disposables = (IList<IDisposable>)@this.Properties[DisposablesProperty];
        //    //if (disposables == null)
        //    //{
        //    //    disposables = new List<IDisposable>();
        //    //    @this.Properties[DisposablesProperty] = disposables;
        //    //}
        //    //return disposables;

        //    return null;
        //}
        //public static void Disposables(this HttpClient @this, IList<IDisposable> disposables)
        //{
        //    //if (disposables == null)
        //    //    throw new ArgumentNullException(nameof(disposables));

        //    //var oldDisposables = (IList<IDisposable>)@this.Properties[DisposablesProperty];
        //    //if (oldDisposables != null)
        //    //{
        //    //    for (int i = 0; i < oldDisposables.Count; i++)
        //    //    {
        //    //        disposables.Add(oldDisposables[i]);
        //    //    }
        //    //}
        //    //@this.Properties[DisposablesProperty] = disposables;
        //}

        //TODO Request取消
        //public static TaskCompletionSource<HttpResponse> TaskCompletionSource(HttpRequest request)
        //{

        //    return null;
        //}
        //public static void TaskCompletionSource(HttpRequest request, TaskCompletionSource<HttpResponse> tcs)
        //{

        //}

        //不要了
        //public static HttpClient UseHeaders(this HttpClient @this, HttpHeaders headers)
        //{
        //    return @this;
        //}
        //public static HttpClient UseHeaders(this HttpClient @this, Action<HttpHeaders> headers)
        //{
        //    return @this;
        //}
        #region private
        private class DelegatingClient : HttpClient
        {
            private HttpClient _client;
            private Func<HttpRequest, HttpClient, Task<HttpResponse>> _handler;
            public DelegatingClient(HttpClient client, Func<HttpRequest, HttpClient, Task<HttpResponse>> handler)
            {
                _client = client;
                _handler = handler;
            }
            public override Task<HttpResponse> SendAsync(HttpRequest request)
            {
                return _handler.Invoke(request, _client);
            }
        }
        private class CookieClient : HttpClient
        {
            public class Cookie
            {
                //public string Guid { get; set; }//store
                public string Name { get; set; }
                public string Value { get; set; }
                public string Path { get; set; }//default=/
                public string Domain { get; set; }//.domain.com
                public DateTimeOffset? Expires { get; set; }
                public bool HostOnly { get; set; } //secure httponly 
                public static bool TryParse(string header, out Cookie cookie)
                {
                    var span = header.AsSpan();
                    var length = span.Length;
                    if (length == 0)
                    {
                        cookie = null;
                        return false;
                    }

                    cookie = new Cookie();
                    var tempOffset = 0;
                    ReadOnlySpan<char> paramName = null;
                    ReadOnlySpan<char> paramValue = null;
                    for (var index = 0; ;)
                    {
                        if (index == span.Length)
                        {
                            if (paramName == null)
                            {
                                paramName = span.Slice(tempOffset, index - tempOffset).TrimStart();
                                paramValue = ReadOnlySpan<char>.Empty;
                            }
                            else
                            {
                                paramValue = span.Slice(tempOffset, index - tempOffset);
                            }
                            tempOffset = -1;
                            goto param;
                        }
                        switch (span[index++])
                        {
                            case ';':
                                if (paramName == null)
                                {
                                    paramName = span.Slice(tempOffset, index - tempOffset - 1).TrimStart();
                                    paramValue = ReadOnlySpan<char>.Empty;
                                }
                                else
                                {
                                    paramValue = span.Slice(tempOffset, index - tempOffset - 1);
                                }
                                tempOffset = index;
                                goto param;
                            case '=':
                                if (paramName != null)
                                    continue;
                                paramName = span.Slice(tempOffset, index - tempOffset - 1).TrimStart();
                                if (index == length)
                                {
                                    paramValue = ReadOnlySpan<char>.Empty;
                                    tempOffset = -1;
                                    goto param;
                                }
                                if (span[index] == '"')
                                {
                                    var tempSpan = span.Slice(index + 1);
                                    var tempIndex = tempSpan.IndexOf('"');
                                    if (tempIndex == -1)
                                        throw new FormatException();
                                    paramValue = tempSpan.Slice(0, tempIndex);
                                    index += tempIndex + 2;
                                    if (index == length)
                                    {
                                        tempOffset = -1;
                                    }
                                    else if (span[index] == ';')
                                    {
                                        index += 1;
                                        tempOffset = index;
                                    }
                                    else if (span[index] == ',')
                                    {
                                        tempOffset = -1;
                                    }
                                    else
                                    {
                                        cookie = null;
                                        return false;
                                    }
                                    goto param;
                                }
                                tempOffset = index;
                                continue;
                            case ',':
                                if (paramName == null)
                                {
                                    paramName = span.Slice(tempOffset, index - tempOffset - 1);
                                    paramValue = ReadOnlySpan<char>.Empty;
                                }
                                else
                                {
                                    paramValue = span.Slice(tempOffset, index - tempOffset - 1);
                                }
                                tempOffset = -1;
                                goto param;
                            default:
                                continue;
                        }
                    param:
                        if (cookie.Name == null)
                        {
                            cookie.Name = new string(paramName);
                            cookie.Value = new string(paramValue);
                        }
                        else
                        {
                            if (paramName.EqualsIgnoreCase("domain"))
                            {
                                if (!paramValue.IsEmpty)
                                    cookie.Domain = paramValue[0] == '.' ? new string(paramValue.Slice(1)) : new string(paramValue);
                            }
                            else if (paramName.EqualsIgnoreCase("path"))
                            {
                                if (!paramValue.IsEmpty && paramValue[0] == '/')
                                    cookie.Path = new string(paramValue);
                            }
                            else if (paramName.EqualsIgnoreCase("max-age"))
                            {
                                if (long.TryParse(paramValue, out var maxAge))
                                {
                                    var expires = DateTime.Now.AddSeconds(maxAge);
                                    if (cookie.Expires == null || cookie.Expires > expires)
                                        cookie.Expires = expires;
                                }
                            }
                            else if (paramName.EqualsIgnoreCase("expires"))
                            {
                                if (DateTime.TryParse(paramValue, out var expires))
                                {
                                    if (cookie.Expires == null || cookie.Expires > expires)
                                        cookie.Expires = expires;
                                }
                            }
                        }
                        if (tempOffset == -1)
                        {
                            return true;
                        }
                        paramName = null;
                        paramValue = null;
                    }
                }
            }
            public CookieClient(HttpClient client, IList<string> setCookies)
            {
                //TODO?
                //使用Timer缓存CookieHeader
                //树
                //.b.com  value
                //a.b.com  value //锁住结点
                //c.b.com
                //x.a.b.com
                //x.c.b.com
                //不设置cookie 最大项了

                _client = client;
                _container = new ConcurrentDictionary<string, List<Cookie>>(StringComparer.OrdinalIgnoreCase);//?CopyOnWrite
                if (setCookies != null) 
                {
                    foreach (var setCookie in setCookies)
                    {
                        if (Cookie.TryParse(setCookie, out var cookie))
                        {
                            if (string.IsNullOrEmpty(cookie.Domain))
                                throw new ArgumentException("Domain");

                            if (string.IsNullOrEmpty(cookie.Path))
                                cookie.Path = "/";

                            if (_container.TryGetValue(cookie.Domain, out var cookies))
                            {
                                cookies.Add(cookie);
                            }
                            else 
                            {
                                _container.TryAdd(cookie.Domain, new List<Cookie>() { cookie });
                            }
                        }
                        else 
                        {
                            throw new ArgumentException("SetCookie");
                        }
                    }
                }
            }

            private HttpClient _client;
            private ConcurrentDictionary<string, List<Cookie>> _container;
            public override async Task<HttpResponse> SendAsync(HttpRequest request)
            {
                var host = request.Url.Host;
                var path = request.Url.Path ?? "/";
                if (string.IsNullOrEmpty(host))
                    throw new ArgumentException("url must have host");
                var domain = request.Url.Domain;
                if (string.IsNullOrEmpty(domain))//hostOnly TODO? Remove(HttpHeaders.Cookie) risk
                {
                    if (_container.TryGetValue(host, out var cookies))
                    {
                        lock (cookies)
                        {
                            var sb = StringExtensions.ThreadRent(out var disposable);
                            try
                            {
                                var separator = false;
                                for (int i = cookies.Count - 1; i >= 0; i--)
                                {
                                    var cookie = cookies[i];
                                    if (cookie.Expires != null && cookie.Expires <= DateTimeOffset.Now)
                                    {
                                        cookies.RemoveAt(i);
                                        continue;
                                    }
                                    //Debug.Assert(cookie.HostOnly);
                                    //Debug.Assert(cookie.Domain.EqualsIgnoreCase(host));
                                    if (cookie.Path != "/")
                                    {
                                        if (!path.StartsWith(cookie.Path))
                                            continue;
                                        if (cookie.Path[cookie.Path.Length - 1] != '/')
                                        {
                                            if (path.Length > cookie.Path.Length && path[cookie.Path.Length] != '/')
                                                continue;
                                        }
                                    }
                                    if (separator)
                                        sb.Write("; ");
                                    else
                                        separator = true;
                                    sb.Write(cookie.Name);
                                    sb.Write('=');
                                    sb.Write(cookie.Value);
                                }

                                if (sb.Length > 0)
                                {
                                    request.Headers[HttpHeaders.Cookie] = sb.ToString();
                                }
                                else
                                {
                                    request.Headers.Remove(HttpHeaders.Cookie);
                                }
                            }
                            finally
                            {
                                disposable.Dispose();
                            }
                        }
                    }
                    else 
                    {
                        request.Headers.Remove(HttpHeaders.Cookie);
                    }
                }
                else
                {
                    if (_container.TryGetValue(domain, out var cookies))
                    {
                        lock (cookies)
                        {
                            var sb = StringExtensions.ThreadRent(out var disposable);
                            try
                            {
                                var separator = false;
                                for (int i = cookies.Count - 1; i >= 0; i--)
                                {
                                    var cookie = cookies[i];
                                    if (cookie.Expires != null && cookie.Expires <= DateTimeOffset.Now)
                                    {
                                        cookies.RemoveAt(i);
                                        continue;
                                    }
                                    if (cookie.HostOnly)
                                    {
                                        if (!cookie.Domain.EqualsIgnoreCase(host))
                                            continue;
                                    }
                                    else
                                    {
                                        if (!host.EndsWith(cookie.Domain, StringComparison.OrdinalIgnoreCase))
                                            continue;
                                        if (host.Length > cookie.Domain.Length && host[host.Length - cookie.Domain.Length - 1] != '.')
                                            continue;
                                    }
                                    if (cookie.Path != "/")//不是根路径
                                    {
                                        if (!path.StartsWith(cookie.Path))//不忽略大小写
                                            continue;
                                        if (cookie.Path[cookie.Path.Length - 1] != '/')
                                        {
                                            if (path.Length > cookie.Path.Length && path[cookie.Path.Length] != '/')
                                                continue;
                                        }
                                    }
                                    if (separator)
                                        sb.Write("; ");
                                    else
                                        separator = true;
                                    sb.Write(cookie.Name);
                                    sb.Write('=');
                                    sb.Write(cookie.Value);
                                }

                                if (sb.Length > 0)
                                {
                                    request.Headers[HttpHeaders.Cookie] = sb.ToString();
                                }
                                else
                                {
                                    request.Headers.Remove(HttpHeaders.Cookie);
                                }
                            }
                            finally
                            {
                                disposable.Dispose();
                            }
                        }
                    }
                    else 
                    {
                        request.Headers.Remove(HttpHeaders.Cookie);
                    }
                }
                var response = await _client.SendAsync(request);
                if (response != null)
                {
                    //if (host != request.Url.Host && path != request.Url.Path)
                    //    throw new InvalidOperationException("host path must can't change if UseCookie");

                    var setCookies = response.Headers.GetValues(HttpHeaders.SetCookie);
                    if (setCookies != null)
                    {
                        var cookiesKey = string.IsNullOrEmpty(domain) ? host : domain;
                        foreach (var setCookie in setCookies)
                        {
                            if (Cookie.TryParse(setCookie, out var cookie))
                            {
                                if (string.IsNullOrEmpty(cookie.Domain))
                                {
                                    cookie.HostOnly = true;
                                    cookie.Domain = host;
                                }
                                else if (string.IsNullOrEmpty(domain))
                                {
                                    if (!cookie.Domain.EqualsIgnoreCase(host))
                                        continue;//忽略无效
                                    cookie.HostOnly = true;
                                }
                                else
                                {
                                    if (!cookie.Domain.EndsWith(domain, StringComparison.OrdinalIgnoreCase))
                                        continue;
                                    if (cookie.Domain.Length > domain.Length && cookie.Domain[cookie.Domain.Length - domain.Length - 1] != '.')
                                        continue;
                                }
                                if (string.IsNullOrEmpty(cookie.Path))
                                    cookie.Path = path;

                                if (!_container.TryGetValue(cookiesKey, out var cookies))
                                {
                                    lock (_container)
                                    {
                                        if (!_container.TryGetValue(cookiesKey, out cookies))
                                        {
                                            cookies = new List<Cookie>();
                                            _container.TryAdd(cookiesKey, cookies);
                                        }
                                    }
                                }
                                lock (cookies)
                                {
                                    for (int j = cookies.Count - 1; j >= 0; j--)
                                    {
                                        var item = cookies[j];
                                        if (item.Expires != null && item.Expires <= DateTimeOffset.Now)
                                        {
                                            cookies.RemoveAt(j);
                                            continue;
                                        }
                                        if (cookie.Name == item.Name && cookie.Path == item.Path && cookie.Domain.EqualsIgnoreCase(item.Domain))
                                        {
                                            cookies.RemoveAt(j);
                                            if (cookie.Expires != null && cookie.Expires <= DateTimeOffset.Now)
                                                cookie = null;
                                            continue;
                                        }
                                    }

                                    if (cookie != null)
                                        cookies.Add(cookie);
                                }
                            }
                        }
                    }
                }
                return response;
            }
        }
        private class RedirectClient : HttpClient
        {
            private HttpClient _client;
            private int _maxRedirections;
            public RedirectClient(HttpClient client, int maxRedirections)
            {
                _client = client;
                _maxRedirections = maxRedirections;
            }
            public override async Task<HttpResponse> SendAsync(HttpRequest request)
            {
                var response = await _client.SendAsync(request);
                if (response == null)
                    return null;

                switch (response.StatusCode)
                {
                    case 301:
                    case 302:
                    case 307:
                    case 300:
                    case 303:
                    case 308:
                        break;
                    default:
                        return response;
                }
                for (int i = 0; i < _maxRedirections; i++)
                {
                    if (!response.Headers.TryGetValue(HttpHeaders.Location, out var location)
                        || string.IsNullOrEmpty(location))
                        return response;

                    if (location[0] == '/')
                        request.Url.AbsolutePath = location;
                    else
                        request.Url.AbsoluteUri = location;

                    if (request.Method == HttpMethod.Post &&
                    (response.StatusCode == 301
                    || response.StatusCode == 302
                    || response.StatusCode == 303
                    || response.StatusCode == 300))
                    {
                        request.Method = HttpMethod.Get;
                        request.Content = null;
                        request.Headers.Remove(HttpHeaders.ContentLength);
                        request.Headers.Remove(HttpHeaders.TransferEncoding);
                    }
                    else if (request.Content != null)
                    {
                        if (!request.Content.Rewind())
                            throw new InvalidOperationException(nameof(IHttpContent.Rewind));
                    }
                    response.Dispose();
                    response = await _client.SendAsync(request);
                    if (response == null)
                        return null;
                    switch (response.StatusCode)
                    {
                        case 301:
                        case 302:
                        case 307:
                        case 300:
                        case 303:
                        case 308:
                            break;
                        default:
                            return response;
                    }
                }
                return response;
            }
        }
        //private class BaseUrlClient : HttpClient
        //{
        //    private HttpClient _client;
        //    private Url _url;//动态编译技术
        //    public BaseUrlClient(HttpClient client, Url url)
        //    {
        //        _client = client;
        //        _url = url;
        //    }
        //    public override Task<HttpResponse> SendAsync(HttpRequest request)
        //    {
        //        var url = request.Url;
        //        if (string.IsNullOrEmpty(url.Scheme))
        //        {
        //            url.Scheme = _url.Scheme;
        //            url.UserInfo = _url.UserInfo;
        //            url.Host = _url.Host;
        //            url.Port = _url.Port;
        //        }

        //        if (!string.IsNullOrEmpty(_url.Path))
        //        {
        //            //if(_url.Path[_url.Path.Length-1]=='/')
        //            url.Path = _url.Path + url.Path;
        //        }
        //        if (!string.IsNullOrEmpty(_url.Query))
        //        {
        //            if (string.IsNullOrEmpty(url.Query))
        //            {
        //                url.Query = _url.Query;
        //            }
        //            else
        //            {
        //                if (_url.Query.EndsWith('&'))
        //                {
        //                    url.Query = StringExtensions.Concat(_url.Query, url.Query.AsSpan(1));
        //                }
        //                else
        //                {
        //                    url.Query = StringExtensions.Concat(_url.Query, "&", url.Query.AsSpan(1));
        //                }
        //            }
        //        }
        //        return _client.SendAsync(request);
        //    }
        //}
        private class FormDataContent : IHttpContent, IDisposable
        {
            #region private
            private static byte[] _Name = Encoding.ASCII.GetBytes("\r\nContent-Disposition: form-data; name=\"");
            private static byte[] _FileName = Encoding.ASCII.GetBytes("\"; filename=\"");
            private static byte[] _ContentType = Encoding.ASCII.GetBytes("\"\r\nContent-Type: ");
            #endregion
            //--{boundary}
            public FormDataContent(IFormParams formParams, IFormFileParams formFileParams, ReadOnlySpan<byte> boundary, Encoding encoding)
            {
                var buffer = MemoryContent.Rent(out _disposable);
                //var sb = StringExtensions.ThreadRent(out var disposable);
                try
                {
                    if (formParams != null)
                    {
                        for (int i = 0; i < formParams.Count; i++)
                        {
                            buffer.Write(boundary);
                            buffer.Write(_Name);
                            buffer.WriteChars(formParams[i].Key, encoding);
                            buffer.Write((byte)'\"');
                            buffer.Write((byte)'\r');
                            buffer.Write((byte)'\n');
                            buffer.Write((byte)'\r');
                            buffer.Write((byte)'\n');
                            buffer.WriteChars(formParams[i].Value, encoding);
                            buffer.Write((byte)'\r');
                            buffer.Write((byte)'\n');
                        }
                        _form = buffer.Sequence;
                    }
                    var seqLength = _form.Length;
                    _length = seqLength;
                    if (formFileParams != null && formFileParams.Count > 0)
                    {
                        _files = new List<(ReadOnlySequence<byte> header, Stream content)>(formFileParams.Count);
                        {
                            var file = formFileParams[0].Value;
                            buffer.Write(boundary);
                            buffer.Write(_Name);
                            buffer.WriteChars(formFileParams[0].Key, encoding);
                            buffer.Write(_FileName);
                            buffer.WriteChars(file.FileName, encoding);
                            if (!string.IsNullOrEmpty(file.ContentType))
                            {
                                buffer.Write(_ContentType);
                                buffer.WriteByteString(file.ContentType);
                                buffer.Write((byte)'\r');
                                buffer.Write((byte)'\n');
                                buffer.Write((byte)'\r');
                                buffer.Write((byte)'\n');
                            }
                            else
                            {
                                buffer.Write((byte)'\"');
                                buffer.Write((byte)'\r');
                                buffer.Write((byte)'\n');
                                buffer.Write((byte)'\r');
                                buffer.Write((byte)'\n');
                            }
                            var seq = buffer.Sequence.Slice(seqLength);
                            seqLength += seq.Length;
                            _files.Add((seq, file.Length == 0 ? null : file.OpenRead()));
                            _length += seq.Length + file.Length;
                        }
                        for (int i = 1; i < formFileParams.Count; i++)
                        {
                            var file = formFileParams[i].Value;
                            buffer.Write((byte)'\r');
                            buffer.Write((byte)'\n');
                            buffer.Write(boundary);
                            buffer.Write(_Name);
                            buffer.WriteChars(formFileParams[i].Key, encoding);
                            buffer.Write(_FileName);
                            buffer.WriteChars(file.FileName, encoding);
                            if (!string.IsNullOrEmpty(file.ContentType))
                            {
                                buffer.Write(_ContentType);
                                buffer.WriteByteString(file.ContentType);
                                buffer.Write((byte)'\r');
                                buffer.Write((byte)'\n');
                                buffer.Write((byte)'\r');
                                buffer.Write((byte)'\n');
                            }
                            else
                            {
                                buffer.Write((byte)'\"');
                                buffer.Write((byte)'\r');
                                buffer.Write((byte)'\n');
                                buffer.Write((byte)'\r');
                                buffer.Write((byte)'\n');
                            }
                            var seq = buffer.Sequence.Slice(seqLength);
                            seqLength += seq.Length;
                            _files.Add((seq, file.Length == 0 ? null : file.OpenRead()));
                            _length += seq.Length + file.Length;
                        }
                        buffer.Write((byte)'\r');
                        buffer.Write((byte)'\n');
                        _length += 2;
                    }
                    else
                    {
                        _files = new List<(ReadOnlySequence<byte> Header, Stream Content)>(0);
                    }
                    buffer.Write(boundary);
                    buffer.Write((byte)'-');
                    buffer.Write((byte)'-');
                    buffer.Write((byte)'\r');
                    buffer.Write((byte)'\n');
                    _end = buffer.Sequence.Slice(seqLength);
                    _length += boundary.Length + 4;
                    _index = -1;
                    _position = 0;
                }
                catch
                {
                    _disposable.Dispose();
                    throw;
                }
            }
            private long _length;
            private int _index;//-1 start
            private int _position;//Sequence
            private ReadOnlySequence<byte> _form;
            private List<(ReadOnlySequence<byte> Header, Stream Content)> _files;
            private ReadOnlySequence<byte> _end;
            private IDisposable _disposable;
            public long Available => _index > _files.Count ? 0 : -1;
            public long Length => _length;
            public bool Rewind()
            {
                _index = -1;
                _position = 0;
                foreach (var file in _files)
                {
                    try
                    {
                        file.Content.Position = 0;
                    }
                    catch 
                    {
                        return false;
                    }
                }
                return true;
            }
            public long ComputeLength() => _length;
            public int Read(Span<byte> buffer)
            {
                if (_index > _files.Count)
                    return 0;

                var available = buffer.Length;
                if (available == 0)
                    return 0;
                do
                {
                    if (_index == -1)
                    {
                        var seq = _form.Slice(_position);
                        var toCopy = seq.Length >= available ? available : (int)seq.Length;
                        seq.Slice(0, toCopy).CopyTo(buffer.Slice(buffer.Length - available, toCopy));
                        _position += toCopy;
                        available -= toCopy;
                        if (_position == _form.Length)
                        {
                            _index += 1;
                            _position = 0;
                        }
                    }
                    else if (_index == _files.Count)
                    {
                        var seq = _end.Slice(_position);
                        var toCopy = seq.Length >= available ? available : (int)seq.Length;
                        seq.Slice(0, toCopy).CopyTo(buffer.Slice(buffer.Length - available, toCopy));
                        _position += toCopy;
                        available -= toCopy;
                        if (_position == _end.Length)
                        {
                            _index += 1;
                            _position = 0;
                        }
                        return buffer.Length - available;
                    }
                    else
                    {
                        var file = _files[_index];
                        if (_position < file.Header.Length)
                        {
                            var seq = file.Header.Slice(_position);
                            var toCopy = seq.Length >= available ? available : (int)seq.Length;
                            seq.Slice(0, toCopy).CopyTo(buffer.Slice(buffer.Length - available, toCopy));
                            _position += toCopy;
                            available -= toCopy;
                        }
                        if (available == 0)
                            return buffer.Length;
                        Debug.Assert(_position >= file.Header.Length);
                        if (file.Content == null)
                        {
                            _index += 1;
                            _position = 0;
                        }
                        else
                        {
                            var result = file.Content.Read(buffer.Slice(buffer.Length - available));
                            if (result == 0)
                            {
                                _index += 1;
                                _position = 0;
                            }
                            else
                            {
                                available -= result;
                            }
                        }
                    }

                } while (available > 0);

                return buffer.Length;
            }
            public int Read(byte[] buffer, int offset, int count)
            {
                return Read(buffer.AsSpan(offset, count));
            }
            public async ValueTask<int> ReadAsync(Memory<byte> buffer)
            {
                if (_index > _files.Count)
                    return 0;

                var available = buffer.Length;
                if (available == 0)
                    return 0;
                do
                {
                    if (_index == -1)
                    {
                        var seq = _form.Slice(_position);
                        var toCopy = seq.Length >= available ? available : (int)seq.Length;
                        seq.Slice(0, toCopy).CopyTo(buffer.Span.Slice(buffer.Length - available, toCopy));
                        _position += toCopy;
                        available -= toCopy;
                        if (_position == _form.Length)
                        {
                            _index += 1;
                            _position = 0;
                        }
                    }
                    else if (_index == _files.Count)
                    {
                        var seq = _end.Slice(_position);
                        var toCopy = seq.Length >= available ? available : (int)seq.Length;
                        seq.Slice(0, toCopy).CopyTo(buffer.Span.Slice(buffer.Length - available, toCopy));
                        _position += toCopy;
                        available -= toCopy;
                        if (_position == _end.Length)
                        {
                            _index += 1;
                            _position = 0;
                        }
                        return buffer.Length - available;
                    }
                    else
                    {
                        var file = _files[_index];
                        if (_position < file.Header.Length)
                        {
                            var seq = file.Header.Slice(_position);
                            var toCopy = seq.Length >= available ? available : (int)seq.Length;
                            seq.Slice(0, toCopy).CopyTo(buffer.Span.Slice(buffer.Length - available, toCopy));
                            _position += toCopy;
                            available -= toCopy;
                        }
                        if (available == 0)
                            return buffer.Length;
                        Debug.Assert(_position >= file.Header.Length);
                        if (file.Content == null)
                        {
                            _index += 1;
                            _position = 0;
                        }
                        else
                        {
                            var result = await file.Content.ReadAsync(buffer.Slice(buffer.Length - available));
                            if (result == 0)
                            {
                                _index += 1;
                                _position = 0;
                            }
                            else
                            {
                                available -= result;
                            }
                        }
                    }

                } while (available > 0);

                return buffer.Length;
            }
            public ValueTask<int> ReadAsync(byte[] buffer, int offset, int count)
            {
                return ReadAsync(buffer.AsMemory(offset, count));
            }
            public void Dispose()
            {
                if (_disposable == null)
                    return;
                _disposable.Dispose();
                foreach (var file in _files)
                {
                    try { file.Content.Dispose(); } catch { }
                }
                _disposable = null;
            }
        }
        private class DeflateDecoderContent : IHttpContent, IDisposable
        {
            private DeflateDecoder _decoder;
            private IHttpContent _content;
            private int _offset;
            private int _length;
            private byte[] _buffer;
            public DeflateDecoderContent(IHttpContent content, DeflateDecoder decoder)
            {
                Debug.Assert(content != null);
                Debug.Assert(decoder != null);
                _content = content;
                _decoder = decoder;
                _buffer = ArrayPool<byte>.Shared.Rent(8192);
            }
            public long Available => _content == null ? 0 : -1;
            public long Length => -1;
            public bool Rewind() => false;
            public long ComputeLength() => -1;
            public int Read(Span<byte> buffer)
            {
                if (buffer.IsEmpty)
                    return 0;
                if (_content == null)
                    return 0;
                if (_buffer == null)
                {
                    _decoder.Decompress(Array.Empty<byte>(), buffer, true, out var bytesConsumed, out var bytesWritten, out var completed);
                    Debug.Assert(bytesConsumed == 0);
                    Debug.Assert(bytesWritten != 0);
                    if (completed)
                    {
                        _content = null;
                        _decoder.Dispose();
                        _decoder = null;
                    }
                    return bytesWritten;
                }
                else
                {
                    if (_length == 0)
                    {
                        _length = _content.Read(_buffer);
                        if (_length == 0)
                        {
                            ArrayPool<byte>.Shared.Return(_buffer);
                            _buffer = null;
                            _decoder.Decompress(Array.Empty<byte>(), buffer, true, out var bytesConsumed, out var bytesWritten, out var completed);
                            Debug.Assert(bytesConsumed == 0);
                            Debug.Assert(bytesWritten != 0);
                            if (completed)
                            {
                                _content = null;
                                _decoder.Dispose();
                                _decoder = null;
                            }
                            return bytesWritten;
                        }
                        else
                        {
                            _offset = 0;
                            _decoder.Decompress(_buffer.AsSpan(_offset, _length), buffer, false, out var bytesConsumed, out var bytesWritten, out var completed);
                            _offset += bytesConsumed;
                            _length -= bytesConsumed;
                            if (completed)
                            {
                                //_content.Available != 0 is BUG
                                if (_length != 0)
                                    throw new InvalidDataException("Remaining");
                                _length = _content.Read(_buffer);
                                if (_length != 0)
                                    throw new InvalidDataException("Remaining");
                                ArrayPool<byte>.Shared.Return(_buffer);
                                _buffer = null;
                                _content = null;
                                _decoder.Dispose();
                                _decoder = null;
                                return bytesWritten;
                            }
                            if (bytesWritten == 0)
                                return Read(buffer);
                            return bytesWritten;
                        }
                    }
                    else
                    {
                        _decoder.Decompress(_buffer.AsSpan(_offset, _length), buffer, false, out var bytesConsumed, out var bytesWritten, out var completed);
                        _offset += bytesConsumed;
                        _length -= bytesConsumed;
                        if (completed)
                        {
                            if (_length != 0)
                                throw new InvalidDataException("Remaining");
                            _length = _content.Read(_buffer);
                            if (_length != 0)
                                throw new InvalidDataException("Remaining");
                            ArrayPool<byte>.Shared.Return(_buffer);
                            _buffer = null;
                            _content = null;
                            _decoder.Dispose();
                            _decoder = null;
                        }
                        if (bytesWritten == 0)
                            return Read(buffer);
                        return bytesWritten;
                    }
                }
            }
            public int Read(byte[] buffer, int offset, int count)
            {
                return Read(buffer.AsSpan(offset, count));
            }
            public async ValueTask<int> ReadAsync(Memory<byte> buffer)
            {
                if (buffer.IsEmpty)
                    return 0;
                if (_content == null)
                    return 0;
                if (_buffer == null)
                {
                    _decoder.Decompress(Array.Empty<byte>(), buffer.Span, true, out var bytesConsumed, out var bytesWritten, out var completed);
                    Debug.Assert(bytesConsumed == 0);
                    Debug.Assert(bytesWritten != 0);
                    if (completed)
                    {
                        _content = null;
                        _decoder.Dispose();
                        _decoder = null;
                    }
                    return bytesWritten;
                }
                else
                {
                    if (_length == 0)
                    {
                        _length = await _content.ReadAsync(_buffer);
                        if (_length == 0)
                        {
                            ArrayPool<byte>.Shared.Return(_buffer);
                            _buffer = null;
                            _decoder.Decompress(Array.Empty<byte>(), buffer.Span, true, out var bytesConsumed, out var bytesWritten, out var completed);
                            Debug.Assert(bytesConsumed == 0);
                            Debug.Assert(bytesWritten != 0);
                            if (completed)
                            {
                                _content = null;
                                _decoder.Dispose();
                                _decoder = null;
                            }
                            return bytesWritten;
                        }
                        else
                        {
                            _offset = 0;
                            _decoder.Decompress(_buffer.AsSpan(_offset, _length), buffer.Span, false, out var bytesConsumed, out var bytesWritten, out var completed);
                            _offset += bytesConsumed;
                            _length -= bytesConsumed;
                            if (completed)
                            {
                                if (_length != 0)
                                    throw new InvalidDataException("Remaining");
                                _length = await _content.ReadAsync(_buffer);
                                if (_length != 0)
                                    throw new InvalidDataException("Remaining");
                                ArrayPool<byte>.Shared.Return(_buffer);
                                _buffer = null;
                                _content = null;
                                _decoder.Dispose();
                                _decoder = null;
                                return bytesWritten;
                            }
                            if (bytesWritten == 0)
                                return await ReadAsync(buffer);
                            return bytesWritten;
                        }
                    }
                    else
                    {
                        _decoder.Decompress(_buffer.AsSpan(_offset, _length), buffer.Span, false, out var bytesConsumed, out var bytesWritten, out var completed);
                        _offset += bytesConsumed;
                        _length -= bytesConsumed;
                        if (completed)
                        {
                            if (_length != 0)
                                throw new InvalidDataException("Remaining");
                            _length = await _content.ReadAsync(_buffer);
                            if (_length != 0)
                                throw new InvalidDataException("Remaining");
                            ArrayPool<byte>.Shared.Return(_buffer);
                            _buffer = null;
                            _content = null;
                            _decoder.Dispose();
                            _decoder = null;
                        }
                        if (bytesWritten == 0)
                            return await ReadAsync(buffer);
                        return bytesWritten;
                    }
                }
            }
            public ValueTask<int> ReadAsync(byte[] buffer, int offset, int count)
            {
                return ReadAsync(buffer.AsMemory(offset, count));
            }
            public void Dispose()
            {
                var content = _content;
                _content = null;
                if (content != null)
                {
                    Debug.Assert(_decoder != null);
                    _decoder.Dispose();
                    _decoder = null;
                }
                var buffer = _buffer;
                _buffer = null;
                if (buffer != null)
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }
        private class BrotliDecoderContent : IHttpContent, IDisposable
        {
            private BrotliDecoder _decoder;
            private IHttpContent _content;
            private int _offset;
            private int _length;
            private byte[] _buffer;
            public BrotliDecoderContent(IHttpContent content, BrotliDecoder decoder)
            {
                Debug.Assert(content != null);
                _content = content;
                _decoder = decoder;
                _buffer = ArrayPool<byte>.Shared.Rent(8192);
            }
            public long Available => _content == null ? 0 : -1;
            public long Length => -1;
            public bool Rewind() => false;
            public long ComputeLength() => -1;
            public int Read(Span<byte> buffer)
            {
                if (buffer.IsEmpty)
                    return 0;
                if (_content == null)
                    return 0;
                if (_buffer == null)
                {
                    var status = _decoder.Decompress(Array.Empty<byte>(), buffer, out var bytesConsumed, out var bytesWritten);
                    if (status == OperationStatus.Done)
                    {
                        _content = null;
                        _decoder.Dispose();
                    }
                    else if (status != OperationStatus.DestinationTooSmall)//?
                    {
                        throw new InvalidDataException($"OperationStatus:{status}");
                    }
                    return bytesWritten;
                }
                else
                {
                    if (_length == 0)
                    {
                        _length = _content.Read(_buffer);
                        if (_length == 0)
                        {
                            ArrayPool<byte>.Shared.Return(_buffer);
                            _buffer = null;
                            var status = _decoder.Decompress(Array.Empty<byte>(), buffer, out var bytesConsumed, out var bytesWritten);
                            if (status == OperationStatus.Done)
                            {
                                _content = null;
                                _decoder.Dispose();
                            }
                            else if (status != OperationStatus.DestinationTooSmall)//?
                            {
                                throw new InvalidDataException($"OperationStatus:{status}");
                            }
                            return bytesWritten;
                        }
                        else
                        {
                            _offset = 0;
                            var status = _decoder.Decompress(_buffer.AsSpan(_offset, _length), buffer, out var bytesConsumed, out var bytesWritten);
                            _offset += bytesConsumed;
                            _length -= bytesConsumed;
                            if (status == OperationStatus.Done)
                            {
                                if (_length != 0)
                                    throw new InvalidDataException("Remaining");
                                _length = _content.Read(_buffer);
                                if (_length != 0)
                                    throw new InvalidDataException("Remaining");
                                ArrayPool<byte>.Shared.Return(_buffer);
                                _buffer = null;
                                _content = null;
                                _decoder.Dispose();
                            }
                            else if (status == OperationStatus.InvalidData) 
                            {
                                throw new InvalidDataException($"OperationStatus:{status}");
                            }
                            if (bytesWritten == 0)
                                return Read(buffer);
                            return bytesWritten;
                        }
                    }
                    else
                    {
                        var status = _decoder.Decompress(_buffer.AsSpan(_offset, _length), buffer, out var bytesConsumed, out var bytesWritten);
                        _offset += bytesConsumed;
                        _length -= bytesConsumed;
                        if (status == OperationStatus.Done)
                        {
                            if (_length != 0)
                                throw new InvalidDataException("Remaining");
                            _length = _content.Read(_buffer);
                            if (_length != 0)
                                throw new InvalidDataException("Remaining");
                            ArrayPool<byte>.Shared.Return(_buffer);
                            _buffer = null;
                            _content = null;
                            _decoder.Dispose();
                        }
                        else if (status == OperationStatus.InvalidData)
                        {
                            throw new InvalidDataException($"OperationStatus:{status}");
                        }
                        if (bytesWritten == 0)
                            return Read(buffer);
                        return bytesWritten;
                    }
                }
            }
            public int Read(byte[] buffer, int offset, int count)
            {
                return Read(buffer.AsSpan(offset, count));
            }
            public async ValueTask<int> ReadAsync(Memory<byte> buffer)
            {
                if (buffer.IsEmpty)
                    return 0;
                if (_content == null)
                    return 0;
                if (_buffer == null)
                {
                    var status = _decoder.Decompress(Array.Empty<byte>(), buffer.Span, out var bytesConsumed, out var bytesWritten);
                    if (status == OperationStatus.Done)
                    {
                        _content = null;
                        _decoder.Dispose();
                    }
                    else if (status != OperationStatus.DestinationTooSmall)//?
                    {
                        throw new InvalidDataException($"OperationStatus:{status}");
                    }
                    return bytesWritten;
                }
                else
                {
                    if (_length == 0)
                    {
                        _length = await _content.ReadAsync(_buffer);
                        if (_length == 0)
                        {
                            ArrayPool<byte>.Shared.Return(_buffer);
                            _buffer = null;
                            var status = _decoder.Decompress(Array.Empty<byte>(), buffer.Span, out var bytesConsumed, out var bytesWritten);
                            if (status == OperationStatus.Done)
                            {
                                _content = null;
                                _decoder.Dispose();
                            }
                            else if (status != OperationStatus.DestinationTooSmall)//?
                            {
                                throw new InvalidDataException($"OperationStatus:{status}");
                            }
                            return bytesWritten;
                        }
                        else
                        {
                            _offset = 0;
                            var status = _decoder.Decompress(_buffer.AsSpan(_offset, _length), buffer.Span, out var bytesConsumed, out var bytesWritten);
                            _offset += bytesConsumed;
                            _length -= bytesConsumed;
                            if (status == OperationStatus.Done)
                            {
                                if (_length != 0)
                                    throw new InvalidDataException("Remaining");
                                _length = await _content.ReadAsync(_buffer);
                                if (_length != 0)
                                    throw new InvalidDataException("Remaining");
                                ArrayPool<byte>.Shared.Return(_buffer);
                                _buffer = null;
                                _content = null;
                                _decoder.Dispose();
                            }
                            else if (status == OperationStatus.InvalidData)
                            {
                                throw new InvalidDataException($"OperationStatus:{status}");
                            }
                            if (bytesWritten == 0)
                                return await ReadAsync(buffer);
                            return bytesWritten;
                        }
                    }
                    else
                    {
                        var status = _decoder.Decompress(_buffer.AsSpan(_offset, _length), buffer.Span, out var bytesConsumed, out var bytesWritten);
                        _offset += bytesConsumed;
                        _length -= bytesConsumed;
                        if (status == OperationStatus.Done)
                        {
                            if (_length != 0)
                                throw new InvalidDataException("Remaining");
                            _length = await _content.ReadAsync(_buffer);
                            if (_length != 0)
                                throw new InvalidDataException("Remaining");
                            ArrayPool<byte>.Shared.Return(_buffer);
                            _buffer = null;
                            _content = null;
                            _decoder.Dispose();
                        }
                        else if (status == OperationStatus.InvalidData)
                        {
                            throw new InvalidDataException($"OperationStatus:{status}");
                        }
                        if (bytesWritten == 0)
                            return await ReadAsync(buffer);
                        return bytesWritten;
                    }
                }
            }
            public ValueTask<int> ReadAsync(byte[] buffer, int offset, int count)
            {
                return ReadAsync(buffer.AsMemory(offset, count));
            }
            public void Dispose()
            {
                var content = _content;
                _content = null;
                if (content != null)
                {
                    _decoder.Dispose();
                }
                var buffer = _buffer;
                _buffer = null;
                if (buffer != null)
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }
        //private class TimeoutClient : HttpClient
        //{
        //    private HttpClient _client;
        //    private int _timeout;
        //    private int _readTimeout;
        //    public TimeoutClient(HttpClient client, int timeout, int readTimeout)
        //    {
        //        _client = client;
        //        _timeout = timeout;
        //        _readTimeout = readTimeout;
        //    }
        //    public override async Task<HttpResponse> SendAsync(HttpRequest request)
        //    {
        //        var response = default(HttpResponse);
        //        if (_timeout > 0)
        //        {
        //            response = await _client.SendAsync(request).Timeout(_timeout,
        //                (task) =>
        //                {
        //                    if (task.IsCompletedSuccessfully)
        //                    {
        //                        task.Result.Dispose();
        //                    }
        //                });
        //        }
        //        else
        //        {
        //            response = await _client.SendAsync(request);
        //        }
        //        if (response.Content != null && _readTimeout > 0)
        //        {
        //            response.Content = new TimeoutContent(response, _readTimeout);
        //        }
        //        return response;
        //    }
        //    public class TimeoutContent : IHttpContent
        //    {
        //        private HttpResponse _response;
        //        private IHttpContent _content;
        //        private int _timeout;
        //        public TimeoutContent(HttpResponse response, int timeout)
        //        {
        //            _response = response;
        //            _content = response.Content;
        //            _timeout = timeout;
        //        }
        //        public long Available => _content.Available;
        //        public long Length => _content.Length;
        //        public long ComputeLength() => _content.ComputeLength();
        //        public bool Rewind() => _content.Rewind();
        //        public int Read(Span<byte> buffer) 
        //        {
        //            unsafe
        //            {
        //                fixed (byte* pBytes = buffer)
        //                {
        //                    var valueTask = _content.ReadAsync(new UnmanagedMemory<byte>(pBytes, buffer.Length));
        //                    if (valueTask.IsCompleted)
        //                        return valueTask.Result;

        //                    var task = valueTask.AsTask();
        //                    try
        //                    {
        //                        return task.Timeout(_timeout).Result;
        //                    }
        //                    catch (TimeoutException)
        //                    {
        //                        _response.Dispose();
        //                        return task.Result;
        //                    }
        //                }
        //            }
        //        }
        //        public int Read(byte[] buffer, int offset, int count) 
        //        {
        //            var valueTask = _content.ReadAsync(buffer, offset, count);
        //            if (valueTask.IsCompleted)
        //                return valueTask.Result;

        //            var task = valueTask.AsTask();
        //            try
        //            {
        //                return task.Timeout(_timeout).Result;
        //            }
        //            catch (TimeoutException)
        //            {
        //                _response.Dispose();
        //                return task.Result;
        //            }
        //        }
        //        public async ValueTask<int> ReadAsync(Memory<byte> buffer)
        //        {
        //            var valueTask = _content.ReadAsync(buffer);
        //            if (valueTask.IsCompleted)
        //                return valueTask.Result;

        //            var task = valueTask.AsTask();
        //            try
        //            {
        //                return await task.Timeout(_timeout);
        //            }
        //            catch (TimeoutException)
        //            {
        //                _response.Dispose();
        //                return await task;
        //            }
        //        }
        //        public async ValueTask<int> ReadAsync(byte[] buffer, int offset, int count)
        //        {
        //            var valueTask = _content.ReadAsync(buffer, offset, count);
        //            if (valueTask.IsCompleted)
        //                return valueTask.Result;

        //            var task = valueTask.AsTask();
        //            try
        //            {
        //                return await task.Timeout(_timeout);
        //            }
        //            catch (TimeoutException)
        //            {
        //                _response.Dispose();
        //                return await task;
        //            }
        //        }
        //    }
        //}
        private class TimeoutQueueClient : HttpClient
        {
            private static Action<Task<HttpResponse>> _Continuation = (task) =>
            {
                if (task.IsCompletedSuccessfully)
                {
                    task.Result.Dispose();
                }
            };
            private HttpClient _client;
            private TaskTimeoutQueue<HttpResponse> _timeout;
            private TaskTimeoutQueue<int> _readTimeout;
            public TimeoutQueueClient(HttpClient client, TaskTimeoutQueue<HttpResponse> timeout, TaskTimeoutQueue<int> readTimeout)
            {
                _client = client;
                _timeout = timeout;
                _readTimeout = readTimeout;
            }
            public override async Task<HttpResponse> SendAsync(HttpRequest request)
            {
                var response = await _client.SendAsync(request).Timeout(_timeout, _Continuation);
                if (response.Content != null && _readTimeout != null)
                {
                    response.Content = new TimeoutContent(response, _readTimeout);
                }
                return response;
            }
            public class TimeoutContent : IHttpContent
            {
                private HttpResponse _response;
                private IHttpContent _content;
                private TaskTimeoutQueue<int> _timeout;
                public TimeoutContent(HttpResponse response, TaskTimeoutQueue<int> timeout)
                {
                    _response = response;
                    _content = response.Content;
                    _timeout = timeout;
                }
                public long Available => _content.Available;
                public long Length => _content.Length;
                public long ComputeLength() => _content.ComputeLength();
                public bool Rewind() => _content.Rewind();
                public int Read(Span<byte> buffer) 
                {
                    unsafe
                    {
                        fixed (byte* pBytes = buffer)
                        {
                            var valueTask = _content.ReadAsync(new UnmanagedMemory<byte>(pBytes, buffer.Length));
                            if (valueTask.IsCompleted)
                                return valueTask.Result;

                            var task = valueTask.AsTask();
                            try
                            {
                                return task.Timeout(_timeout).Result;
                            }
                            catch
                            {
                                _response.Dispose();
                                try { task.Wait(); } catch { }
                                throw;
                            }
                        }
                    }
                }
                public int Read(byte[] buffer, int offset, int count)
                {
                    var valueTask = _content.ReadAsync(buffer, offset, count);
                    if (valueTask.IsCompleted)
                        return valueTask.Result;

                    var task = valueTask.AsTask();
                    try
                    {
                        return task.Timeout(_timeout).Result;
                    }
                    catch
                    {
                        _response.Dispose();
                        try { task.Wait(); } catch { }
                        throw;
                    }
                }
                public async ValueTask<int> ReadAsync(Memory<byte> buffer)
                {
                    var valueTask = _content.ReadAsync(buffer);
                    if (valueTask.IsCompleted)
                        return valueTask.Result;

                    var task = valueTask.AsTask();
                    try
                    {
                        return await task.Timeout(_timeout);
                    }
                    catch
                    {
                        _response.Dispose();
                        try { await task; } catch { }
                        throw;
                    }
                }
                public async ValueTask<int> ReadAsync(byte[] buffer, int offset, int count)
                {
                    var valueTask = _content.ReadAsync(buffer, offset, count);
                    if (valueTask.IsCompleted)
                        return valueTask.Result;

                    var task = valueTask.AsTask();
                    try
                    {
                        return await task.Timeout(_timeout);
                    }
                    catch
                    {
                        _response.Dispose();
                        try { await task; } catch { }
                        throw;
                    }
                }
            }
        }
        #endregion

        #region Use(onRequest,onResponse)
        //public static HttpClient Use(this HttpClient @this, Action<HttpRequest> onRequest)
        //{
        //    if (@this == null)
        //        throw new ArgumentNullException(nameof(@this));
        //    if (onRequest == null)
        //        throw new ArgumentNullException(nameof(onRequest));

        //    return new OnRequestClient(@this, onRequest);
        //}
        //public static HttpClient Use(this HttpClient @this, Func<HttpRequest, Task> onRequest)
        //{
        //    if (@this == null)
        //        throw new ArgumentNullException(nameof(@this));
        //    if (onRequest == null)
        //        throw new ArgumentNullException(nameof(onRequest));

        //    return new OnRequestAsyncClient(@this, onRequest);
        //}
        //public static HttpClient Use(this HttpClient @this, Action<HttpRequest, HttpResponse> onResponse)
        //{
        //    if (@this == null)
        //        throw new ArgumentNullException(nameof(@this));
        //    if (onResponse == null)
        //        throw new ArgumentNullException(nameof(onResponse));

        //    return new OnResponseClient(@this, onResponse);
        //}
        //public static HttpClient Use(this HttpClient @this, Func<HttpRequest, HttpResponse, Task> onResponse)
        //{
        //    if (@this == null)
        //        throw new ArgumentNullException(nameof(@this));
        //    if (onResponse == null)
        //        throw new ArgumentNullException(nameof(onResponse));

        //    return new OnResponseAsyncClient(@this, onResponse);
        //}
        //private class OnRequestClient : HttpClient
        //{
        //    private HttpClient _client;
        //    private Action<HttpRequest> _onRequest;
        //    public OnRequestClient(HttpClient client, Action<HttpRequest> onRequest)
        //    {
        //        _client = client;
        //        _onRequest = onRequest;
        //    }
        //    public override Task<HttpResponse> SendAsync(HttpRequest request)
        //    {
        //        _onRequest.Invoke(request);
        //        return _client.SendAsync(request);
        //    }
        //}
        //private class OnRequestAsyncClient : HttpClient
        //{
        //    private HttpClient _client;
        //    private Func<HttpRequest, Task> _onRequest;
        //    public OnRequestAsyncClient(HttpClient client, Func<HttpRequest, Task> onRequest)
        //    {
        //        _client = client;
        //        _onRequest = onRequest;
        //    }
        //    public override async Task<HttpResponse> SendAsync(HttpRequest request)
        //    {
        //        await _onRequest.Invoke(request);
        //        return await _client.SendAsync(request);
        //    }
        //}
        //private class OnResponseClient : HttpClient
        //{
        //    private HttpClient _client;
        //    private Action<HttpRequest, HttpResponse> _onResponse;
        //    public OnResponseClient(HttpClient client, Action<HttpRequest, HttpResponse> onResponse)
        //    {
        //        _client = client;
        //        _onResponse = onResponse;
        //    }
        //    public override async Task<HttpResponse> SendAsync(HttpRequest request)
        //    {
        //        var response = await _client.SendAsync(request);
        //        _onResponse.Invoke(request, response);
        //        return response;
        //    }
        //}
        //private class OnResponseAsyncClient : HttpClient
        //{
        //    private HttpClient _client;
        //    private Func<HttpRequest, HttpResponse, Task> _onResponse;
        //    public OnResponseAsyncClient(HttpClient client, Func<HttpRequest, HttpResponse, Task> onResponse)
        //    {
        //        _client = client;
        //        _onResponse = onResponse;
        //    }
        //    public override async Task<HttpResponse> SendAsync(HttpRequest request)
        //    {
        //        var response = await _client.SendAsync(request);
        //        await _onResponse.Invoke(request, response);
        //        return response;
        //    }
        //}
        #endregion
        //private class RetryClient : HttpClient
        //{
        //    private HttpClient _client;
        //    private int _maxRetry;
        //    public RetryClient(HttpClient client, int maxRetry)
        //    {
        //        _client = client;
        //        _maxRetry = maxRetry;
        //    }
        //    public override Task<HttpResponse> SendAsync(HttpRequest request)
        //    {
        //        throw new NotImplementedException();
        //    }
        //}
    }
}
