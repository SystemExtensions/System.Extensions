
namespace System.Data
{
    using System.Reflection;
    using System.Data.Common;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using System.Threading.Tasks;
    public static class SqlDbExtensions
    {
        static SqlDbExtensions()
        {
            _Asc = typeof(SqlExpression).GetMethod("Asc");
            _Desc = typeof(SqlExpression).GetMethod("Desc");
            _Except = typeof(SqlDbExtensions).GetMethod("Except");
            _Navigate = typeof(SqlDbExtensions).GetMethod("Navigate");
            _Select1 = typeof(SqlDbExtensions).GetMethod("Select", new[] { typeof(SqlExpression), typeof(object) });
            var entityType = Type.MakeGenericMethodParameter(0);
            var selectType = typeof(Expression<>).MakeGenericType(typeof(Func<,>).MakeGenericType(entityType, typeof(object)));
            var whereType = typeof(Expression<>).MakeGenericType(typeof(Func<,>).MakeGenericType(entityType, typeof(bool)));
            var groupByType = typeof(Expression<>).MakeGenericType(typeof(Func<,>).MakeGenericType(entityType, typeof(object)));
            var havingType = typeof(Expression<>).MakeGenericType(typeof(Func<,>).MakeGenericType(entityType, typeof(bool)));
            var orderByType = typeof(Expression<>).MakeGenericType(typeof(Action<>).MakeGenericType(entityType));
            _Select2 = typeof(SqlDbExtensions).GetMethod("Select", new[] { typeof(SqlExpression), selectType, whereType });
            _Select3 = typeof(SqlDbExtensions).GetMethod("Select", new[] { typeof(SqlExpression), selectType, whereType, orderByType });
            _Select4 = typeof(SqlDbExtensions).GetMethod("Select", new[] { typeof(SqlExpression), selectType, whereType, groupByType, havingType, orderByType });
            _Select5 = typeof(SqlDbExtensions).GetMethod("Select", new[] { typeof(SqlExpression), typeof(int), typeof(int), selectType, whereType, orderByType });
            _Select6 = typeof(SqlDbExtensions).GetMethod("Select", new[] { typeof(SqlExpression), typeof(int), typeof(int), selectType, whereType, groupByType, havingType, orderByType });
            _Format1 = typeof(string).GetMethod("Format", new[] { typeof(string), typeof(object) });
            _Format2 = typeof(string).GetMethod("Format", new[] { typeof(string), typeof(object), typeof(object) });
            _Format3 = typeof(string).GetMethod("Format", new[] { typeof(string), typeof(object), typeof(object), typeof(object) });
            _Formats = typeof(string).GetMethod("Format", new[] { typeof(string), typeof(object[]) });

            _TableResolver = (type) => type.Name;
            _PropertyResolver = (property) => property.Name;
            _IdentityResolver = (type) =>
            {
                var properties = type.GetGeneralProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                for (var currentType = type; currentType != null; currentType = currentType.BaseType)
                {
                    foreach (var property in properties)
                    {
                        if (!property.CanWrite)
                            continue;
                        if (property.IsDefined(typeof(IgnoreDataColumnAttribute)))
                            continue;
                        if (property.DeclaringType == currentType)
                            return property;
                    }
                }
                return null;
            };
        }
        #region Register
        private static Func<Type, string> _TableResolver;
        private static Func<Type, PropertyInfo> _IdentityResolver;
        private static Func<PropertyInfo, string> _PropertyResolver;
        public static void RegisterTable(Func<Type, string> tableResolver)
        {
            if (tableResolver == null)
                throw new ArgumentNullException(nameof(tableResolver));

            _TableResolver = tableResolver;
        }
        public static void RegisterTable(out Func<Type, string> tableResolver)
        {
            tableResolver = _TableResolver;
        }
        public static void RegisterIdentity(Func<Type, PropertyInfo> identityResolver)
        {
            if (identityResolver == null)
                throw new ArgumentNullException(nameof(identityResolver));

            _IdentityResolver = identityResolver;
        }
        public static void RegisterIdentity(out Func<Type, PropertyInfo> identityResolver)
        {
            identityResolver = _IdentityResolver;
        }
        public static void RegisterProperty(Func<PropertyInfo, string> propertyResolver)
        {
            if (propertyResolver == null)
                throw new ArgumentNullException(nameof(propertyResolver));

            _PropertyResolver = propertyResolver;
        }
        public static void RegisterProperty(out Func<PropertyInfo, string> propertyResolver)
        {
            propertyResolver = _PropertyResolver;
        }
        private static class Identity<TEntity>
        {
            static Identity()
            {
                var identityResolver = _IdentityResolver;
                Value = identityResolver(typeof(TEntity));
            }

            public static PropertyInfo Value;
        }
        public static void RegisterIdentity<TEntity>(out PropertyInfo identity)
        {
            identity = Identity<TEntity>.Value;
        }
        private static class DbReader<TDbDataReader> where TDbDataReader : DbDataReader
        {
            private static readonly object _Sync = new object();
            private static Stack<Func<Type, ParameterExpression, ParameterExpression, Expression>> _FieldHandlers;
            private static Stack<object> _Handlers;
            static DbReader()
            {
                _FieldHandlers = new Stack<Func<Type, ParameterExpression, ParameterExpression, Expression>>();
                _Handlers = new Stack<object>();

                var isDBNull = typeof(TDbDataReader).GetMethod("IsDBNull", new[] { typeof(int) });
                //Get{type.Name}
                Register((type, reader, i) => {
                    var get = typeof(TDbDataReader).GetMethod($"Get{type.Name}", new[] { typeof(int) });
                    if (get == null || get.ReturnType != type)
                        return null;

                    //??? IsDBNull OR try catch
                    return Expression.Condition(
                        Expression.Call(reader, isDBNull, i),
                        Expression.Default(type),
                        Expression.Call(reader, get, i));
                });
                //GetValue
                Register(typeof(object), (reader, i) => Expression.Call(reader, typeof(TDbDataReader).GetMethod("GetValue", new[] { typeof(int) }), i));
                //Nullable<>
                Register((type, reader, i) => {
                    if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(Nullable<>))
                        return null;

                    var nullableType = type.GetGenericArguments()[0];
                    Register(nullableType, reader, i, out var expression);
                    if (expression == null)
                        return Expression.Empty();
                    return Expression.Condition(
                        Expression.Call(reader, isDBNull, i),
                        Expression.Default(type),
                        Expression.New(type.GetConstructor(new[] { nullableType }), expression));
                });
                //Enum
                Register((type, reader, i) => {
                    if (!type.IsEnum)
                        return null;

                    var getInt32 = typeof(TDbDataReader).GetMethod("GetInt32", new[] { typeof(int) });
                    return Expression.Convert(Expression.Call(reader, getInt32, i), type);
                });

