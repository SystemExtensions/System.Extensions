using System;
using System.Data;
using System.Linq.Expressions;

namespace WebSample 
{ 
    public class Where<TEntity>
    {
        private Expression<Func<TEntity, SqlExpression, bool>> _where;
        public Where() 
        {
            _where = _Where;
        }
        public Where(Expression<Func<TEntity, bool>> where)
        {
            _where= _Where.And(where);
        }
        public Where(Expression<Func<TEntity, SqlExpression, bool>> where) 
        {
            _where = _Where.And(where);
        }
        public Where<TEntity> And(Expression<Func<TEntity, bool>> where) 
        {
            _where = _where.And(where);
            return this;
        }
        public Where<TEntity> And(Expression<Func<TEntity, SqlExpression, bool>> where)
        {
            _where = _where.And(where);
            return this;
        }
        public Where<TEntity> Or(Expression<Func<TEntity, bool>> where)
        {
            _where = _where.Or(where);
            return this;
        }
        public Where<TEntity> Or(Expression<Func<TEntity, SqlExpression, bool>> where)
        {
            _where = _where.Or(where);
            return this;
        }
        public Where<TEntity> AndIf(bool condition, Expression<Func<TEntity, bool>> where)
        {
            _where = _where.AndIf(condition, where);
            return this;
        }
        public Where<TEntity> AndIf(bool condition, Expression<Func<TEntity, SqlExpression, bool>> where)
        {
            _where = _where.AndIf(condition, where);
            return this;
        }
        public Where<TEntity> AndIf(string value, Expression<Func<TEntity, bool>> where)
        {
            return AndIf(!string.IsNullOrEmpty(value), where);
        }
        public Where<TEntity> AndIf(string value, Expression<Func<TEntity, SqlExpression, bool>> where)
        {
            return AndIf(!string.IsNullOrEmpty(value), where);
        }
        public Where<TEntity> OrIf(bool condition, Expression<Func<TEntity, bool>> where)
        {
            _where = _where.OrIf(condition, where);
            return this;
        }
        public Where<TEntity> OrIf(bool condition, Expression<Func<TEntity, SqlExpression, bool>> where)
        {
            _where = _where.OrIf(condition, where);
            return this;
        }
        public Where<TEntity> OrIf(string value, Expression<Func<TEntity, bool>> where)
        {
            return OrIf(!string.IsNullOrEmpty(value), where);
        }
        public Where<TEntity> OrIf(string value, Expression<Func<TEntity, SqlExpression, bool>> where)
        {
            return OrIf(!string.IsNullOrEmpty(value), where);
        }
        public override string ToString() => _where?.ToString();

        public static implicit operator Expression<Func<TEntity, SqlExpression, bool>>(Where<TEntity> @this) => @this?._where;

        private static Expression<Func<TEntity, SqlExpression, bool>> _Where;//default where
        static Where() 
        {
            //sample (e,s)=>e.IsDelete==false

            var entity = Expression.Parameter(typeof(TEntity), "e");
            var sqlExpr = Expression.Parameter(typeof(SqlExpression), "s");
            var isDelete = typeof(TEntity).GetProperty("IsDelete");

            if (isDelete != null) 
            {
                _Where = Expression.Lambda<Func<TEntity, SqlExpression, bool>>(
                    Expression.Equal(
                        Expression.Property(entity, isDelete),
                        Expression.Constant(false)
                        ),
                    entity, sqlExpr
                    );
            }
        }
    }
}
