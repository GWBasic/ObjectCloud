using ObjectCloud.Common;
using ObjectCloud.DataAccess.SessionManager;
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

namespace ObjectCloud.DataAccess.SQLite.SessionManager
{
    public partial class EmbeddedDatabaseCreator : ObjectCloud.DataAccess.SessionManager.IEmbeddedDatabaseCreator
    {
        /// <summary>
        /// Schema creation sql
        /// </summary>
        const string schemaSql =
@"create table Session 
(
	UserID			guid not null,
	MaxAge			integer not null,
	WhenToDelete			integer not null,
	KeepAlive			boolean not null,
	SessionID			guid not null	primary key
);Create index Session_WhenToDelete on Session (WhenToDelete);

PRAGMA user_version = 3;
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

	public class DatabaseConnectorFactory : ObjectCloud.DataAccess.SessionManager.IDatabaseConnectorFactory
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
		
	partial class DatabaseTransaction : ObjectCloud.DataAccess.SessionManager.IDatabaseTransaction
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
			
			_Session_Table = new Session_Table(sqlConnection, EmbeddedDatabaseConnector, databaseConnector);
		}
		
		public void Dispose()
		{
			using (TimedLock.Lock(sqlConnection)){
				sqlConnection.Close();
				sqlConnection.Dispose();
			}
		}
		
		public T CallOnTransaction<T>(GenericArgumentReturn<ObjectCloud.DataAccess.SessionManager.IDatabaseTransaction, T> del)
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
		
		public void CallOnTransaction(GenericArgument<ObjectCloud.DataAccess.SessionManager.IDatabaseTransaction> del)
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
		
		public ObjectCloud.DataAccess.SessionManager.Session_Table Session
		{
			get { return _Session_Table; }
		}
		private Session_Table _Session_Table;
		
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
		
	internal class Session_Readable : ISession_Readable
	{
		public ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid> UserID
		{
			get { return _UserID; }
		}	
		internal ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid> _UserID = default(ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>);
		
		public System.TimeSpan MaxAge
		{
			get { return _MaxAge; }
		}	
		internal System.TimeSpan _MaxAge = default(System.TimeSpan);
		
		public System.DateTime WhenToDelete
		{
			get { return _WhenToDelete; }
		}	
		internal System.DateTime _WhenToDelete = default(System.DateTime);
		
		public System.Boolean KeepAlive
		{
			get { return _KeepAlive; }
		}	
		internal System.Boolean _KeepAlive = default(System.Boolean);
		
		public ObjectCloud.Common.ID<ObjectCloud.Interfaces.WebServer.ISession, System.Guid> SessionID
		{
			get { return _SessionID; }
		}	
		internal ObjectCloud.Common.ID<ObjectCloud.Interfaces.WebServer.ISession, System.Guid> _SessionID = default(ObjectCloud.Common.ID<ObjectCloud.Interfaces.WebServer.ISession, System.Guid>);
		
	}
	
	public partial class Session_Table : ObjectCloud.DataAccess.SessionManager.Session_Table
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

		static Session_Table()
		{
			ObjectCloud.DataAccess.SessionManager.Session_Table._UserID = ObjectCloud.ORM.DataAccess.Column.Construct<Session_Table, ISession_Writable, ISession_Readable>("UserID");
			ObjectCloud.DataAccess.SessionManager.Session_Table._MaxAge = ObjectCloud.ORM.DataAccess.Column.Construct<Session_Table, ISession_Writable, ISession_Readable>("MaxAge");
			ObjectCloud.DataAccess.SessionManager.Session_Table._WhenToDelete = ObjectCloud.ORM.DataAccess.Column.Construct<Session_Table, ISession_Writable, ISession_Readable>("WhenToDelete");
			ObjectCloud.DataAccess.SessionManager.Session_Table._KeepAlive = ObjectCloud.ORM.DataAccess.Column.Construct<Session_Table, ISession_Writable, ISession_Readable>("KeepAlive");
			ObjectCloud.DataAccess.SessionManager.Session_Table._SessionID = ObjectCloud.ORM.DataAccess.Column.Construct<Session_Table, ISession_Writable, ISession_Readable>("SessionID");
		}
		
		internal DbConnection Connection;
		internal DatabaseConnector DatabaseConnector;
	
		
		internal Session_Table(DbConnection connection, IEmbeddedDatabaseConnector embeddedDatabaseConnector, DatabaseConnector databaseConnector)
		{
		    Connection = connection;
			EmbeddedDatabaseConnector = embeddedDatabaseConnector;
		    DatabaseConnector = databaseConnector;
		}
		
