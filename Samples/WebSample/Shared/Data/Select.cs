using System;
using System.Data;
using System.Linq.Expressions;

namespace WebSample
{
    public class Select<TEntity>
    {
        private Expression<Func<TEntity, SqlExpression, TEntity>> _select;
        public Select(Expression<Func<TEntity, SqlExpression, TEntity>> select) 
        {
            if (select == null)
                throw new ArgumentNullException(nameof(select));

            _select = select;
        }
        public override string ToString() => _select?.ToString();

        public static implicit operator Expression<Func<TEntity, SqlExpression, TEntity>>(Select<TEntity> @this)=> @this?._select;

        //缓存导航的select表达式树
        //cache Navigate select Expression
        private static Select<TEntity> _Navigate;
        public static Select<TEntity> Navigate=> _Navigate;
        static Select() 
        {
            _Navigate = new Select<TEntity>((e, s) => s.Navigate(e));

            //new Select<TEntity>((e, s) => e);
        }
    }
}
