
namespace System.Extensions.Net
{
    using System.Threading.Tasks;
    public interface IConnectionHandler
    {
        Task HandleAsync(IConnection connection);
    }
}