                //GetField(0)
                Register((type, reader) => {
                    var i = Expression.Parameter(typeof(int), "i");
                    Register(type, reader, i, out var expression);
                    if (expression == null)
                        return null;
                    return Expression.Block(new[] { i },
                        Expression.Assign(i, Expression.Constant(0)),
                        expression);
                });
                //DataTable
                Register((reader) => {
                    var dt = new DataTable();
                    int fieldCount = reader.FieldCount;
                    for (int i = 0; i < fieldCount; i++)
                    {
                        dt.Columns.Add(reader.GetName(i), reader.GetFieldType(i));
                    }
                    dt.BeginLoadData();
                    var objValues = new object[fieldCount];
                    do
                    {
                        reader.GetValues(objValues);
                        dt.LoadDataRow(objValues, true);

                    } while (reader.Read());
                    dt.EndLoadData();
                    return dt;
                });
                //ISet<>
                Register((type, reader) => {
                    if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(ISet<>))
                        return null;

                    Constructor.Register(type, out var constructor, out _);
                    if (constructor == null)
                        return Expression.Empty();
                    var eleType = type.GetGenericArguments()[0];
                    Register(eleType, reader, out var expression, out var entity, out var entityExpr);
                    if (expression == null)
                        return Expression.Empty();
                    var read = typeof(TDbDataReader).GetMethod("Read", Type.EmptyTypes);
                    var add = type.GetMethod("Add");
                    var value = Expression.Variable(type, "value");
                    var returnLabel = Expression.Label(type);
                    var item = Expression.Variable(eleType, "item");
                    if (entity == null)
                    {
                        return Expression.Block(new[] { value, item },
                            Expression.Assign(value, constructor),
                            Expression.Loop(
                                Expression.Block(
                                    Expression.Assign(item, expression),
                                    Expression.Call(value, add, item),
                                    Expression.IfThen(
                                        Expression.Not(Expression.Call(reader, read)),
                                        Expression.Return(returnLabel, value))
                                )),
                            Expression.Label(returnLabel, value));
                    }
                    else
                    {
                        return Expression.Block(new[] { entity, value, item },
                           Expression.Assign(value, constructor),
                           Expression.Assign(entity, entityExpr),
                           Expression.Loop(
                               Expression.Block(
                                   Expression.Assign(item, expression),
                                   Expression.Call(value, add, item),
                                   Expression.IfThen(
                                       Expression.Not(Expression.Call(reader, read)),
                                       Expression.Return(returnLabel, value))
                           )),
                           Expression.Label(returnLabel, value));
                    }
                });
                //HashSet<>
                Register((type, reader) => {
                    if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(HashSet<>))
                        return null;

                    Constructor.Register(type, out var constructor, out _);
                    if (constructor == null)
                        return Expression.Empty();
                    var eleType = type.GetGenericArguments()[0];
                    Register(eleType, reader, out var expression, out var entity, out var entityExpr);
                    if (expression == null)
                        return Expression.Empty();
                    var read = typeof(TDbDataReader).GetMethod("Read", Type.EmptyTypes);
                    var add = type.GetMethod("Add");
                    var value = Expression.Variable(type, "value");
                    var returnLabel = Expression.Label(type);
                    var item = Expression.Variable(eleType, "item");
                    if (entity == null)
                    {
                        return Expression.Block(new[] { value, item },
                            Expression.Assign(value, constructor),
                            Expression.Loop(
                                Expression.Block(
                                    Expression.Assign(item, expression),
                                    Expression.Call(value, add, item),
                                    Expression.IfThen(
                                        Expression.Not(Expression.Call(reader, read)),
                                        Expression.Return(returnLabel, value))
                                )),
                            Expression.Label(returnLabel, value));
                    }
                    else
                    {
                        return Expression.Block(new[] { entity, value, item },
                           Expression.Assign(value, constructor),
                           Expression.Assign(entity, entityExpr),
                           Expression.Loop(
                               Expression.Block(
                                   Expression.Assign(item, expression),
                                   Expression.Call(value, add, item),
                                   Expression.IfThen(
                                       Expression.Not(Expression.Call(reader, read)),
                                       Expression.Return(returnLabel, value))
                           )),
                           Expression.Label(returnLabel, value));
                    }
                });
                //IList<>
                Register((type, reader) => {
                    if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(IList<>))
                        return null;

                    Constructor.Register(type, out var constructor, out _);
                    if (constructor == null)
                        return Expression.Empty();
                    var eleType = type.GetGenericArguments()[0];
                    Register(eleType, reader, out var expression, out var entity, out var entityExpr);
                    if (expression == null)
                        return Expression.Empty();
                    var read = typeof(TDbDataReader).GetMethod("Read", Type.EmptyTypes);
                    var add = typeof(ICollection<>).MakeGenericType(eleType).GetMethod("Add");
                    var value = Expression.Variable(type, "value");
                    var returnLabel = Expression.Label(type);
                    var item = Expression.Variable(eleType, "item");
                    if (entity == null)
                    {
                        return Expression.Block(new[] { value, item },
                            Expression.Assign(value, constructor),
                            Expression.Loop(
                                Expression.Block(
                                    Expression.Assign(item, expression),
                                    Expression.Call(value, add, item),
                                    Expression.IfThen(
                                        Expression.Not(Expression.Call(reader, read)),
                                        Expression.Return(returnLabel, value))
                                )),
                            Expression.Label(returnLabel, value));
                    }
                    else
                    {
                        return Expression.Block(new[] { entity, value, item },
                           Expression.Assign(value, constructor),
                           Expression.Assign(entity, entityExpr),
                           Expression.Loop(
                               Expression.Block(
                                   Expression.Assign(item, expression),
                                   Expression.Call(value, add, item),
                                   Expression.IfThen(
                                       Expression.Not(Expression.Call(reader, read)),
                                       Expression.Return(returnLabel, value))
                           )),
                           Expression.Label(returnLabel, value));
                    }
                });
                //List<>
                Register((type, reader) => {
                    if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(List<>))
                        return null;

                    Constructor.Register(type, out var constructor, out _);
                    if (constructor == null)
                        return Expression.Empty();
                    var eleType = type.GetGenericArguments()[0];
                    Register(eleType, reader, out var expression, out var entity, out var entityExpr);
                    if (expression == null)
                        return Expression.Empty();
                    var read = typeof(TDbDataReader).GetMethod("Read", Type.EmptyTypes);
                    var add = type.GetMethod("Add");
                    var value = Expression.Variable(type, "value");
                    var returnLabel = Expression.Label(type);
                    var item = Expression.Variable(eleType, "item");
                    if (entity == null)
                    {
                        return Expression.Block(new[] { value, item },
                            Expression.Assign(value, constructor),
                            Expression.Loop(
                                Expression.Block(
                                    Expression.Assign(item, expression),
                                    Expression.Call(value, add, item),
                                    Expression.IfThen(
                                        Expression.Not(Expression.Call(reader, read)),
                                        Expression.Return(returnLabel, value))
                                )),
                            Expression.Label(returnLabel, value));
                    }
                    else
                    {
                        return Expression.Block(new[] { entity, value, item },
                           Expression.Assign(value, constructor),
                           Expression.Assign(entity, entityExpr),
                           Expression.Loop(
                               Expression.Block(
                                   Expression.Assign(item, expression),
                                   Expression.Call(value, add, item),
                                   Expression.IfThen(
                                       Expression.Not(Expression.Call(reader, read)),
                                       Expression.Return(returnLabel, value))
                           )),
                           Expression.Label(returnLabel, value));
                    }
                });
                //Queue<>
                Register((type, reader) => {
                    if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(Queue<>))
                        return null;

                    Constructor.Register(type, out var constructor, out _);
                    if (constructor == null)
                        return Expression.Empty();
                    var eleType = type.GetGenericArguments()[0];
                    Register(eleType, reader, out var expression, out var entity, out var entityExpr);
                    if (expression == null)
                        return Expression.Empty();
                    var read = typeof(TDbDataReader).GetMethod("Read", Type.EmptyTypes);
                    var enqueue = type.GetMethod("Enqueue");
                    var value = Expression.Variable(type, "value");
                    var returnLabel = Expression.Label(type);
                    var item = Expression.Variable(eleType, "item");
                    if (entity == null)
                    {
                        return Expression.Block(new[] { value, item },
                            Expression.Assign(value, constructor),
                            Expression.Loop(
                                Expression.Block(
                                    Expression.Assign(item, expression),
                                    Expression.Call(value, enqueue, item),
                                    Expression.IfThen(
                                        Expression.Not(Expression.Call(reader, read)),
                                        Expression.Return(returnLabel, value))
                                )),
                            Expression.Label(returnLabel, value));
                    }
                    else
                    {
                        return Expression.Block(new[] { entity, value, item },
                           Expression.Assign(value, constructor),
                           Expression.Assign(entity, entityExpr),
                           Expression.Loop(
                               Expression.Block(
                                   Expression.Assign(item, expression),
                                   Expression.Call(value, enqueue, item),
                                   Expression.IfThen(
                                       Expression.Not(Expression.Call(reader, read)),
                                       Expression.Return(returnLabel, value))
                           )),
                           Expression.Label(returnLabel, value));
                    }
                });
                //Stack<>
                Register((type, reader) => {
                    if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(Stack<>))
                        return null;

                    Constructor.Register(type, out var constructor, out _);
                    if (constructor == null)
                        return Expression.Empty();
                    var eleType = type.GetGenericArguments()[0];
                    Register(eleType, reader, out var expression, out var entity, out var entityExpr);
                    if (expression == null)
                        return Expression.Empty();
                    var read = typeof(TDbDataReader).GetMethod("Read", Type.EmptyTypes);
                    var push = type.GetMethod("Push");
                    var value = Expression.Variable(type, "value");
                    var returnLabel = Expression.Label(type);
                    var item = Expression.Variable(eleType, "item");
                    if (entity == null)
                    {
                        return Expression.Block(new[] { value, item },
                            Expression.Assign(value, constructor),
                            Expression.Loop(
                                Expression.Block(
                                    Expression.Assign(item, expression),
                                    Expression.Call(value, push, item),
                                    Expression.IfThen(
                                        Expression.Not(Expression.Call(reader, read)),
                                        Expression.Return(returnLabel, value))
                                )),
                            Expression.Label(returnLabel, value));
                    }
                    else
                    {
                        return Expression.Block(new[] { entity, value, item },
                           Expression.Assign(value, constructor),
                           Expression.Assign(entity, entityExpr),
                           Expression.Loop(
                               Expression.Block(
                                   Expression.Assign(item, expression),
                                   Expression.Call(value, push, item),
                                   Expression.IfThen(
                                       Expression.Not(Expression.Call(reader, read)),
                                       Expression.Return(returnLabel, value))
                           )),
                           Expression.Label(returnLabel, value));
                    }
                });
                //Array
                Register((type, reader) => {
                    if (!type.IsArray)
                        return null;

                    var eleType = type.GetElementType();
                    var listType = typeof(List<>).MakeGenericType(eleType);
                    Register(listType, reader, out var expression, out _);
                    if (expression == null)
                        return Expression.Empty();
                    var list = Expression.Variable(listType, "list");
                    return Expression.Block(new[] { list },
                        Expression.Assign(list, expression),
                        Expression.Condition(
                            Expression.Equal(list, Expression.Constant(null)),
                            Expression.Default(type),
                            Expression.Call(list, listType.GetMethod("ToArray", Type.EmptyTypes))
                            )
                        );
                });
                //ValueTuple
                Register((type, reader) => {
                    if (!type.IsGenericType)
                        return null;
                    var typeDefinition = type.GetGenericTypeDefinition();
                    if (typeDefinition == typeof(ValueTuple<>) || typeDefinition == typeof(ValueTuple<,>)
                    || typeDefinition == typeof(ValueTuple<,,>) || typeDefinition == typeof(ValueTuple<,,,>)
                    || typeDefinition == typeof(ValueTuple<,,,,>) || typeDefinition == typeof(ValueTuple<,,,,,>)
                    || typeDefinition == typeof(ValueTuple<,,,,,,>) || typeDefinition == typeof(ValueTuple<,,,,,,,>))
                    {
                        var eleTypes = type.GetGenericArguments();
                        var ctor = type.GetConstructor(eleTypes);
                        var indexs = new ParameterExpression[eleTypes.Length];
                        var exprs = new Expression[eleTypes.Length];
                        var block = new Expression[eleTypes.Length + 1];
                        for (int i = 0; i < eleTypes.Length; i++)
                        {
                            indexs[i] = Expression.Variable(typeof(int), $"i{i}");
                            Register(eleTypes[i], reader, indexs[i], out var expression);
                            if (expression == null)
                                return Expression.Empty();
                            exprs[i] = expression;
                            block[i] = Expression.Assign(indexs[i], Expression.Constant(i));
                        }
                        block[eleTypes.Length] = Expression.New(ctor, exprs);
                        return Expression.Block(indexs, block);
                    }
                    return null;
                });
                //Tuple
                Register((type, reader) => {
                    if (!type.IsGenericType)
                        return null;
                    var typeDefinition = type.GetGenericTypeDefinition();
                    if (typeDefinition == typeof(Tuple<>) || typeDefinition == typeof(Tuple<,>)
                    || typeDefinition == typeof(Tuple<,,>) || typeDefinition == typeof(Tuple<,,,>)
                    || typeDefinition == typeof(Tuple<,,,,>) || typeDefinition == typeof(Tuple<,,,,,>)
                    || typeDefinition == typeof(Tuple<,,,,,,>) || typeDefinition == typeof(Tuple<,,,,,,,>))
                    {
                        var eleTypes = type.GetGenericArguments();
                        var ctor = type.GetConstructor(eleTypes);
                        var indexs = new ParameterExpression[eleTypes.Length];
                        var exprs = new Expression[eleTypes.Length];
                        var block = new Expression[eleTypes.Length + 1];
                        for (int i = 0; i < eleTypes.Length; i++)
                        {
                            indexs[i] = Expression.Variable(typeof(int), $"i{i}");
                            Register(eleTypes[i], reader, indexs[i], out var expression);
                            if (expression == null)
                                return Expression.Empty();
                            exprs[i] = expression;
                            block[i] = Expression.Assign(indexs[i], Expression.Constant(i));
                        }
                        block[eleTypes.Length] = Expression.New(ctor, exprs);
                        return Expression.Block(indexs, block);
                    }
                    return null;
                });
            }
            public class DbEntity
            {
                private int[] _columns;
                private Dictionary<string, DbEntity> _entities;
                public bool TryGetColumn(int propertyIndex, out int index)
                {
                    index = _columns[propertyIndex];
                    return index != -1;
                }
                public bool TryGetEntity(string name, out DbEntity entity)
                {
                    return _entities.TryGetValue(name, out entity);
                }
                public static DbEntity Create<TEntity>(TDbDataReader reader)
                {
                    var @this = new DbEntity();
                    var columns = DbEntity<TEntity>.Columns;
                    var entities = DbEntity<TEntity>.Entities;
                    @this._columns = new int[columns.Count];
                    for (int i = 0; i < @this._columns.Length; i++)
                    {
                        @this._columns[i] = -1;
                    }
                    if (entities.Count > 0)
                        @this._entities = new Dictionary<string, DbEntity>(entities.Count);
                    var fieldCount = reader.FieldCount;
                    for (int i = 0; i < fieldCount; i++)
                    {
                        var name = reader.GetName(i);
                        if (columns.TryGetValue(name, out var index))
                        {
                            @this._columns[index] = i;
                        }
                        else
                        {
                            foreach ((var entityName, var entityPrefix, var create, var add) in entities)
                            {
                                if (name.StartsWith(entityPrefix))
                                {
                                    if (!@this._entities.TryGetValue(entityName, out var entity))
                                    {
                                        entity = create();
                                        @this._entities.Add(entityName, entity);
                                    }
                                    add(entity, name.Substring(entityPrefix.Length), i);
                                }
                            }
                        }
                    }
                    return @this;
                }
                public static DbEntity Create<TEntity>()
                {
                    var @this = new DbEntity();
                    var columns = DbEntity<TEntity>.Columns;
                    var entities = DbEntity<TEntity>.Entities;
                    @this._columns = new int[columns.Count];
                    for (int i = 0; i < @this._columns.Length; i++)
                    {
                        @this._columns[i] = -1;
                    }
                    if (entities.Count > 0)
                        @this._entities = new Dictionary<string, DbEntity>(entities.Count);
                    return @this;
                }
                public static void Add<TEntity>(DbEntity @this, string name, int i)
                {
                    var columns = DbEntity<TEntity>.Columns;
                    var entities = DbEntity<TEntity>.Entities;
                    if (columns.TryGetValue(name, out var index))
                    {
                        @this._columns[index] = i;
                    }
                    else
                    {
                        foreach ((var entityName, var entityPrefix, var create, var add) in entities)
                        {
                            if (name.StartsWith(entityPrefix))
                            {
                                if (!@this._entities.TryGetValue(entityName, out var entity))
                                {
                                    entity = create();
                                    @this._entities.Add(entityName, entity);
                                }
                                add(entity, name.Substring(entityPrefix.Length), i);
                            }
                        }
                    }
                }
            }
            public static class DbEntity<TEntity>
            {
                static DbEntity()
                {
                    Columns = new Dictionary<string, int>();
                    Entities = new List<(string, string, Func<DbEntity>, Action<DbEntity, string, int>)>();
                    var type = typeof(TEntity);
                    Constructor.Register(type, out var constructor, out _);
                    if (constructor == null)
                        return;
                    RegisterProperty(out var propertyResolver);
                    var tryGetColumn = typeof(DbEntity).GetMethod("TryGetColumn");
                    var tryGetEntity = typeof(DbEntity).GetMethod("TryGetEntity");
                    var reader = Expression.Parameter(typeof(TDbDataReader), "reader");
                    var entity = Expression.Parameter(typeof(DbEntity), "entity");
                    var value = Expression.Variable(type, "value");
                    var fieldIndex = Expression.Variable(typeof(int), "fieldIndex");
                    var variables = new List<ParameterExpression>() { value, fieldIndex };
                    var exprs = new List<Expression>() { Expression.Assign(value, constructor) };
                    var propertyIndex = 0;
                    var properties = type.GetGeneralProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    foreach (var property in properties)
                    {
                        if (!property.CanWrite)
                            continue;
                        if (property.IsDefined(typeof(IgnoreDataColumnAttribute)))
                            continue;

                        var dataColumnAttribute = property.GetCustomAttribute<DataColumnAttribute>();
                        Register(property.PropertyType, reader, fieldIndex, out var fieldExpression);
                        if (fieldExpression != null)
                        {
                            var propertyName = dataColumnAttribute?.Name ?? propertyResolver(property);
                            if (propertyName == null)
                                continue;
                            Columns.Add(propertyName, propertyIndex);
                            exprs.Add(
                               Expression.IfThen(
                                   Expression.Call(entity, tryGetColumn, Expression.Constant(propertyIndex), fieldIndex),
                                   Expression.Assign(Expression.Property(value, property), fieldExpression)));
                            propertyIndex += 1;
                        }
                        else
                        {
                            var create = typeof(DbEntity).GetMethod("Create", Type.EmptyTypes).MakeGenericMethod(property.PropertyType);
                            var add = typeof(DbEntity).GetMethod("Add").MakeGenericMethod(property.PropertyType);
                            Entities.Add((property.Name, $"{property.Name}.", create.CreateDelegate<Func<DbEntity>>(), add.CreateDelegate<Action<DbEntity, string, int>>()));
                            var innerEntity = Expression.Variable(typeof(DbEntity), $"{property.Name}Entity");
                            variables.Add(innerEntity);
                            var read = typeof(DbReader<TDbDataReader>).GetMethod("Read");
                            exprs.Add(
                                Expression.IfThen(
                                    Expression.Call(entity, tryGetEntity, Expression.Constant(property.Name), innerEntity),
                                    Expression.Assign(
                                        Expression.Property(value, property),
                                        Expression.Call(read.MakeGenericMethod(property.PropertyType), reader, innerEntity))));
                        }
                    }
                    exprs.Add(value);
                    Handler = Expression.Lambda<Func<TDbDataReader, DbEntity, TEntity>>(
                        Expression.Block(variables, exprs), reader, entity).Compile();
                }
                public static Dictionary<string, int> Columns;
                public static List<(string, string, Func<DbEntity>, Action<DbEntity, string, int>)> Entities;
                public static Func<TDbDataReader, DbEntity, TEntity> Handler;
            }
            public static TEntity Read<TEntity>(TDbDataReader reader, DbEntity entity)
            {
                var handler = DbEntity<TEntity>.Handler;
                if (handler == null)
                    return default;

                return handler(reader, entity);
            }
            public static void Register<T>(Func<TDbDataReader, int, T> handler)
            {
                Register((type, reader, i) => {
                    if (type != typeof(T))
                        return null;

                    return Expression.Invoke(Expression.Constant(handler), reader, i);
                });
            }
            public static void Register(Type type, Func<ParameterExpression, ParameterExpression, Expression> handler)
            {
                Register((_type, reader, i) => {
                    if (_type != type)
                        return null;

                    return handler(reader, i);
                });
            }
            public static void Register(Func<Type, ParameterExpression, ParameterExpression, Expression> handler)
            {
                lock (_Sync)
                {
                    _FieldHandlers.Push(handler);
                }
            }
            public static void Register(Type type, ParameterExpression reader, ParameterExpression i, out Expression expression)
            {
                expression = null;
                lock (_Sync)
                {
                    foreach (var handler in _FieldHandlers)
                    {
                        expression = handler.Invoke(type, reader, i);
                        if (expression != null)
                        {
                            if (expression.Type == typeof(void) && expression.NodeType == ExpressionType.Default)
                                expression = null;
                            return;
                        }
                    }
                }
            }
            public static void Register<T>(Func<TDbDataReader, T> handler)
            {
                lock (_Sync)
                {
                    _Handlers.Push(new Tuple<Type, Delegate>(typeof(T), handler));
                }
            }
            public static void Register(Type type, Func<ParameterExpression, Expression> handler)
            {
                Register((_type, reader) => {
                    if (_type != type)
                        return null;

                    return handler(reader);
                });
            }
            public static void Register(Func<Type, ParameterExpression, Expression> handler)
            {
                lock (_Sync)
                {
                    _Handlers.Push(handler);
                }
            }
            public static void Register(Type type, ParameterExpression reader, out Expression expression, out Delegate @delegate)
            {
                expression = null;
                @delegate = null;
                lock (_Sync)
                {
                    foreach (var handler in _Handlers)
                    {
                        if (handler is Func<Type, ParameterExpression, Expression> exprHandler)
                        {
                            expression = exprHandler.Invoke(type, reader);
                            if (expression != null)
                            {
                                if (expression.Type == typeof(void) && expression.NodeType == ExpressionType.Default)
                                    expression = null;
                                return;
                            }
                        }
                        else
                        {
                            (var _type, var _delegate) = (Tuple<Type, Delegate>)handler;
                            if (_type == type)
                            {
                                expression = Expression.Invoke(Expression.Constant(_delegate), reader);
                                @delegate = _delegate;
                                return;
                            }
                        }
                    }
                    var create = typeof(DbEntity).GetMethod("Create", new[] { typeof(TDbDataReader) });
                    var read = typeof(DbReader<TDbDataReader>).GetMethod("Read");
                    var entity = Expression.Variable(typeof(DbEntity), "entity");
                    expression = Expression.Block(new[] { entity },
                        Expression.Assign(entity, Expression.Call(create.MakeGenericMethod(type), reader)),
                        Expression.Call(read.MakeGenericMethod(type), reader, entity));
                }
            }
            public static void Register(Type type, ParameterExpression reader, out Expression expression, out ParameterExpression entity, out Expression entityExpr)
            {
                expression = null;
                entity = null;
                entityExpr = null;
                lock (_Sync)
                {
                    foreach (var handler in _Handlers)
                    {
                        if (handler is Func<Type, ParameterExpression, Expression> exprHandler)
                        {
                            expression = exprHandler.Invoke(type, reader);
                            if (expression != null)
                            {
                                if (expression.Type == typeof(void) && expression.NodeType == ExpressionType.Default)
                                    expression = null;
                                return;
                            }
                        }
                        else
                        {
                            (var _type, var _delegate) = (Tuple<Type, Delegate>)handler;
                            if (_type == type)
                            {
                                expression = Expression.Invoke(Expression.Constant(_delegate), reader);
                                return;
                            }
                        }
                    }
                    var create = typeof(DbEntity).GetMethod("Create", new[] { typeof(TDbDataReader) });
                    var read = typeof(DbReader<TDbDataReader>).GetMethod("Read");
                    entity = Expression.Variable(typeof(DbEntity), "entity");
                    entityExpr = Expression.Call(create.MakeGenericMethod(type), reader);
                    expression = Expression.Call(read.MakeGenericMethod(type), reader, entity);
                }
            }
        }
        public static void RegisterDbReader<TDbDataReader, T>(Func<TDbDataReader, int, T> handler) where TDbDataReader : DbDataReader
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            DbReader<TDbDataReader>.Register(handler);
        }
        public static void RegisterDbReader<TDbDataReader>(Type type, Func<ParameterExpression, ParameterExpression, Expression> handler) where TDbDataReader : DbDataReader
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            DbReader<TDbDataReader>.Register(type, handler);
        }
        public static void RegisterDbReader<TDbDataReader>(Func<Type, ParameterExpression, ParameterExpression, Expression> handler) where TDbDataReader : DbDataReader
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            DbReader<TDbDataReader>.Register(handler);
        }
        public static void RegisterDbReader<TDbDataReader>(Type type, ParameterExpression reader, ParameterExpression i, out Expression expression) where TDbDataReader : DbDataReader
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));
            if (i == null)
                throw new ArgumentNullException(nameof(i));

            DbReader<TDbDataReader>.Register(type, reader, i, out expression);
        }
        public static void RegisterDbReader<TDbDataReader, T>(Func<TDbDataReader, T> handler) where TDbDataReader : DbDataReader
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            DbReader<TDbDataReader>.Register(handler);
        }
        public static void RegisterDbReader<TDbDataReader>(Type type, Func<ParameterExpression, Expression> handler) where TDbDataReader : DbDataReader
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            DbReader<TDbDataReader>.Register(type, handler);
        }
        public static void RegisterDbReader<TDbDataReader>(Func<Type, ParameterExpression, Expression> handler) where TDbDataReader : DbDataReader
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            DbReader<TDbDataReader>.Register(handler);
        }
        public static void RegisterDbReader<TDbDataReader>(Type type, ParameterExpression reader, out Expression expression, out Delegate @delegate) where TDbDataReader : DbDataReader
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            DbReader<TDbDataReader>.Register(type, reader, out expression, out @delegate);
        }
        public static void RegisterDbReader<TDbDataReader>(Type type, ParameterExpression reader, out Expression expression, out ParameterExpression entity, out Expression entityExpr) where TDbDataReader : DbDataReader
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            DbReader<TDbDataReader>.Register(type, reader, out expression, out entity, out entityExpr);
        }
        private static class DbExpression<TDbConnection> where TDbConnection : DbConnection
        {
            private static object _Sync = new object();
            private static Dictionary<MemberInfo, Func<MemberExpression, IReadOnlyList<object>>> _Members;
            private static Dictionary<MethodInfo, Func<MethodCallExpression, IReadOnlyList<object>>> _Methods;
            static DbExpression()
            {
                _Members = new Dictionary<MemberInfo, Func<MemberExpression, IReadOnlyList<object>>>();
                _Methods = new Dictionary<MethodInfo, Func<MethodCallExpression, IReadOnlyList<object>>>();

                _Methods.Add(typeof(SqlExpression).GetMethod("Sql"), expr => {
                    if (TryFormat(expr.Arguments[0], out var format, out var argExprs, out var startIndex))
                    {
                        var exprObjs = new List<object>();
                        for (int i = 0, pos = 0; ;)
                        {
                            if (i == format.Length)
                            {
                                if (pos < format.Length)
                                    exprObjs.Add(format.Substring(pos));
                                break;
                            }
                            var ch = format[i++];
                            if (ch == '{')
                            {
                                exprObjs.Add(format.Substring(pos, i - pos - 1));
                                pos = i++;
                            }
                            else if (ch == '}')
                            {
                                if (i == format.Length)
                                {
                                    exprObjs.Add(argExprs[startIndex + int.Parse(format.AsSpan(pos, i - pos - 1))]);
                                    break;
                                }
                                if (i < format.Length && format[i] == '}')
                                    exprObjs.Add(format.Substring(pos, i - pos - 1));
                                else
                                    exprObjs.Add(argExprs[startIndex + int.Parse(format.AsSpan(pos, i - pos - 1))]);
                                pos = i++;
                            }
                        }
                        return exprObjs;
                    }
                    else
                    {
                        return new object[] { expr.Arguments[0].Invoke().ToString() };
                    }
                });
                _Methods.Add(typeof(SqlExpression).GetMethod("Asc"), expr => {
                    //" ASC"
                    return expr.Object.NodeType == ExpressionType.Parameter
                    ? new object[] { expr.Arguments[0] }
                    : new object[] { expr.Object, ",", expr.Arguments[0] };
                });
                _Methods.Add(typeof(SqlExpression).GetMethod("Desc"), expr => {
                    return expr.Object.NodeType == ExpressionType.Parameter
                    ? new object[] { expr.Arguments[0], " DESC" }
                    : new object[] { expr.Object, ",", expr.Arguments[0], " DESC" };
                });
                _Methods.Add(typeof(SqlExpression).GetMethod("Distinct"), expr => new object[] { "DISTINCT ", expr.Arguments[0] });
                _Methods.Add(typeof(SqlExpression).GetMethod("Max"), expr => new object[] { "MAX(", expr.Arguments[0], ")" });
                _Methods.Add(typeof(SqlExpression).GetMethod("Min"), expr => new object[] { "MIN(", expr.Arguments[0], ")" });
                _Methods.Add(typeof(SqlExpression).GetMethod("Sum"), expr => new object[] { "SUM(", expr.Arguments[0], ")" });
                _Methods.Add(typeof(SqlExpression).GetMethod("Avg"), expr => new object[] { "AVG(", expr.Arguments[0], ")" });
                _Methods.Add(typeof(SqlExpression).GetMethod("Count", Type.EmptyTypes), expr => new object[] { "COUNT(*)" });//COUNT(1)
                _Methods.Add(typeof(SqlExpression).GetMethod("Count", new[] { typeof(object) }), expr => new object[] { "COUNT(", expr.Arguments[0], ")" });
                _Methods.Add(typeof(SqlExpression).GetMethod("Exists"), expr => new object[] { "EXISTS ", expr.Arguments[0] });
                _Methods.Add(typeof(SqlExpression).GetMethod("NotExists"), expr => new object[] { "NOT EXISTS ", expr.Arguments[0] });
                _Methods.Add(typeof(SqlExpression).GetMethod("Like"), expr => new object[] { expr.Arguments[0], " LIKE ", expr.Arguments[1] });
                _Methods.Add(typeof(SqlExpression).GetMethod("NotLike"), expr => new object[] { expr.Arguments[0], " NOT LIKE ", expr.Arguments[1] });
                _Methods.Add(typeof(SqlExpression).GetMethod("In"), expr =>
                {
                    if (expr.Arguments[1].TryInvoke(out var value))
                    {
                        var inObjs = (IList)value;
                        if (inObjs == null || inObjs.Count == 0)
                            return new object[] { " 1<>1 " };
                        var exprObjs = new List<object>() { expr.Arguments[0], " IN (", Expression.Constant(inObjs[0]) };
                        for (int i = 1; i < inObjs.Count; i++)
                        {
                            exprObjs.Add(",");
                            exprObjs.Add(Expression.Constant(inObjs[i]));
                        }
                        exprObjs.Add(")");
                        return exprObjs;
                    }
                    else
                    {
                        return new object[] { expr.Arguments[0], " IN (", expr.Arguments[1], ")" };
                    }
                });
                _Methods.Add(typeof(SqlExpression).GetMethod("NotIn"), expr =>
                {
                    if (expr.Arguments[1].TryInvoke(out var value))
                    {
                        var inObjs = (IList)value;
                        if (inObjs == null || inObjs.Count == 0)
                            return new object[] { " 1=1 " };
                        var exprObjs = new List<object>() { expr.Arguments[0], " NOT IN (", Expression.Constant(inObjs[0]) };
                        for (int i = 1; i < inObjs.Count; i++)
                        {
                            exprObjs.Add(",");
                            exprObjs.Add(Expression.Constant(inObjs[i]));
                        }
                        exprObjs.Add(")");
                        return exprObjs;
                    }
                    else
                    {
                        return new object[] { expr.Arguments[0], " NOT IN (", expr.Arguments[1], ")" };
                    }
                });
                _Methods.Add(typeof(SqlExpression).GetMethod("Between"), expr => new object[] { expr.Arguments[0], " BETWEEN ", expr.Arguments[1], " AND ", expr.Arguments[2] });
                _Methods.Add(typeof(SqlExpression).GetMethod("NotBetween"), expr => new object[] { expr.Arguments[0], " NOT BETWEEN ", expr.Arguments[1], " AND ", expr.Arguments[2] });
                _Methods.Add(typeof(SqlExpression).GetMethod("Equals", new[] { typeof(object), typeof(object) }), expr =>
                {
                    var exprObjs = new List<object>();
                    exprObjs.Add(expr.Arguments[0]);
                    if (expr.Arguments[1].TryInvoke(out var value))
                    {
                        if (value == null)
                        {
                            exprObjs.Add(" IS NULL");
                        }
                        else
                        {
                            exprObjs.Add(" = ");
                            exprObjs.Add(Expression.Constant(value));
                        }
                    }
                    else
                    {
                        exprObjs.Add(" = ");
                        exprObjs.Add(expr.Arguments[1]);
                    }
                    return exprObjs;
                });
                _Methods.Add(typeof(SqlExpression).GetMethod("NotEquals", new[] { typeof(object), typeof(object) }), expr =>
                {
                    var exprObjs = new List<object>();
                    exprObjs.Add(expr.Arguments[0]);
                    if (expr.Arguments[1].TryInvoke(out var value))
                    {
                        if (value == null)
                        {
                            exprObjs.Add(" IS NOT NULL");
                        }
                        else
                        {
                            exprObjs.Add(" <> ");
                            exprObjs.Add(Expression.Constant(value));
                        }
                    }
                    else
                    {
                        exprObjs.Add(" <> ");
                        exprObjs.Add(expr.Arguments[1]);
                    }
                    return exprObjs;
                });

                _Members.Add(typeof(DateTime?).GetProperty("Value"), expr => new object[] { expr.Expression });
                _Members.Add(typeof(DateTimeOffset?).GetProperty("Value"), expr => new object[] { expr.Expression });
                _Methods.Add(typeof(string).GetMethod("IsNullOrEmpty"), expr => new object[] { "(", expr.Arguments[0], " IS NULL OR ", expr.Arguments[0], " = '')" });
                _Methods.Add(typeof(string).GetMethod("ToLower", Type.EmptyTypes), expr => new object[] { "LOWER(", expr.Object, ")" });
                _Methods.Add(typeof(string).GetMethod("ToUpper", Type.EmptyTypes), expr => new object[] { "UPPER(", expr.Object, ")" });
                _Methods.Add(typeof(string).GetMethod("Trim", Type.EmptyTypes), expr => new object[] { "TRIM(", expr.Object, ")" });
                _Methods.Add(typeof(string).GetMethod("TrimStart", Type.EmptyTypes), expr => new object[] { "LTRIM(", expr.Object, ")" });
                _Methods.Add(typeof(string).GetMethod("TrimEnd", Type.EmptyTypes), expr => new object[] { "RTRIM(", expr.Object, ")" });
                _Methods.Add(typeof(string).GetMethod("Replace", new[] { typeof(string), typeof(string) }), expr => new object[] { "REPLACE(", expr.Object, ",", expr.Arguments[0], ",", expr.Arguments[1], ")" });
                //_Methods.Add(typeof(string).GetMethod("Replace", new[] { typeof(char), typeof(char) }), expr => new object[] { "REPLACE(", expr.Object, ",", expr.Arguments[0], ",", expr.Arguments[1], ")" });
                var cast = typeof(SqlDbExtensions).GetMethod("Cast", new[] { typeof(SqlExpression), typeof(object) });
                switch (typeof(TDbConnection).Name)
                {
                    case "SqlConnection":
                        {
                            _Members.Add(typeof(string).GetProperty("Length"), expr => new object[] { "LEN(", expr.Expression, ")" });
                            _Members.Add(typeof(DateTime).GetProperty("Now"), expr => new object[] { "GETDATE()" });
                            _Members.Add(typeof(DateTime).GetProperty("UtcNow"), expr => new object[] { "GETUTCDATE()" });
                            _Members.Add(typeof(DateTime).GetProperty("Today"), expr => new object[] { "CAST(GETDATE() AS DATE)" });
                            _Members.Add(typeof(DateTime).GetProperty("Date"), expr => new object[] { "CAST(", expr.Expression, " AS DATE)" });
                            _Members.Add(typeof(DateTime).GetProperty("Year"), expr => new object[] { "DATEPART(YEAR,", expr.Expression, ")" });
                            _Members.Add(typeof(DateTime).GetProperty("Month"), expr => new object[] { "DATEPART(MONTH,", expr.Expression, ")" });
                            _Members.Add(typeof(DateTime).GetProperty("Day"), expr => new object[] { "DATEPART(DAY,", expr.Expression, ")" });

                            _Methods.Add(cast.MakeGenericMethod(typeof(int)), expr => new object[] { "CAST(", expr.Arguments[1], " AS INT)" });
                            _Methods.Add(cast.MakeGenericMethod(typeof(long)), expr => new object[] { "CAST(", expr.Arguments[1], " AS BIGINT)" });
                            _Methods.Add(cast.MakeGenericMethod(typeof(double)), expr => new object[] { "CAST(", expr.Arguments[1], " AS FLOAT)" });
                            _Methods.Add(cast.MakeGenericMethod(typeof(decimal)), expr => new object[] { "CAST(", expr.Arguments[1], " AS DECIMAL)" });
                            _Methods.Add(cast.MakeGenericMethod(typeof(string)), expr => new object[] { "CAST(", expr.Arguments[1], " AS NVARCHAR)" });
                            _Methods.Add(cast.MakeGenericMethod(typeof(DateTime)), expr => new object[] { "CAST(", expr.Arguments[1], " AS DATETIME)" });

                            _Methods.Add(typeof(string).GetMethod("Concat", new[] { typeof(string), typeof(string) }), expr => new object[] { expr.Arguments[0], "+", expr.Arguments[1] });
                            _Methods.Add(typeof(string).GetMethod("Concat", new[] { typeof(string), typeof(string), typeof(string) }), expr => new object[] { expr.Arguments[0], "+", expr.Arguments[1], "+", expr.Arguments[2] });
                            _Methods.Add(typeof(string).GetMethod("Concat", new[] { typeof(string), typeof(string), typeof(string), typeof(string) }), expr => new object[] { expr.Arguments[0], "+", expr.Arguments[1], "+", expr.Arguments[2], "+", expr.Arguments[3] });
                            _Methods.Add(typeof(string).GetMethod("IndexOf", new[] { typeof(string) }), expr => new object[] { "CHARINDEX(", expr.Arguments[0], ",", expr.Object, ")-1" });
                            _Methods.Add(typeof(string).GetMethod("Contains", new[] { typeof(string) }), expr => new object[] { "CHARINDEX(", expr.Arguments[0], ",", expr.Object, ")>0" });
                            _Methods.Add(typeof(string).GetMethod("Substring", new[] { typeof(int) }), expr => new object[] { "SUBSTRING(", expr.Object, ",", expr.Arguments[0], "+1,LEN(", expr.Object, "))" });
                            _Methods.Add(typeof(string).GetMethod("Substring", new[] { typeof(int), typeof(int) }), expr => new object[] { "SUBSTRING(", expr.Object, ",", expr.Arguments[0], "+1,", expr.Arguments[1], ")" });
                            _Methods.Add(typeof(string).GetMethod("StartsWith", new[] { typeof(string) }), expr => new object[] { "SUBSTRING(", expr.Object, ",1,LEN(", expr.Arguments[0], "))=", expr.Arguments[0] });
                            _Methods.Add(typeof(string).GetMethod("EndsWith", new[] { typeof(string) }), expr => new object[] { "SUBSTRING(", expr.Object, ",LEN(", expr.Object, ")-LEN(", expr.Arguments[0], ")+1,LEN(", expr.Arguments[0], "))=", expr.Arguments[0] });
                        }
                        break;
                    case "SQLiteConnection":
                    case "SqliteConnection":
                        {
                            _Members.Add(typeof(string).GetProperty("Length"), expr => new object[] { "LENGTH(", expr.Expression, ")" });
                            _Members.Add(typeof(DateTime).GetProperty("Now"), expr => new object[] { "DATETIME('NOW','LOCALTIME')" });
                            _Members.Add(typeof(DateTime).GetProperty("UtcNow"), expr => new object[] { "DATETIME()" });
                            _Members.Add(typeof(DateTime).GetProperty("Today"), expr => new object[] { "DATE('NOW','LOCALTIME')" });
                            _Members.Add(typeof(DateTime).GetProperty("Date"), expr => new object[] { "DATE(", expr.Expression, ",'LOCALTIME')" });
                            _Members.Add(typeof(DateTime).GetProperty("Year"), expr => new object[] { "CAST(STRFTIME('%Y',", expr.Expression, ") AS INTEGER)" });
                            _Members.Add(typeof(DateTime).GetProperty("Month"), expr => new object[] { "CAST(STRFTIME('%m',", expr.Expression, ") AS INTEGER)" });
                            _Members.Add(typeof(DateTime).GetProperty("Day"), expr => new object[] { "CAST(STRFTIME('%d',", expr.Expression, ") AS INTEGER)" });

                            _Methods.Add(cast.MakeGenericMethod(typeof(int)), expr => new object[] { "CAST(", expr.Arguments[1], " AS INTEGER)" });
                            _Methods.Add(cast.MakeGenericMethod(typeof(long)), expr => new object[] { "CAST(", expr.Arguments[1], " AS INTEGER)" });
                            _Methods.Add(cast.MakeGenericMethod(typeof(double)), expr => new object[] { "CAST(", expr.Arguments[1], " AS DOUBLE)" });
                            _Methods.Add(cast.MakeGenericMethod(typeof(decimal)), expr => new object[] { "CAST(", expr.Arguments[1], " AS DECIMAL)" });
                            _Methods.Add(cast.MakeGenericMethod(typeof(string)), expr => new object[] { "CAST(", expr.Arguments[1], " AS CHARACTER)" });
                            _Methods.Add(cast.MakeGenericMethod(typeof(DateTime)), expr => new object[] { "DATETIME(", expr.Arguments[1], ")" });

                            _Methods.Add(typeof(string).GetMethod("Concat", new[] { typeof(string), typeof(string) }), expr => new object[] { expr.Arguments[0], "||", expr.Arguments[1] });
                            _Methods.Add(typeof(string).GetMethod("Concat", new[] { typeof(string), typeof(string), typeof(string) }), expr => new object[] { expr.Arguments[0], "||", expr.Arguments[1], "||", expr.Arguments[2] });
                            _Methods.Add(typeof(string).GetMethod("Concat", new[] { typeof(string), typeof(string), typeof(string), typeof(string) }), expr => new object[] { expr.Arguments[0], "||", expr.Arguments[1], "||", expr.Arguments[2], "||", expr.Arguments[3] });
                            _Methods.Add(typeof(string).GetMethod("IndexOf", new[] { typeof(string) }), expr => new object[] { "INSTR(", expr.Object, ",", expr.Arguments[0], ")-1" });
                            _Methods.Add(typeof(string).GetMethod("Contains", new[] { typeof(string) }), expr => new object[] { "INSTR(", expr.Object, ",", expr.Arguments[0], ")>0" });
                            _Methods.Add(typeof(string).GetMethod("Substring", new[] { typeof(int) }), expr => new object[] { "SUBSTR(", expr.Object, ",", expr.Arguments[0], "+1)" });
                            _Methods.Add(typeof(string).GetMethod("Substring", new[] { typeof(int), typeof(int) }), expr => new object[] { "SUBSTR(", expr.Object, ",", expr.Arguments[0], "+1,", expr.Arguments[1], ")" });
                            _Methods.Add(typeof(string).GetMethod("StartsWith", new[] { typeof(string) }), expr => new object[] { "SUBSTR(", expr.Object, ",1,LENGTH(", expr.Arguments[0], "))=", expr.Arguments[0] });
                            _Methods.Add(typeof(string).GetMethod("EndsWith", new[] { typeof(string) }), expr => new object[] { "SUBSTR(", expr.Object, ",LENGTH(", expr.Object, ")-LENGTH(", expr.Arguments[0], ")+1,LENGTH(", expr.Arguments[0], "))=", expr.Arguments[0] });
                        }
                        break;
                    case "OracleConnection":
                        {
                            _Members.Add(typeof(string).GetProperty("Length"), expr => new object[] { "LENGTH(", expr.Expression, ")" });
                            _Members.Add(typeof(DateTime).GetProperty("Now"), expr => new object[] { "SYSTIMESTAMP" });
                            _Members.Add(typeof(DateTime).GetProperty("UtcNow"), expr => new object[] { "SYS_EXTRACT_UTC(SYSTIMESTAMP)" });
                            _Members.Add(typeof(DateTime).GetProperty("Today"), expr => new object[] { "TRUNC(SYSDATE,'DD')" });
                            _Members.Add(typeof(DateTime).GetProperty("Date"), expr => new object[] { "TRUNC(", expr.Expression, ",'DD')" });
                            _Members.Add(typeof(DateTime).GetProperty("Year"), expr => new object[] { "CAST(TO_CHAR(", expr.Expression, ",'yyyy') AS NUMBER)" });
                            _Members.Add(typeof(DateTime).GetProperty("Month"), expr => new object[] { "CAST(TO_CHAR(", expr.Expression, ",'mm') AS NUMBER)" });
                            _Members.Add(typeof(DateTime).GetProperty("Day"), expr => new object[] { "CAST(TO_CHAR(", expr.Expression, ",'dd') AS NUMBER)" });

                            _Methods.Add(cast.MakeGenericMethod(typeof(int)), expr => new object[] { "CAST(", expr.Arguments[1], " AS NUMBER)" });
                            _Methods.Add(cast.MakeGenericMethod(typeof(long)), expr => new object[] { "CAST(", expr.Arguments[1], " AS NUMBER)" });
                            _Methods.Add(cast.MakeGenericMethod(typeof(double)), expr => new object[] { "CAST(", expr.Arguments[1], " AS NUMBER)" });
                            _Methods.Add(cast.MakeGenericMethod(typeof(decimal)), expr => new object[] { "CAST(", expr.Arguments[1], " AS NUMBER)" });
                            _Methods.Add(cast.MakeGenericMethod(typeof(string)), expr => new object[] { "TO_CHAR(", expr.Arguments[1], ")" });
                            var parseExact = typeof(DateTime).GetMethod("ParseExact", new[] { typeof(string), typeof(string), typeof(IFormatProvider) });
                            _Methods.Add(parseExact, expr => new object[] { "TO_DATE(", expr.Arguments[0], ",", expr.Arguments[1], ")" });

                            _Methods.Add(typeof(string).GetMethod("Concat", new[] { typeof(string), typeof(string) }), expr => new object[] { expr.Arguments[0], "||", expr.Arguments[1] });
                            _Methods.Add(typeof(string).GetMethod("Concat", new[] { typeof(string), typeof(string), typeof(string) }), expr => new object[] { expr.Arguments[0], "||", expr.Arguments[1], "||", expr.Arguments[2] });
                            _Methods.Add(typeof(string).GetMethod("Concat", new[] { typeof(string), typeof(string), typeof(string), typeof(string) }), expr => new object[] { expr.Arguments[0], "||", expr.Arguments[1], "||", expr.Arguments[2], "||", expr.Arguments[3] });
                            _Methods.Add(typeof(string).GetMethod("IndexOf", new[] { typeof(string) }), expr => new object[] { "INSTR(", expr.Object, ",", expr.Arguments[0], ")-1" });
                            _Methods.Add(typeof(string).GetMethod("Contains", new[] { typeof(string) }), expr => new object[] { "INSTR(", expr.Object, ",", expr.Arguments[0], ")>0" });
                            _Methods.Add(typeof(string).GetMethod("Substring", new[] { typeof(int) }), expr => new object[] { "SUBSTR(", expr.Object, ",", expr.Arguments[0], "+1,LENGTH(", expr.Object, "))" });
                            _Methods.Add(typeof(string).GetMethod("Substring", new[] { typeof(int), typeof(int) }), expr => new object[] { "SUBSTR(", expr.Object, ",", expr.Arguments[0], "+1,", expr.Arguments[1], ")" });
                            _Methods.Add(typeof(string).GetMethod("StartsWith", new[] { typeof(string) }), expr => new object[] { "SUBSTR(", expr.Object, ",1,LENGTH(", expr.Arguments[0], "))=", expr.Arguments[0] });
                            _Methods.Add(typeof(string).GetMethod("EndsWith", new[] { typeof(string) }), expr => new object[] { "SUBSTR(", expr.Object, ",LENGTH(", expr.Object, ")-LENGTH(", expr.Arguments[0], ")+1,LENGTH(", expr.Arguments[0], "))=", expr.Arguments[0] });
                        }
                        break;
                    case "NpgsqlConnection":
                        {
                            _Members.Add(typeof(string).GetProperty("Length"), expr => new object[] { "CHAR_LENGTH(", expr.Expression, ")" });
                            _Members.Add(typeof(DateTime).GetProperty("Now"), expr => new object[] { "NOW()" });
                            _Members.Add(typeof(DateTime).GetProperty("UtcNow"), expr => new object[] { "NOW() AT TIME ZONE 'UTC'" });
                            _Members.Add(typeof(DateTime).GetProperty("Today"), expr => new object[] { "CAST(NOW() AS DATE)" });
                            _Members.Add(typeof(DateTime).GetProperty("Date"), expr => new object[] { "CAST(", expr.Expression, " AS DATE)" });
                            _Members.Add(typeof(DateTime).GetProperty("Year"), expr => new object[] { "CAST(DATE_PART('YEAR',", expr.Expression, ") AS INTEGER)" });
                            _Members.Add(typeof(DateTime).GetProperty("Month"), expr => new object[] { "CAST(DATE_PART('MONTH',", expr.Expression, ") AS INTEGER)" });
                            _Members.Add(typeof(DateTime).GetProperty("Day"), expr => new object[] { "CAST(DATE_PART('DAY',", expr.Expression, ") AS INTEGER)" });

                            _Methods.Add(cast.MakeGenericMethod(typeof(int)), expr => new object[] { expr.Arguments[1], "::INTEGER" });
                            _Methods.Add(cast.MakeGenericMethod(typeof(long)), expr => new object[] { expr.Arguments[1], "::INTEGER" });
                            _Methods.Add(cast.MakeGenericMethod(typeof(double)), expr => new object[] { expr.Arguments[1], "::FLOAT8" });
                            _Methods.Add(cast.MakeGenericMethod(typeof(decimal)), expr => new object[] { expr.Arguments[1], "::NUMERIC" });
                            _Methods.Add(cast.MakeGenericMethod(typeof(string)), expr => new object[] { expr.Arguments[1], "::VARCHAR" });
                            _Methods.Add(cast.MakeGenericMethod(typeof(DateTime)), expr => new object[] { expr.Arguments[1], "::TIMESTAMP" });

                            _Methods.Add(typeof(string).GetMethod("Concat", new[] { typeof(string), typeof(string) }), expr => new object[] { expr.Arguments[0], "||", expr.Arguments[1] });
                            _Methods.Add(typeof(string).GetMethod("Concat", new[] { typeof(string), typeof(string), typeof(string) }), expr => new object[] { expr.Arguments[0], "||", expr.Arguments[1], "||", expr.Arguments[2] });
                            _Methods.Add(typeof(string).GetMethod("Concat", new[] { typeof(string), typeof(string), typeof(string), typeof(string) }), expr => new object[] { expr.Arguments[0], "||", expr.Arguments[1], "||", expr.Arguments[2], "||", expr.Arguments[3] });
                            _Methods.Add(typeof(string).GetMethod("IndexOf", new[] { typeof(string) }), expr => new object[] { "POSITION(", expr.Arguments[0], " IN ", expr.Object, ")-1" });
                            _Methods.Add(typeof(string).GetMethod("Contains", new[] { typeof(string) }), expr => new object[] { "POSITION(", expr.Arguments[0], " IN ", expr.Object, ")>0" });
                            _Methods.Add(typeof(string).GetMethod("Substring", new[] { typeof(int) }), expr => new object[] { "SUBSTRING(", expr.Object, " FROM ", expr.Arguments[0], "+1)" });
                            _Methods.Add(typeof(string).GetMethod("Substring", new[] { typeof(int), typeof(int) }), expr => new object[] { "SUBSTRING(", expr.Object, " FROM ", expr.Arguments[0], "+1 FOR ", expr.Arguments[1], ")" });
                            _Methods.Add(typeof(string).GetMethod("StartsWith", new[] { typeof(string) }), expr => new object[] { "SUBSTRING(", expr.Object, ",1,CHAR_LENGTH(", expr.Arguments[0], "))=", expr.Arguments[0] });
                            _Methods.Add(typeof(string).GetMethod("EndsWith", new[] { typeof(string) }), expr => new object[] { "SUBSTRING(", expr.Object, ",CHAR_LENGTH(", expr.Object, ")-CHAR_LENGTH(", expr.Arguments[0], ")+1,CHAR_LENGTH(", expr.Arguments[0], "))=", expr.Arguments[0] });
                        }
                        break;
                    case "MySqlConnection":
                        {
                            _Members.Add(typeof(string).GetProperty("Length"), expr => new object[] { "CHAR_LENGTH(", expr.Expression, ")" });
                            _Members.Add(typeof(DateTime).GetProperty("Now"), expr => new object[] { "NOW()" });
                            _Members.Add(typeof(DateTime).GetProperty("UtcNow"), expr => new object[] { "UTC_TIMESTAMP()" });
                            _Members.Add(typeof(DateTime).GetProperty("Today"), expr => new object[] { "CURDATE()" });
                            _Members.Add(typeof(DateTime).GetProperty("Date"), expr => new object[] { "DATE(", expr.Expression, ")" });
                            _Members.Add(typeof(DateTime).GetProperty("Year"), expr => new object[] { "YEAR(", expr.Expression, ")" });
                            _Members.Add(typeof(DateTime).GetProperty("Month"), expr => new object[] { "MONTH(", expr.Expression, ")" });
                            _Members.Add(typeof(DateTime).GetProperty("Day"), expr => new object[] { "DAY(", expr.Expression, ")" });

                            _Methods.Add(cast.MakeGenericMethod(typeof(int)), expr => new object[] { "CAST(", expr.Arguments[1], " AS SIGNED)" });
                            _Methods.Add(cast.MakeGenericMethod(typeof(long)), expr => new object[] { "CAST(", expr.Arguments[1], " AS SIGNED)" });
                            _Methods.Add(cast.MakeGenericMethod(typeof(double)), expr => new object[] { "CAST(", expr.Arguments[1], " AS DECIMAL)" });
                            _Methods.Add(cast.MakeGenericMethod(typeof(decimal)), expr => new object[] { "CAST(", expr.Arguments[1], " AS DECIMAL)" });
                            _Methods.Add(cast.MakeGenericMethod(typeof(string)), expr => new object[] { "CAST(", expr.Arguments[1], " AS CHAR)" });
                            _Methods.Add(cast.MakeGenericMethod(typeof(DateTime)), expr => new object[] { "CAST(", expr.Arguments[1], " AS DATETIME)" });

                            _Methods.Add(typeof(string).GetMethod("Concat", new[] { typeof(string), typeof(string) }), expr => new object[] { "CONCAT(", expr.Arguments[0], ",", expr.Arguments[1], ")" });
                            _Methods.Add(typeof(string).GetMethod("Concat", new[] { typeof(string), typeof(string), typeof(string) }), expr => new object[] { "CONCAT(", expr.Arguments[0], ",", expr.Arguments[1], ",", expr.Arguments[2], ")" });
                            _Methods.Add(typeof(string).GetMethod("Concat", new[] { typeof(string), typeof(string), typeof(string), typeof(string) }), expr => new object[] { "CONCAT(", expr.Arguments[0], ",", expr.Arguments[1], ",", expr.Arguments[2], ",", expr.Arguments[3], ")" });
                            _Methods.Add(typeof(string).GetMethod("IndexOf", new[] { typeof(string) }), expr => new object[] { "INSTR(", expr.Object, ",", expr.Arguments[0], ")-1" });
                            _Methods.Add(typeof(string).GetMethod("Contains", new[] { typeof(string) }), expr => new object[] { "INSTR(", expr.Object, ",", expr.Arguments[0], ")>0" });
                            _Methods.Add(typeof(string).GetMethod("Substring", new[] { typeof(int) }), expr => new object[] { "SUBSTRING(", expr.Object, ",", expr.Arguments[0], "+1,CHAR_LENGTH(", expr.Object, "))" });
                            _Methods.Add(typeof(string).GetMethod("Substring", new[] { typeof(int), typeof(int) }), expr => new object[] { "SUBSTRING(", expr.Object, ",", expr.Arguments[0], "+1,", expr.Arguments[1], ")" });
                            _Methods.Add(typeof(string).GetMethod("StartsWith", new[] { typeof(string) }), expr => new object[] { "SUBSTRING(", expr.Object, ",1,CHAR_LENGTH(", expr.Arguments[0], "))=", expr.Arguments[0] });
                            _Methods.Add(typeof(string).GetMethod("EndsWith", new[] { typeof(string) }), expr => new object[] { "SUBSTRING(", expr.Object, ",CHAR_LENGTH(", expr.Object, ")-CHAR_LENGTH(", expr.Arguments[0], ")+1,CHAR_LENGTH(", expr.Arguments[0], "))=", expr.Arguments[0] });
                        }
                        break;
                    default:
                        break;
                }
            }
            public static void Register(Action<Dictionary<MemberInfo, Func<MemberExpression, IReadOnlyList<object>>>> handler)
            {
                lock (_Sync)
                {
                    var members = new Dictionary<MemberInfo, Func<MemberExpression, IReadOnlyList<object>>>(_Members);
                    handler(members);
                    _Members = members;
                }
            }
            public static void Register(MemberInfo member, out Func<MemberExpression, IReadOnlyList<object>> handler)
            {
                _Members.TryGetValue(member, out handler);
            }
            public static void Register(MemberExpression expression, out IReadOnlyList<object> exprObjs)
            {
                _Members.TryGetValue(expression.Member, out var handler);
                exprObjs = handler?.Invoke(expression);
            }
            public static void Register(Action<Dictionary<MethodInfo, Func<MethodCallExpression, IReadOnlyList<object>>>> handler)
            {
                lock (_Sync)
                {
                    var methods = new Dictionary<MethodInfo, Func<MethodCallExpression, IReadOnlyList<object>>>(_Methods);
                    handler(methods);
                    _Methods = methods;
                }
            }
            public static void Register(MethodInfo method, out Func<MethodCallExpression, IReadOnlyList<object>> handler)
            {
                _Methods.TryGetValue(method, out handler);
            }
            public static void Register(MethodCallExpression expression, out IReadOnlyList<object> exprObjs)
            {
                var method = expression.Method;
                var methods = _Methods;
                methods.TryGetValue(method, out var handler);
                if (handler != null)
                {
                    exprObjs = handler.Invoke(expression);
                    return;
                }
                if (method.IsGenericMethod)
                {
                    methods.TryGetValue(method.GetGenericMethodDefinition(), out handler);
                }
                exprObjs = handler?.Invoke(expression);
            }
        }
        public static void RegisterDbMember<TDbConnection>(Action<Dictionary<MemberInfo, Func<MemberExpression, IReadOnlyList<object>>>> handler) where TDbConnection : DbConnection
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            DbExpression<TDbConnection>.Register(handler);
        }
        public static void RegisterDbMember<TDbConnection>(MemberInfo member, out Func<MemberExpression, IReadOnlyList<object>> handler) where TDbConnection : DbConnection
        {
            if (member == null)
                throw new ArgumentNullException(nameof(member));

            DbExpression<TDbConnection>.Register(member, out handler);
        }
        public static void RegisterDbMember<TDbConnection>(MemberExpression expression, out IReadOnlyList<object> exprObjs) where TDbConnection : DbConnection
        {
            if (expression == null)
                throw new ArgumentNullException(nameof(expression));

            DbExpression<TDbConnection>.Register(expression, out exprObjs);
        }
        public static void RegisterDbMethod<TDbConnection>(Action<Dictionary<MethodInfo, Func<MethodCallExpression, IReadOnlyList<object>>>> handler) where TDbConnection : DbConnection
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            DbExpression<TDbConnection>.Register(handler);
        }
        public static void RegisterDbMethod<TDbConnection>(MethodInfo method, out Func<MethodCallExpression, IReadOnlyList<object>> handler) where TDbConnection : DbConnection
        {
            if (method == null)
                throw new ArgumentNullException(nameof(method));

            DbExpression<TDbConnection>.Register(method, out handler);
        }
        public static void RegisterDbMethod<TDbConnection>(MethodCallExpression expression, out IReadOnlyList<object> exprObjs) where TDbConnection : DbConnection
        {
            if (expression == null)
                throw new ArgumentNullException(nameof(expression));

            DbExpression<TDbConnection>.Register(expression, out exprObjs);
        }
        private static class DbParameter<TDbConnection> where TDbConnection : DbConnection
        {
            static DbParameter()
            {
                _Handlers = new Stack<object>();
                _ObjHandlers = new Dictionary<Type, Action<DbCommand, string, object>>();

                //DbParameter
                Register((type, cmd, name, value) => {
                    if (!typeof(DbParameter).IsAssignableFrom(type))
                        return null;

                    var add = typeof(DbParameterCollection).GetMethod("Add", new[] { typeof(object) });
                    return Expression.Block(
                        Expression.IfThen(
                            Expression.NotEqual(name, Expression.Constant(null)),
                            Expression.Assign(Expression.Property(Expression.Convert(value, typeof(DbParameter)), "ParameterName"), name)
                            ),
                        Expression.Call(Expression.Property(cmd, "Parameters"), add, value)
                        );
                });
                //Enum
                Register((type, value) => {
                    if (!type.IsEnum)
                        return null;

                    return Expression.Convert(Expression.Convert(Expression.Convert(value, type), typeof(int)), typeof(object));
                });
                //string(only SqlServer??)
                Register<string>((cmd, name, value) => {
                    var parameter = cmd.CreateParameter();
                    parameter.ParameterName = name;
                    parameter.Value = value;
                    if (((string)value).Length < 4000)
                    {
                        parameter.Size = 4000;
                    }
                    cmd.Parameters.Add(parameter);
                });
                //bool
                //Register((type, value) => {
                //    if (type != typeof(bool))
                //        return null;

                //    return Expression.Convert(Expression.Condition(Expression.Convert(value, type), Expression.Constant(1), Expression.Constant(0)), typeof(object));
                //});
            }

            private static readonly object _Sync = new object();
            private static Stack<object> _Handlers;
            private static Dictionary<Type, Action<DbCommand, string, object>> _ObjHandlers;
            public static void Register<T>(Action<DbCommand, string, object> handler)
            {
                Register((type, cmd, name, value) => {
                    if (type != typeof(T))
                        return null;

                    return Expression.Invoke(Expression.Constant(handler), cmd, name, value);
                });
            }
            public static void Register<T>(Func<object, object> handler)
            {
                Register((type, value) => {
                    if (type != typeof(T))
                        return null;

                    return Expression.Invoke(Expression.Constant(handler), value);
                });
            }
            public static void Register(Func<Type, ParameterExpression, Expression> handler)
            {
                Register((type, cmd, name, value) => {
                    var newValue = handler.Invoke(type, value);
                    if (newValue == null)
                        return null;

                    var parameter = Expression.Variable(typeof(DbParameter), "parameter");
                    var createParameter = typeof(DbCommand).GetMethod("CreateParameter", Type.EmptyTypes);
                    var add = typeof(DbParameterCollection).GetMethod("Add", new[] { typeof(object) });
                    return Expression.Block(new[] { parameter },
                        Expression.Assign(parameter, Expression.Call(cmd, createParameter)),
                        Expression.Assign(Expression.Property(parameter, "ParameterName"), name),
                        Expression.Assign(Expression.Property(parameter, "Value"), newValue),
                        Expression.Call(Expression.Property(cmd, "Parameters"), add, parameter)
                        );
                });
            }
            public static void Register(Func<Type, ParameterExpression, ParameterExpression, ParameterExpression, Expression> handler)
            {
                lock (_Sync)
                {
                    _Handlers.Push(handler);
                }
            }
            public static void Register(Type type, ParameterExpression cmd, ParameterExpression name, ParameterExpression value, out Expression expression, out Delegate @delegate)
            {
                expression = null;
                @delegate = null;
                lock (_Sync)
                {
                    foreach (var handler in _Handlers)
                    {
                        if (handler is Func<Type, ParameterExpression, ParameterExpression, ParameterExpression, Expression> exprHandler)
                        {
                            expression = exprHandler.Invoke(type, cmd, name, value);
                            if (expression != null)
                            {
                                if (expression.Type == typeof(void) && expression.NodeType == ExpressionType.Default)
                                    expression = null;
                                return;
                            }
                        }
                        else
                        {
                            (var _type, var _delegate) = (Tuple<Type, Delegate>)handler;
                            if (_type == type)
                            {
                                expression = Expression.Invoke(Expression.Constant(_delegate), cmd, name, value);
                                @delegate = _delegate;
                                return;
                            }
                        }
                    }
                }
            }
            public static void Register(Type type, out Action<DbCommand, string, object> handler)
            {
                if (!_ObjHandlers.TryGetValue(type, out handler))
                {
                    lock (_Sync)
                    {
                        if (!_ObjHandlers.TryGetValue(type, out handler))
                        {
                            var cmd = Expression.Parameter(typeof(DbCommand), "cmd");
                            var name = Expression.Parameter(typeof(string), "name");
                            var value = Expression.Parameter(typeof(object), "value");
                            Register(type, cmd, name, value, out var expression, out var @delegate);
                            if (expression == null)
                            {
                                var parameter = Expression.Variable(typeof(DbParameter), "parameter");
                                var createParameter = typeof(DbCommand).GetMethod("CreateParameter", Type.EmptyTypes);
                                var add = typeof(DbParameterCollection).GetMethod("Add", new[] { typeof(object) });
                                expression = Expression.Block(new[] { parameter },
                                    Expression.Assign(parameter, Expression.Call(cmd, createParameter)),
                                    Expression.Assign(Expression.Property(parameter, "ParameterName"), name),
                                    Expression.Assign(Expression.Property(parameter, "Value"), value),
                                    Expression.Call(Expression.Property(cmd, "Parameters"), add, parameter)
                                    );
                            }
                            handler = @delegate == null
                               ? Expression.Lambda<Action<DbCommand, string, object>>(expression, cmd, name, value).Compile()
                               : (Action<DbCommand, string, object>)@delegate;

                            var objHandlers = new Dictionary<Type, Action<DbCommand, string, object>>(_ObjHandlers);
                            objHandlers.Add(type, handler);
                            _ObjHandlers = objHandlers;
                        }
                    }
                }
            }
        }
        public static void RegisterDbParameter<TDbConnection, T>(Action<DbCommand, string, object> handler) where TDbConnection : DbConnection
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            DbParameter<TDbConnection>.Register<T>(handler);
        }
        public static void RegisterDbParameter<TDbConnection, T>(Func<object, object> handler) where TDbConnection : DbConnection
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            DbParameter<TDbConnection>.Register<T>(handler);
        }
        public static void RegisterDbParameter<TDbConnection>(Func<Type, ParameterExpression, ParameterExpression, ParameterExpression, Expression> handler) where TDbConnection : DbConnection
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            DbParameter<TDbConnection>.Register(handler);
        }
        public static void RegisterDbParameter<TDbConnection>(Func<Type, ParameterExpression, Expression> handler) where TDbConnection : DbConnection
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            DbParameter<TDbConnection>.Register(handler);
        }
        public static void AddParameter<TDbConnection>(DbCommand cmd, string name, object value) where TDbConnection : DbConnection
        {
            if (value == null)
            {
                var parameter = cmd.CreateParameter();
                parameter.ParameterName = name;
                parameter.Value = DBNull.Value;
                cmd.Parameters.Add(parameter);
            }
            else
            {
                DbParameter<TDbConnection>.Register(value.GetType(), out var handler);
                handler.Invoke(cmd, name, value);
            }
        }
        #endregion

        #region SqlExpression
        private static MethodInfo _Asc, _Desc;
        private static MethodInfo _Except;
        private static MethodInfo _Navigate;
        private static MethodInfo _Select1;
        private static MethodInfo _Select2, _Select3, _Select4;
        private static MethodInfo _Select5, _Select6;
        private static MethodInfo _Format1, _Format2, _Format3, _Formats;
        public static object Except(object properties)
        {
            throw new InvalidOperationException(nameof(Except));
        }
        public static bool TryExcept(MethodCallExpression expression, out Expression except)
        {
            var method = expression.Method;
            if (method == _Except)
            {
                except = expression.Arguments[0];
                return true;
            }
            else
            {
                except = null;
                return false;
            }
        }
        public static T Navigate<T>(this SqlExpression @this, T param)
        {
            throw new InvalidOperationException(nameof(Navigate));
        }
        public static bool TryNavigate(MethodCallExpression expression, out Expression navigate)
        {
            var method = expression.Method;
            if (expression.Object == null
                && method.IsGenericMethod
                && method.GetGenericMethodDefinition() == _Navigate)
            {
                navigate = expression.Arguments[1];
                return true;
            }
            else
            {
                navigate = null;
                return false;
            }
        }
        public static object Select(this SqlExpression @this, object select)
        {
            throw new InvalidOperationException(nameof(Select));
        }

        //TODO Select(out from)
        public static bool TrySelect(MethodCallExpression expression, out Expression select)
        {
            if (expression.Method == _Select1)
            {
                select = expression.Arguments[1];
                return true;
            }
            else
            {
                select = null;
                return false;
            }
        }
        public static TResult Select<TEntity, TResult>(this SqlExpression @this, Expression<Func<TEntity, object>> select, Expression<Func<TEntity, bool>> where)
        {
            throw new InvalidOperationException(nameof(Select));
        }
        public static TResult Select<TEntity, TResult>(this SqlExpression @this, Expression<Func<TEntity, object>> select, Expression<Func<TEntity, bool>> where, Expression<Action<TEntity>> orderBy)
        {
            throw new InvalidOperationException(nameof(Select));
        }
        public static TResult Select<TEntity, TResult>(this SqlExpression @this, Expression<Func<TEntity, object>> select, Expression<Func<TEntity, bool>> where, Expression<Func<TEntity, object>> groupBy, Expression<Func<TEntity, bool>> having, Expression<Action<TEntity>> orderBy)
        {
            throw new InvalidOperationException(nameof(Select));
        }
        public static bool TrySelect(MethodCallExpression expression, out LambdaExpression select, out LambdaExpression where, out LambdaExpression groupBy, out LambdaExpression having, out LambdaExpression orderBy)
        {
            var method = expression.Method;
            if (expression.Object == null && method.IsGenericMethod)
            {
                var methodDefinition = method.GetGenericMethodDefinition();
                if (methodDefinition == _Select2)
                {
                    select = (LambdaExpression)expression.Arguments[1].Invoke();
                    where = (LambdaExpression)expression.Arguments[2].Invoke();
                    groupBy = null;
                    having = null;
                    orderBy = null;
                    return true;
                }
                else if (methodDefinition == _Select3)
                {
                    select = (LambdaExpression)expression.Arguments[1].Invoke();
                    where = (LambdaExpression)expression.Arguments[2].Invoke();
                    groupBy = null;
                    having = null;
                    orderBy = (LambdaExpression)expression.Arguments[3].Invoke();
                    return true;
                }
                else if (methodDefinition == _Select4)
                {
                    select = (LambdaExpression)expression.Arguments[1].Invoke();
                    where = (LambdaExpression)expression.Arguments[2].Invoke();
                    groupBy = (LambdaExpression)expression.Arguments[3].Invoke();
                    having = (LambdaExpression)expression.Arguments[4].Invoke();
                    orderBy = (LambdaExpression)expression.Arguments[5].Invoke();
                    return true;
                }
            }
            select = null;
            where = null;
            groupBy = null;
            having = null;
            orderBy = null;
            return false;
        }
        public static TResult Select<TEntity, TResult>(this SqlExpression @this, int offset, int fetch, Expression<Func<TEntity, object>> select, Expression<Func<TEntity, bool>> where, Expression<Action<TEntity>> orderBy)
        {
            throw new InvalidOperationException(nameof(Select));
        }
        public static TResult Select<TEntity, TResult>(this SqlExpression @this, int offset, int fetch, Expression<Func<TEntity, object>> select, Expression<Func<TEntity, bool>> where, Expression<Func<TEntity, object>> groupBy, Expression<Func<TEntity, bool>> having, Expression<Action<TEntity>> orderBy)
        {
            throw new InvalidOperationException(nameof(Select));
        }
        public static bool TrySelect(MethodCallExpression expression, out int offset, out int fetch, out LambdaExpression select, out LambdaExpression where, out LambdaExpression groupBy, out LambdaExpression having, out LambdaExpression orderBy)
        {
            var method = expression.Method;
            if (expression.Object == null && method.IsGenericMethod)
            {
                var methodDefinition = method.GetGenericMethodDefinition();
                if (methodDefinition == _Select5)
                {
                    offset = (int)expression.Arguments[1].Invoke();
                    fetch = (int)expression.Arguments[2].Invoke();
                    select = (LambdaExpression)expression.Arguments[3].Invoke();
                    where = (LambdaExpression)expression.Arguments[4].Invoke();
                    groupBy = null;
                    having = null;
                    orderBy = (LambdaExpression)expression.Arguments[5].Invoke();
                    return true;
                }
                else if (methodDefinition == _Select6)
                {
                    offset = (int)expression.Arguments[1].Invoke();
                    fetch = (int)expression.Arguments[2].Invoke();
                    select = (LambdaExpression)expression.Arguments[3].Invoke();
                    where = (LambdaExpression)expression.Arguments[4].Invoke();
                    groupBy = (LambdaExpression)expression.Arguments[5].Invoke();
                    having = (LambdaExpression)expression.Arguments[6].Invoke();
                    orderBy = (LambdaExpression)expression.Arguments[7].Invoke();
                    return true;
                }
            }
            offset = 0;
            fetch = 0;
            select = null;
            where = null;
            groupBy = null;
            having = null;
            orderBy = null;
            return false;
        }
        public static bool TryFormat(Expression expression, out string format, out IReadOnlyList<Expression> argExprs, out int startIndex)
        {
            var formatExpr = expression as MethodCallExpression;
            if (formatExpr != null)
            {
                var method = formatExpr.Method;
                if (method == _Format1 || method == _Format2 || method == _Format3)
                {
                    format = (string)((ConstantExpression)formatExpr.Arguments[0]).Value;
                    argExprs = formatExpr.Arguments;
                    startIndex = 1;
                    return true;
                }
                else if (method == _Formats)
                {
                    format = (string)((ConstantExpression)formatExpr.Arguments[0]).Value;
                    argExprs = ((NewArrayExpression)formatExpr.Arguments[1]).Expressions;
                    startIndex = 0;
                    return true;
                }
            }
            format = null;
            argExprs = null;
            startIndex = 0;
            return false;
        }
        //TODO?? Cast<DateTime>(param1,param2)
        public static T Cast<T>(this SqlExpression @this, object param)
        {
            throw new InvalidOperationException(nameof(Cast));
        }
        #endregion 
        public static Expression<Action<TEntity, SqlExpression>> OrderBy<TEntity>(this SqlDb _)
        {
            return null;
        }
        public static Expression<Action<TEntity, SqlExpression>> Asc<TEntity>(this Expression<Action<TEntity, SqlExpression>> @this, Expression<Func<TEntity, object>> orderBy)
        {
            if (orderBy == null)
                return @this;

            if (@this == null)
            {
                var sqlExpr = Expression.Parameter(typeof(SqlExpression), "s");
                return Expression.Lambda<Action<TEntity, SqlExpression>>(
                    Expression.Call(sqlExpr, _Asc, orderBy.Body),
                    orderBy.Parameters[0], sqlExpr);
            }
            else
            {
                var orderByBody = orderBy.Body.Replace(orderBy.Parameters[0], @this.Parameters[0]);
                return Expression.Lambda<Action<TEntity, SqlExpression>>(
                    Expression.Call(@this.Body, _Asc, orderByBody), @this.Parameters);
            }
        }
        public static Expression<Action<TEntity, SqlExpression>> Desc<TEntity>(this Expression<Action<TEntity, SqlExpression>> @this, Expression<Func<TEntity, object>> orderBy)
        {
            if (orderBy == null)
                return @this;

            if (@this == null)
            {
                var sqlExpr = Expression.Parameter(typeof(SqlExpression), "s");
                return Expression.Lambda<Action<TEntity, SqlExpression>>(
                    Expression.Call(sqlExpr, _Desc, orderBy.Body),
                    orderBy.Parameters[0], sqlExpr);
            }
            else
            {
                var orderByBody = orderBy.Body.Replace(orderBy.Parameters[0], @this.Parameters[0]);
                return Expression.Lambda<Action<TEntity, SqlExpression>>(
                    Expression.Call(@this.Body, _Desc, orderByBody), @this.Parameters);
            }
        }
        public static Expression<Action<TEntity, SqlExpression>> AscIf<TEntity>(this Expression<Action<TEntity, SqlExpression>> @this, bool condition, Expression<Func<TEntity, object>> orderBy)
        {
            if (condition)
                return @this.Asc(orderBy);
            return @this;
        }
        public static Expression<Action<TEntity, SqlExpression>> DescIf<TEntity>(this Expression<Action<TEntity, SqlExpression>> @this, bool condition, Expression<Func<TEntity, object>> orderBy)
        {
            if (condition)
                return @this.Desc(orderBy);
            return @this;
        }
        public static Expression<Func<TEntity, SqlExpression, bool>> Where<TEntity>(object identity)
        {
            RegisterIdentity<TEntity>(out var identityProperty);
            var entity = Expression.Parameter(typeof(TEntity), "entity");
            var sqlExpr = Expression.Parameter(typeof(SqlExpression), "s");
            return Expression.Lambda<Func<TEntity, SqlExpression, bool>>(
                Expression.Equal(
                    Expression.Property(entity, identityProperty),
                    Expression.Constant(identity, identityProperty.PropertyType)),
                entity, sqlExpr);
        }
        public static Expression<Func<TEntity, SqlExpression, bool>> Where<TEntity>(this SqlDb _)
        {
            return null;
        }
        public static Expression<Func<TEntity, SqlExpression, bool>> Where<TEntity>(this SqlDb _, Expression<Func<TEntity, SqlExpression, bool>> where)
        {
            return where;
        }
        public static Expression<Func<TEntity, SqlExpression, bool>> Where<TEntity>(this SqlDb _, Expression<Func<TEntity, bool>> where)
        {
            if (where == null)
                return null;

            return Expression.Lambda<Func<TEntity, SqlExpression, bool>>(where.Body, where.Parameters[0], Expression.Parameter(typeof(SqlExpression)));
        }
        public static Expression<Func<TEntity, SqlExpression, bool>> And<TEntity>(this Expression<Func<TEntity, SqlExpression, bool>> @this, Expression<Func<TEntity, SqlExpression, bool>> where)
        {
            if (@this == null)
                return where;
            if (where == null)
                return @this;

            var andBody = where.Body.Replace(where.Parameters[0], @this.Parameters[0]);
            return Expression.Lambda<Func<TEntity, SqlExpression, bool>>(Expression.AndAlso(@this.Body, andBody), @this.Parameters);
        }
        public static Expression<Func<TEntity, SqlExpression, bool>> And<TEntity>(this Expression<Func<TEntity, SqlExpression, bool>> @this, Expression<Func<TEntity, bool>> where)
        {
            if (where == null)
                return @this;

            if (@this == null)
            {
                var sqlExpr = Expression.Parameter(typeof(SqlExpression), "s");
                return Expression.Lambda<Func<TEntity, SqlExpression, bool>>(
                    where.Body, where.Parameters[0], sqlExpr);
            }
            else
            {
                var andBody = where.Body.Replace(where.Parameters[0], @this.Parameters[0]);
                return Expression.Lambda<Func<TEntity, SqlExpression, bool>>(
                    Expression.AndAlso(@this.Body, andBody), @this.Parameters);
            }
        }
        public static Expression<Func<TEntity, SqlExpression, bool>> Or<TEntity>(this Expression<Func<TEntity, SqlExpression, bool>> @this, Expression<Func<TEntity, SqlExpression, bool>> where)
        {
            if (@this == null)
                return where;
            if (where == null)
                return @this;

            var orBody = where.Body.Replace(where.Parameters[0], @this.Parameters[0]); ;
            return Expression.Lambda<Func<TEntity, SqlExpression, bool>>(Expression.OrElse(@this.Body, orBody), @this.Parameters);
        }
        public static Expression<Func<TEntity, SqlExpression, bool>> Or<TEntity>(this Expression<Func<TEntity, SqlExpression, bool>> @this, Expression<Func<TEntity, bool>> where)
        {
            if (where == null)
                return @this;

            if (@this == null)
            {
                var sqlExpr = Expression.Parameter(typeof(SqlExpression), "s");
                return Expression.Lambda<Func<TEntity, SqlExpression, bool>>(
                    where.Body, where.Parameters[0], sqlExpr);
            }
            else
            {
                var orBody = where.Body.Replace(where.Parameters[0], @this.Parameters[0]);
                return Expression.Lambda<Func<TEntity, SqlExpression, bool>>(
                    Expression.OrElse(@this.Body, orBody), @this.Parameters);
            }
        }
        public static Expression<Func<TEntity, SqlExpression, bool>> AndIf<TEntity>(this Expression<Func<TEntity, SqlExpression, bool>> @this, bool condition, Expression<Func<TEntity, SqlExpression, bool>> where)
        {
            if (condition)
                return @this.And(where);
            return @this;
        }
        public static Expression<Func<TEntity, SqlExpression, bool>> AndIf<TEntity>(this Expression<Func<TEntity, SqlExpression, bool>> @this, bool condition, Expression<Func<TEntity, bool>> where)
        {
            if (condition)
                return @this.And(where);
            return @this;
        }
        public static Expression<Func<TEntity, SqlExpression, bool>> OrIf<TEntity>(this Expression<Func<TEntity, SqlExpression, bool>> @this, bool condition, Expression<Func<TEntity, SqlExpression, bool>> where)
        {
            if (condition)
                return @this.Or(where);
            return @this;
        }
        public static Expression<Func<TEntity, SqlExpression, bool>> OrIf<TEntity>(this Expression<Func<TEntity, SqlExpression, bool>> @this, bool condition, Expression<Func<TEntity, bool>> where)
        {
            if (condition)
                return @this.Or(where);
            return @this;
        }
        public static T Execute<T>(this SqlDb @this, string sql)
        {
            (var reader, var disposable) = @this.ExecuteReader(sql);
            try
            {
                return @this.Read<T>(reader);
            }
            finally
            {
                reader.Close();
                disposable.Dispose();
            }
        }
        public static T Execute<T>(this SqlDb @this, string sql, object parameters)
        {
            (var reader, var disposable) = @this.ExecuteReader(sql, parameters);
            try
            {
                return @this.Read<T>(reader);
            }
            finally
            {
                reader.Close();
                disposable.Dispose();
            }
        }
        public static T Execute<T>(this SqlDb @this, string sql, IList<(string, object)> parameters)
        {
            (var reader, var disposable) = @this.ExecuteReader(sql, parameters);
            try
            {
                return @this.Read<T>(reader);
            }
            finally
            {
                reader.Close();
                disposable.Dispose();
            }
        }
        public static (T1, T2) Execute<T1, T2>(this SqlDb @this, string sql)
        {
            (var reader, var disposable) = @this.ExecuteReader(sql);
            try
            {
                var t1 = @this.Read<T1>(reader);
                reader.NextResult();
                var t2 = @this.Read<T2>(reader);
                return (t1, t2);
            }
            finally
            {
                reader.Close();
                disposable.Dispose();
            }
        }
        public static (T1, T2) Execute<T1, T2>(this SqlDb @this, string sql, object parameters)
        {
            (var reader, var disposable) = @this.ExecuteReader(sql, parameters);
            try
            {
                var t1 = @this.Read<T1>(reader);
                reader.NextResult();
                var t2 = @this.Read<T2>(reader);
                return (t1, t2);
            }
            finally
            {
                reader.Close();
                disposable.Dispose();
            }
        }
        public static (T1, T2) Execute<T1, T2>(this SqlDb @this, string sql, IList<(string, object)> parameters)
        {
            (var reader, var disposable) = @this.ExecuteReader(sql, parameters);
            try
            {
                var t1 = @this.Read<T1>(reader);
                reader.NextResult();
                var t2 = @this.Read<T2>(reader);
                return (t1, t2);
            }
            finally
            {
                reader.Close();
                disposable.Dispose();
            }
        }
        public static (T1, T2, T3) Execute<T1, T2, T3>(this SqlDb @this, string sql)
        {
            (var reader, var disposable) = @this.ExecuteReader(sql);
            try
            {
                var t1 = @this.Read<T1>(reader);
                reader.NextResult();
                var t2 = @this.Read<T2>(reader);
                reader.NextResult();
                var t3 = @this.Read<T3>(reader);
                return (t1, t2, t3);
            }
            finally
            {
                reader.Close();
                disposable.Dispose();
            }
        }
        public static (T1, T2, T3) Execute<T1, T2, T3>(this SqlDb @this, string sql, object parameters)
        {
            (var reader, var disposable) = @this.ExecuteReader(sql, parameters);
            try
            {
                var t1 = @this.Read<T1>(reader);
                reader.NextResult();
                var t2 = @this.Read<T2>(reader);
                reader.NextResult();
                var t3 = @this.Read<T3>(reader);
                return (t1, t2, t3);
            }
            finally
            {
                reader.Close();
                disposable.Dispose();
            }
        }
        public static (T1, T2, T3) Execute<T1, T2, T3>(this SqlDb @this, string sql, IList<(string, object)> parameters)
        {
            (var reader, var disposable) = @this.ExecuteReader(sql, parameters);
            try
            {
                var t1 = @this.Read<T1>(reader);
                reader.NextResult();
                var t2 = @this.Read<T2>(reader);
                reader.NextResult();
                var t3 = @this.Read<T3>(reader);
                return (t1, t2, t3);
            }
            finally
            {
                reader.Close();
                disposable.Dispose();
            }
        }
        public static (T1, T2, T3, T4) Execute<T1, T2, T3, T4>(this SqlDb @this, string sql)
        {
            (var reader, var disposable) = @this.ExecuteReader(sql);
            try
            {
                var t1 = @this.Read<T1>(reader);
                reader.NextResult();
                var t2 = @this.Read<T2>(reader);
                reader.NextResult();
                var t3 = @this.Read<T3>(reader);
                reader.NextResult();
                var t4 = @this.Read<T4>(reader);
                return (t1, t2, t3, t4);
            }
            finally
            {
                reader.Close();
                disposable.Dispose();
            }
        }
        public static (T1, T2, T3, T4) Execute<T1, T2, T3, T4>(this SqlDb @this, string sql, object parameters)
        {
            (var reader, var disposable) = @this.ExecuteReader(sql, parameters);
            try
            {
                var t1 = @this.Read<T1>(reader);
                reader.NextResult();
                var t2 = @this.Read<T2>(reader);
                reader.NextResult();
                var t3 = @this.Read<T3>(reader);
                reader.NextResult();
                var t4 = @this.Read<T4>(reader);
                return (t1, t2, t3, t4);
            }
            finally
            {
                reader.Close();
                disposable.Dispose();
            }
        }
        public static (T1, T2, T3, T4) Execute<T1, T2, T3, T4>(this SqlDb @this, string sql, IList<(string, object)> parameters)
        {
            (var reader, var disposable) = @this.ExecuteReader(sql, parameters);
            try
            {
                var t1 = @this.Read<T1>(reader);
                reader.NextResult();
                var t2 = @this.Read<T2>(reader);
                reader.NextResult();
                var t3 = @this.Read<T3>(reader);
                reader.NextResult();
                var t4 = @this.Read<T4>(reader);
                return (t1, t2, t3, t4);
            }
            finally
            {
                reader.Close();
                disposable.Dispose();
            }
        }
        public static async Task<T> ExecuteAsync<T>(this SqlDb @this, string sql)
        {
            (var reader, var disposable) = await @this.ExecuteReaderAsync(sql);
            try
            {
                return @this.Read<T>(reader);
            }
            finally
            {
                reader.Close();
                disposable.Dispose();
            }
        }
        public static async Task<T> ExecuteAsync<T>(this SqlDb @this, string sql, object parameters)
        {
            (var reader, var disposable) = await @this.ExecuteReaderAsync(sql, parameters);
            try
            {
                return @this.Read<T>(reader);
            }
            finally
            {
                reader.Close();
                disposable.Dispose();
            }
        }
        public static async Task<T> ExecuteAsync<T>(this SqlDb @this, string sql, IList<(string, object)> parameters)
        {
            (var reader, var disposable) = await @this.ExecuteReaderAsync(sql, parameters);
            try
            {
                return @this.Read<T>(reader);
            }
            finally
            {
                reader.Close();
                disposable.Dispose();
            }
        }
        public static async Task<(T1, T2)> ExecuteAsync<T1, T2>(this SqlDb @this, string sql)
        {
            (var reader, var disposable) = await @this.ExecuteReaderAsync(sql);
            try
            {
                var t1 = @this.Read<T1>(reader);
                reader.NextResult();
                var t2 = @this.Read<T2>(reader);
                return (t1, t2);
            }
            finally
            {
                reader.Close();
                disposable.Dispose();
            }
        }
        public static async Task<(T1, T2)> ExecuteAsync<T1, T2>(this SqlDb @this, string sql, object parameters)
        {
            (var reader, var disposable) = await @this.ExecuteReaderAsync(sql, parameters);
            try
            {
                var t1 = @this.Read<T1>(reader);
                reader.NextResult();
                var t2 = @this.Read<T2>(reader);
                return (t1, t2);
            }
            finally
            {
                reader.Close();
                disposable.Dispose();
            }
        }
        public static async Task<(T1, T2)> ExecuteAsync<T1, T2>(this SqlDb @this, string sql, IList<(string, object)> parameters)
        {
            (var reader, var disposable) = await @this.ExecuteReaderAsync(sql, parameters);
            try
            {
                var t1 = @this.Read<T1>(reader);
                reader.NextResult();
                var t2 = @this.Read<T2>(reader);
                return (t1, t2);
            }
            finally
            {
                reader.Close();
                disposable.Dispose();
            }
        }
        public static async Task<(T1, T2, T3)> ExecuteAsync<T1, T2, T3>(this SqlDb @this, string sql)
        {
            (var reader, var disposable) = await @this.ExecuteReaderAsync(sql);
            try
            {
                var t1 = @this.Read<T1>(reader);
                reader.NextResult();
                var t2 = @this.Read<T2>(reader);
                reader.NextResult();
                var t3 = @this.Read<T3>(reader);
                return (t1, t2, t3);
            }
            finally
            {
                reader.Close();
                disposable.Dispose();
            }
        }
        public static async Task<(T1, T2, T3)> ExecuteAsync<T1, T2, T3>(this SqlDb @this, string sql, object parameters)
        {
            (var reader, var disposable) = await @this.ExecuteReaderAsync(sql, parameters);
            try
            {
                var t1 = @this.Read<T1>(reader);
                reader.NextResult();
                var t2 = @this.Read<T2>(reader);
                reader.NextResult();
                var t3 = @this.Read<T3>(reader);
                return (t1, t2, t3);
            }
            finally
            {
                reader.Close();
                disposable.Dispose();
            }
        }
        public static async Task<(T1, T2, T3)> ExecuteAsync<T1, T2, T3>(this SqlDb @this, string sql, IList<(string, object)> parameters)
        {
            (var reader, var disposable) = await @this.ExecuteReaderAsync(sql, parameters);
            try
            {
                var t1 = @this.Read<T1>(reader);
                reader.NextResult();
                var t2 = @this.Read<T2>(reader);
                reader.NextResult();
                var t3 = @this.Read<T3>(reader);
                return (t1, t2, t3);
            }
            finally
            {
                reader.Close();
                disposable.Dispose();
            }
        }
        public static async Task<(T1, T2, T3, T4)> ExecuteAsync<T1, T2, T3, T4>(this SqlDb @this, string sql)
        {
            (var reader, var disposable) = await @this.ExecuteReaderAsync(sql);
            try
            {
                var t1 = @this.Read<T1>(reader);
                reader.NextResult();
                var t2 = @this.Read<T2>(reader);
                reader.NextResult();
                var t3 = @this.Read<T3>(reader);
                reader.NextResult();
                var t4 = @this.Read<T4>(reader);
                return (t1, t2, t3, t4);
            }
            finally
            {
                reader.Close();
                disposable.Dispose();
            }
        }
        public static async Task<(T1, T2, T3, T4)> ExecuteAsync<T1, T2, T3, T4>(this SqlDb @this, string sql, object parameters)
        {
            (var reader, var disposable) = await @this.ExecuteReaderAsync(sql, parameters);
            try
            {
                var t1 = @this.Read<T1>(reader);
                reader.NextResult();
                var t2 = @this.Read<T2>(reader);
                reader.NextResult();
                var t3 = @this.Read<T3>(reader);
                reader.NextResult();
                var t4 = @this.Read<T4>(reader);
                return (t1, t2, t3, t4);
            }
            finally
            {
                reader.Close();
                disposable.Dispose();
            }
        }
        public static async Task<(T1, T2, T3, T4)> ExecuteAsync<T1, T2, T3, T4>(this SqlDb @this, string sql, IList<(string, object)> parameters)
        {
            (var reader, var disposable) = await @this.ExecuteReaderAsync(sql, parameters);
            try
            {
                var t1 = @this.Read<T1>(reader);
                reader.NextResult();
                var t2 = @this.Read<T2>(reader);
                reader.NextResult();
                var t3 = @this.Read<T3>(reader);
                reader.NextResult();
                var t4 = @this.Read<T4>(reader);
                return (t1, t2, t3, t4);
            }
            finally
            {
                reader.Close();
                disposable.Dispose();
            }
        }
        public static T ExecuteFormat<T>(this SqlDb @this, FormattableString sqlFormat)
        {
            (var reader, var disposable) = @this.ExecuteReaderFormat(sqlFormat);
            try
            {
                return @this.Read<T>(reader);
            }
            finally
            {
                reader.Close();
                disposable.Dispose();
            }
        }
        public static (T1, T2) ExecuteFormat<T1, T2>(this SqlDb @this, FormattableString sqlFormat)
        {
            (var reader, var disposable) = @this.ExecuteReaderFormat(sqlFormat);
            try
            {
                var t1 = @this.Read<T1>(reader);
                reader.NextResult();
                var t2 = @this.Read<T2>(reader);
                return (t1, t2);
            }
            finally
            {
                reader.Close();
                disposable.Dispose();
            }
        }
        public static (T1, T2, T3) ExecuteFormat<T1, T2, T3>(this SqlDb @this, FormattableString sqlFormat)
        {
            (var reader, var disposable) = @this.ExecuteReaderFormat(sqlFormat);
            try
            {
                var t1 = @this.Read<T1>(reader);
                reader.NextResult();
                var t2 = @this.Read<T2>(reader);
                reader.NextResult();
                var t3 = @this.Read<T3>(reader);
                return (t1, t2, t3);
            }
            finally
            {
                reader.Close();
                disposable.Dispose();
            }
        }
        public static (T1, T2, T3, T4) ExecuteFormat<T1, T2, T3, T4>(this SqlDb @this, FormattableString sqlFormat)
        {
            (var reader, var disposable) = @this.ExecuteReaderFormat(sqlFormat);
            try
            {
                var t1 = @this.Read<T1>(reader);
                reader.NextResult();
                var t2 = @this.Read<T2>(reader);
                reader.NextResult();
                var t3 = @this.Read<T3>(reader);
                reader.NextResult();
                var t4 = @this.Read<T4>(reader);
                return (t1, t2, t3, t4);
            }
            finally
            {
                reader.Close();
                disposable.Dispose();
            }
        }
        public static async Task<T> ExecuteFormatAsync<T>(this SqlDb @this, FormattableString sqlFormat)
        {
            (var reader, var disposable) = await @this.ExecuteReaderFormatAsync(sqlFormat);
            try
            {
                return @this.Read<T>(reader);
            }
            finally
            {
                reader.Close();
                disposable.Dispose();
            }
        }
        public static async Task<(T1, T2)> ExecuteFormatAsync<T1, T2>(this SqlDb @this, FormattableString sqlFormat)
        {
            (var reader, var disposable) = await @this.ExecuteReaderFormatAsync(sqlFormat);
            try
            {
                var t1 = @this.Read<T1>(reader);
                reader.NextResult();
                var t2 = @this.Read<T2>(reader);
                return (t1, t2);
            }
            finally
            {
                reader.Close();
                disposable.Dispose();
            }
        }
        public static async Task<(T1, T2, T3)> ExecuteFormatAsync<T1, T2, T3>(this SqlDb @this, FormattableString sqlFormat)
        {
            (var reader, var disposable) = await @this.ExecuteReaderFormatAsync(sqlFormat);
            try
            {
                var t1 = @this.Read<T1>(reader);
                reader.NextResult();
                var t2 = @this.Read<T2>(reader);
                reader.NextResult();
                var t3 = @this.Read<T3>(reader);
                return (t1, t2, t3);
            }
            finally
            {
                reader.Close();
                disposable.Dispose();
            }
        }
        public static async Task<(T1, T2, T3, T4)> ExecuteFormatAsync<T1, T2, T3, T4>(this SqlDb @this, FormattableString sqlFormat)
        {
            (var reader, var disposable) = await @this.ExecuteReaderFormatAsync(sqlFormat);
            try
            {
                var t1 = @this.Read<T1>(reader);
                reader.NextResult();
                var t2 = @this.Read<T2>(reader);
                reader.NextResult();
                var t3 = @this.Read<T3>(reader);
                reader.NextResult();
                var t4 = @this.Read<T4>(reader);
                return (t1, t2, t3, t4);
            }
            finally
            {
                reader.Close();
                disposable.Dispose();
            }
        }
        public static List<TEntity> Select<TEntity>(this SqlDb @this, Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where)
        {
            return @this.Select(select, where, null);
        }
        public static Task<List<TEntity>> SelectAsync<TEntity>(this SqlDb @this, Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where)
        {
            return @this.SelectAsync(select, where, null);
        }
        public static TResult Select<TEntity, TResult>(this SqlDb @this, Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<TEntity, SqlExpression, bool>> where)
        {
            return @this.Select<TEntity, TResult>(select, null, where, null, null, null);
        }
        public static TResult Select<TEntity, TResult>(this SqlDb @this, Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
        {
            return @this.Select<TEntity, TResult>(select, null, where, null, null, orderBy);
        }


        public static Task<TResult> SelectAsync<TEntity, TResult>(this SqlDb @this, Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<TEntity, SqlExpression, bool>> where)
        {
            return @this.SelectAsync<TEntity, TResult>(select, null, where, null, null, null);
        }
        public static Task<TResult> SelectAsync<TEntity, TResult>(this SqlDb @this, Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
        {
            return @this.SelectAsync<TEntity, TResult>(select, null, where, null, null, orderBy);
        }
        public static TResult Select<TEntity, TResult>(this SqlDb @this, int offset, int fetch, Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
        {
            return @this.Select<TEntity, TResult>(offset, fetch, select, null, where, null, null, orderBy);
        }
        public static Task<TResult> SelectAsync<TEntity, TResult>(this SqlDb @this, int offset, int fetch, Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
        {
            return @this.SelectAsync<TEntity, TResult>(offset, fetch, select, null, where, null, null, orderBy);
        }

        public static (TResult, int) SelectPaged<TEntity, TResult>(this SqlDb @this, int offset, int fetch, Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy) 
        {
            return @this.SelectPaged<TEntity, TResult>(offset, fetch, select, null, where, orderBy);
        }
        public static Task<(TResult, int)> SelectPagedAsync<TEntity, TResult>(this SqlDb @this, int offset, int fetch, Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
        {
            return @this.SelectPagedAsync<TEntity, TResult>(offset, fetch, select, null, where, orderBy);
        }

        //Transaction
        public static void Execute(this ITransactionFactory @this, Action transactionAction)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (transactionAction == null)
                throw new ArgumentNullException(nameof(transactionAction));

            var transaction = @this.Create();
            try
            {
                transactionAction();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
            transaction.Commit();
        }
        public static void Execute(this ITransactionFactory @this, Action transactionAction, IsolationLevel isolationLevel)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (transactionAction == null)
                throw new ArgumentNullException(nameof(transactionAction));

            var transaction = @this.Create(isolationLevel);
            try
            {
                transactionAction();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
            transaction.Commit();
        }
        public static async Task ExecuteAsync(this ITransactionFactory @this, Func<Task> transactionAction)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (transactionAction == null)
                throw new ArgumentNullException(nameof(transactionAction));

            var transaction = @this.Create();
            try
            {
                await transactionAction();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
            transaction.Commit();
        }
        public static async Task ExecuteAsync(this ITransactionFactory @this, Func<Task> transactionAction, IsolationLevel isolationLevel)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (transactionAction == null)
                throw new ArgumentNullException(nameof(transactionAction));

            var transaction = @this.Create(isolationLevel);
            try
            {
                await transactionAction();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
            transaction.Commit();
        }
    }
}
