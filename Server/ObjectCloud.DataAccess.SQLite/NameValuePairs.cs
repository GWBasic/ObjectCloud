using ObjectCloud.Common;
using ObjectCloud.DataAccess.NameValuePairs;
using ObjectCloud.Interfaces.Database;
using ObjectCloud.ORM.DataAccess;
using ObjectCloud.ORM.DataAccess.SQLite;
using ObjectCloud.ORM.DataAccess.WhereConditionals;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Text;
using System.Threading;

namespace ObjectCloud.DataAccess.SQLite.NameValuePairs
{
    public partial class EmbeddedDatabaseCreator : ObjectCloud.DataAccess.NameValuePairs.IEmbeddedDatabaseCreator
    {
        /// <summary>
        /// Schema creation sql
        /// </summary>
        const string schemaSql =
@"create table Pairs 
(
	Value			string not null,
	Name			string not null	primary key
);
PRAGMA user_version = 2;
";

        /// <summary>
        /// Used to connect to the embedded database
        /// </summary>
        public IEmbeddedDatabaseConnector EmbeddedDatabaseConnector
        {
            get { return _EmbeddedDatabaseConnector; }
            set { _EmbeddedDatabaseConnector = value; }
        }
        private IEmbeddedDatabaseConnector _EmbeddedDatabaseConnector;

