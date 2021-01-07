using System.Extensions.Http;
using System.Runtime.Serialization;

namespace WebSample
{
    public class JsonData
    {
        [DataMember(Name ="code")]
        public int Code { get; set; }

        [DataMember(Name = "msg")]
        public string Message { get; set; }

        [DataMember(Name = "count", EmitDefaultValue = false)]
        public int? Count { get; set; }//Page

        [DataMember(Name = "data", EmitDefaultValue = false)]
        public object Data { get; set; }
        public void Invoke(HttpRequest request, HttpResponse response)
        {
            response.UseJson(this);
        }
    }
}
