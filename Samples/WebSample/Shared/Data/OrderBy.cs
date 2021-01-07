using System;
using System.Data;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace WebSample
{
    public class OrderBy<TEntity>
    {
        private Expression<Action<TEntity, SqlExpression>> _orderBy;

        public OrderBy() { }
        public OrderBy(Expression<Action<TEntity, SqlExpression>> orderBy) 
        {
            _orderBy = orderBy;
        }
        public OrderBy<TEntity> Desc(Expression<Func<TEntity, object>> orderBy) 
        {
            _orderBy = _orderBy.Desc(orderBy);
            return this;
        }
        public OrderBy<TEntity> DescIf(bool condition, Expression<Func<TEntity, object>> orderBy)
        {
            _orderBy = _orderBy.DescIf(condition, orderBy);
            return this;
        }
        public OrderBy<TEntity> Asc(Expression<Func<TEntity, object>> orderBy)
        {
            _orderBy = _orderBy.Asc(orderBy);
            return this;
        }
        public OrderBy<TEntity> AscIf(bool condition, Expression<Func<TEntity, object>> orderBy)
        {
            _orderBy = _orderBy.AscIf(condition, orderBy);
            return this;
        }

        public static implicit operator Expression<Action<TEntity, SqlExpression>>(OrderBy<TEntity> @this)=> @this?._orderBy;
        public override string ToString() => _orderBy?.ToString();

        //private static Expression<Action<TEntity, SqlExpression>> _OrderBy;//default OrderBy
        //static OrderBy()
        //{
        //    //sample (e,s)=>s.Desc(e.Id)

        //    var entity = Expression.Parameter(typeof(TEntity), "e");
        //    var sqlExpr = Expression.Parameter(typeof(SqlExpression), "s");
        //    var id = typeof(TEntity).GetProperty("Id");
        //    if (id != null)
        //    {
        //        _OrderBy = Expression.Lambda<Action<TEntity, SqlExpression>>(
        //            Expression.Call(
        //                typeof(SqlExpression).GetMethod("Desc"),
        //                Expression.Property(entity, id)
        //                ),
        //            entity, sqlExpr
        //            );
        //    }
        //}
    }
    public class OrderByDictionary<TEntity>
    {
        private string _asc;
        private string _desc;
        private Dictionary<(string, string), OrderBy<TEntity>> _orderByDictionary;
        public OrderByDictionary()
            : this("asc", "desc", null)
        { }
        public OrderByDictionary(string asc,string desc)
            :this(asc,desc,null)
        { }
        public OrderByDictionary(Expression<Func<TEntity, object[]>> orderByProperties)
            :this("asc","desc", orderByProperties)
        { }
        public OrderByDictionary(string asc, string desc, Expression<Func<TEntity, object[]>> orderByProperties)
        {
            _asc = asc;
            _desc = desc;
            if (orderByProperties == null)
                return;

            _orderByDictionary = new Dictionary<(string, string), OrderBy<TEntity>>();//Comparer??
            var exprs= ((NewArrayExpression)orderByProperties.Body).Expressions;
            foreach (var expr in exprs)
            {
                var entity = orderByProperties.Parameters[0];
                var sqlExpr = Expression.Parameter(typeof(SqlExpression), "s");
                var ascExpr = Expression.Lambda<Action<TEntity, SqlExpression>>(
                    Expression.Call(sqlExpr, typeof(SqlExpression).GetMethod("Asc"), expr),
                    entity, sqlExpr
                    );
                var descExpr = Expression.Lambda<Action<TEntity, SqlExpression>>(
                   Expression.Call(sqlExpr, typeof(SqlExpression).GetMethod("Desc"), expr),
                   entity, sqlExpr
                   );

                var exprString = expr.ToString();
                if (expr.NodeType == ExpressionType.Convert)
                    exprString = ((UnaryExpression)expr).Operand.ToString();

                var i1 = exprString.IndexOf('.');
                var name = exprString.Substring(i1 + 1);

                _orderByDictionary.Add((name, _asc), new OrderBy<TEntity>(ascExpr));
                _orderByDictionary.Add((name, _desc), new OrderBy<TEntity>(descExpr));
            }
        }
        public OrderBy<TEntity> this[(string, string) key]
        {
            get
            {
                _orderByDictionary.TryGetValue(key, out var orderBy);
                return orderBy;
            }
        }
        public OrderByDictionary<TEntity> Add(string field, Expression<Func<TEntity, object>> orderByProperty)
        {
            var entity = orderByProperty.Parameters[0];
            var sqlExpr = Expression.Parameter(typeof(SqlExpression), "s");
            var ascExpr = Expression.Lambda<Action<TEntity, SqlExpression>>(
                Expression.Call(sqlExpr, typeof(SqlExpression).GetMethod("Asc"), orderByProperty.Body),
                entity, sqlExpr
                );
            var descExpr = Expression.Lambda<Action<TEntity, SqlExpression>>(
               Expression.Call(sqlExpr, typeof(SqlExpression).GetMethod("Desc"), orderByProperty.Body),
               entity, sqlExpr
               );

            _orderByDictionary.Add((field, _asc), new OrderBy<TEntity>(ascExpr));
            _orderByDictionary.Add((field, _desc), new OrderBy<TEntity>(descExpr));
            return this;
        }
        public OrderByDictionary<TEntity> AddAsc(string field, Expression<Func<TEntity, object>> orderByProperty)
        {
            var entity = orderByProperty.Parameters[0];
            var sqlExpr = Expression.Parameter(typeof(SqlExpression), "s");
            var ascExpr = Expression.Lambda<Action<TEntity, SqlExpression>>(
                Expression.Call(sqlExpr, typeof(SqlExpression).GetMethod("Asc"), orderByProperty.Body),
                entity, sqlExpr
                );
            _orderByDictionary.Add((field, _asc), new OrderBy<TEntity>(ascExpr));
            return this;
        }
        public OrderByDictionary<TEntity> AddDesc(string field, Expression<Func<TEntity, object>> orderByProperty)
        {
            var entity = orderByProperty.Parameters[0];
            var sqlExpr = Expression.Parameter(typeof(SqlExpression), "s");
            var descExpr = Expression.Lambda<Action<TEntity, SqlExpression>>(
               Expression.Call(sqlExpr, typeof(SqlExpression).GetMethod("Desc"), orderByProperty.Body),
               entity, sqlExpr
               );
            _orderByDictionary.Add((field, _desc), new OrderBy<TEntity>(descExpr));
            return this;
        }
    }
}
