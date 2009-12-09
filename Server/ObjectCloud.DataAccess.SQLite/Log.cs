using ObjectCloud.Common;
using ObjectCloud.DataAccess.Log;
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

namespace ObjectCloud.DataAccess.SQLite.Log
{
    public partial class EmbeddedDatabaseCreator : ObjectCloud.DataAccess.Log.IEmbeddedDatabaseCreator
    {
        /// <summary>
        /// Schema creation sql
        /// </summary>
        const string schemaSql =
@"create table Classes 
(
	Name			string not null unique,
	ClassId			integer not null	primary key AUTOINCREMENT
);Create index Classes_Name on Classes (Name);
create table Log 
(
	ClassId			integer not null,
	TimeStamp			integer not null,
	Level			integer not null,
	ThreadId			integer not null,
	SessionId			guid,
	RemoteEndPoint			string,
	UserId			guid,
	Message			string not null,
	ExceptionClassId			integer,
	ExceptionMessage			string,
	ExceptionStackTrace			string
);Create index Log_ClassId on Log (ClassId);
Create index Log_TimeStamp on Log (TimeStamp);
Create index Log_Level on Log (Level);
Create index Log_ThreadId on Log (ThreadId);
Create index Log_SessionId on Log (SessionId);
Create index Log_RemoteEndPoint on Log (RemoteEndPoint);
Create index Log_UserId on Log (UserId);
Create index Log_ExceptionClassId on Log (ExceptionClassId);
Create  index Log_TimeStamp_Level on Log (TimeStamp, Level);
create table Lifespan 
(
	Timespan			integer not null,
	Level			integer not null	primary key
);
PRAGMA user_version = 4;
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

	public class DatabaseConnectorFactory : ObjectCloud.DataAccess.Log.IDatabaseConnectorFactory
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
		
	partial class DatabaseTransaction : ObjectCloud.DataAccess.Log.IDatabaseTransaction
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
			
			_Classes_Table = new Classes_Table(sqlConnection, EmbeddedDatabaseConnector, databaseConnector);
			_Log_Table = new Log_Table(sqlConnection, EmbeddedDatabaseConnector, databaseConnector);
			_Lifespan_Table = new Lifespan_Table(sqlConnection, EmbeddedDatabaseConnector, databaseConnector);
		}
		
		public void Dispose()
		{
			using (TimedLock.Lock(sqlConnection)){
				sqlConnection.Close();
				sqlConnection.Dispose();
			}
		}
		
		public T CallOnTransaction<T>(GenericArgumentReturn<ObjectCloud.DataAccess.Log.IDatabaseTransaction, T> del)
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
		
		public void CallOnTransaction(GenericArgument<ObjectCloud.DataAccess.Log.IDatabaseTransaction> del)
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
		
		public ObjectCloud.DataAccess.Log.Classes_Table Classes
		{
			get { return _Classes_Table; }
		}
		private Classes_Table _Classes_Table;
		public ObjectCloud.DataAccess.Log.Log_Table Log
		{
			get { return _Log_Table; }
		}
		private Log_Table _Log_Table;
		public ObjectCloud.DataAccess.Log.Lifespan_Table Lifespan
		{
			get { return _Lifespan_Table; }
		}
		private Lifespan_Table _Lifespan_Table;
		
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
		
	internal class Classes_Readable : IClasses_Readable
	{
		public System.String Name
		{
			get { return _Name; }
		}	
		internal System.String _Name = default(System.String);
		
		public System.Int64 ClassId
		{
			get { return _ClassId; }
		}	
		internal System.Int64 _ClassId = default(System.Int64);
		
	}
	
	public partial class Classes_Table : ObjectCloud.DataAccess.Log.Classes_Table
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

