// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Common;
using ObjectCloud.ORM.DataAccess.DomainModel;

namespace ObjectCloud.ORM.DataAccess.Generator.SqLite
{
    public class EntityGenerator : ISubGenerator
    {
        public EntityGenerator() { }

        public EntityGenerator(
            Database database,
            string baseClassNamespace)
        {
            Database = database;
            BaseClassNamespace = baseClassNamespace;
        }

        public Database Database
        {
            get { return database; }
            set { database = value; }
        }
        private Database database;

        public string BaseClassNamespace
        {
            get { return baseClassNamespace; }
            set { baseClassNamespace = value; }
        }
        private string baseClassNamespace;

        public IEnumerable<string> Usings
        {
            get
            {
                yield return "MongoDB.Bson";
                yield return "MongoDB.Bson.Serialization";
                yield return "System";
                yield return "System.Collections.Generic";
                yield return "System.Data.Common";
                yield return "System.IO";
                yield return "System.Text";
                yield return "System.Threading";
                yield return "ObjectCloud.Common";
                yield return "ObjectCloud.Common.Threading";
                yield return "ObjectCloud.Interfaces.Database";
                yield return "ObjectCloud.ORM.DataAccess";
                yield return "ObjectCloud.ORM.DataAccess.WhereConditionals";
                yield return "ObjectCloud.ORM.DataAccess.SQLite";
            }
        }

        /// <summary>
        /// Specific results converters for certain types that SQLite borks
        /// </summary>
        private static readonly Dictionary<Type, string> QueryResultsConverters = new Dictionary<Type, string>();

        static EntityGenerator()
        {
            QueryResultsConverters[typeof(int)] = "Convert.ToInt32({0})";
        }

        /// <summary>
        /// Returns the appropriate conversion / cast for the expression given the expected type
        /// </summary>
        /// <param name="type"></param>
        /// <param name="expression"></param>
        /// <returns></returns>
        private static string GetResultConverter(Type type, string expression)
        {
            if (QueryResultsConverters.ContainsKey(type))
                return string.Format(QueryResultsConverters[type], expression);
            else
                return string.Format("(({0}){1})", type.FullName, expression);
        }