		protected override void DoInsert(ObjectCloud.DataAccess.SessionManager.Session_Table.Session_Inserter inserter)
		{
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    List<string> columnNames = new List<string>();
		    List<string> arguments = new List<string>();
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    if (inserter.UserID_Changed)
			    {
			        columnNames.Add("UserID");
			        arguments.Add("@UserID");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@UserID";
			        parm.Value = inserter._UserID.Value;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.MaxAge_Changed)
			    {
			        columnNames.Add("MaxAge");
			        arguments.Add("@MaxAge");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@MaxAge";
			        parm.Value = inserter._MaxAge.Ticks;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.WhenToDelete_Changed)
			    {
			        columnNames.Add("WhenToDelete");
			        arguments.Add("@WhenToDelete");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@WhenToDelete";
			        parm.Value = inserter._WhenToDelete.Ticks;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.KeepAlive_Changed)
			    {
			        columnNames.Add("KeepAlive");
			        arguments.Add("@KeepAlive");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@KeepAlive";
			        parm.Value = inserter._KeepAlive ? 1 : 0;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.SessionID_Changed)
			    {
			        columnNames.Add("SessionID");
			        arguments.Add("@SessionID");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@SessionID";
			        parm.Value = inserter._SessionID.Value;
			        command.Parameters.Add(parm);
			    }
			
				string commandString = string.Format("insert into Session ({0}) values ({1})",
			        StringGenerator.GenerateCommaSeperatedList(columnNames),
			        StringGenerator.GenerateCommaSeperatedList(arguments));
			
			    command.CommandText = commandString;
			
			    command.ExecuteNonQuery();
			};
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			}
		}
		
		protected override TKey DoInsertAndReturnPrimaryKey<TKey>(ObjectCloud.DataAccess.SessionManager.Session_Table.Session_Inserter inserter)
		{
			object toReturn;
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    List<string> columnNames = new List<string>();
		    List<string> arguments = new List<string>();
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    if (inserter.UserID_Changed)
			    {
			        columnNames.Add("UserID");
			        arguments.Add("@UserID");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@UserID";
			        parm.Value = inserter._UserID.Value;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.MaxAge_Changed)
			    {
			        columnNames.Add("MaxAge");
			        arguments.Add("@MaxAge");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@MaxAge";
			        parm.Value = inserter._MaxAge.Ticks;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.WhenToDelete_Changed)
			    {
			        columnNames.Add("WhenToDelete");
			        arguments.Add("@WhenToDelete");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@WhenToDelete";
			        parm.Value = inserter._WhenToDelete.Ticks;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.KeepAlive_Changed)
			    {
			        columnNames.Add("KeepAlive");
			        arguments.Add("@KeepAlive");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@KeepAlive";
			        parm.Value = inserter._KeepAlive ? 1 : 0;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.SessionID_Changed)
			    {
			        columnNames.Add("SessionID");
			        arguments.Add("@SessionID");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@SessionID";
			        parm.Value = inserter._SessionID.Value;
			        command.Parameters.Add(parm);
			    }
			
				string commandString = string.Format("insert into Session ({0}) values ({1});select last_insert_rowid() AS RecordID;",
			        StringGenerator.GenerateCommaSeperatedList(columnNames),
			        StringGenerator.GenerateCommaSeperatedList(arguments));
			
			    command.CommandText = commandString;
			
			    toReturn = command.ExecuteScalar();
			};
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			    return (TKey) toReturn;
			}
		}
			
			
		public override IEnumerable<ISession_Readable> Select(ObjectCloud.ORM.DataAccess.WhereConditionals.ComparisonCondition condition, uint? max, OrderBy sortOrder, params ObjectCloud.ORM.DataAccess.Column[] orderBy)
		{
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    StringBuilder commandBuilder = new StringBuilder("select UserID, MaxAge, WhenToDelete, KeepAlive, SessionID from Session");
		
		    using (DbCommand command = Connection.CreateCommand())
		    {
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(Session_Table) != ((Column)entity).Table)
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
			            object[] values = new object[5];
			            dataReader.GetValues(values);
			
			            Session_Readable toYield = new Session_Readable();
			
			            if (System.DBNull.Value != values[0])
			              toYield._UserID = new ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>(((System.Guid)values[0]));
			            else
			              toYield._UserID = default(ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>);

			            if (System.DBNull.Value != values[1])
			              toYield._MaxAge = TimeSpan.FromTicks(((System.Int64)values[1]));
			            else
			              toYield._MaxAge = default(System.TimeSpan);

			            if (System.DBNull.Value != values[2])
			              toYield._WhenToDelete = new DateTime(((System.Int64)values[2]));
			            else
			              toYield._WhenToDelete = default(System.DateTime);

			            if (System.DBNull.Value != values[3])
			              toYield._KeepAlive = 1 == Convert.ToInt32(values[3]);
			            else
			              toYield._KeepAlive = default(System.Boolean);

			            if (System.DBNull.Value != values[4])
			              toYield._SessionID = new ObjectCloud.Common.ID<ObjectCloud.Interfaces.WebServer.ISession, System.Guid>(((System.Guid)values[4]));
			            else
			              toYield._SessionID = default(ObjectCloud.Common.ID<ObjectCloud.Interfaces.WebServer.ISession, System.Guid>);

			
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
		    StringBuilder commandBuilder = new StringBuilder("delete from Session");
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(Session_Table) != ((Column)entity).Table)
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
		
		protected override int DoUpdate(ComparisonCondition condition, Session_Inserter inserter)
		{
			int rowsAffected;
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    StringBuilder commandBuilder = new StringBuilder("update Session");
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    List<string> setStatements = new List<string>();
			
			    if (inserter.UserID_Changed)
			    {
			        setStatements.Add("UserID = @UserID");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@UserID";
			        parm.Value = inserter._UserID.Value;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.MaxAge_Changed)
			    {
			        setStatements.Add("MaxAge = @MaxAge");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@MaxAge";
			        parm.Value = inserter._MaxAge.Ticks;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.WhenToDelete_Changed)
			    {
			        setStatements.Add("WhenToDelete = @WhenToDelete");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@WhenToDelete";
			        parm.Value = inserter._WhenToDelete.Ticks;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.KeepAlive_Changed)
			    {
			        setStatements.Add("KeepAlive = @KeepAlive");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@KeepAlive";
			        parm.Value = inserter._KeepAlive ? 1 : 0;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.SessionID_Changed)
			    {
			        setStatements.Add("SessionID = @SessionID");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@SessionID";
			        parm.Value = inserter._SessionID.Value;
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
			                if (typeof(Session_Table) != ((Column)entity).Table)
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