		static Classes_Table()
		{
			ObjectCloud.DataAccess.Log.Classes_Table._Name = ObjectCloud.ORM.DataAccess.Column.Construct<Classes_Table, IClasses_Writable, IClasses_Readable>("Name");
			ObjectCloud.DataAccess.Log.Classes_Table._ClassId = ObjectCloud.ORM.DataAccess.Column.Construct<Classes_Table, IClasses_Writable, IClasses_Readable>("ClassId");
		}
		
		internal DbConnection Connection;
		internal DatabaseConnector DatabaseConnector;
	
		
		internal Classes_Table(DbConnection connection, IEmbeddedDatabaseConnector embeddedDatabaseConnector, DatabaseConnector databaseConnector)
		{
		    Connection = connection;
			EmbeddedDatabaseConnector = embeddedDatabaseConnector;
		    DatabaseConnector = databaseConnector;
		}
		
		protected override void DoInsert(ObjectCloud.DataAccess.Log.Classes_Table.Classes_Inserter inserter)
		{
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    List<string> columnNames = new List<string>();
		    List<string> arguments = new List<string>();
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    if (inserter.Name_Changed)
			    {
			        columnNames.Add("Name");
			        arguments.Add("@Name");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Name";
			        parm.Value = inserter._Name;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.ClassId_Changed)
			    {
			        columnNames.Add("ClassId");
			        arguments.Add("@ClassId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@ClassId";
			        parm.Value = inserter._ClassId;
			        command.Parameters.Add(parm);
			    }
			
				string commandString = string.Format("insert into Classes ({0}) values ({1})",
			        StringGenerator.GenerateCommaSeperatedList(columnNames),
			        StringGenerator.GenerateCommaSeperatedList(arguments));
			
			    command.CommandText = commandString;
			
			    command.ExecuteNonQuery();
			};
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			}
		}
		
		protected override TKey DoInsertAndReturnPrimaryKey<TKey>(ObjectCloud.DataAccess.Log.Classes_Table.Classes_Inserter inserter)
		{
			object toReturn;
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    List<string> columnNames = new List<string>();
		    List<string> arguments = new List<string>();
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    if (inserter.Name_Changed)
			    {
			        columnNames.Add("Name");
			        arguments.Add("@Name");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Name";
			        parm.Value = inserter._Name;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.ClassId_Changed)
			    {
			        columnNames.Add("ClassId");
			        arguments.Add("@ClassId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@ClassId";
			        parm.Value = inserter._ClassId;
			        command.Parameters.Add(parm);
			    }
			
				string commandString = string.Format("insert into Classes ({0}) values ({1});select last_insert_rowid() AS RecordID;",
			        StringGenerator.GenerateCommaSeperatedList(columnNames),
			        StringGenerator.GenerateCommaSeperatedList(arguments));
			
			    command.CommandText = commandString;
			
			    toReturn = command.ExecuteScalar();
			};
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			    return (TKey) toReturn;
			}
		}
			
			
		public override IEnumerable<IClasses_Readable> Select(ObjectCloud.ORM.DataAccess.WhereConditionals.ComparisonCondition condition, uint? max, OrderBy sortOrder, params ObjectCloud.ORM.DataAccess.Column[] orderBy)
		{
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    StringBuilder commandBuilder = new StringBuilder("select Name, ClassId from Classes");
		
		    using (DbCommand command = Connection.CreateCommand())
		    {
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(Classes_Table) != ((Column)entity).Table)
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
			
			            Classes_Readable toYield = new Classes_Readable();
			
			            if (System.DBNull.Value != values[0])
			              toYield._Name = ((System.String)values[0]);
			            else
			              toYield._Name = default(System.String);

			            if (System.DBNull.Value != values[1])
			              toYield._ClassId = ((System.Int64)values[1]);
			            else
			              toYield._ClassId = default(System.Int64);

			
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
		    StringBuilder commandBuilder = new StringBuilder("delete from Classes");
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(Classes_Table) != ((Column)entity).Table)
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
		
		protected override int DoUpdate(ComparisonCondition condition, Classes_Inserter inserter)
		{
			int rowsAffected;
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    StringBuilder commandBuilder = new StringBuilder("update Classes");
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    List<string> setStatements = new List<string>();
			
			    if (inserter.Name_Changed)
			    {
			        setStatements.Add("Name = @Name");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Name";
			        parm.Value = inserter._Name;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.ClassId_Changed)
			    {
			        setStatements.Add("ClassId = @ClassId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@ClassId";
			        parm.Value = inserter._ClassId;
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
			                if (typeof(Classes_Table) != ((Column)entity).Table)
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
	internal class Log_Readable : ILog_Readable
	{
		public System.Int64 ClassId
		{
			get { return _ClassId; }
		}	
		internal System.Int64 _ClassId = default(System.Int64);
		
		public System.DateTime TimeStamp
		{
			get { return _TimeStamp; }
		}	
		internal System.DateTime _TimeStamp = default(System.DateTime);
		
		public ObjectCloud.Interfaces.Disk.LoggingLevel Level
		{
			get { return _Level; }
		}	
		internal ObjectCloud.Interfaces.Disk.LoggingLevel _Level = default(ObjectCloud.Interfaces.Disk.LoggingLevel);
		
		public System.Int32 ThreadId
		{
			get { return _ThreadId; }
		}	
		internal System.Int32 _ThreadId = default(System.Int32);
		
		public System.Nullable<ObjectCloud.Common.ID<ObjectCloud.Interfaces.WebServer.ISession, System.Guid>> SessionId
		{
			get { return _SessionId; }
		}	
		internal System.Nullable<ObjectCloud.Common.ID<ObjectCloud.Interfaces.WebServer.ISession, System.Guid>> _SessionId = default(System.Nullable<ObjectCloud.Common.ID<ObjectCloud.Interfaces.WebServer.ISession, System.Guid>>);
		
		public System.String RemoteEndPoint
		{
			get { return _RemoteEndPoint; }
		}	
		internal System.String _RemoteEndPoint = default(System.String);
		
		public System.Nullable<ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>> UserId
		{
			get { return _UserId; }
		}	
		internal System.Nullable<ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>> _UserId = default(System.Nullable<ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>>);
		
		public System.String Message
		{
			get { return _Message; }
		}	
		internal System.String _Message = default(System.String);
		
		public System.Nullable<System.Int64> ExceptionClassId
		{
			get { return _ExceptionClassId; }
		}	
		internal System.Nullable<System.Int64> _ExceptionClassId = default(System.Nullable<System.Int64>);
		
		public System.String ExceptionMessage
		{
			get { return _ExceptionMessage; }
		}	
		internal System.String _ExceptionMessage = default(System.String);
		
		public System.String ExceptionStackTrace
		{
			get { return _ExceptionStackTrace; }
		}	
		internal System.String _ExceptionStackTrace = default(System.String);
		
	}
	
	public partial class Log_Table : ObjectCloud.DataAccess.Log.Log_Table
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

		static Log_Table()
		{
			ObjectCloud.DataAccess.Log.Log_Table._ClassId = ObjectCloud.ORM.DataAccess.Column.Construct<Log_Table, ILog_Writable, ILog_Readable>("ClassId");
			ObjectCloud.DataAccess.Log.Log_Table._TimeStamp = ObjectCloud.ORM.DataAccess.Column.Construct<Log_Table, ILog_Writable, ILog_Readable>("TimeStamp");
			ObjectCloud.DataAccess.Log.Log_Table._Level = ObjectCloud.ORM.DataAccess.Column.Construct<Log_Table, ILog_Writable, ILog_Readable>("Level");
			ObjectCloud.DataAccess.Log.Log_Table._ThreadId = ObjectCloud.ORM.DataAccess.Column.Construct<Log_Table, ILog_Writable, ILog_Readable>("ThreadId");
			ObjectCloud.DataAccess.Log.Log_Table._SessionId = ObjectCloud.ORM.DataAccess.Column.Construct<Log_Table, ILog_Writable, ILog_Readable>("SessionId");
			ObjectCloud.DataAccess.Log.Log_Table._RemoteEndPoint = ObjectCloud.ORM.DataAccess.Column.Construct<Log_Table, ILog_Writable, ILog_Readable>("RemoteEndPoint");
			ObjectCloud.DataAccess.Log.Log_Table._UserId = ObjectCloud.ORM.DataAccess.Column.Construct<Log_Table, ILog_Writable, ILog_Readable>("UserId");
			ObjectCloud.DataAccess.Log.Log_Table._Message = ObjectCloud.ORM.DataAccess.Column.Construct<Log_Table, ILog_Writable, ILog_Readable>("Message");
			ObjectCloud.DataAccess.Log.Log_Table._ExceptionClassId = ObjectCloud.ORM.DataAccess.Column.Construct<Log_Table, ILog_Writable, ILog_Readable>("ExceptionClassId");
			ObjectCloud.DataAccess.Log.Log_Table._ExceptionMessage = ObjectCloud.ORM.DataAccess.Column.Construct<Log_Table, ILog_Writable, ILog_Readable>("ExceptionMessage");
			ObjectCloud.DataAccess.Log.Log_Table._ExceptionStackTrace = ObjectCloud.ORM.DataAccess.Column.Construct<Log_Table, ILog_Writable, ILog_Readable>("ExceptionStackTrace");
		}
		
		internal DbConnection Connection;
		internal DatabaseConnector DatabaseConnector;
	
		
		internal Log_Table(DbConnection connection, IEmbeddedDatabaseConnector embeddedDatabaseConnector, DatabaseConnector databaseConnector)
		{
		    Connection = connection;
			EmbeddedDatabaseConnector = embeddedDatabaseConnector;
		    DatabaseConnector = databaseConnector;
		}
		
		protected override void DoInsert(ObjectCloud.DataAccess.Log.Log_Table.Log_Inserter inserter)
		{
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    List<string> columnNames = new List<string>();
		    List<string> arguments = new List<string>();
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    if (inserter.ClassId_Changed)
			    {
			        columnNames.Add("ClassId");
			        arguments.Add("@ClassId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@ClassId";
			        parm.Value = inserter._ClassId;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.TimeStamp_Changed)
			    {
			        columnNames.Add("TimeStamp");
			        arguments.Add("@TimeStamp");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@TimeStamp";
			        parm.Value = inserter._TimeStamp.Ticks;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.Level_Changed)
			    {
			        columnNames.Add("Level");
			        arguments.Add("@Level");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Level";
			        parm.Value = ((System.Int32)inserter._Level);
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.ThreadId_Changed)
			    {
			        columnNames.Add("ThreadId");
			        arguments.Add("@ThreadId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@ThreadId";
			        parm.Value = inserter._ThreadId;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.SessionId_Changed)
			    {
			        columnNames.Add("SessionId");
			        arguments.Add("@SessionId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@SessionId";
			        parm.Value = ObjectCloud.Common.ID<ObjectCloud.Interfaces.WebServer.ISession, System.Guid>.GetValueOrNull(inserter._SessionId);
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.RemoteEndPoint_Changed)
			    {
			        columnNames.Add("RemoteEndPoint");
			        arguments.Add("@RemoteEndPoint");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@RemoteEndPoint";
			        parm.Value = inserter._RemoteEndPoint;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.UserId_Changed)
			    {
			        columnNames.Add("UserId");
			        arguments.Add("@UserId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@UserId";
			        parm.Value = ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>.GetValueOrNull(inserter._UserId);
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.Message_Changed)
			    {
			        columnNames.Add("Message");
			        arguments.Add("@Message");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Message";
			        parm.Value = inserter._Message;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.ExceptionClassId_Changed)
			    {
			        columnNames.Add("ExceptionClassId");
			        arguments.Add("@ExceptionClassId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@ExceptionClassId";
			        parm.Value = inserter._ExceptionClassId;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.ExceptionMessage_Changed)
			    {
			        columnNames.Add("ExceptionMessage");
			        arguments.Add("@ExceptionMessage");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@ExceptionMessage";
			        parm.Value = inserter._ExceptionMessage;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.ExceptionStackTrace_Changed)
			    {
			        columnNames.Add("ExceptionStackTrace");
			        arguments.Add("@ExceptionStackTrace");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@ExceptionStackTrace";
			        parm.Value = inserter._ExceptionStackTrace;
			        command.Parameters.Add(parm);
			    }
			
				string commandString = string.Format("insert into Log ({0}) values ({1})",
			        StringGenerator.GenerateCommaSeperatedList(columnNames),
			        StringGenerator.GenerateCommaSeperatedList(arguments));
			
			    command.CommandText = commandString;
			
			    command.ExecuteNonQuery();
			};
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			}
		}
		
		protected override TKey DoInsertAndReturnPrimaryKey<TKey>(ObjectCloud.DataAccess.Log.Log_Table.Log_Inserter inserter)
		{
			object toReturn;
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    List<string> columnNames = new List<string>();
		    List<string> arguments = new List<string>();
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    if (inserter.ClassId_Changed)
			    {
			        columnNames.Add("ClassId");
			        arguments.Add("@ClassId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@ClassId";
			        parm.Value = inserter._ClassId;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.TimeStamp_Changed)
			    {
			        columnNames.Add("TimeStamp");
			        arguments.Add("@TimeStamp");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@TimeStamp";
			        parm.Value = inserter._TimeStamp.Ticks;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.Level_Changed)
			    {
			        columnNames.Add("Level");
			        arguments.Add("@Level");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Level";
			        parm.Value = ((System.Int32)inserter._Level);
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.ThreadId_Changed)
			    {
			        columnNames.Add("ThreadId");
			        arguments.Add("@ThreadId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@ThreadId";
			        parm.Value = inserter._ThreadId;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.SessionId_Changed)
			    {
			        columnNames.Add("SessionId");
			        arguments.Add("@SessionId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@SessionId";
			        parm.Value = ObjectCloud.Common.ID<ObjectCloud.Interfaces.WebServer.ISession, System.Guid>.GetValueOrNull(inserter._SessionId);
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.RemoteEndPoint_Changed)
			    {
			        columnNames.Add("RemoteEndPoint");
			        arguments.Add("@RemoteEndPoint");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@RemoteEndPoint";
			        parm.Value = inserter._RemoteEndPoint;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.UserId_Changed)
			    {
			        columnNames.Add("UserId");
			        arguments.Add("@UserId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@UserId";
			        parm.Value = ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>.GetValueOrNull(inserter._UserId);
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.Message_Changed)
			    {
			        columnNames.Add("Message");
			        arguments.Add("@Message");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Message";
			        parm.Value = inserter._Message;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.ExceptionClassId_Changed)
			    {
			        columnNames.Add("ExceptionClassId");
			        arguments.Add("@ExceptionClassId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@ExceptionClassId";
			        parm.Value = inserter._ExceptionClassId;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.ExceptionMessage_Changed)
			    {
			        columnNames.Add("ExceptionMessage");
			        arguments.Add("@ExceptionMessage");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@ExceptionMessage";
			        parm.Value = inserter._ExceptionMessage;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.ExceptionStackTrace_Changed)
			    {
			        columnNames.Add("ExceptionStackTrace");
			        arguments.Add("@ExceptionStackTrace");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@ExceptionStackTrace";
			        parm.Value = inserter._ExceptionStackTrace;
			        command.Parameters.Add(parm);
			    }
			
				string commandString = string.Format("insert into Log ({0}) values ({1});select last_insert_rowid() AS RecordID;",
			        StringGenerator.GenerateCommaSeperatedList(columnNames),
			        StringGenerator.GenerateCommaSeperatedList(arguments));
			
			    command.CommandText = commandString;
			
			    toReturn = command.ExecuteScalar();
			};
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			    return (TKey) toReturn;
			}
		}
			
			
		public override IEnumerable<ILog_Readable> Select(ObjectCloud.ORM.DataAccess.WhereConditionals.ComparisonCondition condition, uint? max, OrderBy sortOrder, params ObjectCloud.ORM.DataAccess.Column[] orderBy)
		{
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    StringBuilder commandBuilder = new StringBuilder("select ClassId, TimeStamp, Level, ThreadId, SessionId, RemoteEndPoint, UserId, Message, ExceptionClassId, ExceptionMessage, ExceptionStackTrace from Log");
		
		    using (DbCommand command = Connection.CreateCommand())
		    {
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(Log_Table) != ((Column)entity).Table)
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
			            object[] values = new object[11];
			            dataReader.GetValues(values);
			
			            Log_Readable toYield = new Log_Readable();
			
			            if (System.DBNull.Value != values[0])
			              toYield._ClassId = ((System.Int64)values[0]);
			            else
			              toYield._ClassId = default(System.Int64);

			            if (System.DBNull.Value != values[1])
			              toYield._TimeStamp = new DateTime(((System.Int64)values[1]));
			            else
			              toYield._TimeStamp = default(System.DateTime);

			            if (System.DBNull.Value != values[2])
			              toYield._Level = ((ObjectCloud.Interfaces.Disk.LoggingLevel)Convert.ToInt32(values[2]));
			            else
			              toYield._Level = default(ObjectCloud.Interfaces.Disk.LoggingLevel);

			            if (System.DBNull.Value != values[3])
			              toYield._ThreadId = Convert.ToInt32(values[3]);
			            else
			              toYield._ThreadId = default(System.Int32);

			            if (System.DBNull.Value != values[4])
			              toYield._SessionId = new ObjectCloud.Common.ID<ObjectCloud.Interfaces.WebServer.ISession, System.Guid>(((System.Guid)values[4]));
			            else
			              toYield._SessionId = default(System.Nullable<ObjectCloud.Common.ID<ObjectCloud.Interfaces.WebServer.ISession, System.Guid>>);

			            if (System.DBNull.Value != values[5])
			              toYield._RemoteEndPoint = ((System.String)values[5]);
			            else
			              toYield._RemoteEndPoint = default(System.String);

			            if (System.DBNull.Value != values[6])
			              toYield._UserId = new ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>(((System.Guid)values[6]));
			            else
			              toYield._UserId = default(System.Nullable<ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>>);

			            if (System.DBNull.Value != values[7])
			              toYield._Message = ((System.String)values[7]);
			            else
			              toYield._Message = default(System.String);

			            if (System.DBNull.Value != values[8])
			              toYield._ExceptionClassId = ((System.Int64)values[8]);
			            else
			              toYield._ExceptionClassId = default(System.Nullable<System.Int64>);

			            if (System.DBNull.Value != values[9])
			              toYield._ExceptionMessage = ((System.String)values[9]);
			            else
			              toYield._ExceptionMessage = default(System.String);

			            if (System.DBNull.Value != values[10])
			              toYield._ExceptionStackTrace = ((System.String)values[10]);
			            else
			              toYield._ExceptionStackTrace = default(System.String);

			
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
		    StringBuilder commandBuilder = new StringBuilder("delete from Log");
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(Log_Table) != ((Column)entity).Table)
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
		
		protected override int DoUpdate(ComparisonCondition condition, Log_Inserter inserter)
		{
			int rowsAffected;
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    StringBuilder commandBuilder = new StringBuilder("update Log");
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    List<string> setStatements = new List<string>();
			
			    if (inserter.ClassId_Changed)
			    {
			        setStatements.Add("ClassId = @ClassId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@ClassId";
			        parm.Value = inserter._ClassId;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.TimeStamp_Changed)
			    {
			        setStatements.Add("TimeStamp = @TimeStamp");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@TimeStamp";
			        parm.Value = inserter._TimeStamp.Ticks;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.Level_Changed)
			    {
			        setStatements.Add("Level = @Level");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Level";
			        parm.Value = ((System.Int32)inserter._Level);
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.ThreadId_Changed)
			    {
			        setStatements.Add("ThreadId = @ThreadId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@ThreadId";
			        parm.Value = inserter._ThreadId;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.SessionId_Changed)
			    {
			        setStatements.Add("SessionId = @SessionId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@SessionId";
			        parm.Value = ObjectCloud.Common.ID<ObjectCloud.Interfaces.WebServer.ISession, System.Guid>.GetValueOrNull(inserter._SessionId);
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.RemoteEndPoint_Changed)
			    {
			        setStatements.Add("RemoteEndPoint = @RemoteEndPoint");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@RemoteEndPoint";
			        parm.Value = inserter._RemoteEndPoint;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.UserId_Changed)
			    {
			        setStatements.Add("UserId = @UserId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@UserId";
			        parm.Value = ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>.GetValueOrNull(inserter._UserId);
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.Message_Changed)
			    {
			        setStatements.Add("Message = @Message");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Message";
			        parm.Value = inserter._Message;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.ExceptionClassId_Changed)
			    {
			        setStatements.Add("ExceptionClassId = @ExceptionClassId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@ExceptionClassId";
			        parm.Value = inserter._ExceptionClassId;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.ExceptionMessage_Changed)
			    {
			        setStatements.Add("ExceptionMessage = @ExceptionMessage");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@ExceptionMessage";
			        parm.Value = inserter._ExceptionMessage;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.ExceptionStackTrace_Changed)
			    {
			        setStatements.Add("ExceptionStackTrace = @ExceptionStackTrace");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@ExceptionStackTrace";
			        parm.Value = inserter._ExceptionStackTrace;
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
			                if (typeof(Log_Table) != ((Column)entity).Table)
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
	internal class Lifespan_Readable : ILifespan_Readable
	{
		public System.TimeSpan Timespan
		{
			get { return _Timespan; }
		}	
		internal System.TimeSpan _Timespan = default(System.TimeSpan);
		
		public ObjectCloud.Interfaces.Disk.LoggingLevel Level
		{
			get { return _Level; }
		}	
		internal ObjectCloud.Interfaces.Disk.LoggingLevel _Level = default(ObjectCloud.Interfaces.Disk.LoggingLevel);
		
	}
	
	public partial class Lifespan_Table : ObjectCloud.DataAccess.Log.Lifespan_Table
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

		static Lifespan_Table()
		{
			ObjectCloud.DataAccess.Log.Lifespan_Table._Timespan = ObjectCloud.ORM.DataAccess.Column.Construct<Lifespan_Table, ILifespan_Writable, ILifespan_Readable>("Timespan");
			ObjectCloud.DataAccess.Log.Lifespan_Table._Level = ObjectCloud.ORM.DataAccess.Column.Construct<Lifespan_Table, ILifespan_Writable, ILifespan_Readable>("Level");
		}
		
		internal DbConnection Connection;
		internal DatabaseConnector DatabaseConnector;
	
		
		internal Lifespan_Table(DbConnection connection, IEmbeddedDatabaseConnector embeddedDatabaseConnector, DatabaseConnector databaseConnector)
		{
		    Connection = connection;
			EmbeddedDatabaseConnector = embeddedDatabaseConnector;
		    DatabaseConnector = databaseConnector;
		}
		
		protected override void DoInsert(ObjectCloud.DataAccess.Log.Lifespan_Table.Lifespan_Inserter inserter)
		{
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    List<string> columnNames = new List<string>();
		    List<string> arguments = new List<string>();
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    if (inserter.Timespan_Changed)
			    {
			        columnNames.Add("Timespan");
			        arguments.Add("@Timespan");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Timespan";
			        parm.Value = inserter._Timespan.Ticks;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.Level_Changed)
			    {
			        columnNames.Add("Level");
			        arguments.Add("@Level");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Level";
			        parm.Value = ((System.Int32)inserter._Level);
			        command.Parameters.Add(parm);
			    }
			
				string commandString = string.Format("insert into Lifespan ({0}) values ({1})",
			        StringGenerator.GenerateCommaSeperatedList(columnNames),
			        StringGenerator.GenerateCommaSeperatedList(arguments));
			
			    command.CommandText = commandString;
			
			    command.ExecuteNonQuery();
			};
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			}
		}
		
		protected override TKey DoInsertAndReturnPrimaryKey<TKey>(ObjectCloud.DataAccess.Log.Lifespan_Table.Lifespan_Inserter inserter)
		{
			object toReturn;
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    List<string> columnNames = new List<string>();
		    List<string> arguments = new List<string>();
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    if (inserter.Timespan_Changed)
			    {
			        columnNames.Add("Timespan");
			        arguments.Add("@Timespan");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Timespan";
			        parm.Value = inserter._Timespan.Ticks;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.Level_Changed)
			    {
			        columnNames.Add("Level");
			        arguments.Add("@Level");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Level";
			        parm.Value = ((System.Int32)inserter._Level);
			        command.Parameters.Add(parm);
			    }
			
				string commandString = string.Format("insert into Lifespan ({0}) values ({1});select last_insert_rowid() AS RecordID;",
			        StringGenerator.GenerateCommaSeperatedList(columnNames),
			        StringGenerator.GenerateCommaSeperatedList(arguments));
			
			    command.CommandText = commandString;
			
			    toReturn = command.ExecuteScalar();
			};
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			    return (TKey) toReturn;
			}
		}
			
			
		public override IEnumerable<ILifespan_Readable> Select(ObjectCloud.ORM.DataAccess.WhereConditionals.ComparisonCondition condition, uint? max, OrderBy sortOrder, params ObjectCloud.ORM.DataAccess.Column[] orderBy)
		{
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    StringBuilder commandBuilder = new StringBuilder("select Timespan, Level from Lifespan");
		
		    using (DbCommand command = Connection.CreateCommand())
		    {
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(Lifespan_Table) != ((Column)entity).Table)
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
			
			            Lifespan_Readable toYield = new Lifespan_Readable();
			
			            if (System.DBNull.Value != values[0])
			              toYield._Timespan = TimeSpan.FromTicks(((System.Int64)values[0]));
			            else
			              toYield._Timespan = default(System.TimeSpan);

			            if (System.DBNull.Value != values[1])
			              toYield._Level = ((ObjectCloud.Interfaces.Disk.LoggingLevel)Convert.ToInt32(values[1]));
			            else
			              toYield._Level = default(ObjectCloud.Interfaces.Disk.LoggingLevel);

			
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
		    StringBuilder commandBuilder = new StringBuilder("delete from Lifespan");
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(Lifespan_Table) != ((Column)entity).Table)
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
		
		protected override int DoUpdate(ComparisonCondition condition, Lifespan_Inserter inserter)
		{
			int rowsAffected;
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    StringBuilder commandBuilder = new StringBuilder("update Lifespan");
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    List<string> setStatements = new List<string>();
			
			    if (inserter.Timespan_Changed)
			    {
			        setStatements.Add("Timespan = @Timespan");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Timespan";
			        parm.Value = inserter._Timespan.Ticks;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.Level_Changed)
			    {
			        setStatements.Add("Level = @Level");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Level";
			        parm.Value = ((System.Int32)inserter._Level);
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
			                if (typeof(Lifespan_Table) != ((Column)entity).Table)
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