        public void Create(string filename)
        {
            EmbeddedDatabaseConnector.CreateFile(filename);

            DbConnection connection = EmbeddedDatabaseConnector.OpenEmbedded(filename);
            connection.Open();

            try
            {
                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandText = schemaSql;
                    command.ExecuteNonQuery();
                }
            }
            finally
            {
                connection.Close();
                connection.Dispose();
            }

        }
    }

	public class DatabaseConnectorFactory : ObjectCloud.DataAccess.NameValuePairs.IDatabaseConnectorFactory
	{
        /// <summary>
        /// Used to connect to the embedded database
        /// </summary>
        public IEmbeddedDatabaseConnector EmbeddedDatabaseConnector
        {
            get { return _EmbeddedDatabaseConnector; }
            set { _EmbeddedDatabaseConnector = value; }
        }
        private IEmbeddedDatabaseConnector _EmbeddedDatabaseConnector;

		public IDatabaseConnector CreateConnectorForEmbedded(string path)
		{
			return new DatabaseConnector(path, EmbeddedDatabaseConnector);
		}
	}
		
	public partial class DatabaseConnector : IDatabaseConnector
	{
        /// <summary>
        /// Occurs after a transaction is committed
        /// </summary>
        public event EventHandler<IDatabaseConnector, EventArgs> DatabaseWritten;

                internal void OnDatabaseWritten(EventArgs e)
                {
                    if (null != DatabaseWritten)
                        DatabaseWritten(this, e);
                }

		public DateTime LastModified
		{
			get { return File.GetLastWriteTimeUtc(Path); }
		}

        /// <summary>
        /// Used to connect to the embedded database
        /// </summary>
        public IEmbeddedDatabaseConnector EmbeddedDatabaseConnector
        {
            get { return _EmbeddedDatabaseConnector; }
            set { _EmbeddedDatabaseConnector = value; }
        }
        private IEmbeddedDatabaseConnector _EmbeddedDatabaseConnector;

		private string Path;
		
		public DatabaseConnector(string path, IEmbeddedDatabaseConnector embeddedDatabaseConnector)
		{
			Path = path;
			EmbeddedDatabaseConnector = embeddedDatabaseConnector;
		
			using (ObjectCloud.Common.Timeout timeout = ObjectCloud.Common.Timeout.RunMax(TimeSpan.FromSeconds(3), delegate(Thread thread) { EventBus.OnFatalException(this, new EventArgs<Exception>(new CantOpenDatabaseException("Can't open " + Path))); }))
			using (DbConnection connection = EmbeddedDatabaseConnector.Open("Data Source=\"" + Path + "\""))
				try
				{
					connection.Open();
					timeout.Dispose();
					
					//YOU need to write the following function in a partial class.  It should perform automatic schema upgrades by
					//looking at and setting PRAGMA user_version
					DoUpgradeIfNeeded(connection);
				}
				finally
				{
					connection.Close();
				}
		}
		
		public IDatabaseConnection Connect()
		{
			using (ObjectCloud.Common.Timeout timeout = ObjectCloud.Common.Timeout.RunMax(TimeSpan.FromSeconds(3), delegate(Thread thread) { EventBus.OnFatalException(this, new EventArgs<Exception>(new CantOpenDatabaseException("Can't open " + Path))); }))
		{
			DbConnection connection = EmbeddedDatabaseConnector.Open("Data Source=\"" + Path + "\"");
		
			try
			{
				connection.Open();
		
				return new DatabaseConnection(connection, EmbeddedDatabaseConnector, this);
			}
			catch
			{
				connection.Close();
				connection.Dispose();
		
				throw;
			}
			}
		}
		
		public void Restore(string pathToRestoreFrom)
		{
			File.Delete(Path);
			File.Copy(pathToRestoreFrom, Path);
		}
	}
		
	partial class DatabaseTransaction : ObjectCloud.DataAccess.NameValuePairs.IDatabaseTransaction
	{
        /// <summary>
        /// Used to connect to the embedded database
        /// </summary>
        public IEmbeddedDatabaseConnector EmbeddedDatabaseConnector
        {
            get { return _EmbeddedDatabaseConnector; }
            set { _EmbeddedDatabaseConnector = value; }
        }
        private IEmbeddedDatabaseConnector _EmbeddedDatabaseConnector;

		internal DatabaseConnection DatabaseConnection;
		internal DbConnection connection;
		internal DbTransaction transaction;
		
		internal DatabaseTransaction(DbConnection connection, IEmbeddedDatabaseConnector embeddedDatabaseConnector, DatabaseConnection databaseConnection)
		{
			DatabaseConnection = databaseConnection;
			this.connection = connection;
			transaction = connection.BeginTransaction();
			EmbeddedDatabaseConnector = embeddedDatabaseConnector;
		}
		
		public void Commit()
		{
			transaction.Commit();
		}
		
		public void Rollback()
		{
			transaction.Rollback();
		}
		
		public void Dispose()
		{
			transaction.Dispose();
		}
	}
		
	public partial class DatabaseConnection : IDatabaseConnection
	{
        /// <summary>
        /// Used to connect to the embedded database
        /// </summary>
        public IEmbeddedDatabaseConnector EmbeddedDatabaseConnector
        {
            get { return _EmbeddedDatabaseConnector; }
            set { _EmbeddedDatabaseConnector = value; }
        }
        private IEmbeddedDatabaseConnector _EmbeddedDatabaseConnector;

		DbConnection sqlConnection;
		internal DatabaseConnector DatabaseConnector;
		
		public DatabaseConnection(DbConnection sqlConnection, IEmbeddedDatabaseConnector embeddedDatabaseConnector, DatabaseConnector databaseConnector)
		{
			this.sqlConnection = sqlConnection;
			EmbeddedDatabaseConnector = embeddedDatabaseConnector;
			DatabaseConnector = databaseConnector;
			
			_Pairs_Table = new Pairs_Table(sqlConnection, EmbeddedDatabaseConnector, databaseConnector);
		}
		
		public void Dispose()
		{
			using (TimedLock.Lock(sqlConnection)){
				sqlConnection.Close();
				sqlConnection.Dispose();
			}
		}
		
		public T CallOnTransaction<T>(GenericArgumentReturn<ObjectCloud.DataAccess.NameValuePairs.IDatabaseTransaction, T> del)
		{
		    using (TimedLock.Lock(sqlConnection))
		        using (DatabaseTransaction transaction = new DatabaseTransaction(sqlConnection, EmbeddedDatabaseConnector, this))
		            try
		            {
		                return del(transaction);
		            }
		            catch
		            {
		                transaction.Rollback();
		                throw;
		            }
		}
		
		public void CallOnTransaction(GenericArgument<ObjectCloud.DataAccess.NameValuePairs.IDatabaseTransaction> del)
		{
		    using (TimedLock.Lock(sqlConnection))
		        using (DatabaseTransaction transaction = new DatabaseTransaction(sqlConnection, EmbeddedDatabaseConnector, this))
		            try
		            {
		                del(transaction);
		            }
		            catch
		            {
		                transaction.Rollback();
		                throw;
		            }
		}
		
		public DbConnection DbConnection
		{
			get { return sqlConnection; }
		}
		
		public ObjectCloud.DataAccess.NameValuePairs.Pairs_Table Pairs
		{
			get { return _Pairs_Table; }
		}
		private Pairs_Table _Pairs_Table;
		
		public void Vacuum()
		{
		    using (TimedLock.Lock(sqlConnection))
		    {
				DbCommand command = sqlConnection.CreateCommand();
				command.CommandText = "vacuum";
				command.ExecuteNonQuery();
		    }
		}
		
	}
		
	internal class Pairs_Readable : IPairs_Readable
	{
		public System.String Value
		{
			get { return _Value; }
		}	
		internal System.String _Value = default(System.String);
		
		public System.String Name
		{
			get { return _Name; }
		}	
		internal System.String _Name = default(System.String);
		
	}
	
	public partial class Pairs_Table : ObjectCloud.DataAccess.NameValuePairs.Pairs_Table
	{
        /// <summary>
        /// Used to connect to the embedded database
        /// </summary>
        public IEmbeddedDatabaseConnector EmbeddedDatabaseConnector
        {
            get { return _EmbeddedDatabaseConnector; }
            set { _EmbeddedDatabaseConnector = value; }
        }
        private IEmbeddedDatabaseConnector _EmbeddedDatabaseConnector;

		static Pairs_Table()
		{
			ObjectCloud.DataAccess.NameValuePairs.Pairs_Table._Value = ObjectCloud.ORM.DataAccess.Column.Construct<Pairs_Table, IPairs_Writable, IPairs_Readable>("Value");
			ObjectCloud.DataAccess.NameValuePairs.Pairs_Table._Name = ObjectCloud.ORM.DataAccess.Column.Construct<Pairs_Table, IPairs_Writable, IPairs_Readable>("Name");
		}
		
		internal DbConnection Connection;
		internal DatabaseConnector DatabaseConnector;
	
		
		internal Pairs_Table(DbConnection connection, IEmbeddedDatabaseConnector embeddedDatabaseConnector, DatabaseConnector databaseConnector)
		{
		    Connection = connection;
			EmbeddedDatabaseConnector = embeddedDatabaseConnector;
		    DatabaseConnector = databaseConnector;
		}
		
		protected override void DoInsert(ObjectCloud.DataAccess.NameValuePairs.Pairs_Table.Pairs_Inserter inserter)
		{
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    List<string> columnNames = new List<string>();
		    List<string> arguments = new List<string>();
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    if (inserter.Value_Changed)
			    {
			        columnNames.Add("Value");
			        arguments.Add("@Value");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Value";
			        parm.Value = inserter._Value;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.Name_Changed)
			    {
			        columnNames.Add("Name");
			        arguments.Add("@Name");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Name";
			        parm.Value = inserter._Name;
			        command.Parameters.Add(parm);
			    }
			
				string commandString = string.Format("insert into Pairs ({0}) values ({1})",
			        StringGenerator.GenerateCommaSeperatedList(columnNames),
			        StringGenerator.GenerateCommaSeperatedList(arguments));
			
			    command.CommandText = commandString;
			
			    command.ExecuteNonQuery();
			};
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			}
		}
		
		protected override TKey DoInsertAndReturnPrimaryKey<TKey>(ObjectCloud.DataAccess.NameValuePairs.Pairs_Table.Pairs_Inserter inserter)
		{
			object toReturn;
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    List<string> columnNames = new List<string>();
		    List<string> arguments = new List<string>();
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    if (inserter.Value_Changed)
			    {
			        columnNames.Add("Value");
			        arguments.Add("@Value");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Value";
			        parm.Value = inserter._Value;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.Name_Changed)
			    {
			        columnNames.Add("Name");
			        arguments.Add("@Name");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Name";
			        parm.Value = inserter._Name;
			        command.Parameters.Add(parm);
			    }
			
				string commandString = string.Format("insert into Pairs ({0}) values ({1});select last_insert_rowid() AS RecordID;",
			        StringGenerator.GenerateCommaSeperatedList(columnNames),
			        StringGenerator.GenerateCommaSeperatedList(arguments));
			
			    command.CommandText = commandString;
			
			    toReturn = command.ExecuteScalar();
			};
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			    return (TKey) toReturn;
			}
		}
			
			
		public override IEnumerable<IPairs_Readable> Select(ObjectCloud.ORM.DataAccess.WhereConditionals.ComparisonCondition condition, uint? max, OrderBy sortOrder, params ObjectCloud.ORM.DataAccess.Column[] orderBy)
		{
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    StringBuilder commandBuilder = new StringBuilder("select Value, Name from Pairs");
		
		    using (DbCommand command = Connection.CreateCommand())
		    {
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(Pairs_Table) != ((Column)entity).Table)
			                    throw new InvalidWhereClause("Only columns from the table selected on are valid");
			
			        string whereClause;
			        List<DbParameter> parameters = new List<DbParameter>(EmbeddedDatabaseConnector.Build(condition, out whereClause));
			
			        commandBuilder.Append(whereClause);
			        command.Parameters.AddRange(parameters.ToArray());
			    }
			
			    if (null != orderBy)
			        if (orderBy.Length > 0)
			            commandBuilder.AppendFormat(" order by {0} {1} ", StringGenerator.GenerateCommaSeperatedList(orderBy), sortOrder.ToString().ToLower());
			
			    if (null != max)
			        commandBuilder.AppendFormat(" limit {0} ", max);
			
			    command.CommandText = commandBuilder.ToString();
			
			    DbDataReader dataReader;
			    try
			    {
			        dataReader = command.ExecuteReader();
			    }
			    catch (Exception e)
			    {
			        throw new QueryException("Exception when running query", e);
			    }
			
			    using (dataReader)
			    {
			        while (dataReader.Read())
			        {
			            object[] values = new object[2];
			            dataReader.GetValues(values);
			
			            Pairs_Readable toYield = new Pairs_Readable();
			
			            if (System.DBNull.Value != values[0])
			              toYield._Value = ((System.String)values[0]);
			            else
			              toYield._Value = default(System.String);

			            if (System.DBNull.Value != values[1])
			              toYield._Name = ((System.String)values[1]);
			            else
			              toYield._Name = default(System.String);

			
			            yield return toYield;
			        }
			
			        dataReader.Close();
			    }
			}
		}}
		
		public override int Delete(ObjectCloud.ORM.DataAccess.WhereConditionals.ComparisonCondition condition)
		{
			int rowsAffected;
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    StringBuilder commandBuilder = new StringBuilder("delete from Pairs");
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(Pairs_Table) != ((Column)entity).Table)
			                    throw new InvalidWhereClause("Only columns from the table selected on are valid");
			
			        string whereClause;
			        List<DbParameter> parameters = new List<DbParameter>(EmbeddedDatabaseConnector.Build(condition, out whereClause));
			
			        commandBuilder.Append(whereClause);
			        command.Parameters.AddRange(parameters.ToArray());
			    }
			
			    command.CommandText = commandBuilder.ToString();
			    rowsAffected = command.ExecuteNonQuery();
			
			}}
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			    return rowsAffected;
			}
		
		protected override int DoUpdate(ComparisonCondition condition, Pairs_Inserter inserter)
		{
			int rowsAffected;
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    StringBuilder commandBuilder = new StringBuilder("update Pairs");
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    List<string> setStatements = new List<string>();
			
			    if (inserter.Value_Changed)
			    {
			        setStatements.Add("Value = @Value");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Value";
			        parm.Value = inserter._Value;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.Name_Changed)
			    {
			        setStatements.Add("Name = @Name");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Name";
			        parm.Value = inserter._Name;
			        command.Parameters.Add(parm);
			    }
			
			    commandBuilder.AppendFormat(" set {0}",
			        StringGenerator.GenerateCommaSeperatedList(setStatements));
			
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(Pairs_Table) != ((Column)entity).Table)
			                    throw new InvalidWhereClause("Only columns from the table selected on are valid");
			
			        string whereClause;
			        List<DbParameter> parameters = new List<DbParameter>(EmbeddedDatabaseConnector.Build(condition, out whereClause));
			
			        commandBuilder.Append(whereClause);
			        command.Parameters.AddRange(parameters.ToArray());
			    }
			
			    command.CommandText = commandBuilder.ToString();
			    rowsAffected = command.ExecuteNonQuery();
			
			};
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			    return rowsAffected;
			}
		}
	}

}