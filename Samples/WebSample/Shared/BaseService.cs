using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Extensions.Http;

namespace WebSample
{
    public class BaseService
    {
        //public IQueryParams QueryParams { get; set; }
        //public IFormParams FormParams { get; set; }
        public HttpRequest Request { get; set; }
        public HttpResponse Response { get; set; }
        public SqlDb Db { get; set; }

        private IDictionary<string, object> _viewData;
        public IDictionary<string, object> ViewData 
        {
            get 
            {
                if (_viewData == null)
                    _viewData = new Dictionary<string, object>();

                return _viewData;
            }
        }
        public View View(string name)
        {
            return new View(name, null, _viewData);
        }
        public View View(string name, object model)
        {
            return new View(name, model, _viewData);
        }
        public View View(string name, object model, IDictionary<string, object> viewData)
        {
            return new View(name, model, viewData);
        }
        public JsonData Json(int code, string message) 
        {
            return new JsonData() 
            {
                Code=code,
                Message=message
            };
        }
        public JsonData Json(int code, string message,object data)
        {
            return new JsonData()
            {
                Code = code,
                Message = message,
                Data=data
            };
        }
        public JsonData Json(int code, string message, int count, object data)
        {
            return new JsonData()
            {
                Code = code,
                Message = message,
                Count = count,
                Data = data
            };
        }

        //many ways
        //public void Json(int code, string message) 
        //{
        //    new JsonData()
        //    {
        //        Code = code,
        //        Message = message
        //    }.Invoke(Request, Response);
        //}


        //your Custom
    }
}
