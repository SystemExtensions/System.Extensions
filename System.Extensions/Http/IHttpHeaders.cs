
namespace System.Extensions.Http
{
    using System.Collections.Generic;
    public interface IHttpHeaders : IEnumerable<KeyValuePair<string, string>>
    {
        KeyValuePair<string, string> this[int index] { get; }
        string this[string name] { get; set; }
        int Count { get; }
        bool Contains(string name);
        void Add(string name, string value);
        bool TryGetValue(string name, out string value);
        string[] GetValues(string name);
        int Remove(string name);
        void Clear();
    }
}
