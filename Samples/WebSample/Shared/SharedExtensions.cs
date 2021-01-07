using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using System.Linq.Expressions;

namespace WebSample
{
    //根据自己希望的默认进行扩展
    //Extensions according to the default you want
    public static class SharedExtensions
    {

        //SqlDb
        public static Task<TEntity> SelectSingleAsync<TEntity>(this SqlDb @this, object identity)
        {
            return @this.SelectSingleAsync<TEntity>(Select<TEntity>.Navigate, identity);
        }
        public static Task<TEntity> SelectSingleAsync<TEntity>(this SqlDb @this, Expression<Func<TEntity, SqlExpression, bool>> where)
        {
            return @this.SelectSingleAsync(Select<TEntity>.Navigate, where);
        }
        public static Task<List<TEntity>> SelectAsync<TEntity>(this SqlDb @this, Expression<Func<TEntity, SqlExpression, bool>> where)
        {
            return @this.SelectAsync(Select<TEntity>.Navigate, where, null);
        }
        public static Task<(List<TEntity>, int)> SelectPagedAsync<TEntity>(this SqlDb @this, int offset, int fetch, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
        {
            return @this.SelectPagedAsync(offset, fetch, Select<TEntity>.Navigate, where, orderBy);
        }
        //Insert When(Auto increment primary key)
        private class InsertProperties //Except
        {
            public InsertProperties(int identity) {}
        }
        private static class Insert<TEntity>
        {
            static Insert() 
            {
                var entity = Expression.Parameter(typeof(TEntity), "entity");

                //Filter
                var filters = new List<Expression>();
                //Set entity.CreateTime=DateTime.Now
                var createTime = typeof(TEntity).GetProperty("CreateTime");
                if (createTime != null)
                {
                    filters.Add(Expression.Assign(Expression.Property(entity, createTime), Expression.Property(null, typeof(DateTime).GetProperty("Now"))));
                }
                if (filters.Count > 0)
                {
                    Filter = Expression.Lambda<Action<TEntity>>(Expression.Block(filters), entity).Compile();
                }

                //Properties
                //Ignore Identity
                //entity=>SqlDbExtensions.Except(new InsertProperties<int>(entity.Id));OR entity=>SqlDbExtensions.Except(entity.Id)
                var properties = new List<Expression>();
                SqlDbExtensions.RegisterIdentity<TEntity>(out var identity);
                if (identity == null)
                {
                    properties.Add(Expression.Default(typeof(int)));
                }
                else
                {
                    properties.Add(Expression.Property(entity, identity));
                }
                Properties = Expression.Lambda<Func<TEntity, object>>(
                        Expression.Call(null, typeof(SqlDbExtensions).GetMethod("Except"),
                            Expression.New(typeof(InsertProperties).GetConstructors()[0], properties)),
                        entity
                        );
            }
            public static Action<TEntity> Filter;
            public static Expression<Func<TEntity, object>> Properties;
        }
        public static Task<int> InsertAsync<TEntity>(this SqlDb @this, TEntity entity) 
        {
            Insert<TEntity>.Filter?.Invoke(entity);
            return @this.InsertAsync<TEntity>(entity, Insert<TEntity>.Properties);
        }
        public static Task<int> InsertIdentityAsync<TEntity>(this SqlDb @this, TEntity entity)
        {
            Insert<TEntity>.Filter?.Invoke(entity);
            return @this.InsertIdentityAsync<TEntity, int>(entity, Insert<TEntity>.Properties);//Identity is int
        }
        private class UpdateProperties //Except
        {
            public UpdateProperties(int identity, DateTime createTime) { }
        }
        private static class Update<TEntity>
        {
            static Update()
            {
                var entity = Expression.Parameter(typeof(TEntity), "entity");

                //Filter
                var filters = new List<Expression>();
                //Set entity.UpdateTime=DateTime.Now
                var updateTime = typeof(TEntity).GetProperty("UpdateTime");
                if (updateTime != null)
                {
                    filters.Add(Expression.Assign(Expression.Property(entity, updateTime), Expression.Property(null, typeof(DateTime).GetProperty("Now"))));
                }
                if (filters.Count > 0)
                {
                    Filter = Expression.Lambda<Action<TEntity>>(Expression.Block(filters), entity).Compile();
                }

                //Properties
                //Ignore Identity,CreateTime
                //entity=>SqlDbExtensions.Except(new UpdateProperties<int>(entity.Id,entity.CreateTime));
                var properties = new List<Expression>();
                SqlDbExtensions.RegisterIdentity<TEntity>(out var identity);
                if (identity == null)
                {
                    properties.Add(Expression.Default(typeof(int)));
                }
                else
                {
                    properties.Add(Expression.Property(entity, identity));
                }
                var createTime = typeof(TEntity).GetProperty("CreateTime");
                if (createTime == null)
                {
                    properties.Add(Expression.Default(typeof(DateTime)));
                }
                else
                {
                    properties.Add(Expression.Property(entity, createTime));
                }
                Properties = Expression.Lambda<Func<TEntity, object>>(
                        Expression.Call(null, typeof(SqlDbExtensions).GetMethod("Except"),
                            Expression.New(typeof(UpdateProperties).GetConstructors()[0], properties)),
                        entity
                        );
            }
            public static Action<TEntity> Filter;
            public static Expression<Func<TEntity, object>> Properties;
        }
        public static Task<int> UpdateAsync<TEntity>(this SqlDb @this, TEntity entity)
        {
            Update<TEntity>.Filter?.Invoke(entity);
            return @this.UpdateAsync<TEntity>(entity, Update<TEntity>.Properties);
        }
        public static Task<int> UpdateAsync<TEntity>(this SqlDb @this, TEntity entity, Expression<Func<TEntity, SqlExpression, bool>> where)
        {
            Update<TEntity>.Filter?.Invoke(entity);
            return @this.UpdateAsync<TEntity>(entity, Update<TEntity>.Properties, where);
        }

        //your Other Extensions


    }
}