        public IEnumerable<string> Generate()
        {
            yield return "\tpublic class DatabaseConnectorFactory : " + BaseClassNamespace + ".IDatabaseConnectorFactory\n";
            yield return "\t{\n";
            yield return "        /// <summary>\n";
            yield return "        /// Used to connect to the embedded database\n";
            yield return "        /// </summary>\n";
            yield return "        public IEmbeddedDatabaseConnector EmbeddedDatabaseConnector\n";
            yield return "        {\n";
            yield return "            get { return _EmbeddedDatabaseConnector; }\n";
            yield return "            set { _EmbeddedDatabaseConnector = value; }\n";
            yield return "        }\n";
            yield return "        private IEmbeddedDatabaseConnector _EmbeddedDatabaseConnector;\n";
            yield return "\n";
            yield return "\t\tpublic IDatabaseConnector CreateConnectorForEmbedded(string path)\n";
            yield return "\t\t{\n";
            yield return "\t\t\treturn new DatabaseConnector(path, EmbeddedDatabaseConnector);\n";
            yield return "\t\t}\n";
            yield return "\t}\n";
            yield return "\t\t\n";
            yield return "\tpublic partial class DatabaseConnector : IDatabaseConnector\n";
            yield return "\t{\n";
            yield return "        /// <summary>\n";
            yield return "        /// Occurs after a transaction is committed\n";
            yield return "        /// </summary>\n";
            yield return "        public event EventHandler<IDatabaseConnector, EventArgs> DatabaseWritten;\n";
            yield return "\n";
            yield return "                internal void OnDatabaseWritten(EventArgs e)\n";
            yield return "                {\n";
            yield return "                    if (null != DatabaseWritten)\n";
            yield return "                        DatabaseWritten(this, e);\n";
            yield return "                }\n";
            yield return "\n";
            yield return "\t\tpublic DateTime LastModified\n";
            yield return "\t\t{\n";
            yield return "\t\t\tget { return File.GetLastWriteTimeUtc(Path); }\n";
            yield return "\t\t}\n";
            yield return "\n";        
            yield return "        /// <summary>\n";
            yield return "        /// Used to connect to the embedded database\n";
            yield return "        /// </summary>\n";
            yield return "        public IEmbeddedDatabaseConnector EmbeddedDatabaseConnector\n";
            yield return "        {\n";
            yield return "            get { return _EmbeddedDatabaseConnector; }\n";
            yield return "            set { _EmbeddedDatabaseConnector = value; }\n";
            yield return "        }\n";
            yield return "        private IEmbeddedDatabaseConnector _EmbeddedDatabaseConnector;\n";
            yield return "\n";
            yield return "\t\tprivate string Path;\n";
            yield return "\t\t\n";
            yield return "\t\tpublic DatabaseConnector(string path, IEmbeddedDatabaseConnector embeddedDatabaseConnector)\n";
            yield return "\t\t{\n";
            yield return "\t\t\tPath = path;\n";
            yield return "\t\t\tEmbeddedDatabaseConnector = embeddedDatabaseConnector;\n";
            yield return "\t\t\n";
            yield return "\t\t\tusing (ObjectCloud.Common.Threading.Timeout timeout = ObjectCloud.Common.Threading.Timeout.RunMax(TimeSpan.FromSeconds(3), delegate(Thread thread) { EventBus.OnFatalException(this, new EventArgs<Exception>(new CantOpenDatabaseException(\"Can't open \" + Path))); }))\n";
            yield return "\t\t\tusing (DbConnection connection = EmbeddedDatabaseConnector.Open(\"Data Source=\\\"\" + Path + \"\\\"\"))\n";
            yield return "\t\t\t\ttry\n";
            yield return "\t\t\t\t{\n";
            yield return "\t\t\t\t\tconnection.Open();\n";
            yield return "\t\t\t\t\ttimeout.Dispose();\n";
            yield return "\t\t\t\t\t\n";
            yield return "\t\t\t\t\t//YOU need to write the following function in a partial class.  It should perform automatic schema upgrades by\n";
            yield return "\t\t\t\t\t//looking at and setting PRAGMA user_version\n";
            yield return "\t\t\t\t\tDoUpgradeIfNeeded(connection);\n";
            yield return "\t\t\t\t}\n";
            yield return "\t\t\t\tfinally\n";
            yield return "\t\t\t\t{\n";
            yield return "\t\t\t\t\tconnection.Close();\n";
            yield return "\t\t\t\t}\n";
            yield return "\t\t}\n";
            yield return "\t\t\n";
            yield return "\t\tpublic IDatabaseConnection Connect()\n";
            yield return "\t\t{\n";
            yield return "\t\t\tusing (ObjectCloud.Common.Threading.Timeout timeout = ObjectCloud.Common.Threading.Timeout.RunMax(TimeSpan.FromSeconds(3), delegate(Thread thread) { EventBus.OnFatalException(this, new EventArgs<Exception>(new CantOpenDatabaseException(\"Can't open \" + Path))); }))\n";
            yield return "\t\t{\n";
            yield return "\t\t\tDbConnection connection = EmbeddedDatabaseConnector.Open(\"Data Source=\\\"\" + Path + \"\\\"\");\n";
            yield return "\t\t\n";
            yield return "\t\t\ttry\n";
            yield return "\t\t\t{\n";
            yield return "\t\t\t\tconnection.Open();\n";
            yield return "\t\t\n";
            yield return "\t\t\t\treturn new DatabaseConnection(connection, EmbeddedDatabaseConnector, this);\n";
            yield return "\t\t\t}\n";
            yield return "\t\t\tcatch\n";
            yield return "\t\t\t{\n";
            yield return "\t\t\t\tconnection.Close();\n";
            yield return "\t\t\t\tconnection.Dispose();\n";
            yield return "\t\t\n";
            yield return "\t\t\t\tthrow;\n";
            yield return "\t\t\t}\n";
            yield return "\t\t\t}\n";
            yield return "\t\t}\n";
            yield return "\t\t\n";
            yield return "\t\tpublic void Restore(string pathToRestoreFrom)\n";
            yield return "\t\t{\n";
            yield return "\t\t\tFile.Delete(Path);\n";
            yield return "\t\t\tFile.Copy(pathToRestoreFrom, Path);\n";
            yield return "\t\t}\n";
            yield return "\t}\n";
            yield return "\t\t\n";
            yield return "\tpartial class DatabaseTransaction : " + BaseClassNamespace + ".IDatabaseTransaction\n";
            yield return "\t{\n";
            yield return "        /// <summary>\n";
            yield return "        /// Used to connect to the embedded database\n";
            yield return "        /// </summary>\n";
            yield return "        public IEmbeddedDatabaseConnector EmbeddedDatabaseConnector\n";
            yield return "        {\n";
            yield return "            get { return _EmbeddedDatabaseConnector; }\n";
            yield return "            set { _EmbeddedDatabaseConnector = value; }\n";
            yield return "        }\n";
            yield return "        private IEmbeddedDatabaseConnector _EmbeddedDatabaseConnector;\n";
            yield return "\n";
            yield return "\t\tinternal DatabaseConnection DatabaseConnection;\n";
            yield return "\t\tinternal DbConnection connection;\n";
            yield return "\t\tinternal DbTransaction transaction;\n";
            yield return "\t\t\n";
            yield return "\t\tinternal DatabaseTransaction(DbConnection connection, IEmbeddedDatabaseConnector embeddedDatabaseConnector, DatabaseConnection databaseConnection)\n";
            yield return "\t\t{\n";
            yield return "\t\t\tDatabaseConnection = databaseConnection;\n";
            yield return "\t\t\tthis.connection = connection;\n";
            yield return "\t\t\ttransaction = connection.BeginTransaction();\n";
            yield return "\t\t\tEmbeddedDatabaseConnector = embeddedDatabaseConnector;\n";
            yield return "\t\t}\n";
            yield return "\t\t\n";
            yield return "\t\tpublic void Commit()\n";
            yield return "\t\t{\n";
            yield return "\t\t\ttransaction.Commit();\n";
            yield return "\t\t}\n";
            yield return "\t\t\n";
            yield return "\t\tpublic void Rollback()\n";
            yield return "\t\t{\n";
            yield return "\t\t\ttransaction.Rollback();\n";
            yield return "\t\t}\n";
            yield return "\t\t\n";
            yield return "\t\tpublic void Dispose()\n";
            yield return "\t\t{\n";
            yield return "\t\t\ttransaction.Dispose();\n";
            yield return "\t\t}\n";
            yield return "\t}\n";
            yield return "\t\t\n";
            yield return "\tpublic partial class DatabaseConnection : IDatabaseConnection\n";
            yield return "\t{\n";
            yield return "        /// <summary>\n";
            yield return "        /// Used to connect to the embedded database\n";
            yield return "        /// </summary>\n";
            yield return "        public IEmbeddedDatabaseConnector EmbeddedDatabaseConnector\n";
            yield return "        {\n";
            yield return "            get { return _EmbeddedDatabaseConnector; }\n";
            yield return "            set { _EmbeddedDatabaseConnector = value; }\n";
            yield return "        }\n";
            yield return "        private IEmbeddedDatabaseConnector _EmbeddedDatabaseConnector;\n";
            yield return "\n";
            yield return "\t\tDbConnection sqlConnection;\n";
            yield return "\t\tinternal DatabaseConnector DatabaseConnector;\n";
            yield return "\t\t\n";
            yield return "\t\tpublic DatabaseConnection(DbConnection sqlConnection, IEmbeddedDatabaseConnector embeddedDatabaseConnector, DatabaseConnector databaseConnector)\n";
            yield return "\t\t{\n";
            yield return "\t\t\tthis.sqlConnection = sqlConnection;\n";
            yield return "\t\t\tEmbeddedDatabaseConnector = embeddedDatabaseConnector;\n";
            yield return "\t\t\tDatabaseConnector = databaseConnector;\n";
            yield return "\t\t\t\n";

            foreach (Table table in Database.Tables)
            {
                yield return "\t\t\t_" + table.Name + "_Table = new " + table.Name + "_Table(sqlConnection, EmbeddedDatabaseConnector, databaseConnector);\n";
            }
            yield return "\t\t}\n";
            yield return "\t\t\n";
            yield return "\t\tpublic void Dispose()\n";
            yield return "\t\t{\n";
            yield return "\t\t\tDbConnection connection = sqlConnection;\n";
            yield return "\t\t\tif (null != connection)\n";
            yield return "\t\t\t\tif (connection == Interlocked.CompareExchange<DbConnection>(ref sqlConnection, null, connection))\n";
            yield return "\t\t\t\t{\n";
            yield return "\t\t\t\t\tconnection.Close();\n";
            yield return "\t\t\t\t\tconnection.Dispose();\n";
            yield return "\t\t\t\t\tGC.SuppressFinalize(this);\n";
            yield return "\t\t\t\t}\n";
            yield return "\t\t}\n";
            yield return "\t\t\n";
            yield return "\t\t~DatabaseConnection()\n";
            yield return "\t\t{\n";
            yield return "\t\t\tDispose();\n";
            yield return "\t\t}\n";
            yield return "\t\t\n";
            yield return "\t\tpublic T CallOnTransaction<T>(GenericArgumentReturn<" + BaseClassNamespace + ".IDatabaseTransaction, T> del)\n";
            yield return "\t\t{\n";
            yield return "\t\t    using (TimedLock.Lock(sqlConnection))\n";
            yield return "\t\t        using (DatabaseTransaction transaction = new DatabaseTransaction(sqlConnection, EmbeddedDatabaseConnector, this))\n";
            yield return "\t\t            try\n";
            yield return "\t\t            {\n";
            yield return "\t\t                return del(transaction);\n";
            yield return "\t\t            }\n";
            yield return "\t\t            catch\n";
            yield return "\t\t            {\n";
            yield return "\t\t                transaction.Rollback();\n";
            yield return "\t\t                throw;\n";
            yield return "\t\t            }\n";
            yield return "\t\t}\n";
            yield return "\t\t\n";
            yield return "\t\tpublic void CallOnTransaction(GenericArgument<" + BaseClassNamespace + ".IDatabaseTransaction> del)\n";
            yield return "\t\t{\n";
            yield return "\t\t    using (TimedLock.Lock(sqlConnection))\n";
            yield return "\t\t        using (DatabaseTransaction transaction = new DatabaseTransaction(sqlConnection, EmbeddedDatabaseConnector, this))\n";
            yield return "\t\t            try\n";
            yield return "\t\t            {\n";
            yield return "\t\t                del(transaction);\n";
            yield return "\t\t            }\n";
            yield return "\t\t            catch\n";
            yield return "\t\t            {\n";
            yield return "\t\t                transaction.Rollback();\n";
            yield return "\t\t                throw;\n";
            yield return "\t\t            }\n";
            yield return "\t\t}\n";
            yield return "\t\t\n";
            yield return "\t\tpublic DbConnection DbConnection\n";
            yield return "\t\t{\n";
            yield return "\t\t	get { return sqlConnection; }\n";
            yield return "\t\t}\n";
            yield return "\t\t\n";

            foreach (Table table in Database.Tables)
            {
                yield return "\t\tpublic " + BaseClassNamespace + "." + table.Name + "_Table " + table.Name + "\n";
                yield return "\t\t{\n";
                yield return "\t\t\tget { return _" + table.Name + "_Table; }\n";
                yield return "\t\t}\n";
                yield return "\t\tprivate " + table.Name + "_Table _" + table.Name + "_Table;\n";
            }

            yield return "\t\t\n";
            yield return "\t\tpublic void Vacuum()\n";
            yield return "\t\t{\n";
            yield return "\t\t    using (TimedLock.Lock(sqlConnection))\n";
            yield return "\t\t    {\n";
            yield return "\t\t		DbCommand command = sqlConnection.CreateCommand();\n";
            yield return "\t\t		command.CommandText = \"vacuum\";\n";
            yield return "\t\t		command.ExecuteNonQuery();\n";
            yield return "\t\t    }\n";
            yield return "\t\t}\n";
            yield return "\t\t\n";
            yield return "\t}\n";
            yield return "\t\t\n";

            foreach (Table table in Database.Tables)
            {
                List<string> columnNames = new List<string>();
                foreach (Column column in table.Columns)
                    columnNames.Add(column.Name);

                // Readable entity

                yield return "\tinternal class " + table.Name + "_Readable : I" + table.Name + "_Readable\n";
                yield return "\t{\n";

                foreach (Column column in table.Columns)
                {
                    yield return "\t\tpublic " + column.Type.TypeName + " " + column.Name + "\n";
                    yield return "\t\t{\n";
                    yield return "\t\t\tget { return _" + column.Name + "; }\n";
                    yield return "\t\t}\t\n";
                    yield return "\t\tinternal " + column.Type.TypeName + " _" + column.Name + " = default(" + column.Type.TypeName + ");\n";
                    yield return "\t\t\n";
                }

                yield return "\t}\n";
                yield return "\t\n";

                // Table accessor

                yield return "\tpublic partial class " + table.Name + "_Table : " + baseClassNamespace + "." + table.Name + "_Table\n";
                yield return "\t{\n";
                //yield return "        private Cache<ComparisonCondition, IEnumerable<I" +  table.Name + "_Readable>> SelectCache;\n";
                //yield return "        \n";
                yield return "        /// <summary>\n";
                yield return "        /// Used to connect to the embedded database\n";
                yield return "        /// </summary>\n";
                yield return "        public IEmbeddedDatabaseConnector EmbeddedDatabaseConnector\n";
                yield return "        {\n";
                yield return "            get { return _EmbeddedDatabaseConnector; }\n";
                yield return "            set { _EmbeddedDatabaseConnector = value; }\n";
                yield return "        }\n";
                yield return "        private IEmbeddedDatabaseConnector _EmbeddedDatabaseConnector;\n";
                yield return "\n";
                yield return "\t\tstatic " + table.Name + "_Table()\n";
                yield return "\t\t{\n";

                foreach (Column column in table.Columns)
                {
                    yield return "\t\t\t" + baseClassNamespace + "." + table.Name + "_Table._" + column.Name + " = ObjectCloud.ORM.DataAccess.Column.Construct<" + table.Name + "_Table, I" + table.Name + "_Writable, I" + table.Name + "_Readable>(\"" + column.Name + "\",\n";
                    yield return "\t\t\t\tdelegate(object writable, object value)\n";
                    yield return "\t\t\t\t{\n";
                    yield return "\t\t\t\t\t((I" + table.Name + "_Writable)writable)." + column.Name + " = (" + column.Type.TypeName + ")value;\n";
                    yield return "\t\t\t\t});\n";
                }

                yield return "\t\t}\n";
                yield return "\t\t\n";
                yield return "\t\tinternal DbConnection Connection;\n";
                yield return "\t\tinternal DatabaseConnector DatabaseConnector;\n";
                yield return "\t\n";
                yield return "\t\t\n";
                yield return "\t\tinternal " + table.Name + "_Table(DbConnection connection, IEmbeddedDatabaseConnector embeddedDatabaseConnector, DatabaseConnector databaseConnector)\n";
                yield return "\t\t{\n";
                yield return "\t\t    Connection = connection;\n";
                yield return "\t\t\tEmbeddedDatabaseConnector = embeddedDatabaseConnector;\n";
                yield return "\t\t    DatabaseConnector = databaseConnector;\n";
                yield return "\t\t}\n";
                yield return "\t\t\n";
                yield return "\t\tprotected override void DoInsert(" + baseClassNamespace + "." + table.Name + "_Table." + table.Name + "_Inserter inserter)\n";
                yield return "\t\t{\n";
                yield return "\t\t\tusing (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))\n";
                yield return "\t\t{\n";
                yield return "\t\t    List<string> columnNames = new List<string>();\n";
                yield return "\t\t    List<string> arguments = new List<string>();\n";
                yield return "\t\t\n";
                yield return "\t\t    using (DbCommand command = Connection.CreateCommand())\n";
                yield return "\t\t\t{\n";

                foreach (Column column in table.Columns)
                {
                    yield return "\t\t\t    if (inserter." + column.Name + "_Changed)\n";
                    yield return "\t\t\t    {\n";
                    yield return "\t\t\t        columnNames.Add(\"" + column.Name + "\");\n";
                    yield return "\t\t\t        arguments.Add(\"@" + column.Name + "\");\n";
                    yield return "\t\t\t        DbParameter parm = command.CreateParameter();\n";
                    yield return "\t\t\t        parm.ParameterName = \"@" + column.Name + "\";\n";
                    yield return "\t\t\t        parm.Value = " + string.Format(column.Type.GetConverter, "inserter._" + column.Name) + ";\n";
                    yield return "\t\t\t        command.Parameters.Add(parm);\n";
                    yield return "\t\t\t    }\n";
                    yield return "\t\t\t\n";
                }

                yield return "\t\t\t\tstring commandString = string.Format(\"insert into " + table.Name + " ({0}) values ({1})\",\n";
                yield return "\t\t\t        StringGenerator.GenerateCommaSeperatedList(columnNames),\n";
                yield return "\t\t\t        StringGenerator.GenerateCommaSeperatedList(arguments));\n";
                yield return "\t\t\t\n";
                yield return "\t\t\t    command.CommandText = commandString;\n";
                yield return "\t\t\t\n";
                yield return "\t\t\t    command.ExecuteNonQuery();\n";
                //yield return "\t\t\t\tSelectCache.Clear();\n";
                yield return "\t\t\t};\n";
                yield return "\t\t\t\tDatabaseConnector.OnDatabaseWritten(new EventArgs());\n";
                yield return "\t\t\t}\n";
                yield return "\t\t}\n";
                yield return "\t\t\n";
                yield return "\t\tprotected override TKey DoInsertAndReturnPrimaryKey<TKey>(" + baseClassNamespace + "." + table.Name + "_Table." + table.Name + "_Inserter inserter)\n";
                yield return "\t\t{\n";
                yield return "\t\t\tobject toReturn;\n";
                yield return "\t\t\tusing (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))\n";
                yield return "\t\t{\n";
                yield return "\t\t    List<string> columnNames = new List<string>();\n";
                yield return "\t\t    List<string> arguments = new List<string>();\n";
                yield return "\t\t\n";
                yield return "\t\t    using (DbCommand command = Connection.CreateCommand())\n";
                yield return "\t\t\t{\n";

                foreach (Column column in table.Columns)
                {
                    yield return "\t\t\t    if (inserter." + column.Name + "_Changed)\n";
                    yield return "\t\t\t    {\n";
                    yield return "\t\t\t        columnNames.Add(\"" + column.Name + "\");\n";
                    yield return "\t\t\t        arguments.Add(\"@" + column.Name + "\");\n";
                    yield return "\t\t\t        DbParameter parm = command.CreateParameter();\n";
                    yield return "\t\t\t        parm.ParameterName = \"@" + column.Name + "\";\n";
                    yield return "\t\t\t        parm.Value = " + string.Format(column.Type.GetConverter, "inserter._" + column.Name) + ";\n";
                    yield return "\t\t\t        command.Parameters.Add(parm);\n";
                    yield return "\t\t\t    }\n";
                    yield return "\t\t\t\n";
                }

                yield return "\t\t\t\tstring commandString = string.Format(\"insert into " + table.Name + " ({0}) values ({1});select last_insert_rowid() AS RecordID;\",\n";
                yield return "\t\t\t        StringGenerator.GenerateCommaSeperatedList(columnNames),\n";
                yield return "\t\t\t        StringGenerator.GenerateCommaSeperatedList(arguments));\n";
                yield return "\t\t\t\n";
                yield return "\t\t\t    command.CommandText = commandString;\n";
                yield return "\t\t\t\n";
                yield return "\t\t\t    toReturn = command.ExecuteScalar();\n";
                //yield return "\t\t\t\tSelectCache.Clear();\n";
                yield return "\t\t\t};\n";
                yield return "\t\t\t\tDatabaseConnector.OnDatabaseWritten(new EventArgs());\n";
                yield return "\t\t\t    return (TKey) toReturn;\n";
                yield return "\t\t\t}\n";
                yield return "\t\t}\n";
                yield return "\t\t\t\n";
                yield return "\t\t\t\n";
                yield return "\t\tpublic override IEnumerable<I" + table.Name + "_Readable> Select(ObjectCloud.ORM.DataAccess.WhereConditionals.ComparisonCondition condition, uint? max, OrderBy sortOrder, params ObjectCloud.ORM.DataAccess.Column[] orderBy)\n";
                yield return "\t\t{\n";
                yield return "\t\t\tusing (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))\n";
                yield return "\t\t{\n";
                yield return "\t\t    StringBuilder commandBuilder = new StringBuilder(\"select " + StringGenerator.GenerateCommaSeperatedList(columnNames) + " from " + table.Name + "\");\n";
                yield return "\t\t\n";
                yield return "\t\t    using (DbCommand command = Connection.CreateCommand())\n";
                yield return "\t\t    {\n";
                yield return "\t\t\t    // A null condition just avoids the where clause\n";
                yield return "\t\t\t    if (null != condition)\n";
                yield return "\t\t\t    {\n";
                yield return "\t\t\t        // For now, avoid where clauses with foriegn columns\n";
                yield return "\t\t\t        foreach (object entity in condition.Entities)\n";
                yield return "\t\t\t            if (entity is Column)\n";
                yield return "\t\t\t                if (typeof(" + table.Name + "_Table) != ((Column)entity).Table)\n";
                yield return "\t\t\t                    throw new InvalidWhereClause(\"Only columns from the table selected on are valid\");\n";
                yield return "\t\t\t\n";
                yield return "\t\t\t        string whereClause;\n";
                yield return "\t\t\t        List<DbParameter> parameters = new List<DbParameter>(EmbeddedDatabaseConnector.Build(condition, out whereClause));\n";
                yield return "\t\t\t\n";
                yield return "\t\t\t        commandBuilder.Append(whereClause);\n";
                yield return "\t\t\t        command.Parameters.AddRange(parameters.ToArray());\n";
                yield return "\t\t\t    }\n";
                yield return "\t\t\t\n";
                yield return "\t\t\t    if (null != orderBy)\n";
                yield return "\t\t\t        if (orderBy.Length > 0)\n";
                yield return "\t\t\t            commandBuilder.AppendFormat(\" order by {0} {1} \", StringGenerator.GenerateCommaSeperatedList(orderBy), sortOrder.ToString().ToLower());\n";
                yield return "\t\t\t\n";
                yield return "\t\t\t    if (null != max)\n";
                yield return "\t\t\t        commandBuilder.AppendFormat(\" limit {0} \", max);\n";
                yield return "\t\t\t\n";
                yield return "\t\t\t    command.CommandText = commandBuilder.ToString();\n";
                yield return "\t\t\t\n";
                yield return "\t\t\t    DbDataReader dataReader;\n";
                yield return "\t\t\t    try\n";
                yield return "\t\t\t    {\n";
                yield return "\t\t\t        dataReader = command.ExecuteReader();\n";
                yield return "\t\t\t    }\n";
                yield return "\t\t\t    catch (Exception e)\n";
                yield return "\t\t\t    {\n";
                yield return "\t\t\t        throw new QueryException(\"Exception when running query\", e);\n";
                yield return "\t\t\t    }\n";
                yield return "\t\t\t\n";
                yield return "\t\t\t    using (dataReader)\n";
                yield return "\t\t\t    {\n";
                yield return "\t\t\t        while (dataReader.Read())\n";
                yield return "\t\t\t        {\n";
                yield return "\t\t\t            object[] values = new object[" + table.Columns.Count.ToString() + "];\n";
                yield return "\t\t\t            dataReader.GetValues(values);\n";
                yield return "\t\t\t\n";
                yield return "\t\t\t            " + table.Name + "_Readable toYield = new " + table.Name + "_Readable();\n";
                yield return "\t\t\t\n";

                for (int ctr = 0; ctr < table.Columns.Count; ctr++)
                {
                    yield return "\t\t\t            if (System.DBNull.Value != values[" + ctr.ToString() + "])\n";
                    yield return "\t\t\t              toYield._" + table.Columns[ctr].Name + " = " + string.Format(table.Columns[ctr].Type.SetConverter, GetResultConverter(table.Columns[ctr].Type.DotNetType_NotNullable, "values[" + ctr.ToString() + "]")) + ";\n";
                    yield return "\t\t\t            else\n";
                    yield return "\t\t\t              toYield._" + table.Columns[ctr].Name + " = default(" + table.Columns[ctr].Type.TypeName + ");\n\n";
                }

                yield return "\t\t\t\n";
                yield return "\t\t\t            yield return toYield;\n";
                yield return "\t\t\t        }\n";
                yield return "\t\t\t\n";
                yield return "\t\t\t        dataReader.Close();\n";
                yield return "\t\t\t    }\n";
                yield return "\t\t\t}\n";
                yield return "\t\t}}\n";
                yield return "\t\t\n";
                yield return "\t\tpublic override int Delete(ObjectCloud.ORM.DataAccess.WhereConditionals.ComparisonCondition condition)\n";
                yield return "\t\t{\n";
                yield return "\t\t\tint rowsAffected;\n";
                yield return "\t\t\tusing (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))\n";
                yield return "\t\t{\n";
                yield return "\t\t    StringBuilder commandBuilder = new StringBuilder(\"delete from " + table.Name + "\");\n";
                yield return "\t\t\n";
                yield return "\t\t    using (DbCommand command = Connection.CreateCommand())\n";
                yield return "\t\t\t{\n";
                yield return "\t\t\t    // A null condition just avoids the where clause\n";
                yield return "\t\t\t    if (null != condition)\n";
                yield return "\t\t\t    {\n";
                yield return "\t\t\t        // For now, avoid where clauses with foriegn columns\n";
                yield return "\t\t\t        foreach (object entity in condition.Entities)\n";
                yield return "\t\t\t            if (entity is Column)\n";
                yield return "\t\t\t                if (typeof(" + table.Name + "_Table) != ((Column)entity).Table)\n";
                yield return "\t\t\t                    throw new InvalidWhereClause(\"Only columns from the table selected on are valid\");\n";
                yield return "\t\t\t\n";
                yield return "\t\t\t        string whereClause;\n";
                yield return "\t\t\t        List<DbParameter> parameters = new List<DbParameter>(EmbeddedDatabaseConnector.Build(condition, out whereClause));\n";
                yield return "\t\t\t\n";
                yield return "\t\t\t        commandBuilder.Append(whereClause);\n";
                yield return "\t\t\t        command.Parameters.AddRange(parameters.ToArray());\n";
                yield return "\t\t\t    }\n";
                yield return "\t\t\t\n";
                yield return "\t\t\t    command.CommandText = commandBuilder.ToString();\n";
                yield return "\t\t\t    rowsAffected = command.ExecuteNonQuery();\n";
                //yield return "\t\t\t\tSelectCache.Clear();\n";
                yield return "\t\t\t\n";
                yield return "\t\t\t}}\n";
                yield return "\t\t\t\tDatabaseConnector.OnDatabaseWritten(new EventArgs());\n";
                yield return "\t\t\t    return rowsAffected;\n";
                yield return "\t\t\t}\n";
                yield return "\t\t\n";
                yield return "\t\tprotected override int DoUpdate(ComparisonCondition condition, " + table.Name + "_Inserter inserter)\n";
                yield return "\t\t{\n";
                yield return "\t\t\tint rowsAffected;\n";
                yield return "\t\t\tusing (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))\n";
                yield return "\t\t{\n";
                yield return "\t\t    StringBuilder commandBuilder = new StringBuilder(\"update " + table.Name + "\");\n";
                yield return "\t\t\n";
                yield return "\t\t    using (DbCommand command = Connection.CreateCommand())\n";
                yield return "\t\t\t{\n";
                yield return "\t\t\t    List<string> setStatements = new List<string>();\n";
                yield return "\t\t\t\n";

                foreach (Column column in table.Columns)
                {
                    yield return "\t\t\t    if (inserter." + column.Name + "_Changed)\n";
                    yield return "\t\t\t    {\n";
                    yield return "\t\t\t        setStatements.Add(\"" + column.Name + " = @" + column.Name + "\");\n";
                    yield return "\t\t\t        DbParameter parm = command.CreateParameter();\n";
                    yield return "\t\t\t        parm.ParameterName = \"@" + column.Name + "\";\n";
                    yield return "\t\t\t        parm.Value = " + string.Format(column.Type.GetConverter, "inserter._" + column.Name) + ";\n";
                    yield return "\t\t\t        command.Parameters.Add(parm);\n";
                    yield return "\t\t\t    }\n";
                    yield return "\t\t\t\n";
                }

                yield return "\t\t\t    commandBuilder.AppendFormat(\" set {0}\",\n";
                yield return "\t\t\t        StringGenerator.GenerateCommaSeperatedList(setStatements));\n";
                yield return "\t\t\t\n";
                yield return "\t\t\t    // A null condition just avoids the where clause\n";
                yield return "\t\t\t    if (null != condition)\n";
                yield return "\t\t\t    {\n";
                yield return "\t\t\t        // For now, avoid where clauses with foriegn columns\n";
                yield return "\t\t\t        foreach (object entity in condition.Entities)\n";
                yield return "\t\t\t            if (entity is Column)\n";
                yield return "\t\t\t                if (typeof(" + table.Name + "_Table) != ((Column)entity).Table)\n";
                yield return "\t\t\t                    throw new InvalidWhereClause(\"Only columns from the table selected on are valid\");\n";
                yield return "\t\t\t\n";
                yield return "\t\t\t        string whereClause;\n";
                yield return "\t\t\t        List<DbParameter> parameters = new List<DbParameter>(EmbeddedDatabaseConnector.Build(condition, out whereClause));\n";
                yield return "\t\t\t\n";
                yield return "\t\t\t        commandBuilder.Append(whereClause);\n";
                yield return "\t\t\t        command.Parameters.AddRange(parameters.ToArray());\n";
                yield return "\t\t\t    }\n";
                yield return "\t\t\t\n";
                yield return "\t\t\t    command.CommandText = commandBuilder.ToString();\n";
                yield return "\t\t\t    rowsAffected = command.ExecuteNonQuery();\n";
                yield return "\t\t\t\n";
                //yield return "\t\t\t\tSelectCache.Clear();\n";
                yield return "\t\t\t};\n";
                yield return "\t\t\t\tDatabaseConnector.OnDatabaseWritten(new EventArgs());\n";
                yield return "\t\t\t    return rowsAffected;\n";
                yield return "\t\t\t}\n";
                yield return "\t\t}\n";
                yield return "\t}\n";

            }
        }
    }
}
