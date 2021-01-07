
namespace System.Extensions.Http
{
    using System.IO;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    public interface IFormFile
    {
        long Length { get; }
        string FileName { get; }
        string ContentType { get; }
        Task SaveAsync(string filePath);
        Stream OpenRead();

        //void CopyTo(string destFile);
        //使用扩展方法TryGetExtension
        //ContentDisposition 
        //string Extension { get; }
        //FileInfo Content { get; }//放弃IHttpContent 
    }
    public interface IFormFileParams : IEnumerable<KeyValuePair<string, IFormFile>>
    {
        KeyValuePair<string, IFormFile> this[int index] { get; }
        int Count { get; }
        bool Contains(string name);
        bool TryGetValue(string name, out IFormFile value);
        IFormFile[] GetValues(string name);
    }
}
