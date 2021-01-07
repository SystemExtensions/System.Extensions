
namespace System.Extensions.Http
{
    using System.Collections.Generic;
    public interface IPathParams : IEnumerable<KeyValuePair<string, string>>
    {
        KeyValuePair<string, string> this[int index] { get; }
        int Count { get; }
        bool Contains(string name);
        bool TryGetValue(string name, out string value);
        string[] GetValues(string name);
    }
}
