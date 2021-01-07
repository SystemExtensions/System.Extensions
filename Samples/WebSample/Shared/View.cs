using System;
using System.Collections.Generic;
using System.Extensions.Http;
using System.Threading.Tasks;
using System.Buffers;
using System.Runtime.Loader;

namespace WebSample
{
    public class View
    {
        public static ViewEngine Engine { get; set; }
        //Register
        static View()
        {
            Engine = new ViewEngine();
            Engine.Register(AssemblyLoadContext.Default.Assemblies);
        }
        public View() 
        {
        
        }
        public View(string name, object model, IDictionary<string, object> viewData)
        {
            Name = name;
            Model = model;
            ViewData = viewData;
        }
        public string Name { get; set; }
        public object Model { get; set; }
        public IDictionary<string, object> ViewData { get; set; }
        public async ValueTask Invoke(HttpRequest request, HttpResponse response) 
        {
            var sb = StringContent.Rent(out var disposable);
            response.RegisterForDispose(disposable);

            var engine = Engine;
            var view= engine.Create(Name);
            if (view == null)
                throw new KeyNotFoundException($"View:{Name}");

            view.Engine = engine;
            view.Model = Model;
            view.ViewData = ViewData;
            view.Output = sb.AsWriter();
            await view.ExecuteAsync();
            response.Headers.Add(HttpHeaders.ContentType, "text/html; charset=utf-8");
            response.Content = StringContent.Create(sb.Sequence);
            //response.Content.ComputeLength();//content-length
        }
    }
}
