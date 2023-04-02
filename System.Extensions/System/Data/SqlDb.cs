
namespace System.Data
{
    using System.Diagnostics;
    using System.Reflection;
    using System.Data.Common;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    public abstract class SqlDb
    {
        public static void Register<TDbConnection>(Type dbType) where TDbConnection : DbConnection
        {
            if (dbType == null)
                throw new ArgumentNullException(nameof(dbType));

            var ctor = dbType.GetConstructor(new[] { typeof(Type), typeof(Func<(DbCommand, IDisposable)>), typeof(Action<DbCommand>) });
            if (ctor == null)
                throw new ArgumentException("Constructor(Type,Func<(DbCommand, IDisposable)>,Action<DbCommand>)", nameof(dbType));

            Db<TDbConnection>.DbType = dbType;
        }
        public static void Register<TDbConnection>(out Type dbType) where TDbConnection : DbConnection
        {
            dbType = Db<TDbConnection>.DbType;
            if (dbType == null)  //default
            {
                switch (typeof(TDbConnection).Name)
                {
                    case "SqlConnection":
                        dbType = typeof(SqlServerDb);
                        return;
                    case "SQLiteConnection"://System.Data.Sqlite
                    case "SqliteConnection"://Microsoft.Data.Sqlite
                        dbType = typeof(SqliteDb);
                        return;
                    case "OracleConnection":
                        dbType = typeof(OracleDb);
                        return;
                    case "NpgsqlConnection":
                        dbType = typeof(PostgreSqlDb);
                        return;
                    case "MySqlConnection":
                        dbType = typeof(MySqlDb);
                        return;
                }
            }
        }
        public static SqlDb Create<TDbConnection>(string connectionString) where TDbConnection : DbConnection
        {
            return Create<TDbConnection>(connectionString, null);
        }
        public static SqlDb Create<TDbConnection>(string connectionString, Action<DbCommand> cmdExecuting) where TDbConnection : DbConnection
        {
            if (connectionString == null)
                throw new ArgumentNullException(nameof(connectionString));

            var returnCtor = typeof(ValueTuple<DbCommand, IDisposable>).GetConstructor(new[] { typeof(DbCommand), typeof(IDisposable) });
            var connectionType = typeof(TDbConnection);
            var createCommand = connectionType.GetMethod("CreateCommand", Type.EmptyTypes);
            var commandType = createCommand.ReturnType;
            if (typeof(TDbConnection).Name == "OracleConnection")
            {
                var bindByName = commandType.GetProperty("BindByName");
                var connection = Expression.Variable(connectionType, "connection");
                var command = Expression.Variable(commandType, "command");
                var expr = Expression.Block(new[] { connection, command },
                    Expression.Assign(connection, Expression.New(connectionType.GetConstructor(new[] { typeof(string) }),
                    Expression.Constant(connectionString))), Expression.Assign(command, Expression.Call(connection, createCommand)),
                    Expression.Assign(Expression.Property(command, bindByName), Expression.Constant(true)),
                    Expression.New(returnCtor, command, connection));
                return Create<TDbConnection>(Expression.Lambda<Func<(DbCommand, IDisposable)>>(expr).Compile(), cmdExecuting);
            }
            else
            {
                var connection = Expression.Variable(connectionType, "connection");
                var expr = Expression.Block(new[] { connection },
                    Expression.Assign(connection, Expression.New(connectionType.GetConstructor(new[] { typeof(string) }), Expression.Constant(connectionString))),
                    Expression.New(returnCtor, Expression.Call(connection, createCommand), connection));
                return Create<TDbConnection>(Expression.Lambda<Func<(DbCommand, IDisposable)>>(expr).Compile(), cmdExecuting);
            }
        }
        public static SqlDb Create<TDbConnection>(string connectionString, out ITransactionFactory transactionFactory) where TDbConnection : DbConnection
        {
            return Create<TDbConnection>(connectionString, null, out transactionFactory);
        }
        public static SqlDb Create<TDbConnection>(string connectionString, Action<DbCommand> cmdExecuting, out ITransactionFactory transactionFactory) where TDbConnection : DbConnection
        {
            if (connectionString == null)
                throw new ArgumentNullException(nameof(connectionString));

            var connectionType = typeof(TDbConnection);
            var _transactionFactory = new TransactionFactory();
            _transactionFactory.Transaction = new AsyncLocal<Transaction>();
            //BeginDbTransaction
            {
                var isolationLevel = Expression.Parameter(typeof(IsolationLevel), "isolationLevel");
                var connection = Expression.Variable(connectionType, "connection");
                var expr = Expression.Block(new[] { connection },
                    Expression.Assign(connection, Expression.New(connectionType.GetConstructor(new[] { typeof(string) }), Expression.Constant(connectionString))),
                    Expression.Call(connection, connectionType.GetMethod("Open", Type.EmptyTypes)),
                    Expression.Call(connection, connectionType.GetMethod("BeginTransaction", new[] { typeof(IsolationLevel) }), isolationLevel));
                _transactionFactory.BeginDbTransaction = Expression.Lambda<Func<IsolationLevel, DbTransaction>>(expr, isolationLevel).Compile();
            }
            transactionFactory = _transactionFactory;
            var returnCtor = typeof(ValueTuple<DbCommand, IDisposable>).GetConstructor(new[] { typeof(DbCommand), typeof(IDisposable) });
            var createCommand = connectionType.GetMethod("CreateCommand", Type.EmptyTypes);
            var commandType = createCommand.ReturnType;
            if (typeof(TDbConnection).Name == "OracleConnection")
            {
                _transactionFactory.IsolationLevel = IsolationLevel.ReadCommitted;
                var bindByName = commandType.GetProperty("BindByName");
                var transaction = Expression.Variable(typeof(Transaction), "transaction");
                var connection = Expression.Variable(connectionType, "connection");
                var command = Expression.Variable(commandType, "command");

                var expr = Expression.Block(new[] { transaction, connection, command },
                    Expression.Assign(transaction, Expression.Property(Expression.Field(Expression.Constant(_transactionFactory), typeof(TransactionFactory).GetField("Transaction")), typeof(AsyncLocal<Transaction>).GetProperty("Value"))),
                    Expression.Condition(Expression.Equal(transaction, Expression.Constant(null, typeof(Transaction))),
                    Expression.Block(
                        Expression.Assign(connection, Expression.New(connectionType.GetConstructor(new[] { typeof(string) }), Expression.Constant(connectionString))),
                        Expression.Assign(command, Expression.Call(connection, createCommand)),
                        Expression.Assign(Expression.Property(command, bindByName), Expression.Constant(true)),
                        Expression.New(returnCtor, command, connection)),
                    Expression.Block(
                        Expression.Assign(connection, Expression.Convert(Expression.Property(transaction, typeof(Transaction).GetProperty("Connection")), connectionType)),
                        Expression.Assign(command, Expression.Call(connection, createCommand)),
                        Expression.Assign(Expression.Property(command, bindByName), Expression.Constant(true)),
                        Expression.Assign(Expression.Property(Expression.Convert(command, typeof(DbCommand)), typeof(DbCommand).GetProperty("Transaction")), Expression.Field(transaction, typeof(Transaction).GetField("DbTransaction"))),
                        Expression.New(returnCtor, command, transaction)
                        )
                    ));
                return Create<TDbConnection>(Expression.Lambda<Func<(DbCommand, IDisposable)>>(expr).Compile(), cmdExecuting);
            }
            else
            {
                _transactionFactory.IsolationLevel = IsolationLevel.Unspecified;
                var transaction = Expression.Variable(typeof(Transaction), "transaction");
                var connection = Expression.Variable(connectionType, "connection");
                var command = Expression.Variable(commandType, "command");
                var expr = Expression.Block(new[] { transaction, connection },
                    Expression.Assign(transaction, Expression.Property(Expression.Field(Expression.Constant(_transactionFactory), typeof(TransactionFactory).GetField("Transaction")), typeof(AsyncLocal<Transaction>).GetProperty("Value"))),
                    Expression.Condition(Expression.Equal(transaction, Expression.Constant(null, typeof(Transaction))),
                    Expression.Block(
                        Expression.Assign(connection, Expression.New(connectionType.GetConstructor(new[] { typeof(string) }), Expression.Constant(connectionString))),
                        Expression.New(returnCtor, Expression.Call(connection, createCommand), connection)),
                    Expression.Block(new[] { command },
                        Expression.Assign(connection, Expression.Convert(Expression.Property(transaction, typeof(Transaction).GetProperty("Connection")), connectionType)),
                        Expression.Assign(command, Expression.Call(connection, createCommand)),
                        Expression.Assign(Expression.Property(Expression.Convert(command, typeof(DbCommand)), typeof(DbCommand).GetProperty("Transaction")), Expression.Field(transaction, typeof(Transaction).GetField("DbTransaction"))),
                        Expression.New(returnCtor, command, transaction)
                        )
                    ));
                return Create<TDbConnection>(Expression.Lambda<Func<(DbCommand, IDisposable)>>(expr).Compile(), cmdExecuting);
            }
        }
        public static SqlDb Create<TDbConnection>(Func<(DbCommand, IDisposable)> getCommand, Action<DbCommand> cmdExecuting) where TDbConnection : DbConnection
        {
            if (getCommand == null)
                throw new ArgumentNullException(nameof(getCommand));

            Register<TDbConnection>(out var dbType);
            if (dbType == null)
                throw new NotSupportedException("Database");

            var ctor = dbType.GetConstructor(new[] { typeof(Type), typeof(Func<(DbCommand, IDisposable)>), typeof(Action<DbCommand>) });
            Debug.Assert(ctor != null);
            return (SqlDb)ctor.Invoke(new object[] { typeof(TDbConnection), getCommand, cmdExecuting });
        }

        //临时 TODO 优化
        //private static Func<Type, ParameterExpression, Expression> _OnResultExecuted;
        //public static void Read(Func<Type, ParameterExpression, Expression> onResultExecuted) 
        //{
        //    _OnResultExecuted = onResultExecuted;
        //}
        //public static void Read(Type type, ParameterExpression value, out Expression expression)
        //{
        //    expression = _OnResultExecuted?.Invoke(type, value);
        //}

        #region abstract
        public abstract int Execute(string sql);
        public abstract int Execute(string sql, object parameters);
        public abstract int Execute(string sql, IList<(string, object)> parameters);
        public abstract Task<int> ExecuteAsync(string sql);
        public abstract Task<int> ExecuteAsync(string sql, object parameters);
        public abstract Task<int> ExecuteAsync(string sql, IList<(string, object)> parameters);
        public abstract int ExecuteFormat(FormattableString sqlFormat);
        public abstract Task<int> ExecuteFormatAsync(FormattableString sqlFormat);
        public abstract (DbDataReader, IDisposable) ExecuteReader(string sql);
        public abstract (DbDataReader, IDisposable) ExecuteReader(string sql, object parameters);
        public abstract (DbDataReader, IDisposable) ExecuteReader(string sql, IList<(string, object)> parameters);
        public abstract (DbDataReader, IDisposable) ExecuteReaderFormat(FormattableString sqlFormat);
        public abstract Task<(DbDataReader, IDisposable)> ExecuteReaderAsync(string sql);
        public abstract Task<(DbDataReader, IDisposable)> ExecuteReaderAsync(string sql, object parameters);
        public abstract Task<(DbDataReader, IDisposable)> ExecuteReaderAsync(string sql, IList<(string, object)> parameters);
        public abstract Task<(DbDataReader, IDisposable)> ExecuteReaderFormatAsync(FormattableString sqlFormat);
        public abstract TEntity Read<TEntity>(DbDataReader dataReader);//TODO?
        public abstract int Insert<TEntity>(Expression<Func<SqlExpression, TEntity>> entity);
        public abstract int Insert<TEntity>(TEntity entity, Expression<Func<TEntity, object>> properties);
        public abstract int InsertRange<TEntity>(IList<TEntity> entities, Expression<Func<TEntity, object>> properties);
        public abstract Task<int> InsertAsync<TEntity>(Expression<Func<SqlExpression, TEntity>> entity);
        public abstract Task<int> InsertAsync<TEntity>(TEntity entity, Expression<Func<TEntity, object>> properties);
        public abstract Task<int> InsertRangeAsync<TEntity>(IList<TEntity> entities, Expression<Func<TEntity, object>> properties);
        public abstract TIdentity InsertIdentity<TEntity, TIdentity>(Expression<Func<SqlExpression, TEntity>> entity);
        public abstract TIdentity InsertIdentity<TEntity, TIdentity>(TEntity entity, Expression<Func<TEntity, object>> properties);
        public abstract Task<TIdentity> InsertIdentityAsync<TEntity, TIdentity>(Expression<Func<SqlExpression, TEntity>> entity);
        public abstract Task<TIdentity> InsertIdentityAsync<TEntity, TIdentity>(TEntity entity, Expression<Func<TEntity, object>> properties);
        public abstract int Delete<TEntity>(object identity);
        public abstract int Delete<TEntity>(Expression<Func<TEntity, SqlExpression, bool>> where);
        public abstract Task<int> DeleteAsync<TEntity>(object identity);
        public abstract Task<int> DeleteAsync<TEntity>(Expression<Func<TEntity, SqlExpression, bool>> where);
        public abstract int Update<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> entity, object identity);
        public abstract int Update<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> entity, Expression<Func<TEntity, SqlExpression, bool>> where);
        public abstract int Update<TEntity>(TEntity entity, Expression<Func<TEntity, object>> properties);
        public abstract int Update<TEntity>(TEntity entity, Expression<Func<TEntity, object>> properties, Expression<Func<TEntity, SqlExpression, bool>> where);
        public abstract Task<int> UpdateAsync<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> entity, object identity);
        public abstract Task<int> UpdateAsync<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> entity, Expression<Func<TEntity, SqlExpression, bool>> where);
        public abstract Task<int> UpdateAsync<TEntity>(TEntity entity, Expression<Func<TEntity, object>> properties);
        public abstract Task<int> UpdateAsync<TEntity>(TEntity entity, Expression<Func<TEntity, object>> properties, Expression<Func<TEntity, SqlExpression, bool>> where);
        public abstract TEntity SelectSingle<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> select, object identity);
        public abstract Task<TEntity> SelectSingleAsync<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> select, object identity);
        public abstract TEntity SelectSingle<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where);
        public abstract Task<TEntity> SelectSingleAsync<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where);
        public abstract List<TEntity> Select<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy);
        public abstract Task<List<TEntity>> SelectAsync<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy);
        public abstract List<TEntity> Select<TEntity>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy);
        public abstract Task<List<TEntity>> SelectAsync<TEntity>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy);
        public abstract (List<TEntity>, int) SelectPaged<TEntity>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy);
        public abstract Task<(List<TEntity>, int)> SelectPagedAsync<TEntity>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy);
        public abstract TResult Select<TEntity, TResult>(Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<SqlExpression, object>> from, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Func<TEntity, SqlExpression, object>> groupBy, Expression<Func<TEntity, SqlExpression, bool>> having, Expression<Action<TEntity, SqlExpression>> orderBy);
        public abstract Task<TResult> SelectAsync<TEntity, TResult>(Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<SqlExpression, object>> from, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Func<TEntity, SqlExpression, object>> groupBy, Expression<Func<TEntity, SqlExpression, bool>> having, Expression<Action<TEntity, SqlExpression>> orderBy);
        public abstract TResult Select<TEntity, TResult>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<SqlExpression, object>> from, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Func<TEntity, SqlExpression, object>> groupBy, Expression<Func<TEntity, SqlExpression, bool>> having, Expression<Action<TEntity, SqlExpression>> orderBy);
        public abstract Task<TResult> SelectAsync<TEntity, TResult>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<SqlExpression, object>> from, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Func<TEntity, SqlExpression, object>> groupBy, Expression<Func<TEntity, SqlExpression, bool>> having, Expression<Action<TEntity, SqlExpression>> orderBy);
        public abstract (TResult, int) SelectPaged<TEntity, TResult>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<SqlExpression, object>> from, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy);
        public abstract Task<(TResult, int)> SelectPagedAsync<TEntity, TResult>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<SqlExpression, object>> from, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy);
        #endregion

        #region SqlServerDb
        public class SqlServerDb : SqlDb
        {
            private static object _Sync = new object();
            private static Type _ConnectionType;
            private static Type _DataReaderType;
            private static Action<DbCommand, string, object> _AddParameter;
            private static Func<MemberExpression, IReadOnlyList<object>> _DbMember;
            private static Func<MethodCallExpression, IReadOnlyList<object>> _DbMethod;
            [ThreadStatic] private static List<string> _CommandText;
            [ThreadStatic] private static List<(string, object)> _Parameters;
            private Func<(DbCommand, IDisposable)> _getCommand;
            private Action<DbCommand> _cmdExecuting;
            public static List<string> CommandText
            {
                get
                {
                    var commandText = _CommandText;
                    if (commandText == null)
                    {
                        commandText = new List<string>();
                        _CommandText = commandText;
                    }
                    else
                    {
                        commandText.Clear();
                    }
                    return commandText;
                }
            }
            public static List<(string, object)> Parameters
            {
                get
                {
                    var parameters = _Parameters;
                    if (parameters == null)
                    {
                        parameters = new List<(string, object)>();
                        _Parameters = parameters;
                    }
                    else
                    {
                        parameters.Clear();
                    }
                    return parameters;
                }
            }
            private static class Query
            {
                private static object _Sync;
                private static string[] _Parameters;
                private static string[] _Tables;
                private static Dictionary<Type, Action<DbCommand, object>> _Handlers;
                static Query()
                {
                    _Sync = new object();
                    _Parameters = new[]
                    {
                        "@p0","@p1","@p2","@p3","@p4","@p5","@p6","@p7",
                        "@p8","@p9","@p10","@p11","@p12","@p13","@p14","@p15",
                        "@p16","@p17","@p18","@p19","@p20","@p21","@p22","@p23",
                        "@p24","@p25","@p26","@p27","@p28","@p29","@p30","@p31",
                        "@p32","@p33","@p34","@p35","@p36","@p37","@p38","@p39",
                        "@p40","@p41","@p42","@p43","@p44","@p45","@p46","@p47",
                        "@p48","@p49","@p50","@p51","@p52","@p53","@p54","@p55",
                        "@p56","@p57","@p58","@p59","@p60","@p61","@p62","@p63"
                    };
                    _Tables = new[]
                    {
                        "t0","t1","t2","t3","t4","t5","t6","t7",
                        "t8","t9","t10","t11","t12","t13","t14","t15"
                    };
                    _Handlers = new Dictionary<Type, Action<DbCommand, object>>();
                }
                public static string GetParameter(int index)
                {
                    return index < _Parameters.Length ? _Parameters[index] : $"@p{index}";
                }
                public static string GetTable(int index)
                {
                    return index < _Tables.Length ? _Tables[index] : $"t{index}";
                }
                public static void AddParameter(DbCommand command, object objParameters)
                {
                    if (objParameters == null)
                        return;

                    var type = objParameters.GetType();
                    if (!_Handlers.TryGetValue(type, out var handler))
                    {
                        lock (_Sync)
                        {
                            if (!_Handlers.TryGetValue(type, out handler))
                            {
                                var cmd = Expression.Parameter(typeof(DbCommand), "cmd");
                                var objValue = Expression.Parameter(typeof(object), "objValue");
                                var tValue = Expression.Variable(type, "value");
                                var addParameter = typeof(SqlDbExtensions).GetMethod("AddParameter").MakeGenericMethod(_ConnectionType);
                                var properties = type.GetGeneralProperties(BindingFlags.Instance | BindingFlags.Public);
                                var exprs = new List<Expression>();
                                exprs.Add(Expression.Assign(tValue, Expression.Convert(objValue, type)));
                                foreach (var property in properties)
                                {
                                    if (!property.CanRead)
                                        continue;
                                    exprs.Add(Expression.Call(
                                        addParameter, cmd,
                                        Expression.Constant($"@{property.Name}"),
                                        Expression.Convert(Expression.Property(tValue, property), typeof(object))
                                        ));
                                }
                                handler = Expression.Lambda<Action<DbCommand, object>>(Expression.Block(new[] { tValue }, exprs), cmd, objValue).Compile();
                                var handlers = new Dictionary<Type, Action<DbCommand, object>>(_Handlers);
                                handlers.Add(type, handler);
                                _Handlers = handlers;
                            }
                        }
                    }
                    handler(command, objParameters);
                }
            }
            private static class Reader<TEntity>
            {
                public static Func<DbDataReader, TEntity> Handler;
            }
            public class DbTable
            {
                private class Comparer : IEqualityComparer<PropertyInfo>
                {
                    public bool Equals(PropertyInfo x, PropertyInfo y) =>
                        x.MetadataToken == y.MetadataToken && x.DeclaringType == y.DeclaringType;
                    public int GetHashCode(PropertyInfo obj) => obj.MetadataToken;
                }

                public string Name;
                public string Identity;
                public string Join;
                public Dictionary<PropertyInfo, string> Fields;
                public Dictionary<PropertyInfo, string> Columns;
                public Dictionary<PropertyInfo, DbTable> Tables;

                private static int _MaxDepth = 8;//TODO??_TypeReference
                private static Comparer _Comparer = new Comparer();
                private static object _Sync = new object();
                private static Dictionary<Type, DbTable> _Tables = new Dictionary<Type, DbTable>();
                public static DbTable Get(Type type)
                {
                    if (!_Tables.TryGetValue(type, out var table))
                    {
                        lock (_Sync)
                        {
                            if (!_Tables.TryGetValue(type, out table))
                            {
                                table = Get(0, null, null, type);
                            }
                        }
                    }
                    return table;
                }
                private static DbTable Get(int depth, string prefix, string join, Type type)
                {
                    var table = new DbTable();
                    table.Join = join;
                    table.Fields = new Dictionary<PropertyInfo, string>(_Comparer);
                    table.Columns = new Dictionary<PropertyInfo, string>(_Comparer);
                    table.Tables = new Dictionary<PropertyInfo, DbTable>(_Comparer);
                    var registerReader = typeof(SqlDbExtensions).GetMethod("RegisterDbReader", new[] { typeof(Type), typeof(ParameterExpression), typeof(ParameterExpression), typeof(Expression).MakeByRefType() })
                    .MakeGenericMethod(_DataReaderType);
                    var readerArgs = new object[] { null, Expression.Parameter(_DataReaderType), Expression.Parameter(typeof(int)), null };
                    SqlDbExtensions.RegisterTable(out var tableResolver);
                    SqlDbExtensions.RegisterIdentity(out var identityResolver);
                    SqlDbExtensions.RegisterProperty(out var propertyResolver);

                    var dataTableAttribute = type.GetCustomAttribute<DataTableAttribute>();
                    table.Name = $"[{dataTableAttribute?.Name ?? tableResolver(type)}]";
                    var identityProperty = identityResolver(type);
                    if (identityProperty != null)
                    {
                        var dataColumnAttribute = identityProperty.GetCustomAttribute<DataColumnAttribute>();
                        table.Identity = $"[{dataColumnAttribute?.Name ?? propertyResolver(identityProperty)}]";
                    }
                    var properties = type.GetGeneralProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    foreach (var property in properties)
                    {
                        if (!property.CanWrite)
                            continue;
                        if (property.IsDefined(typeof(IgnoreDataColumnAttribute)))
                            continue;
                        var dataColumnAttribute = property.GetCustomAttribute<DataColumnAttribute>();
                        readerArgs[0] = property.PropertyType;
                        registerReader.Invoke(null, readerArgs);
                        if (readerArgs[3] != null)
                        {
                            string columnName = dataColumnAttribute?.Name ?? propertyResolver(property);
                            table.Fields.Add(property, $"[{columnName}]");
                            table.Columns.Add(property, $"[{prefix}{columnName}]");
                        }
                        else
                        {
                            if (depth > _MaxDepth)
                                continue;
                            var joinIdentityProperty = identityResolver(property.PropertyType);
                            if (joinIdentityProperty == null)
                                continue;
                            var joinName = dataColumnAttribute?.Name;
                            if (joinName == null)
                            {
                                var onPropery = type.GetProperty($"{property.Name}{joinIdentityProperty.Name}", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                if (onPropery == null)
                                {
                                    if (joinIdentityProperty.Name == $"{type.Name}{identityProperty.Name}")
                                    {
                                        onPropery = identityProperty;
                                    }
                                    else
                                    {
                                        continue;
                                    }
                                }
                                dataColumnAttribute = onPropery.GetCustomAttribute<DataColumnAttribute>();
                                joinName = dataColumnAttribute?.Name ?? propertyResolver(onPropery);
                            }
                            joinName = $"[{joinName}]";
                            string columnName = propertyResolver(property);
                            var propertyPrefix = $"{prefix}{columnName}.";
                            var propertyTable = Get(depth + 1, propertyPrefix, joinName, property.PropertyType);
                            table.Tables.Add(property, propertyTable);
                        }
                    }
                    return table;
                }
            }
            public class DbEntity
            {
                public string Name;
                public DbTable Table;
                public Dictionary<PropertyInfo, DbEntity> Entities;
            }
            public class DbEntityContext
            {
                private int _table;
                private Dictionary<ParameterExpression, DbEntity> _entities;
                public DbEntityContext()
                {
                    _entities = new Dictionary<ParameterExpression, DbEntity>();
                }

                public DbEntity Add(ParameterExpression parameter)
                {
                    var entity = new DbEntity()
                    {
                        Name = Query.GetTable(_table++),
                        Table = DbTable.Get(parameter.Type),
                        Entities = new Dictionary<PropertyInfo, DbEntity>()
                    };
                    _entities.Add(parameter, entity);
                    return entity;
                }
                public void Add(ParameterExpression parameter, DbEntity entity)
                {
                    _entities.Add(parameter, entity);
                }
                public DbEntity Add(DbEntity entity, PropertyInfo property)
                {
                    if (entity.Entities.TryGetValue(property, out var propertyEntity))
                    {
                        return propertyEntity;
                    }
                    else if (entity.Table.Tables.TryGetValue(property, out var entityTable))
                    {
                        propertyEntity = new DbEntity();
                        propertyEntity.Name = Query.GetTable(_table++);
                        propertyEntity.Table = entityTable;
                        propertyEntity.Entities = new Dictionary<PropertyInfo, DbEntity>();
                        entity.Entities.Add(property, propertyEntity);
                        return propertyEntity;
                    }
                    return null;
                }
                public DbEntity Convert(Expression expression)
                {
                    if (expression == null)
                        return null;

                    switch (expression.NodeType)
                    {
                        case ExpressionType.Convert:
                            return Convert(((UnaryExpression)expression).Operand);
                        case ExpressionType.Parameter:
                            {
                                _entities.TryGetValue((ParameterExpression)expression, out var entity);
                                return entity;
                            }
                        case ExpressionType.MemberAccess:
                            {
                                var expr = (MemberExpression)expression;
                                if (expr.Member is PropertyInfo property)
                                {
                                    var propertyEntity = Convert(expr.Expression);
                                    if (propertyEntity != null)
                                    {
                                        if (propertyEntity.Entities.TryGetValue(property, out var entity))
                                        {
                                            return entity;
                                        }
                                        else if (propertyEntity.Table.Tables.TryGetValue(property, out var propertyTable))
                                        {
                                            entity = new DbEntity();
                                            entity.Name = Query.GetTable(_table++);
                                            entity.Table = propertyTable;
                                            entity.Entities = new Dictionary<PropertyInfo, DbEntity>();
                                            propertyEntity.Entities.Add(property, entity);
                                            return entity;
                                        }
                                    }
                                }
                            }
                            return null;
                        default:
                            return null;
                    }
                }
            }
            public static Dictionary<PropertyInfo, string> GetFields(DbTable table, Expression expression)
            {
                switch (expression.NodeType)
                {
                    case ExpressionType.Parameter:
                        return table.Fields;
                    case ExpressionType.Convert:
                        expression = ((UnaryExpression)expression).Operand;
                        if (expression.NodeType == ExpressionType.MemberAccess)
                            goto case ExpressionType.MemberAccess;
                        break;
                    case ExpressionType.MemberAccess:
                        {
                            var expr = (MemberExpression)expression;
                            if (expr.Member is PropertyInfo property
                                && table.Fields.TryGetValue(property, out var field))
                            {
                                return new Dictionary<PropertyInfo, string>()
                                {
                                    { property,field}
                                };
                            }
                        }
                        break;
                    case ExpressionType.New:
                        {
                            var fields = new Dictionary<PropertyInfo, string>();
                            var exprArgs = ((NewExpression)expression).Arguments;
                            for (int i = 0; i < exprArgs.Count; i++)
                            {
                                var expr = exprArgs[i].NodeType == ExpressionType.Convert ?
                                    ((UnaryExpression)exprArgs[i]).Operand as MemberExpression
                                    : exprArgs[i] as MemberExpression;
                                if (expr != null && expr.Member is PropertyInfo property
                                    && table.Fields.TryGetValue(property, out var field))
                                {
                                    fields.Add(property, field);
                                }
                            }
                            return fields;
                        }
                    case ExpressionType.Call:
                        {
                            if (SqlDbExtensions.TryExcept((MethodCallExpression)expression, out var except))
                            {
                                switch (except.NodeType)
                                {
                                    case ExpressionType.Convert:
                                        except = ((UnaryExpression)except).Operand;
                                        if (except.NodeType == ExpressionType.MemberAccess)
                                            goto case ExpressionType.MemberAccess;
                                        break;
                                    case ExpressionType.MemberAccess:
                                        {
                                            var expr = (MemberExpression)except;
                                            if (expr.Member is PropertyInfo property && table.Fields.ContainsKey(property))
                                            {
                                                var fields = new Dictionary<PropertyInfo, string>(table.Fields);
                                                fields.Remove(property);
                                                return fields;
                                            }
                                        }
                                        break;
                                    case ExpressionType.New:
                                        {
                                            var fields = new Dictionary<PropertyInfo, string>(table.Fields);
                                            var exprArgs = ((NewExpression)except).Arguments;
                                            for (int i = 0; i < exprArgs.Count; i++)
                                            {
                                                var expr = exprArgs[i].NodeType == ExpressionType.Convert ?
                                                    ((UnaryExpression)exprArgs[i]).Operand as MemberExpression
                                                    : exprArgs[i] as MemberExpression;
                                                if (expr != null && expr.Member is PropertyInfo property && fields.ContainsKey(property))
                                                {
                                                    fields.Remove(property);
                                                }
                                            }
                                            return fields;
                                        }
                                    default:
                                        break;
                                }
                                return new Dictionary<PropertyInfo, string>(table.Fields);
                            }
                        }
                        break;
                    default:
                        break;
                }
                return new Dictionary<PropertyInfo, string>();
            }
            public static void Join(DbEntity entity, DbEntity joinEntity, List<string> sql)
            {
                sql.Add(" LEFT JOIN ");
                sql.Add(joinEntity.Table.Name);
                sql.Add(" ");
                sql.Add(joinEntity.Name);
                sql.Add(" ON ");
                sql.Add(joinEntity.Name);
                sql.Add(".");
                sql.Add(joinEntity.Table.Identity);
                sql.Add("=");
                sql.Add(entity.Name);
                sql.Add(".");
                sql.Add(joinEntity.Table.Join);
                foreach (var item in joinEntity.Entities)
                {
                    Join(joinEntity, item.Value, sql);
                }
            }
            public static void Convert(Expression expression, List<string> sql, List<(string, object)> parameters, DbEntityContext context)
            {
                switch (expression.NodeType)
                {
                    case ExpressionType.Parameter:
                        {
                            var entity = context.Convert(expression);
                            if (entity != null)
                            {
                                foreach ((_, var field) in entity.Table.Fields)
                                {
                                    sql.Add(entity.Name);
                                    sql.Add(".");
                                    sql.Add(field);
                                    sql.Add(",");
                                }
                                sql.RemoveAt(sql.Count - 1);
                            }
                        }
                        break;
                    case ExpressionType.Constant:
                        {
                            var value = ((ConstantExpression)expression).Value;
                            var parameter = Query.GetParameter(parameters.Count);
                            sql.Add(parameter);
                            parameters.Add((parameter, value));
                        }
                        break;
                    case ExpressionType.MemberAccess:
                        {
                            var expr = (MemberExpression)expression;
                            var exprObjs = _DbMember(expr);
                            if (exprObjs != null)
                            {
                                for (int i = 0; i < exprObjs.Count; i++)
                                {
                                    var exprObj = exprObjs[i] as Expression;
                                    if (exprObj != null)
                                        Convert(exprObj, sql, parameters, context);
                                    else
                                        sql.Add((string)exprObjs[i]);
                                }
                                break;
                            }
                            if (expr.Member is PropertyInfo property)
                            {
                                var entity = context.Convert(expr.Expression);
                                if (entity != null)
                                {
                                    if (entity.Table.Fields.TryGetValue(property, out var field))
                                    {
                                        sql.Add(entity.Name);
                                        sql.Add(".");
                                        sql.Add(field);
                                    }
                                    else
                                    {
                                        var propertyEntity = context.Add(entity, property);
                                        if (propertyEntity != null)
                                        {
                                            foreach ((_, var propertyField) in propertyEntity.Table.Fields)
                                            {
                                                sql.Add(propertyEntity.Name);
                                                sql.Add(".");
                                                sql.Add(propertyField);
                                                sql.Add(",");
                                            }
                                            sql.RemoveAt(sql.Count - 1);
                                        }
                                    }
                                    break;
                                }
                            }
                            var value = expr.Invoke();
                            var parameter = Query.GetParameter(parameters.Count);
                            sql.Add(parameter);
                            parameters.Add((parameter, value));
                        }
                        break;
                    case ExpressionType.Call:
                        {
                            var expr = (MethodCallExpression)expression;
                            var exprObjs = _DbMethod(expr);
                            if (exprObjs != null)
                            {
                                for (int i = 0; i < exprObjs.Count; i++)
                                {
                                    var exprObj = exprObjs[i] as Expression;
                                    if (exprObj != null)
                                        Convert(exprObj, sql, parameters, context);
                                    else
                                        sql.Add((string)exprObjs[i]);
                                }
                            }
                            else if (SqlDbExtensions.TrySelect(expr, out var objSelect))
                            {
                                Select(objSelect, sql, parameters, context);
                            }
                            else if (SqlDbExtensions.TrySelect(expr, out var select, out var where, out var groupBy, out var having, out var orderBy))
                            {
                                sql.Add("(");
                                sql.Add(Select(select, where, groupBy, having, orderBy, parameters, context));
                                sql.Add(")");
                            }
                            else if (SqlDbExtensions.TrySelect(expr, out var offset, out var fetch, out select, out where, out groupBy, out having, out orderBy))
                            {
                                sql.Add("(");
                                sql.Add(Select(offset, fetch, select, where, groupBy, having, orderBy, parameters, context));
                                sql.Add(")");
                            }
                            else if (SqlDbExtensions.TryNavigate(expr, out var navigate))
                            {
                                var entity = context.Convert(navigate);
                                if (entity == null)
                                {
                                    Convert(navigate, sql, parameters, context);
                                }
                                else
                                {
                                    Navigate(entity, sql, context);
                                    sql.RemoveAt(sql.Count - 1);
                                }
                            }
                            else
                            {
                                var value = expression.Invoke();
                                var parameter = Query.GetParameter(parameters.Count);
                                sql.Add(parameter);
                                parameters.Add((parameter, value));
                            }
                        }
                        break;
                    case ExpressionType.NewArrayInit:
                        {
                            var argExprs = ((NewArrayExpression)expression).Expressions;
                            Convert(argExprs[0], sql, parameters, context);
                            for (int i = 1; i < argExprs.Count; i++)
                            {
                                sql.Add(",");
                                Convert(argExprs[i], sql, parameters, context);
                            }
                        }
                        break;
                    case ExpressionType.Convert:
                        Convert(((UnaryExpression)expression).Operand, sql, parameters, context);
                        break;
                    case ExpressionType.Not:
                        sql.Add("NOT ");
                        Convert(((UnaryExpression)expression).Operand, sql, parameters, context);
                        break;
                    case ExpressionType.Equal:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" = ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.NotEqual:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" <> ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.GreaterThan:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" > ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.GreaterThanOrEqual:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" >= ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.LessThan:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" < ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.LessThanOrEqual:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" <= ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.AndAlso:
                        sql.Add("(");
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" AND ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        sql.Add(")");
                        break;
                    case ExpressionType.OrElse:
                        sql.Add("(");
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" OR ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        sql.Add(")");
                        break;
                    case ExpressionType.And:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" & ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.Or:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" | ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.Add:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" + ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.Subtract:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" - ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.Multiply:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" * ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.Divide:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" / ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.Modulo:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" % ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.Coalesce:
                        sql.Add("ISNULL(");
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(",");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        sql.Add(")");
                        break;
                    case ExpressionType.Conditional:
                        sql.Add("CASE WHEN ");
                        Convert(((ConditionalExpression)expression).Test, sql, parameters, context);
                        sql.Add(" THEN ");
                        Convert(((ConditionalExpression)expression).IfTrue, sql, parameters, context);
                        sql.Add(" ELSE ");
                        Convert(((ConditionalExpression)expression).IfFalse, sql, parameters, context);
                        sql.Add(" END");
                        break;
                    default:
                        throw new NotSupportedException(expression.ToString());
                }
            }
            public static void Navigate(DbEntity entity, List<string> sql, DbEntityContext context)
            {
                foreach ((_, var field) in entity.Table.Fields)
                {
                    sql.Add(entity.Name);
                    sql.Add(".");
                    sql.Add(field);
                    sql.Add(",");
                }
                foreach ((var property, _) in entity.Table.Tables)
                {
                    var propertyEntity = context.Add(entity, property);
                    if (propertyEntity != null)
                        Navigate(propertyEntity, sql, context);
                }
            }
            public static void Navigate(DbTable table, DbEntity entity, List<string> sql, DbEntityContext context)
            {
                foreach ((var property, var field) in entity.Table.Fields)
                {
                    sql.Add(entity.Name);
                    sql.Add(".");
                    sql.Add(field);
                    sql.Add(" AS ");
                    sql.Add(entity.Table.Columns[property]);
                    sql.Add(",");
                }
                foreach ((var property, var propertyTable) in table.Tables)
                {
                    var propertyEntity = context.Add(entity, property);
                    if (propertyEntity != null)
                        Navigate(propertyTable, propertyEntity, sql, context);
                }
            }
            public static void Select(DbTable table, PropertyInfo member, Expression expression, List<string> sql, List<(string, object)> parameters, DbEntityContext context)
            {
                if (table.Columns.TryGetValue(member, out var column))
                {
                    Convert(expression, sql, parameters, context);
                    sql.Add(" AS ");
                    sql.Add(column);
                    sql.Add(",");
                }
                else if (table.Tables.TryGetValue(member, out var memberTable))
                {
                    switch (expression.NodeType)
                    {
                        case ExpressionType.MemberInit:
                            {
                                var expr = (MemberInitExpression)expression;
                                var bindings = expr.Bindings;
                                for (int i = 0; i < bindings.Count; i++)
                                {
                                    var memberAssignment = (MemberAssignment)bindings[i];
                                    if (memberAssignment.Member is PropertyInfo memberMember)
                                        Select(memberTable, memberMember, memberAssignment.Expression, sql, parameters, context);
                                }
                            }
                            break;
                        case ExpressionType.Call:
                            {
                                var expr = (MethodCallExpression)expression;
                                if (SqlDbExtensions.TryNavigate(expr, out var navigate))
                                {
                                    var entity = context.Convert(navigate);
                                    if (entity != null)
                                    {
                                        Navigate(memberTable, entity, sql, context);
                                        break;
                                    }
                                }
                            }
                            break;
                        default:
                            {
                                var entity = context.Convert(expression);
                                if (entity != null)
                                {
                                    foreach ((var property, var field) in entity.Table.Fields)
                                    {
                                        sql.Add(entity.Name);
                                        sql.Add(".");
                                        sql.Add(field);
                                        sql.Add(" AS ");
                                        sql.Add(memberTable.Columns[property]);
                                        sql.Add(",");
                                    }
                                }
                            }
                            break;
                    }

                }
            }
            public static void Select(Expression expression, List<string> sql, List<(string, object)> parameters, DbEntityContext context)
            {
                switch (expression.NodeType)
                {
                    case ExpressionType.Convert:
                        Select(((UnaryExpression)expression).Operand, sql, parameters, context);
                        break;
                    case ExpressionType.MemberInit:
                        {
                            var expr = (MemberInitExpression)expression;
                            var table = DbTable.Get(expr.Type);
                            var bindings = expr.Bindings;
                            for (int i = 0; i < bindings.Count; i++)
                            {
                                var memberAssignment = (MemberAssignment)bindings[i];
                                if (memberAssignment.Member is PropertyInfo member)
                                    Select(table, member, memberAssignment.Expression, sql, parameters, context);
                            }
                            sql.RemoveAt(sql.Count - 1);
                        }
                        break;
                    case ExpressionType.NewArrayInit:
                        {
                            var argExprs = ((NewArrayExpression)expression).Expressions;
                            Convert(argExprs[0], sql, parameters, context);
                            for (int i = 1; i < argExprs.Count; i++)
                            {
                                sql.Add(",");
                                Select(argExprs[i], sql, parameters, context);
                            }
                        }
                        break;
                    case ExpressionType.Call:
                        {
                            var expr = (MethodCallExpression)expression;
                            if (SqlDbExtensions.TryNavigate(expr, out var navigate))
                            {
                                var entity = context.Convert(navigate);
                                if (entity == null)
                                {
                                    Convert(navigate, sql, parameters, context);
                                }
                                else
                                {
                                    Navigate(entity.Table, entity, sql, context);
                                    sql.RemoveAt(sql.Count - 1);
                                }
                            }
                            else
                            {
                                Convert(expression, sql, parameters, context);
                            }
                        }
                        break;
                    default:
                        {
                            var entity = context.Convert(expression);
                            if (entity == null)
                            {
                                Convert(expression, sql, parameters, context);
                            }
                            else
                            {
                                foreach ((var property, var field) in entity.Table.Fields)
                                {
                                    sql.Add(entity.Name);
                                    sql.Add(".");
                                    sql.Add(field);
                                    sql.Add(" AS ");
                                    sql.Add(entity.Table.Columns[property]);
                                    sql.Add(",");
                                }
                                sql.RemoveAt(sql.Count - 1);
                            }
                        }
                        break;
                }
            }
            public static string Select(LambdaExpression select, LambdaExpression where, LambdaExpression groupBy, LambdaExpression having, LambdaExpression orderBy, List<(string, object)> parameters, DbEntityContext context)
            {
                var sql = new List<string>();
                var entity = context.Add(select.Parameters[0]);//context.Add(select, parameters, context)
                if (where != null)
                {
                    context.Add(where.Parameters[0], entity);
                    sql.Add(" WHERE ");
                    Convert(where.Body, sql, parameters, context);
                }
                if (groupBy != null)
                {
                    context.Add(groupBy.Parameters[0], entity);
                    sql.Add(" GROUP BY ");
                    Convert(groupBy.Body, sql, parameters, context);
                }
                if (having != null)
                {
                    context.Add(having.Parameters[0], entity);
                    sql.Add(" HAVING ");
                    Convert(having.Body, sql, parameters, context);
                }
                if (orderBy != null)
                {
                    context.Add(orderBy.Parameters[0], entity);
                    sql.Add(" ORDER BY ");
                    Convert(orderBy.Body, sql, parameters, context);
                }
                var index = sql.Count;
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);
                //Convert(select.Body, sql, parameters, context);

                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }
                return sql.Concat((index, sql.Count), (0, index));
            }
            public static string Select(int offset, int fetch, LambdaExpression select, LambdaExpression where, LambdaExpression groupBy, LambdaExpression having, LambdaExpression orderBy, List<(string, object)> parameters, DbEntityContext context)
            {
                var sql = new List<string>();
                var entity = context.Add(select.Parameters[0]);
                if (where != null)
                {
                    context.Add(where.Parameters[0], entity);
                    sql.Add(" WHERE ");
                    Convert(where.Body, sql, parameters, context);
                }
                if (groupBy != null)
                {
                    context.Add(groupBy.Parameters[0], entity);
                    sql.Add(" GROUP BY ");
                    Convert(groupBy.Body, sql, parameters, context);
                }
                if (having != null)
                {
                    context.Add(having.Parameters[0], entity);
                    sql.Add(" HAVING ");
                    Convert(having.Body, sql, parameters, context);
                }
                if (orderBy != null)
                {
                    context.Add(orderBy.Parameters[0], entity);
                    sql.Add(" ORDER BY ");
                    Convert(orderBy.Body, sql, parameters, context);
                }
                sql.Add($" OFFSET {offset} ROWS");
                if (fetch > 0)
                    sql.Add($" FETCH NEXT {fetch} ROWS ONLY");
                var index = sql.Count;
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);
                //Convert(select.Body, sql, parameters, context);

                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }
                return sql.Concat((index, sql.Count), (0, index));
            }
            public static string Insert(LambdaExpression entity, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var context = new DbEntityContext();
                var table = DbTable.Get(entity.Body.Type);
                sql.Add("INSERT INTO ");
                sql.Add(table.Name);
                sql.Add("(");
                if (entity.Body.NodeType == ExpressionType.MemberInit)
                {
                    var bindings = ((MemberInitExpression)entity.Body).Bindings;
                    Span<int> indexs = bindings.Count < 256 ? stackalloc int[bindings.Count] : new int[bindings.Count];
                    var index = 0;
                    //fields
                    {
                        var i = 0;
                        for (; i < bindings.Count; i++)
                        {
                            var memberAssignment = (MemberAssignment)bindings[i];
                            if (table.Fields.TryGetValue((PropertyInfo)memberAssignment.Member, out var name))
                            {
                                sql.Add(name);
                                indexs[index++] = i++;
                                break;
                            }
                        }
                        for (; i < bindings.Count; i++)
                        {
                            var memberAssignment = (MemberAssignment)bindings[i];
                            if (table.Fields.TryGetValue((PropertyInfo)memberAssignment.Member, out var name))
                            {
                                sql.Add(",");
                                sql.Add(name);
                                indexs[index++] = i;
                            }
                        }
                    }
                    sql.Add(") VALUES (");
                    //values
                    {
                        if (index > 0)
                        {
                            Convert(((MemberAssignment)bindings[indexs[0]]).Expression, sql, parameters, context);
                        }
                        for (int i = 1; i < index; i++)
                        {
                            sql.Add(",");
                            Convert(((MemberAssignment)bindings[indexs[i]]).Expression, sql, parameters, context);
                        }
                    }
                }
                sql.Add(")");
                return sql.Concat();
            }
            public static string Insert(object entity, LambdaExpression properties, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var table = DbTable.Get(properties.Parameters[0].Type);
                var fields = GetFields(table, properties.Body);
                sql.Add("INSERT INTO ");
                sql.Add(table.Name);
                sql.Add("(");
                var enumerator = fields.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    sql.Add(enumerator.Current.Value);
                }
                while (enumerator.MoveNext())
                {
                    sql.Add(",");
                    sql.Add(enumerator.Current.Value);
                }
                sql.Add(") VALUES (");
                enumerator = fields.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    var value = enumerator.Current.Key.GetValue(entity);
                    var parameter = Query.GetParameter(parameters.Count);
                    parameters.Add((parameter, value));
                    sql.Add(parameter);
                }
                while (enumerator.MoveNext())
                {
                    sql.Add(",");
                    var value = enumerator.Current.Key.GetValue(entity);
                    var parameter = Query.GetParameter(parameters.Count);
                    parameters.Add((parameter, value));
                    sql.Add(parameter);
                }
                sql.Add(")");
                return sql.Concat();
            }
            public static string InsertIdentity(LambdaExpression entity, List<(string, object)> parameters)
            {
                var sql = Insert(entity, parameters);
                return sql + ";SELECT SCOPE_IDENTITY()";
            }
            public static string InsertIdentity(object entity, LambdaExpression properties, List<(string, object)> parameters)
            {
                var sql = Insert(entity, properties, parameters);
                return sql + ";SELECT SCOPE_IDENTITY()";
            }
            public static string Delete(DbTable table, object identity, List<(string, object)> parameters)
            {
                var sql = CommandText;
                sql.Add("DELETE FROM ");
                sql.Add(table.Name);
                sql.Add(" WHERE ");
                sql.Add(table.Name);
                sql.Add(".");
                sql.Add(table.Identity);
                if (identity == null)
                {
                    sql.Add(" IS NULL");
                }
                else
                {
                    var parameter = Query.GetParameter(parameters.Count);
                    sql.Add(" = ");
                    sql.Add(parameter);
                    parameters.Add((parameter, identity));
                }
                return sql.Concat();
            }
            public static string Delete(DbTable table, LambdaExpression where, List<(string, object)> parameters)
            {
                var sql = CommandText;
                sql.Add("DELETE FROM ");
                sql.Add(table.Name);
                sql.Add(" WHERE ");
                var context = new DbEntityContext();
                context.Add(where.Parameters[0], new DbEntity()
                {
                    Name = table.Name,
                    Table = table,
                    Entities = new Dictionary<PropertyInfo, DbEntity>()
                });
                Convert(where.Body, sql, parameters, context);
                return sql.Concat();
            }
            public static string Update(LambdaExpression entity, object identity, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var table = DbTable.Get(entity.Body.Type);
                var context = new DbEntityContext();
                context.Add(entity.Parameters[0], new DbEntity()
                {
                    Name = table.Name,
                    Table = table,
                    Entities = new Dictionary<PropertyInfo, DbEntity>()
                });
                sql.Add("UPDATE ");
                sql.Add(table.Name);
                sql.Add(" SET ");
                if (entity.Body.NodeType == ExpressionType.MemberInit)
                {
                    var bindings = ((MemberInitExpression)entity.Body).Bindings;
                    var i = 0;
                    for (; i < bindings.Count;)
                    {
                        var memberAssignment = (MemberAssignment)bindings[i++];
                        if (table.Fields.TryGetValue((PropertyInfo)memberAssignment.Member, out var name))
                        {
                            sql.Add(name);
                            sql.Add("=");
                            Convert(memberAssignment.Expression, sql, parameters, context);
                            break;
                        }
                    }
                    for (; i < bindings.Count;)
                    {
                        var memberAssignment = (MemberAssignment)bindings[i++];
                        if (table.Fields.TryGetValue((PropertyInfo)memberAssignment.Member, out var name))
                        {
                            sql.Add(",");
                            sql.Add(name);
                            sql.Add("=");
                            Convert(memberAssignment.Expression, sql, parameters, context);
                        }
                    }
                }
                sql.Add(" WHERE ");
                sql.Add(table.Name);
                sql.Add(".");
                sql.Add(table.Identity);
                if (identity == null)
                {
                    sql.Add(" IS NULL");
                }
                else
                {
                    var parameter = Query.GetParameter(parameters.Count);
                    sql.Add(" = ");
                    sql.Add(parameter);
                    parameters.Add((parameter, identity));
                }
                return sql.Concat();
            }
            public static string Update(LambdaExpression entity, LambdaExpression where, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var table = DbTable.Get(entity.Body.Type);
                var context = new DbEntityContext();
                var dbEntity = new DbEntity()
                {
                    Name = table.Name,
                    Table = table,
                    Entities = new Dictionary<PropertyInfo, DbEntity>()
                };
                context.Add(entity.Parameters[0], dbEntity);
                sql.Add("UPDATE ");
                sql.Add(table.Name);
                sql.Add(" SET ");
                if (entity.Body.NodeType == ExpressionType.MemberInit)
                {
                    var bindings = ((MemberInitExpression)entity.Body).Bindings;
                    var i = 0;
                    for (; i < bindings.Count;)
                    {
                        var memberAssignment = (MemberAssignment)bindings[i++];
                        if (table.Fields.TryGetValue((PropertyInfo)memberAssignment.Member, out var name))
                        {
                            sql.Add(name);
                            sql.Add("=");
                            Convert(memberAssignment.Expression, sql, parameters, context);
                            break;
                        }
                    }
                    for (; i < bindings.Count;)
                    {
                        var memberAssignment = (MemberAssignment)bindings[i++];
                        if (table.Fields.TryGetValue((PropertyInfo)memberAssignment.Member, out var name))
                        {
                            sql.Add(",");
                            sql.Add(name);
                            sql.Add("=");
                            Convert(memberAssignment.Expression, sql, parameters, context);
                        }
                    }
                }
                sql.Add(" WHERE ");
                context.Add(where.Parameters[0], dbEntity);
                Convert(where.Body, sql, parameters, context);
                return sql.Concat();
            }
            public static string Update(object entity, LambdaExpression properties, object identity, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var table = DbTable.Get(properties.Parameters[0].Type);
                var fields = GetFields(table, properties.Body);
                sql.Add("UPDATE ");
                sql.Add(table.Name);
                sql.Add(" SET ");
                var enumerator = fields.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    var value = enumerator.Current.Key.GetValue(entity);
                    var parameter = Query.GetParameter(parameters.Count);
                    parameters.Add((parameter, value));
                    sql.Add(enumerator.Current.Value);
                    sql.Add("=");
                    sql.Add(parameter);
                }
                while (enumerator.MoveNext())
                {
                    sql.Add(",");
                    var value = enumerator.Current.Key.GetValue(entity);
                    var parameter = Query.GetParameter(parameters.Count);
                    parameters.Add((parameter, value));
                    sql.Add(enumerator.Current.Value);
                    sql.Add("=");
                    sql.Add(parameter);
                }
                sql.Add(" WHERE ");
                sql.Add(table.Name);
                sql.Add(".");
                sql.Add(table.Identity);
                if (identity == null)
                {
                    sql.Add(" IS NULL");
                }
                else
                {
                    var parameter = Query.GetParameter(parameters.Count);
                    sql.Add(" = ");
                    sql.Add(parameter);
                    parameters.Add((parameter, identity));
                }
                return sql.Concat();
            }
            public static string Update(object entity, LambdaExpression properties, LambdaExpression where, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var table = DbTable.Get(properties.Parameters[0].Type);
                var fields = GetFields(table, properties.Body);
                sql.Add("UPDATE ");
                sql.Add(table.Name);
                sql.Add(" SET ");
                var enumerator = fields.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    var value = enumerator.Current.Key.GetValue(entity);
                    var parameter = Query.GetParameter(parameters.Count);
                    parameters.Add((parameter, value));
                    sql.Add(enumerator.Current.Value);
                    sql.Add("=");
                    sql.Add(parameter);
                }
                while (enumerator.MoveNext())
                {
                    sql.Add(",");
                    var value = enumerator.Current.Key.GetValue(entity);
                    var parameter = Query.GetParameter(parameters.Count);
                    parameters.Add((parameter, value));
                    sql.Add(enumerator.Current.Value);
                    sql.Add("=");
                    sql.Add(parameter);
                }
                sql.Add(" WHERE ");
                var context = new DbEntityContext();
                context.Add(where.Parameters[0], new DbEntity()
                {
                    Name = table.Name,
                    Table = table,
                    Entities = new Dictionary<PropertyInfo, DbEntity>()
                });
                Convert(where.Body, sql, parameters, context);
                return sql.Concat();
            }
            public static string SelectSingle(LambdaExpression select, object identity, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var context = new DbEntityContext();
                var entity = context.Add(select.Parameters[0]);
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);

                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }
                sql.Add(" WHERE ");
                sql.Add(entity.Name);
                sql.Add(".");
                sql.Add(entity.Table.Identity);
                if (identity == null)
                {
                    sql.Add(" IS NULL");
                }
                else
                {
                    var parameter = Query.GetParameter(parameters.Count);
                    sql.Add(" = ");
                    sql.Add(parameter);
                    parameters.Add((parameter, identity));
                }
                return sql.Concat();
            }
            public static string SelectSingle(LambdaExpression select, LambdaExpression where, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var context = new DbEntityContext();
                var entity = context.Add(select.Parameters[0]);
                if (where != null)
                {
                    context.Add(where.Parameters[0], entity);
                    sql.Add(" WHERE ");
                    Convert(where.Body, sql, parameters, context);
                }
                var index = sql.Count;
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);

                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }

                return sql.Concat((index, sql.Count), (0, index));
            }
            public static string Select(LambdaExpression select, LambdaExpression where, LambdaExpression orderBy, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var context = new DbEntityContext();
                var entity = context.Add(select.Parameters[0]);
                if (where != null)
                {
                    context.Add(where.Parameters[0], entity);
                    sql.Add(" WHERE ");
                    Convert(where.Body, sql, parameters, context);
                }
                if (orderBy != null)
                {
                    context.Add(orderBy.Parameters[0], entity);
                    sql.Add(" ORDER BY ");
                    Convert(orderBy.Body, sql, parameters, context);
                }
                var index = sql.Count;
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);

                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }
                return sql.Concat((index, sql.Count), (0, index));
            }
            public static string Select(int offset, int fetch, LambdaExpression select, LambdaExpression where, LambdaExpression orderBy, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var context = new DbEntityContext();
                var entity = context.Add(select.Parameters[0]);
                if (where != null)
                {
                    context.Add(where.Parameters[0], entity);
                    sql.Add(" WHERE ");
                    Convert(where.Body, sql, parameters, context);
                }
                if (orderBy != null)
                {
                    context.Add(orderBy.Parameters[0], entity);
                    sql.Add(" ORDER BY ");
                    Convert(orderBy.Body, sql, parameters, context);
                }
                sql.Add($" OFFSET {offset} ROWS FETCH NEXT {fetch} ROWS ONLY");
                var index = sql.Count;
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);

                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }
                return sql.Concat((index, sql.Count), (0, index));
            }
            public static string SelectPaged(int offset, int fetch, LambdaExpression select, LambdaExpression from, LambdaExpression where, LambdaExpression orderBy, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var context = new DbEntityContext();
                var entity = context.Add(select.Parameters[0]);
                if (from != null)
                {
                    var fromSql = new List<string>();
                    Convert(from.Body, fromSql, parameters, context);
                    entity.Table.Name = fromSql.Concat();
                }
                if (where != null)
                {
                    context.Add(where.Parameters[0], entity);
                    sql.Add(" WHERE ");
                    Convert(where.Body, sql, parameters, context);
                }
                var whereIndex = sql.Count;
                if (orderBy != null)
                {
                    context.Add(orderBy.Parameters[0], entity);
                    sql.Add(" ORDER BY ");
                    Convert(orderBy.Body, sql, parameters, context);
                }
                sql.Add($" OFFSET {offset} ROWS FETCH NEXT {fetch} ROWS ONLY");
                var selectIndex = sql.Count;
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);
                var fromIndex = sql.Count;
                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }
                var countIndex = sql.Count;
                sql.Add(";SELECT COUNT(1)");
                return sql.Concat((selectIndex, countIndex), (0, selectIndex), (countIndex, sql.Count), (fromIndex, countIndex), (0, whereIndex));
            }
            public static string Select(LambdaExpression select, LambdaExpression from, LambdaExpression where, LambdaExpression groupBy, LambdaExpression having, LambdaExpression orderBy, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var context = new DbEntityContext();
                var entity = context.Add(select.Parameters[0]);
                if (from != null)
                {
                    var fromSql = new List<string>();
                    Convert(from.Body, fromSql, parameters, context);
                    entity.Table.Name = fromSql.Concat();
                }

                if (where != null)
                {
                    context.Add(where.Parameters[0], entity);
                    sql.Add(" WHERE ");
                    Convert(where.Body, sql, parameters, context);
                }
                if (groupBy != null)
                {
                    context.Add(groupBy.Parameters[0], entity);
                    sql.Add(" GROUP BY ");
                    Convert(groupBy.Body, sql, parameters, context);
                }
                if (having != null)
                {
                    context.Add(having.Parameters[0], entity);
                    sql.Add(" HAVING ");
                    Convert(having.Body, sql, parameters, context);
                }
                if (orderBy != null)
                {
                    context.Add(orderBy.Parameters[0], entity);
                    sql.Add(" ORDER BY ");
                    Convert(orderBy.Body, sql, parameters, context);
                }
                var index = sql.Count;
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);

                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }
                return sql.Concat((index, sql.Count), (0, index));
            }
            public static string Select(int offset, int fetch, LambdaExpression select, LambdaExpression from, LambdaExpression where, LambdaExpression groupBy, LambdaExpression having, LambdaExpression orderBy, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var context = new DbEntityContext();
                var entity = context.Add(select.Parameters[0]);
                if (from != null)
                {
                    var fromSql = new List<string>();
                    Convert(from.Body, fromSql, parameters, context);
                    entity.Table.Name = fromSql.Concat();
                }
                if (where != null)
                {
                    context.Add(where.Parameters[0], entity);
                    sql.Add(" WHERE ");
                    Convert(where.Body, sql, parameters, context);
                }
                if (groupBy != null)
                {
                    context.Add(groupBy.Parameters[0], entity);
                    sql.Add(" GROUP BY ");
                    Convert(groupBy.Body, sql, parameters, context);
                }
                if (having != null)
                {
                    context.Add(having.Parameters[0], entity);
                    sql.Add(" HAVING ");
                    Convert(having.Body, sql, parameters, context);
                }
                if (orderBy != null)
                {
                    context.Add(orderBy.Parameters[0], entity);
                    sql.Add(" ORDER BY ");
                    Convert(orderBy.Body, sql, parameters, context);
                }
                sql.Add($" OFFSET {offset} ROWS FETCH NEXT {fetch} ROWS ONLY");
                var index = sql.Count;
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);

                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }
                return sql.Concat((index, sql.Count), (0, index));
            }
            public SqlServerDb(Type connectionType, Func<(DbCommand, IDisposable)> getCommand, Action<DbCommand> cmdExecuting)
            {
                if (_ConnectionType == null)
                {
                    lock (_Sync)
                    {
                        if (_ConnectionType == null)
                        {
                            _ConnectionType = connectionType;
                            _DataReaderType = connectionType.GetMethod("CreateCommand", Type.EmptyTypes).ReturnType.
                                GetMethod("ExecuteReader", Type.EmptyTypes).ReturnType;

                            _AddParameter = typeof(SqlDbExtensions).GetMethod("AddParameter").MakeGenericMethod(connectionType).
                                CreateDelegate<Action<DbCommand, string, object>>();

                            var member = Expression.Parameter(typeof(MemberExpression), "member");
                            var registerMember = typeof(SqlDbExtensions).GetMethod("RegisterDbMember", new[] { typeof(MemberExpression), typeof(IReadOnlyList<object>).MakeByRefType() });
                            var memberObjs = Expression.Variable(typeof(IReadOnlyList<object>), "exprObjs");
                            _DbMember = Expression.Lambda<Func<MemberExpression, IReadOnlyList<object>>>(
                                Expression.Block(new[] { memberObjs },
                                Expression.Call(null, registerMember.MakeGenericMethod(connectionType), member, memberObjs),
                                memberObjs), member).Compile();

                            var method = Expression.Parameter(typeof(MethodCallExpression), "method");
                            var registerMethod = typeof(SqlDbExtensions).GetMethod("RegisterDbMethod", new[] { typeof(MethodCallExpression), typeof(IReadOnlyList<object>).MakeByRefType() });
                            var methodObjs = Expression.Variable(typeof(IReadOnlyList<object>), "exprObjs");
                            _DbMethod = Expression.Lambda<Func<MethodCallExpression, IReadOnlyList<object>>>(
                                Expression.Block(new[] { methodObjs },
                                Expression.Call(null, registerMethod.MakeGenericMethod(connectionType), method, methodObjs),
                                methodObjs), method).Compile();
                        }
                    }
                }
                _getCommand = getCommand;
                _cmdExecuting = cmdExecuting;
            }
            public override int Execute(string sql)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    return cmd.ExecuteNonQuery();
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            public override int Execute(string sql, object parameters)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    Query.AddParameter(cmd, parameters);
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    return cmd.ExecuteNonQuery();
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            public override int Execute(string sql, IList<(string, object)> parameters)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    if (parameters != null && parameters.Count > 0)
                    {
                        for (int i = 0; i < parameters.Count; i++)
                        {
                            (var name, var value) = parameters[i];
                            _AddParameter(cmd, name, value);
                        }
                    }
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    return cmd.ExecuteNonQuery();
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            public override async Task<int> ExecuteAsync(string sql)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    return await cmd.ExecuteNonQueryAsync();
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            public override async Task<int> ExecuteAsync(string sql, object parameters)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    Query.AddParameter(cmd, parameters);
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    return await cmd.ExecuteNonQueryAsync();
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            public override async Task<int> ExecuteAsync(string sql, IList<(string, object)> parameters)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    if (parameters != null && parameters.Count > 0)
                    {
                        for (int i = 0; i < parameters.Count; i++)
                        {
                            (var name, var value) = parameters[i];
                            _AddParameter(cmd, name, value);
                        }
                    }
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    return await cmd.ExecuteNonQueryAsync();
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            public override int ExecuteFormat(FormattableString sqlFormat)
            {
                if (sqlFormat == null)
                    throw new ArgumentNullException(nameof(sqlFormat));

                var parameters = Parameters;
                var args = sqlFormat.GetArguments();
                var names = new string[args.Length];
                for (int i = 0; i < args.Length; i++)
                {
                    var name = Query.GetParameter(parameters.Count);
                    parameters.Add((name, args[i]));
                    names[i] = name;
                }
                return Execute(string.Format(sqlFormat.Format, names), parameters);
            }
            public override Task<int> ExecuteFormatAsync(FormattableString sqlFormat)
            {
                if (sqlFormat == null)
                    throw new ArgumentNullException(nameof(sqlFormat));

                var parameters = Parameters;
                var args = sqlFormat.GetArguments();
                var names = new string[args.Length];
                for (int i = 0; i < args.Length; i++)
                {
                    var name = Query.GetParameter(parameters.Count);
                    parameters.Add((name, args[i]));
                    names[i] = name;
                }
                return ExecuteAsync(string.Format(sqlFormat.Format, names), parameters);
            }
            public override (DbDataReader, IDisposable) ExecuteReader(string sql)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    var reader = cmd.ExecuteReader();
                    return (reader, disposable);
                }
                catch
                {
                    disposable.Dispose();
                    throw;
                }
            }
            public override (DbDataReader, IDisposable) ExecuteReader(string sql, object parameters)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    Query.AddParameter(cmd, parameters);
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    var reader = cmd.ExecuteReader();
                    return (reader, disposable);
                }
                catch
                {
                    disposable.Dispose();
                    throw;
                }
            }
            public override (DbDataReader, IDisposable) ExecuteReader(string sql, IList<(string, object)> parameters)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    if (parameters != null && parameters.Count > 0)
                    {
                        for (int i = 0; i < parameters.Count; i++)
                        {
                            (var name, var value) = parameters[i];
                            _AddParameter(cmd, name, value);
                        }
                    }
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    var reader = cmd.ExecuteReader();
                    return (reader, disposable);
                }
                catch
                {
                    disposable.Dispose();
                    throw;
                }
            }
            public override (DbDataReader, IDisposable) ExecuteReaderFormat(FormattableString sqlFormat)
            {
                if (sqlFormat == null)
                    throw new ArgumentNullException(nameof(sqlFormat));

                var parameters = Parameters;
                var args = sqlFormat.GetArguments();
                var names = new string[args.Length];
                for (int i = 0; i < args.Length; i++)
                {
                    var name = Query.GetParameter(parameters.Count);
                    parameters.Add((name, args[i]));
                    names[i] = name;
                }
                return ExecuteReader(string.Format(sqlFormat.Format, names), parameters);
            }
            public override async Task<(DbDataReader, IDisposable)> ExecuteReaderAsync(string sql)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    var reader = await cmd.ExecuteReaderAsync();
                    return (reader, disposable);
                }
                catch
                {
                    disposable.Dispose();
                    throw;
                }
            }
            public override async Task<(DbDataReader, IDisposable)> ExecuteReaderAsync(string sql, object parameters)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    Query.AddParameter(cmd, parameters);
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    var reader = await cmd.ExecuteReaderAsync();
                    return (reader, disposable);
                }
                catch
                {
                    disposable.Dispose();
                    throw;
                }
            }
            public override async Task<(DbDataReader, IDisposable)> ExecuteReaderAsync(string sql, IList<(string, object)> parameters)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    if (parameters != null && parameters.Count > 0)
                    {
                        for (int i = 0; i < parameters.Count; i++)
                        {
                            (var name, var value) = parameters[i];
                            _AddParameter(cmd, name, value);
                        }
                    }
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    var reader = await cmd.ExecuteReaderAsync();
                    return (reader, disposable);
                }
                catch
                {
                    disposable.Dispose();
                    throw;
                }
            }
            public override Task<(DbDataReader, IDisposable)> ExecuteReaderFormatAsync(FormattableString sqlFormat)
            {
                if (sqlFormat == null)
                    throw new ArgumentNullException(nameof(sqlFormat));

                var parameters = Parameters;
                var args = sqlFormat.GetArguments();
                var names = new string[args.Length];
                for (int i = 0; i < args.Length; i++)
                {
                    var name = Query.GetParameter(parameters.Count);
                    parameters.Add((name, args[i]));
                    names[i] = name;
                }
                return ExecuteReaderAsync(string.Format(sqlFormat.Format, names), parameters);
            }
            public override TEntity Read<TEntity>(DbDataReader dataReader)
            {
                var handler = Reader<TEntity>.Handler;
                if (handler == null)
                {
                    lock (_Sync)
                    {
                        handler = Reader<TEntity>.Handler;
                        if (handler == null)
                        {
                            var register = typeof(SqlDbExtensions).GetMethod("RegisterDbReader", new[] { typeof(Type), typeof(ParameterExpression), typeof(Expression).MakeByRefType(), typeof(Delegate).MakeByRefType() }).MakeGenericMethod(_DataReaderType);
                            var dbReader = Expression.Parameter(typeof(DbDataReader), "dbReader");
                            var reader = Expression.Variable(_DataReaderType, "reader");
                            var args = new object[] { typeof(TEntity), reader, default(Expression), default(Delegate) };
                            register.Invoke(null, args);
                            var read = _DataReaderType.GetMethod("Read", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
                            var expr = Expression.Block(new[] { reader },
                                Expression.Assign(reader, Expression.Convert(dbReader, _DataReaderType)),
                                Expression.Condition(Expression.Call(reader, read), (Expression)args[2], Expression.Default(typeof(TEntity))));
                            handler = Expression.Lambda<Func<DbDataReader, TEntity>>(expr, dbReader).Compile();
                            Reader<TEntity>.Handler = handler;
                        }
                    }
                }
                return handler(dataReader);
            }
            public override int Insert<TEntity>(Expression<Func<SqlExpression, TEntity>> entity)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                var parameters = Parameters;
                var sql = Insert(entity, parameters);
                return Execute(sql, parameters);
            }
            public override int Insert<TEntity>(TEntity entity, Expression<Func<TEntity, object>> properties)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));

                var parameters = Parameters;
                var sql = Insert(entity, properties, parameters);
                return Execute(sql, parameters);
            }
            public override int InsertRange<TEntity>(IList<TEntity> entities, Expression<Func<TEntity, object>> properties)
            {
                if (entities == null)
                    throw new ArgumentNullException(nameof(entities));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));

                var result = 0;
                var sql = new List<string>();
                var parameters = new List<(string, object)>();
                var table = DbTable.Get(properties.Parameters[0].Type);
                var fields = GetFields(table, properties.Body);
                for (int index = 0; ;)
                {
                    if (index == entities.Count)
                        break;
                    var entity = entities[index++];
                    var count = 1;
                    var fieldCount = fields.Count;
                    sql.Clear();
                    parameters.Clear();
                    sql.Add("INSERT INTO ");
                    sql.Add(table.Name);
                    sql.Add("(");
                    var enumerator = fields.GetEnumerator();
                    if (enumerator.MoveNext())
                    {
                        sql.Add(enumerator.Current.Value);
                    }
                    while (enumerator.MoveNext())
                    {
                        sql.Add(",");
                        sql.Add(enumerator.Current.Value);
                    }
                    sql.Add(") VALUES (");
                    enumerator = fields.GetEnumerator();
                    if (enumerator.MoveNext())
                    {
                        var value = enumerator.Current.Key.GetValue(entity);
                        var parameter = Query.GetParameter(parameters.Count);
                        parameters.Add((parameter, value));
                        sql.Add(parameter);
                    }
                    while (enumerator.MoveNext())
                    {
                        sql.Add(",");
                        var value = enumerator.Current.Key.GetValue(entity);
                        var parameter = Query.GetParameter(parameters.Count);
                        parameters.Add((parameter, value));
                        sql.Add(parameter);
                    }
                    sql.Add(")");
                    while (index < entities.Count)
                    {
                        if (count == 1000 || fieldCount + fields.Count >= 2100)
                            break;
                        count += 1;
                        fieldCount += fields.Count;
                        entity = entities[index++];
                        sql.Add(",(");
                        enumerator = fields.GetEnumerator();
                        if (enumerator.MoveNext())
                        {
                            var value = enumerator.Current.Key.GetValue(entity);
                            var parameter = Query.GetParameter(parameters.Count);
                            parameters.Add((parameter, value));
                            sql.Add(parameter);
                        }
                        while (enumerator.MoveNext())
                        {
                            sql.Add(",");
                            var value = enumerator.Current.Key.GetValue(entity);
                            var parameter = Query.GetParameter(parameters.Count);
                            parameters.Add((parameter, value));
                            sql.Add(parameter);
                        }
                        sql.Add(")");
                    }
                    result += Execute(sql.Concat(), parameters);
                }
                return result;
            }
            public override Task<int> InsertAsync<TEntity>(Expression<Func<SqlExpression, TEntity>> entity)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                var parameters = Parameters;
                var sql = Insert(entity, parameters);
                return ExecuteAsync(sql, parameters);
            }
            public override Task<int> InsertAsync<TEntity>(TEntity entity, Expression<Func<TEntity, object>> properties)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));

                var parameters = Parameters;
                var sql = Insert(entity, properties, parameters);
                return ExecuteAsync(sql, parameters);
            }
            public override async Task<int> InsertRangeAsync<TEntity>(IList<TEntity> entities, Expression<Func<TEntity, object>> properties)
            {
                if (entities == null)
                    throw new ArgumentNullException(nameof(entities));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));

                var result = 0;
                var sql = new List<string>();
                var parameters = new List<(string, object)>();
                var table = DbTable.Get(properties.Parameters[0].Type);
                var fields = GetFields(table, properties.Body);
                for (int index = 0; ;)
                {
                    if (index == entities.Count)
                        break;
                    var entity = entities[index++];
                    var count = 1;
                    var fieldCount = fields.Count;
                    sql.Clear();
                    parameters.Clear();
                    sql.Add("INSERT INTO ");
                    sql.Add(table.Name);
                    sql.Add("(");
                    var enumerator = fields.GetEnumerator();
                    if (enumerator.MoveNext())
                    {
                        sql.Add(enumerator.Current.Value);
                    }
                    while (enumerator.MoveNext())
                    {
                        sql.Add(",");
                        sql.Add(enumerator.Current.Value);
                    }
                    sql.Add(") VALUES (");
                    enumerator = fields.GetEnumerator();
                    if (enumerator.MoveNext())
                    {
                        var value = enumerator.Current.Key.GetValue(entity);
                        var parameter = Query.GetParameter(parameters.Count);
                        parameters.Add((parameter, value));
                        sql.Add(parameter);
                    }
                    while (enumerator.MoveNext())
                    {
                        sql.Add(",");
                        var value = enumerator.Current.Key.GetValue(entity);
                        var parameter = Query.GetParameter(parameters.Count);
                        parameters.Add((parameter, value));
                        sql.Add(parameter);
                    }
                    sql.Add(")");
                    while (index < entities.Count)
                    {
                        if (count == 1000 || fieldCount + fields.Count >= 2100)
                            break;
                        count += 1;
                        fieldCount += fields.Count;
                        entity = entities[index++];
                        sql.Add(",(");
                        enumerator = fields.GetEnumerator();
                        if (enumerator.MoveNext())
                        {
                            var value = enumerator.Current.Key.GetValue(entity);
                            var parameter = Query.GetParameter(parameters.Count);
                            parameters.Add((parameter, value));
                            sql.Add(parameter);
                        }
                        while (enumerator.MoveNext())
                        {
                            sql.Add(",");
                            var value = enumerator.Current.Key.GetValue(entity);
                            var parameter = Query.GetParameter(parameters.Count);
                            parameters.Add((parameter, value));
                            sql.Add(parameter);
                        }
                        sql.Add(")");
                    }
                    result += await ExecuteAsync(sql.Concat(), parameters);
                }
                return result;
            }
            public override TIdentity InsertIdentity<TEntity, TIdentity>(Expression<Func<SqlExpression, TEntity>> entity)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                var parameters = Parameters;
                var sql = InsertIdentity(entity, parameters);
                //return this.Execute<TIdentity>(sql, parameters); 
                var identity = this.Execute<decimal>(sql, parameters);
                return Converter.Convert<decimal, TIdentity>(identity);
            }
            public override TIdentity InsertIdentity<TEntity, TIdentity>(TEntity entity, Expression<Func<TEntity, object>> properties)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));

                var parameters = Parameters;
                var sql = InsertIdentity(entity, properties, parameters);
                var identity = this.Execute<decimal>(sql, parameters);
                return Converter.Convert<decimal, TIdentity>(identity);
            }
            public override async Task<TIdentity> InsertIdentityAsync<TEntity, TIdentity>(Expression<Func<SqlExpression, TEntity>> entity)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                var parameters = Parameters;
                var sql = InsertIdentity(entity, parameters);
                //return this.ExecuteAsync<TIdentity>(sql, parameters);
                var identity = await this.ExecuteAsync<decimal>(sql, parameters);
                return Converter.Convert<decimal, TIdentity>(identity);
            }
            public override async Task<TIdentity> InsertIdentityAsync<TEntity, TIdentity>(TEntity entity, Expression<Func<TEntity, object>> properties)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));

                var parameters = Parameters;
                var sql = InsertIdentity(entity, properties, parameters);
                var identity = await this.ExecuteAsync<decimal>(sql, parameters);
                return Converter.Convert<decimal, TIdentity>(identity);
            }
            public override int Delete<TEntity>(object identity)
            {
                var parameters = Parameters;
                var sql = Delete(DbTable.Get(typeof(TEntity)), identity, parameters);
                return Execute(sql, parameters);
            }
            public override int Delete<TEntity>(Expression<Func<TEntity, SqlExpression, bool>> where)
            {
                if (where == null)
                    throw new ArgumentNullException(nameof(where));

                var parameters = Parameters;
                var sql = Delete(DbTable.Get(typeof(TEntity)), where, parameters);
                return Execute(sql, parameters);
            }
            public override Task<int> DeleteAsync<TEntity>(object identity)
            {
                var parameters = Parameters;
                var sql = Delete(DbTable.Get(typeof(TEntity)), identity, parameters);
                return ExecuteAsync(sql, parameters);
            }
            public override Task<int> DeleteAsync<TEntity>(Expression<Func<TEntity, SqlExpression, bool>> where)
            {
                if (where == null)
                    throw new ArgumentNullException(nameof(where));

                var parameters = Parameters;
                var sql = Delete(DbTable.Get(typeof(TEntity)), where, parameters);
                return ExecuteAsync(sql, parameters);
            }
            public override int Update<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> entity, object identity)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                var parameters = Parameters;
                var sql = Update(entity, identity, parameters);
                return Execute(sql, parameters);
            }
            public override int Update<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> entity, Expression<Func<TEntity, SqlExpression, bool>> where)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (where == null)
                    throw new ArgumentNullException(nameof(where));

                var parameters = Parameters;
                var sql = Update(entity, where, parameters);
                return Execute(sql, parameters);
            }
            public override int Update<TEntity>(TEntity entity, Expression<Func<TEntity, object>> properties)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));

                SqlDbExtensions.RegisterIdentity<TEntity>(out var identity);
                if (identity == null)
                    throw new ArgumentNullException(nameof(identity));

                var parameters = Parameters;
                var sql = Update(entity, properties, identity.GetValue(entity), parameters);
                return Execute(sql, parameters);
            }
            public override int Update<TEntity>(TEntity entity, Expression<Func<TEntity, object>> properties, Expression<Func<TEntity, SqlExpression, bool>> where)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));
                if (where == null)
                    throw new ArgumentNullException(nameof(where));

                var parameters = Parameters;
                var sql = Update(entity, properties, where, parameters);
                return Execute(sql, parameters);
            }
            public override Task<int> UpdateAsync<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> entity, object identity)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                var parameters = Parameters;
                var sql = Update(entity, identity, parameters);
                return ExecuteAsync(sql, parameters);
            }
            public override Task<int> UpdateAsync<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> entity, Expression<Func<TEntity, SqlExpression, bool>> where)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (where == null)
                    throw new ArgumentNullException(nameof(where));

                var parameters = Parameters;
                var sql = Update(entity, where, parameters);
                return ExecuteAsync(sql, parameters);
            }
            public override Task<int> UpdateAsync<TEntity>(TEntity entity, Expression<Func<TEntity, object>> properties)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));

                SqlDbExtensions.RegisterIdentity<TEntity>(out var identity);
                if (identity == null)
                    throw new ArgumentNullException(nameof(identity));

                var parameters = Parameters;
                var sql = Update(entity, properties, identity.GetValue(entity), parameters);
                return ExecuteAsync(sql, parameters);
            }
            public override Task<int> UpdateAsync<TEntity>(TEntity entity, Expression<Func<TEntity, object>> properties, Expression<Func<TEntity, SqlExpression, bool>> where)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));
                if (where == null)
                    throw new ArgumentNullException(nameof(where));

                var parameters = Parameters;
                var sql = Update(entity, properties, where, parameters);
                return ExecuteAsync(sql, parameters);
            }
            public override TEntity SelectSingle<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> select, object identity)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = SelectSingle(select, identity, parameters);
                return this.Execute<TEntity>(sql, parameters);
            }
            public override Task<TEntity> SelectSingleAsync<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> select, object identity)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = SelectSingle(select, identity, parameters);
                return this.ExecuteAsync<TEntity>(sql, parameters);
            }
            public override TEntity SelectSingle<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = SelectSingle(select, where, parameters);
                return this.Execute<TEntity>(sql, parameters);
            }
            public override Task<TEntity> SelectSingleAsync<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = SelectSingle(select, where, parameters);
                return this.ExecuteAsync<TEntity>(sql, parameters);
            }
            public override List<TEntity> Select<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = Select(select, where, orderBy, parameters);
                return this.Execute<List<TEntity>>(sql, parameters);
            }
            public override Task<List<TEntity>> SelectAsync<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = Select(select, where, orderBy, parameters);
                return this.ExecuteAsync<List<TEntity>>(sql, parameters);
            }
            public override List<TEntity> Select<TEntity>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = Select(offset, fetch, select, where, orderBy, parameters);
                return this.Execute<List<TEntity>>(sql, parameters);
            }
            public override Task<List<TEntity>> SelectAsync<TEntity>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = Select(offset, fetch, select, where, orderBy, parameters);
                return this.ExecuteAsync<List<TEntity>>(sql, parameters);
            }
            public override (List<TEntity>, int) SelectPaged<TEntity>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = SelectPaged(offset, fetch, select, null, where, orderBy, parameters);
                return this.Execute<List<TEntity>, int>(sql, parameters);
            }
            public override Task<(List<TEntity>, int)> SelectPagedAsync<TEntity>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = SelectPaged(offset, fetch, select, null, where, orderBy, parameters);
                return this.ExecuteAsync<List<TEntity>, int>(sql, parameters);
            }
            public override TResult Select<TEntity, TResult>(Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<SqlExpression, object>> from, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Func<TEntity, SqlExpression, object>> groupBy, Expression<Func<TEntity, SqlExpression, bool>> having, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = Select(select, from, where, groupBy, having, orderBy, parameters);
                return this.Execute<TResult>(sql, parameters);
            }
            public override Task<TResult> SelectAsync<TEntity, TResult>(Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<SqlExpression, object>> from, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Func<TEntity, SqlExpression, object>> groupBy, Expression<Func<TEntity, SqlExpression, bool>> having, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = Select(select, from, where, groupBy, having, orderBy, parameters);
                return this.ExecuteAsync<TResult>(sql, parameters);
            }
            public override TResult Select<TEntity, TResult>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<SqlExpression, object>> from, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Func<TEntity, SqlExpression, object>> groupBy, Expression<Func<TEntity, SqlExpression, bool>> having, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = Select(offset, fetch, select, from, where, groupBy, having, orderBy, parameters);
                return this.Execute<TResult>(sql, parameters);
            }
            public override Task<TResult> SelectAsync<TEntity, TResult>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<SqlExpression, object>> from, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Func<TEntity, SqlExpression, object>> groupBy, Expression<Func<TEntity, SqlExpression, bool>> having, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = Select(offset, fetch, select, from, where, groupBy, having, orderBy, parameters);
                return this.ExecuteAsync<TResult>(sql, parameters);
            }
            public override (TResult, int) SelectPaged<TEntity, TResult>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<SqlExpression, object>> from, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = SelectPaged(offset, fetch, select, from, where, orderBy, parameters);
                return this.Execute<TResult, int>(sql, parameters);
            }
            public override Task<(TResult, int)> SelectPagedAsync<TEntity, TResult>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<SqlExpression, object>> from, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = SelectPaged(offset, fetch, select, from, where, orderBy, parameters);
                return this.ExecuteAsync<TResult, int>(sql, parameters);
            }
        }
        #endregion

        #region SqliteDb
        public class SqliteDb : SqlDb
        {
            private static object _Sync = new object();
            private static Type _ConnectionType;
            private static Type _DataReaderType;
            private static Action<DbCommand, string, object> _AddParameter;
            private static Func<MemberExpression, IReadOnlyList<object>> _DbMember;
            private static Func<MethodCallExpression, IReadOnlyList<object>> _DbMethod;
            [ThreadStatic] private static List<string> _CommandText;
            [ThreadStatic] private static List<(string, object)> _Parameters;
            private Func<(DbCommand, IDisposable)> _getCommand;
            private Action<DbCommand> _cmdExecuting;
            public static List<string> CommandText
            {
                get
                {
                    var commandText = _CommandText;
                    if (commandText == null)
                    {
                        commandText = new List<string>();
                        _CommandText = commandText;
                    }
                    else
                    {
                        commandText.Clear();
                    }
                    return commandText;
                }
            }
            public static List<(string, object)> Parameters
            {
                get
                {
                    var parameters = _Parameters;
                    if (parameters == null)
                    {
                        parameters = new List<(string, object)>();
                        _Parameters = parameters;
                    }
                    else
                    {
                        parameters.Clear();
                    }
                    return parameters;
                }
            }
            private static class Query
            {
                private static object _Sync;
                private static string[] _Parameters;
                private static string[] _Tables;
                private static Dictionary<Type, Action<DbCommand, object>> _Handlers;
                static Query()
                {
                    _Sync = new object();
                    _Parameters = new[]
                   {
                        "@p0","@p1","@p2","@p3","@p4","@p5","@p6","@p7",
                        "@p8","@p9","@p10","@p11","@p12","@p13","@p14","@p15",
                        "@p16","@p17","@p18","@p19","@p20","@p21","@p22","@p23",
                        "@p24","@p25","@p26","@p27","@p28","@p29","@p30","@p31",
                        "@p32","@p33","@p34","@p35","@p36","@p37","@p38","@p39",
                        "@p40","@p41","@p42","@p43","@p44","@p45","@p46","@p47",
                        "@p48","@p49","@p50","@p51","@p52","@p53","@p54","@p55",
                        "@p56","@p57","@p58","@p59","@p60","@p61","@p62","@p63"
                    };
                    _Tables = new[]
                    {
                        "t0","t1","t2","t3","t4","t5","t6","t7",
                        "t8","t9","t10","t11","t12","t13","t14","t15"
                    };
                    _Handlers = new Dictionary<Type, Action<DbCommand, object>>();
                }
                public static string GetParameter(int index)
                {
                    return index < _Parameters.Length ? _Parameters[index] : $"@p{index}";
                }
                public static string GetTable(int index)
                {
                    return index < _Tables.Length ? _Tables[index] : $"t{index}";
                }
                public static void AddParameter(DbCommand command, object objParameters)
                {
                    if (objParameters == null)
                        return;

                    var type = objParameters.GetType();
                    if (!_Handlers.TryGetValue(type, out var handler))
                    {
                        lock (_Sync)
                        {
                            if (!_Handlers.TryGetValue(type, out handler))
                            {
                                var cmd = Expression.Parameter(typeof(DbCommand), "cmd");
                                var objValue = Expression.Parameter(typeof(object), "objValue");
                                var tValue = Expression.Variable(type, "value");
                                var addParameter = typeof(SqlDbExtensions).GetMethod("AddParameter").MakeGenericMethod(_ConnectionType);
                                var properties = type.GetGeneralProperties(BindingFlags.Instance | BindingFlags.Public);
                                var exprs = new List<Expression>();
                                exprs.Add(Expression.Assign(tValue, Expression.Convert(objValue, type)));
                                foreach (var property in properties)
                                {
                                    if (!property.CanRead)
                                        continue;
                                    exprs.Add(Expression.Call(
                                        addParameter, cmd,
                                        Expression.Constant($"@{property.Name}"),
                                        Expression.Convert(Expression.Property(tValue, property), typeof(object))
                                        ));
                                }
                                handler = Expression.Lambda<Action<DbCommand, object>>(Expression.Block(new[] { tValue }, exprs), cmd, objValue).Compile();
                                var handlers = new Dictionary<Type, Action<DbCommand, object>>(_Handlers);
                                handlers.Add(type, handler);
                                _Handlers = handlers;
                            }
                        }
                    }
                    handler(command, objParameters);
                }
            }
            private static class Reader<TEntity>
            {
                public static Func<DbDataReader, TEntity> Handler;
            }
            public class DbTable
            {
                private class Comparer : IEqualityComparer<PropertyInfo>
                {
                    public bool Equals(PropertyInfo x, PropertyInfo y) =>
                        x.MetadataToken == y.MetadataToken && x.DeclaringType == y.DeclaringType;
                    public int GetHashCode(PropertyInfo obj) => obj.MetadataToken;
                }

                public string Name;
                public string Identity;
                public string Join;
                public Dictionary<PropertyInfo, string> Fields;
                public Dictionary<PropertyInfo, string> Columns;
                public Dictionary<PropertyInfo, DbTable> Tables;

                private static int _MaxDepth = 8;//TODO??_TypeReference
                private static Comparer _Comparer = new Comparer();
                private static object _Sync = new object();
                private static Dictionary<Type, DbTable> _Tables = new Dictionary<Type, DbTable>();
                public static DbTable Get(Type type)
                {
                    if (!_Tables.TryGetValue(type, out var table))
                    {
                        lock (_Sync)
                        {
                            if (!_Tables.TryGetValue(type, out table))
                            {
                                table = Get(0, null, null, type);
                            }
                        }
                    }
                    return table;
                }
                private static DbTable Get(int depth, string prefix, string join, Type type)
                {
                    var table = new DbTable();
                    table.Join = join;
                    table.Fields = new Dictionary<PropertyInfo, string>(_Comparer);
                    table.Columns = new Dictionary<PropertyInfo, string>(_Comparer);
                    table.Tables = new Dictionary<PropertyInfo, DbTable>(_Comparer);
                    var registerReader = typeof(SqlDbExtensions).GetMethod("RegisterDbReader", new[] { typeof(Type), typeof(ParameterExpression), typeof(ParameterExpression), typeof(Expression).MakeByRefType() })
                    .MakeGenericMethod(_DataReaderType);
                    var readerArgs = new object[] { null, Expression.Parameter(_DataReaderType), Expression.Parameter(typeof(int)), null };
                    SqlDbExtensions.RegisterTable(out var tableResolver);
                    SqlDbExtensions.RegisterIdentity(out var identityResolver);
                    SqlDbExtensions.RegisterProperty(out var propertyResolver);

                    var dataTableAttribute = type.GetCustomAttribute<DataTableAttribute>();
                    table.Name = $"[{dataTableAttribute?.Name ?? tableResolver(type)}]";
                    var identityProperty = identityResolver(type);
                    if (identityProperty != null)
                    {
                        var dataColumnAttribute = identityProperty.GetCustomAttribute<DataColumnAttribute>();
                        table.Identity = $"[{dataColumnAttribute?.Name ?? propertyResolver(identityProperty)}]";
                    }
                    var properties = type.GetGeneralProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    foreach (var property in properties)
                    {
                        if (!property.CanWrite)
                            continue;
                        if (property.IsDefined(typeof(IgnoreDataColumnAttribute)))
                            continue;
                        var dataColumnAttribute = property.GetCustomAttribute<DataColumnAttribute>();
                        readerArgs[0] = property.PropertyType;
                        registerReader.Invoke(null, readerArgs);
                        if (readerArgs[3] != null)
                        {
                            string columnName = dataColumnAttribute?.Name ?? propertyResolver(property);
                            table.Fields.Add(property, $"[{columnName}]");
                            table.Columns.Add(property, $"[{prefix}{columnName}]");
                        }
                        else
                        {
                            if (depth > _MaxDepth)
                                continue;
                            var joinIdentityProperty = identityResolver(property.PropertyType);
                            if (joinIdentityProperty == null)
                                continue;
                            var joinName = dataColumnAttribute?.Name;
                            if (joinName == null)
                            {
                                var onPropery = type.GetProperty($"{property.Name}{joinIdentityProperty.Name}", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                if (onPropery == null)
                                {
                                    if (joinIdentityProperty.Name == $"{type.Name}{identityProperty.Name}")
                                    {
                                        onPropery = identityProperty;
                                    }
                                    else
                                    {
                                        continue;
                                    }
                                }
                                dataColumnAttribute = onPropery.GetCustomAttribute<DataColumnAttribute>();
                                joinName = dataColumnAttribute?.Name ?? propertyResolver(onPropery);
                            }
                            joinName = $"[{joinName}]";
                            string columnName = propertyResolver(property);
                            var propertyPrefix = $"{prefix}{columnName}.";
                            var propertyTable = Get(depth + 1, propertyPrefix, joinName, property.PropertyType);
                            table.Tables.Add(property, propertyTable);
                        }
                    }
                    return table;
                }
            }
            public class DbEntity
            {
                public string Name;
                public DbTable Table;
                public Dictionary<PropertyInfo, DbEntity> Entities;
            }
            public class DbEntityContext
            {
                private int _table;
                private Dictionary<ParameterExpression, DbEntity> _entities;
                public DbEntityContext()
                {
                    _entities = new Dictionary<ParameterExpression, DbEntity>();
                }
                public DbEntity Add(ParameterExpression parameter)
                {
                    var entity = new DbEntity()
                    {
                        Name = Query.GetTable(_table++),
                        Table = DbTable.Get(parameter.Type),
                        Entities = new Dictionary<PropertyInfo, DbEntity>()
                    };
                    _entities.Add(parameter, entity);
                    return entity;
                }
                public void Add(ParameterExpression parameter, DbEntity entity)
                {
                    _entities.Add(parameter, entity);
                }
                public DbEntity Add(DbEntity entity, PropertyInfo property)
                {
                    if (entity.Entities.TryGetValue(property, out var propertyEntity))
                    {
                        return propertyEntity;
                    }
                    else if (entity.Table.Tables.TryGetValue(property, out var entityTable))
                    {
                        propertyEntity = new DbEntity();
                        propertyEntity.Name = Query.GetTable(_table++);
                        propertyEntity.Table = entityTable;
                        propertyEntity.Entities = new Dictionary<PropertyInfo, DbEntity>();
                        entity.Entities.Add(property, propertyEntity);
                        return propertyEntity;
                    }
                    return null;
                }
                public DbEntity Convert(Expression expression)
                {
                    if (expression == null)
                        return null;

                    switch (expression.NodeType)
                    {
                        case ExpressionType.Convert:
                            return Convert(((UnaryExpression)expression).Operand);
                        case ExpressionType.Parameter:
                            {
                                _entities.TryGetValue((ParameterExpression)expression, out var entity);
                                return entity;
                            }
                        case ExpressionType.MemberAccess:
                            {
                                var expr = (MemberExpression)expression;
                                if (expr.Member is PropertyInfo property)
                                {
                                    var propertyEntity = Convert(expr.Expression);
                                    if (propertyEntity != null)
                                    {
                                        if (propertyEntity.Entities.TryGetValue(property, out var entity))
                                        {
                                            return entity;
                                        }
                                        else if (propertyEntity.Table.Tables.TryGetValue(property, out var propertyTable))
                                        {
                                            entity = new DbEntity();
                                            entity.Name = Query.GetTable(_table++);
                                            entity.Table = propertyTable;
                                            entity.Entities = new Dictionary<PropertyInfo, DbEntity>();
                                            propertyEntity.Entities.Add(property, entity);
                                            return entity;
                                        }
                                    }
                                }
                            }
                            return null;
                        default:
                            return null;
                    }
                }
            }
            public static Dictionary<PropertyInfo, string> GetFields(DbTable table, Expression expression)
            {
                switch (expression.NodeType)
                {
                    case ExpressionType.Parameter:
                        return table.Fields;
                    case ExpressionType.Convert:
                        expression = ((UnaryExpression)expression).Operand;
                        if (expression.NodeType == ExpressionType.MemberAccess)
                            goto case ExpressionType.MemberAccess;
                        break;
                    case ExpressionType.MemberAccess:
                        {
                            var expr = (MemberExpression)expression;
                            if (expr.Member is PropertyInfo property
                                && table.Fields.TryGetValue(property, out var field))
                            {
                                return new Dictionary<PropertyInfo, string>()
                                {
                                    { property,field}
                                };
                            }
                        }
                        break;
                    case ExpressionType.New:
                        {
                            var fields = new Dictionary<PropertyInfo, string>();
                            var exprArgs = ((NewExpression)expression).Arguments;
                            for (int i = 0; i < exprArgs.Count; i++)
                            {
                                var expr = exprArgs[i].NodeType == ExpressionType.Convert ?
                                    ((UnaryExpression)exprArgs[i]).Operand as MemberExpression
                                    : exprArgs[i] as MemberExpression;
                                if (expr != null && expr.Member is PropertyInfo property
                                    && table.Fields.TryGetValue(property, out var field))
                                {
                                    fields.Add(property, field);
                                }
                            }
                            return fields;
                        }
                    case ExpressionType.Call:
                        {
                            if (SqlDbExtensions.TryExcept((MethodCallExpression)expression, out var except))
                            {
                                switch (except.NodeType)
                                {
                                    case ExpressionType.Convert:
                                        except = ((UnaryExpression)except).Operand;
                                        if (except.NodeType == ExpressionType.MemberAccess)
                                            goto case ExpressionType.MemberAccess;
                                        break;
                                    case ExpressionType.MemberAccess:
                                        {
                                            var expr = (MemberExpression)except;
                                            if (expr.Member is PropertyInfo property && table.Fields.ContainsKey(property))
                                            {
                                                var fields = new Dictionary<PropertyInfo, string>(table.Fields);
                                                fields.Remove(property);
                                                return fields;
                                            }
                                        }
                                        break;
                                    case ExpressionType.New:
                                        {
                                            var fields = new Dictionary<PropertyInfo, string>(table.Fields);
                                            var exprArgs = ((NewExpression)except).Arguments;
                                            for (int i = 0; i < exprArgs.Count; i++)
                                            {
                                                var expr = exprArgs[i].NodeType == ExpressionType.Convert ?
                                                   ((UnaryExpression)exprArgs[i]).Operand as MemberExpression
                                                   : exprArgs[i] as MemberExpression;
                                                if (expr != null && expr.Member is PropertyInfo property && fields.ContainsKey(property))
                                                {
                                                    fields.Remove(property);
                                                }
                                            }
                                            return fields;
                                        }
                                    default:
                                        break;
                                }
                                return new Dictionary<PropertyInfo, string>(table.Fields);
                            }
                        }
                        break;
                    default:
                        break;
                }
                return new Dictionary<PropertyInfo, string>();
            }
            public static void Join(DbEntity entity, DbEntity joinEntity, List<string> sql)
            {
                sql.Add(" LEFT JOIN ");
                sql.Add(joinEntity.Table.Name);
                sql.Add(" ");
                sql.Add(joinEntity.Name);
                sql.Add(" ON ");
                sql.Add(joinEntity.Name);
                sql.Add(".");
                sql.Add(joinEntity.Table.Identity);
                sql.Add("=");
                sql.Add(entity.Name);
                sql.Add(".");
                sql.Add(joinEntity.Table.Join);
                foreach (var item in joinEntity.Entities)
                {
                    Join(joinEntity, item.Value, sql);
                }
            }
            public static void Convert(Expression expression, List<string> sql, List<(string, object)> parameters, DbEntityContext context)
            {
                switch (expression.NodeType)
                {
                    case ExpressionType.Parameter:
                        {
                            var entity = context.Convert(expression);
                            if (entity != null)
                            {
                                foreach ((_, var field) in entity.Table.Fields)
                                {
                                    sql.Add(entity.Name);
                                    sql.Add(".");
                                    sql.Add(field);
                                    sql.Add(",");
                                }
                                sql.RemoveAt(sql.Count - 1);
                            }
                        }
                        break;
                    case ExpressionType.Constant:
                        {
                            var value = ((ConstantExpression)expression).Value;
                            var parameter = Query.GetParameter(parameters.Count);
                            sql.Add(parameter);
                            parameters.Add((parameter, value));
                        }
                        break;
                    case ExpressionType.MemberAccess:
                        {
                            var expr = (MemberExpression)expression;
                            var exprObjs = _DbMember(expr);
                            if (exprObjs != null)
                            {
                                for (int i = 0; i < exprObjs.Count; i++)
                                {
                                    var exprObj = exprObjs[i] as Expression;
                                    if (exprObj != null)
                                        Convert(exprObj, sql, parameters, context);
                                    else
                                        sql.Add((string)exprObjs[i]);
                                }
                                break;
                            }
                            if (expr.Member is PropertyInfo property)
                            {
                                var entity = context.Convert(expr.Expression);
                                if (entity != null)
                                {
                                    if (entity.Table.Fields.TryGetValue(property, out var field))
                                    {
                                        sql.Add(entity.Name);
                                        sql.Add(".");
                                        sql.Add(field);
                                    }
                                    else
                                    {
                                        var propertyEntity = context.Add(entity, property);
                                        if (propertyEntity != null)
                                        {
                                            foreach ((_, var propertyField) in propertyEntity.Table.Fields)
                                            {
                                                sql.Add(propertyEntity.Name);
                                                sql.Add(".");
                                                sql.Add(propertyField);
                                                sql.Add(",");
                                            }
                                            sql.RemoveAt(sql.Count - 1);
                                        }
                                    }
                                    break;
                                }
                            }
                            var value = expr.Invoke();
                            var parameter = Query.GetParameter(parameters.Count);
                            sql.Add(parameter);
                            parameters.Add((parameter, value));
                        }
                        break;
                    case ExpressionType.Call:
                        {
                            var expr = (MethodCallExpression)expression;
                            var exprObjs = _DbMethod(expr);
                            if (exprObjs != null)
                            {
                                for (int i = 0; i < exprObjs.Count; i++)
                                {
                                    var exprObj = exprObjs[i] as Expression;
                                    if (exprObj != null)
                                        Convert(exprObj, sql, parameters, context);
                                    else
                                        sql.Add((string)exprObjs[i]);
                                }
                            }
                            else if (SqlDbExtensions.TrySelect(expr, out var objSelect))
                            {
                                Select(objSelect, sql, parameters, context);
                            }
                            else if (SqlDbExtensions.TrySelect(expr, out var select, out var where, out var groupBy, out var having, out var orderBy))
                            {
                                sql.Add("(");
                                sql.Add(Select(select, where, groupBy, having, orderBy, parameters, context));
                                sql.Add(")");
                            }
                            else if (SqlDbExtensions.TrySelect(expr, out var offset, out var fetch, out select, out where, out groupBy, out having, out orderBy))
                            {
                                sql.Add("(");
                                sql.Add(Select(offset, fetch, select, where, groupBy, having, orderBy, parameters, context));
                                sql.Add(")");
                            }
                            else if (SqlDbExtensions.TryNavigate(expr, out var navigate))
                            {
                                var entity = context.Convert(navigate);
                                if (entity == null)
                                {
                                    Convert(navigate, sql, parameters, context);
                                }
                                else
                                {
                                    Navigate(entity, sql, context);
                                    sql.RemoveAt(sql.Count - 1);
                                }
                            }
                            else
                            {
                                var value = expression.Invoke();
                                var parameter = Query.GetParameter(parameters.Count);
                                sql.Add(parameter);
                                parameters.Add((parameter, value));
                            }
                        }
                        break;
                    case ExpressionType.NewArrayInit:
                        {
                            var argExprs = ((NewArrayExpression)expression).Expressions;
                            Convert(argExprs[0], sql, parameters, context);
                            for (int i = 1; i < argExprs.Count; i++)
                            {
                                sql.Add(",");
                                Convert(argExprs[i], sql, parameters, context);
                            }
                        }
                        break;
                    case ExpressionType.Convert:
                        Convert(((UnaryExpression)expression).Operand, sql, parameters, context);
                        break;
                    case ExpressionType.Not:
                        sql.Add("NOT ");
                        Convert(((UnaryExpression)expression).Operand, sql, parameters, context);
                        break;
                    case ExpressionType.Equal:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" = ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.NotEqual:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" <> ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.GreaterThan:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" > ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.GreaterThanOrEqual:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" >= ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.LessThan:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" < ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.LessThanOrEqual:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" <= ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.AndAlso:
                        sql.Add("(");
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" AND ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        sql.Add(")");
                        break;
                    case ExpressionType.OrElse:
                        sql.Add("(");
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" OR ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        sql.Add(")");
                        break;
                    case ExpressionType.And:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" & ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.Or:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" | ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.Add:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" + ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.Subtract:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" - ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.Multiply:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" * ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.Divide:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" / ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.Modulo:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" % ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.Coalesce:
                        sql.Add("IFNULL(");
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(",");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        sql.Add(")");
                        break;
                    case ExpressionType.Conditional:
                        sql.Add("CASE WHEN ");
                        Convert(((ConditionalExpression)expression).Test, sql, parameters, context);
                        sql.Add(" THEN ");
                        Convert(((ConditionalExpression)expression).IfTrue, sql, parameters, context);
                        sql.Add(" ELSE ");
                        Convert(((ConditionalExpression)expression).IfFalse, sql, parameters, context);
                        sql.Add(" END");
                        break;
                    default:
                        throw new NotSupportedException(expression.ToString());
                }
            }
            public static void Navigate(DbEntity entity, List<string> sql, DbEntityContext context)
            {
                foreach ((_, var field) in entity.Table.Fields)
                {
                    sql.Add(entity.Name);
                    sql.Add(".");
                    sql.Add(field);
                    sql.Add(",");
                }
                foreach ((var property, _) in entity.Table.Tables)
                {
                    var propertyEntity = context.Add(entity, property);
                    if (propertyEntity != null)
                        Navigate(propertyEntity, sql, context);
                }
            }
            public static void Navigate(DbTable table, DbEntity entity, List<string> sql, DbEntityContext context)
            {
                foreach ((var property, var field) in entity.Table.Fields)
                {
                    sql.Add(entity.Name);
                    sql.Add(".");
                    sql.Add(field);
                    sql.Add(" AS ");
                    sql.Add(entity.Table.Columns[property]);
                    sql.Add(",");
                }
                foreach ((var property, var propertyTable) in table.Tables)
                {
                    var propertyEntity = context.Add(entity, property);
                    if (propertyEntity != null)
                        Navigate(propertyTable, propertyEntity, sql, context);
                }
            }
            public static void Select(DbTable table, PropertyInfo member, Expression expression, List<string> sql, List<(string, object)> parameters, DbEntityContext context)
            {
                if (table.Columns.TryGetValue(member, out var column))
                {
                    Convert(expression, sql, parameters, context);
                    sql.Add(" AS ");
                    sql.Add(column);
                    sql.Add(",");
                }
                else if (table.Tables.TryGetValue(member, out var memberTable))
                {
                    switch (expression.NodeType)
                    {
                        case ExpressionType.MemberInit:
                            {
                                var expr = (MemberInitExpression)expression;
                                var bindings = expr.Bindings;
                                for (int i = 0; i < bindings.Count; i++)
                                {
                                    var memberAssignment = (MemberAssignment)bindings[i];
                                    if (memberAssignment.Member is PropertyInfo memberMember)
                                        Select(memberTable, memberMember, memberAssignment.Expression, sql, parameters, context);
                                }
                            }
                            break;
                        case ExpressionType.Call:
                            {
                                var expr = (MethodCallExpression)expression;
                                if (SqlDbExtensions.TryNavigate(expr, out var navigate))
                                {
                                    var entity = context.Convert(navigate);
                                    if (entity != null)
                                    {
                                        Navigate(memberTable, entity, sql, context);
                                        break;
                                    }
                                }
                            }
                            break;
                        default:
                            {
                                var entity = context.Convert(expression);
                                if (entity != null)
                                {
                                    foreach ((var property, var field) in entity.Table.Fields)
                                    {
                                        sql.Add(entity.Name);
                                        sql.Add(".");
                                        sql.Add(field);
                                        sql.Add(" AS ");
                                        sql.Add(memberTable.Columns[property]);
                                        sql.Add(",");
                                    }
                                }
                            }
                            break;
                    }

                }
            }
            public static void Select(Expression expression, List<string> sql, List<(string, object)> parameters, DbEntityContext context)
            {
                switch (expression.NodeType)
                {
                    case ExpressionType.Convert:
                        Select(((UnaryExpression)expression).Operand, sql, parameters, context);
                        break;
                    case ExpressionType.MemberInit:
                        {
                            var expr = (MemberInitExpression)expression;
                            var table = DbTable.Get(expr.Type);
                            var bindings = expr.Bindings;
                            for (int i = 0; i < bindings.Count; i++)
                            {
                                var memberAssignment = (MemberAssignment)bindings[i];
                                if (memberAssignment.Member is PropertyInfo member)
                                    Select(table, member, memberAssignment.Expression, sql, parameters, context);
                            }
                            sql.RemoveAt(sql.Count - 1);
                        }
                        break;
                    case ExpressionType.NewArrayInit:
                        {
                            var argExprs = ((NewArrayExpression)expression).Expressions;
                            Convert(argExprs[0], sql, parameters, context);
                            for (int i = 1; i < argExprs.Count; i++)
                            {
                                sql.Add(",");
                                Select(argExprs[i], sql, parameters, context);
                            }
                        }
                        break;
                    case ExpressionType.Call:
                        {
                            var expr = (MethodCallExpression)expression;
                            if (SqlDbExtensions.TryNavigate(expr, out var navigate))
                            {
                                var entity = context.Convert(navigate);
                                if (entity == null)
                                {
                                    Convert(navigate, sql, parameters, context);
                                }
                                else
                                {
                                    Navigate(entity.Table, entity, sql, context);
                                    sql.RemoveAt(sql.Count - 1);
                                }
                            }
                            else
                            {
                                Convert(expression, sql, parameters, context);
                            }
                        }
                        break;
                    default:
                        {
                            var entity = context.Convert(expression);
                            if (entity == null)
                            {
                                Convert(expression, sql, parameters, context);
                            }
                            else
                            {
                                foreach ((var property, var field) in entity.Table.Fields)
                                {
                                    sql.Add(entity.Name);
                                    sql.Add(".");
                                    sql.Add(field);
                                    sql.Add(" AS ");
                                    sql.Add(entity.Table.Columns[property]);
                                    sql.Add(",");
                                }
                                sql.RemoveAt(sql.Count - 1);
                            }
                        }
                        break;
                }
            }
            public static string Select(LambdaExpression select, LambdaExpression where, LambdaExpression groupBy, LambdaExpression having, LambdaExpression orderBy, List<(string, object)> parameters, DbEntityContext context)
            {
                var sql = new List<string>();
                var entity = context.Add(select.Parameters[0]);
                if (where != null)
                {
                    context.Add(where.Parameters[0], entity);
                    sql.Add(" WHERE ");
                    Convert(where.Body, sql, parameters, context);
                }
                if (groupBy != null)
                {
                    context.Add(groupBy.Parameters[0], entity);
                    sql.Add(" GROUP BY ");
                    Convert(groupBy.Body, sql, parameters, context);
                }
                if (having != null)
                {
                    context.Add(having.Parameters[0], entity);
                    sql.Add(" HAVING ");
                    Convert(having.Body, sql, parameters, context);
                }
                if (orderBy != null)
                {
                    context.Add(orderBy.Parameters[0], entity);
                    sql.Add(" ORDER BY ");
                    Convert(orderBy.Body, sql, parameters, context);
                }
                var index = sql.Count;
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);
                //Convert(select.Body, sql, parameters, context);

                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }
                return sql.Concat((index, sql.Count), (0, index));
            }
            public static string Select(int offset, int fetch, LambdaExpression select, LambdaExpression where, LambdaExpression groupBy, LambdaExpression having, LambdaExpression orderBy, List<(string, object)> parameters, DbEntityContext context)
            {
                var sql = new List<string>();
                var entity = context.Add(select.Parameters[0]);
                if (where != null)
                {
                    context.Add(where.Parameters[0], entity);
                    sql.Add(" WHERE ");
                    Convert(where.Body, sql, parameters, context);
                }
                if (groupBy != null)
                {
                    context.Add(groupBy.Parameters[0], entity);
                    sql.Add(" GROUP BY ");
                    Convert(groupBy.Body, sql, parameters, context);
                }
                if (having != null)
                {
                    context.Add(having.Parameters[0], entity);
                    sql.Add(" HAVING ");
                    Convert(having.Body, sql, parameters, context);
                }
                if (orderBy != null)
                {
                    context.Add(orderBy.Parameters[0], entity);
                    sql.Add(" ORDER BY ");
                    Convert(orderBy.Body, sql, parameters, context);
                }
                sql.Add($" LIMIT {offset},{fetch}");
                var index = sql.Count;
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);
                //Convert(select.Body, sql, parameters, context);

                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }
                return sql.Concat((index, sql.Count), (0, index));
            }
            public static string Insert(LambdaExpression entity, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var context = new DbEntityContext();
                var table = DbTable.Get(entity.Body.Type);
                sql.Add("INSERT INTO ");
                sql.Add(table.Name);
                sql.Add("(");
                if (entity.Body.NodeType == ExpressionType.MemberInit)
                {
                    var bindings = ((MemberInitExpression)entity.Body).Bindings;
                    Span<int> indexs = bindings.Count < 256 ? stackalloc int[bindings.Count] : new int[bindings.Count];
                    var index = 0;
                    //fields
                    {
                        var i = 0;
                        for (; i < bindings.Count; i++)
                        {
                            var memberAssignment = (MemberAssignment)bindings[i];
                            if (table.Fields.TryGetValue((PropertyInfo)memberAssignment.Member, out var name))
                            {
                                sql.Add(name);
                                indexs[index++] = i++;
                                break;
                            }
                        }
                        for (; i < bindings.Count; i++)
                        {
                            var memberAssignment = (MemberAssignment)bindings[i];
                            if (table.Fields.TryGetValue((PropertyInfo)memberAssignment.Member, out var name))
                            {
                                sql.Add(",");
                                sql.Add(name);
                                indexs[index++] = i;
                            }
                        }
                    }
                    sql.Add(") VALUES (");
                    //values
                    {
                        if (index > 0)
                        {
                            Convert(((MemberAssignment)bindings[indexs[0]]).Expression, sql, parameters, context);
                        }
                        for (int i = 1; i < index; i++)
                        {
                            sql.Add(",");
                            Convert(((MemberAssignment)bindings[indexs[i]]).Expression, sql, parameters, context);
                        }
                    }
                }
                sql.Add(")");
                return sql.Concat();
            }
            public static string Insert(object entity, LambdaExpression properties, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var table = DbTable.Get(properties.Parameters[0].Type);
                var fields = GetFields(table, properties.Body);
                sql.Add("INSERT INTO ");
                sql.Add(table.Name);
                sql.Add("(");
                var enumerator = fields.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    sql.Add(enumerator.Current.Value);
                }
                while (enumerator.MoveNext())
                {
                    sql.Add(",");
                    sql.Add(enumerator.Current.Value);
                }
                sql.Add(") VALUES (");
                enumerator = fields.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    var value = enumerator.Current.Key.GetValue(entity);
                    var parameter = Query.GetParameter(parameters.Count);
                    parameters.Add((parameter, value));
                    sql.Add(parameter);
                }
                while (enumerator.MoveNext())
                {
                    sql.Add(",");
                    var value = enumerator.Current.Key.GetValue(entity);
                    var parameter = Query.GetParameter(parameters.Count);
                    parameters.Add((parameter, value));
                    sql.Add(parameter);
                }
                sql.Add(")");
                return sql.Concat();
            }
            public static string InsertIdentity(LambdaExpression entity, List<(string, object)> parameters)
            {
                var sql = Insert(entity, parameters);
                return sql + ";SELECT LAST_INSERT_ROWID()";
            }
            public static string InsertIdentity(object entity, LambdaExpression properties, List<(string, object)> parameters)
            {
                var sql = Insert(entity, properties, parameters);
                return sql + ";SELECT LAST_INSERT_ROWID()";
            }
            public static string Delete(DbTable table, object identity, List<(string, object)> parameters)
            {
                var sql = CommandText;
                sql.Add("DELETE FROM ");
                sql.Add(table.Name);
                sql.Add(" WHERE ");
                sql.Add(table.Name);
                sql.Add(".");
                sql.Add(table.Identity);
                if (identity == null)
                {
                    sql.Add(" IS NULL");
                }
                else
                {
                    var parameter = Query.GetParameter(parameters.Count);
                    sql.Add(" = ");
                    sql.Add(parameter);
                    parameters.Add((parameter, identity));
                }
                return sql.Concat();
            }
            public static string Delete(DbTable table, LambdaExpression where, List<(string, object)> parameters)
            {
                var sql = CommandText;
                sql.Add("DELETE FROM ");
                sql.Add(table.Name);
                sql.Add(" WHERE ");
                var context = new DbEntityContext();
                context.Add(where.Parameters[0], new DbEntity()
                {
                    Name = table.Name,
                    Table = table,
                    Entities = new Dictionary<PropertyInfo, DbEntity>()
                });
                Convert(where.Body, sql, parameters, context);
                return sql.Concat();
            }
            public static string Update(LambdaExpression entity, object identity, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var table = DbTable.Get(entity.Body.Type);
                var context = new DbEntityContext();
                context.Add(entity.Parameters[0], new DbEntity()
                {
                    Name = table.Name,
                    Table = table,
                    Entities = new Dictionary<PropertyInfo, DbEntity>()
                });
                sql.Add("UPDATE ");
                sql.Add(table.Name);
                sql.Add(" SET ");
                if (entity.Body.NodeType == ExpressionType.MemberInit)
                {
                    var bindings = ((MemberInitExpression)entity.Body).Bindings;
                    var i = 0;
                    for (; i < bindings.Count;)
                    {
                        var memberAssignment = (MemberAssignment)bindings[i++];
                        if (table.Fields.TryGetValue((PropertyInfo)memberAssignment.Member, out var name))
                        {
                            sql.Add(name);
                            sql.Add("=");
                            Convert(memberAssignment.Expression, sql, parameters, context);
                            break;
                        }
                    }
                    for (; i < bindings.Count;)
                    {
                        var memberAssignment = (MemberAssignment)bindings[i++];
                        if (table.Fields.TryGetValue((PropertyInfo)memberAssignment.Member, out var name))
                        {
                            sql.Add(",");
                            sql.Add(name);
                            sql.Add("=");
                            Convert(memberAssignment.Expression, sql, parameters, context);
                        }
                    }
                }
                sql.Add(" WHERE ");
                sql.Add(table.Name);
                sql.Add(".");
                sql.Add(table.Identity);
                if (identity == null)
                {
                    sql.Add(" IS NULL");
                }
                else
                {
                    var parameter = Query.GetParameter(parameters.Count);
                    sql.Add(" = ");
                    sql.Add(parameter);
                    parameters.Add((parameter, identity));
                }
                return sql.Concat();
            }
            public static string Update(LambdaExpression entity, LambdaExpression where, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var table = DbTable.Get(entity.Body.Type);
                var context = new DbEntityContext();
                var dbEntity = new DbEntity()
                {
                    Name = table.Name,
                    Table = table,
                    Entities = new Dictionary<PropertyInfo, DbEntity>()
                };
                context.Add(entity.Parameters[0], dbEntity);
                sql.Add("UPDATE ");
                sql.Add(table.Name);
                sql.Add(" SET ");
                if (entity.Body.NodeType == ExpressionType.MemberInit)
                {
                    var bindings = ((MemberInitExpression)entity.Body).Bindings;
                    var i = 0;
                    for (; i < bindings.Count;)
                    {
                        var memberAssignment = (MemberAssignment)bindings[i++];
                        if (table.Fields.TryGetValue((PropertyInfo)memberAssignment.Member, out var name))
                        {
                            sql.Add(name);
                            sql.Add("=");
                            Convert(memberAssignment.Expression, sql, parameters, context);
                            break;
                        }
                    }
                    for (; i < bindings.Count;)
                    {
                        var memberAssignment = (MemberAssignment)bindings[i++];
                        if (table.Fields.TryGetValue((PropertyInfo)memberAssignment.Member, out var name))
                        {
                            sql.Add(",");
                            sql.Add(name);
                            sql.Add("=");
                            Convert(memberAssignment.Expression, sql, parameters, context);
                        }
                    }
                }
                sql.Add(" WHERE ");
                context.Add(where.Parameters[0], dbEntity);
                Convert(where.Body, sql, parameters, context);
                return sql.Concat();
            }
            public static string Update(object entity, LambdaExpression properties, object identity, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var table = DbTable.Get(properties.Parameters[0].Type);
                var fields = GetFields(table, properties.Body);
                sql.Add("UPDATE ");
                sql.Add(table.Name);
                sql.Add(" SET ");
                var enumerator = fields.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    var value = enumerator.Current.Key.GetValue(entity);
                    var parameter = Query.GetParameter(parameters.Count);
                    parameters.Add((parameter, value));
                    sql.Add(enumerator.Current.Value);
                    sql.Add("=");
                    sql.Add(parameter);
                }
                while (enumerator.MoveNext())
                {
                    sql.Add(",");
                    var value = enumerator.Current.Key.GetValue(entity);
                    var parameter = Query.GetParameter(parameters.Count);
                    parameters.Add((parameter, value));
                    sql.Add(enumerator.Current.Value);
                    sql.Add("=");
                    sql.Add(parameter);
                }
                sql.Add(" WHERE ");
                sql.Add(table.Name);
                sql.Add(".");
                sql.Add(table.Identity);
                if (identity == null)
                {
                    sql.Add(" IS NULL");
                }
                else
                {
                    var parameter = Query.GetParameter(parameters.Count);
                    sql.Add(" = ");
                    sql.Add(parameter);
                    parameters.Add((parameter, identity));
                }
                return sql.Concat();
            }
            public static string Update(object entity, LambdaExpression properties, LambdaExpression where, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var table = DbTable.Get(properties.Parameters[0].Type);
                var fields = GetFields(table, properties.Body);
                sql.Add("UPDATE ");
                sql.Add(table.Name);
                sql.Add(" SET ");
                var enumerator = fields.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    var value = enumerator.Current.Key.GetValue(entity);
                    var parameter = Query.GetParameter(parameters.Count);
                    parameters.Add((parameter, value));
                    sql.Add(enumerator.Current.Value);
                    sql.Add("=");
                    sql.Add(parameter);
                }
                while (enumerator.MoveNext())
                {
                    sql.Add(",");
                    var value = enumerator.Current.Key.GetValue(entity);
                    var parameter = Query.GetParameter(parameters.Count);
                    parameters.Add((parameter, value));
                    sql.Add(enumerator.Current.Value);
                    sql.Add("=");
                    sql.Add(parameter);
                }
                sql.Add(" WHERE ");
                var context = new DbEntityContext();
                context.Add(where.Parameters[0], new DbEntity()
                {
                    Name = table.Name,
                    Table = table,
                    Entities = new Dictionary<PropertyInfo, DbEntity>()
                });
                Convert(where.Body, sql, parameters, context);
                return sql.Concat();
            }
            public static string SelectSingle(LambdaExpression select, object identity, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var context = new DbEntityContext();
                var entity = context.Add(select.Parameters[0]);
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);

                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }
                sql.Add(" WHERE ");
                sql.Add(entity.Name);
                sql.Add(".");
                sql.Add(entity.Table.Identity);
                if (identity == null)
                {
                    sql.Add(" IS NULL");
                }
                else
                {
                    var parameter = Query.GetParameter(parameters.Count);
                    sql.Add(" = ");
                    sql.Add(parameter);
                    parameters.Add((parameter, identity));
                }
                return sql.Concat();
            }
            public static string SelectSingle(LambdaExpression select, LambdaExpression where, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var context = new DbEntityContext();
                var entity = context.Add(select.Parameters[0]);
                if (where != null)
                {
                    context.Add(where.Parameters[0], entity);
                    sql.Add(" WHERE ");
                    Convert(where.Body, sql, parameters, context);
                }
                var index = sql.Count;
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);

                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }

                return sql.Concat((index, sql.Count), (0, index));
            }
            public static string Select(LambdaExpression select, LambdaExpression where, LambdaExpression orderBy, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var context = new DbEntityContext();
                var entity = context.Add(select.Parameters[0]);
                if (where != null)
                {
                    context.Add(where.Parameters[0], entity);
                    sql.Add(" WHERE ");
                    Convert(where.Body, sql, parameters, context);
                }
                if (orderBy != null)
                {
                    context.Add(orderBy.Parameters[0], entity);
                    sql.Add(" ORDER BY ");
                    Convert(orderBy.Body, sql, parameters, context);
                }
                var index = sql.Count;
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);

                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }
                return sql.Concat((index, sql.Count), (0, index));
            }
            public static string Select(int offset, int fetch, LambdaExpression select, LambdaExpression where, LambdaExpression orderBy, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var context = new DbEntityContext();
                var entity = context.Add(select.Parameters[0]);
                if (where != null)
                {
                    context.Add(where.Parameters[0], entity);
                    sql.Add(" WHERE ");
                    Convert(where.Body, sql, parameters, context);
                }
                if (orderBy != null)
                {
                    context.Add(orderBy.Parameters[0], entity);
                    sql.Add(" ORDER BY ");
                    Convert(orderBy.Body, sql, parameters, context);
                }
                sql.Add($" LIMIT {offset},{fetch}");
                var index = sql.Count;
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);

                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }
                return sql.Concat((index, sql.Count), (0, index));
            }
            public static string SelectPaged(int offset, int fetch, LambdaExpression select, LambdaExpression from, LambdaExpression where, LambdaExpression orderBy, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var context = new DbEntityContext();
                var entity = context.Add(select.Parameters[0]);
                if (from != null)
                {
                    var fromSql = new List<string>();
                    Convert(from.Body, fromSql, parameters, context);
                    entity.Table.Name = fromSql.Concat();
                }
                if (where != null)
                {
                    context.Add(where.Parameters[0], entity);
                    sql.Add(" WHERE ");
                    Convert(where.Body, sql, parameters, context);
                }
                var whereIndex = sql.Count;
                if (orderBy != null)
                {
                    context.Add(orderBy.Parameters[0], entity);
                    sql.Add(" ORDER BY ");
                    Convert(orderBy.Body, sql, parameters, context);
                }
                sql.Add($" LIMIT {offset},{fetch}");
                var selectIndex = sql.Count;
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);
                var fromIndex = sql.Count;
                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }
                var countIndex = sql.Count;
                sql.Add(";SELECT COUNT(1)");
                return sql.Concat((selectIndex, countIndex), (0, selectIndex), (countIndex, sql.Count), (fromIndex, countIndex), (0, whereIndex));
            }
            public static string Select(LambdaExpression select, LambdaExpression from, LambdaExpression where, LambdaExpression groupBy, LambdaExpression having, LambdaExpression orderBy, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var context = new DbEntityContext();
                var entity = context.Add(select.Parameters[0]);
                if (from != null)
                {
                    var fromSql = new List<string>();
                    Convert(from.Body, fromSql, parameters, context);
                    entity.Table.Name = fromSql.Concat();
                }
                if (where != null)
                {
                    context.Add(where.Parameters[0], entity);
                    sql.Add(" WHERE ");
                    Convert(where.Body, sql, parameters, context);
                }
                if (groupBy != null)
                {
                    context.Add(groupBy.Parameters[0], entity);
                    sql.Add(" GROUP BY ");
                    Convert(groupBy.Body, sql, parameters, context);
                }
                if (having != null)
                {
                    context.Add(having.Parameters[0], entity);
                    sql.Add(" HAVING ");
                    Convert(having.Body, sql, parameters, context);
                }
                if (orderBy != null)
                {
                    context.Add(orderBy.Parameters[0], entity);
                    sql.Add(" ORDER BY ");
                    Convert(orderBy.Body, sql, parameters, context);
                }
                var index = sql.Count;
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);

                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }
                return sql.Concat((index, sql.Count), (0, index));
            }
            public static string Select(int offset, int fetch, LambdaExpression select, LambdaExpression from, LambdaExpression where, LambdaExpression groupBy, LambdaExpression having, LambdaExpression orderBy, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var context = new DbEntityContext();
                var entity = context.Add(select.Parameters[0]);
                if (from != null)
                {
                    var fromSql = new List<string>();
                    Convert(from.Body, fromSql, parameters, context);
                    entity.Table.Name = fromSql.Concat();
                }
                if (where != null)
                {
                    context.Add(where.Parameters[0], entity);
                    sql.Add(" WHERE ");
                    Convert(where.Body, sql, parameters, context);
                }
                if (groupBy != null)
                {
                    context.Add(groupBy.Parameters[0], entity);
                    sql.Add(" GROUP BY ");
                    Convert(groupBy.Body, sql, parameters, context);
                }
                if (having != null)
                {
                    context.Add(having.Parameters[0], entity);
                    sql.Add(" HAVING ");
                    Convert(having.Body, sql, parameters, context);
                }
                if (orderBy != null)
                {
                    context.Add(orderBy.Parameters[0], entity);
                    sql.Add(" ORDER BY ");
                    Convert(orderBy.Body, sql, parameters, context);
                }
                sql.Add($" LIMIT {offset},{fetch}");
                var index = sql.Count;
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);

                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }
                return sql.Concat((index, sql.Count), (0, index));
            }
            public SqliteDb(Type connectionType, Func<(DbCommand, IDisposable)> getCommand, Action<DbCommand> cmdExecuting)
            {
                if (_ConnectionType == null)
                {
                    lock (_Sync)
                    {
                        if (_ConnectionType == null)
                        {
                            _ConnectionType = connectionType;
                            _DataReaderType = connectionType.GetMethod("CreateCommand", Type.EmptyTypes).ReturnType.
                                GetMethod("ExecuteReader", Type.EmptyTypes).ReturnType;

                            _AddParameter = typeof(SqlDbExtensions).GetMethod("AddParameter").MakeGenericMethod(connectionType).
                               CreateDelegate<Action<DbCommand, string, object>>();

                            var member = Expression.Parameter(typeof(MemberExpression), "member");
                            var registerMember = typeof(SqlDbExtensions).GetMethod("RegisterDbMember", new[] { typeof(MemberExpression), typeof(IReadOnlyList<object>).MakeByRefType() });
                            var memberObjs = Expression.Variable(typeof(IReadOnlyList<object>), "exprObjs");
                            _DbMember = Expression.Lambda<Func<MemberExpression, IReadOnlyList<object>>>(
                                Expression.Block(new[] { memberObjs },
                                Expression.Call(null, registerMember.MakeGenericMethod(connectionType), member, memberObjs),
                                memberObjs), member).Compile();

                            var method = Expression.Parameter(typeof(MethodCallExpression), "method");
                            var registerMethod = typeof(SqlDbExtensions).GetMethod("RegisterDbMethod", new[] { typeof(MethodCallExpression), typeof(IReadOnlyList<object>).MakeByRefType() });
                            var methodObjs = Expression.Variable(typeof(IReadOnlyList<object>), "exprObjs");
                            _DbMethod = Expression.Lambda<Func<MethodCallExpression, IReadOnlyList<object>>>(
                                Expression.Block(new[] { methodObjs },
                                Expression.Call(null, registerMethod.MakeGenericMethod(connectionType), method, methodObjs),
                                methodObjs), method).Compile();
                        }
                    }
                }
                _getCommand = getCommand;
                _cmdExecuting = cmdExecuting;
            }
            public override int Execute(string sql)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    return cmd.ExecuteNonQuery();
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            public override int Execute(string sql, object parameters)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    Query.AddParameter(cmd, parameters);
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    return cmd.ExecuteNonQuery();
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            public override int Execute(string sql, IList<(string, object)> parameters)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    if (parameters != null && parameters.Count > 0)
                    {
                        for (int i = 0; i < parameters.Count; i++)
                        {
                            (var name, var value) = parameters[i];
                            _AddParameter(cmd, name, value);
                        }
                    }
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    return cmd.ExecuteNonQuery();
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            public override async Task<int> ExecuteAsync(string sql)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    return await cmd.ExecuteNonQueryAsync();
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            public override async Task<int> ExecuteAsync(string sql, object parameters)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    Query.AddParameter(cmd, parameters);
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    return await cmd.ExecuteNonQueryAsync();
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            public override async Task<int> ExecuteAsync(string sql, IList<(string, object)> parameters)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    if (parameters != null && parameters.Count > 0)
                    {
                        for (int i = 0; i < parameters.Count; i++)
                        {
                            (var name, var value) = parameters[i];
                            _AddParameter(cmd, name, value);
                        }
                    }
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    return await cmd.ExecuteNonQueryAsync();
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            public override int ExecuteFormat(FormattableString sqlFormat)
            {
                if (sqlFormat == null)
                    throw new ArgumentNullException(nameof(sqlFormat));

                var parameters = Parameters;
                var args = sqlFormat.GetArguments();
                var names = new string[args.Length];
                for (int i = 0; i < args.Length; i++)
                {
                    var name = Query.GetParameter(parameters.Count);
                    parameters.Add((name, args[i]));
                    names[i] = name;
                }
                return Execute(string.Format(sqlFormat.Format, names), parameters);
            }
            public override Task<int> ExecuteFormatAsync(FormattableString sqlFormat)
            {
                if (sqlFormat == null)
                    throw new ArgumentNullException(nameof(sqlFormat));

                var parameters = Parameters;
                var args = sqlFormat.GetArguments();
                var names = new string[args.Length];
                for (int i = 0; i < args.Length; i++)
                {
                    var name = Query.GetParameter(parameters.Count);
                    parameters.Add((name, args[i]));
                    names[i] = name;
                }
                return ExecuteAsync(string.Format(sqlFormat.Format, names), parameters);
            }
            public override (DbDataReader, IDisposable) ExecuteReader(string sql)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    var reader = cmd.ExecuteReader();
                    return (reader, disposable);
                }
                catch
                {
                    disposable.Dispose();
                    throw;
                }
            }
            public override (DbDataReader, IDisposable) ExecuteReader(string sql, object parameters)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    Query.AddParameter(cmd, parameters);
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    var reader = cmd.ExecuteReader();
                    return (reader, disposable);
                }
                catch
                {
                    disposable.Dispose();
                    throw;
                }
            }
            public override (DbDataReader, IDisposable) ExecuteReader(string sql, IList<(string, object)> parameters)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    if (parameters != null && parameters.Count > 0)
                    {
                        for (int i = 0; i < parameters.Count; i++)
                        {
                            (var name, var value) = parameters[i];
                            _AddParameter(cmd, name, value);
                        }
                    }
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    var reader = cmd.ExecuteReader();
                    return (reader, disposable);
                }
                catch
                {
                    disposable.Dispose();
                    throw;
                }
            }
            public override (DbDataReader, IDisposable) ExecuteReaderFormat(FormattableString sqlFormat)
            {
                if (sqlFormat == null)
                    throw new ArgumentNullException(nameof(sqlFormat));

                var parameters = Parameters;
                var args = sqlFormat.GetArguments();
                var names = new string[args.Length];
                for (int i = 0; i < args.Length; i++)
                {
                    var name = Query.GetParameter(parameters.Count);
                    parameters.Add((name, args[i]));
                    names[i] = name;
                }
                return ExecuteReader(string.Format(sqlFormat.Format, names), parameters);
            }
            public override async Task<(DbDataReader, IDisposable)> ExecuteReaderAsync(string sql)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    var reader = await cmd.ExecuteReaderAsync();
                    return (reader, disposable);
                }
                catch
                {
                    disposable.Dispose();
                    throw;
                }
            }
            public override async Task<(DbDataReader, IDisposable)> ExecuteReaderAsync(string sql, object parameters)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    Query.AddParameter(cmd, parameters);
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    var reader = await cmd.ExecuteReaderAsync();
                    return (reader, disposable);
                }
                catch
                {
                    disposable.Dispose();
                    throw;
                }
            }
            public override async Task<(DbDataReader, IDisposable)> ExecuteReaderAsync(string sql, IList<(string, object)> parameters)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    if (parameters != null && parameters.Count > 0)
                    {
                        for (int i = 0; i < parameters.Count; i++)
                        {
                            (var name, var value) = parameters[i];
                            _AddParameter(cmd, name, value);
                        }
                    }
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    var reader = await cmd.ExecuteReaderAsync();
                    return (reader, disposable);
                }
                catch
                {
                    disposable.Dispose();
                    throw;
                }
            }
            public override Task<(DbDataReader, IDisposable)> ExecuteReaderFormatAsync(FormattableString sqlFormat)
            {
                if (sqlFormat == null)
                    throw new ArgumentNullException(nameof(sqlFormat));

                var parameters = Parameters;
                var args = sqlFormat.GetArguments();
                var names = new string[args.Length];
                for (int i = 0; i < args.Length; i++)
                {
                    var name = Query.GetParameter(parameters.Count);
                    parameters.Add((name, args[i]));
                    names[i] = name;
                }
                return ExecuteReaderAsync(string.Format(sqlFormat.Format, names), parameters);
            }
            public override TEntity Read<TEntity>(DbDataReader dataReader)
            {
                var handler = Reader<TEntity>.Handler;
                if (handler == null)
                {
                    lock (_Sync)
                    {
                        handler = Reader<TEntity>.Handler;
                        if (handler == null)
                        {
                            var register = typeof(SqlDbExtensions).GetMethod("RegisterDbReader", new[] { typeof(Type), typeof(ParameterExpression), typeof(Expression).MakeByRefType(), typeof(Delegate).MakeByRefType() }).MakeGenericMethod(_DataReaderType);
                            var dbReader = Expression.Parameter(typeof(DbDataReader), "dbReader");
                            var reader = Expression.Variable(_DataReaderType, "reader");
                            var args = new object[] { typeof(TEntity), reader, default(Expression), default(Delegate) };
                            register.Invoke(null, args);
                            var read = _DataReaderType.GetMethod("Read", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
                            var expr = Expression.Block(new[] { reader },
                                Expression.Assign(reader, Expression.Convert(dbReader, _DataReaderType)),
                                Expression.Condition(Expression.Call(reader, read), (Expression)args[2], Expression.Default(typeof(TEntity))));
                            handler = Expression.Lambda<Func<DbDataReader, TEntity>>(expr, dbReader).Compile();
                            Reader<TEntity>.Handler = handler;
                        }
                    }
                }
                return handler(dataReader);
            }
            public override int Insert<TEntity>(Expression<Func<SqlExpression, TEntity>> entity)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                var parameters = Parameters;
                var sql = Insert(entity, parameters);
                return Execute(sql, parameters);
            }
            public override int Insert<TEntity>(TEntity entity, Expression<Func<TEntity, object>> properties)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));

                var parameters = Parameters;
                var sql = Insert(entity, properties, parameters);
                return Execute(sql, parameters);
            }
            public override int InsertRange<TEntity>(IList<TEntity> entities, Expression<Func<TEntity, object>> properties)
            {
                if (entities == null)
                    throw new ArgumentNullException(nameof(entities));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));

                var result = 0;
                var sql = new List<string>();
                var parameters = new List<(string, object)>();
                var table = DbTable.Get(properties.Parameters[0].Type);
                var fields = GetFields(table, properties.Body);
                for (int index = 0; ;)
                {
                    if (index == entities.Count)
                        break;
                    var entity = entities[index++];
                    var count = 1;
                    var fieldCount = fields.Count;
                    sql.Clear();
                    parameters.Clear();
                    sql.Add("INSERT INTO ");
                    sql.Add(table.Name);
                    sql.Add("(");
                    var enumerator = fields.GetEnumerator();
                    if (enumerator.MoveNext())
                    {
                        sql.Add(enumerator.Current.Value);
                    }
                    while (enumerator.MoveNext())
                    {
                        sql.Add(",");
                        sql.Add(enumerator.Current.Value);
                    }
                    sql.Add(") VALUES (");
                    enumerator = fields.GetEnumerator();
                    if (enumerator.MoveNext())
                    {
                        var value = enumerator.Current.Key.GetValue(entity);
                        var parameter = Query.GetParameter(parameters.Count);
                        parameters.Add((parameter, value));
                        sql.Add(parameter);
                    }
                    while (enumerator.MoveNext())
                    {
                        sql.Add(",");
                        var value = enumerator.Current.Key.GetValue(entity);
                        var parameter = Query.GetParameter(parameters.Count);
                        parameters.Add((parameter, value));
                        sql.Add(parameter);
                    }
                    sql.Add(")");
                    while (index < entities.Count)
                    {
                        if (count == 5000 || fieldCount + fields.Count >= 999)
                            break;
                        count += 1;
                        fieldCount += fields.Count;
                        entity = entities[index++];
                        sql.Add(",(");
                        enumerator = fields.GetEnumerator();
                        if (enumerator.MoveNext())
                        {
                            var value = enumerator.Current.Key.GetValue(entity);
                            var parameter = Query.GetParameter(parameters.Count);
                            parameters.Add((parameter, value));
                            sql.Add(parameter);
                        }
                        while (enumerator.MoveNext())
                        {
                            sql.Add(",");
                            var value = enumerator.Current.Key.GetValue(entity);
                            var parameter = Query.GetParameter(parameters.Count);
                            parameters.Add((parameter, value));
                            sql.Add(parameter);
                        }
                        sql.Add(")");
                    }
                    result += Execute(sql.Concat(), parameters);
                }
                return result;
            }
            public override Task<int> InsertAsync<TEntity>(Expression<Func<SqlExpression, TEntity>> entity)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                var parameters = Parameters;
                var sql = Insert(entity, parameters);
                return ExecuteAsync(sql, parameters);
            }
            public override Task<int> InsertAsync<TEntity>(TEntity entity, Expression<Func<TEntity, object>> properties)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));

                var parameters = Parameters;
                var sql = Insert(entity, properties, parameters);
                return ExecuteAsync(sql, parameters);
            }
            public override async Task<int> InsertRangeAsync<TEntity>(IList<TEntity> entities, Expression<Func<TEntity, object>> properties)
            {
                if (entities == null)
                    throw new ArgumentNullException(nameof(entities));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));

                var result = 0;
                var sql = new List<string>();
                var parameters = new List<(string, object)>();
                var table = DbTable.Get(properties.Parameters[0].Type);
                var fields = GetFields(table, properties.Body);
                for (int index = 0; ;)
                {
                    if (index == entities.Count)
                        break;
                    var entity = entities[index++];
                    var count = 1;
                    var fieldCount = fields.Count;
                    sql.Clear();
                    parameters.Clear();
                    sql.Add("INSERT INTO ");
                    sql.Add(table.Name);
                    sql.Add("(");
                    var enumerator = fields.GetEnumerator();
                    if (enumerator.MoveNext())
                    {
                        sql.Add(enumerator.Current.Value);
                    }
                    while (enumerator.MoveNext())
                    {
                        sql.Add(",");
                        sql.Add(enumerator.Current.Value);
                    }
                    sql.Add(") VALUES (");
                    enumerator = fields.GetEnumerator();
                    if (enumerator.MoveNext())
                    {
                        var value = enumerator.Current.Key.GetValue(entity);
                        var parameter = Query.GetParameter(parameters.Count);
                        parameters.Add((parameter, value));
                        sql.Add(parameter);
                    }
                    while (enumerator.MoveNext())
                    {
                        sql.Add(",");
                        var value = enumerator.Current.Key.GetValue(entity);
                        var parameter = Query.GetParameter(parameters.Count);
                        parameters.Add((parameter, value));
                        sql.Add(parameter);
                    }
                    sql.Add(")");
                    while (index < entities.Count)
                    {
                        if (count == 5000 || fieldCount + fields.Count >= 999)
                            break;
                        count += 1;
                        fieldCount += fields.Count;
                        entity = entities[index++];
                        sql.Add(",(");
                        enumerator = fields.GetEnumerator();
                        if (enumerator.MoveNext())
                        {
                            var value = enumerator.Current.Key.GetValue(entity);
                            var parameter = Query.GetParameter(parameters.Count);
                            parameters.Add((parameter, value));
                            sql.Add(parameter);
                        }
                        while (enumerator.MoveNext())
                        {
                            sql.Add(",");
                            var value = enumerator.Current.Key.GetValue(entity);
                            var parameter = Query.GetParameter(parameters.Count);
                            parameters.Add((parameter, value));
                            sql.Add(parameter);
                        }
                        sql.Add(")");
                    }
                    result += await ExecuteAsync(sql.Concat(), parameters);
                }
                return result;
            }
            public override TIdentity InsertIdentity<TEntity, TIdentity>(Expression<Func<SqlExpression, TEntity>> entity)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                var parameters = Parameters;
                var sql = InsertIdentity(entity, parameters);
                return this.Execute<TIdentity>(sql, parameters);
            }
            public override TIdentity InsertIdentity<TEntity, TIdentity>(TEntity entity, Expression<Func<TEntity, object>> properties)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));

                var parameters = Parameters;
                var sql = InsertIdentity(entity, properties, parameters);
                return this.Execute<TIdentity>(sql, parameters);
            }
            public override Task<TIdentity> InsertIdentityAsync<TEntity, TIdentity>(Expression<Func<SqlExpression, TEntity>> entity)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                var parameters = Parameters;
                var sql = InsertIdentity(entity, parameters);
                return this.ExecuteAsync<TIdentity>(sql, parameters);
            }
            public override Task<TIdentity> InsertIdentityAsync<TEntity, TIdentity>(TEntity entity, Expression<Func<TEntity, object>> properties)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));

                var parameters = Parameters;
                var sql = InsertIdentity(entity, properties, parameters);
                return this.ExecuteAsync<TIdentity>(sql, parameters);
            }
            public override int Delete<TEntity>(object identity)
            {
                var parameters = Parameters;
                var sql = Delete(DbTable.Get(typeof(TEntity)), identity, parameters);
                return Execute(sql, parameters);
            }
            public override int Delete<TEntity>(Expression<Func<TEntity, SqlExpression, bool>> where)
            {
                if (where == null)
                    throw new ArgumentNullException(nameof(where));

                var parameters = Parameters;
                var sql = Delete(DbTable.Get(typeof(TEntity)), where, parameters);
                return Execute(sql, parameters);
            }
            public override Task<int> DeleteAsync<TEntity>(object identity)
            {
                var parameters = Parameters;
                var sql = Delete(DbTable.Get(typeof(TEntity)), identity, parameters);
                return ExecuteAsync(sql, parameters);
            }
            public override Task<int> DeleteAsync<TEntity>(Expression<Func<TEntity, SqlExpression, bool>> where)
            {
                if (where == null)
                    throw new ArgumentNullException(nameof(where));

                var parameters = Parameters;
                var sql = Delete(DbTable.Get(typeof(TEntity)), where, parameters);
                return ExecuteAsync(sql, parameters);
            }
            public override int Update<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> entity, object identity)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                var parameters = Parameters;
                var sql = Update(entity, identity, parameters);
                return Execute(sql, parameters);
            }
            public override int Update<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> entity, Expression<Func<TEntity, SqlExpression, bool>> where)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (where == null)
                    throw new ArgumentNullException(nameof(where));

                var parameters = Parameters;
                var sql = Update(entity, where, parameters);
                return Execute(sql, parameters);
            }
            public override int Update<TEntity>(TEntity entity, Expression<Func<TEntity, object>> properties)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));

                SqlDbExtensions.RegisterIdentity<TEntity>(out var identity);
                if (identity == null)
                    throw new ArgumentNullException(nameof(identity));

                var parameters = Parameters;
                var sql = Update(entity, properties, identity.GetValue(entity), parameters);
                return Execute(sql, parameters);
            }
            public override int Update<TEntity>(TEntity entity, Expression<Func<TEntity, object>> properties, Expression<Func<TEntity, SqlExpression, bool>> where)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));
                if (where == null)
                    throw new ArgumentNullException(nameof(where));

                var parameters = Parameters;
                var sql = Update(entity, properties, where, parameters);
                return Execute(sql, parameters);
            }
            public override Task<int> UpdateAsync<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> entity, object identity)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                var parameters = Parameters;
                var sql = Update(entity, identity, parameters);
                return ExecuteAsync(sql, parameters);
            }
            public override Task<int> UpdateAsync<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> entity, Expression<Func<TEntity, SqlExpression, bool>> where)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (where == null)
                    throw new ArgumentNullException(nameof(where));

                var parameters = Parameters;
                var sql = Update(entity, where, parameters);
                return ExecuteAsync(sql, parameters);
            }
            public override Task<int> UpdateAsync<TEntity>(TEntity entity, Expression<Func<TEntity, object>> properties)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));

                SqlDbExtensions.RegisterIdentity<TEntity>(out var identity);
                if (identity == null)
                    throw new ArgumentNullException(nameof(identity));

                var parameters = Parameters;
                var sql = Update(entity, properties, identity.GetValue(entity), parameters);
                return ExecuteAsync(sql, parameters);
            }
            public override Task<int> UpdateAsync<TEntity>(TEntity entity, Expression<Func<TEntity, object>> properties, Expression<Func<TEntity, SqlExpression, bool>> where)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));
                if (where == null)
                    throw new ArgumentNullException(nameof(where));

                var parameters = Parameters;
                var sql = Update(entity, properties, where, parameters);
                return ExecuteAsync(sql, parameters);
            }
            public override TEntity SelectSingle<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> select, object identity)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = SelectSingle(select, identity, parameters);
                return this.Execute<TEntity>(sql, parameters);
            }
            public override Task<TEntity> SelectSingleAsync<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> select, object identity)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = SelectSingle(select, identity, parameters);
                return this.ExecuteAsync<TEntity>(sql, parameters);
            }
            public override TEntity SelectSingle<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = SelectSingle(select, where, parameters);
                return this.Execute<TEntity>(sql, parameters);
            }
            public override Task<TEntity> SelectSingleAsync<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = SelectSingle(select, where, parameters);
                return this.ExecuteAsync<TEntity>(sql, parameters);
            }
            public override List<TEntity> Select<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = Select(select, where, orderBy, parameters);
                return this.Execute<List<TEntity>>(sql, parameters);
            }
            public override Task<List<TEntity>> SelectAsync<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = Select(select, where, orderBy, parameters);
                return this.ExecuteAsync<List<TEntity>>(sql, parameters);
            }
            public override List<TEntity> Select<TEntity>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = Select(offset, fetch, select, where, orderBy, parameters);
                return this.Execute<List<TEntity>>(sql, parameters);
            }
            public override Task<List<TEntity>> SelectAsync<TEntity>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = Select(offset, fetch, select, where, orderBy, parameters);
                return this.ExecuteAsync<List<TEntity>>(sql, parameters);
            }
            public override (List<TEntity>, int) SelectPaged<TEntity>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = SelectPaged(offset, fetch, select, null, where, orderBy, parameters);
                return this.Execute<List<TEntity>, int>(sql, parameters);
            }
            public override Task<(List<TEntity>, int)> SelectPagedAsync<TEntity>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = SelectPaged(offset, fetch, select, null, where, orderBy, parameters);
                return this.ExecuteAsync<List<TEntity>, int>(sql, parameters);
            }
            public override TResult Select<TEntity, TResult>(Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<SqlExpression, object>> from, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Func<TEntity, SqlExpression, object>> groupBy, Expression<Func<TEntity, SqlExpression, bool>> having, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = Select(select, from, where, groupBy, having, orderBy, parameters);
                return this.Execute<TResult>(sql, parameters);
            }
            public override Task<TResult> SelectAsync<TEntity, TResult>(Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<SqlExpression, object>> from, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Func<TEntity, SqlExpression, object>> groupBy, Expression<Func<TEntity, SqlExpression, bool>> having, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = Select(select, from, where, groupBy, having, orderBy, parameters);
                return this.ExecuteAsync<TResult>(sql, parameters);
            }
            public override TResult Select<TEntity, TResult>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<SqlExpression, object>> from, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Func<TEntity, SqlExpression, object>> groupBy, Expression<Func<TEntity, SqlExpression, bool>> having, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = Select(offset, fetch, select, from, where, groupBy, having, orderBy, parameters);
                return this.Execute<TResult>(sql, parameters);
            }
            public override Task<TResult> SelectAsync<TEntity, TResult>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<SqlExpression, object>> from, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Func<TEntity, SqlExpression, object>> groupBy, Expression<Func<TEntity, SqlExpression, bool>> having, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = Select(offset, fetch, select, from, where, groupBy, having, orderBy, parameters);
                return this.ExecuteAsync<TResult>(sql, parameters);
            }
            public override (TResult, int) SelectPaged<TEntity, TResult>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<SqlExpression, object>> from, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = SelectPaged(offset, fetch, select, from, where, orderBy, parameters);
                return this.Execute<TResult, int>(sql, parameters);
            }
            public override Task<(TResult, int)> SelectPagedAsync<TEntity, TResult>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<SqlExpression, object>> from, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = SelectPaged(offset, fetch, select, from, where, orderBy, parameters);
                return this.ExecuteAsync<TResult, int>(sql, parameters);
            }
        }
        #endregion

        #region OracleDb
        public class OracleDb : SqlDb
        {
            private static object _Sync = new object();
            private static Type _ConnectionType;
            private static Type _DataReaderType;
            private static Action<DbCommand, string, object> _AddParameter;
            private static Func<string, int, DbParameter> _DbOutputParameter;
            private static Func<MemberExpression, IReadOnlyList<object>> _DbMember;
            private static Func<MethodCallExpression, IReadOnlyList<object>> _DbMethod;
            [ThreadStatic] private static List<string> _CommandText;
            [ThreadStatic] private static List<(string, object)> _Parameters;
            private Func<(DbCommand, IDisposable)> _getCommand;
            private Action<DbCommand> _cmdExecuting;
            public static List<string> CommandText
            {
                get
                {
                    var commandText = _CommandText;
                    if (commandText == null)
                    {
                        commandText = new List<string>();
                        _CommandText = commandText;
                    }
                    else
                    {
                        commandText.Clear();
                    }
                    return commandText;
                }
            }
            public static List<(string, object)> Parameters
            {
                get
                {
                    var parameters = _Parameters;
                    if (parameters == null)
                    {
                        parameters = new List<(string, object)>();
                        _Parameters = parameters;
                    }
                    else
                    {
                        parameters.Clear();
                    }
                    return parameters;
                }
            }
            private static class Query
            {
                private static object _Sync;
                private static string[] _Parameters;
                private static string[] _Tables;
                private static Dictionary<Type, Action<DbCommand, object>> _Handlers;
                static Query()
                {
                    _Sync = new object();
                    _Parameters = new[]
                    {
                        ":p0",":p1",":p2",":p3",":p4",":p5",":p6",":p7",
                        ":p8",":p9",":p10",":p11",":p12",":p13",":p14",":p15",
                        ":p16",":p17",":p18",":p19",":p20",":p21",":p22",":p23",
                        ":p24",":p25",":p26",":p27",":p28",":p29",":p30",":p31",
                        ":p32",":p33",":p34",":p35",":p36",":p37",":p38",":p39",
                        ":p40",":p41",":p42",":p43",":p44",":p45",":p46",":p47",
                        ":p48",":p49",":p50",":p51",":p52",":p53",":p54",":p55",
                        ":p56",":p57",":p58",":p59",":p60",":p61",":p62",":p63"
                    };
                    _Tables = new[]
                    {
                        "t0","t1","t2","t3","t4","t5","t6","t7",
                        "t8","t9","t10","t11","t12","t13","t14","t15"
                    };
                    _Handlers = new Dictionary<Type, Action<DbCommand, object>>();
                }
                public static string GetParameter(int index)
                {
                    return index < _Parameters.Length ? _Parameters[index] : $":p{index}";
                }
                public static string GetTable(int index)
                {
                    return index < _Tables.Length ? _Tables[index] : $"t{index}";
                }
                public static void AddParameter(DbCommand command, object objParameters)
                {
                    if (objParameters == null)
                        return;

                    var type = objParameters.GetType();
                    if (!_Handlers.TryGetValue(type, out var handler))
                    {
                        lock (_Sync)
                        {
                            if (!_Handlers.TryGetValue(type, out handler))
                            {
                                var cmd = Expression.Parameter(typeof(DbCommand), "cmd");
                                var objValue = Expression.Parameter(typeof(object), "objValue");
                                var tValue = Expression.Variable(type, "value");
                                var addParameter = typeof(SqlDbExtensions).GetMethod("AddParameter").MakeGenericMethod(_ConnectionType);
                                var properties = type.GetGeneralProperties(BindingFlags.Instance | BindingFlags.Public);
                                var exprs = new List<Expression>();
                                exprs.Add(Expression.Assign(tValue, Expression.Convert(objValue, type)));
                                foreach (var property in properties)
                                {
                                    if (!property.CanRead)
                                        continue;
                                    exprs.Add(Expression.Call(
                                        addParameter, cmd,
                                        Expression.Constant($":{property.Name}"),
                                        Expression.Convert(Expression.Property(tValue, property), typeof(object))
                                        ));
                                }
                                handler = Expression.Lambda<Action<DbCommand, object>>(Expression.Block(new[] { tValue }, exprs), cmd, objValue).Compile();
                                var handlers = new Dictionary<Type, Action<DbCommand, object>>(_Handlers);
                                handlers.Add(type, handler);
                                _Handlers = handlers;
                            }
                        }
                    }
                    handler(command, objParameters);
                }
            }
            private static class Reader<TEntity>
            {
                public static Func<DbDataReader, TEntity> Handler;
            }
            public class DbTable
            {
                private class Comparer : IEqualityComparer<PropertyInfo>
                {
                    public bool Equals(PropertyInfo x, PropertyInfo y) =>
                        x.MetadataToken == y.MetadataToken && x.DeclaringType == y.DeclaringType;
                    public int GetHashCode(PropertyInfo obj) => obj.MetadataToken;
                }

                public string Name;
                public string Identity;
                public string Join;
                public Dictionary<PropertyInfo, string> Fields;
                public Dictionary<PropertyInfo, string> Columns;
                public Dictionary<PropertyInfo, DbTable> Tables;

                private static int _MaxDepth = 8;//TODO??_TypeReference
                private static Comparer _Comparer = new Comparer();
                private static object _Sync = new object();
                private static Dictionary<Type, DbTable> _Tables = new Dictionary<Type, DbTable>();
                public static DbTable Get(Type type)
                {
                    if (!_Tables.TryGetValue(type, out var table))
                    {
                        lock (_Sync)
                        {
                            if (!_Tables.TryGetValue(type, out table))
                            {
                                table = Get(0, null, null, type);
                            }
                        }
                    }
                    return table;
                }
                private static DbTable Get(int depth, string prefix, string join, Type type)
                {
                    var table = new DbTable();
                    table.Join = join;
                    table.Fields = new Dictionary<PropertyInfo, string>(_Comparer);
                    table.Columns = new Dictionary<PropertyInfo, string>(_Comparer);
                    table.Tables = new Dictionary<PropertyInfo, DbTable>(_Comparer);
                    var registerReader = typeof(SqlDbExtensions).GetMethod("RegisterDbReader", new[] { typeof(Type), typeof(ParameterExpression), typeof(ParameterExpression), typeof(Expression).MakeByRefType() })
                    .MakeGenericMethod(_DataReaderType);
                    var readerArgs = new object[] { null, Expression.Parameter(_DataReaderType), Expression.Parameter(typeof(int)), null };
                    SqlDbExtensions.RegisterTable(out var tableResolver);
                    SqlDbExtensions.RegisterIdentity(out var identityResolver);
                    SqlDbExtensions.RegisterProperty(out var propertyResolver);

                    var dataTableAttribute = type.GetCustomAttribute<DataTableAttribute>();
                    table.Name = $"\"{dataTableAttribute?.Name ?? tableResolver(type)}\"";
                    var identityProperty = identityResolver(type);
                    if (identityProperty != null)
                    {
                        var dataColumnAttribute = identityProperty.GetCustomAttribute<DataColumnAttribute>();
                        table.Identity = $"\"{dataColumnAttribute?.Name ?? propertyResolver(identityProperty)}\"";
                    }
                    var properties = type.GetGeneralProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    foreach (var property in properties)
                    {
                        if (!property.CanWrite)
                            continue;
                        if (property.IsDefined(typeof(IgnoreDataColumnAttribute)))
                            continue;
                        var dataColumnAttribute = property.GetCustomAttribute<DataColumnAttribute>();
                        readerArgs[0] = property.PropertyType;
                        registerReader.Invoke(null, readerArgs);
                        if (readerArgs[3] != null)
                        {
                            string columnName = dataColumnAttribute?.Name ?? propertyResolver(property);
                            table.Fields.Add(property, $"\"{columnName}\"");
                            table.Columns.Add(property, $"\"{prefix}{columnName}\"");
                        }
                        else
                        {
                            if (depth > _MaxDepth)
                                continue;
                            var joinIdentityProperty = identityResolver(property.PropertyType);
                            if (joinIdentityProperty == null)
                                continue;
                            var joinName = dataColumnAttribute?.Name;
                            if (joinName == null)
                            {
                                var onPropery = type.GetProperty($"{property.Name}{joinIdentityProperty.Name}", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                if (onPropery == null)
                                {
                                    if (joinIdentityProperty.Name == $"{type.Name}{identityProperty.Name}")
                                    {
                                        onPropery = identityProperty;
                                    }
                                    else
                                    {
                                        continue;
                                    }
                                }
                                dataColumnAttribute = onPropery.GetCustomAttribute<DataColumnAttribute>();
                                joinName = dataColumnAttribute?.Name ?? propertyResolver(onPropery);
                            }
                            joinName = $"\"{joinName}\"";
                            string columnName = propertyResolver(property);
                            var propertyPrefix = $"{prefix}{columnName}.";
                            var propertyTable = Get(depth + 1, propertyPrefix, joinName, property.PropertyType);
                            table.Tables.Add(property, propertyTable);
                        }
                    }
                    return table;
                }
            }
            public class DbEntity
            {
                public string Name;
                public DbTable Table;
                public Dictionary<PropertyInfo, DbEntity> Entities;
            }
            public class DbEntityContext
            {
                private int _table;
                private Dictionary<ParameterExpression, DbEntity> _entities;
                public DbEntityContext()
                {
                    _entities = new Dictionary<ParameterExpression, DbEntity>();
                }
                public DbEntity Add(ParameterExpression parameter)
                {
                    var entity = new DbEntity()
                    {
                        Name = Query.GetTable(_table++),
                        Table = DbTable.Get(parameter.Type),
                        Entities = new Dictionary<PropertyInfo, DbEntity>()
                    };
                    _entities.Add(parameter, entity);
                    return entity;
                }
                public void Add(ParameterExpression parameter, DbEntity entity)
                {
                    _entities.Add(parameter, entity);
                }
                public DbEntity Add(DbEntity entity, PropertyInfo property)
                {
                    if (entity.Entities.TryGetValue(property, out var propertyEntity))
                    {
                        return propertyEntity;
                    }
                    else if (entity.Table.Tables.TryGetValue(property, out var entityTable))
                    {
                        propertyEntity = new DbEntity();
                        propertyEntity.Name = Query.GetTable(_table++);
                        propertyEntity.Table = entityTable;
                        propertyEntity.Entities = new Dictionary<PropertyInfo, DbEntity>();
                        entity.Entities.Add(property, propertyEntity);
                        return propertyEntity;
                    }
                    return null;
                }
                public DbEntity Convert(Expression expression)
                {
                    if (expression == null)
                        return null;

                    switch (expression.NodeType)
                    {
                        case ExpressionType.Convert:
                            return Convert(((UnaryExpression)expression).Operand);
                        case ExpressionType.Parameter:
                            {
                                _entities.TryGetValue((ParameterExpression)expression, out var entity);
                                return entity;
                            }
                        case ExpressionType.MemberAccess:
                            {
                                var expr = (MemberExpression)expression;
                                if (expr.Member is PropertyInfo property)
                                {
                                    var propertyEntity = Convert(expr.Expression);
                                    if (propertyEntity != null)
                                    {
                                        if (propertyEntity.Entities.TryGetValue(property, out var entity))
                                        {
                                            return entity;
                                        }
                                        else if (propertyEntity.Table.Tables.TryGetValue(property, out var propertyTable))
                                        {
                                            entity = new DbEntity();
                                            entity.Name = Query.GetTable(_table++);
                                            entity.Table = propertyTable;
                                            entity.Entities = new Dictionary<PropertyInfo, DbEntity>();
                                            propertyEntity.Entities.Add(property, entity);
                                            return entity;
                                        }
                                    }
                                }
                            }
                            return null;
                        default:
                            return null;
                    }
                }
            }
            public static Dictionary<PropertyInfo, string> GetFields(DbTable table, Expression expression)
            {
                switch (expression.NodeType)
                {
                    case ExpressionType.Parameter:
                        return table.Fields;
                    case ExpressionType.Convert:
                        expression = ((UnaryExpression)expression).Operand;
                        if (expression.NodeType == ExpressionType.MemberAccess)
                            goto case ExpressionType.MemberAccess;
                        break;
                    case ExpressionType.MemberAccess:
                        {
                            var expr = (MemberExpression)expression;
                            if (expr.Member is PropertyInfo property
                                && table.Fields.TryGetValue(property, out var field))
                            {
                                return new Dictionary<PropertyInfo, string>()
                                {
                                    { property,field}
                                };
                            }
                        }
                        break;
                    case ExpressionType.New:
                        {
                            var fields = new Dictionary<PropertyInfo, string>();
                            var exprArgs = ((NewExpression)expression).Arguments;
                            for (int i = 0; i < exprArgs.Count; i++)
                            {
                                var expr = exprArgs[i].NodeType == ExpressionType.Convert ?
                                   ((UnaryExpression)exprArgs[i]).Operand as MemberExpression
                                   : exprArgs[i] as MemberExpression;
                                if (expr != null && expr.Member is PropertyInfo property
                                    && table.Fields.TryGetValue(property, out var field))
                                {
                                    fields.Add(property, field);
                                }
                            }
                            return fields;
                        }
                    case ExpressionType.Call:
                        {
                            if (SqlDbExtensions.TryExcept((MethodCallExpression)expression, out var except))
                            {
                                switch (except.NodeType)
                                {
                                    case ExpressionType.Convert:
                                        except = ((UnaryExpression)except).Operand;
                                        if (except.NodeType == ExpressionType.MemberAccess)
                                            goto case ExpressionType.MemberAccess;
                                        break;
                                    case ExpressionType.MemberAccess:
                                        {
                                            var expr = (MemberExpression)except;
                                            if (expr.Member is PropertyInfo property && table.Fields.ContainsKey(property))
                                            {
                                                var fields = new Dictionary<PropertyInfo, string>(table.Fields);
                                                fields.Remove(property);
                                                return fields;
                                            }
                                        }
                                        break;
                                    case ExpressionType.New:
                                        {
                                            var fields = new Dictionary<PropertyInfo, string>(table.Fields);
                                            var exprArgs = ((NewExpression)except).Arguments;
                                            for (int i = 0; i < exprArgs.Count; i++)
                                            {
                                                var expr = exprArgs[i].NodeType == ExpressionType.Convert ?
                                                   ((UnaryExpression)exprArgs[i]).Operand as MemberExpression
                                                   : exprArgs[i] as MemberExpression;
                                                if (expr != null && expr.Member is PropertyInfo property && fields.ContainsKey(property))
                                                {
                                                    fields.Remove(property);
                                                }
                                            }
                                            return fields;
                                        }
                                    default:
                                        break;
                                }
                                return new Dictionary<PropertyInfo, string>(table.Fields);
                            }
                        }
                        break;
                    default:
                        break;
                }
                return new Dictionary<PropertyInfo, string>();
            }
            public static void Join(DbEntity entity, DbEntity joinEntity, List<string> sql)
            {
                sql.Add(" LEFT JOIN ");
                sql.Add(joinEntity.Table.Name);
                sql.Add(" ");
                sql.Add(joinEntity.Name);
                sql.Add(" ON ");
                sql.Add(joinEntity.Name);
                sql.Add(".");
                sql.Add(joinEntity.Table.Identity);
                sql.Add("=");
                sql.Add(entity.Name);
                sql.Add(".");
                sql.Add(joinEntity.Table.Join);
                foreach (var item in joinEntity.Entities)
                {
                    Join(joinEntity, item.Value, sql);
                }
            }
            public static void Convert(Expression expression, List<string> sql, List<(string, object)> parameters, DbEntityContext context)
            {
                switch (expression.NodeType)
                {
                    case ExpressionType.Parameter:
                        {
                            var entity = context.Convert(expression);
                            if (entity != null)
                            {
                                foreach ((_, var field) in entity.Table.Fields)
                                {
                                    sql.Add(entity.Name);
                                    sql.Add(".");
                                    sql.Add(field);
                                    sql.Add(",");
                                }
                                sql.RemoveAt(sql.Count - 1);
                            }
                        }
                        break;
                    case ExpressionType.Constant:
                        {
                            var value = ((ConstantExpression)expression).Value;
                            var parameter = Query.GetParameter(parameters.Count);
                            sql.Add(parameter);
                            parameters.Add((parameter, value));
                        }
                        break;
                    case ExpressionType.MemberAccess:
                        {
                            var expr = (MemberExpression)expression;
                            var exprObjs = _DbMember(expr);
                            if (exprObjs != null)
                            {
                                for (int i = 0; i < exprObjs.Count; i++)
                                {
                                    var exprObj = exprObjs[i] as Expression;
                                    if (exprObj != null)
                                        Convert(exprObj, sql, parameters, context);
                                    else
                                        sql.Add((string)exprObjs[i]);
                                }
                                break;
                            }
                            if (expr.Member is PropertyInfo property)
                            {
                                var entity = context.Convert(expr.Expression);
                                if (entity != null)
                                {
                                    if (entity.Table.Fields.TryGetValue(property, out var field))
                                    {
                                        sql.Add(entity.Name);
                                        sql.Add(".");
                                        sql.Add(field);
                                    }
                                    else
                                    {
                                        var propertyEntity = context.Add(entity, property);
                                        if (propertyEntity != null)
                                        {
                                            foreach ((_, var propertyField) in propertyEntity.Table.Fields)
                                            {
                                                sql.Add(propertyEntity.Name);
                                                sql.Add(".");
                                                sql.Add(propertyField);
                                                sql.Add(",");
                                            }
                                            sql.RemoveAt(sql.Count - 1);
                                        }
                                    }
                                    break;
                                }
                            }
                            var value = expr.Invoke();
                            var parameter = Query.GetParameter(parameters.Count);
                            sql.Add(parameter);
                            parameters.Add((parameter, value));
                        }
                        break;
                    case ExpressionType.Call:
                        {
                            var expr = (MethodCallExpression)expression;
                            var exprObjs = _DbMethod(expr);
                            if (exprObjs != null)
                            {
                                for (int i = 0; i < exprObjs.Count; i++)
                                {
                                    var exprObj = exprObjs[i] as Expression;
                                    if (exprObj != null)
                                        Convert(exprObj, sql, parameters, context);
                                    else
                                        sql.Add((string)exprObjs[i]);
                                }
                            }
                            else if (SqlDbExtensions.TrySelect(expr, out var objSelect))
                            {
                                Select(objSelect, sql, parameters, context);
                            }
                            else if (SqlDbExtensions.TrySelect(expr, out var select, out var where, out var groupBy, out var having, out var orderBy))
                            {
                                sql.Add("(");
                                sql.Add(Select(select, where, groupBy, having, orderBy, parameters, context));
                                sql.Add(")");
                            }
                            else if (SqlDbExtensions.TrySelect(expr, out var offset, out var fetch, out select, out where, out groupBy, out having, out orderBy))
                            {
                                sql.Add("(");
                                sql.Add(Select(offset, fetch, select, where, groupBy, having, orderBy, parameters, context));
                                sql.Add(")");
                            }
                            else if (SqlDbExtensions.TryNavigate(expr, out var navigate))
                            {
                                var entity = context.Convert(navigate);
                                if (entity == null)
                                {
                                    Convert(navigate, sql, parameters, context);
                                }
                                else
                                {
                                    Navigate(entity, sql, context);
                                    sql.RemoveAt(sql.Count - 1);
                                }
                            }
                            else
                            {
                                var value = expression.Invoke();
                                var parameter = Query.GetParameter(parameters.Count);
                                sql.Add(parameter);
                                parameters.Add((parameter, value));
                            }
                        }
                        break;
                    case ExpressionType.NewArrayInit:
                        {
                            var argExprs = ((NewArrayExpression)expression).Expressions;
                            Convert(argExprs[0], sql, parameters, context);
                            for (int i = 1; i < argExprs.Count; i++)
                            {
                                sql.Add(",");
                                Convert(argExprs[i], sql, parameters, context);
                            }
                        }
                        break;
                    case ExpressionType.Convert:
                        Convert(((UnaryExpression)expression).Operand, sql, parameters, context);
                        break;
                    case ExpressionType.Not:
                        sql.Add("NOT ");
                        Convert(((UnaryExpression)expression).Operand, sql, parameters, context);
                        break;
                    case ExpressionType.Equal:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" = ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.NotEqual:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" <> ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.GreaterThan:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" > ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.GreaterThanOrEqual:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" >= ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.LessThan:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" < ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.LessThanOrEqual:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" <= ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.AndAlso:
                        sql.Add("(");
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" AND ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        sql.Add(")");
                        break;
                    case ExpressionType.OrElse:
                        sql.Add("(");
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" OR ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        sql.Add(")");
                        break;
                    case ExpressionType.And:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" & ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.Or:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" | ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.Add:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" + ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.Subtract:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" - ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.Multiply:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" * ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.Divide:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" / ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.Modulo:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" % ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.Coalesce:
                        sql.Add("NVL(");
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(",");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        sql.Add(")");
                        break;
                    case ExpressionType.Conditional:
                        sql.Add("CASE WHEN ");
                        Convert(((ConditionalExpression)expression).Test, sql, parameters, context);
                        sql.Add(" THEN ");
                        Convert(((ConditionalExpression)expression).IfTrue, sql, parameters, context);
                        sql.Add(" ELSE ");
                        Convert(((ConditionalExpression)expression).IfFalse, sql, parameters, context);
                        sql.Add(" END");
                        break;
                    default:
                        throw new NotSupportedException(expression.ToString());
                }
            }
            public static void Navigate(DbEntity entity, List<string> sql, DbEntityContext context)
            {
                foreach ((_, var field) in entity.Table.Fields)
                {
                    sql.Add(entity.Name);
                    sql.Add(".");
                    sql.Add(field);
                    sql.Add(",");
                }
                foreach ((var property, _) in entity.Table.Tables)
                {
                    var propertyEntity = context.Add(entity, property);
                    if (propertyEntity != null)
                        Navigate(propertyEntity, sql, context);
                }
            }
            public static void Navigate(DbTable table, DbEntity entity, List<string> sql, DbEntityContext context)
            {
                foreach ((var property, var field) in entity.Table.Fields)
                {
                    sql.Add(entity.Name);
                    sql.Add(".");
                    sql.Add(field);
                    sql.Add(" AS ");
                    sql.Add(entity.Table.Columns[property]);
                    sql.Add(",");
                }
                foreach ((var property, var propertyTable) in table.Tables)
                {
                    var propertyEntity = context.Add(entity, property);
                    if (propertyEntity != null)
                        Navigate(propertyTable, propertyEntity, sql, context);
                }
            }
            public static void Select(DbTable table, PropertyInfo member, Expression expression, List<string> sql, List<(string, object)> parameters, DbEntityContext context)
            {
                if (table.Columns.TryGetValue(member, out var column))
                {
                    Convert(expression, sql, parameters, context);
                    sql.Add(" AS ");
                    sql.Add(column);
                    sql.Add(",");
                }
                else if (table.Tables.TryGetValue(member, out var memberTable))
                {
                    switch (expression.NodeType)
                    {
                        case ExpressionType.MemberInit:
                            {
                                var expr = (MemberInitExpression)expression;
                                var bindings = expr.Bindings;
                                for (int i = 0; i < bindings.Count; i++)
                                {
                                    var memberAssignment = (MemberAssignment)bindings[i];
                                    if (memberAssignment.Member is PropertyInfo memberMember)
                                        Select(memberTable, memberMember, memberAssignment.Expression, sql, parameters, context);
                                }
                            }
                            break;
                        case ExpressionType.Call:
                            {
                                var expr = (MethodCallExpression)expression;
                                if (SqlDbExtensions.TryNavigate(expr, out var navigate))
                                {
                                    var entity = context.Convert(navigate);
                                    if (entity != null)
                                    {
                                        Navigate(memberTable, entity, sql, context);
                                        break;
                                    }
                                }
                            }
                            break;
                        default:
                            {
                                var entity = context.Convert(expression);
                                if (entity != null)
                                {
                                    foreach ((var property, var field) in entity.Table.Fields)
                                    {
                                        sql.Add(entity.Name);
                                        sql.Add(".");
                                        sql.Add(field);
                                        sql.Add(" AS ");
                                        sql.Add(memberTable.Columns[property]);
                                        sql.Add(",");
                                    }
                                }
                            }
                            break;
                    }

                }
            }
            public static void Select(Expression expression, List<string> sql, List<(string, object)> parameters, DbEntityContext context)
            {
                switch (expression.NodeType)
                {
                    case ExpressionType.Convert:
                        Select(((UnaryExpression)expression).Operand, sql, parameters, context);
                        break;
                    case ExpressionType.MemberInit:
                        {
                            var expr = (MemberInitExpression)expression;
                            var table = DbTable.Get(expr.Type);
                            var bindings = expr.Bindings;
                            for (int i = 0; i < bindings.Count; i++)
                            {
                                var memberAssignment = (MemberAssignment)bindings[i];
                                if (memberAssignment.Member is PropertyInfo member)
                                    Select(table, member, memberAssignment.Expression, sql, parameters, context);
                            }
                            sql.RemoveAt(sql.Count - 1);
                        }
                        break;
                    case ExpressionType.NewArrayInit:
                        {
                            var argExprs = ((NewArrayExpression)expression).Expressions;
                            Convert(argExprs[0], sql, parameters, context);
                            for (int i = 1; i < argExprs.Count; i++)
                            {
                                sql.Add(",");
                                Select(argExprs[i], sql, parameters, context);
                            }
                        }
                        break;
                    case ExpressionType.Call:
                        {
                            var expr = (MethodCallExpression)expression;
                            if (SqlDbExtensions.TryNavigate(expr, out var navigate))
                            {
                                var entity = context.Convert(navigate);
                                if (entity == null)
                                {
                                    Convert(navigate, sql, parameters, context);
                                }
                                else
                                {
                                    Navigate(entity.Table, entity, sql, context);
                                    sql.RemoveAt(sql.Count - 1);
                                }
                            }
                            else
                            {
                                Convert(expression, sql, parameters, context);
                            }
                        }
                        break;
                    default:
                        {
                            var entity = context.Convert(expression);
                            if (entity == null)
                            {
                                Convert(expression, sql, parameters, context);
                            }
                            else
                            {
                                foreach ((var property, var field) in entity.Table.Fields)
                                {
                                    sql.Add(entity.Name);
                                    sql.Add(".");
                                    sql.Add(field);
                                    sql.Add(" AS ");
                                    sql.Add(entity.Table.Columns[property]);
                                    sql.Add(",");
                                }
                                sql.RemoveAt(sql.Count - 1);
                            }
                        }
                        break;
                }
            }
            public static string Select(LambdaExpression select, LambdaExpression where, LambdaExpression groupBy, LambdaExpression having, LambdaExpression orderBy, List<(string, object)> parameters, DbEntityContext context)
            {
                var sql = new List<string>();
                var entity = context.Add(select.Parameters[0]);
                if (where != null)
                {
                    context.Add(where.Parameters[0], entity);
                    sql.Add(" WHERE ");
                    Convert(where.Body, sql, parameters, context);
                }
                if (groupBy != null)
                {
                    context.Add(groupBy.Parameters[0], entity);
                    sql.Add(" GROUP BY ");
                    Convert(groupBy.Body, sql, parameters, context);
                }
                if (having != null)
                {
                    context.Add(having.Parameters[0], entity);
                    sql.Add(" HAVING ");
                    Convert(having.Body, sql, parameters, context);
                }
                if (orderBy != null)
                {
                    context.Add(orderBy.Parameters[0], entity);
                    sql.Add(" ORDER BY ");
                    Convert(orderBy.Body, sql, parameters, context);
                }
                var index = sql.Count;
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);
                //Convert(select.Body, sql, parameters, context);

                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }
                return sql.Concat((index, sql.Count), (0, index));
            }
            public static string Select(int offset, int fetch, LambdaExpression select, LambdaExpression where, LambdaExpression groupBy, LambdaExpression having, LambdaExpression orderBy, List<(string, object)> parameters, DbEntityContext context)
            {
                var sql = new List<string>();
                var entity = context.Add(select.Parameters[0]);
                if (where != null)
                {
                    context.Add(where.Parameters[0], entity);
                    sql.Add(" WHERE ");
                    Convert(where.Body, sql, parameters, context);
                }
                if (groupBy != null)
                {
                    context.Add(groupBy.Parameters[0], entity);
                    sql.Add(" GROUP BY ");
                    Convert(groupBy.Body, sql, parameters, context);
                }
                if (having != null)
                {
                    context.Add(having.Parameters[0], entity);
                    sql.Add(" HAVING ");
                    Convert(having.Body, sql, parameters, context);
                }
                if (orderBy != null)
                {
                    context.Add(orderBy.Parameters[0], entity);
                    sql.Add(" ORDER BY ");
                    Convert(orderBy.Body, sql, parameters, context);
                }
                sql.Add($" OFFSET {offset} ROWS FETCH NEXT {fetch} ROWS ONLY");
                var index = sql.Count;
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);
                //Convert(select.Body, sql, parameters, context);

                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }
                return sql.Concat((index, sql.Count), (0, index));
            }
            public static string Insert(LambdaExpression entity, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var context = new DbEntityContext();
                var table = DbTable.Get(entity.Body.Type);
                sql.Add("INSERT INTO ");
                sql.Add(table.Name);
                sql.Add("(");
                if (entity.Body.NodeType == ExpressionType.MemberInit)
                {
                    var bindings = ((MemberInitExpression)entity.Body).Bindings;
                    Span<int> indexs = bindings.Count < 256 ? stackalloc int[bindings.Count] : new int[bindings.Count];
                    var index = 0;
                    //fields
                    {
                        var i = 0;
                        for (; i < bindings.Count; i++)
                        {
                            var memberAssignment = (MemberAssignment)bindings[i];
                            if (table.Fields.TryGetValue((PropertyInfo)memberAssignment.Member, out var name))
                            {
                                sql.Add(name);
                                indexs[index++] = i++;
                                break;
                            }
                        }
                        for (; i < bindings.Count; i++)
                        {
                            var memberAssignment = (MemberAssignment)bindings[i];
                            if (table.Fields.TryGetValue((PropertyInfo)memberAssignment.Member, out var name))
                            {
                                sql.Add(",");
                                sql.Add(name);
                                indexs[index++] = i;
                            }
                        }
                    }
                    sql.Add(") VALUES (");
                    //values
                    {
                        if (index > 0)
                        {
                            Convert(((MemberAssignment)bindings[indexs[0]]).Expression, sql, parameters, context);
                        }
                        for (int i = 1; i < index; i++)
                        {
                            sql.Add(",");
                            Convert(((MemberAssignment)bindings[indexs[i]]).Expression, sql, parameters, context);
                        }
                    }
                }
                sql.Add(")");
                return sql.Concat();
            }
            public static string Insert(object entity, LambdaExpression properties, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var table = DbTable.Get(properties.Parameters[0].Type);
                var fields = GetFields(table, properties.Body);
                sql.Add("INSERT INTO ");
                sql.Add(table.Name);
                sql.Add("(");
                var enumerator = fields.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    sql.Add(enumerator.Current.Value);
                }
                while (enumerator.MoveNext())
                {
                    sql.Add(",");
                    sql.Add(enumerator.Current.Value);
                }
                sql.Add(") VALUES (");
                enumerator = fields.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    var value = enumerator.Current.Key.GetValue(entity);
                    var parameter = Query.GetParameter(parameters.Count);
                    parameters.Add((parameter, value));
                    sql.Add(parameter);
                }
                while (enumerator.MoveNext())
                {
                    sql.Add(",");
                    var value = enumerator.Current.Key.GetValue(entity);
                    var parameter = Query.GetParameter(parameters.Count);
                    parameters.Add((parameter, value));
                    sql.Add(parameter);
                }
                sql.Add(")");
                return sql.Concat();
            }
            public static string InsertIdentity(LambdaExpression entity, List<(string, object)> parameters, out DbParameter identity)
            {
                var sql = Insert(entity, parameters);
                var parameter = Query.GetParameter(parameters.Count);
                //OracleDbType.Int32=112
                identity = _DbOutputParameter(parameter, 112);
                parameters.Add((null, identity));
                var table = DbTable.Get(entity.Body.Type);
                return sql + " RETURNING " + table.Identity + " INTO " + parameter;
            }
            public static string InsertIdentity(object entity, LambdaExpression properties, List<(string, object)> parameters, out DbParameter identity)
            {
                var sql = Insert(entity, properties, parameters);
                var parameter = Query.GetParameter(parameters.Count);
                //OracleDbType.Int32=112
                identity = _DbOutputParameter(parameter, 112);
                parameters.Add((null, identity));
                var table = DbTable.Get(properties.Parameters[0].Type);
                return sql + " RETURNING " + table.Identity + " INTO " + parameter;
            }
            public static string Delete(DbTable table, object identity, List<(string, object)> parameters)
            {
                var sql = CommandText;
                sql.Add("DELETE FROM ");
                sql.Add(table.Name);
                sql.Add(" WHERE ");
                sql.Add(table.Name);
                sql.Add(".");
                sql.Add(table.Identity);
                if (identity == null)
                {
                    sql.Add(" IS NULL");
                }
                else
                {
                    var parameter = Query.GetParameter(parameters.Count);
                    sql.Add(" = ");
                    sql.Add(parameter);
                    parameters.Add((parameter, identity));
                }
                return sql.Concat();
            }
            public static string Delete(DbTable table, LambdaExpression where, List<(string, object)> parameters)
            {
                var sql = CommandText;
                sql.Add("DELETE FROM ");
                sql.Add(table.Name);
                sql.Add(" WHERE ");
                var context = new DbEntityContext();
                context.Add(where.Parameters[0], new DbEntity()
                {
                    Name = table.Name,
                    Table = table,
                    Entities = new Dictionary<PropertyInfo, DbEntity>()
                });
                Convert(where.Body, sql, parameters, context);
                return sql.Concat();
            }
            public static string Update(LambdaExpression entity, object identity, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var table = DbTable.Get(entity.Body.Type);
                var context = new DbEntityContext();
                context.Add(entity.Parameters[0], new DbEntity()
                {
                    Name = table.Name,
                    Table = table,
                    Entities = new Dictionary<PropertyInfo, DbEntity>()
                });
                sql.Add("UPDATE ");
                sql.Add(table.Name);
                sql.Add(" SET ");
                if (entity.Body.NodeType == ExpressionType.MemberInit)
                {
                    var bindings = ((MemberInitExpression)entity.Body).Bindings;
                    var i = 0;
                    for (; i < bindings.Count;)
                    {
                        var memberAssignment = (MemberAssignment)bindings[i++];
                        if (table.Fields.TryGetValue((PropertyInfo)memberAssignment.Member, out var name))
                        {
                            sql.Add(name);
                            sql.Add("=");
                            Convert(memberAssignment.Expression, sql, parameters, context);
                            break;
                        }
                    }
                    for (; i < bindings.Count;)
                    {
                        var memberAssignment = (MemberAssignment)bindings[i++];
                        if (table.Fields.TryGetValue((PropertyInfo)memberAssignment.Member, out var name))
                        {
                            sql.Add(",");
                            sql.Add(name);
                            sql.Add("=");
                            Convert(memberAssignment.Expression, sql, parameters, context);
                        }
                    }
                }
                sql.Add(" WHERE ");
                sql.Add(table.Name);
                sql.Add(".");
                sql.Add(table.Identity);
                if (identity == null)
                {
                    sql.Add(" IS NULL");
                }
                else
                {
                    var parameter = Query.GetParameter(parameters.Count);
                    sql.Add(" = ");
                    sql.Add(parameter);
                    parameters.Add((parameter, identity));
                }
                return sql.Concat();
            }
            public static string Update(LambdaExpression entity, LambdaExpression where, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var table = DbTable.Get(entity.Body.Type);
                var context = new DbEntityContext();
                var dbEntity = new DbEntity()
                {
                    Name = table.Name,
                    Table = table,
                    Entities = new Dictionary<PropertyInfo, DbEntity>()
                };
                context.Add(entity.Parameters[0], dbEntity);
                sql.Add("UPDATE ");
                sql.Add(table.Name);
                sql.Add(" SET ");
                if (entity.Body.NodeType == ExpressionType.MemberInit)
                {
                    var bindings = ((MemberInitExpression)entity.Body).Bindings;
                    var i = 0;
                    for (; i < bindings.Count;)
                    {
                        var memberAssignment = (MemberAssignment)bindings[i++];
                        if (table.Fields.TryGetValue((PropertyInfo)memberAssignment.Member, out var name))
                        {
                            sql.Add(name);
                            sql.Add("=");
                            Convert(memberAssignment.Expression, sql, parameters, context);
                            break;
                        }
                    }
                    for (; i < bindings.Count;)
                    {
                        var memberAssignment = (MemberAssignment)bindings[i++];
                        if (table.Fields.TryGetValue((PropertyInfo)memberAssignment.Member, out var name))
                        {
                            sql.Add(",");
                            sql.Add(name);
                            sql.Add("=");
                            Convert(memberAssignment.Expression, sql, parameters, context);
                        }
                    }
                }
                sql.Add(" WHERE ");
                context.Add(where.Parameters[0], dbEntity);
                Convert(where.Body, sql, parameters, context);
                return sql.Concat();
            }
            public static string Update(object entity, LambdaExpression properties, object identity, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var table = DbTable.Get(properties.Parameters[0].Type);
                var fields = GetFields(table, properties.Body);
                sql.Add("UPDATE ");
                sql.Add(table.Name);
                sql.Add(" SET ");
                var enumerator = fields.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    var value = enumerator.Current.Key.GetValue(entity);
                    var parameter = Query.GetParameter(parameters.Count);
                    parameters.Add((parameter, value));
                    sql.Add(enumerator.Current.Value);
                    sql.Add("=");
                    sql.Add(parameter);
                }
                while (enumerator.MoveNext())
                {
                    sql.Add(",");
                    var value = enumerator.Current.Key.GetValue(entity);
                    var parameter = Query.GetParameter(parameters.Count);
                    parameters.Add((parameter, value));
                    sql.Add(enumerator.Current.Value);
                    sql.Add("=");
                    sql.Add(parameter);
                }
                sql.Add(" WHERE ");
                sql.Add(table.Name);
                sql.Add(".");
                sql.Add(table.Identity);
                if (identity == null)
                {
                    sql.Add(" IS NULL");
                }
                else
                {
                    var parameter = Query.GetParameter(parameters.Count);
                    sql.Add(" = ");
                    sql.Add(parameter);
                    parameters.Add((parameter, identity));
                }
                return sql.Concat();
            }
            public static string Update(object entity, LambdaExpression properties, LambdaExpression where, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var table = DbTable.Get(properties.Parameters[0].Type);
                var fields = GetFields(table, properties.Body);
                sql.Add("UPDATE ");
                sql.Add(table.Name);
                sql.Add(" SET ");
                var enumerator = fields.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    var value = enumerator.Current.Key.GetValue(entity);
                    var parameter = Query.GetParameter(parameters.Count);
                    parameters.Add((parameter, value));
                    sql.Add(enumerator.Current.Value);
                    sql.Add("=");
                    sql.Add(parameter);
                }
                while (enumerator.MoveNext())
                {
                    sql.Add(",");
                    var value = enumerator.Current.Key.GetValue(entity);
                    var parameter = Query.GetParameter(parameters.Count);
                    parameters.Add((parameter, value));
                    sql.Add(enumerator.Current.Value);
                    sql.Add("=");
                    sql.Add(parameter);
                }
                sql.Add(" WHERE ");
                var context = new DbEntityContext();
                context.Add(where.Parameters[0], new DbEntity()
                {
                    Name = table.Name,
                    Table = table,
                    Entities = new Dictionary<PropertyInfo, DbEntity>()
                });
                Convert(where.Body, sql, parameters, context);
                return sql.Concat();
            }
            public static string SelectSingle(LambdaExpression select, object identity, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var context = new DbEntityContext();
                var entity = context.Add(select.Parameters[0]);
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);

                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }
                sql.Add(" WHERE ");
                sql.Add(entity.Name);
                sql.Add(".");
                sql.Add(entity.Table.Identity);
                if (identity == null)
                {
                    sql.Add(" IS NULL");
                }
                else
                {
                    var parameter = Query.GetParameter(parameters.Count);
                    sql.Add(" = ");
                    sql.Add(parameter);
                    parameters.Add((parameter, identity));
                }
                return sql.Concat();
            }
            public static string SelectSingle(LambdaExpression select, LambdaExpression where, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var context = new DbEntityContext();
                var entity = context.Add(select.Parameters[0]);
                if (where != null)
                {
                    context.Add(where.Parameters[0], entity);
                    sql.Add(" WHERE ");
                    Convert(where.Body, sql, parameters, context);
                }
                var index = sql.Count;
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);

                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }

                return sql.Concat((index, sql.Count), (0, index));
            }
            public static string Select(LambdaExpression select, LambdaExpression where, LambdaExpression orderBy, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var context = new DbEntityContext();
                var entity = context.Add(select.Parameters[0]);
                if (where != null)
                {
                    context.Add(where.Parameters[0], entity);
                    sql.Add(" WHERE ");
                    Convert(where.Body, sql, parameters, context);
                }
                if (orderBy != null)
                {
                    context.Add(orderBy.Parameters[0], entity);
                    sql.Add(" ORDER BY ");
                    Convert(orderBy.Body, sql, parameters, context);
                }
                var index = sql.Count;
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);

                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }
                return sql.Concat((index, sql.Count), (0, index));
            }
            public static string Select(int offset, int fetch, LambdaExpression select, LambdaExpression where, LambdaExpression orderBy, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var context = new DbEntityContext();
                var entity = context.Add(select.Parameters[0]);
                if (where != null)
                {
                    context.Add(where.Parameters[0], entity);
                    sql.Add(" WHERE ");
                    Convert(where.Body, sql, parameters, context);
                }
                if (orderBy != null)
                {
                    context.Add(orderBy.Parameters[0], entity);
                    sql.Add(" ORDER BY ");
                    Convert(orderBy.Body, sql, parameters, context);
                }
                sql.Add($" OFFSET {offset} ROWS FETCH NEXT {fetch} ROWS ONLY");
                var index = sql.Count;
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);

                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }
                return sql.Concat((index, sql.Count), (0, index));
            }
            public static string SelectPaged(int offset, int fetch, LambdaExpression select, LambdaExpression from, LambdaExpression where, LambdaExpression orderBy, List<(string, object)> parameters)
            {
                //OracleDbType.RefCursor = 121
                var cursor1 = Query.GetParameter(parameters.Count);
                parameters.Add((null, _DbOutputParameter(cursor1, 121)));
                var cursor2 = Query.GetParameter(parameters.Count);
                parameters.Add((null, _DbOutputParameter(cursor2, 121)));
                var sql = CommandText;
                var context = new DbEntityContext();
                var entity = context.Add(select.Parameters[0]);
                if (from != null)
                {
                    var fromSql = new List<string>();
                    Convert(from.Body, fromSql, parameters, context);
                    entity.Table.Name = fromSql.Concat();
                }
                if (where != null)
                {
                    context.Add(where.Parameters[0], entity);
                    sql.Add(" WHERE ");
                    Convert(where.Body, sql, parameters, context);
                }
                var whereIndex = sql.Count;
                if (orderBy != null)
                {
                    context.Add(orderBy.Parameters[0], entity);
                    sql.Add(" ORDER BY ");
                    Convert(orderBy.Body, sql, parameters, context);
                }
                sql.Add($" OFFSET {offset} ROWS FETCH NEXT {fetch} ROWS ONLY");
                var selectIndex = sql.Count;
                sql.Add("BEGIN OPEN ");
                sql.Add(cursor1);
                sql.Add(" FOR SELECT ");
                Select(select.Body, sql, parameters, context);
                var fromIndex = sql.Count;
                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }
                var countIndex = sql.Count;
                sql.Add(";OPEN ");
                sql.Add(cursor2);
                sql.Add(" FOR SELECT COUNT(1)");
                var endIndex = sql.Count;
                sql.Add(";END;");
                return sql.Concat((selectIndex, countIndex), (0, selectIndex), (countIndex, endIndex), (fromIndex, countIndex), (0, whereIndex), (endIndex, sql.Count));
            }
            public static string Select(LambdaExpression select, LambdaExpression from, LambdaExpression where, LambdaExpression groupBy, LambdaExpression having, LambdaExpression orderBy, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var context = new DbEntityContext();
                var entity = context.Add(select.Parameters[0]);
                if (from != null)
                {
                    var fromSql = new List<string>();
                    Convert(from.Body, fromSql, parameters, context);
                    entity.Table.Name = fromSql.Concat();
                }
                if (where != null)
                {
                    context.Add(where.Parameters[0], entity);
                    sql.Add(" WHERE ");
                    Convert(where.Body, sql, parameters, context);
                }
                if (groupBy != null)
                {
                    context.Add(groupBy.Parameters[0], entity);
                    sql.Add(" GROUP BY ");
                    Convert(groupBy.Body, sql, parameters, context);
                }
                if (having != null)
                {
                    context.Add(having.Parameters[0], entity);
                    sql.Add(" HAVING ");
                    Convert(having.Body, sql, parameters, context);
                }
                if (orderBy != null)
                {
                    context.Add(orderBy.Parameters[0], entity);
                    sql.Add(" ORDER BY ");
                    Convert(orderBy.Body, sql, parameters, context);
                }
                var index = sql.Count;
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);

                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }
                return sql.Concat((index, sql.Count), (0, index));
            }
            public static string Select(int offset, int fetch, LambdaExpression select, LambdaExpression from, LambdaExpression where, LambdaExpression groupBy, LambdaExpression having, LambdaExpression orderBy, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var context = new DbEntityContext();
                var entity = context.Add(select.Parameters[0]);
                if (from != null)
                {
                    var fromSql = new List<string>();
                    Convert(from.Body, fromSql, parameters, context);
                    entity.Table.Name = fromSql.Concat();
                }
                if (where != null)
                {
                    context.Add(where.Parameters[0], entity);
                    sql.Add(" WHERE ");
                    Convert(where.Body, sql, parameters, context);
                }
                if (groupBy != null)
                {
                    context.Add(groupBy.Parameters[0], entity);
                    sql.Add(" GROUP BY ");
                    Convert(groupBy.Body, sql, parameters, context);
                }
                if (having != null)
                {
                    context.Add(having.Parameters[0], entity);
                    sql.Add(" HAVING ");
                    Convert(having.Body, sql, parameters, context);
                }
                if (orderBy != null)
                {
                    context.Add(orderBy.Parameters[0], entity);
                    sql.Add(" ORDER BY ");
                    Convert(orderBy.Body, sql, parameters, context);
                }
                sql.Add($" OFFSET {offset} ROWS FETCH NEXT {fetch} ROWS ONLY");
                var index = sql.Count;
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);

                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }
                return sql.Concat((index, sql.Count), (0, index));
            }
            public OracleDb(Type connectionType, Func<(DbCommand, IDisposable)> getCommand, Action<DbCommand> cmdExecuting)
            {
                if (_ConnectionType == null)
                {
                    lock (_Sync)
                    {
                        if (_ConnectionType == null)
                        {
                            _ConnectionType = connectionType;
                            _DataReaderType = connectionType.GetMethod("CreateCommand", Type.EmptyTypes).ReturnType.
                                GetMethod("ExecuteReader", Type.EmptyTypes).ReturnType;

                            _AddParameter = typeof(SqlDbExtensions).GetMethod("AddParameter").MakeGenericMethod(connectionType).
                               CreateDelegate<Action<DbCommand, string, object>>();

                            var parameterType = connectionType.GetMethod("CreateCommand", Type.EmptyTypes).ReturnType.
                                GetMethod("CreateParameter", Type.EmptyTypes).ReturnType;
                            var oracleDbType = parameterType.GetProperty("OracleDbType").PropertyType;
                            var parameterCtor = parameterType.GetConstructor(new[] { typeof(string), oracleDbType, typeof(ParameterDirection) });
                            var parameterName = Expression.Parameter(typeof(string), "parameterName");
                            var dbType = Expression.Parameter(typeof(int), "dbType");
                            _DbOutputParameter = Expression.Lambda<Func<string, int, DbParameter>>(
                                Expression.New(parameterCtor, parameterName, Expression.Convert(dbType, oracleDbType),
                                Expression.Constant(ParameterDirection.Output)), parameterName, dbType).Compile();

                            var member = Expression.Parameter(typeof(MemberExpression), "member");
                            var registerMember = typeof(SqlDbExtensions).GetMethod("RegisterDbMember", new[] { typeof(MemberExpression), typeof(IReadOnlyList<object>).MakeByRefType() });
                            var memberObjs = Expression.Variable(typeof(IReadOnlyList<object>), "exprObjs");
                            _DbMember = Expression.Lambda<Func<MemberExpression, IReadOnlyList<object>>>(
                                Expression.Block(new[] { memberObjs },
                                Expression.Call(null, registerMember.MakeGenericMethod(connectionType), member, memberObjs),
                                memberObjs), member).Compile();

                            var method = Expression.Parameter(typeof(MethodCallExpression), "method");
                            var registerMethod = typeof(SqlDbExtensions).GetMethod("RegisterDbMethod", new[] { typeof(MethodCallExpression), typeof(IReadOnlyList<object>).MakeByRefType() });
                            var methodObjs = Expression.Variable(typeof(IReadOnlyList<object>), "exprObjs");
                            _DbMethod = Expression.Lambda<Func<MethodCallExpression, IReadOnlyList<object>>>(
                                Expression.Block(new[] { methodObjs },
                                Expression.Call(null, registerMethod.MakeGenericMethod(connectionType), method, methodObjs),
                                methodObjs), method).Compile();
                        }
                    }
                }
                _getCommand = getCommand;
                _cmdExecuting = cmdExecuting;
            }
            public override int Execute(string sql)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    return cmd.ExecuteNonQuery();
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            public override int Execute(string sql, object parameters)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    Query.AddParameter(cmd, parameters);
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    return cmd.ExecuteNonQuery();
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            public override int Execute(string sql, IList<(string, object)> parameters)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    if (parameters != null && parameters.Count > 0)
                    {
                        for (int i = 0; i < parameters.Count; i++)
                        {
                            (var name, var value) = parameters[i];
                            _AddParameter(cmd, name, value);
                        }
                    }
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    return cmd.ExecuteNonQuery();
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            public override async Task<int> ExecuteAsync(string sql)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    return await cmd.ExecuteNonQueryAsync();
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            public override async Task<int> ExecuteAsync(string sql, object parameters)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    Query.AddParameter(cmd, parameters);
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    return await cmd.ExecuteNonQueryAsync();
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            public override async Task<int> ExecuteAsync(string sql, IList<(string, object)> parameters)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    if (parameters != null && parameters.Count > 0)
                    {
                        for (int i = 0; i < parameters.Count; i++)
                        {
                            (var name, var value) = parameters[i];
                            _AddParameter(cmd, name, value);
                        }
                    }
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    return await cmd.ExecuteNonQueryAsync();
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            public override int ExecuteFormat(FormattableString sqlFormat)
            {
                if (sqlFormat == null)
                    throw new ArgumentNullException(nameof(sqlFormat));

                var parameters = Parameters;
                var args = sqlFormat.GetArguments();
                var names = new string[args.Length];
                for (int i = 0; i < args.Length; i++)
                {
                    var name = Query.GetParameter(parameters.Count);
                    parameters.Add((name, args[i]));
                    names[i] = name;
                }
                return Execute(string.Format(sqlFormat.Format, names), parameters);
            }
            public override Task<int> ExecuteFormatAsync(FormattableString sqlFormat)
            {
                if (sqlFormat == null)
                    throw new ArgumentNullException(nameof(sqlFormat));

                var parameters = Parameters;
                var args = sqlFormat.GetArguments();
                var names = new string[args.Length];
                for (int i = 0; i < args.Length; i++)
                {
                    var name = Query.GetParameter(parameters.Count);
                    parameters.Add((name, args[i]));
                    names[i] = name;
                }
                return ExecuteAsync(string.Format(sqlFormat.Format, names), parameters);
            }
            public override (DbDataReader, IDisposable) ExecuteReader(string sql)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    var reader = cmd.ExecuteReader();
                    return (reader, disposable);
                }
                catch
                {
                    disposable.Dispose();
                    throw;
                }
            }
            public override (DbDataReader, IDisposable) ExecuteReader(string sql, object parameters)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    Query.AddParameter(cmd, parameters);
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    var reader = cmd.ExecuteReader();
                    return (reader, disposable);
                }
                catch
                {
                    disposable.Dispose();
                    throw;
                }
            }
            public override (DbDataReader, IDisposable) ExecuteReader(string sql, IList<(string, object)> parameters)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    if (parameters != null && parameters.Count > 0)
                    {
                        for (int i = 0; i < parameters.Count; i++)
                        {
                            (var name, var value) = parameters[i];
                            _AddParameter(cmd, name, value);
                        }
                    }
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    var reader = cmd.ExecuteReader();
                    return (reader, disposable);
                }
                catch
                {
                    disposable.Dispose();
                    throw;
                }
            }
            public override (DbDataReader, IDisposable) ExecuteReaderFormat(FormattableString sqlFormat)
            {
                if (sqlFormat == null)
                    throw new ArgumentNullException(nameof(sqlFormat));

                var parameters = Parameters;
                var args = sqlFormat.GetArguments();
                var names = new string[args.Length];
                for (int i = 0; i < args.Length; i++)
                {
                    var name = Query.GetParameter(parameters.Count);
                    parameters.Add((name, args[i]));
                    names[i] = name;
                }
                return ExecuteReader(string.Format(sqlFormat.Format, names), parameters);
            }
            public override async Task<(DbDataReader, IDisposable)> ExecuteReaderAsync(string sql)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    var reader = await cmd.ExecuteReaderAsync();
                    return (reader, disposable);
                }
                catch
                {
                    disposable.Dispose();
                    throw;
                }
            }
            public override async Task<(DbDataReader, IDisposable)> ExecuteReaderAsync(string sql, object parameters)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    Query.AddParameter(cmd, parameters);
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    var reader = await cmd.ExecuteReaderAsync();
                    return (reader, disposable);
                }
                catch
                {
                    disposable.Dispose();
                    throw;
                }
            }
            public override async Task<(DbDataReader, IDisposable)> ExecuteReaderAsync(string sql, IList<(string, object)> parameters)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    if (parameters != null && parameters.Count > 0)
                    {
                        for (int i = 0; i < parameters.Count; i++)
                        {
                            (var name, var value) = parameters[i];
                            _AddParameter(cmd, name, value);
                        }
                    }
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    var reader = await cmd.ExecuteReaderAsync();
                    return (reader, disposable);
                }
                catch
                {
                    disposable.Dispose();
                    throw;
                }
            }
            public override Task<(DbDataReader, IDisposable)> ExecuteReaderFormatAsync(FormattableString sqlFormat)
            {
                if (sqlFormat == null)
                    throw new ArgumentNullException(nameof(sqlFormat));

                var parameters = Parameters;
                var args = sqlFormat.GetArguments();
                var names = new string[args.Length];
                for (int i = 0; i < args.Length; i++)
                {
                    var name = Query.GetParameter(parameters.Count);
                    parameters.Add((name, args[i]));
                    names[i] = name;
                }
                return ExecuteReaderAsync(string.Format(sqlFormat.Format, names), parameters);
            }
            public override TEntity Read<TEntity>(DbDataReader dataReader)
            {
                var handler = Reader<TEntity>.Handler;
                if (handler == null)
                {
                    lock (_Sync)
                    {
                        handler = Reader<TEntity>.Handler;
                        if (handler == null)
                        {
                            var register = typeof(SqlDbExtensions).GetMethod("RegisterDbReader", new[] { typeof(Type), typeof(ParameterExpression), typeof(Expression).MakeByRefType(), typeof(Delegate).MakeByRefType() }).MakeGenericMethod(_DataReaderType);
                            var dbReader = Expression.Parameter(typeof(DbDataReader), "dbReader");
                            var reader = Expression.Variable(_DataReaderType, "reader");
                            var args = new object[] { typeof(TEntity), reader, default(Expression), default(Delegate) };
                            register.Invoke(null, args);
                            var read = _DataReaderType.GetMethod("Read", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
                            var expr = Expression.Block(new[] { reader },
                                Expression.Assign(reader, Expression.Convert(dbReader, _DataReaderType)),
                                Expression.Condition(Expression.Call(reader, read), (Expression)args[2], Expression.Default(typeof(TEntity))));
                            handler = Expression.Lambda<Func<DbDataReader, TEntity>>(expr, dbReader).Compile();
                            Reader<TEntity>.Handler = handler;
                        }
                    }
                }
                return handler(dataReader);
            }
            public override int Insert<TEntity>(Expression<Func<SqlExpression, TEntity>> entity)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                var parameters = Parameters;
                var sql = Insert(entity, parameters);
                return Execute(sql, parameters);
            }
            public override int Insert<TEntity>(TEntity entity, Expression<Func<TEntity, object>> properties)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));

                var parameters = Parameters;
                var sql = Insert(entity, properties, parameters);
                return Execute(sql, parameters);
            }
            public override int InsertRange<TEntity>(IList<TEntity> entities, Expression<Func<TEntity, object>> properties)
            {
                if (entities == null)
                    throw new ArgumentNullException(nameof(entities));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));

                var result = 0;
                var sql = new List<string>();
                var parameters = new List<(string, object)>();
                var table = DbTable.Get(properties.Parameters[0].Type);
                var fields = GetFields(table, properties.Body);
                for (int index = 0; ;)
                {
                    if (index == entities.Count)
                        break;
                    var entity = entities[index++];
                    var count = 1;
                    var fieldCount = fields.Count;
                    sql.Clear();
                    parameters.Clear();
                    sql.Add("INSERT INTO ");
                    sql.Add(table.Name);
                    sql.Add("(");
                    var enumerator = fields.GetEnumerator();
                    if (enumerator.MoveNext())
                    {
                        sql.Add(enumerator.Current.Value);
                    }
                    while (enumerator.MoveNext())
                    {
                        sql.Add(",");
                        sql.Add(enumerator.Current.Value);
                    }
                    sql.Add(") SELECT * FROM (SELECT ");
                    enumerator = fields.GetEnumerator();
                    if (enumerator.MoveNext())
                    {
                        var value = enumerator.Current.Key.GetValue(entity);
                        var parameter = Query.GetParameter(parameters.Count);
                        parameters.Add((parameter, value));
                        sql.Add(parameter);
                    }
                    while (enumerator.MoveNext())
                    {
                        sql.Add(",");
                        var value = enumerator.Current.Key.GetValue(entity);
                        var parameter = Query.GetParameter(parameters.Count);
                        parameters.Add((parameter, value));
                        sql.Add(parameter);
                    }
                    sql.Add(" FROM DUAL");
                    while (index < entities.Count)
                    {
                        if (count == 500 || fieldCount + fields.Count >= 999)
                            break;
                        count += 1;
                        fieldCount += fields.Count;
                        entity = entities[index++];
                        sql.Add(" UNION ALL SELECT ");
                        enumerator = fields.GetEnumerator();
                        if (enumerator.MoveNext())
                        {
                            var value = enumerator.Current.Key.GetValue(entity);
                            var parameter = Query.GetParameter(parameters.Count);
                            parameters.Add((parameter, value));
                            sql.Add(parameter);
                        }
                        while (enumerator.MoveNext())
                        {
                            sql.Add(",");
                            var value = enumerator.Current.Key.GetValue(entity);
                            var parameter = Query.GetParameter(parameters.Count);
                            parameters.Add((parameter, value));
                            sql.Add(parameter);
                        }
                        sql.Add(" FROM DUAL");
                    }
                    sql.Add(")");
                    result += Execute(sql.Concat(), parameters);
                }
                return result;
            }
            public override Task<int> InsertAsync<TEntity>(Expression<Func<SqlExpression, TEntity>> entity)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                var parameters = Parameters;
                var sql = Insert(entity, parameters);
                return ExecuteAsync(sql, parameters);
            }
            public override Task<int> InsertAsync<TEntity>(TEntity entity, Expression<Func<TEntity, object>> properties)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));

                var parameters = Parameters;
                var sql = Insert(entity, properties, parameters);
                return ExecuteAsync(sql, parameters);
            }
            public override async Task<int> InsertRangeAsync<TEntity>(IList<TEntity> entities, Expression<Func<TEntity, object>> properties)
            {
                if (entities == null)
                    throw new ArgumentNullException(nameof(entities));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));

                var result = 0;
                var sql = new List<string>();
                var parameters = new List<(string, object)>();
                var table = DbTable.Get(properties.Parameters[0].Type);
                var fields = GetFields(table, properties.Body);
                for (int index = 0; ;)
                {
                    if (index == entities.Count)
                        break;
                    var entity = entities[index++];
                    var count = 1;
                    var fieldCount = fields.Count;
                    sql.Clear();
                    parameters.Clear();
                    sql.Add("INSERT INTO ");
                    sql.Add(table.Name);
                    sql.Add("(");
                    var enumerator = fields.GetEnumerator();
                    if (enumerator.MoveNext())
                    {
                        sql.Add(enumerator.Current.Value);
                    }
                    while (enumerator.MoveNext())
                    {
                        sql.Add(",");
                        sql.Add(enumerator.Current.Value);
                    }
                    sql.Add(") SELECT * FROM (SELECT ");
                    enumerator = fields.GetEnumerator();
                    if (enumerator.MoveNext())
                    {
                        var value = enumerator.Current.Key.GetValue(entity);
                        var parameter = Query.GetParameter(parameters.Count);
                        parameters.Add((parameter, value));
                        sql.Add(parameter);
                    }
                    while (enumerator.MoveNext())
                    {
                        sql.Add(",");
                        var value = enumerator.Current.Key.GetValue(entity);
                        var parameter = Query.GetParameter(parameters.Count);
                        parameters.Add((parameter, value));
                        sql.Add(parameter);
                    }
                    sql.Add(" FROM DUAL");
                    while (index < entities.Count)
                    {
                        if (count == 500 || fieldCount + fields.Count >= 999)
                            break;
                        count += 1;
                        fieldCount += fields.Count;
                        entity = entities[index++];
                        sql.Add(" UNION ALL SELECT ");
                        enumerator = fields.GetEnumerator();
                        if (enumerator.MoveNext())
                        {
                            var value = enumerator.Current.Key.GetValue(entity);
                            var parameter = Query.GetParameter(parameters.Count);
                            parameters.Add((parameter, value));
                            sql.Add(parameter);
                        }
                        while (enumerator.MoveNext())
                        {
                            sql.Add(",");
                            var value = enumerator.Current.Key.GetValue(entity);
                            var parameter = Query.GetParameter(parameters.Count);
                            parameters.Add((parameter, value));
                            sql.Add(parameter);
                        }
                        sql.Add(" FROM DUAL");
                    }
                    sql.Add(")");
                    result += await ExecuteAsync(sql.Concat(), parameters);
                }
                return result;
            }
            public override TIdentity InsertIdentity<TEntity, TIdentity>(Expression<Func<SqlExpression, TEntity>> entity)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                var parameters = Parameters;
                var sql = InsertIdentity(entity, parameters, out var identity);
                Execute(sql, parameters);
                return Converter.Convert<object, TIdentity>(identity.Value);
            }
            public override TIdentity InsertIdentity<TEntity, TIdentity>(TEntity entity, Expression<Func<TEntity, object>> properties)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));

                var parameters = Parameters;
                var sql = InsertIdentity(entity, properties, parameters, out var identity);
                Execute(sql, parameters);
                return Converter.Convert<object, TIdentity>(identity.Value);
            }
            public override async Task<TIdentity> InsertIdentityAsync<TEntity, TIdentity>(Expression<Func<SqlExpression, TEntity>> entity)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                var parameters = Parameters;
                var sql = InsertIdentity(entity, parameters, out var identity);
                await ExecuteAsync(sql, parameters);
                return Converter.Convert<object, TIdentity>(identity.Value);
            }
            public override async Task<TIdentity> InsertIdentityAsync<TEntity, TIdentity>(TEntity entity, Expression<Func<TEntity, object>> properties)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));

                var parameters = Parameters;
                var sql = InsertIdentity(entity, properties, parameters, out var identity);
                await ExecuteAsync(sql, parameters);
                return Converter.Convert<object, TIdentity>(identity.Value);
            }
            public override int Delete<TEntity>(object identity)
            {
                var parameters = Parameters;
                var sql = Delete(DbTable.Get(typeof(TEntity)), identity, parameters);
                return Execute(sql, parameters);
            }
            public override int Delete<TEntity>(Expression<Func<TEntity, SqlExpression, bool>> where)
            {
                if (where == null)
                    throw new ArgumentNullException(nameof(where));

                var parameters = Parameters;
                var sql = Delete(DbTable.Get(typeof(TEntity)), where, parameters);
                return Execute(sql, parameters);
            }
            public override Task<int> DeleteAsync<TEntity>(object identity)
            {
                var parameters = Parameters;
                var sql = Delete(DbTable.Get(typeof(TEntity)), identity, parameters);
                return ExecuteAsync(sql, parameters);
            }
            public override Task<int> DeleteAsync<TEntity>(Expression<Func<TEntity, SqlExpression, bool>> where)
            {
                if (where == null)
                    throw new ArgumentNullException(nameof(where));

                var parameters = Parameters;
                var sql = Delete(DbTable.Get(typeof(TEntity)), where, parameters);
                return ExecuteAsync(sql, parameters);
            }
            public override int Update<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> entity, object identity)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                var parameters = Parameters;
                var sql = Update(entity, identity, parameters);
                return Execute(sql, parameters);
            }
            public override int Update<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> entity, Expression<Func<TEntity, SqlExpression, bool>> where)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (where == null)
                    throw new ArgumentNullException(nameof(where));

                var parameters = Parameters;
                var sql = Update(entity, where, parameters);
                return Execute(sql, parameters);
            }
            public override int Update<TEntity>(TEntity entity, Expression<Func<TEntity, object>> properties)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));

                SqlDbExtensions.RegisterIdentity<TEntity>(out var identity);
                if (identity == null)
                    throw new ArgumentNullException(nameof(identity));

                var parameters = Parameters;
                var sql = Update(entity, properties, identity.GetValue(entity), parameters);
                return Execute(sql, parameters);
            }
            public override int Update<TEntity>(TEntity entity, Expression<Func<TEntity, object>> properties, Expression<Func<TEntity, SqlExpression, bool>> where)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));
                if (where == null)
                    throw new ArgumentNullException(nameof(where));

                var parameters = Parameters;
                var sql = Update(entity, properties, where, parameters);
                return Execute(sql, parameters);
            }
            public override Task<int> UpdateAsync<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> entity, object identity)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                var parameters = Parameters;
                var sql = Update(entity, identity, parameters);
                return ExecuteAsync(sql, parameters);
            }
            public override Task<int> UpdateAsync<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> entity, Expression<Func<TEntity, SqlExpression, bool>> where)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (where == null)
                    throw new ArgumentNullException(nameof(where));

                var parameters = Parameters;
                var sql = Update(entity, where, parameters);
                return ExecuteAsync(sql, parameters);
            }
            public override Task<int> UpdateAsync<TEntity>(TEntity entity, Expression<Func<TEntity, object>> properties)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));

                SqlDbExtensions.RegisterIdentity<TEntity>(out var identity);
                if (identity == null)
                    throw new ArgumentNullException(nameof(identity));

                var parameters = Parameters;
                var sql = Update(entity, properties, identity.GetValue(entity), parameters);
                return ExecuteAsync(sql, parameters);
            }
            public override Task<int> UpdateAsync<TEntity>(TEntity entity, Expression<Func<TEntity, object>> properties, Expression<Func<TEntity, SqlExpression, bool>> where)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));
                if (where == null)
                    throw new ArgumentNullException(nameof(where));

                var parameters = Parameters;
                var sql = Update(entity, properties, where, parameters);
                return ExecuteAsync(sql, parameters);
            }
            public override TEntity SelectSingle<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> select, object identity)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = SelectSingle(select, identity, parameters);
                return this.Execute<TEntity>(sql, parameters);
            }
            public override Task<TEntity> SelectSingleAsync<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> select, object identity)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = SelectSingle(select, identity, parameters);
                return this.ExecuteAsync<TEntity>(sql, parameters);
            }
            public override TEntity SelectSingle<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = SelectSingle(select, where, parameters);
                return this.Execute<TEntity>(sql, parameters);
            }
            public override Task<TEntity> SelectSingleAsync<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = SelectSingle(select, where, parameters);
                return this.ExecuteAsync<TEntity>(sql, parameters);
            }
            public override List<TEntity> Select<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = Select(select, where, orderBy, parameters);
                return this.Execute<List<TEntity>>(sql, parameters);
            }
            public override Task<List<TEntity>> SelectAsync<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = Select(select, where, orderBy, parameters);
                return this.ExecuteAsync<List<TEntity>>(sql, parameters);
            }
            public override List<TEntity> Select<TEntity>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = Select(offset, fetch, select, where, orderBy, parameters);
                return this.Execute<List<TEntity>>(sql, parameters);
            }
            public override Task<List<TEntity>> SelectAsync<TEntity>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = Select(offset, fetch, select, where, orderBy, parameters);
                return this.ExecuteAsync<List<TEntity>>(sql, parameters);
            }
            public override (List<TEntity>, int) SelectPaged<TEntity>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = SelectPaged(offset, fetch, select, null, where, orderBy, parameters);
                return this.Execute<List<TEntity>, int>(sql, parameters);
            }
            public override Task<(List<TEntity>, int)> SelectPagedAsync<TEntity>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = SelectPaged(offset, fetch, select, null, where, orderBy, parameters);
                return this.ExecuteAsync<List<TEntity>, int>(sql, parameters);
            }
            public override TResult Select<TEntity, TResult>(Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<SqlExpression, object>> from, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Func<TEntity, SqlExpression, object>> groupBy, Expression<Func<TEntity, SqlExpression, bool>> having, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = Select(select, from, where, groupBy, having, orderBy, parameters);
                return this.Execute<TResult>(sql, parameters);
            }
            public override Task<TResult> SelectAsync<TEntity, TResult>(Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<SqlExpression, object>> from, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Func<TEntity, SqlExpression, object>> groupBy, Expression<Func<TEntity, SqlExpression, bool>> having, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = Select(select, from, where, groupBy, having, orderBy, parameters);
                return this.ExecuteAsync<TResult>(sql, parameters);
            }
            public override TResult Select<TEntity, TResult>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<SqlExpression, object>> from, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Func<TEntity, SqlExpression, object>> groupBy, Expression<Func<TEntity, SqlExpression, bool>> having, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = Select(offset, fetch, select, from, where, groupBy, having, orderBy, parameters);
                return this.Execute<TResult>(sql, parameters);
            }
            public override Task<TResult> SelectAsync<TEntity, TResult>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<SqlExpression, object>> from, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Func<TEntity, SqlExpression, object>> groupBy, Expression<Func<TEntity, SqlExpression, bool>> having, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = Select(offset, fetch, select, from, where, groupBy, having, orderBy, parameters);
                return this.ExecuteAsync<TResult>(sql, parameters);
            }
            public override (TResult, int) SelectPaged<TEntity, TResult>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<SqlExpression, object>> from, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = SelectPaged(offset, fetch, select, from, where, orderBy, parameters);
                return this.Execute<TResult, int>(sql, parameters);
            }
            public override Task<(TResult, int)> SelectPagedAsync<TEntity, TResult>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<SqlExpression, object>> from, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = SelectPaged(offset, fetch, select, from, where, orderBy, parameters);
                return this.ExecuteAsync<TResult, int>(sql, parameters);
            }
        }
        #endregion

        #region PostgreSqlDb
        public class PostgreSqlDb : SqlDb
        {
            private static object _Sync = new object();
            private static Type _ConnectionType;
            private static Type _DataReaderType;
            private static Action<DbCommand, string, object> _AddParameter;
            private static Func<MemberExpression, IReadOnlyList<object>> _DbMember;
            private static Func<MethodCallExpression, IReadOnlyList<object>> _DbMethod;
            [ThreadStatic] private static List<string> _CommandText;
            [ThreadStatic] private static List<(string, object)> _Parameters;
            private Func<(DbCommand, IDisposable)> _getCommand;
            private Action<DbCommand> _cmdExecuting;
            public static List<string> CommandText
            {
                get
                {
                    var commandText = _CommandText;
                    if (commandText == null)
                    {
                        commandText = new List<string>();
                        _CommandText = commandText;
                    }
                    else
                    {
                        commandText.Clear();
                    }
                    return commandText;
                }
            }
            public static List<(string, object)> Parameters
            {
                get
                {
                    var parameters = _Parameters;
                    if (parameters == null)
                    {
                        parameters = new List<(string, object)>();
                        _Parameters = parameters;
                    }
                    else
                    {
                        parameters.Clear();
                    }
                    return parameters;
                }
            }
            private static class Query
            {
                private static object _Sync;
                private static string[] _Parameters;
                private static string[] _Tables;
                private static Dictionary<Type, Action<DbCommand, object>> _Handlers;
                static Query()
                {
                    _Sync = new object();
                    _Parameters = new[]
                    {
                        "@p0","@p1","@p2","@p3","@p4","@p5","@p6","@p7",
                        "@p8","@p9","@p10","@p11","@p12","@p13","@p14","@p15",
                        "@p16","@p17","@p18","@p19","@p20","@p21","@p22","@p23",
                        "@p24","@p25","@p26","@p27","@p28","@p29","@p30","@p31",
                        "@p32","@p33","@p34","@p35","@p36","@p37","@p38","@p39",
                        "@p40","@p41","@p42","@p43","@p44","@p45","@p46","@p47",
                        "@p48","@p49","@p50","@p51","@p52","@p53","@p54","@p55",
                        "@p56","@p57","@p58","@p59","@p60","@p61","@p62","@p63"
                    };
                    _Tables = new[]
                    {
                        "t0","t1","t2","t3","t4","t5","t6","t7",
                        "t8","t9","t10","t11","t12","t13","t14","t15"
                    };
                    _Handlers = new Dictionary<Type, Action<DbCommand, object>>();
                }
                public static string GetParameter(int index)
                {
                    return index < _Parameters.Length ? _Parameters[index] : $"@p{index}";
                }
                public static string GetTable(int index)
                {
                    return index < _Tables.Length ? _Tables[index] : $"t{index}";
                }
                public static void AddParameter(DbCommand command, object objParameters)
                {
                    if (objParameters == null)
                        return;

                    var type = objParameters.GetType();
                    if (!_Handlers.TryGetValue(type, out var handler))
                    {
                        lock (_Sync)
                        {
                            if (!_Handlers.TryGetValue(type, out handler))
                            {
                                var cmd = Expression.Parameter(typeof(DbCommand), "cmd");
                                var objValue = Expression.Parameter(typeof(object), "objValue");
                                var tValue = Expression.Variable(type, "value");
                                var addParameter = typeof(SqlDbExtensions).GetMethod("AddParameter").MakeGenericMethod(_ConnectionType);
                                var properties = type.GetGeneralProperties(BindingFlags.Instance | BindingFlags.Public);
                                var exprs = new List<Expression>();
                                exprs.Add(Expression.Assign(tValue, Expression.Convert(objValue, type)));
                                foreach (var property in properties)
                                {
                                    if (!property.CanRead)
                                        continue;
                                    exprs.Add(Expression.Call(
                                        addParameter, cmd,
                                        Expression.Constant($"@{property.Name}"),
                                        Expression.Convert(Expression.Property(tValue, property), typeof(object))
                                        ));
                                }
                                handler = Expression.Lambda<Action<DbCommand, object>>(Expression.Block(new[] { tValue }, exprs), cmd, objValue).Compile();
                                var handlers = new Dictionary<Type, Action<DbCommand, object>>(_Handlers);
                                handlers.Add(type, handler);
                                _Handlers = handlers;
                            }
                        }
                    }
                    handler(command, objParameters);
                }
            }
            private static class Reader<TEntity>
            {
                public static Func<DbDataReader, TEntity> Handler;
            }
            public class DbTable
            {
                private class Comparer : IEqualityComparer<PropertyInfo>
                {
                    public bool Equals(PropertyInfo x, PropertyInfo y) =>
                        x.MetadataToken == y.MetadataToken && x.DeclaringType == y.DeclaringType;
                    public int GetHashCode(PropertyInfo obj) => obj.MetadataToken;
                }

                public string Name;
                public string Identity;
                public string Join;
                public Dictionary<PropertyInfo, string> Fields;
                public Dictionary<PropertyInfo, string> Columns;
                public Dictionary<PropertyInfo, DbTable> Tables;

                private static int _MaxDepth = 8;//TODO??_TypeReference
                private static Comparer _Comparer = new Comparer();
                private static object _Sync = new object();
                private static Dictionary<Type, DbTable> _Tables = new Dictionary<Type, DbTable>();
                public static DbTable Get(Type type)
                {
                    if (!_Tables.TryGetValue(type, out var table))
                    {
                        lock (_Sync)
                        {
                            if (!_Tables.TryGetValue(type, out table))
                            {
                                table = Get(0, null, null, type);
                            }
                        }
                    }
                    return table;
                }
                private static DbTable Get(int depth, string prefix, string join, Type type)
                {
                    var table = new DbTable();
                    table.Join = join;
                    table.Fields = new Dictionary<PropertyInfo, string>(_Comparer);
                    table.Columns = new Dictionary<PropertyInfo, string>(_Comparer);
                    table.Tables = new Dictionary<PropertyInfo, DbTable>(_Comparer);
                    var registerReader = typeof(SqlDbExtensions).GetMethod("RegisterDbReader", new[] { typeof(Type), typeof(ParameterExpression), typeof(ParameterExpression), typeof(Expression).MakeByRefType() })
                    .MakeGenericMethod(_DataReaderType);
                    var readerArgs = new object[] { null, Expression.Parameter(_DataReaderType), Expression.Parameter(typeof(int)), null };
                    SqlDbExtensions.RegisterTable(out var tableResolver);
                    SqlDbExtensions.RegisterIdentity(out var identityResolver);
                    SqlDbExtensions.RegisterProperty(out var propertyResolver);

                    var dataTableAttribute = type.GetCustomAttribute<DataTableAttribute>();
                    table.Name = $"\"{dataTableAttribute?.Name ?? tableResolver(type)}\"";
                    var identityProperty = identityResolver(type);
                    if (identityProperty != null)
                    {
                        var dataColumnAttribute = identityProperty.GetCustomAttribute<DataColumnAttribute>();
                        table.Identity = $"\"{dataColumnAttribute?.Name ?? propertyResolver(identityProperty)}\"";
                    }
                    var properties = type.GetGeneralProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    foreach (var property in properties)
                    {
                        if (!property.CanWrite)
                            continue;
                        if (property.IsDefined(typeof(IgnoreDataColumnAttribute)))
                            continue;
                        var dataColumnAttribute = property.GetCustomAttribute<DataColumnAttribute>();
                        readerArgs[0] = property.PropertyType;
                        registerReader.Invoke(null, readerArgs);
                        if (readerArgs[3] != null)
                        {
                            string columnName = dataColumnAttribute?.Name ?? propertyResolver(property);
                            table.Fields.Add(property, $"\"{columnName}\"");
                            table.Columns.Add(property, $"\"{prefix}{columnName}\"");
                        }
                        else
                        {
                            if (depth > _MaxDepth)
                                continue;
                            var joinIdentityProperty = identityResolver(property.PropertyType);
                            if (joinIdentityProperty == null)
                                continue;
                            var joinName = dataColumnAttribute?.Name;
                            if (joinName == null)
                            {
                                var onPropery = type.GetProperty($"{property.Name}{joinIdentityProperty.Name}", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                if (onPropery == null)
                                {
                                    if (joinIdentityProperty.Name == $"{type.Name}{identityProperty.Name}")
                                    {
                                        onPropery = identityProperty;
                                    }
                                    else
                                    {
                                        continue;
                                    }
                                }
                                dataColumnAttribute = onPropery.GetCustomAttribute<DataColumnAttribute>();
                                joinName = dataColumnAttribute?.Name ?? propertyResolver(onPropery);
                            }
                            joinName = $"\"{joinName}\"";
                            string columnName = propertyResolver(property);
                            var propertyPrefix = $"{prefix}{columnName}.";
                            var propertyTable = Get(depth + 1, propertyPrefix, joinName, property.PropertyType);
                            table.Tables.Add(property, propertyTable);
                        }
                    }
                    return table;
                }
            }
            public class DbEntity
            {
                public string Name;
                public DbTable Table;
                public Dictionary<PropertyInfo, DbEntity> Entities;
            }
            public class DbEntityContext
            {
                private int _table;
                private Dictionary<ParameterExpression, DbEntity> _entities;
                public DbEntityContext()
                {
                    _entities = new Dictionary<ParameterExpression, DbEntity>();
                }
                public DbEntity Add(ParameterExpression parameter)
                {
                    var entity = new DbEntity()
                    {
                        Name = Query.GetTable(_table++),
                        Table = DbTable.Get(parameter.Type),
                        Entities = new Dictionary<PropertyInfo, DbEntity>()
                    };
                    _entities.Add(parameter, entity);
                    return entity;
                }
                public void Add(ParameterExpression parameter, DbEntity entity)
                {
                    _entities.Add(parameter, entity);
                }
                public DbEntity Add(DbEntity entity, PropertyInfo property)
                {
                    if (entity.Entities.TryGetValue(property, out var propertyEntity))
                    {
                        return propertyEntity;
                    }
                    else if (entity.Table.Tables.TryGetValue(property, out var entityTable))
                    {
                        propertyEntity = new DbEntity();
                        propertyEntity.Name = Query.GetTable(_table++);
                        propertyEntity.Table = entityTable;
                        propertyEntity.Entities = new Dictionary<PropertyInfo, DbEntity>();
                        entity.Entities.Add(property, propertyEntity);
                        return propertyEntity;
                    }
                    return null;
                }
                public DbEntity Convert(Expression expression)
                {
                    if (expression == null)
                        return null;

                    switch (expression.NodeType)
                    {
                        case ExpressionType.Convert:
                            return Convert(((UnaryExpression)expression).Operand);
                        case ExpressionType.Parameter:
                            {
                                _entities.TryGetValue((ParameterExpression)expression, out var entity);
                                return entity;
                            }
                        case ExpressionType.MemberAccess:
                            {
                                var expr = (MemberExpression)expression;
                                if (expr.Member is PropertyInfo property)
                                {
                                    var propertyEntity = Convert(expr.Expression);
                                    if (propertyEntity != null)
                                    {
                                        if (propertyEntity.Entities.TryGetValue(property, out var entity))
                                        {
                                            return entity;
                                        }
                                        else if (propertyEntity.Table.Tables.TryGetValue(property, out var propertyTable))
                                        {
                                            entity = new DbEntity();
                                            entity.Name = Query.GetTable(_table++);
                                            entity.Table = propertyTable;
                                            entity.Entities = new Dictionary<PropertyInfo, DbEntity>();
                                            propertyEntity.Entities.Add(property, entity);
                                            return entity;
                                        }
                                    }
                                }
                            }
                            return null;
                        default:
                            return null;
                    }
                }
            }
            public static Dictionary<PropertyInfo, string> GetFields(DbTable table, Expression expression)
            {
                switch (expression.NodeType)
                {
                    case ExpressionType.Parameter:
                        return table.Fields;
                    case ExpressionType.Convert:
                        expression = ((UnaryExpression)expression).Operand;
                        if (expression.NodeType == ExpressionType.MemberAccess)
                            goto case ExpressionType.MemberAccess;
                        break;
                    case ExpressionType.MemberAccess:
                        {
                            var expr = (MemberExpression)expression;
                            if (expr.Member is PropertyInfo property
                                && table.Fields.TryGetValue(property, out var field))
                            {
                                return new Dictionary<PropertyInfo, string>()
                                {
                                    { property,field}
                                };
                            }
                        }
                        break;
                    case ExpressionType.New:
                        {
                            var fields = new Dictionary<PropertyInfo, string>();
                            var exprArgs = ((NewExpression)expression).Arguments;
                            for (int i = 0; i < exprArgs.Count; i++)
                            {
                                var expr = exprArgs[i].NodeType == ExpressionType.Convert ?
                                   ((UnaryExpression)exprArgs[i]).Operand as MemberExpression
                                   : exprArgs[i] as MemberExpression;
                                if (expr != null && expr.Member is PropertyInfo property
                                    && table.Fields.TryGetValue(property, out var field))
                                {
                                    fields.Add(property, field);
                                }
                            }
                            return fields;
                        }
                    case ExpressionType.Call:
                        {
                            if (SqlDbExtensions.TryExcept((MethodCallExpression)expression, out var except))
                            {
                                switch (except.NodeType)
                                {
                                    case ExpressionType.Convert:
                                        except = ((UnaryExpression)except).Operand;
                                        if (except.NodeType == ExpressionType.MemberAccess)
                                            goto case ExpressionType.MemberAccess;
                                        break;
                                    case ExpressionType.MemberAccess:
                                        {
                                            var expr = (MemberExpression)except;
                                            if (expr.Member is PropertyInfo property && table.Fields.ContainsKey(property))
                                            {
                                                var fields = new Dictionary<PropertyInfo, string>(table.Fields);
                                                fields.Remove(property);
                                                return fields;
                                            }
                                        }
                                        break;
                                    case ExpressionType.New:
                                        {
                                            var fields = new Dictionary<PropertyInfo, string>(table.Fields);
                                            var exprArgs = ((NewExpression)except).Arguments;
                                            for (int i = 0; i < exprArgs.Count; i++)
                                            {
                                                var expr = exprArgs[i].NodeType == ExpressionType.Convert ?
                                                   ((UnaryExpression)exprArgs[i]).Operand as MemberExpression
                                                   : exprArgs[i] as MemberExpression;
                                                if (expr != null && expr.Member is PropertyInfo property && fields.ContainsKey(property))
                                                {
                                                    fields.Remove(property);
                                                }
                                            }
                                            return fields;
                                        }
                                    default:
                                        break;
                                }
                                return new Dictionary<PropertyInfo, string>(table.Fields);
                            }
                        }
                        break;
                    default:
                        break;
                }
                return new Dictionary<PropertyInfo, string>();
            }
            public static void Join(DbEntity entity, DbEntity joinEntity, List<string> sql)
            {
                sql.Add(" LEFT JOIN ");
                sql.Add(joinEntity.Table.Name);
                sql.Add(" ");
                sql.Add(joinEntity.Name);
                sql.Add(" ON ");
                sql.Add(joinEntity.Name);
                sql.Add(".");
                sql.Add(joinEntity.Table.Identity);
                sql.Add("=");
                sql.Add(entity.Name);
                sql.Add(".");
                sql.Add(joinEntity.Table.Join);
                foreach (var item in joinEntity.Entities)
                {
                    Join(joinEntity, item.Value, sql);
                }
            }
            public static void Convert(Expression expression, List<string> sql, List<(string, object)> parameters, DbEntityContext context)
            {
                switch (expression.NodeType)
                {
                    case ExpressionType.Parameter:
                        {
                            var entity = context.Convert(expression);
                            if (entity != null)
                            {
                                foreach ((_, var field) in entity.Table.Fields)
                                {
                                    sql.Add(entity.Name);
                                    sql.Add(".");
                                    sql.Add(field);
                                    sql.Add(",");
                                }
                                sql.RemoveAt(sql.Count - 1);
                            }
                        }
                        break;
                    case ExpressionType.Constant:
                        {
                            var value = ((ConstantExpression)expression).Value;
                            var parameter = Query.GetParameter(parameters.Count);
                            sql.Add(parameter);
                            parameters.Add((parameter, value));
                        }
                        break;
                    case ExpressionType.MemberAccess:
                        {
                            var expr = (MemberExpression)expression;
                            var exprObjs = _DbMember(expr);
                            if (exprObjs != null)
                            {
                                for (int i = 0; i < exprObjs.Count; i++)
                                {
                                    var exprObj = exprObjs[i] as Expression;
                                    if (exprObj != null)
                                        Convert(exprObj, sql, parameters, context);
                                    else
                                        sql.Add((string)exprObjs[i]);
                                }
                                break;
                            }
                            if (expr.Member is PropertyInfo property)
                            {
                                var entity = context.Convert(expr.Expression);
                                if (entity != null)
                                {
                                    if (entity.Table.Fields.TryGetValue(property, out var field))
                                    {
                                        sql.Add(entity.Name);
                                        sql.Add(".");
                                        sql.Add(field);
                                    }
                                    else
                                    {
                                        var propertyEntity = context.Add(entity, property);
                                        if (propertyEntity != null)
                                        {
                                            foreach ((_, var propertyField) in propertyEntity.Table.Fields)
                                            {
                                                sql.Add(propertyEntity.Name);
                                                sql.Add(".");
                                                sql.Add(propertyField);
                                                sql.Add(",");
                                            }
                                            sql.RemoveAt(sql.Count - 1);
                                        }
                                    }
                                    break;
                                }
                            }
                            var value = expr.Invoke();
                            var parameter = Query.GetParameter(parameters.Count);
                            sql.Add(parameter);
                            parameters.Add((parameter, value));
                        }
                        break;
                    case ExpressionType.Call:
                        {
                            var expr = (MethodCallExpression)expression;
                            var exprObjs = _DbMethod(expr);
                            if (exprObjs != null)
                            {
                                for (int i = 0; i < exprObjs.Count; i++)
                                {
                                    var exprObj = exprObjs[i] as Expression;
                                    if (exprObj != null)
                                        Convert(exprObj, sql, parameters, context);
                                    else
                                        sql.Add((string)exprObjs[i]);
                                }
                            }
                            else if (SqlDbExtensions.TrySelect(expr, out var objSelect))
                            {
                                Select(objSelect, sql, parameters, context);
                            }
                            else if (SqlDbExtensions.TrySelect(expr, out var select, out var where, out var groupBy, out var having, out var orderBy))
                            {
                                sql.Add("(");
                                sql.Add(Select(select, where, groupBy, having, orderBy, parameters, context));
                                sql.Add(")");
                            }
                            else if (SqlDbExtensions.TrySelect(expr, out var offset, out var fetch, out select, out where, out groupBy, out having, out orderBy))
                            {
                                sql.Add("(");
                                sql.Add(Select(offset, fetch, select, where, groupBy, having, orderBy, parameters, context));
                                sql.Add(")");
                            }
                            else if (SqlDbExtensions.TryNavigate(expr, out var navigate))
                            {
                                var entity = context.Convert(navigate);
                                if (entity == null)
                                {
                                    Convert(navigate, sql, parameters, context);
                                }
                                else
                                {
                                    Navigate(entity, sql, context);
                                    sql.RemoveAt(sql.Count - 1);
                                }
                            }
                            else
                            {
                                var value = expression.Invoke();
                                var parameter = Query.GetParameter(parameters.Count);
                                sql.Add(parameter);
                                parameters.Add((parameter, value));
                            }
                        }
                        break;
                    case ExpressionType.NewArrayInit:
                        {
                            var argExprs = ((NewArrayExpression)expression).Expressions;
                            Convert(argExprs[0], sql, parameters, context);
                            for (int i = 1; i < argExprs.Count; i++)
                            {
                                sql.Add(",");
                                Convert(argExprs[i], sql, parameters, context);
                            }
                        }
                        break;
                    case ExpressionType.Convert:
                        Convert(((UnaryExpression)expression).Operand, sql, parameters, context);
                        break;
                    case ExpressionType.Not:
                        sql.Add("NOT ");
                        Convert(((UnaryExpression)expression).Operand, sql, parameters, context);
                        break;
                    case ExpressionType.Equal:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" = ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.NotEqual:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" <> ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.GreaterThan:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" > ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.GreaterThanOrEqual:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" >= ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.LessThan:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" < ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.LessThanOrEqual:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" <= ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.AndAlso:
                        sql.Add("(");
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" AND ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        sql.Add(")");
                        break;
                    case ExpressionType.OrElse:
                        sql.Add("(");
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" OR ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        sql.Add(")");
                        break;
                    case ExpressionType.And:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" & ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.Or:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" | ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.Add:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" + ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.Subtract:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" - ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.Multiply:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" * ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.Divide:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" / ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.Modulo:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" % ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.Coalesce:
                        sql.Add("COALESCE(");
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(",");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        sql.Add(")");
                        break;
                    case ExpressionType.Conditional:
                        sql.Add("CASE WHEN ");
                        Convert(((ConditionalExpression)expression).Test, sql, parameters, context);
                        sql.Add(" THEN ");
                        Convert(((ConditionalExpression)expression).IfTrue, sql, parameters, context);
                        sql.Add(" ELSE ");
                        Convert(((ConditionalExpression)expression).IfFalse, sql, parameters, context);
                        sql.Add(" END");
                        break;
                    default:
                        throw new NotSupportedException(expression.ToString());
                }
            }
            public static void Navigate(DbEntity entity, List<string> sql, DbEntityContext context)
            {
                foreach ((_, var field) in entity.Table.Fields)
                {
                    sql.Add(entity.Name);
                    sql.Add(".");
                    sql.Add(field);
                    sql.Add(",");
                }
                foreach ((var property, _) in entity.Table.Tables)
                {
                    var propertyEntity = context.Add(entity, property);
                    if (propertyEntity != null)
                        Navigate(propertyEntity, sql, context);
                }
            }
            public static void Navigate(DbTable table, DbEntity entity, List<string> sql, DbEntityContext context)
            {
                foreach ((var property, var field) in entity.Table.Fields)
                {
                    sql.Add(entity.Name);
                    sql.Add(".");
                    sql.Add(field);
                    sql.Add(" AS ");
                    sql.Add(entity.Table.Columns[property]);
                    sql.Add(",");
                }
                foreach ((var property, var propertyTable) in table.Tables)
                {
                    var propertyEntity = context.Add(entity, property);
                    if (propertyEntity != null)
                        Navigate(propertyTable, propertyEntity, sql, context);
                }
            }
            public static void Select(DbTable table, PropertyInfo member, Expression expression, List<string> sql, List<(string, object)> parameters, DbEntityContext context)
            {
                if (table.Columns.TryGetValue(member, out var column))
                {
                    Convert(expression, sql, parameters, context);
                    sql.Add(" AS ");
                    sql.Add(column);
                    sql.Add(",");
                }
                else if (table.Tables.TryGetValue(member, out var memberTable))
                {
                    switch (expression.NodeType)
                    {
                        case ExpressionType.MemberInit:
                            {
                                var expr = (MemberInitExpression)expression;
                                var bindings = expr.Bindings;
                                for (int i = 0; i < bindings.Count; i++)
                                {
                                    var memberAssignment = (MemberAssignment)bindings[i];
                                    if (memberAssignment.Member is PropertyInfo memberMember)
                                        Select(memberTable, memberMember, memberAssignment.Expression, sql, parameters, context);
                                }
                            }
                            break;
                        case ExpressionType.Call:
                            {
                                var expr = (MethodCallExpression)expression;
                                if (SqlDbExtensions.TryNavigate(expr, out var navigate))
                                {
                                    var entity = context.Convert(navigate);
                                    if (entity != null)
                                    {
                                        Navigate(memberTable, entity, sql, context);
                                        break;
                                    }
                                }
                            }
                            break;
                        default:
                            {
                                var entity = context.Convert(expression);
                                if (entity != null)
                                {
                                    foreach ((var property, var field) in entity.Table.Fields)
                                    {
                                        sql.Add(entity.Name);
                                        sql.Add(".");
                                        sql.Add(field);
                                        sql.Add(" AS ");
                                        sql.Add(memberTable.Columns[property]);
                                        sql.Add(",");
                                    }
                                }
                            }
                            break;
                    }

                }
            }
            public static void Select(Expression expression, List<string> sql, List<(string, object)> parameters, DbEntityContext context)
            {
                switch (expression.NodeType)
                {
                    case ExpressionType.Convert:
                        Select(((UnaryExpression)expression).Operand, sql, parameters, context);
                        break;
                    case ExpressionType.MemberInit:
                        {
                            var expr = (MemberInitExpression)expression;
                            var table = DbTable.Get(expr.Type);
                            var bindings = expr.Bindings;
                            for (int i = 0; i < bindings.Count; i++)
                            {
                                var memberAssignment = (MemberAssignment)bindings[i];
                                if (memberAssignment.Member is PropertyInfo member)
                                    Select(table, member, memberAssignment.Expression, sql, parameters, context);
                            }
                            sql.RemoveAt(sql.Count - 1);
                        }
                        break;
                    case ExpressionType.NewArrayInit:
                        {
                            var argExprs = ((NewArrayExpression)expression).Expressions;
                            Convert(argExprs[0], sql, parameters, context);
                            for (int i = 1; i < argExprs.Count; i++)
                            {
                                sql.Add(",");
                                Select(argExprs[i], sql, parameters, context);
                            }
                        }
                        break;
                    case ExpressionType.Call:
                        {
                            var expr = (MethodCallExpression)expression;
                            if (SqlDbExtensions.TryNavigate(expr, out var navigate))
                            {
                                var entity = context.Convert(navigate);
                                if (entity == null)
                                {
                                    Convert(navigate, sql, parameters, context);
                                }
                                else
                                {
                                    Navigate(entity.Table, entity, sql, context);
                                    sql.RemoveAt(sql.Count - 1);
                                }
                            }
                            else
                            {
                                Convert(expression, sql, parameters, context);
                            }
                        }
                        break;
                    default:
                        {
                            var entity = context.Convert(expression);
                            if (entity == null)
                            {
                                Convert(expression, sql, parameters, context);
                            }
                            else
                            {
                                foreach ((var property, var field) in entity.Table.Fields)
                                {
                                    sql.Add(entity.Name);
                                    sql.Add(".");
                                    sql.Add(field);
                                    sql.Add(" AS ");
                                    sql.Add(entity.Table.Columns[property]);
                                    sql.Add(",");
                                }
                                sql.RemoveAt(sql.Count - 1);
                            }
                        }
                        break;
                }
            }
            public static string Select(LambdaExpression select, LambdaExpression where, LambdaExpression groupBy, LambdaExpression having, LambdaExpression orderBy, List<(string, object)> parameters, DbEntityContext context)
            {
                var sql = new List<string>();
                var entity = context.Add(select.Parameters[0]);
                if (where != null)
                {
                    context.Add(where.Parameters[0], entity);
                    sql.Add(" WHERE ");
                    Convert(where.Body, sql, parameters, context);
                }
                if (groupBy != null)
                {
                    context.Add(groupBy.Parameters[0], entity);
                    sql.Add(" GROUP BY ");
                    Convert(groupBy.Body, sql, parameters, context);
                }
                if (having != null)
                {
                    context.Add(having.Parameters[0], entity);
                    sql.Add(" HAVING ");
                    Convert(having.Body, sql, parameters, context);
                }
                if (orderBy != null)
                {
                    context.Add(orderBy.Parameters[0], entity);
                    sql.Add(" ORDER BY ");
                    Convert(orderBy.Body, sql, parameters, context);
                }
                var index = sql.Count;
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);
                //Convert(select.Body, sql, parameters, context);

                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }
                return sql.Concat((index, sql.Count), (0, index));
            }
            public static string Select(int offset, int fetch, LambdaExpression select, LambdaExpression where, LambdaExpression groupBy, LambdaExpression having, LambdaExpression orderBy, List<(string, object)> parameters, DbEntityContext context)
            {
                var sql = new List<string>();
                var entity = context.Add(select.Parameters[0]);
                if (where != null)
                {
                    context.Add(where.Parameters[0], entity);
                    sql.Add(" WHERE ");
                    Convert(where.Body, sql, parameters, context);
                }
                if (groupBy != null)
                {
                    context.Add(groupBy.Parameters[0], entity);
                    sql.Add(" GROUP BY ");
                    Convert(groupBy.Body, sql, parameters, context);
                }
                if (having != null)
                {
                    context.Add(having.Parameters[0], entity);
                    sql.Add(" HAVING ");
                    Convert(having.Body, sql, parameters, context);
                }
                if (orderBy != null)
                {
                    context.Add(orderBy.Parameters[0], entity);
                    sql.Add(" ORDER BY ");
                    Convert(orderBy.Body, sql, parameters, context);
                }
                sql.Add($" OFFSET {offset} LIMIT {fetch}");
                var index = sql.Count;
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);
                //Convert(select.Body, sql, parameters, context);

                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }
                return sql.Concat((index, sql.Count), (0, index));
            }
            public static string Insert(LambdaExpression entity, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var context = new DbEntityContext();
                var table = DbTable.Get(entity.Body.Type);
                sql.Add("INSERT INTO ");
                sql.Add(table.Name);
                sql.Add("(");
                if (entity.Body.NodeType == ExpressionType.MemberInit)
                {
                    var bindings = ((MemberInitExpression)entity.Body).Bindings;
                    Span<int> indexs = bindings.Count < 256 ? stackalloc int[bindings.Count] : new int[bindings.Count];
                    var index = 0;
                    //fields
                    {
                        var i = 0;
                        for (; i < bindings.Count; i++)
                        {
                            var memberAssignment = (MemberAssignment)bindings[i];
                            if (table.Fields.TryGetValue((PropertyInfo)memberAssignment.Member, out var name))
                            {
                                sql.Add(name);
                                indexs[index++] = i++;
                                break;
                            }
                        }
                        for (; i < bindings.Count; i++)
                        {
                            var memberAssignment = (MemberAssignment)bindings[i];
                            if (table.Fields.TryGetValue((PropertyInfo)memberAssignment.Member, out var name))
                            {
                                sql.Add(",");
                                sql.Add(name);
                                indexs[index++] = i;
                            }
                        }
                    }
                    sql.Add(") VALUES (");
                    //values
                    {
                        if (index > 0)
                        {
                            Convert(((MemberAssignment)bindings[indexs[0]]).Expression, sql, parameters, context);
                        }
                        for (int i = 1; i < index; i++)
                        {
                            sql.Add(",");
                            Convert(((MemberAssignment)bindings[indexs[i]]).Expression, sql, parameters, context);
                        }
                    }
                }
                sql.Add(")");
                return sql.Concat();
            }
            public static string Insert(object entity, LambdaExpression properties, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var table = DbTable.Get(properties.Parameters[0].Type);
                var fields = GetFields(table, properties.Body);
                sql.Add("INSERT INTO ");
                sql.Add(table.Name);
                sql.Add("(");
                var enumerator = fields.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    sql.Add(enumerator.Current.Value);
                }
                while (enumerator.MoveNext())
                {
                    sql.Add(",");
                    sql.Add(enumerator.Current.Value);
                }
                sql.Add(") VALUES (");
                enumerator = fields.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    var value = enumerator.Current.Key.GetValue(entity);
                    var parameter = Query.GetParameter(parameters.Count);
                    parameters.Add((parameter, value));
                    sql.Add(parameter);
                }
                while (enumerator.MoveNext())
                {
                    sql.Add(",");
                    var value = enumerator.Current.Key.GetValue(entity);
                    var parameter = Query.GetParameter(parameters.Count);
                    parameters.Add((parameter, value));
                    sql.Add(parameter);
                }
                sql.Add(")");
                return sql.Concat();
            }
            public static string InsertIdentity(LambdaExpression entity, List<(string, object)> parameters)
            {
                var sql = Insert(entity, parameters);
                var table = DbTable.Get(entity.Body.Type);
                return sql + " RETURNING " + table.Identity;
            }
            public static string InsertIdentity(object entity, LambdaExpression properties, List<(string, object)> parameters)
            {
                var sql = Insert(entity, properties, parameters);
                var table = DbTable.Get(properties.Parameters[0].Type);
                return sql + " RETURNING " + table.Identity;
            }
            public static string Delete(DbTable table, object identity, List<(string, object)> parameters)
            {
                var sql = CommandText;
                sql.Add("DELETE FROM ");
                sql.Add(table.Name);
                sql.Add(" WHERE ");
                sql.Add(table.Name);
                sql.Add(".");
                sql.Add(table.Identity);
                if (identity == null)
                {
                    sql.Add(" IS NULL");
                }
                else
                {
                    var parameter = Query.GetParameter(parameters.Count);
                    sql.Add(" = ");
                    sql.Add(parameter);
                    parameters.Add((parameter, identity));
                }
                return sql.Concat();
            }
            public static string Delete(DbTable table, LambdaExpression where, List<(string, object)> parameters)
            {
                var sql = CommandText;
                sql.Add("DELETE FROM ");
                sql.Add(table.Name);
                sql.Add(" WHERE ");
                var context = new DbEntityContext();
                context.Add(where.Parameters[0], new DbEntity()
                {
                    Name = table.Name,
                    Table = table,
                    Entities = new Dictionary<PropertyInfo, DbEntity>()
                });
                Convert(where.Body, sql, parameters, context);
                return sql.Concat();
            }
            public static string Update(LambdaExpression entity, object identity, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var table = DbTable.Get(entity.Body.Type);
                var context = new DbEntityContext();
                context.Add(entity.Parameters[0], new DbEntity()
                {
                    Name = table.Name,
                    Table = table,
                    Entities = new Dictionary<PropertyInfo, DbEntity>()
                });
                sql.Add("UPDATE ");
                sql.Add(table.Name);
                sql.Add(" SET ");
                if (entity.Body.NodeType == ExpressionType.MemberInit)
                {
                    var bindings = ((MemberInitExpression)entity.Body).Bindings;
                    var i = 0;
                    for (; i < bindings.Count;)
                    {
                        var memberAssignment = (MemberAssignment)bindings[i++];
                        if (table.Fields.TryGetValue((PropertyInfo)memberAssignment.Member, out var name))
                        {
                            sql.Add(name);
                            sql.Add("=");
                            Convert(memberAssignment.Expression, sql, parameters, context);
                            break;
                        }
                    }
                    for (; i < bindings.Count;)
                    {
                        var memberAssignment = (MemberAssignment)bindings[i++];
                        if (table.Fields.TryGetValue((PropertyInfo)memberAssignment.Member, out var name))
                        {
                            sql.Add(",");
                            sql.Add(name);
                            sql.Add("=");
                            Convert(memberAssignment.Expression, sql, parameters, context);
                        }
                    }
                }
                sql.Add(" WHERE ");
                sql.Add(table.Name);
                sql.Add(".");
                sql.Add(table.Identity);
                if (identity == null)
                {
                    sql.Add(" IS NULL");
                }
                else
                {
                    var parameter = Query.GetParameter(parameters.Count);
                    sql.Add(" = ");
                    sql.Add(parameter);
                    parameters.Add((parameter, identity));
                }
                return sql.Concat();
            }
            public static string Update(LambdaExpression entity, LambdaExpression where, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var table = DbTable.Get(entity.Body.Type);
                var context = new DbEntityContext();
                var dbEntity = new DbEntity()
                {
                    Name = table.Name,
                    Table = table,
                    Entities = new Dictionary<PropertyInfo, DbEntity>()
                };
                context.Add(entity.Parameters[0], dbEntity);
                sql.Add("UPDATE ");
                sql.Add(table.Name);
                sql.Add(" SET ");
                if (entity.Body.NodeType == ExpressionType.MemberInit)
                {
                    var bindings = ((MemberInitExpression)entity.Body).Bindings;
                    var i = 0;
                    for (; i < bindings.Count;)
                    {
                        var memberAssignment = (MemberAssignment)bindings[i++];
                        if (table.Fields.TryGetValue((PropertyInfo)memberAssignment.Member, out var name))
                        {
                            sql.Add(name);
                            sql.Add("=");
                            Convert(memberAssignment.Expression, sql, parameters, context);
                            break;
                        }
                    }
                    for (; i < bindings.Count;)
                    {
                        var memberAssignment = (MemberAssignment)bindings[i++];
                        if (table.Fields.TryGetValue((PropertyInfo)memberAssignment.Member, out var name))
                        {
                            sql.Add(",");
                            sql.Add(name);
                            sql.Add("=");
                            Convert(memberAssignment.Expression, sql, parameters, context);
                        }
                    }
                }
                sql.Add(" WHERE ");
                context.Add(where.Parameters[0], dbEntity);
                Convert(where.Body, sql, parameters, context);
                return sql.Concat();
            }
            public static string Update(object entity, LambdaExpression properties, object identity, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var table = DbTable.Get(properties.Parameters[0].Type);
                var fields = GetFields(table, properties.Body);
                sql.Add("UPDATE ");
                sql.Add(table.Name);
                sql.Add(" SET ");
                var enumerator = fields.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    var value = enumerator.Current.Key.GetValue(entity);
                    var parameter = Query.GetParameter(parameters.Count);
                    parameters.Add((parameter, value));
                    sql.Add(enumerator.Current.Value);
                    sql.Add("=");
                    sql.Add(parameter);
                }
                while (enumerator.MoveNext())
                {
                    sql.Add(",");
                    var value = enumerator.Current.Key.GetValue(entity);
                    var parameter = Query.GetParameter(parameters.Count);
                    parameters.Add((parameter, value));
                    sql.Add(enumerator.Current.Value);
                    sql.Add("=");
                    sql.Add(parameter);
                }
                sql.Add(" WHERE ");
                sql.Add(table.Name);
                sql.Add(".");
                sql.Add(table.Identity);
                if (identity == null)
                {
                    sql.Add(" IS NULL");
                }
                else
                {
                    var parameter = Query.GetParameter(parameters.Count);
                    sql.Add(" = ");
                    sql.Add(parameter);
                    parameters.Add((parameter, identity));
                }
                return sql.Concat();
            }
            public static string Update(object entity, LambdaExpression properties, LambdaExpression where, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var table = DbTable.Get(properties.Parameters[0].Type);
                var fields = GetFields(table, properties.Body);
                sql.Add("UPDATE ");
                sql.Add(table.Name);
                sql.Add(" SET ");
                var enumerator = fields.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    var value = enumerator.Current.Key.GetValue(entity);
                    var parameter = Query.GetParameter(parameters.Count);
                    parameters.Add((parameter, value));
                    sql.Add(enumerator.Current.Value);
                    sql.Add("=");
                    sql.Add(parameter);
                }
                while (enumerator.MoveNext())
                {
                    sql.Add(",");
                    var value = enumerator.Current.Key.GetValue(entity);
                    var parameter = Query.GetParameter(parameters.Count);
                    parameters.Add((parameter, value));
                    sql.Add(enumerator.Current.Value);
                    sql.Add("=");
                    sql.Add(parameter);
                }
                sql.Add(" WHERE ");
                var context = new DbEntityContext();
                context.Add(where.Parameters[0], new DbEntity()
                {
                    Name = table.Name,
                    Table = table,
                    Entities = new Dictionary<PropertyInfo, DbEntity>()
                });
                Convert(where.Body, sql, parameters, context);
                return sql.Concat();
            }
            public static string SelectSingle(LambdaExpression select, object identity, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var context = new DbEntityContext();
                var entity = context.Add(select.Parameters[0]);
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);

                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }
                sql.Add(" WHERE ");
                sql.Add(entity.Name);
                sql.Add(".");
                sql.Add(entity.Table.Identity);
                if (identity == null)
                {
                    sql.Add(" IS NULL");
                }
                else
                {
                    var parameter = Query.GetParameter(parameters.Count);
                    sql.Add(" = ");
                    sql.Add(parameter);
                    parameters.Add((parameter, identity));
                }
                return sql.Concat();
            }
            public static string SelectSingle(LambdaExpression select, LambdaExpression where, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var context = new DbEntityContext();
                var entity = context.Add(select.Parameters[0]);
                if (where != null)
                {
                    context.Add(where.Parameters[0], entity);
                    sql.Add(" WHERE ");
                    Convert(where.Body, sql, parameters, context);
                }
                var index = sql.Count;
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);

                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }

                return sql.Concat((index, sql.Count), (0, index));
            }
            public static string Select(LambdaExpression select, LambdaExpression where, LambdaExpression orderBy, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var context = new DbEntityContext();
                var entity = context.Add(select.Parameters[0]);
                if (where != null)
                {
                    context.Add(where.Parameters[0], entity);
                    sql.Add(" WHERE ");
                    Convert(where.Body, sql, parameters, context);
                }
                if (orderBy != null)
                {
                    context.Add(orderBy.Parameters[0], entity);
                    sql.Add(" ORDER BY ");
                    Convert(orderBy.Body, sql, parameters, context);
                }
                var index = sql.Count;
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);

                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }
                return sql.Concat((index, sql.Count), (0, index));
            }
            public static string Select(int offset, int fetch, LambdaExpression select, LambdaExpression where, LambdaExpression orderBy, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var context = new DbEntityContext();
                var entity = context.Add(select.Parameters[0]);
                if (where != null)
                {
                    context.Add(where.Parameters[0], entity);
                    sql.Add(" WHERE ");
                    Convert(where.Body, sql, parameters, context);
                }
                if (orderBy != null)
                {
                    context.Add(orderBy.Parameters[0], entity);
                    sql.Add(" ORDER BY ");
                    Convert(orderBy.Body, sql, parameters, context);
                }
                sql.Add($" OFFSET {offset} LIMIT {fetch}");
                var index = sql.Count;
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);

                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }
                return sql.Concat((index, sql.Count), (0, index));
            }
            public static string SelectPaged(int offset, int fetch, LambdaExpression select, LambdaExpression from, LambdaExpression where, LambdaExpression orderBy, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var context = new DbEntityContext();
                var entity = context.Add(select.Parameters[0]);
                if (from != null)
                {
                    var fromSql = new List<string>();
                    Convert(from.Body, fromSql, parameters, context);
                    entity.Table.Name = fromSql.Concat();
                }
                if (where != null)
                {
                    context.Add(where.Parameters[0], entity);
                    sql.Add(" WHERE ");
                    Convert(where.Body, sql, parameters, context);
                }
                var whereIndex = sql.Count;
                if (orderBy != null)
                {
                    context.Add(orderBy.Parameters[0], entity);
                    sql.Add(" ORDER BY ");
                    Convert(orderBy.Body, sql, parameters, context);
                }
                sql.Add($" OFFSET {offset} LIMIT {fetch}");
                var selectIndex = sql.Count;
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);
                var fromIndex = sql.Count;
                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }
                var countIndex = sql.Count;
                sql.Add(";SELECT COUNT(1)");
                return sql.Concat((selectIndex, countIndex), (0, selectIndex), (countIndex, sql.Count), (fromIndex, countIndex), (0, whereIndex));
            }
            public static string Select(LambdaExpression select, LambdaExpression from, LambdaExpression where, LambdaExpression groupBy, LambdaExpression having, LambdaExpression orderBy, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var context = new DbEntityContext();
                var entity = context.Add(select.Parameters[0]);
                if (from != null)
                {
                    var fromSql = new List<string>();
                    Convert(from.Body, fromSql, parameters, context);
                    entity.Table.Name = fromSql.Concat();
                }
                if (where != null)
                {
                    context.Add(where.Parameters[0], entity);
                    sql.Add(" WHERE ");
                    Convert(where.Body, sql, parameters, context);
                }
                if (groupBy != null)
                {
                    context.Add(groupBy.Parameters[0], entity);
                    sql.Add(" GROUP BY ");
                    Convert(groupBy.Body, sql, parameters, context);
                }
                if (having != null)
                {
                    context.Add(having.Parameters[0], entity);
                    sql.Add(" HAVING ");
                    Convert(having.Body, sql, parameters, context);
                }
                if (orderBy != null)
                {
                    context.Add(orderBy.Parameters[0], entity);
                    sql.Add(" ORDER BY ");
                    Convert(orderBy.Body, sql, parameters, context);
                }
                var index = sql.Count;
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);

                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }
                return sql.Concat((index, sql.Count), (0, index));
            }
            public static string Select(int offset, int fetch, LambdaExpression select, LambdaExpression from, LambdaExpression where, LambdaExpression groupBy, LambdaExpression having, LambdaExpression orderBy, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var context = new DbEntityContext();
                var entity = context.Add(select.Parameters[0]);
                if (from != null)
                {
                    var fromSql = new List<string>();
                    Convert(from.Body, fromSql, parameters, context);
                    entity.Table.Name = fromSql.Concat();
                }
                if (where != null)
                {
                    context.Add(where.Parameters[0], entity);
                    sql.Add(" WHERE ");
                    Convert(where.Body, sql, parameters, context);
                }
                if (groupBy != null)
                {
                    context.Add(groupBy.Parameters[0], entity);
                    sql.Add(" GROUP BY ");
                    Convert(groupBy.Body, sql, parameters, context);
                }
                if (having != null)
                {
                    context.Add(having.Parameters[0], entity);
                    sql.Add(" HAVING ");
                    Convert(having.Body, sql, parameters, context);
                }
                if (orderBy != null)
                {
                    context.Add(orderBy.Parameters[0], entity);
                    sql.Add(" ORDER BY ");
                    Convert(orderBy.Body, sql, parameters, context);
                }
                sql.Add($" OFFSET {offset} LIMIT {fetch}");
                var index = sql.Count;
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);

                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }
                return sql.Concat((index, sql.Count), (0, index));
            }
            public PostgreSqlDb(Type connectionType, Func<(DbCommand, IDisposable)> getCommand, Action<DbCommand> cmdExecuting)
            {
                if (_ConnectionType == null)
                {
                    lock (_Sync)
                    {
                        if (_ConnectionType == null)
                        {
                            _ConnectionType = connectionType;
                            _DataReaderType = connectionType.GetMethod("CreateCommand", Type.EmptyTypes).ReturnType.
                                GetMethod("ExecuteReader", Type.EmptyTypes).ReturnType;

                            _AddParameter = typeof(SqlDbExtensions).GetMethod("AddParameter").MakeGenericMethod(connectionType).
                               CreateDelegate<Action<DbCommand, string, object>>();

                            var member = Expression.Parameter(typeof(MemberExpression), "member");
                            var registerMember = typeof(SqlDbExtensions).GetMethod("RegisterDbMember", new[] { typeof(MemberExpression), typeof(IReadOnlyList<object>).MakeByRefType() });
                            var memberObjs = Expression.Variable(typeof(IReadOnlyList<object>), "exprObjs");
                            _DbMember = Expression.Lambda<Func<MemberExpression, IReadOnlyList<object>>>(
                                Expression.Block(new[] { memberObjs },
                                Expression.Call(null, registerMember.MakeGenericMethod(connectionType), member, memberObjs),
                                memberObjs), member).Compile();

                            var method = Expression.Parameter(typeof(MethodCallExpression), "method");
                            var registerMethod = typeof(SqlDbExtensions).GetMethod("RegisterDbMethod", new[] { typeof(MethodCallExpression), typeof(IReadOnlyList<object>).MakeByRefType() });
                            var methodObjs = Expression.Variable(typeof(IReadOnlyList<object>), "exprObjs");
                            _DbMethod = Expression.Lambda<Func<MethodCallExpression, IReadOnlyList<object>>>(
                                Expression.Block(new[] { methodObjs },
                                Expression.Call(null, registerMethod.MakeGenericMethod(connectionType), method, methodObjs),
                                methodObjs), method).Compile();
                        }
                    }
                }
                _getCommand = getCommand;
                _cmdExecuting = cmdExecuting;
            }
            public override int Execute(string sql)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    return cmd.ExecuteNonQuery();
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            public override int Execute(string sql, object parameters)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    Query.AddParameter(cmd, parameters);
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    return cmd.ExecuteNonQuery();
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            public override int Execute(string sql, IList<(string, object)> parameters)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    if (parameters != null && parameters.Count > 0)
                    {
                        for (int i = 0; i < parameters.Count; i++)
                        {
                            (var name, var value) = parameters[i];
                            _AddParameter(cmd, name, value);
                        }
                    }
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    return cmd.ExecuteNonQuery();
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            public override async Task<int> ExecuteAsync(string sql)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    return await cmd.ExecuteNonQueryAsync();
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            public override async Task<int> ExecuteAsync(string sql, object parameters)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    Query.AddParameter(cmd, parameters);
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    return await cmd.ExecuteNonQueryAsync();
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            public override async Task<int> ExecuteAsync(string sql, IList<(string, object)> parameters)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    if (parameters != null && parameters.Count > 0)
                    {
                        for (int i = 0; i < parameters.Count; i++)
                        {
                            (var name, var value) = parameters[i];
                            _AddParameter(cmd, name, value);
                        }
                    }
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    return await cmd.ExecuteNonQueryAsync();
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            public override int ExecuteFormat(FormattableString sqlFormat)
            {
                if (sqlFormat == null)
                    throw new ArgumentNullException(nameof(sqlFormat));

                var parameters = Parameters;
                var args = sqlFormat.GetArguments();
                var names = new string[args.Length];
                for (int i = 0; i < args.Length; i++)
                {
                    var name = Query.GetParameter(parameters.Count);
                    parameters.Add((name, args[i]));
                    names[i] = name;
                }
                return Execute(string.Format(sqlFormat.Format, names), parameters);
            }
            public override Task<int> ExecuteFormatAsync(FormattableString sqlFormat)
            {
                if (sqlFormat == null)
                    throw new ArgumentNullException(nameof(sqlFormat));

                var parameters = Parameters;
                var args = sqlFormat.GetArguments();
                var names = new string[args.Length];
                for (int i = 0; i < args.Length; i++)
                {
                    var name = Query.GetParameter(parameters.Count);
                    parameters.Add((name, args[i]));
                    names[i] = name;
                }
                return ExecuteAsync(string.Format(sqlFormat.Format, names), parameters);
            }
            public override (DbDataReader, IDisposable) ExecuteReader(string sql)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    var reader = cmd.ExecuteReader();
                    return (reader, disposable);
                }
                catch
                {
                    disposable.Dispose();
                    throw;
                }
            }
            public override (DbDataReader, IDisposable) ExecuteReader(string sql, object parameters)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    Query.AddParameter(cmd, parameters);
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    var reader = cmd.ExecuteReader();
                    return (reader, disposable);
                }
                catch
                {
                    disposable.Dispose();
                    throw;
                }
            }
            public override (DbDataReader, IDisposable) ExecuteReader(string sql, IList<(string, object)> parameters)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    if (parameters != null && parameters.Count > 0)
                    {
                        for (int i = 0; i < parameters.Count; i++)
                        {
                            (var name, var value) = parameters[i];
                            _AddParameter(cmd, name, value);
                        }
                    }
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    var reader = cmd.ExecuteReader();
                    return (reader, disposable);
                }
                catch
                {
                    disposable.Dispose();
                    throw;
                }
            }
            public override (DbDataReader, IDisposable) ExecuteReaderFormat(FormattableString sqlFormat)
            {
                if (sqlFormat == null)
                    throw new ArgumentNullException(nameof(sqlFormat));

                var parameters = Parameters;
                var args = sqlFormat.GetArguments();
                var names = new string[args.Length];
                for (int i = 0; i < args.Length; i++)
                {
                    var name = Query.GetParameter(parameters.Count);
                    parameters.Add((name, args[i]));
                    names[i] = name;
                }
                return ExecuteReader(string.Format(sqlFormat.Format, names), parameters);
            }
            public override async Task<(DbDataReader, IDisposable)> ExecuteReaderAsync(string sql)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    var reader = await cmd.ExecuteReaderAsync();
                    return (reader, disposable);
                }
                catch
                {
                    disposable.Dispose();
                    throw;
                }
            }
            public override async Task<(DbDataReader, IDisposable)> ExecuteReaderAsync(string sql, object parameters)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    Query.AddParameter(cmd, parameters);
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    var reader = await cmd.ExecuteReaderAsync();
                    return (reader, disposable);
                }
                catch
                {
                    disposable.Dispose();
                    throw;
                }
            }
            public override async Task<(DbDataReader, IDisposable)> ExecuteReaderAsync(string sql, IList<(string, object)> parameters)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    if (parameters != null && parameters.Count > 0)
                    {
                        for (int i = 0; i < parameters.Count; i++)
                        {
                            (var name, var value) = parameters[i];
                            _AddParameter(cmd, name, value);
                        }
                    }
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    var reader = await cmd.ExecuteReaderAsync();
                    return (reader, disposable);
                }
                catch
                {
                    disposable.Dispose();
                    throw;
                }
            }
            public override Task<(DbDataReader, IDisposable)> ExecuteReaderFormatAsync(FormattableString sqlFormat)
            {
                if (sqlFormat == null)
                    throw new ArgumentNullException(nameof(sqlFormat));

                var parameters = Parameters;
                var args = sqlFormat.GetArguments();
                var names = new string[args.Length];
                for (int i = 0; i < args.Length; i++)
                {
                    var name = Query.GetParameter(parameters.Count);
                    parameters.Add((name, args[i]));
                    names[i] = name;
                }
                return ExecuteReaderAsync(string.Format(sqlFormat.Format, names), parameters);
            }
            public override TEntity Read<TEntity>(DbDataReader dataReader)
            {
                var handler = Reader<TEntity>.Handler;
                if (handler == null)
                {
                    lock (_Sync)
                    {
                        handler = Reader<TEntity>.Handler;
                        if (handler == null)
                        {
                            var register = typeof(SqlDbExtensions).GetMethod("RegisterDbReader", new[] { typeof(Type), typeof(ParameterExpression), typeof(Expression).MakeByRefType(), typeof(Delegate).MakeByRefType() }).MakeGenericMethod(_DataReaderType);
                            var dbReader = Expression.Parameter(typeof(DbDataReader), "dbReader");
                            var reader = Expression.Variable(_DataReaderType, "reader");
                            var args = new object[] { typeof(TEntity), reader, default(Expression), default(Delegate) };
                            register.Invoke(null, args);
                            var read = _DataReaderType.GetMethod("Read", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
                            var expr = Expression.Block(new[] { reader },
                                Expression.Assign(reader, Expression.Convert(dbReader, _DataReaderType)),
                                Expression.Condition(Expression.Call(reader, read), (Expression)args[2], Expression.Default(typeof(TEntity))));
                            handler = Expression.Lambda<Func<DbDataReader, TEntity>>(expr, dbReader).Compile();
                            Reader<TEntity>.Handler = handler;
                        }
                    }
                }
                return handler(dataReader);
            }
            public override int Insert<TEntity>(Expression<Func<SqlExpression, TEntity>> entity)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                var parameters = Parameters;
                var sql = Insert(entity, parameters);
                return Execute(sql, parameters);
            }
            public override int Insert<TEntity>(TEntity entity, Expression<Func<TEntity, object>> properties)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));

                var parameters = Parameters;
                var sql = Insert(entity, properties, parameters);
                return Execute(sql, parameters);
            }
            public override int InsertRange<TEntity>(IList<TEntity> entities, Expression<Func<TEntity, object>> properties)
            {
                if (entities == null)
                    throw new ArgumentNullException(nameof(entities));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));

                var result = 0;
                var sql = new List<string>();
                var parameters = new List<(string, object)>();
                var table = DbTable.Get(properties.Parameters[0].Type);
                var fields = GetFields(table, properties.Body);
                for (int index = 0; ;)
                {
                    if (index == entities.Count)
                        break;
                    var entity = entities[index++];
                    var count = 1;
                    var fieldCount = fields.Count;
                    sql.Clear();
                    parameters.Clear();
                    sql.Add("INSERT INTO ");
                    sql.Add(table.Name);
                    sql.Add("(");
                    var enumerator = fields.GetEnumerator();
                    if (enumerator.MoveNext())
                    {
                        sql.Add(enumerator.Current.Value);
                    }
                    while (enumerator.MoveNext())
                    {
                        sql.Add(",");
                        sql.Add(enumerator.Current.Value);
                    }
                    sql.Add(") VALUES (");
                    enumerator = fields.GetEnumerator();
                    if (enumerator.MoveNext())
                    {
                        var value = enumerator.Current.Key.GetValue(entity);
                        var parameter = Query.GetParameter(parameters.Count);
                        parameters.Add((parameter, value));
                        sql.Add(parameter);
                    }
                    while (enumerator.MoveNext())
                    {
                        sql.Add(",");
                        var value = enumerator.Current.Key.GetValue(entity);
                        var parameter = Query.GetParameter(parameters.Count);
                        parameters.Add((parameter, value));
                        sql.Add(parameter);
                    }
                    sql.Add(")");
                    while (index < entities.Count)
                    {
                        if (count == 5000 || fieldCount + fields.Count >= 3000)
                            break;
                        count += 1;
                        fieldCount += fields.Count;
                        entity = entities[index++];
                        sql.Add(",(");
                        enumerator = fields.GetEnumerator();
                        if (enumerator.MoveNext())
                        {
                            var value = enumerator.Current.Key.GetValue(entity);
                            var parameter = Query.GetParameter(parameters.Count);
                            parameters.Add((parameter, value));
                            sql.Add(parameter);
                        }
                        while (enumerator.MoveNext())
                        {
                            sql.Add(",");
                            var value = enumerator.Current.Key.GetValue(entity);
                            var parameter = Query.GetParameter(parameters.Count);
                            parameters.Add((parameter, value));
                            sql.Add(parameter);
                        }
                        sql.Add(")");
                    }
                    result += Execute(sql.Concat(), parameters);
                }
                return result;
            }
            public override Task<int> InsertAsync<TEntity>(Expression<Func<SqlExpression, TEntity>> entity)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                var parameters = Parameters;
                var sql = Insert(entity, parameters);
                return ExecuteAsync(sql, parameters);
            }
            public override Task<int> InsertAsync<TEntity>(TEntity entity, Expression<Func<TEntity, object>> properties)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));

                var parameters = Parameters;
                var sql = Insert(entity, properties, parameters);
                return ExecuteAsync(sql, parameters);
            }
            public override async Task<int> InsertRangeAsync<TEntity>(IList<TEntity> entities, Expression<Func<TEntity, object>> properties)
            {
                if (entities == null)
                    throw new ArgumentNullException(nameof(entities));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));

                var result = 0;
                var sql = new List<string>();
                var parameters = new List<(string, object)>();
                var table = DbTable.Get(properties.Parameters[0].Type);
                var fields = GetFields(table, properties.Body);
                for (int index = 0; ;)
                {
                    if (index == entities.Count)
                        break;
                    var entity = entities[index++];
                    var count = 1;
                    var fieldCount = fields.Count;
                    sql.Clear();
                    parameters.Clear();
                    sql.Add("INSERT INTO ");
                    sql.Add(table.Name);
                    sql.Add("(");
                    var enumerator = fields.GetEnumerator();
                    if (enumerator.MoveNext())
                    {
                        sql.Add(enumerator.Current.Value);
                    }
                    while (enumerator.MoveNext())
                    {
                        sql.Add(",");
                        sql.Add(enumerator.Current.Value);
                    }
                    sql.Add(") VALUES (");
                    enumerator = fields.GetEnumerator();
                    if (enumerator.MoveNext())
                    {
                        var value = enumerator.Current.Key.GetValue(entity);
                        var parameter = Query.GetParameter(parameters.Count);
                        parameters.Add((parameter, value));
                        sql.Add(parameter);
                    }
                    while (enumerator.MoveNext())
                    {
                        sql.Add(",");
                        var value = enumerator.Current.Key.GetValue(entity);
                        var parameter = Query.GetParameter(parameters.Count);
                        parameters.Add((parameter, value));
                        sql.Add(parameter);
                    }
                    sql.Add(")");
                    while (index < entities.Count)
                    {
                        if (count == 5000 || fieldCount + fields.Count >= 3000)
                            break;
                        count += 1;
                        fieldCount += fields.Count;
                        entity = entities[index++];
                        sql.Add(",(");
                        enumerator = fields.GetEnumerator();
                        if (enumerator.MoveNext())
                        {
                            var value = enumerator.Current.Key.GetValue(entity);
                            var parameter = Query.GetParameter(parameters.Count);
                            parameters.Add((parameter, value));
                            sql.Add(parameter);
                        }
                        while (enumerator.MoveNext())
                        {
                            sql.Add(",");
                            var value = enumerator.Current.Key.GetValue(entity);
                            var parameter = Query.GetParameter(parameters.Count);
                            parameters.Add((parameter, value));
                            sql.Add(parameter);
                        }
                        sql.Add(")");
                    }
                    result += await ExecuteAsync(sql.Concat(), parameters);
                }
                return result;
            }
            public override TIdentity InsertIdentity<TEntity, TIdentity>(Expression<Func<SqlExpression, TEntity>> entity)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                var parameters = Parameters;
                var sql = InsertIdentity(entity, parameters);
                return this.Execute<TIdentity>(sql, parameters);
            }
            public override TIdentity InsertIdentity<TEntity, TIdentity>(TEntity entity, Expression<Func<TEntity, object>> properties)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));

                var parameters = Parameters;
                var sql = InsertIdentity(entity, properties, parameters);
                return this.Execute<TIdentity>(sql, parameters);
            }
            public override Task<TIdentity> InsertIdentityAsync<TEntity, TIdentity>(Expression<Func<SqlExpression, TEntity>> entity)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                var parameters = Parameters;
                var sql = InsertIdentity(entity, parameters);
                return this.ExecuteAsync<TIdentity>(sql, parameters);
            }
            public override Task<TIdentity> InsertIdentityAsync<TEntity, TIdentity>(TEntity entity, Expression<Func<TEntity, object>> properties)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));

                var parameters = Parameters;
                var sql = InsertIdentity(entity, properties, parameters);
                return this.ExecuteAsync<TIdentity>(sql, parameters);
            }
            public override int Delete<TEntity>(object identity)
            {
                var parameters = Parameters;
                var sql = Delete(DbTable.Get(typeof(TEntity)), identity, parameters);
                return Execute(sql, parameters);
            }
            public override int Delete<TEntity>(Expression<Func<TEntity, SqlExpression, bool>> where)
            {
                if (where == null)
                    throw new ArgumentNullException(nameof(where));

                var parameters = Parameters;
                var sql = Delete(DbTable.Get(typeof(TEntity)), where, parameters);
                return Execute(sql, parameters);
            }
            public override Task<int> DeleteAsync<TEntity>(object identity)
            {
                var parameters = Parameters;
                var sql = Delete(DbTable.Get(typeof(TEntity)), identity, parameters);
                return ExecuteAsync(sql, parameters);
            }
            public override Task<int> DeleteAsync<TEntity>(Expression<Func<TEntity, SqlExpression, bool>> where)
            {
                if (where == null)
                    throw new ArgumentNullException(nameof(where));

                var parameters = Parameters;
                var sql = Delete(DbTable.Get(typeof(TEntity)), where, parameters);
                return ExecuteAsync(sql, parameters);
            }
            public override int Update<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> entity, object identity)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                var parameters = Parameters;
                var sql = Update(entity, identity, parameters);
                return Execute(sql, parameters);
            }
            public override int Update<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> entity, Expression<Func<TEntity, SqlExpression, bool>> where)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (where == null)
                    throw new ArgumentNullException(nameof(where));

                var parameters = Parameters;
                var sql = Update(entity, where, parameters);
                return Execute(sql, parameters);
            }
            public override int Update<TEntity>(TEntity entity, Expression<Func<TEntity, object>> properties)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));

                SqlDbExtensions.RegisterIdentity<TEntity>(out var identity);
                if (identity == null)
                    throw new ArgumentNullException(nameof(identity));

                var parameters = Parameters;
                var sql = Update(entity, properties, identity.GetValue(entity), parameters);
                return Execute(sql, parameters);
            }
            public override int Update<TEntity>(TEntity entity, Expression<Func<TEntity, object>> properties, Expression<Func<TEntity, SqlExpression, bool>> where)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));
                if (where == null)
                    throw new ArgumentNullException(nameof(where));

                var parameters = Parameters;
                var sql = Update(entity, properties, where, parameters);
                return Execute(sql, parameters);
            }
            public override Task<int> UpdateAsync<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> entity, object identity)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                var parameters = Parameters;
                var sql = Update(entity, identity, parameters);
                return ExecuteAsync(sql, parameters);
            }
            public override Task<int> UpdateAsync<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> entity, Expression<Func<TEntity, SqlExpression, bool>> where)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (where == null)
                    throw new ArgumentNullException(nameof(where));

                var parameters = Parameters;
                var sql = Update(entity, where, parameters);
                return ExecuteAsync(sql, parameters);
            }
            public override Task<int> UpdateAsync<TEntity>(TEntity entity, Expression<Func<TEntity, object>> properties)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));

                SqlDbExtensions.RegisterIdentity<TEntity>(out var identity);
                if (identity == null)
                    throw new ArgumentNullException(nameof(identity));

                var parameters = Parameters;
                var sql = Update(entity, properties, identity.GetValue(entity), parameters);
                return ExecuteAsync(sql, parameters);
            }
            public override Task<int> UpdateAsync<TEntity>(TEntity entity, Expression<Func<TEntity, object>> properties, Expression<Func<TEntity, SqlExpression, bool>> where)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));
                if (where == null)
                    throw new ArgumentNullException(nameof(where));

                var parameters = Parameters;
                var sql = Update(entity, properties, where, parameters);
                return ExecuteAsync(sql, parameters);
            }
            public override TEntity SelectSingle<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> select, object identity)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = SelectSingle(select, identity, parameters);
                return this.Execute<TEntity>(sql, parameters);
            }
            public override Task<TEntity> SelectSingleAsync<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> select, object identity)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = SelectSingle(select, identity, parameters);
                return this.ExecuteAsync<TEntity>(sql, parameters);
            }
            public override TEntity SelectSingle<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = SelectSingle(select, where, parameters);
                return this.Execute<TEntity>(sql, parameters);
            }
            public override Task<TEntity> SelectSingleAsync<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = SelectSingle(select, where, parameters);
                return this.ExecuteAsync<TEntity>(sql, parameters);
            }
            public override List<TEntity> Select<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = Select(select, where, orderBy, parameters);
                return this.Execute<List<TEntity>>(sql, parameters);
            }
            public override Task<List<TEntity>> SelectAsync<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = Select(select, where, orderBy, parameters);
                return this.ExecuteAsync<List<TEntity>>(sql, parameters);
            }
            public override List<TEntity> Select<TEntity>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = Select(offset, fetch, select, where, orderBy, parameters);
                return this.Execute<List<TEntity>>(sql, parameters);
            }
            public override Task<List<TEntity>> SelectAsync<TEntity>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = Select(offset, fetch, select, where, orderBy, parameters);
                return this.ExecuteAsync<List<TEntity>>(sql, parameters);
            }
            public override (List<TEntity>, int) SelectPaged<TEntity>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = SelectPaged(offset, fetch, select, null, where, orderBy, parameters);
                return this.Execute<List<TEntity>, int>(sql, parameters);
            }
            public override Task<(List<TEntity>, int)> SelectPagedAsync<TEntity>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = SelectPaged(offset, fetch, select, null, where, orderBy, parameters);
                return this.ExecuteAsync<List<TEntity>, int>(sql, parameters);
            }
            public override TResult Select<TEntity, TResult>(Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<SqlExpression, object>> from, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Func<TEntity, SqlExpression, object>> groupBy, Expression<Func<TEntity, SqlExpression, bool>> having, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = Select(select, from, where, groupBy, having, orderBy, parameters);
                return this.Execute<TResult>(sql, parameters);
            }
            public override Task<TResult> SelectAsync<TEntity, TResult>(Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<SqlExpression, object>> from, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Func<TEntity, SqlExpression, object>> groupBy, Expression<Func<TEntity, SqlExpression, bool>> having, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = Select(select, from, where, groupBy, having, orderBy, parameters);
                return this.ExecuteAsync<TResult>(sql, parameters);
            }
            public override TResult Select<TEntity, TResult>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<SqlExpression, object>> from, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Func<TEntity, SqlExpression, object>> groupBy, Expression<Func<TEntity, SqlExpression, bool>> having, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = Select(offset, fetch, select, from, where, groupBy, having, orderBy, parameters);
                return this.Execute<TResult>(sql, parameters);
            }
            public override Task<TResult> SelectAsync<TEntity, TResult>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<SqlExpression, object>> from, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Func<TEntity, SqlExpression, object>> groupBy, Expression<Func<TEntity, SqlExpression, bool>> having, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = Select(offset, fetch, select, from, where, groupBy, having, orderBy, parameters);
                return this.ExecuteAsync<TResult>(sql, parameters);
            }
            public override (TResult, int) SelectPaged<TEntity, TResult>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<SqlExpression, object>> from, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = SelectPaged(offset, fetch, select, from, where, orderBy, parameters);
                return this.Execute<TResult, int>(sql, parameters);
            }
            public override Task<(TResult, int)> SelectPagedAsync<TEntity, TResult>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<SqlExpression, object>> from, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = SelectPaged(offset, fetch, select, from, where, orderBy, parameters);
                return this.ExecuteAsync<TResult, int>(sql, parameters);
            }
        }
        #endregion

        #region MySqlDb
        public class MySqlDb : SqlDb
        {
            private static object _Sync = new object();
            private static Type _ConnectionType;
            private static Type _DataReaderType;
            private static Action<DbCommand, string, object> _AddParameter;
            private static Func<MemberExpression, IReadOnlyList<object>> _DbMember;
            private static Func<MethodCallExpression, IReadOnlyList<object>> _DbMethod;
            [ThreadStatic] private static List<string> _CommandText;
            [ThreadStatic] private static List<(string, object)> _Parameters;
            private Func<(DbCommand, IDisposable)> _getCommand;
            private Action<DbCommand> _cmdExecuting;
            public static List<string> CommandText
            {
                get
                {
                    var commandText = _CommandText;
                    if (commandText == null)
                    {
                        commandText = new List<string>();
                        _CommandText = commandText;
                    }
                    else
                    {
                        commandText.Clear();
                    }
                    return commandText;
                }
            }
            public static List<(string, object)> Parameters
            {
                get
                {
                    var parameters = _Parameters;
                    if (parameters == null)
                    {
                        parameters = new List<(string, object)>();
                        _Parameters = parameters;
                    }
                    else
                    {
                        parameters.Clear();
                    }
                    return parameters;
                }
            }
            private static class Query
            {
                private static object _Sync;
                private static string[] _Parameters;
                private static string[] _Tables;
                private static Dictionary<Type, Action<DbCommand, object>> _Handlers;
                static Query()
                {
                    _Sync = new object();
                    _Parameters = new[]
                   {
                        "@p0","@p1","@p2","@p3","@p4","@p5","@p6","@p7",
                        "@p8","@p9","@p10","@p11","@p12","@p13","@p14","@p15",
                        "@p16","@p17","@p18","@p19","@p20","@p21","@p22","@p23",
                        "@p24","@p25","@p26","@p27","@p28","@p29","@p30","@p31",
                        "@p32","@p33","@p34","@p35","@p36","@p37","@p38","@p39",
                        "@p40","@p41","@p42","@p43","@p44","@p45","@p46","@p47",
                        "@p48","@p49","@p50","@p51","@p52","@p53","@p54","@p55",
                        "@p56","@p57","@p58","@p59","@p60","@p61","@p62","@p63"
                    };
                    _Tables = new[]
                    {
                        "t0","t1","t2","t3","t4","t5","t6","t7",
                        "t8","t9","t10","t11","t12","t13","t14","t15"
                    };
                    _Handlers = new Dictionary<Type, Action<DbCommand, object>>();
                }
                public static string GetParameter(int index)
                {
                    return index < _Parameters.Length ? _Parameters[index] : $"@p{index}";
                }
                public static string GetTable(int index)
                {
                    return index < _Tables.Length ? _Tables[index] : $"t{index}";
                }
                public static void AddParameter(DbCommand command, object objParameters)
                {
                    if (objParameters == null)
                        return;

                    var type = objParameters.GetType();
                    if (!_Handlers.TryGetValue(type, out var handler))
                    {
                        lock (_Sync)
                        {
                            if (!_Handlers.TryGetValue(type, out handler))
                            {
                                var cmd = Expression.Parameter(typeof(DbCommand), "cmd");
                                var objValue = Expression.Parameter(typeof(object), "objValue");
                                var tValue = Expression.Variable(type, "value");
                                var addParameter = typeof(SqlDbExtensions).GetMethod("AddParameter").MakeGenericMethod(_ConnectionType);
                                var properties = type.GetGeneralProperties(BindingFlags.Instance | BindingFlags.Public);
                                var exprs = new List<Expression>();
                                exprs.Add(Expression.Assign(tValue, Expression.Convert(objValue, type)));
                                foreach (var property in properties)
                                {
                                    if (!property.CanRead)
                                        continue;
                                    exprs.Add(Expression.Call(
                                        addParameter, cmd,
                                        Expression.Constant($"@{property.Name}"),
                                        Expression.Convert(Expression.Property(tValue, property), typeof(object))
                                        ));
                                }
                                handler = Expression.Lambda<Action<DbCommand, object>>(Expression.Block(new[] { tValue }, exprs), cmd, objValue).Compile();
                                var handlers = new Dictionary<Type, Action<DbCommand, object>>(_Handlers);
                                handlers.Add(type, handler);
                                _Handlers = handlers;
                            }
                        }
                    }
                    handler(command, objParameters);
                }
            }
            private static class Reader<TEntity>
            {
                public static Func<DbDataReader, TEntity> Handler;
            }
            public class DbTable
            {
                private class Comparer : IEqualityComparer<PropertyInfo>
                {
                    public bool Equals(PropertyInfo x, PropertyInfo y) =>
                        x.MetadataToken == y.MetadataToken && x.DeclaringType == y.DeclaringType;
                    public int GetHashCode(PropertyInfo obj) => obj.MetadataToken;
                }

                public string Name;
                public string Identity;
                public string Join;
                public Dictionary<PropertyInfo, string> Fields;
                public Dictionary<PropertyInfo, string> Columns;
                public Dictionary<PropertyInfo, DbTable> Tables;

                private static int _MaxDepth = 8;//TODO??_TypeReference
                private static Comparer _Comparer = new Comparer();
                private static object _Sync = new object();
                private static Dictionary<Type, DbTable> _Tables = new Dictionary<Type, DbTable>();
                public static DbTable Get(Type type)
                {
                    if (!_Tables.TryGetValue(type, out var table))
                    {
                        lock (_Sync)
                        {
                            if (!_Tables.TryGetValue(type, out table))
                            {
                                table = Get(0, null, null, type);
                            }
                        }
                    }
                    return table;
                }
                private static DbTable Get(int depth, string prefix, string join, Type type)
                {
                    var table = new DbTable();
                    table.Join = join;
                    table.Fields = new Dictionary<PropertyInfo, string>(_Comparer);
                    table.Columns = new Dictionary<PropertyInfo, string>(_Comparer);
                    table.Tables = new Dictionary<PropertyInfo, DbTable>(_Comparer);
                    var registerReader = typeof(SqlDbExtensions).GetMethod("RegisterDbReader", new[] { typeof(Type), typeof(ParameterExpression), typeof(ParameterExpression), typeof(Expression).MakeByRefType() })
                    .MakeGenericMethod(_DataReaderType);
                    var readerArgs = new object[] { null, Expression.Parameter(_DataReaderType), Expression.Parameter(typeof(int)), null };
                    SqlDbExtensions.RegisterTable(out var tableResolver);
                    SqlDbExtensions.RegisterIdentity(out var identityResolver);
                    SqlDbExtensions.RegisterProperty(out var propertyResolver);

                    var dataTableAttribute = type.GetCustomAttribute<DataTableAttribute>();
                    table.Name = $"`{dataTableAttribute?.Name ?? tableResolver(type)}`";
                    var identityProperty = identityResolver(type);
                    if (identityProperty != null)
                    {
                        var dataColumnAttribute = identityProperty.GetCustomAttribute<DataColumnAttribute>();
                        table.Identity = $"`{dataColumnAttribute?.Name ?? propertyResolver(identityProperty)}`";
                    }
                    var properties = type.GetGeneralProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    foreach (var property in properties)
                    {
                        if (!property.CanWrite)
                            continue;
                        if (property.IsDefined(typeof(IgnoreDataColumnAttribute)))
                            continue;
                        var dataColumnAttribute = property.GetCustomAttribute<DataColumnAttribute>();
                        readerArgs[0] = property.PropertyType;
                        registerReader.Invoke(null, readerArgs);
                        if (readerArgs[3] != null)
                        {
                            string columnName = dataColumnAttribute?.Name ?? propertyResolver(property);
                            table.Fields.Add(property, $"`{columnName}`");
                            table.Columns.Add(property, $"`{prefix}{columnName}`");
                        }
                        else
                        {
                            if (depth > _MaxDepth)
                                continue;
                            var joinIdentityProperty = identityResolver(property.PropertyType);
                            if (joinIdentityProperty == null)
                                continue;
                            var joinName = dataColumnAttribute?.Name;
                            if (joinName == null)
                            {
                                var onPropery = type.GetProperty($"{property.Name}{joinIdentityProperty.Name}", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                if (onPropery == null)
                                {
                                    if (joinIdentityProperty.Name == $"{type.Name}{identityProperty.Name}")
                                    {
                                        onPropery = identityProperty;
                                    }
                                    else
                                    {
                                        continue;
                                    }
                                }
                                dataColumnAttribute = onPropery.GetCustomAttribute<DataColumnAttribute>();
                                joinName = dataColumnAttribute?.Name ?? propertyResolver(onPropery);
                            }
                            joinName = $"`{joinName}`";
                            string columnName = propertyResolver(property);
                            var propertyPrefix = $"{prefix}{columnName}.";
                            var propertyTable = Get(depth + 1, propertyPrefix, joinName, property.PropertyType);
                            table.Tables.Add(property, propertyTable);
                        }
                    }
                    return table;
                }
            }
            public class DbEntity
            {
                public string Name;
                public DbTable Table;
                public Dictionary<PropertyInfo, DbEntity> Entities;
            }
            public class DbEntityContext
            {
                private int _table;
                private Dictionary<ParameterExpression, DbEntity> _entities;
                public DbEntityContext()
                {
                    _entities = new Dictionary<ParameterExpression, DbEntity>();
                }
                public DbEntity Add(ParameterExpression parameter)
                {
                    var entity = new DbEntity()
                    {
                        Name = Query.GetTable(_table++),
                        Table = DbTable.Get(parameter.Type),
                        Entities = new Dictionary<PropertyInfo, DbEntity>()
                    };
                    _entities.Add(parameter, entity);
                    return entity;
                }
                public void Add(ParameterExpression parameter, DbEntity entity)
                {
                    _entities.Add(parameter, entity);
                }
                public DbEntity Add(DbEntity entity, PropertyInfo property)
                {
                    if (entity.Entities.TryGetValue(property, out var propertyEntity))
                    {
                        return propertyEntity;
                    }
                    else if (entity.Table.Tables.TryGetValue(property, out var entityTable))
                    {
                        propertyEntity = new DbEntity();
                        propertyEntity.Name = Query.GetTable(_table++);
                        propertyEntity.Table = entityTable;
                        propertyEntity.Entities = new Dictionary<PropertyInfo, DbEntity>();
                        entity.Entities.Add(property, propertyEntity);
                        return propertyEntity;
                    }
                    return null;
                }
                public DbEntity Convert(Expression expression)
                {
                    if (expression == null)
                        return null;

                    switch (expression.NodeType)
                    {
                        case ExpressionType.Convert:
                            return Convert(((UnaryExpression)expression).Operand);
                        case ExpressionType.Parameter:
                            {
                                _entities.TryGetValue((ParameterExpression)expression, out var entity);
                                return entity;
                            }
                        case ExpressionType.MemberAccess:
                            {
                                var expr = (MemberExpression)expression;
                                if (expr.Member is PropertyInfo property)
                                {
                                    var propertyEntity = Convert(expr.Expression);
                                    if (propertyEntity != null)
                                    {
                                        if (propertyEntity.Entities.TryGetValue(property, out var entity))
                                        {
                                            return entity;
                                        }
                                        else if (propertyEntity.Table.Tables.TryGetValue(property, out var propertyTable))
                                        {
                                            entity = new DbEntity();
                                            entity.Name = Query.GetTable(_table++);
                                            entity.Table = propertyTable;
                                            entity.Entities = new Dictionary<PropertyInfo, DbEntity>();
                                            propertyEntity.Entities.Add(property, entity);
                                            return entity;
                                        }
                                    }
                                }
                            }
                            return null;
                        default:
                            return null;
                    }
                }
            }
            public static Dictionary<PropertyInfo, string> GetFields(DbTable table, Expression expression)
            {
                switch (expression.NodeType)
                {
                    case ExpressionType.Parameter:
                        return table.Fields;
                    case ExpressionType.Convert:
                        expression = ((UnaryExpression)expression).Operand;
                        if (expression.NodeType == ExpressionType.MemberAccess)
                            goto case ExpressionType.MemberAccess;
                        break;
                    case ExpressionType.MemberAccess:
                        {
                            var expr = (MemberExpression)expression;
                            if (expr.Member is PropertyInfo property
                                && table.Fields.TryGetValue(property, out var field))
                            {
                                return new Dictionary<PropertyInfo, string>()
                                {
                                    { property,field}
                                };
                            }
                        }
                        break;
                    case ExpressionType.New:
                        {
                            var fields = new Dictionary<PropertyInfo, string>();
                            var exprArgs = ((NewExpression)expression).Arguments;
                            for (int i = 0; i < exprArgs.Count; i++)
                            {
                                var expr = exprArgs[i].NodeType == ExpressionType.Convert ?
                                   ((UnaryExpression)exprArgs[i]).Operand as MemberExpression
                                   : exprArgs[i] as MemberExpression;
                                if (expr != null && expr.Member is PropertyInfo property
                                    && table.Fields.TryGetValue(property, out var field))
                                {
                                    fields.Add(property, field);
                                }
                            }
                            return fields;
                        }
                    case ExpressionType.Call:
                        {
                            if (SqlDbExtensions.TryExcept((MethodCallExpression)expression, out var except))
                            {
                                switch (except.NodeType)
                                {
                                    case ExpressionType.Convert:
                                        except = ((UnaryExpression)except).Operand;
                                        if (except.NodeType == ExpressionType.MemberAccess)
                                            goto case ExpressionType.MemberAccess;
                                        break;
                                    case ExpressionType.MemberAccess:
                                        {
                                            var expr = (MemberExpression)except;
                                            if (expr.Member is PropertyInfo property && table.Fields.ContainsKey(property))
                                            {
                                                var fields = new Dictionary<PropertyInfo, string>(table.Fields);
                                                fields.Remove(property);
                                                return fields;
                                            }
                                        }
                                        break;
                                    case ExpressionType.New:
                                        {
                                            var fields = new Dictionary<PropertyInfo, string>(table.Fields);
                                            var exprArgs = ((NewExpression)except).Arguments;
                                            for (int i = 0; i < exprArgs.Count; i++)
                                            {
                                                var expr = exprArgs[i].NodeType == ExpressionType.Convert ?
                                                   ((UnaryExpression)exprArgs[i]).Operand as MemberExpression
                                                   : exprArgs[i] as MemberExpression;
                                                if (expr != null && expr.Member is PropertyInfo property && fields.ContainsKey(property))
                                                {
                                                    fields.Remove(property);
                                                }
                                            }
                                            return fields;
                                        }
                                    default:
                                        break;
                                }
                                return new Dictionary<PropertyInfo, string>(table.Fields);
                            }
                        }
                        break;
                    default:
                        break;
                }
                return new Dictionary<PropertyInfo, string>();
            }
            public static void Join(DbEntity entity, DbEntity joinEntity, List<string> sql)
            {
                sql.Add(" LEFT JOIN ");
                sql.Add(joinEntity.Table.Name);
                sql.Add(" ");
                sql.Add(joinEntity.Name);
                sql.Add(" ON ");
                sql.Add(joinEntity.Name);
                sql.Add(".");
                sql.Add(joinEntity.Table.Identity);
                sql.Add("=");
                sql.Add(entity.Name);
                sql.Add(".");
                sql.Add(joinEntity.Table.Join);
                foreach (var item in joinEntity.Entities)
                {
                    Join(joinEntity, item.Value, sql);
                }
            }
            public static void Convert(Expression expression, List<string> sql, List<(string, object)> parameters, DbEntityContext context)
            {
                switch (expression.NodeType)
                {
                    case ExpressionType.Parameter:
                        {
                            var entity = context.Convert(expression);
                            if (entity != null)
                            {
                                foreach ((_, var field) in entity.Table.Fields)
                                {
                                    sql.Add(entity.Name);
                                    sql.Add(".");
                                    sql.Add(field);
                                    sql.Add(",");
                                }
                                sql.RemoveAt(sql.Count - 1);
                            }
                        }
                        break;
                    case ExpressionType.Constant:
                        {
                            var value = ((ConstantExpression)expression).Value;
                            var parameter = Query.GetParameter(parameters.Count);
                            sql.Add(parameter);
                            parameters.Add((parameter, value));
                        }
                        break;
                    case ExpressionType.MemberAccess:
                        {
                            var expr = (MemberExpression)expression;
                            var exprObjs = _DbMember(expr);
                            if (exprObjs != null)
                            {
                                for (int i = 0; i < exprObjs.Count; i++)
                                {
                                    var exprObj = exprObjs[i] as Expression;
                                    if (exprObj != null)
                                        Convert(exprObj, sql, parameters, context);
                                    else
                                        sql.Add((string)exprObjs[i]);
                                }
                                break;
                            }
                            if (expr.Member is PropertyInfo property)
                            {
                                var entity = context.Convert(expr.Expression);
                                if (entity != null)
                                {
                                    if (entity.Table.Fields.TryGetValue(property, out var field))
                                    {
                                        sql.Add(entity.Name);
                                        sql.Add(".");
                                        sql.Add(field);
                                    }
                                    else
                                    {
                                        var propertyEntity = context.Add(entity, property);
                                        if (propertyEntity != null)
                                        {
                                            foreach ((_, var propertyField) in propertyEntity.Table.Fields)
                                            {
                                                sql.Add(propertyEntity.Name);
                                                sql.Add(".");
                                                sql.Add(propertyField);
                                                sql.Add(",");
                                            }
                                            sql.RemoveAt(sql.Count - 1);
                                        }
                                    }
                                    break;
                                }
                            }
                            var value = expr.Invoke();
                            var parameter = Query.GetParameter(parameters.Count);
                            sql.Add(parameter);
                            parameters.Add((parameter, value));
                        }
                        break;
                    case ExpressionType.Call:
                        {
                            var expr = (MethodCallExpression)expression;
                            var exprObjs = _DbMethod(expr);
                            if (exprObjs != null)
                            {
                                for (int i = 0; i < exprObjs.Count; i++)
                                {
                                    var exprObj = exprObjs[i] as Expression;
                                    if (exprObj != null)
                                        Convert(exprObj, sql, parameters, context);
                                    else
                                        sql.Add((string)exprObjs[i]);
                                }
                            }
                            else if (SqlDbExtensions.TrySelect(expr, out var objSelect))
                            {
                                Select(objSelect, sql, parameters, context);
                            }
                            else if (SqlDbExtensions.TrySelect(expr, out var select, out var where, out var groupBy, out var having, out var orderBy))
                            {
                                sql.Add("(");
                                sql.Add(Select(select, where, groupBy, having, orderBy, parameters, context));
                                sql.Add(")");
                            }
                            else if (SqlDbExtensions.TrySelect(expr, out var offset, out var fetch, out select, out where, out groupBy, out having, out orderBy))
                            {
                                sql.Add("(");
                                sql.Add(Select(offset, fetch, select, where, groupBy, having, orderBy, parameters, context));
                                sql.Add(")");
                            }
                            else if (SqlDbExtensions.TryNavigate(expr, out var navigate))
                            {
                                var entity = context.Convert(navigate);
                                if (entity == null)
                                {
                                    Convert(navigate, sql, parameters, context);
                                }
                                else
                                {
                                    Navigate(entity, sql, context);
                                    sql.RemoveAt(sql.Count - 1);
                                }
                            }
                            else
                            {
                                var value = expression.Invoke();
                                var parameter = Query.GetParameter(parameters.Count);
                                sql.Add(parameter);
                                parameters.Add((parameter, value));
                            }
                        }
                        break;
                    case ExpressionType.NewArrayInit:
                        {
                            var argExprs = ((NewArrayExpression)expression).Expressions;
                            Convert(argExprs[0], sql, parameters, context);
                            for (int i = 1; i < argExprs.Count; i++)
                            {
                                sql.Add(",");
                                Convert(argExprs[i], sql, parameters, context);
                            }
                        }
                        break;
                    case ExpressionType.Convert:
                        Convert(((UnaryExpression)expression).Operand, sql, parameters, context);
                        break;
                    case ExpressionType.Not:
                        sql.Add("NOT ");
                        Convert(((UnaryExpression)expression).Operand, sql, parameters, context);
                        break;
                    case ExpressionType.Equal:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" = ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.NotEqual:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" <> ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.GreaterThan:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" > ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.GreaterThanOrEqual:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" >= ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.LessThan:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" < ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.LessThanOrEqual:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" <= ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.AndAlso:
                        sql.Add("(");
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" AND ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        sql.Add(")");
                        break;
                    case ExpressionType.OrElse:
                        sql.Add("(");
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" OR ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        sql.Add(")");
                        break;
                    case ExpressionType.And:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" & ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.Or:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" | ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.Add:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" + ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.Subtract:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" - ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.Multiply:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" * ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.Divide:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" / ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.Modulo:
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(" % ");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        break;
                    case ExpressionType.Coalesce:
                        sql.Add("IFNULL(");
                        Convert(((BinaryExpression)expression).Left, sql, parameters, context);
                        sql.Add(",");
                        Convert(((BinaryExpression)expression).Right, sql, parameters, context);
                        sql.Add(")");
                        break;
                    case ExpressionType.Conditional:
                        sql.Add("CASE WHEN ");
                        Convert(((ConditionalExpression)expression).Test, sql, parameters, context);
                        sql.Add(" THEN ");
                        Convert(((ConditionalExpression)expression).IfTrue, sql, parameters, context);
                        sql.Add(" ELSE ");
                        Convert(((ConditionalExpression)expression).IfFalse, sql, parameters, context);
                        sql.Add(" END");
                        break;
                    default:
                        throw new NotSupportedException(expression.ToString());
                }
            }
            public static void Navigate(DbEntity entity, List<string> sql, DbEntityContext context)
            {
                foreach ((_, var field) in entity.Table.Fields)
                {
                    sql.Add(entity.Name);
                    sql.Add(".");
                    sql.Add(field);
                    sql.Add(",");
                }
                foreach ((var property, _) in entity.Table.Tables)
                {
                    var propertyEntity = context.Add(entity, property);
                    if (propertyEntity != null)
                        Navigate(propertyEntity, sql, context);
                }
            }
            public static void Navigate(DbTable table, DbEntity entity, List<string> sql, DbEntityContext context)
            {
                foreach ((var property, var field) in entity.Table.Fields)
                {
                    sql.Add(entity.Name);
                    sql.Add(".");
                    sql.Add(field);
                    sql.Add(" AS ");
                    sql.Add(entity.Table.Columns[property]);
                    sql.Add(",");
                }
                foreach ((var property, var propertyTable) in table.Tables)
                {
                    var propertyEntity = context.Add(entity, property);
                    if (propertyEntity != null)
                        Navigate(propertyTable, propertyEntity, sql, context);
                }
            }
            public static void Select(DbTable table, PropertyInfo member, Expression expression, List<string> sql, List<(string, object)> parameters, DbEntityContext context)
            {
                if (table.Columns.TryGetValue(member, out var column))
                {
                    Convert(expression, sql, parameters, context);
                    sql.Add(" AS ");
                    sql.Add(column);
                    sql.Add(",");
                }
                else if (table.Tables.TryGetValue(member, out var memberTable))
                {
                    switch (expression.NodeType)
                    {
                        case ExpressionType.MemberInit:
                            {
                                var expr = (MemberInitExpression)expression;
                                var bindings = expr.Bindings;
                                for (int i = 0; i < bindings.Count; i++)
                                {
                                    var memberAssignment = (MemberAssignment)bindings[i];
                                    if (memberAssignment.Member is PropertyInfo memberMember)
                                        Select(memberTable, memberMember, memberAssignment.Expression, sql, parameters, context);
                                }
                            }
                            break;
                        case ExpressionType.Call:
                            {
                                var expr = (MethodCallExpression)expression;
                                if (SqlDbExtensions.TryNavigate(expr, out var navigate))
                                {
                                    var entity = context.Convert(navigate);
                                    if (entity != null)
                                    {
                                        Navigate(memberTable, entity, sql, context);
                                        break;
                                    }
                                }
                            }
                            break;
                        default:
                            {
                                var entity = context.Convert(expression);
                                if (entity != null)
                                {
                                    foreach ((var property, var field) in entity.Table.Fields)
                                    {
                                        sql.Add(entity.Name);
                                        sql.Add(".");
                                        sql.Add(field);
                                        sql.Add(" AS ");
                                        sql.Add(memberTable.Columns[property]);
                                        sql.Add(",");
                                    }
                                }
                            }
                            break;
                    }

                }
            }
            public static void Select(Expression expression, List<string> sql, List<(string, object)> parameters, DbEntityContext context)
            {
                switch (expression.NodeType)
                {
                    case ExpressionType.Convert:
                        Select(((UnaryExpression)expression).Operand, sql, parameters, context);
                        break;
                    case ExpressionType.MemberInit:
                        {
                            var expr = (MemberInitExpression)expression;
                            var table = DbTable.Get(expr.Type);
                            var bindings = expr.Bindings;
                            for (int i = 0; i < bindings.Count; i++)
                            {
                                var memberAssignment = (MemberAssignment)bindings[i];
                                if (memberAssignment.Member is PropertyInfo member)
                                    Select(table, member, memberAssignment.Expression, sql, parameters, context);
                            }
                            sql.RemoveAt(sql.Count - 1);
                        }
                        break;
                    case ExpressionType.NewArrayInit:
                        {
                            var argExprs = ((NewArrayExpression)expression).Expressions;
                            Convert(argExprs[0], sql, parameters, context);
                            for (int i = 1; i < argExprs.Count; i++)
                            {
                                sql.Add(",");
                                Select(argExprs[i], sql, parameters, context);
                            }
                        }
                        break;
                    case ExpressionType.Call:
                        {
                            var expr = (MethodCallExpression)expression;
                            if (SqlDbExtensions.TryNavigate(expr, out var navigate))
                            {
                                var entity = context.Convert(navigate);
                                if (entity == null)
                                {
                                    Convert(navigate, sql, parameters, context);
                                }
                                else
                                {
                                    Navigate(entity.Table, entity, sql, context);
                                    sql.RemoveAt(sql.Count - 1);
                                }
                            }
                            else
                            {
                                Convert(expression, sql, parameters, context);
                            }
                        }
                        break;
                    default:
                        {
                            var entity = context.Convert(expression);
                            if (entity == null)
                            {
                                Convert(expression, sql, parameters, context);
                            }
                            else
                            {
                                foreach ((var property, var field) in entity.Table.Fields)
                                {
                                    sql.Add(entity.Name);
                                    sql.Add(".");
                                    sql.Add(field);
                                    sql.Add(" AS ");
                                    sql.Add(entity.Table.Columns[property]);
                                    sql.Add(",");
                                }
                                sql.RemoveAt(sql.Count - 1);
                            }
                        }
                        break;
                }
            }
            public static string Select(LambdaExpression select, LambdaExpression where, LambdaExpression groupBy, LambdaExpression having, LambdaExpression orderBy, List<(string, object)> parameters, DbEntityContext context)
            {
                var sql = new List<string>();
                var entity = context.Add(select.Parameters[0]);
                if (where != null)
                {
                    context.Add(where.Parameters[0], entity);
                    sql.Add(" WHERE ");
                    Convert(where.Body, sql, parameters, context);
                }
                if (groupBy != null)
                {
                    context.Add(groupBy.Parameters[0], entity);
                    sql.Add(" GROUP BY ");
                    Convert(groupBy.Body, sql, parameters, context);
                }
                if (having != null)
                {
                    context.Add(having.Parameters[0], entity);
                    sql.Add(" HAVING ");
                    Convert(having.Body, sql, parameters, context);
                }
                if (orderBy != null)
                {
                    context.Add(orderBy.Parameters[0], entity);
                    sql.Add(" ORDER BY ");
                    Convert(orderBy.Body, sql, parameters, context);
                }
                var index = sql.Count;
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);
                //Convert(select.Body, sql, parameters, context);

                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }
                return sql.Concat((index, sql.Count), (0, index));
            }
            public static string Select(int offset, int fetch, LambdaExpression select, LambdaExpression where, LambdaExpression groupBy, LambdaExpression having, LambdaExpression orderBy, List<(string, object)> parameters, DbEntityContext context)
            {
                var sql = new List<string>();
                var entity = context.Add(select.Parameters[0]);
                if (where != null)
                {
                    context.Add(where.Parameters[0], entity);
                    sql.Add(" WHERE ");
                    Convert(where.Body, sql, parameters, context);
                }
                if (groupBy != null)
                {
                    context.Add(groupBy.Parameters[0], entity);
                    sql.Add(" GROUP BY ");
                    Convert(groupBy.Body, sql, parameters, context);
                }
                if (having != null)
                {
                    context.Add(having.Parameters[0], entity);
                    sql.Add(" HAVING ");
                    Convert(having.Body, sql, parameters, context);
                }
                if (orderBy != null)
                {
                    context.Add(orderBy.Parameters[0], entity);
                    sql.Add(" ORDER BY ");
                    Convert(orderBy.Body, sql, parameters, context);
                }
                sql.Add($" LIMIT {offset},{fetch}");
                var index = sql.Count;
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);
                //Convert(select.Body, sql, parameters, context);

                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }
                return sql.Concat((index, sql.Count), (0, index));
            }
            public static string Insert(LambdaExpression entity, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var context = new DbEntityContext();
                var table = DbTable.Get(entity.Body.Type);
                sql.Add("INSERT INTO ");
                sql.Add(table.Name);
                sql.Add("(");
                if (entity.Body.NodeType == ExpressionType.MemberInit)
                {
                    var bindings = ((MemberInitExpression)entity.Body).Bindings;
                    Span<int> indexs = bindings.Count < 256 ? stackalloc int[bindings.Count] : new int[bindings.Count];
                    var index = 0;
                    //fields
                    {
                        var i = 0;
                        for (; i < bindings.Count; i++)
                        {
                            var memberAssignment = (MemberAssignment)bindings[i];
                            if (table.Fields.TryGetValue((PropertyInfo)memberAssignment.Member, out var name))
                            {
                                sql.Add(name);
                                indexs[index++] = i++;
                                break;
                            }
                        }
                        for (; i < bindings.Count; i++)
                        {
                            var memberAssignment = (MemberAssignment)bindings[i];
                            if (table.Fields.TryGetValue((PropertyInfo)memberAssignment.Member, out var name))
                            {
                                sql.Add(",");
                                sql.Add(name);
                                indexs[index++] = i;
                            }
                        }
                    }
                    sql.Add(") VALUES (");
                    //values
                    {
                        if (index > 0)
                        {
                            Convert(((MemberAssignment)bindings[indexs[0]]).Expression, sql, parameters, context);
                        }
                        for (int i = 1; i < index; i++)
                        {
                            sql.Add(",");
                            Convert(((MemberAssignment)bindings[indexs[i]]).Expression, sql, parameters, context);
                        }
                    }
                }
                sql.Add(")");
                return sql.Concat();
            }
            public static string Insert(object entity, LambdaExpression properties, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var table = DbTable.Get(properties.Parameters[0].Type);
                var fields = GetFields(table, properties.Body);
                sql.Add("INSERT INTO ");
                sql.Add(table.Name);
                sql.Add("(");
                var enumerator = fields.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    sql.Add(enumerator.Current.Value);
                }
                while (enumerator.MoveNext())
                {
                    sql.Add(",");
                    sql.Add(enumerator.Current.Value);
                }
                sql.Add(") VALUES (");
                enumerator = fields.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    var value = enumerator.Current.Key.GetValue(entity);
                    var parameter = Query.GetParameter(parameters.Count);
                    parameters.Add((parameter, value));
                    sql.Add(parameter);
                }
                while (enumerator.MoveNext())
                {
                    sql.Add(",");
                    var value = enumerator.Current.Key.GetValue(entity);
                    var parameter = Query.GetParameter(parameters.Count);
                    parameters.Add((parameter, value));
                    sql.Add(parameter);
                }
                sql.Add(")");
                return sql.Concat();
            }
            public static string InsertIdentity(LambdaExpression entity, List<(string, object)> parameters)
            {
                var sql = Insert(entity, parameters);
                return sql + ";SELECT LAST_INSERT_ID()";
            }
            public static string InsertIdentity(object entity, LambdaExpression properties, List<(string, object)> parameters)
            {
                var sql = Insert(entity, properties, parameters);
                return sql + ";SELECT LAST_INSERT_ID()";
            }
            public static string Delete(DbTable table, object identity, List<(string, object)> parameters)
            {
                var sql = CommandText;
                sql.Add("DELETE FROM ");
                sql.Add(table.Name);
                sql.Add(" WHERE ");
                sql.Add(table.Name);
                sql.Add(".");
                sql.Add(table.Identity);
                if (identity == null)
                {
                    sql.Add(" IS NULL");
                }
                else
                {
                    var parameter = Query.GetParameter(parameters.Count);
                    sql.Add(" = ");
                    sql.Add(parameter);
                    parameters.Add((parameter, identity));
                }
                return sql.Concat();
            }
            public static string Delete(DbTable table, LambdaExpression where, List<(string, object)> parameters)
            {
                var sql = CommandText;
                sql.Add("DELETE FROM ");
                sql.Add(table.Name);
                sql.Add(" WHERE ");
                var context = new DbEntityContext();
                context.Add(where.Parameters[0], new DbEntity()
                {
                    Name = table.Name,
                    Table = table,
                    Entities = new Dictionary<PropertyInfo, DbEntity>()
                });
                Convert(where.Body, sql, parameters, context);
                return sql.Concat();
            }
            public static string Update(LambdaExpression entity, object identity, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var table = DbTable.Get(entity.Body.Type);
                var context = new DbEntityContext();
                context.Add(entity.Parameters[0], new DbEntity()
                {
                    Name = table.Name,
                    Table = table,
                    Entities = new Dictionary<PropertyInfo, DbEntity>()
                });
                sql.Add("UPDATE ");
                sql.Add(table.Name);
                sql.Add(" SET ");
                if (entity.Body.NodeType == ExpressionType.MemberInit)
                {
                    var bindings = ((MemberInitExpression)entity.Body).Bindings;
                    var i = 0;
                    for (; i < bindings.Count;)
                    {
                        var memberAssignment = (MemberAssignment)bindings[i++];
                        if (table.Fields.TryGetValue((PropertyInfo)memberAssignment.Member, out var name))
                        {
                            sql.Add(name);
                            sql.Add("=");
                            Convert(memberAssignment.Expression, sql, parameters, context);
                            break;
                        }
                    }
                    for (; i < bindings.Count;)
                    {
                        var memberAssignment = (MemberAssignment)bindings[i++];
                        if (table.Fields.TryGetValue((PropertyInfo)memberAssignment.Member, out var name))
                        {
                            sql.Add(",");
                            sql.Add(name);
                            sql.Add("=");
                            Convert(memberAssignment.Expression, sql, parameters, context);
                        }
                    }
                }
                sql.Add(" WHERE ");
                sql.Add(table.Name);
                sql.Add(".");
                sql.Add(table.Identity);
                if (identity == null)
                {
                    sql.Add(" IS NULL");
                }
                else
                {
                    var parameter = Query.GetParameter(parameters.Count);
                    sql.Add(" = ");
                    sql.Add(parameter);
                    parameters.Add((parameter, identity));
                }
                return sql.Concat();
            }
            public static string Update(LambdaExpression entity, LambdaExpression where, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var table = DbTable.Get(entity.Body.Type);
                var context = new DbEntityContext();
                var dbEntity = new DbEntity()
                {
                    Name = table.Name,
                    Table = table,
                    Entities = new Dictionary<PropertyInfo, DbEntity>()
                };
                context.Add(entity.Parameters[0], dbEntity);
                sql.Add("UPDATE ");
                sql.Add(table.Name);
                sql.Add(" SET ");
                if (entity.Body.NodeType == ExpressionType.MemberInit)
                {
                    var bindings = ((MemberInitExpression)entity.Body).Bindings;
                    var i = 0;
                    for (; i < bindings.Count;)
                    {
                        var memberAssignment = (MemberAssignment)bindings[i++];
                        if (table.Fields.TryGetValue((PropertyInfo)memberAssignment.Member, out var name))
                        {
                            sql.Add(name);
                            sql.Add("=");
                            Convert(memberAssignment.Expression, sql, parameters, context);
                            break;
                        }
                    }
                    for (; i < bindings.Count;)
                    {
                        var memberAssignment = (MemberAssignment)bindings[i++];
                        if (table.Fields.TryGetValue((PropertyInfo)memberAssignment.Member, out var name))
                        {
                            sql.Add(",");
                            sql.Add(name);
                            sql.Add("=");
                            Convert(memberAssignment.Expression, sql, parameters, context);
                        }
                    }
                }
                sql.Add(" WHERE ");
                context.Add(where.Parameters[0], dbEntity);
                Convert(where.Body, sql, parameters, context);
                return sql.Concat();
            }
            public static string Update(object entity, LambdaExpression properties, object identity, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var table = DbTable.Get(properties.Parameters[0].Type);
                var fields = GetFields(table, properties.Body);
                sql.Add("UPDATE ");
                sql.Add(table.Name);
                sql.Add(" SET ");
                var enumerator = fields.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    var value = enumerator.Current.Key.GetValue(entity);
                    var parameter = Query.GetParameter(parameters.Count);
                    parameters.Add((parameter, value));
                    sql.Add(enumerator.Current.Value);
                    sql.Add("=");
                    sql.Add(parameter);
                }
                while (enumerator.MoveNext())
                {
                    sql.Add(",");
                    var value = enumerator.Current.Key.GetValue(entity);
                    var parameter = Query.GetParameter(parameters.Count);
                    parameters.Add((parameter, value));
                    sql.Add(enumerator.Current.Value);
                    sql.Add("=");
                    sql.Add(parameter);
                }
                sql.Add(" WHERE ");
                sql.Add(table.Name);
                sql.Add(".");
                sql.Add(table.Identity);
                if (identity == null)
                {
                    sql.Add(" IS NULL");
                }
                else
                {
                    var parameter = Query.GetParameter(parameters.Count);
                    sql.Add(" = ");
                    sql.Add(parameter);
                    parameters.Add((parameter, identity));
                }
                return sql.Concat();
            }
            public static string Update(object entity, LambdaExpression properties, LambdaExpression where, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var table = DbTable.Get(properties.Parameters[0].Type);
                var fields = GetFields(table, properties.Body);
                sql.Add("UPDATE ");
                sql.Add(table.Name);
                sql.Add(" SET ");
                var enumerator = fields.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    var value = enumerator.Current.Key.GetValue(entity);
                    var parameter = Query.GetParameter(parameters.Count);
                    parameters.Add((parameter, value));
                    sql.Add(enumerator.Current.Value);
                    sql.Add("=");
                    sql.Add(parameter);
                }
                while (enumerator.MoveNext())
                {
                    sql.Add(",");
                    var value = enumerator.Current.Key.GetValue(entity);
                    var parameter = Query.GetParameter(parameters.Count);
                    parameters.Add((parameter, value));
                    sql.Add(enumerator.Current.Value);
                    sql.Add("=");
                    sql.Add(parameter);
                }
                sql.Add(" WHERE ");
                var context = new DbEntityContext();
                context.Add(where.Parameters[0], new DbEntity()
                {
                    Name = table.Name,
                    Table = table,
                    Entities = new Dictionary<PropertyInfo, DbEntity>()
                });
                Convert(where.Body, sql, parameters, context);
                return sql.Concat();
            }
            public static string SelectSingle(LambdaExpression select, object identity, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var context = new DbEntityContext();
                var entity = context.Add(select.Parameters[0]);
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);

                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }
                sql.Add(" WHERE ");
                sql.Add(entity.Name);
                sql.Add(".");
                sql.Add(entity.Table.Identity);
                if (identity == null)
                {
                    sql.Add(" IS NULL");
                }
                else
                {
                    var parameter = Query.GetParameter(parameters.Count);
                    sql.Add(" = ");
                    sql.Add(parameter);
                    parameters.Add((parameter, identity));
                }
                return sql.Concat();
            }
            public static string SelectSingle(LambdaExpression select, LambdaExpression where, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var context = new DbEntityContext();
                var entity = context.Add(select.Parameters[0]);
                if (where != null)
                {
                    context.Add(where.Parameters[0], entity);
                    sql.Add(" WHERE ");
                    Convert(where.Body, sql, parameters, context);
                }
                var index = sql.Count;
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);

                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }

                return sql.Concat((index, sql.Count), (0, index));
            }
            public static string Select(LambdaExpression select, LambdaExpression where, LambdaExpression orderBy, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var context = new DbEntityContext();
                var entity = context.Add(select.Parameters[0]);
                if (where != null)
                {
                    context.Add(where.Parameters[0], entity);
                    sql.Add(" WHERE ");
                    Convert(where.Body, sql, parameters, context);
                }
                if (orderBy != null)
                {
                    context.Add(orderBy.Parameters[0], entity);
                    sql.Add(" ORDER BY ");
                    Convert(orderBy.Body, sql, parameters, context);
                }
                var index = sql.Count;
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);

                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }
                return sql.Concat((index, sql.Count), (0, index));
            }
            public static string Select(int offset, int fetch, LambdaExpression select, LambdaExpression where, LambdaExpression orderBy, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var context = new DbEntityContext();
                var entity = context.Add(select.Parameters[0]);
                if (where != null)
                {
                    context.Add(where.Parameters[0], entity);
                    sql.Add(" WHERE ");
                    Convert(where.Body, sql, parameters, context);
                }
                if (orderBy != null)
                {
                    context.Add(orderBy.Parameters[0], entity);
                    sql.Add(" ORDER BY ");
                    Convert(orderBy.Body, sql, parameters, context);
                }
                sql.Add($" LIMIT {offset},{fetch}");
                var index = sql.Count;
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);

                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }
                return sql.Concat((index, sql.Count), (0, index));
            }
            public static string SelectPaged(int offset, int fetch, LambdaExpression select, LambdaExpression from, LambdaExpression where, LambdaExpression orderBy, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var context = new DbEntityContext();
                var entity = context.Add(select.Parameters[0]);
                if (from != null)
                {
                    var fromSql = new List<string>();
                    Convert(from.Body, fromSql, parameters, context);
                    entity.Table.Name = fromSql.Concat();
                }
                if (where != null)
                {
                    context.Add(where.Parameters[0], entity);
                    sql.Add(" WHERE ");
                    Convert(where.Body, sql, parameters, context);
                }
                var whereIndex = sql.Count;
                if (orderBy != null)
                {
                    context.Add(orderBy.Parameters[0], entity);
                    sql.Add(" ORDER BY ");
                    Convert(orderBy.Body, sql, parameters, context);
                }
                sql.Add($" LIMIT {offset},{fetch}");
                var selectIndex = sql.Count;
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);
                var fromIndex = sql.Count;
                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }
                var countIndex = sql.Count;
                sql.Add(";SELECT COUNT(1)");
                return sql.Concat((selectIndex, countIndex), (0, selectIndex), (countIndex, sql.Count), (fromIndex, countIndex), (0, whereIndex));
            }
            public static string Select(LambdaExpression select, LambdaExpression from, LambdaExpression where, LambdaExpression groupBy, LambdaExpression having, LambdaExpression orderBy, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var context = new DbEntityContext();
                var entity = context.Add(select.Parameters[0]);
                if (from != null)
                {
                    var fromSql = new List<string>();
                    Convert(from.Body, fromSql, parameters, context);
                    entity.Table.Name = fromSql.Concat();
                }
                if (where != null)
                {
                    context.Add(where.Parameters[0], entity);
                    sql.Add(" WHERE ");
                    Convert(where.Body, sql, parameters, context);
                }
                if (groupBy != null)
                {
                    context.Add(groupBy.Parameters[0], entity);
                    sql.Add(" GROUP BY ");
                    Convert(groupBy.Body, sql, parameters, context);
                }
                if (having != null)
                {
                    context.Add(having.Parameters[0], entity);
                    sql.Add(" HAVING ");
                    Convert(having.Body, sql, parameters, context);
                }
                if (orderBy != null)
                {
                    context.Add(orderBy.Parameters[0], entity);
                    sql.Add(" ORDER BY ");
                    Convert(orderBy.Body, sql, parameters, context);
                }
                var index = sql.Count;
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);

                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }
                return sql.Concat((index, sql.Count), (0, index));
            }
            public static string Select(int offset, int fetch, LambdaExpression select, LambdaExpression from, LambdaExpression where, LambdaExpression groupBy, LambdaExpression having, LambdaExpression orderBy, List<(string, object)> parameters)
            {
                var sql = CommandText;
                var context = new DbEntityContext();
                var entity = context.Add(select.Parameters[0]);
                if (from != null)
                {
                    var fromSql = new List<string>();
                    Convert(from.Body, fromSql, parameters, context);
                    entity.Table.Name = fromSql.Concat();
                }
                if (where != null)
                {
                    context.Add(where.Parameters[0], entity);
                    sql.Add(" WHERE ");
                    Convert(where.Body, sql, parameters, context);
                }
                if (groupBy != null)
                {
                    context.Add(groupBy.Parameters[0], entity);
                    sql.Add(" GROUP BY ");
                    Convert(groupBy.Body, sql, parameters, context);
                }
                if (having != null)
                {
                    context.Add(having.Parameters[0], entity);
                    sql.Add(" HAVING ");
                    Convert(having.Body, sql, parameters, context);
                }
                if (orderBy != null)
                {
                    context.Add(orderBy.Parameters[0], entity);
                    sql.Add(" ORDER BY ");
                    Convert(orderBy.Body, sql, parameters, context);
                }
                sql.Add($" LIMIT {offset},{fetch}");
                var index = sql.Count;
                sql.Add("SELECT ");
                Select(select.Body, sql, parameters, context);

                sql.Add(" FROM ");
                sql.Add(entity.Table.Name);
                sql.Add(" ");
                sql.Add(entity.Name);
                foreach ((_, var joinEntity) in entity.Entities)
                {
                    Join(entity, joinEntity, sql);
                }
                return sql.Concat((index, sql.Count), (0, index));
            }
            public MySqlDb(Type connectionType, Func<(DbCommand, IDisposable)> getCommand, Action<DbCommand> cmdExecuting)
            {
                if (_ConnectionType == null)
                {
                    lock (_Sync)
                    {
                        if (_ConnectionType == null)
                        {
                            _ConnectionType = connectionType;
                            _DataReaderType = connectionType.GetMethod("CreateCommand", Type.EmptyTypes).ReturnType.
                                GetMethod("ExecuteReader", Type.EmptyTypes).ReturnType;

                            _AddParameter = typeof(SqlDbExtensions).GetMethod("AddParameter").MakeGenericMethod(connectionType).
                               CreateDelegate<Action<DbCommand, string, object>>();

                            var member = Expression.Parameter(typeof(MemberExpression), "member");
                            var registerMember = typeof(SqlDbExtensions).GetMethod("RegisterDbMember", new[] { typeof(MemberExpression), typeof(IReadOnlyList<object>).MakeByRefType() });
                            var memberObjs = Expression.Variable(typeof(IReadOnlyList<object>), "exprObjs");
                            _DbMember = Expression.Lambda<Func<MemberExpression, IReadOnlyList<object>>>(
                                Expression.Block(new[] { memberObjs },
                                Expression.Call(null, registerMember.MakeGenericMethod(connectionType), member, memberObjs),
                                memberObjs), member).Compile();

                            var method = Expression.Parameter(typeof(MethodCallExpression), "method");
                            var registerMethod = typeof(SqlDbExtensions).GetMethod("RegisterDbMethod", new[] { typeof(MethodCallExpression), typeof(IReadOnlyList<object>).MakeByRefType() });
                            var methodObjs = Expression.Variable(typeof(IReadOnlyList<object>), "exprObjs");
                            _DbMethod = Expression.Lambda<Func<MethodCallExpression, IReadOnlyList<object>>>(
                                Expression.Block(new[] { methodObjs },
                                Expression.Call(null, registerMethod.MakeGenericMethod(connectionType), method, methodObjs),
                                methodObjs), method).Compile();
                        }
                    }
                }
                _getCommand = getCommand;
                _cmdExecuting = cmdExecuting;
            }
            public override int Execute(string sql)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    return cmd.ExecuteNonQuery();
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            public override int Execute(string sql, object parameters)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    Query.AddParameter(cmd, parameters);
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    return cmd.ExecuteNonQuery();
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            public override int Execute(string sql, IList<(string, object)> parameters)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    if (parameters != null && parameters.Count > 0)
                    {
                        for (int i = 0; i < parameters.Count; i++)
                        {
                            (var name, var value) = parameters[i];
                            _AddParameter(cmd, name, value);
                        }
                    }
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    return cmd.ExecuteNonQuery();
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            public override async Task<int> ExecuteAsync(string sql)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    return await cmd.ExecuteNonQueryAsync();
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            public override async Task<int> ExecuteAsync(string sql, object parameters)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    Query.AddParameter(cmd, parameters);
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    return await cmd.ExecuteNonQueryAsync();
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            public override async Task<int> ExecuteAsync(string sql, IList<(string, object)> parameters)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    if (parameters != null && parameters.Count > 0)
                    {
                        for (int i = 0; i < parameters.Count; i++)
                        {
                            (var name, var value) = parameters[i];
                            _AddParameter(cmd, name, value);
                        }
                    }
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    return await cmd.ExecuteNonQueryAsync();
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            public override int ExecuteFormat(FormattableString sqlFormat)
            {
                if (sqlFormat == null)
                    throw new ArgumentNullException(nameof(sqlFormat));

                var parameters = Parameters;
                var args = sqlFormat.GetArguments();
                var names = new string[args.Length];
                for (int i = 0; i < args.Length; i++)
                {
                    var name = Query.GetParameter(parameters.Count);
                    parameters.Add((name, args[i]));
                    names[i] = name;
                }
                return Execute(string.Format(sqlFormat.Format, names), parameters);
            }
            public override Task<int> ExecuteFormatAsync(FormattableString sqlFormat)
            {
                if (sqlFormat == null)
                    throw new ArgumentNullException(nameof(sqlFormat));

                var parameters = Parameters;
                var args = sqlFormat.GetArguments();
                var names = new string[args.Length];
                for (int i = 0; i < args.Length; i++)
                {
                    var name = Query.GetParameter(parameters.Count);
                    parameters.Add((name, args[i]));
                    names[i] = name;
                }
                return ExecuteAsync(string.Format(sqlFormat.Format, names), parameters);
            }
            public override (DbDataReader, IDisposable) ExecuteReader(string sql)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    var reader = cmd.ExecuteReader();
                    return (reader, disposable);
                }
                catch
                {
                    disposable.Dispose();
                    throw;
                }
            }
            public override (DbDataReader, IDisposable) ExecuteReader(string sql, object parameters)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    Query.AddParameter(cmd, parameters);
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    var reader = cmd.ExecuteReader();
                    return (reader, disposable);
                }
                catch
                {
                    disposable.Dispose();
                    throw;
                }
            }
            public override (DbDataReader, IDisposable) ExecuteReader(string sql, IList<(string, object)> parameters)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    if (parameters != null && parameters.Count > 0)
                    {
                        for (int i = 0; i < parameters.Count; i++)
                        {
                            (var name, var value) = parameters[i];
                            _AddParameter(cmd, name, value);
                        }
                    }
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    var reader = cmd.ExecuteReader();
                    return (reader, disposable);
                }
                catch
                {
                    disposable.Dispose();
                    throw;
                }
            }
            public override (DbDataReader, IDisposable) ExecuteReaderFormat(FormattableString sqlFormat)
            {
                if (sqlFormat == null)
                    throw new ArgumentNullException(nameof(sqlFormat));

                var parameters = Parameters;
                var args = sqlFormat.GetArguments();
                var names = new string[args.Length];
                for (int i = 0; i < args.Length; i++)
                {
                    var name = Query.GetParameter(parameters.Count);
                    parameters.Add((name, args[i]));
                    names[i] = name;
                }
                return ExecuteReader(string.Format(sqlFormat.Format, names), parameters);
            }
            public override async Task<(DbDataReader, IDisposable)> ExecuteReaderAsync(string sql)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    var reader = await cmd.ExecuteReaderAsync();
                    return (reader, disposable);
                }
                catch
                {
                    disposable.Dispose();
                    throw;
                }
            }
            public override async Task<(DbDataReader, IDisposable)> ExecuteReaderAsync(string sql, object parameters)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    Query.AddParameter(cmd, parameters);
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    var reader = await cmd.ExecuteReaderAsync();
                    return (reader, disposable);
                }
                catch
                {
                    disposable.Dispose();
                    throw;
                }
            }
            public override async Task<(DbDataReader, IDisposable)> ExecuteReaderAsync(string sql, IList<(string, object)> parameters)
            {
                (var cmd, var disposable) = _getCommand();
                try
                {
                    cmd.CommandText = sql;
                    if (parameters != null && parameters.Count > 0)
                    {
                        for (int i = 0; i < parameters.Count; i++)
                        {
                            (var name, var value) = parameters[i];
                            _AddParameter(cmd, name, value);
                        }
                    }
                    _cmdExecuting?.Invoke(cmd);
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    var reader = await cmd.ExecuteReaderAsync();
                    return (reader, disposable);
                }
                catch
                {
                    disposable.Dispose();
                    throw;
                }
            }
            public override Task<(DbDataReader, IDisposable)> ExecuteReaderFormatAsync(FormattableString sqlFormat)
            {
                if (sqlFormat == null)
                    throw new ArgumentNullException(nameof(sqlFormat));

                var parameters = Parameters;
                var args = sqlFormat.GetArguments();
                var names = new string[args.Length];
                for (int i = 0; i < args.Length; i++)
                {
                    var name = Query.GetParameter(parameters.Count);
                    parameters.Add((name, args[i]));
                    names[i] = name;
                }
                return ExecuteReaderAsync(string.Format(sqlFormat.Format, names), parameters);
            }
            public override TEntity Read<TEntity>(DbDataReader dataReader)
            {
                var handler = Reader<TEntity>.Handler;
                if (handler == null)
                {
                    lock (_Sync)
                    {
                        handler = Reader<TEntity>.Handler;
                        if (handler == null)
                        {
                            var register = typeof(SqlDbExtensions).GetMethod("RegisterDbReader", new[] { typeof(Type), typeof(ParameterExpression), typeof(Expression).MakeByRefType(), typeof(Delegate).MakeByRefType() }).MakeGenericMethod(_DataReaderType);
                            var dbReader = Expression.Parameter(typeof(DbDataReader), "dbReader");
                            var reader = Expression.Variable(_DataReaderType, "reader");
                            var args = new object[] { typeof(TEntity), reader, default(Expression), default(Delegate) };
                            register.Invoke(null, args);
                            var read = _DataReaderType.GetMethod("Read", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
                            var expr = Expression.Block(new[] { reader },
                                Expression.Assign(reader, Expression.Convert(dbReader, _DataReaderType)),
                                Expression.Condition(Expression.Call(reader, read), (Expression)args[2], Expression.Default(typeof(TEntity))));
                            handler = Expression.Lambda<Func<DbDataReader, TEntity>>(expr, dbReader).Compile();
                            Reader<TEntity>.Handler = handler;
                        }
                    }
                }
                return handler(dataReader);
            }
            public override int Insert<TEntity>(Expression<Func<SqlExpression, TEntity>> entity)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                var parameters = Parameters;
                var sql = Insert(entity, parameters);
                return Execute(sql, parameters);
            }
            public override int Insert<TEntity>(TEntity entity, Expression<Func<TEntity, object>> properties)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));

                var parameters = Parameters;
                var sql = Insert(entity, properties, parameters);
                return Execute(sql, parameters);
            }
            public override int InsertRange<TEntity>(IList<TEntity> entities, Expression<Func<TEntity, object>> properties)
            {
                if (entities == null)
                    throw new ArgumentNullException(nameof(entities));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));

                var result = 0;
                var sql = new List<string>();
                var parameters = new List<(string, object)>();
                var table = DbTable.Get(properties.Parameters[0].Type);
                var fields = GetFields(table, properties.Body);
                for (int index = 0; ;)
                {
                    if (index == entities.Count)
                        break;
                    var entity = entities[index++];
                    var count = 1;
                    var fieldCount = fields.Count;
                    sql.Clear();
                    parameters.Clear();
                    sql.Add("INSERT INTO ");
                    sql.Add(table.Name);
                    sql.Add("(");
                    var enumerator = fields.GetEnumerator();
                    if (enumerator.MoveNext())
                    {
                        sql.Add(enumerator.Current.Value);
                    }
                    while (enumerator.MoveNext())
                    {
                        sql.Add(",");
                        sql.Add(enumerator.Current.Value);
                    }
                    sql.Add(") VALUES (");
                    enumerator = fields.GetEnumerator();
                    if (enumerator.MoveNext())
                    {
                        var value = enumerator.Current.Key.GetValue(entity);
                        var parameter = Query.GetParameter(parameters.Count);
                        parameters.Add((parameter, value));
                        sql.Add(parameter);
                    }
                    while (enumerator.MoveNext())
                    {
                        sql.Add(",");
                        var value = enumerator.Current.Key.GetValue(entity);
                        var parameter = Query.GetParameter(parameters.Count);
                        parameters.Add((parameter, value));
                        sql.Add(parameter);
                    }
                    sql.Add(")");
                    while (index < entities.Count)
                    {
                        if (count == 5000 || fieldCount + fields.Count >= 3000)
                            break;
                        count += 1;
                        fieldCount += fields.Count;
                        entity = entities[index++];
                        sql.Add(",(");
                        enumerator = fields.GetEnumerator();
                        if (enumerator.MoveNext())
                        {
                            var value = enumerator.Current.Key.GetValue(entity);
                            var parameter = Query.GetParameter(parameters.Count);
                            parameters.Add((parameter, value));
                            sql.Add(parameter);
                        }
                        while (enumerator.MoveNext())
                        {
                            sql.Add(",");
                            var value = enumerator.Current.Key.GetValue(entity);
                            var parameter = Query.GetParameter(parameters.Count);
                            parameters.Add((parameter, value));
                            sql.Add(parameter);
                        }
                        sql.Add(")");
                    }
                    result += Execute(sql.Concat(), parameters);
                }
                return result;
            }
            public override Task<int> InsertAsync<TEntity>(Expression<Func<SqlExpression, TEntity>> entity)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                var parameters = Parameters;
                var sql = Insert(entity, parameters);
                return ExecuteAsync(sql, parameters);
            }
            public override Task<int> InsertAsync<TEntity>(TEntity entity, Expression<Func<TEntity, object>> properties)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));

                var parameters = Parameters;
                var sql = Insert(entity, properties, parameters);
                return ExecuteAsync(sql, parameters);
            }
            public override async Task<int> InsertRangeAsync<TEntity>(IList<TEntity> entities, Expression<Func<TEntity, object>> properties)
            {
                if (entities == null)
                    throw new ArgumentNullException(nameof(entities));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));

                var result = 0;
                var sql = new List<string>();
                var parameters = new List<(string, object)>();
                var table = DbTable.Get(properties.Parameters[0].Type);
                var fields = GetFields(table, properties.Body);
                for (int index = 0; ;)
                {
                    if (index == entities.Count)
                        break;
                    var entity = entities[index++];
                    var count = 1;
                    var fieldCount = fields.Count;
                    sql.Clear();
                    parameters.Clear();
                    sql.Add("INSERT INTO ");
                    sql.Add(table.Name);
                    sql.Add("(");
                    var enumerator = fields.GetEnumerator();
                    if (enumerator.MoveNext())
                    {
                        sql.Add(enumerator.Current.Value);
                    }
                    while (enumerator.MoveNext())
                    {
                        sql.Add(",");
                        sql.Add(enumerator.Current.Value);
                    }
                    sql.Add(") VALUES (");
                    enumerator = fields.GetEnumerator();
                    if (enumerator.MoveNext())
                    {
                        var value = enumerator.Current.Key.GetValue(entity);
                        var parameter = Query.GetParameter(parameters.Count);
                        parameters.Add((parameter, value));
                        sql.Add(parameter);
                    }
                    while (enumerator.MoveNext())
                    {
                        sql.Add(",");
                        var value = enumerator.Current.Key.GetValue(entity);
                        var parameter = Query.GetParameter(parameters.Count);
                        parameters.Add((parameter, value));
                        sql.Add(parameter);
                    }
                    sql.Add(")");
                    while (index < entities.Count)
                    {
                        if (count == 5000 || fieldCount + fields.Count >= 3000)
                            break;
                        count += 1;
                        fieldCount += fields.Count;
                        entity = entities[index++];
                        sql.Add(",(");
                        enumerator = fields.GetEnumerator();
                        if (enumerator.MoveNext())
                        {
                            var value = enumerator.Current.Key.GetValue(entity);
                            var parameter = Query.GetParameter(parameters.Count);
                            parameters.Add((parameter, value));
                            sql.Add(parameter);
                        }
                        while (enumerator.MoveNext())
                        {
                            sql.Add(",");
                            var value = enumerator.Current.Key.GetValue(entity);
                            var parameter = Query.GetParameter(parameters.Count);
                            parameters.Add((parameter, value));
                            sql.Add(parameter);
                        }
                        sql.Add(")");
                    }
                    result += await ExecuteAsync(sql.Concat(), parameters);
                }
                return result;
            }
            public override TIdentity InsertIdentity<TEntity, TIdentity>(Expression<Func<SqlExpression, TEntity>> entity)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                var parameters = Parameters;
                var sql = InsertIdentity(entity, parameters);
                return this.Execute<TIdentity>(sql, parameters);
            }
            public override TIdentity InsertIdentity<TEntity, TIdentity>(TEntity entity, Expression<Func<TEntity, object>> properties)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));

                var parameters = Parameters;
                var sql = InsertIdentity(entity, properties, parameters);
                return this.Execute<TIdentity>(sql, parameters);
            }
            public override Task<TIdentity> InsertIdentityAsync<TEntity, TIdentity>(Expression<Func<SqlExpression, TEntity>> entity)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                var parameters = Parameters;
                var sql = InsertIdentity(entity, parameters);
                return this.ExecuteAsync<TIdentity>(sql, parameters);
            }
            public override Task<TIdentity> InsertIdentityAsync<TEntity, TIdentity>(TEntity entity, Expression<Func<TEntity, object>> properties)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));

                var parameters = Parameters;
                var sql = InsertIdentity(entity, properties, parameters);
                return this.ExecuteAsync<TIdentity>(sql, parameters);
            }
            public override int Delete<TEntity>(object identity)
            {
                var parameters = Parameters;
                var sql = Delete(DbTable.Get(typeof(TEntity)), identity, parameters);
                return Execute(sql, parameters);
            }
            public override int Delete<TEntity>(Expression<Func<TEntity, SqlExpression, bool>> where)
            {
                if (where == null)
                    throw new ArgumentNullException(nameof(where));

                var parameters = Parameters;
                var sql = Delete(DbTable.Get(typeof(TEntity)), where, parameters);
                return Execute(sql, parameters);
            }
            public override Task<int> DeleteAsync<TEntity>(object identity)
            {
                var parameters = Parameters;
                var sql = Delete(DbTable.Get(typeof(TEntity)), identity, parameters);
                return ExecuteAsync(sql, parameters);
            }
            public override Task<int> DeleteAsync<TEntity>(Expression<Func<TEntity, SqlExpression, bool>> where)
            {
                if (where == null)
                    throw new ArgumentNullException(nameof(where));

                var parameters = Parameters;
                var sql = Delete(DbTable.Get(typeof(TEntity)), where, parameters);
                return ExecuteAsync(sql, parameters);
            }
            public override int Update<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> entity, object identity)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                var parameters = Parameters;
                var sql = Update(entity, identity, parameters);
                return Execute(sql, parameters);
            }
            public override int Update<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> entity, Expression<Func<TEntity, SqlExpression, bool>> where)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (where == null)
                    throw new ArgumentNullException(nameof(where));

                var parameters = Parameters;
                var sql = Update(entity, where, parameters);
                return Execute(sql, parameters);
            }
            public override int Update<TEntity>(TEntity entity, Expression<Func<TEntity, object>> properties)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));

                SqlDbExtensions.RegisterIdentity<TEntity>(out var identity);
                if (identity == null)
                    throw new ArgumentNullException(nameof(identity));

                var parameters = Parameters;
                var sql = Update(entity, properties, identity.GetValue(entity), parameters);
                return Execute(sql, parameters);
            }
            public override int Update<TEntity>(TEntity entity, Expression<Func<TEntity, object>> properties, Expression<Func<TEntity, SqlExpression, bool>> where)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));
                if (where == null)
                    throw new ArgumentNullException(nameof(where));

                var parameters = Parameters;
                var sql = Update(entity, properties, where, parameters);
                return Execute(sql, parameters);
            }
            public override Task<int> UpdateAsync<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> entity, object identity)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                var parameters = Parameters;
                var sql = Update(entity, identity, parameters);
                return ExecuteAsync(sql, parameters);
            }
            public override Task<int> UpdateAsync<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> entity, Expression<Func<TEntity, SqlExpression, bool>> where)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (where == null)
                    throw new ArgumentNullException(nameof(where));

                var parameters = Parameters;
                var sql = Update(entity, where, parameters);
                return ExecuteAsync(sql, parameters);
            }
            public override Task<int> UpdateAsync<TEntity>(TEntity entity, Expression<Func<TEntity, object>> properties)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));

                SqlDbExtensions.RegisterIdentity<TEntity>(out var identity);
                if (identity == null)
                    throw new ArgumentNullException(nameof(identity));

                var parameters = Parameters;
                var sql = Update(entity, properties, identity.GetValue(entity), parameters);
                return ExecuteAsync(sql, parameters);
            }
            public override Task<int> UpdateAsync<TEntity>(TEntity entity, Expression<Func<TEntity, object>> properties, Expression<Func<TEntity, SqlExpression, bool>> where)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (properties == null)
                    throw new ArgumentNullException(nameof(properties));
                if (where == null)
                    throw new ArgumentNullException(nameof(where));

                var parameters = Parameters;
                var sql = Update(entity, properties, where, parameters);
                return ExecuteAsync(sql, parameters);
            }
            public override TEntity SelectSingle<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> select, object identity)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = SelectSingle(select, identity, parameters);
                return this.Execute<TEntity>(sql, parameters);
            }
            public override Task<TEntity> SelectSingleAsync<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> select, object identity)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = SelectSingle(select, identity, parameters);
                return this.ExecuteAsync<TEntity>(sql, parameters);
            }
            public override TEntity SelectSingle<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = SelectSingle(select, where, parameters);
                return this.Execute<TEntity>(sql, parameters);
            }
            public override Task<TEntity> SelectSingleAsync<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = SelectSingle(select, where, parameters);
                return this.ExecuteAsync<TEntity>(sql, parameters);
            }
            public override List<TEntity> Select<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = Select(select, where, orderBy, parameters);
                return this.Execute<List<TEntity>>(sql, parameters);
            }
            public override Task<List<TEntity>> SelectAsync<TEntity>(Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = Select(select, where, orderBy, parameters);
                return this.ExecuteAsync<List<TEntity>>(sql, parameters);
            }
            public override List<TEntity> Select<TEntity>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = Select(offset, fetch, select, where, orderBy, parameters);
                return this.Execute<List<TEntity>>(sql, parameters);
            }
            public override Task<List<TEntity>> SelectAsync<TEntity>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = Select(offset, fetch, select, where, orderBy, parameters);
                return this.ExecuteAsync<List<TEntity>>(sql, parameters);
            }
            public override (List<TEntity>, int) SelectPaged<TEntity>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = SelectPaged(offset, fetch, select, null, where, orderBy, parameters);
                return this.Execute<List<TEntity>, int>(sql, parameters);
            }
            public override Task<(List<TEntity>, int)> SelectPagedAsync<TEntity>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, TEntity>> select, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = SelectPaged(offset, fetch, select, null, where, orderBy, parameters);
                return this.ExecuteAsync<List<TEntity>, int>(sql, parameters);
            }
            public override TResult Select<TEntity, TResult>(Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<SqlExpression, object>> from, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Func<TEntity, SqlExpression, object>> groupBy, Expression<Func<TEntity, SqlExpression, bool>> having, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = Select(select, from, where, groupBy, having, orderBy, parameters);
                return this.Execute<TResult>(sql, parameters);
            }
            public override Task<TResult> SelectAsync<TEntity, TResult>(Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<SqlExpression, object>> from, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Func<TEntity, SqlExpression, object>> groupBy, Expression<Func<TEntity, SqlExpression, bool>> having, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = Select(select, from, where, groupBy, having, orderBy, parameters);
                return this.ExecuteAsync<TResult>(sql, parameters);
            }
            public override TResult Select<TEntity, TResult>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<SqlExpression, object>> from, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Func<TEntity, SqlExpression, object>> groupBy, Expression<Func<TEntity, SqlExpression, bool>> having, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = Select(offset, fetch, select, from, where, groupBy, having, orderBy, parameters);
                return this.Execute<TResult>(sql, parameters);
            }
            public override Task<TResult> SelectAsync<TEntity, TResult>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<SqlExpression, object>> from, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Func<TEntity, SqlExpression, object>> groupBy, Expression<Func<TEntity, SqlExpression, bool>> having, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = Select(offset, fetch, select, from, where, groupBy, having, orderBy, parameters);
                return this.ExecuteAsync<TResult>(sql, parameters);
            }
            public override (TResult, int) SelectPaged<TEntity, TResult>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<SqlExpression, object>> from, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = SelectPaged(offset, fetch, select, from, where, orderBy, parameters);
                return this.Execute<TResult, int>(sql, parameters);
            }
            public override Task<(TResult, int)> SelectPagedAsync<TEntity, TResult>(int offset, int fetch, Expression<Func<TEntity, SqlExpression, object>> select, Expression<Func<SqlExpression, object>> from, Expression<Func<TEntity, SqlExpression, bool>> where, Expression<Action<TEntity, SqlExpression>> orderBy)
            {
                if (select == null)
                    throw new ArgumentNullException(nameof(select));

                var parameters = Parameters;
                var sql = SelectPaged(offset, fetch, select, from, where, orderBy, parameters);
                return this.ExecuteAsync<TResult, int>(sql, parameters);
            }
        }
        #endregion

        #region Transaction
        private class Transaction : DbTransaction
        {
            public TransactionFactory TransactionFactory;
            public Transaction Previous;//volatile???
            public Transaction Next;
            public DbTransaction DbTransaction;
            public override IsolationLevel IsolationLevel => DbTransaction.IsolationLevel;
            protected override DbConnection DbConnection => DbTransaction.Connection;
            public override void Commit()
            {
                var connection = DbTransaction.Connection;
                if (connection == null)
                    return;
                try
                {
                    DbTransaction.Commit();
                }
                finally
                {
                    if (Previous != null)
                        Previous.Next = Next;
                    if (Next != null)
                        Next.Previous = Previous;
                    if (TransactionFactory.Transaction.Value == this)
                        TransactionFactory.Transaction.Value = Next;

                    Previous = null;
                    Next = null;
                    connection.Close();
                }
            }
            public override void Rollback()
            {
                var connection = DbTransaction.Connection;
                if (connection == null)
                    return;
                try
                {
                    DbTransaction.Rollback();
                }
                finally
                {
                    if (Previous != null)
                        Previous.Next = Next;
                    if (Next != null)
                        Next.Previous = Previous;
                    if (TransactionFactory.Transaction.Value == this)
                        TransactionFactory.Transaction.Value = Next;

                    Previous = null;
                    Next = null;
                    connection.Close();
                }
            }
        }
        private class TransactionFactory : ITransactionFactory
        {
            public IsolationLevel IsolationLevel;
            public AsyncLocal<Transaction> Transaction;
            public Func<IsolationLevel, DbTransaction> BeginDbTransaction;
            public DbTransaction Create() => Create(IsolationLevel);
            public DbTransaction Create(IsolationLevel isolationLevel)
            {
                var transaction = new Transaction()
                {
                    TransactionFactory = this,
                    DbTransaction = BeginDbTransaction(isolationLevel)
                };
                var temp = Transaction.Value;
                if (temp != null)
                {
                    transaction.Previous = temp;
                    temp.Next = transaction;
                }
                Transaction.Value = transaction;
                return transaction;
            }
        }
        #endregion

        #region Register
        private static class Db<TDbConnection> where TDbConnection : DbConnection
        {
            public static Type DbType;
        }
        #endregion
    }
}
