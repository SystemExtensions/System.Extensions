
namespace System.Data
{
    public sealed class SqlExpression
    {
        public T Sql<T>(string sql)
        {
            throw new InvalidOperationException(nameof(Sql));
        }
        public SqlExpression Asc(object param)
        {
            throw new InvalidOperationException(nameof(Asc));
        }
        public SqlExpression Desc(object param)
        {
            throw new InvalidOperationException(nameof(Desc));
        }
        public T Distinct<T>(T param)
        {
            throw new InvalidOperationException(nameof(Distinct));
        }
        public T Max<T>(T param)
        {
            throw new InvalidOperationException(nameof(Max));
        }
        public T Min<T>(T param)
        {
            throw new InvalidOperationException(nameof(Min));
        }
        public T Sum<T>(T param)
        {
            throw new InvalidOperationException(nameof(Sum));
        }
        public T Avg<T>(T param)
        {
            throw new InvalidOperationException(nameof(Avg));
        }
        public int Count()
        {
            throw new InvalidOperationException(nameof(Count));
        }
        public int Count(object param)
        {
            throw new InvalidOperationException(nameof(Count));
        }
        public bool Exists(object param)
        {
            throw new InvalidOperationException(nameof(Exists));
        }
        public bool NotExists(object param)
        {
            throw new InvalidOperationException(nameof(NotExists));
        }
        public bool Like(object param1, string param2)
        {
            throw new InvalidOperationException(nameof(Like));
        }
        public bool NotLike(object param1, string param2)
        {
            throw new InvalidOperationException(nameof(NotLike));
        }
        public bool In(object param1, object param2)
        {
            throw new InvalidOperationException(nameof(In));
        }
        public bool NotIn(object param1, object param2)
        {
            throw new InvalidOperationException(nameof(NotIn));
        }
        public bool Between(object param1, object param2, object param3)
        {
            throw new InvalidOperationException(nameof(Between));
        }
        public bool NotBetween(object param1, object param2, object param3)
        {
            throw new InvalidOperationException(nameof(NotBetween));
        }
        public new bool Equals(object param1, object param2)
        {
            throw new InvalidOperationException(nameof(Equals));
        }
        public bool NotEquals(object param1, object param2)
        {
            throw new InvalidOperationException(nameof(NotEquals));
        }
    }
}
