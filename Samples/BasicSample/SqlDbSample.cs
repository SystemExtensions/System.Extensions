using System;
using System.Text;
using System.Data;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;//using System.Data.SqlClient;
using MySql.Data.MySqlClient;//using MySqlConnector;
using Npgsql;
using Oracle.ManagedDataAccess.Client;

namespace BasicSample
{
    public class SqlDbSample
    {
        public static async Task RunSQLite()//Better synchronization??
        {
            var builder = new SqliteConnectionStringBuilder();
            builder.DataSource = "testdb";
            var connectionString = builder.ToString();
            Console.WriteLine(connectionString);


            var db = SqlDb.Create<SqliteConnection>(connectionString, (cmd) => {
                Console.WriteLine(cmd.CommandText);
            });

            //Number of rows affected
            await db.ExecuteAsync("SELECT 'ZhangHe'");
            var stringParam = "ZhangHe";
            await db.ExecuteAsync("SELECT @String", new { String = stringParam });
            await db.ExecuteAsync("SELECT @String", new List<(string, object)>() { ("String", stringParam) });
            await db.ExecuteFormatAsync($"SELECT {stringParam}");
            //Model
            var exe1 = await db.ExecuteAsync<string>("SELECT 'ZhangHe'");
            var exe2 = await db.ExecuteAsync<string>("SELECT @String", new { String = stringParam });
            var exe3 = await db.ExecuteAsync<string>("SELECT @String", new List<(string, object)>() { ("String", stringParam) });
            var exe4 = await db.ExecuteFormatAsync<string>($"SELECT {stringParam}");
            (var name, var age) = await db.ExecuteAsync<string, int>("SELECT @Name;SELECT @Age", new { Name = stringParam, Age = int.MinValue });
            (var names, var ages, var ids) = await db.ExecuteAsync<List<string>, List<int>, List<int>>("SELECT @Name;SELECT @Age;SELECT @Id", new { Id = 1024, Name = stringParam, Age = int.MinValue });


            try { await db.ExecuteAsync("DROP TABLE [Custom]"); } catch { }
            try { await db.ExecuteAsync("DROP TABLE [CustomDetail]"); } catch { }
            try { await db.ExecuteAsync("DROP TABLE [Address]"); } catch { }
            try { await db.ExecuteAsync("DROP TABLE [Order]"); } catch { }
            await db.ExecuteAsync("CREATE TABLE [Custom]([Id] INTEGER PRIMARY KEY AUTOINCREMENT,[Name] TEXT NULL,[Age] INTEGER NULL)");
            await db.ExecuteAsync("CREATE TABLE [CustomDetail]([CustomId] INTEGER,[NullInt] INTEGER NULL,[Bool] INTEGER NULL)");
            await db.ExecuteAsync("CREATE TABLE [Address]([OrderId] INTEGER NOT NULL,[Name] TEXT NULL,[Detail] TEXT NULL,[Mobile] TEXT NULL,[Sheng] TEXT NULL,[Shi] TEXT NULL,[Xian] TEXT NULL)");
            await db.ExecuteAsync("CREATE TABLE [Order]([Id] INTEGER PRIMARY KEY AUTOINCREMENT,[Name] TEXT NULL,[Price] INTEGER NULL,[CreateTime] datetime NULL,[CustomId] INTEGER NULL,[State] INTEGER NULL,[JsonData] TEXT NULL)");


            //SqlDbExtensions.RegisterTable(type => type.Name);
            //SqlDbExtensions.RegisterProperty(property => property.Name);
            //SqlDbExtensions.RegisterIdentity
            SqlDbExtensions.RegisterDbParameter<SqliteConnection, JsonData>((value) => {
                return JsonWriter.ToJson<JsonData>((JsonData)value);
            });
            //(reader,i)=field,(reader)=model Mapper
            SqlDbExtensions.RegisterDbReader<SqliteDataReader, JsonData>((reader, i) => {
                var json = reader.GetString(i);
                return JsonReader.FromJson<JsonData>(json);
            });
            SqlDbExtensions.RegisterDbMethod<SqliteConnection>((methods) => {
                foreach (var item in methods)
                {
                    var method = item.Key;
                    Console.WriteLine($"{method.DeclaringType}.{method.Name}()");
                }
                methods.Add(typeof(MyExpressionEx).GetMethod("CountPlus1"), (expr) => {
                    return new object[] { "COUNT(", expr.Arguments[1], ")+1" };//string OR Expression
                });
                methods.Add(typeof(MyExpressionEx).GetMethod("CountPlus2"), (expr) => {
                    var exprObjs = new List<object>();//string OR Expression
                    exprObjs.Add("COUNT(");
                    exprObjs.Add(expr.Arguments[0]);
                    exprObjs.Add(")+2");
                    return exprObjs;
                });
            });
            SqlDbExtensions.RegisterDbMember<SqliteConnection>((members) => {
                foreach (var item in members)
                {
                    var member = item.Key;
                    Console.WriteLine($"{member.DeclaringType}.{member.Name}");
                }
            });


            await InsertAsync(db);
            await SelectAsync(db);
            await UpdateAsync(db);
            await DeleteAsync(db);


            //Transaction 
            //out ITransactionFactory use AsyncLocal<>
            //如果不介意性能 不需要分开定义 db,dbTx 
            //If you don't mind performance There is no need to define separately db,dbTx 
            var dbTx = SqlDb.Create<SqliteConnection>(connectionString, (cmd) => {
                Console.WriteLine(cmd.CommandText);
            },out var txFactory);

            await TransactionAsync(dbTx, txFactory);
        }
        public static async Task RunSqlServer()
        {
            var builder = new SqlConnectionStringBuilder();
            builder.DataSource = "49.232.79.230";
            builder.InitialCatalog = "TestDb";
            builder.UserID = "sa";
            builder.Password = "qx(62Bjd]hKu";
            var connectionString = builder.ToString();
            Console.WriteLine(connectionString);


            var db = SqlDb.Create<SqlConnection>(connectionString, (cmd) => {
                Console.WriteLine(cmd.CommandText);
            });

            //Number of rows affected
            await db.ExecuteAsync("SELECT 'ZhangHe'");
            var stringParam = "ZhangHe";
            await db.ExecuteAsync("SELECT @String", new { String = stringParam });
            await db.ExecuteAsync("SELECT @String", new List<(string, object)>() { ("String", stringParam) });
            await db.ExecuteFormatAsync($"SELECT {stringParam}");
            //Model
            var exe1 = await db.ExecuteAsync<string>("SELECT 'ZhangHe'");
            var exe2 = await db.ExecuteAsync<string>("SELECT @String", new { String = stringParam });
            var exe3 = await db.ExecuteAsync<string>("SELECT @String", new List<(string, object)>() { ("String", stringParam) });
            var exe4 = await db.ExecuteFormatAsync<string>($"SELECT {stringParam}");
            (var name, var age) = await db.ExecuteAsync<string, int>("SELECT @Name;SELECT @Age", new { Name = stringParam, Age = int.MinValue });


            try { await db.ExecuteAsync("DROP TABLE [Custom]"); } catch { }
            try { await db.ExecuteAsync("DROP TABLE [CustomDetail]"); } catch { }
            try { await db.ExecuteAsync("DROP TABLE [Address]"); } catch { }
            try { await db.ExecuteAsync("DROP TABLE [Order]"); } catch { }
            await db.ExecuteAsync("CREATE TABLE [Custom]([Id] [int] IDENTITY(1,1) NOT NULL,[Name] [nvarchar](50) NULL,[Age] [int] NULL)");
            await db.ExecuteAsync("CREATE TABLE [CustomDetail]([CustomId] [int],[NullInt] [int] NULL,[Bool] [bit])");
            await db.ExecuteAsync("CREATE TABLE [Address]([OrderId] [int] NOT NULL,[Name] [nvarchar](50) NULL,[Detail] [nvarchar](500) NULL,[Mobile] [nvarchar](500) NULL,[Sheng] [nvarchar](500) NULL,[Shi] [nvarchar](500) NULL,[Xian] [nvarchar](500) NULL)");
            await db.ExecuteAsync("CREATE TABLE [Order]([Id] [int] IDENTITY(1,1) NOT NULL,[Name] [nvarchar](50) NULL,[Price] [int] NULL,[CreateTime] [datetime] NULL,[CustomId] [int] NULL,[State] [int] NULL,[JsonData] [nvarchar](500) NULL)");


            //SqlDbExtensions.RegisterTable(type => type.Name);
            //SqlDbExtensions.RegisterProperty(property => property.Name);
            //SqlDbExtensions.RegisterIdentity
            SqlDbExtensions.RegisterDbParameter<SqlConnection, JsonData>((value) => {
                return JsonWriter.ToJson<JsonData>((JsonData)value);
            });
            //(reader,i)=field,(reader)=model Mapper
            SqlDbExtensions.RegisterDbReader<SqlDataReader, JsonData>((reader, i) => {
                var json = reader.GetString(i);
                return JsonReader.FromJson<JsonData>(json);
            });
            SqlDbExtensions.RegisterDbMethod<SqlConnection>((methods) => {
                foreach (var item in methods)
                {
                    var method = item.Key;
                    Console.WriteLine($"{method.DeclaringType}.{method.Name}()");
                }
                methods.Add(typeof(MyExpressionEx).GetMethod("CountPlus1"), (expr) => {
                    return new object[] { "COUNT(", expr.Arguments[1], ")+1" };//string OR Expression
                });
                methods.Add(typeof(MyExpressionEx).GetMethod("CountPlus2"), (expr) => {
                    var exprObjs = new List<object>();//string OR Expression
                    exprObjs.Add("COUNT(");
                    exprObjs.Add(expr.Arguments[0]);
                    exprObjs.Add(")+2");
                    return exprObjs;
                });
            });
            SqlDbExtensions.RegisterDbMember<SqlConnection>((members) => {
                foreach (var item in members)
                {
                    var member = item.Key;
                    Console.WriteLine($"{member.DeclaringType}.{member.Name}");
                }
            });


            await InsertAsync(db);
            await SelectAsync(db);
            await UpdateAsync(db);
            await DeleteAsync(db);


            //Transaction 
            //out ITransactionFactory use AsyncLocal<>
            //如果不介意性能 不需要分开定义 db,dbTx 
            //If you don't mind performance There is no need to define separately db,dbTx 
            var dbTx = SqlDb.Create<SqlConnection>(connectionString, (cmd) => {
                Console.WriteLine(cmd.CommandText);
            }, out var txFactory);

            await TransactionAsync(dbTx, txFactory);
        }
        public static async Task RunMySql()
        {
            var builder = new MySqlConnectionStringBuilder();
            builder.Database = "testdb";
            builder.Server = "49.232.79.230";
            builder.Port = 3306;
            builder.UserID = "root";
            builder.Password = "qx(62Bjd]hKu";
            var connectionString = builder.ToString();
            Console.WriteLine(connectionString);


            var db = SqlDb.Create<MySqlConnection>(connectionString, (cmd) => {
                Console.WriteLine(cmd.CommandText);
            });

            //Number of rows affected
            await db.ExecuteAsync("SELECT 'ZhangHe'");
            var stringParam = "ZhangHe";
            await db.ExecuteAsync("SELECT @String", new { String = stringParam });
            await db.ExecuteAsync("SELECT @String", new List<(string, object)>() { ("String", stringParam) });
            await db.ExecuteFormatAsync($"SELECT {stringParam}");
            //Model
            var exe1 = await db.ExecuteAsync<string>("SELECT 'ZhangHe'");
            var exe2 = await db.ExecuteAsync<string>("SELECT @String", new { String = stringParam });
            var exe3 = await db.ExecuteAsync<string>("SELECT @String", new List<(string, object)>() { ("String", stringParam) });
            var exe4 = await db.ExecuteFormatAsync<string>($"SELECT {stringParam}");
            (var name, var age) = await db.ExecuteAsync<string, int>("SELECT @Name;SELECT @Age", new { Name = stringParam, Age = int.MinValue });
            (var names, var ages, var ids) = await db.ExecuteAsync<List<string>, List<int>, List<int>>("SELECT @Name;SELECT @Age;SELECT @Id", new { Id = 1024, Name = stringParam, Age = int.MinValue });


            try { await db.ExecuteAsync("DROP TABLE `Custom`"); } catch { }
            try { await db.ExecuteAsync("DROP TABLE `CustomDetail`"); } catch { }
            try { await db.ExecuteAsync("DROP TABLE `Address`"); } catch { }
            try { await db.ExecuteAsync("DROP TABLE `Order`"); } catch { }
            await db.ExecuteAsync("CREATE TABLE `Custom`(`Id` int(11) NOT NULL AUTO_INCREMENT,`Name` varchar(500),`Age` int(11),PRIMARY KEY (`Id`))");
            await db.ExecuteAsync("CREATE TABLE `CustomDetail`(`CustomId` int(11),`NullInt` int(11),`Bool` int(11),PRIMARY KEY (`CustomId`))");
            await db.ExecuteAsync("CREATE TABLE `Address`(`OrderId` int(11),`Name` varchar(500),`Detail` varchar(500),`Mobile` varchar(500),`Sheng` varchar(500),`Shi` varchar(500),`Xian` varchar(500))");
            await db.ExecuteAsync("CREATE TABLE `Order`(`Id` int(11) NOT NULL AUTO_INCREMENT,`Name` varchar(500),`Price` int(11),`CreateTime` datetime,`CustomId` int(11),`State` int(11),`JsonData` varchar(500),PRIMARY KEY (`Id`))");


            //SqlDbExtensions.RegisterTable(type => type.Name);
            //SqlDbExtensions.RegisterProperty(property => property.Name);
            //SqlDbExtensions.RegisterIdentity
            SqlDbExtensions.RegisterDbParameter<MySqlConnection, JsonData>((value) => {
                return JsonWriter.ToJson<JsonData>((JsonData)value);
            });
            //(reader,i)=field,(reader)=model Mapper
            SqlDbExtensions.RegisterDbReader<MySqlDataReader, JsonData>((reader, i) => {
                var json = reader.GetString(i);
                return JsonReader.FromJson<JsonData>(json);
            });
            SqlDbExtensions.RegisterDbMethod<MySqlConnection>((methods) => {
                foreach (var item in methods)
                {
                    var method = item.Key;
                    Console.WriteLine($"{method.DeclaringType}.{method.Name}()");
                }
                methods.Add(typeof(MyExpressionEx).GetMethod("CountPlus1"), (expr) => {
                    return new object[] { "COUNT(", expr.Arguments[1], ")+1" };//string OR Expression
                });
                methods.Add(typeof(MyExpressionEx).GetMethod("CountPlus2"), (expr) => {
                    var exprObjs = new List<object>();//string OR Expression
                    exprObjs.Add("COUNT(");
                    exprObjs.Add(expr.Arguments[0]);
                    exprObjs.Add(")+2");
                    return exprObjs;
                });
            });
            SqlDbExtensions.RegisterDbMember<MySqlConnection>((members) => {
                foreach (var item in members)
                {
                    var member = item.Key;
                    Console.WriteLine($"{member.DeclaringType}.{member.Name}");
                }
            });


            await InsertAsync(db);
            await SelectAsync(db);
            await UpdateAsync(db);
            await DeleteAsync(db);


            //Transaction 
            //out ITransactionFactory use AsyncLocal<>
            //如果不介意性能 不需要分开定义 db,dbTx 
            //If you don't mind performance There is no need to define separately db,dbTx 
            var dbTx = SqlDb.Create<MySqlConnection>(connectionString, (cmd) => {
                Console.WriteLine(cmd.CommandText);
            }, out var txFactory);

            await TransactionAsync(dbTx, txFactory);
        }
        public static async Task RunPostgre()
        {
            var builder = new NpgsqlConnectionStringBuilder();
            builder.Database = "testdb";
            builder.Host = "localhost";
            builder.Port = 5432;
            builder.Username = "postgres";
            builder.Password = "qx(62Bjd]hKu";
            var connectionString = builder.ToString();
            Console.WriteLine(connectionString);


            var db = SqlDb.Create<NpgsqlConnection>(connectionString, (cmd) => {
                Console.WriteLine(cmd.CommandText);
            });

            //Number of rows affected
            await db.ExecuteAsync("SELECT 'ZhangHe'");
            var stringParam = "ZhangHe";
            await db.ExecuteAsync("SELECT @String", new { String = stringParam });
            await db.ExecuteAsync("SELECT @String", new List<(string, object)>() { ("String", stringParam) });
            await db.ExecuteFormatAsync($"SELECT {stringParam}");
            //Model
            var exe1 = await db.ExecuteAsync<string>("SELECT 'ZhangHe'");
            var exe2 = await db.ExecuteAsync<string>("SELECT @String", new { String = stringParam });
            var exe3 = await db.ExecuteAsync<string>("SELECT @String", new List<(string, object)>() { ("String", stringParam) });
            var exe4 = await db.ExecuteFormatAsync<string>($"SELECT {stringParam}");
            (var name, var age) = await db.ExecuteAsync<string, int>("SELECT @Name;SELECT @Age", new { Name = stringParam, Age = int.MinValue });
            (var names, var ages, var ids) = await db.ExecuteAsync<List<string>, List<int>, List<int>>("SELECT @Name;SELECT @Age;SELECT @Id", new { Id = 1024, Name = stringParam, Age = int.MinValue });


            try { await db.ExecuteAsync("DROP TABLE \"Custom\""); } catch { }
            try { await db.ExecuteAsync("DROP TABLE \"CustomDetail\""); } catch { }
            try { await db.ExecuteAsync("DROP TABLE \"Address\""); } catch { }
            try { await db.ExecuteAsync("DROP TABLE \"Order\""); } catch { }
            await db.ExecuteAsync("CREATE TABLE \"Custom\"(\"Id\" integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1),\"Name\" text,\"Age\" integer)");
            await db.ExecuteAsync("CREATE TABLE \"CustomDetail\"(\"CustomId\" integer,\"NullInt\" integer,\"Bool\" boolean)");
            await db.ExecuteAsync("CREATE TABLE \"Address\"(\"OrderId\" integer,\"Name\" text,\"Detail\" text,\"Mobile\" text,\"Sheng\" text,\"Shi\" text,\"Xian\" text)");
            await db.ExecuteAsync("CREATE TABLE \"Order\"(\"Id\" integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1),\"Name\" text,\"Price\" integer,\"CreateTime\" DATE,\"CustomId\" integer,\"State\" integer,\"JsonData\" text)");


            //SqlDbExtensions.RegisterTable(type => type.Name);
            //SqlDbExtensions.RegisterProperty(property => property.Name);
            //SqlDbExtensions.RegisterIdentity
            SqlDbExtensions.RegisterDbParameter<NpgsqlConnection, JsonData>((value) => {
                return JsonWriter.ToJson<JsonData>((JsonData)value);
            });
            //(reader,i)=field,(reader)=model Mapper
            SqlDbExtensions.RegisterDbReader<NpgsqlDataReader, JsonData>((reader, i) => {
                var json = reader.GetString(i);
                return JsonReader.FromJson<JsonData>(json);
            });
            SqlDbExtensions.RegisterDbMethod<NpgsqlConnection>((methods) => {
                foreach (var item in methods)
                {
                    var method = item.Key;
                    Console.WriteLine($"{method.DeclaringType}.{method.Name}()");
                }
                methods.Add(typeof(MyExpressionEx).GetMethod("CountPlus1"), (expr) => {
                    return new object[] { "COUNT(", expr.Arguments[1], ")+1" };//string OR Expression
                });
                methods.Add(typeof(MyExpressionEx).GetMethod("CountPlus2"), (expr) => {
                    var exprObjs = new List<object>();//string OR Expression
                    exprObjs.Add("COUNT(");
                    exprObjs.Add(expr.Arguments[0]);
                    exprObjs.Add(")+2");
                    return exprObjs;
                });
            });
            SqlDbExtensions.RegisterDbMember<NpgsqlConnection>((members) => {
                foreach (var item in members)
                {
                    var member = item.Key;
                    Console.WriteLine($"{member.DeclaringType}.{member.Name}");
                }
            });


            await InsertAsync(db);
            await SelectAsync(db);
            await UpdateAsync(db);
            await DeleteAsync(db);


            //Transaction 
            //out ITransactionFactory use AsyncLocal<>
            //如果不介意性能 不需要分开定义 db,dbTx 
            //If you don't mind performance There is no need to define separately db,dbTx 
            var dbTx = SqlDb.Create<NpgsqlConnection>(connectionString, (cmd) => {
                Console.WriteLine(cmd.CommandText);
            }, out var txFactory);

            await TransactionAsync(dbTx, txFactory);
        }
        public static async Task RunOracle()
        {
            var builder = new OracleConnectionStringBuilder();
            builder.DataSource = "49.232.79.230/orcl";
            builder.UserID = "system";
            builder.Password = "qx(62Bjd]hKu";
            var connectionString = builder.ToString();
            Console.WriteLine(connectionString);

            var db = SqlDb.Create<OracleConnection>(connectionString, (cmd) => {
                Console.WriteLine(cmd.CommandText);
            });

            //Number of rows affected
            await db.ExecuteAsync("SELECT 'ZhangHe' FROM DUAL");
            var stringParam = "ZhangHe";
            await db.ExecuteAsync("SELECT :String FROM DUAL", new { String = stringParam });
            await db.ExecuteAsync("SELECT :String FROM DUAL", new List<(string, object)>() { ("String", stringParam) });
            await db.ExecuteFormatAsync($"SELECT {stringParam} FROM DUAL");
            //Model
            var exe1 = await db.ExecuteAsync<string>("SELECT 'ZhangHe' FROM DUAL");
            var exe2 = await db.ExecuteAsync<string>("SELECT :String FROM DUAL", new { String = stringParam });
            var exe3 = await db.ExecuteAsync<string>("SELECT :String FROM DUAL", new List<(string, object)>() { ("String", stringParam) });
            var exe4 = await db.ExecuteFormatAsync<string>($"SELECT {stringParam} FROM DUAL");
            (var name, var age) = await db.ExecuteAsync<string, int>("BEGIN OPEN :R1 FOR SELECT :Name FROM DUAL;OPEN :R2 FOR SELECT :Age FROM DUAL;END;", new
            {
                R1 = new OracleParameter() { OracleDbType = OracleDbType.RefCursor, Direction = ParameterDirection.Output },
                R2 = new OracleParameter() { OracleDbType = OracleDbType.RefCursor, Direction = ParameterDirection.Output },
                Name = stringParam,
                Age = int.MinValue
            });


            try { await db.ExecuteAsync("DROP TABLE \"SYSTEM\".\"Custom\""); } catch { }
            try { await db.ExecuteAsync("DROP TABLE \"SYSTEM\".\"CustomDetail\""); } catch { }
            try { await db.ExecuteAsync("DROP TABLE \"SYSTEM\".\"Address\""); } catch { }
            try { await db.ExecuteAsync("DROP TABLE \"SYSTEM\".\"Order\""); } catch { }
            await db.ExecuteAsync("CREATE TABLE \"SYSTEM\".\"Custom\"(\"Id\" NUMBER(*,0) GENERATED ALWAYS AS IDENTITY MINVALUE 1 MAXVALUE 9999999999999999999999999999 INCREMENT BY 1 START WITH 1 CACHE 20 NOORDER  NOCYCLE  NOKEEP  NOSCALE ,\"Name\" VARCHAR2(500 BYTE),\"Age\" NUMBER(*,0))");
            await db.ExecuteAsync("CREATE TABLE \"SYSTEM\".\"CustomDetail\"(\"CustomId\" NUMBER(*,0),\"NullInt\" NUMBER(*,0),\"Bool\" NUMBER(1))");
            await db.ExecuteAsync("CREATE TABLE \"SYSTEM\".\"Address\"(\"OrderId\"  NUMBER(*,0),\"Name\" VARCHAR2(500 BYTE),\"Detail\" VARCHAR2(500 BYTE),\"Mobile\" VARCHAR2(500 BYTE),\"Sheng\" VARCHAR2(500 BYTE),\"Shi\" VARCHAR2(500 BYTE),\"Xian\" VARCHAR2(500 BYTE))");
            await db.ExecuteAsync("CREATE TABLE \"SYSTEM\".\"Order\"(\"Id\" NUMBER(*,0) GENERATED ALWAYS AS IDENTITY MINVALUE 1 MAXVALUE 9999999999999999999999999999 INCREMENT BY 1 START WITH 1 CACHE 20 NOORDER  NOCYCLE  NOKEEP  NOSCALE ,\"Name\" VARCHAR2(500 BYTE),\"Price\" NUMBER(*,0),\"CreateTime\" DATE,\"CustomId\" NUMBER(*,0),\"State\" NUMBER(*,0),\"JsonData\" VARCHAR2(500 BYTE))");


            //SqlDbExtensions.RegisterTable(type => type.Name);//ToUpper()
            //SqlDbExtensions.RegisterProperty(property => property.Name);
            //SqlDbExtensions.RegisterIdentity
            SqlDbExtensions.RegisterDbParameter<OracleConnection, JsonData>((value) => {
                return JsonWriter.ToJson<JsonData>((JsonData)value);
            });
            //(reader,i)=field,(reader)=model Mapper
            SqlDbExtensions.RegisterDbReader<OracleDataReader, JsonData>((reader, i) => {
                var json = reader.GetString(i);
                return JsonReader.FromJson<JsonData>(json);
            });
            //如果支持bool<=>int 不需要
            //If supported bool<=>int,not required
            SqlDbExtensions.RegisterDbParameter<OracleConnection, bool>((value) => {
                return (bool)value ? 1 : 0;
            });
            SqlDbExtensions.RegisterDbReader<OracleDataReader, bool>((reader, i) => {
                return reader.GetInt32(i) == 1 ? true : false;
            });

            SqlDbExtensions.RegisterDbMethod<OracleConnection>((methods) => {
                foreach (var item in methods)
                {
                    var method = item.Key;
                    Console.WriteLine($"{method.DeclaringType}.{method.Name}()");
                }
                methods.Add(typeof(MyExpressionEx).GetMethod("CountPlus1"), (expr) => {
                    return new object[] { "COUNT(", expr.Arguments[1], ")+1" };//string OR Expression
                });
                methods.Add(typeof(MyExpressionEx).GetMethod("CountPlus2"), (expr) => {
                    var exprObjs = new List<object>();//string OR Expression
                    exprObjs.Add("COUNT(");
                    exprObjs.Add(expr.Arguments[0]);
                    exprObjs.Add(")+2");
                    return exprObjs;
                });
            });
            SqlDbExtensions.RegisterDbMember<OracleConnection>((members) => {
                foreach (var item in members)
                {
                    var member = item.Key;
                    Console.WriteLine($"{member.DeclaringType}.{member.Name}");
                }
            });


            await InsertAsync(db);
            await SelectAsync(db);
            await UpdateAsync(db);
            await DeleteAsync(db);


            //Transaction 
            //out ITransactionFactory use AsyncLocal<>
            //如果不介意性能 不需要分开定义 db,dbTx 
            //If you don't mind performance There is no need to define separately db,dbTx 
            var dbTx = SqlDb.Create<OracleConnection>(connectionString, (cmd) => {
                Console.WriteLine(cmd.CommandText);
            }, out var txFactory);

            await TransactionAsync(dbTx, txFactory);
        }
        private static async Task InsertAsync(SqlDb db)
        {
            var orders = new List<Order>();
            var customDetails = new List<CustomDetail>();
            for (int i = 0; i < 100; i++)
            {
                var custom = new Custom() { Name = $"Custom{i}", Age = i };
                custom.Id = await db.InsertIdentityAsync<Custom, int>(custom, (c) => SqlDbExtensions.Except(c.Id));
                var random = new Random().Next(0, 9);
                for (int j = 0; j < random; j++)
                {
                    var order = new Order()
                    {
                        Name = $"Order{i}-{j}",
                        CreateTime = DateTime.Now,
                        CustomId = custom.Id,
                        Price = j,
                        State = OrderState.State3,
                        JsonData = new JsonData()
                        {
                            String = "JsonString--XYZ",
                            Long = long.MaxValue
                        }
                    };
                    orders.Add(order);
                }
                customDetails.Add(new CustomDetail()
                {
                    CustomId = custom.Id,
                    Bool = false,
                    NullInt = null,
                    
                });
            }
            await db.InsertRangeAsync<Order>(orders, o => SqlDbExtensions.Except(o.Id));
            await db.InsertRangeAsync<CustomDetail>(customDetails, c => new { c.CustomId, c.Bool, c.NullInt });


            var selectOrders = await db.SelectAsync<Order>((o, s) => o, null);
            foreach (var selectOrder in selectOrders)
            {
                var detail = new string((char)new Random().Next(1, 127), 10);
                await db.InsertAsync<Address>(s => new Address()
                {
                    OrderId = selectOrder.Id,
                    Mobile = selectOrder.Id.ToString(),
                    Sheng = "Sheng",
                    Shi = "Shi",
                    Xian = "Xian",
                    Name = Guid.NewGuid().ToString(),
                    Detail = detail
                });
            }
        }
        private static async Task SelectAsync(SqlDb db)
        {
            //Single
            var order1 = await db.SelectSingleAsync<Order>((o, s) => o, 11);
            var order2 = await db.SelectSingleAsync<Order>((o, s) => s.Navigate(o), 12);
            var order3 = await db.SelectSingleAsync<Order>((o, s) => s.Navigate(o), (o, s) => o.Id == 13);
            var order4 = await db.SelectSingleAsync<Order>((o, s) => new Order()
            {
                Custom = o.Custom,
                Address = o.Address,
                Id = o.Id
            }, 14);
            var order5 = await db.SelectSingleAsync<Order>((o, s) => new Order()
            {
                Id = o.Id,
                Custom = s.Navigate(o.Custom)
            }, 15);
            var order6 = await db.SelectAsync<Order, Order>((o, s) => o, (o, s) => o.Id == 16);//equivalence


            //List<>
            var orders1 = await db.SelectAsync<Order>((o, s) => o, null);
            var orders2 = await db.SelectAsync<Order>((o, s) => s.Navigate(o), null);
            var orders3 = await db.SelectAsync<Order>((o, s) => s.Navigate(o), (o, s) => o.Id > 0);
            var orders4 = await db.SelectAsync<Order>((o, s) => s.Navigate(o), (o, s) => o.Id > 0, (o, s) => s.Desc(o.Custom.Detail.CustomId));
            var orders5 = await db.SelectAsync<Order>((o, s) => new Order() { Id = o.Id }, (o, s) => o.Id > 0, (o, s) => s.Desc(o.Id));
            var orders6 = await db.SelectAsync<Order, List<Order>>((o, s) => o, null);//equivalence


            //Paged
            (var orders7, var count7) = await db.SelectPagedAsync<Order>(10, 30, (o, s) => o, null, (o, s) => s.Desc(o.Id).Asc(o.Name));
            (var orders8, var count8) = await db.SelectPagedAsync<Order>(10, 30, (o, s) => s.Navigate(o), null, (o, s) => s.Desc(o.Id).Desc(o.Custom.Detail.CustomId));


            //where
            var where = db.Where<Order>();//Expression<Func<Order, SqlExpression, bool>> where = null;
            where = where.And((o, s) => o.Id > 0);
            where = where.And((o, s) => o.Name.Length > 0);
            where = where.AndIf(string.IsNullOrEmpty(""), (o, s) => o.CustomId > 0);
            where = where.Or((o, s) => o.Custom.Id < 0);

            var orders9 = await db.SelectAsync<Order>((o, s) => o, where);


            var orders10 = await db.SelectAsync<Order, Stack<Order>>(0, 10, (o, s) => s.Navigate(o), null, (o, s) => s.Desc(o.Id));
            var orders11 = await db.SelectAsync<Order, Queue<Order>>((o, s) => s.Navigate(o), (o, s) => o.Id > 20);

            //select .Sql<>
            var sqlSelect1= await db.SelectAsync<Order, DataTable>((o, s) => s.Sql<object>($"{o.Id},{o.Name}"), null);
            var sqlSelect2 = await db.SelectAsync<Order, DataTable>((o, s) => s.Sql<object>($"{o}"), null);
            //s.Select(object) field As
            var sqlSelect3 = await db.SelectAsync<Order, DataTable>((o, s) => s.Sql<object>($"{s.Select(o)}"), null);
            var sqlSelect4 = await db.SelectAsync<Order, DataTable>((o, s) => s.Sql<object>($"{s.Select(s.Navigate(o))}"), null);


            //Mixed use
            //recommend
            var sqlCountPlus1 = await db.SelectAsync<Order, int>((o, s) => s.Sql<int>($"COUNT({o.Id})+1"), null);
            var sqlCountPlus2 = await db.SelectAsync<Order, int>((o, s) => s.Sql<int>($"COUNT({o.Id})+2"), null);

            //根据需求扩展
            //According to demand
            var countPlus1 = await db.SelectAsync<Order, int>((o, s) => s.CountPlus1(o.Id), null);
            var countPlus2 = await db.SelectAsync<Order, int>((o, s) => MyExpressionEx.CountPlus2(o.Id), null);


            var sqlMixed1 = await db.SelectAsync<Order>((o, s) => o, (o, s) => o.Id > 0 && s.Sql<bool>($"{o.Id}>0"));
            var sqlMixed2 = await db.SelectAsync<Order, (int, string)>((o, s) => s.Sql<object>($"{o.CustomId},{o.Name}"), (o, s) => s.Sql<bool>($"{o.Id}=99"));
            var sqlMixed3 = await db.SelectAsync<Order>((o, s) => o, null, (o, s) => s.Sql<object>($"{o.Id},{o.Name} DESC"));
            var sqlMixed4 = await db.SelectAsync<Order, List<(string, int)>>((o, s) => s.Sql<object>($"{o.Name},{o.Name.Length}"), null);


            var i1 = await db.SelectAsync<Order, int>((o, s) => s.Sql<int>($"MAX({o.Id})"), null);
            var i2 = await db.SelectAsync<Order, int>((o, s) => s.Max(o.Id), null);
            var i3 = await db.SelectAsync<Order, int>((o, s) => s.Sum(o.Price), null);
            var i4 = await db.SelectAsync<Order, int>((o, s) => s.Min(o.Id), null);

            //
            //var avg = await db.SelectAsync<Order, int>((o, s) => s.Avg(o.Price), null);
            var count1 = await db.SelectAsync<Order, int>((o, s) => s.Count(), null);
            var count2 = await db.SelectAsync<Order, int>((o, s) => s.Count(o.Id), null);
            var exists1 = await db.SelectAsync<Order>((o, s) => o, (o, s) => s.Exists(s.Select<Order, object>(o1 => o1, o1 => o1.Id > 100000)));
            var exists2 = await db.SelectAsync<Order>((o, s) => o, (o, s) => s.NotExists(s.Select<Order, object>(o1 => o1, o1 => o1.Id > 100000)));
            var like1 = await db.SelectAsync<Order>((o, s) => o, (o, s) => s.Like(o.Name, "%1%"));
            var like2 = await db.SelectAsync<Order>((o, s) => o, (o, s) => s.NotLike(o.Name, "%1%"));

            var between1 = await db.SelectAsync<Order>((o, s) => o, (o, s) => s.Between(o.Id, 1, 12));
            var between2 = await db.SelectAsync<Order>((o, s) => o, (o, s) => s.NotBetween(o.Id, 1, 12));

            //support IS NULL, IS NOT NULL
            var equals1 = await db.SelectAsync<Order>((o, s) => o, (o, s) => s.Equals(o.Id, null));
            var equals2 = await db.SelectAsync<Order>((o, s) => o, (o, s) => s.NotEquals(o.Id, null));

            var isNullOrEmpty = await db.SelectAsync<Order>((o, s) => o, (o, s) => string.IsNullOrEmpty(o.Name));

            var toUpper = await db.SelectAsync<Order, string>((o, s) => o.Name.ToUpper(), (o, s) => o.Id == 10);
            var toLower = await db.SelectAsync<Order, string>((o, s) => o.Name.ToLower(), (o, s) => o.Id == 10);

            //var trim = await db.SelectAsync<Order, string>((o, s) => o.Name.Trim(), (o, s) => o.Id == 10);
            var trimStart = await db.SelectAsync<Order, string>((o, s) => o.Name.TrimStart(), (o, s) => o.Id == 10);
            var trimEnd = await db.SelectAsync<Order, string>((o, s) => o.Name.TrimEnd(), (o, s) => o.Id == 10);

            var replace1 = await db.SelectAsync<Order, string>((o, s) => o.Name.Replace("Order", "XX"), (o, s) => o.Id == 10);
            var replace2 = await db.SelectAsync<Order, string>((o, s) => o.Name.Replace("0", "B"), (o, s) => o.Id == 10);

            var length1 = await db.SelectAsync<Order, int>((o, s) => o.Name.Length, (o, s) => o.Id == 10);

            var time1 = await db.SelectAsync<Order>((o, s) => o, (o, s) => o.CreateTime < DateTime.Now);
            var time2 = await db.SelectAsync<Order>((o, s) => o, (o, s) => o.CreateTime < DateTime.UtcNow);
            var time3 = await db.SelectAsync<Order>((o, s) => o, (o, s) => o.CreateTime < DateTime.Today);
            var time4 = await db.SelectAsync<Order, DateTime>((o, s) => o.CreateTime.Date, (o, s) => o.Id == 10);
            var time5 = await db.SelectAsync<Order, int>((o, s) => o.CreateTime.Year, (o, s) => o.Id == 10);
            var time6 = await db.SelectAsync<Order, int>((o, s) => o.CreateTime.Month, (o, s) => o.Id == 10);
            var time7 = await db.SelectAsync<Order, int>((o, s) => o.CreateTime.Day, (o, s) => o.Id == 10);
            var time8 = await db.SelectAsync<Order>((o, s) => o, (o, s) => s.Between(o.CreateTime, DateTime.Now.Day(), DateTime.Now.Day(1)));

            var concat1 = await db.SelectAsync<Order, string>((o, s) => string.Concat(o.Name, o.Name), (o, s) => o.Id == 10);
            var concat2 = await db.SelectAsync<Order, string>((o, s) => string.Concat(o.Name, o.Name, o.Name), (o, s) => o.Id == 10);
            var concat3 = await db.SelectAsync<Order, string>((o, s) => string.Concat(o.Name, o.Name, o.Name, o.Name), (o, s) => o.Id == 10);

            var indexOf1 = await db.SelectAsync<Order>((o, s) => o, (o, s) => o.Name.IndexOf("0") >= 0);
            var indexOf2 = await db.SelectAsync<Order>((o, s) => o, (o, s) => o.Name.IndexOf("x") >= 0);

            var contains1 = await db.SelectAsync<Order>((o, s) => o, (o, s) => o.Name.Contains("1"));
            var contains2 = await db.SelectAsync<Order>((o, s) => o, (o, s) => o.Name.Contains("Order"));

            var substring1 = await db.SelectAsync<Order, string>((o, s) => o.Name.Substring(1), (o, s) => o.Id == 10);
            var substring2 = await db.SelectAsync<Order, string>((o, s) => o.Name.Substring(2, 1), (o, s) => o.Id == 10);

            var startsWith1 = await db.SelectAsync<Order>((o, s) => o, (o, s) => o.Name.StartsWith("Or"));
            var endsWith1 = await db.SelectAsync<Order>((o, s) => o, (o, s) => o.Name.EndsWith("0"));

            var cast1 = await db.SelectAsync<Order, int>((o, s) => s.Cast<int>(o.Id), (o, s) => o.Id == 10);
            var cast2 = await db.SelectAsync<Order, long>((o, s) => s.Cast<long>(o.Id), (o, s) => o.Id == 10);
            var cast3 = await db.SelectAsync<Order, double>((o, s) => s.Cast<double>(o.Id), (o, s) => o.Id == 10);
            var cast4 = await db.SelectAsync<Order, decimal>((o, s) => s.Cast<decimal>(int.MaxValue), (o, s) => o.Id == 10);
            var cast5 = await db.SelectAsync<Order, string>((o, s) => s.Cast<string>(o.Id), (o, s) => o.Id == 10);


            //In NotIn
            var orders12 = await db.SelectAsync<Order>((o, s) => o, (o, s) => s.In(o.Id, s.Sql<object>($"{1},{2},{3}")));
            var orders13 = await db.SelectAsync<Order>((o, s) => o, (o, s) => s.In(o.Id, new[] { 1, 2, 3 }));
            var ids = new List<int>();
            for (int i = 0; i < 129; i++)
            {
                ids.Add(i);
            }
            var orders14 = await db.SelectAsync<Order>((o, s) => o, (o, s) => s.In(o.Id, ids));

            var orders15 = await db.SelectAsync<Order>((o, s) => o, (o, s) => s.NotIn(o.Id, s.Sql<object>($"{1},{2},{3},{4},{5},{6},{7},{8}")));


            //sub Select
            var orders16 = await db.SelectAsync<Order>((o, s) => o, (o, s) => s.In(o.Id, s.Select<Order, object>(o1 => o1.Id, null)));
            var orders17 = await db.SelectAsync<Order>((o, s) => o, (o, s) => s.In(o.Id, s.Select<Order, object>(o1 => o1.Id, null, null)));//o1=>s.Desc(o1.Id)
            var orders18 = await db.SelectAsync<Order>((o, s) => o, (o, s) => s.In(o.Id, s.Select<Order, object>(o1 => o1.Id, null, o1 => o1.Id, o1 => o1.Id > 0, null)));


            var a1 = await db.SelectAsync<Order, ISet<Order>>((o, s) => s.Navigate(o), null);
            var a2 = await db.SelectAsync<Order, HashSet<Order>>((o, s) => s.Navigate(o), null);
            var a3 = await db.SelectAsync<Order, IList<Order>>((o, s) => s.Navigate(o), null);
            var a4 = await db.SelectAsync<Order, List<Order>>((o, s) => s.Navigate(o), null);
            var a5 = await db.SelectAsync<Order, Queue<Order>>((o, s) => s.Navigate(o), null);
            var a6 = await db.SelectAsync<Order, Stack<Order>>((o, s) => s.Navigate(o), null);
            var a7 = await db.SelectAsync<Order, DataTable>((o, s) => s.Navigate(o), null);
            var a8 = await db.SelectAsync<Order, (int, string, int, int)>((o, s) => new object[] { o.Id, o.Name, o.Address.OrderId, o.Custom.Id }, null);
            var a9 = await db.SelectAsync<Order, Tuple<int, string, int, int>>((o, s) => new object[] { o.Id, o.Name, o.Address.OrderId, o.Custom.Id }, null);
            var a10 = await db.SelectAsync<Order, List<(int, string, int, int)>>((o, s) => new object[] { o.Id, o.Name, o.Address.OrderId, o.Custom.Id }, null);
            var a11 = await db.SelectAsync<Order, List<Tuple<int, string, int, int>>>((o, s) => new object[] { o.Id, o.Name, o.Address.OrderId, o.Custom.Id }, null);
        }
        private static async Task UpdateAsync(SqlDb db)
        {
            var order = await db.SelectSingleAsync<Order>((o, s) => s.Navigate(o), 50);


            var u1 = await db.UpdateAsync<Order>((o, s) => new Order
            {
                CreateTime = DateTime.Now,
                Address = order.Address,
                Name = order.Name,
            }, order.Id);

            var now = DateTime.Now;
            var u2 = await db.UpdateAsync<Order>((o, s) => new Order
            {
                CreateTime = now,//param
                Address = order.Address,
                Name = order.Name,
            }, (o, s) => o.Id == order.Id);

            var u3 = await db.UpdateAsync<Order>(order, o => new { o.Name, o.CreateTime });

            var u4 = await db.UpdateAsync<Order>(order, o => new { o.Name, o.CreateTime }, (o, s) => o.Id == order.Id);

            var u5 = await db.UpdateAsync<Order>(order, o => SqlDbExtensions.Except(o.Id));

            var u6 = await db.UpdateAsync<Order>(order, o => SqlDbExtensions.Except(new { o.Id, o.CustomId }));

            var u7 = await db.UpdateAsync<Order>(order, o => SqlDbExtensions.Except(new { o.Id, o.CustomId }), (o, s) => o.Id == order.Id);
        }
        private static async Task DeleteAsync(SqlDb db)
        {
            var d1 = await db.DeleteAsync<Order>(int.MaxValue);
            var d2 = await db.DeleteAsync<Order>((o, s) => o.Id == int.MaxValue);
        }
        private static async Task TransactionAsync(SqlDb dbTx, ITransactionFactory txFactory)
        {
            try
            {
                await txFactory.ExecuteAsync(async () => {
                    await dbTx.DeleteAsync<Order>((o, s) => o.Id > 0);
                    await dbTx.DeleteAsync<Custom>((c, s) => c.Id > 0);

                    throw new Exception("Rollback");
                });
            }
            catch
            {
                //ignore Exception
            }

            var tx = txFactory.Create();//Create(IsolationLevel)
            await dbTx.DeleteAsync<Order>((o, s) => o.Id > 0);
            await dbTx.DeleteAsync<Custom>((c, s) => c.Id > 0);
            //tx.Commit();
            tx.Rollback();

            var count = await dbTx.SelectAsync<Order, int>((o, s) => s.Count(), null);
            Console.WriteLine(count);
        }
    }
    //默认类名是表名
    //The default class name is the table name
    //1.[DataTable(Name ="Order")]
    //2.SqlDbExtensions.RegisterTable(type => type.Name);
    public class Order
    {
        //字段属性 通过SqlDbExtensions.RegisterDbReader((reader, i)=>{})注册的类型
        //field properties By SqlDbExtensions.RegisterDbReader((reader, i)=>{})Register Type

