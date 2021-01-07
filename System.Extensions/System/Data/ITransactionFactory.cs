
namespace System.Data
{
    using System.Data.Common;
    public interface ITransactionFactory
    {
        DbTransaction Create();
        DbTransaction Create(IsolationLevel isolationLevel);
    }
}
