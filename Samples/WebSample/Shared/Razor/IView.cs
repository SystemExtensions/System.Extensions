
namespace WebSample
{
    using System;
    using System.IO;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    public interface IView
    {
        object Model { get; set; }
        IDictionary<string, object> ViewData { get; set; }
        IDictionary<string, Func<Task>> Sections { get; set; }
        TextWriter Output { get; set; }
        ViewEngine Engine { get; set; }
        Task ExecuteAsync();
    }
}