        //默认主键是第一个字段属性
        //The default primary key is the first field property
        //1.RegisterIdentity
        public int Id { get; set; }
        //默认属性名是字段名
        //The default property name is the field name
        //1.[DataColumn(Name="Name")]
        //2.SqlDbExtensions.RegisterProperty(property => property.Name);
        public string Name { get; set; }
        //[IgnoreDataColumn]
        public int Price { get; set; }
        public DateTime CreateTime { get; set; }
        public int CustomId { get; set; }
        //非字段属性Non field properties
        //默认左连接条件 Default left join condition
        //1.存在字段(CustomId)=Custom属性名(Custom)+Custom类主键属性名(Id) 
        //Existing field (CustomId) = Custom Property Name (Custom) + Custom class primary key Property Name (Id)
        //2.Address类主键属性名(OrderId)=Order类名(Order)+Order类主键属性名(Id)
        //Address class primary key Property Name (OrderId) = Order class name (Order) + Order class primary key Property name (Id)
        //OR [DataColumn(Name= "CustomId")]
        public Custom Custom { get; set; }
        //OR [DataColumn(Name="Id")]
        public Address Address { get; set; }
        public OrderState State { get; set; }
        public JsonData JsonData { get; set; }
    }
    public enum OrderState
    {
        State1,
        State2,
        State3,
        State4
    }
    public class JsonData
    {
        public string String { get; set; }
        public long Long { get; set; }
    }
    public class Address
    {
        public int OrderId { get; set; }
        public string Name { get; set; }
        public string Detail { get; set; }
        public string Mobile { get; set; }
        public string Sheng { get; set; }
        public string Shi { get; set; }
        public string Xian { get; set; }
    }
    public class Custom
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Age { get; set; }
        public CustomDetail Detail { get; set; }
    }
    public class CustomDetail
    {
        public int CustomId { get; set; }
        public int? NullInt { get; set; }
        //public Guid Guid { get; set; }
        public bool Bool { get; set; }
    }
    public static class MyExpressionEx 
    {
        public static int CountPlus1(this SqlExpression @this, object param1) 
        {
            throw new InvalidOperationException(nameof(CountPlus1));
        }
        public static int CountPlus2(object param1)
        {
            throw new InvalidOperationException(nameof(CountPlus2));
        }
    }
}
