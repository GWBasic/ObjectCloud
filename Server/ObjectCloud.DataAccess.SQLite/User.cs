using ObjectCloud.Common;
using ObjectCloud.DataAccess.User;
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

namespace ObjectCloud.DataAccess.SQLite.User
{
    public partial class EmbeddedDatabaseCreator : ObjectCloud.DataAccess.User.IEmbeddedDatabaseCreator
    {
        /// <summary>
        /// Schema creation sql
        /// </summary>
        const string schemaSql =
@"create table Pairs 
(
	Value			string not null,
	Name			string not null	primary key
);create table Notification 
(
	TimeStamp			integer not null,
	Sender			string not null,
	ObjectUrl			string not null,
	Title			string not null,
	DocumentType			string not null,
	MessageSummary			string not null,
	State			integer not null,
	NotificationId			integer not null	primary key AUTOINCREMENT
);Create index Notification_TimeStamp on Notification (TimeStamp);
Create index Notification_Sender on Notification (Sender);
Create index Notification_ObjectUrl on Notification (ObjectUrl);
Create index Notification_Title on Notification (Title);
Create index Notification_DocumentType on Notification (DocumentType);
Create index Notification_State on Notification (State);
create table ChangeData 
(
	ChangeData			string not null,
	NotificationId			integer not null	primary key references Notification(NotificationId)
);create table Sender 
(
	SenderToken			string,
	RecipientToken			string,
	OpenID			string not null	primary key
);Create index Sender_SenderToken on Sender (SenderToken);
Create index Sender_RecipientToken on Sender (RecipientToken);
create table Token 
(
	Token			string not null,
	Created			integer not null,
	OpenId			string not null	primary key
);Create index Token_Token on Token (Token);
create table Blocked 
(
	OpenIdorDomain			string not null	primary key
);create table ObjectState 
(
	ObjectState			integer not null,
	ObjectUrl			string not null	primary key
);create table Deleted 
(
	OpenId			string not null,
	ObjectUrl			string not null	primary key
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

	public class DatabaseConnectorFactory : ObjectCloud.DataAccess.User.IDatabaseConnectorFactory
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
		
	partial class DatabaseTransaction : ObjectCloud.DataAccess.User.IDatabaseTransaction
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
			_Notification_Table = new Notification_Table(sqlConnection, EmbeddedDatabaseConnector, databaseConnector);
			_ChangeData_Table = new ChangeData_Table(sqlConnection, EmbeddedDatabaseConnector, databaseConnector);
			_Sender_Table = new Sender_Table(sqlConnection, EmbeddedDatabaseConnector, databaseConnector);
			_Token_Table = new Token_Table(sqlConnection, EmbeddedDatabaseConnector, databaseConnector);
			_Blocked_Table = new Blocked_Table(sqlConnection, EmbeddedDatabaseConnector, databaseConnector);
			_ObjectState_Table = new ObjectState_Table(sqlConnection, EmbeddedDatabaseConnector, databaseConnector);
			_Deleted_Table = new Deleted_Table(sqlConnection, EmbeddedDatabaseConnector, databaseConnector);
		}
		
		public void Dispose()
		{
			using (TimedLock.Lock(sqlConnection)){
				sqlConnection.Close();
				sqlConnection.Dispose();
			}
		}
		
		public T CallOnTransaction<T>(GenericArgumentReturn<ObjectCloud.DataAccess.User.IDatabaseTransaction, T> del)
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
		
		public void CallOnTransaction(GenericArgument<ObjectCloud.DataAccess.User.IDatabaseTransaction> del)
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
		
		public ObjectCloud.DataAccess.User.Pairs_Table Pairs
		{
			get { return _Pairs_Table; }
		}
		private Pairs_Table _Pairs_Table;
		public ObjectCloud.DataAccess.User.Notification_Table Notification
		{
			get { return _Notification_Table; }
		}
		private Notification_Table _Notification_Table;
		public ObjectCloud.DataAccess.User.ChangeData_Table ChangeData
		{
			get { return _ChangeData_Table; }
		}
		private ChangeData_Table _ChangeData_Table;
		public ObjectCloud.DataAccess.User.Sender_Table Sender
		{
			get { return _Sender_Table; }
		}
		private Sender_Table _Sender_Table;
		public ObjectCloud.DataAccess.User.Token_Table Token
		{
			get { return _Token_Table; }
		}
		private Token_Table _Token_Table;
		public ObjectCloud.DataAccess.User.Blocked_Table Blocked
		{
			get { return _Blocked_Table; }
		}
		private Blocked_Table _Blocked_Table;
		public ObjectCloud.DataAccess.User.ObjectState_Table ObjectState
		{
			get { return _ObjectState_Table; }
		}
		private ObjectState_Table _ObjectState_Table;
		public ObjectCloud.DataAccess.User.Deleted_Table Deleted
		{
			get { return _Deleted_Table; }
		}
		private Deleted_Table _Deleted_Table;
		
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
	
	public partial class Pairs_Table : ObjectCloud.DataAccess.User.Pairs_Table
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
			ObjectCloud.DataAccess.User.Pairs_Table._Value = ObjectCloud.ORM.DataAccess.Column.Construct<Pairs_Table, IPairs_Writable, IPairs_Readable>("Value");
			ObjectCloud.DataAccess.User.Pairs_Table._Name = ObjectCloud.ORM.DataAccess.Column.Construct<Pairs_Table, IPairs_Writable, IPairs_Readable>("Name");
		}
		
		internal DbConnection Connection;
		internal DatabaseConnector DatabaseConnector;
	
		
		internal Pairs_Table(DbConnection connection, IEmbeddedDatabaseConnector embeddedDatabaseConnector, DatabaseConnector databaseConnector)
		{
		    Connection = connection;
			EmbeddedDatabaseConnector = embeddedDatabaseConnector;
		    DatabaseConnector = databaseConnector;
		}
		
		protected override void DoInsert(ObjectCloud.DataAccess.User.Pairs_Table.Pairs_Inserter inserter)
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
		
		protected override TKey DoInsertAndReturnPrimaryKey<TKey>(ObjectCloud.DataAccess.User.Pairs_Table.Pairs_Inserter inserter)
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
	internal class Notification_Readable : INotification_Readable
	{
		public System.DateTime TimeStamp
		{
			get { return _TimeStamp; }
		}	
		internal System.DateTime _TimeStamp = default(System.DateTime);
		
		public System.String Sender
		{
			get { return _Sender; }
		}	
		internal System.String _Sender = default(System.String);
		
		public System.String ObjectUrl
		{
			get { return _ObjectUrl; }
		}	
		internal System.String _ObjectUrl = default(System.String);
		
		public System.String Title
		{
			get { return _Title; }
		}	
		internal System.String _Title = default(System.String);
		
		public System.String DocumentType
		{
			get { return _DocumentType; }
		}	
		internal System.String _DocumentType = default(System.String);
		
		public System.String MessageSummary
		{
			get { return _MessageSummary; }
		}	
		internal System.String _MessageSummary = default(System.String);
		
		public ObjectCloud.Interfaces.Disk.NotificationState State
		{
			get { return _State; }
		}	
		internal ObjectCloud.Interfaces.Disk.NotificationState _State = default(ObjectCloud.Interfaces.Disk.NotificationState);
		
		public System.Int64 NotificationId
		{
			get { return _NotificationId; }
		}	
		internal System.Int64 _NotificationId = default(System.Int64);
		
	}
	
	public partial class Notification_Table : ObjectCloud.DataAccess.User.Notification_Table
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

		static Notification_Table()
		{
			ObjectCloud.DataAccess.User.Notification_Table._TimeStamp = ObjectCloud.ORM.DataAccess.Column.Construct<Notification_Table, INotification_Writable, INotification_Readable>("TimeStamp");
			ObjectCloud.DataAccess.User.Notification_Table._Sender = ObjectCloud.ORM.DataAccess.Column.Construct<Notification_Table, INotification_Writable, INotification_Readable>("Sender");
			ObjectCloud.DataAccess.User.Notification_Table._ObjectUrl = ObjectCloud.ORM.DataAccess.Column.Construct<Notification_Table, INotification_Writable, INotification_Readable>("ObjectUrl");
			ObjectCloud.DataAccess.User.Notification_Table._Title = ObjectCloud.ORM.DataAccess.Column.Construct<Notification_Table, INotification_Writable, INotification_Readable>("Title");
			ObjectCloud.DataAccess.User.Notification_Table._DocumentType = ObjectCloud.ORM.DataAccess.Column.Construct<Notification_Table, INotification_Writable, INotification_Readable>("DocumentType");
			ObjectCloud.DataAccess.User.Notification_Table._MessageSummary = ObjectCloud.ORM.DataAccess.Column.Construct<Notification_Table, INotification_Writable, INotification_Readable>("MessageSummary");
			ObjectCloud.DataAccess.User.Notification_Table._State = ObjectCloud.ORM.DataAccess.Column.Construct<Notification_Table, INotification_Writable, INotification_Readable>("State");
			ObjectCloud.DataAccess.User.Notification_Table._NotificationId = ObjectCloud.ORM.DataAccess.Column.Construct<Notification_Table, INotification_Writable, INotification_Readable>("NotificationId");
		}
		
		internal DbConnection Connection;
		internal DatabaseConnector DatabaseConnector;
	
		
		internal Notification_Table(DbConnection connection, IEmbeddedDatabaseConnector embeddedDatabaseConnector, DatabaseConnector databaseConnector)
		{
		    Connection = connection;
			EmbeddedDatabaseConnector = embeddedDatabaseConnector;
		    DatabaseConnector = databaseConnector;
		}
		
		protected override void DoInsert(ObjectCloud.DataAccess.User.Notification_Table.Notification_Inserter inserter)
		{
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    List<string> columnNames = new List<string>();
		    List<string> arguments = new List<string>();
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    if (inserter.TimeStamp_Changed)
			    {
			        columnNames.Add("TimeStamp");
			        arguments.Add("@TimeStamp");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@TimeStamp";
			        parm.Value = inserter._TimeStamp.Ticks;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.Sender_Changed)
			    {
			        columnNames.Add("Sender");
			        arguments.Add("@Sender");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Sender";
			        parm.Value = inserter._Sender;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.ObjectUrl_Changed)
			    {
			        columnNames.Add("ObjectUrl");
			        arguments.Add("@ObjectUrl");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@ObjectUrl";
			        parm.Value = inserter._ObjectUrl;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.Title_Changed)
			    {
			        columnNames.Add("Title");
			        arguments.Add("@Title");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Title";
			        parm.Value = inserter._Title;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.DocumentType_Changed)
			    {
			        columnNames.Add("DocumentType");
			        arguments.Add("@DocumentType");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@DocumentType";
			        parm.Value = inserter._DocumentType;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.MessageSummary_Changed)
			    {
			        columnNames.Add("MessageSummary");
			        arguments.Add("@MessageSummary");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@MessageSummary";
			        parm.Value = inserter._MessageSummary;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.State_Changed)
			    {
			        columnNames.Add("State");
			        arguments.Add("@State");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@State";
			        parm.Value = ((System.Int32)inserter._State);
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.NotificationId_Changed)
			    {
			        columnNames.Add("NotificationId");
			        arguments.Add("@NotificationId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@NotificationId";
			        parm.Value = inserter._NotificationId;
			        command.Parameters.Add(parm);
			    }
			
				string commandString = string.Format("insert into Notification ({0}) values ({1})",
			        StringGenerator.GenerateCommaSeperatedList(columnNames),
			        StringGenerator.GenerateCommaSeperatedList(arguments));
			
			    command.CommandText = commandString;
			
			    command.ExecuteNonQuery();
			};
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			}
		}
		
		protected override TKey DoInsertAndReturnPrimaryKey<TKey>(ObjectCloud.DataAccess.User.Notification_Table.Notification_Inserter inserter)
		{
			object toReturn;
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    List<string> columnNames = new List<string>();
		    List<string> arguments = new List<string>();
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    if (inserter.TimeStamp_Changed)
			    {
			        columnNames.Add("TimeStamp");
			        arguments.Add("@TimeStamp");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@TimeStamp";
			        parm.Value = inserter._TimeStamp.Ticks;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.Sender_Changed)
			    {
			        columnNames.Add("Sender");
			        arguments.Add("@Sender");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Sender";
			        parm.Value = inserter._Sender;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.ObjectUrl_Changed)
			    {
			        columnNames.Add("ObjectUrl");
			        arguments.Add("@ObjectUrl");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@ObjectUrl";
			        parm.Value = inserter._ObjectUrl;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.Title_Changed)
			    {
			        columnNames.Add("Title");
			        arguments.Add("@Title");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Title";
			        parm.Value = inserter._Title;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.DocumentType_Changed)
			    {
			        columnNames.Add("DocumentType");
			        arguments.Add("@DocumentType");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@DocumentType";
			        parm.Value = inserter._DocumentType;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.MessageSummary_Changed)
			    {
			        columnNames.Add("MessageSummary");
			        arguments.Add("@MessageSummary");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@MessageSummary";
			        parm.Value = inserter._MessageSummary;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.State_Changed)
			    {
			        columnNames.Add("State");
			        arguments.Add("@State");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@State";
			        parm.Value = ((System.Int32)inserter._State);
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.NotificationId_Changed)
			    {
			        columnNames.Add("NotificationId");
			        arguments.Add("@NotificationId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@NotificationId";
			        parm.Value = inserter._NotificationId;
			        command.Parameters.Add(parm);
			    }
			
				string commandString = string.Format("insert into Notification ({0}) values ({1});select last_insert_rowid() AS RecordID;",
			        StringGenerator.GenerateCommaSeperatedList(columnNames),
			        StringGenerator.GenerateCommaSeperatedList(arguments));
			
			    command.CommandText = commandString;
			
			    toReturn = command.ExecuteScalar();
			};
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			    return (TKey) toReturn;
			}
		}
			
			
		public override IEnumerable<INotification_Readable> Select(ObjectCloud.ORM.DataAccess.WhereConditionals.ComparisonCondition condition, uint? max, OrderBy sortOrder, params ObjectCloud.ORM.DataAccess.Column[] orderBy)
		{
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    StringBuilder commandBuilder = new StringBuilder("select TimeStamp, Sender, ObjectUrl, Title, DocumentType, MessageSummary, State, NotificationId from Notification");
		
		    using (DbCommand command = Connection.CreateCommand())
		    {
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(Notification_Table) != ((Column)entity).Table)
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
			            object[] values = new object[8];
			            dataReader.GetValues(values);
			
			            Notification_Readable toYield = new Notification_Readable();
			
			            if (System.DBNull.Value != values[0])
			              toYield._TimeStamp = new DateTime(((System.Int64)values[0]));
			            else
			              toYield._TimeStamp = default(System.DateTime);

			            if (System.DBNull.Value != values[1])
			              toYield._Sender = ((System.String)values[1]);
			            else
			              toYield._Sender = default(System.String);

			            if (System.DBNull.Value != values[2])
			              toYield._ObjectUrl = ((System.String)values[2]);
			            else
			              toYield._ObjectUrl = default(System.String);

			            if (System.DBNull.Value != values[3])
			              toYield._Title = ((System.String)values[3]);
			            else
			              toYield._Title = default(System.String);

			            if (System.DBNull.Value != values[4])
			              toYield._DocumentType = ((System.String)values[4]);
			            else
			              toYield._DocumentType = default(System.String);

			            if (System.DBNull.Value != values[5])
			              toYield._MessageSummary = ((System.String)values[5]);
			            else
			              toYield._MessageSummary = default(System.String);

			            if (System.DBNull.Value != values[6])
			              toYield._State = ((ObjectCloud.Interfaces.Disk.NotificationState)Convert.ToInt32(values[6]));
			            else
			              toYield._State = default(ObjectCloud.Interfaces.Disk.NotificationState);

			            if (System.DBNull.Value != values[7])
			              toYield._NotificationId = ((System.Int64)values[7]);
			            else
			              toYield._NotificationId = default(System.Int64);

			
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
		    StringBuilder commandBuilder = new StringBuilder("delete from Notification");
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(Notification_Table) != ((Column)entity).Table)
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
		
		protected override int DoUpdate(ComparisonCondition condition, Notification_Inserter inserter)
		{
			int rowsAffected;
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    StringBuilder commandBuilder = new StringBuilder("update Notification");
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    List<string> setStatements = new List<string>();
			
			    if (inserter.TimeStamp_Changed)
			    {
			        setStatements.Add("TimeStamp = @TimeStamp");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@TimeStamp";
			        parm.Value = inserter._TimeStamp.Ticks;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.Sender_Changed)
			    {
			        setStatements.Add("Sender = @Sender");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Sender";
			        parm.Value = inserter._Sender;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.ObjectUrl_Changed)
			    {
			        setStatements.Add("ObjectUrl = @ObjectUrl");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@ObjectUrl";
			        parm.Value = inserter._ObjectUrl;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.Title_Changed)
			    {
			        setStatements.Add("Title = @Title");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Title";
			        parm.Value = inserter._Title;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.DocumentType_Changed)
			    {
			        setStatements.Add("DocumentType = @DocumentType");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@DocumentType";
			        parm.Value = inserter._DocumentType;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.MessageSummary_Changed)
			    {
			        setStatements.Add("MessageSummary = @MessageSummary");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@MessageSummary";
			        parm.Value = inserter._MessageSummary;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.State_Changed)
			    {
			        setStatements.Add("State = @State");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@State";
			        parm.Value = ((System.Int32)inserter._State);
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.NotificationId_Changed)
			    {
			        setStatements.Add("NotificationId = @NotificationId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@NotificationId";
			        parm.Value = inserter._NotificationId;
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
			                if (typeof(Notification_Table) != ((Column)entity).Table)
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
	internal class ChangeData_Readable : IChangeData_Readable
	{
		public System.String ChangeData
		{
			get { return _ChangeData; }
		}	
		internal System.String _ChangeData = default(System.String);
		
		public System.Int64 NotificationId
		{
			get { return _NotificationId; }
		}	
		internal System.Int64 _NotificationId = default(System.Int64);
		
	}
	
	public partial class ChangeData_Table : ObjectCloud.DataAccess.User.ChangeData_Table
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

		static ChangeData_Table()
		{
			ObjectCloud.DataAccess.User.ChangeData_Table._ChangeData = ObjectCloud.ORM.DataAccess.Column.Construct<ChangeData_Table, IChangeData_Writable, IChangeData_Readable>("ChangeData");
			ObjectCloud.DataAccess.User.ChangeData_Table._NotificationId = ObjectCloud.ORM.DataAccess.Column.Construct<ChangeData_Table, IChangeData_Writable, IChangeData_Readable>("NotificationId");
		}
		
		internal DbConnection Connection;
		internal DatabaseConnector DatabaseConnector;
	
		
		internal ChangeData_Table(DbConnection connection, IEmbeddedDatabaseConnector embeddedDatabaseConnector, DatabaseConnector databaseConnector)
		{
		    Connection = connection;
			EmbeddedDatabaseConnector = embeddedDatabaseConnector;
		    DatabaseConnector = databaseConnector;
		}
		
		protected override void DoInsert(ObjectCloud.DataAccess.User.ChangeData_Table.ChangeData_Inserter inserter)
		{
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    List<string> columnNames = new List<string>();
		    List<string> arguments = new List<string>();
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    if (inserter.ChangeData_Changed)
			    {
			        columnNames.Add("ChangeData");
			        arguments.Add("@ChangeData");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@ChangeData";
			        parm.Value = inserter._ChangeData;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.NotificationId_Changed)
			    {
			        columnNames.Add("NotificationId");
			        arguments.Add("@NotificationId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@NotificationId";
			        parm.Value = inserter._NotificationId;
			        command.Parameters.Add(parm);
			    }
			
				string commandString = string.Format("insert into ChangeData ({0}) values ({1})",
			        StringGenerator.GenerateCommaSeperatedList(columnNames),
			        StringGenerator.GenerateCommaSeperatedList(arguments));
			
			    command.CommandText = commandString;
			
			    command.ExecuteNonQuery();
			};
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			}
		}
		
		protected override TKey DoInsertAndReturnPrimaryKey<TKey>(ObjectCloud.DataAccess.User.ChangeData_Table.ChangeData_Inserter inserter)
		{
			object toReturn;
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    List<string> columnNames = new List<string>();
		    List<string> arguments = new List<string>();
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    if (inserter.ChangeData_Changed)
			    {
			        columnNames.Add("ChangeData");
			        arguments.Add("@ChangeData");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@ChangeData";
			        parm.Value = inserter._ChangeData;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.NotificationId_Changed)
			    {
			        columnNames.Add("NotificationId");
			        arguments.Add("@NotificationId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@NotificationId";
			        parm.Value = inserter._NotificationId;
			        command.Parameters.Add(parm);
			    }
			
				string commandString = string.Format("insert into ChangeData ({0}) values ({1});select last_insert_rowid() AS RecordID;",
			        StringGenerator.GenerateCommaSeperatedList(columnNames),
			        StringGenerator.GenerateCommaSeperatedList(arguments));
			
			    command.CommandText = commandString;
			
			    toReturn = command.ExecuteScalar();
			};
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			    return (TKey) toReturn;
			}
		}
			
			
		public override IEnumerable<IChangeData_Readable> Select(ObjectCloud.ORM.DataAccess.WhereConditionals.ComparisonCondition condition, uint? max, OrderBy sortOrder, params ObjectCloud.ORM.DataAccess.Column[] orderBy)
		{
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    StringBuilder commandBuilder = new StringBuilder("select ChangeData, NotificationId from ChangeData");
		
		    using (DbCommand command = Connection.CreateCommand())
		    {
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(ChangeData_Table) != ((Column)entity).Table)
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
			
			            ChangeData_Readable toYield = new ChangeData_Readable();
			
			            if (System.DBNull.Value != values[0])
			              toYield._ChangeData = ((System.String)values[0]);
			            else
			              toYield._ChangeData = default(System.String);

			            if (System.DBNull.Value != values[1])
			              toYield._NotificationId = ((System.Int64)values[1]);
			            else
			              toYield._NotificationId = default(System.Int64);

			
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
		    StringBuilder commandBuilder = new StringBuilder("delete from ChangeData");
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(ChangeData_Table) != ((Column)entity).Table)
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
		
		protected override int DoUpdate(ComparisonCondition condition, ChangeData_Inserter inserter)
		{
			int rowsAffected;
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    StringBuilder commandBuilder = new StringBuilder("update ChangeData");
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    List<string> setStatements = new List<string>();
			
			    if (inserter.ChangeData_Changed)
			    {
			        setStatements.Add("ChangeData = @ChangeData");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@ChangeData";
			        parm.Value = inserter._ChangeData;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.NotificationId_Changed)
			    {
			        setStatements.Add("NotificationId = @NotificationId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@NotificationId";
			        parm.Value = inserter._NotificationId;
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
			                if (typeof(ChangeData_Table) != ((Column)entity).Table)
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
	internal class Sender_Readable : ISender_Readable
	{
		public System.String SenderToken
		{
			get { return _SenderToken; }
		}	
		internal System.String _SenderToken = default(System.String);
		
		public System.String RecipientToken
		{
			get { return _RecipientToken; }
		}	
		internal System.String _RecipientToken = default(System.String);
		
		public System.String OpenID
		{
			get { return _OpenID; }
		}	
		internal System.String _OpenID = default(System.String);
		
	}
	
	public partial class Sender_Table : ObjectCloud.DataAccess.User.Sender_Table
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

		static Sender_Table()
		{
			ObjectCloud.DataAccess.User.Sender_Table._SenderToken = ObjectCloud.ORM.DataAccess.Column.Construct<Sender_Table, ISender_Writable, ISender_Readable>("SenderToken");
			ObjectCloud.DataAccess.User.Sender_Table._RecipientToken = ObjectCloud.ORM.DataAccess.Column.Construct<Sender_Table, ISender_Writable, ISender_Readable>("RecipientToken");
			ObjectCloud.DataAccess.User.Sender_Table._OpenID = ObjectCloud.ORM.DataAccess.Column.Construct<Sender_Table, ISender_Writable, ISender_Readable>("OpenID");
		}
		
		internal DbConnection Connection;
		internal DatabaseConnector DatabaseConnector;
	
		
		internal Sender_Table(DbConnection connection, IEmbeddedDatabaseConnector embeddedDatabaseConnector, DatabaseConnector databaseConnector)
		{
		    Connection = connection;
			EmbeddedDatabaseConnector = embeddedDatabaseConnector;
		    DatabaseConnector = databaseConnector;
		}
		
		protected override void DoInsert(ObjectCloud.DataAccess.User.Sender_Table.Sender_Inserter inserter)
		{
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    List<string> columnNames = new List<string>();
		    List<string> arguments = new List<string>();
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    if (inserter.SenderToken_Changed)
			    {
			        columnNames.Add("SenderToken");
			        arguments.Add("@SenderToken");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@SenderToken";
			        parm.Value = inserter._SenderToken;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.RecipientToken_Changed)
			    {
			        columnNames.Add("RecipientToken");
			        arguments.Add("@RecipientToken");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@RecipientToken";
			        parm.Value = inserter._RecipientToken;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.OpenID_Changed)
			    {
			        columnNames.Add("OpenID");
			        arguments.Add("@OpenID");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@OpenID";
			        parm.Value = inserter._OpenID;
			        command.Parameters.Add(parm);
			    }
			
				string commandString = string.Format("insert into Sender ({0}) values ({1})",
			        StringGenerator.GenerateCommaSeperatedList(columnNames),
			        StringGenerator.GenerateCommaSeperatedList(arguments));
			
			    command.CommandText = commandString;
			
			    command.ExecuteNonQuery();
			};
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			}
		}
		
		protected override TKey DoInsertAndReturnPrimaryKey<TKey>(ObjectCloud.DataAccess.User.Sender_Table.Sender_Inserter inserter)
		{
			object toReturn;
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    List<string> columnNames = new List<string>();
		    List<string> arguments = new List<string>();
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    if (inserter.SenderToken_Changed)
			    {
			        columnNames.Add("SenderToken");
			        arguments.Add("@SenderToken");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@SenderToken";
			        parm.Value = inserter._SenderToken;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.RecipientToken_Changed)
			    {
			        columnNames.Add("RecipientToken");
			        arguments.Add("@RecipientToken");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@RecipientToken";
			        parm.Value = inserter._RecipientToken;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.OpenID_Changed)
			    {
			        columnNames.Add("OpenID");
			        arguments.Add("@OpenID");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@OpenID";
			        parm.Value = inserter._OpenID;
			        command.Parameters.Add(parm);
			    }
			
				string commandString = string.Format("insert into Sender ({0}) values ({1});select last_insert_rowid() AS RecordID;",
			        StringGenerator.GenerateCommaSeperatedList(columnNames),
			        StringGenerator.GenerateCommaSeperatedList(arguments));
			
			    command.CommandText = commandString;
			
			    toReturn = command.ExecuteScalar();
			};
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			    return (TKey) toReturn;
			}
		}
			
			
		public override IEnumerable<ISender_Readable> Select(ObjectCloud.ORM.DataAccess.WhereConditionals.ComparisonCondition condition, uint? max, OrderBy sortOrder, params ObjectCloud.ORM.DataAccess.Column[] orderBy)
		{
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    StringBuilder commandBuilder = new StringBuilder("select SenderToken, RecipientToken, OpenID from Sender");
		
		    using (DbCommand command = Connection.CreateCommand())
		    {
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(Sender_Table) != ((Column)entity).Table)
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
			            object[] values = new object[3];
			            dataReader.GetValues(values);
			
			            Sender_Readable toYield = new Sender_Readable();
			
			            if (System.DBNull.Value != values[0])
			              toYield._SenderToken = ((System.String)values[0]);
			            else
			              toYield._SenderToken = default(System.String);

			            if (System.DBNull.Value != values[1])
			              toYield._RecipientToken = ((System.String)values[1]);
			            else
			              toYield._RecipientToken = default(System.String);

			            if (System.DBNull.Value != values[2])
			              toYield._OpenID = ((System.String)values[2]);
			            else
			              toYield._OpenID = default(System.String);

			
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
		    StringBuilder commandBuilder = new StringBuilder("delete from Sender");
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(Sender_Table) != ((Column)entity).Table)
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
		
		protected override int DoUpdate(ComparisonCondition condition, Sender_Inserter inserter)
		{
			int rowsAffected;
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    StringBuilder commandBuilder = new StringBuilder("update Sender");
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    List<string> setStatements = new List<string>();
			
			    if (inserter.SenderToken_Changed)
			    {
			        setStatements.Add("SenderToken = @SenderToken");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@SenderToken";
			        parm.Value = inserter._SenderToken;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.RecipientToken_Changed)
			    {
			        setStatements.Add("RecipientToken = @RecipientToken");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@RecipientToken";
			        parm.Value = inserter._RecipientToken;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.OpenID_Changed)
			    {
			        setStatements.Add("OpenID = @OpenID");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@OpenID";
			        parm.Value = inserter._OpenID;
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
			                if (typeof(Sender_Table) != ((Column)entity).Table)
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
	internal class Token_Readable : IToken_Readable
	{
		public System.String Token
		{
			get { return _Token; }
		}	
		internal System.String _Token = default(System.String);
		
		public System.DateTime Created
		{
			get { return _Created; }
		}	
		internal System.DateTime _Created = default(System.DateTime);
		
		public System.String OpenId
		{
			get { return _OpenId; }
		}	
		internal System.String _OpenId = default(System.String);
		
	}
	
	public partial class Token_Table : ObjectCloud.DataAccess.User.Token_Table
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

		static Token_Table()
		{
			ObjectCloud.DataAccess.User.Token_Table._Token = ObjectCloud.ORM.DataAccess.Column.Construct<Token_Table, IToken_Writable, IToken_Readable>("Token");
			ObjectCloud.DataAccess.User.Token_Table._Created = ObjectCloud.ORM.DataAccess.Column.Construct<Token_Table, IToken_Writable, IToken_Readable>("Created");
			ObjectCloud.DataAccess.User.Token_Table._OpenId = ObjectCloud.ORM.DataAccess.Column.Construct<Token_Table, IToken_Writable, IToken_Readable>("OpenId");
		}
		
		internal DbConnection Connection;
		internal DatabaseConnector DatabaseConnector;
	
		
		internal Token_Table(DbConnection connection, IEmbeddedDatabaseConnector embeddedDatabaseConnector, DatabaseConnector databaseConnector)
		{
		    Connection = connection;
			EmbeddedDatabaseConnector = embeddedDatabaseConnector;
		    DatabaseConnector = databaseConnector;
		}
		
		protected override void DoInsert(ObjectCloud.DataAccess.User.Token_Table.Token_Inserter inserter)
		{
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    List<string> columnNames = new List<string>();
		    List<string> arguments = new List<string>();
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    if (inserter.Token_Changed)
			    {
			        columnNames.Add("Token");
			        arguments.Add("@Token");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Token";
			        parm.Value = inserter._Token;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.Created_Changed)
			    {
			        columnNames.Add("Created");
			        arguments.Add("@Created");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Created";
			        parm.Value = inserter._Created.Ticks;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.OpenId_Changed)
			    {
			        columnNames.Add("OpenId");
			        arguments.Add("@OpenId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@OpenId";
			        parm.Value = inserter._OpenId;
			        command.Parameters.Add(parm);
			    }
			
				string commandString = string.Format("insert into Token ({0}) values ({1})",
			        StringGenerator.GenerateCommaSeperatedList(columnNames),
			        StringGenerator.GenerateCommaSeperatedList(arguments));
			
			    command.CommandText = commandString;
			
			    command.ExecuteNonQuery();
			};
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			}
		}
		
		protected override TKey DoInsertAndReturnPrimaryKey<TKey>(ObjectCloud.DataAccess.User.Token_Table.Token_Inserter inserter)
		{
			object toReturn;
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    List<string> columnNames = new List<string>();
		    List<string> arguments = new List<string>();
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    if (inserter.Token_Changed)
			    {
			        columnNames.Add("Token");
			        arguments.Add("@Token");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Token";
			        parm.Value = inserter._Token;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.Created_Changed)
			    {
			        columnNames.Add("Created");
			        arguments.Add("@Created");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Created";
			        parm.Value = inserter._Created.Ticks;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.OpenId_Changed)
			    {
			        columnNames.Add("OpenId");
			        arguments.Add("@OpenId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@OpenId";
			        parm.Value = inserter._OpenId;
			        command.Parameters.Add(parm);
			    }
			
				string commandString = string.Format("insert into Token ({0}) values ({1});select last_insert_rowid() AS RecordID;",
			        StringGenerator.GenerateCommaSeperatedList(columnNames),
			        StringGenerator.GenerateCommaSeperatedList(arguments));
			
			    command.CommandText = commandString;
			
			    toReturn = command.ExecuteScalar();
			};
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			    return (TKey) toReturn;
			}
		}
			
			
		public override IEnumerable<IToken_Readable> Select(ObjectCloud.ORM.DataAccess.WhereConditionals.ComparisonCondition condition, uint? max, OrderBy sortOrder, params ObjectCloud.ORM.DataAccess.Column[] orderBy)
		{
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    StringBuilder commandBuilder = new StringBuilder("select Token, Created, OpenId from Token");
		
		    using (DbCommand command = Connection.CreateCommand())
		    {
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(Token_Table) != ((Column)entity).Table)
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
			            object[] values = new object[3];
			            dataReader.GetValues(values);
			
			            Token_Readable toYield = new Token_Readable();
			
			            if (System.DBNull.Value != values[0])
			              toYield._Token = ((System.String)values[0]);
			            else
			              toYield._Token = default(System.String);

			            if (System.DBNull.Value != values[1])
			              toYield._Created = new DateTime(((System.Int64)values[1]));
			            else
			              toYield._Created = default(System.DateTime);

			            if (System.DBNull.Value != values[2])
			              toYield._OpenId = ((System.String)values[2]);
			            else
			              toYield._OpenId = default(System.String);

			
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
		    StringBuilder commandBuilder = new StringBuilder("delete from Token");
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(Token_Table) != ((Column)entity).Table)
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
		
		protected override int DoUpdate(ComparisonCondition condition, Token_Inserter inserter)
		{
			int rowsAffected;
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    StringBuilder commandBuilder = new StringBuilder("update Token");
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    List<string> setStatements = new List<string>();
			
			    if (inserter.Token_Changed)
			    {
			        setStatements.Add("Token = @Token");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Token";
			        parm.Value = inserter._Token;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.Created_Changed)
			    {
			        setStatements.Add("Created = @Created");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Created";
			        parm.Value = inserter._Created.Ticks;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.OpenId_Changed)
			    {
			        setStatements.Add("OpenId = @OpenId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@OpenId";
			        parm.Value = inserter._OpenId;
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
			                if (typeof(Token_Table) != ((Column)entity).Table)
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
	internal class Blocked_Readable : IBlocked_Readable
	{
		public System.String OpenIdorDomain
		{
			get { return _OpenIdorDomain; }
		}	
		internal System.String _OpenIdorDomain = default(System.String);
		
	}
	
	public partial class Blocked_Table : ObjectCloud.DataAccess.User.Blocked_Table
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

		static Blocked_Table()
		{
			ObjectCloud.DataAccess.User.Blocked_Table._OpenIdorDomain = ObjectCloud.ORM.DataAccess.Column.Construct<Blocked_Table, IBlocked_Writable, IBlocked_Readable>("OpenIdorDomain");
		}
		
		internal DbConnection Connection;
		internal DatabaseConnector DatabaseConnector;
	
		
		internal Blocked_Table(DbConnection connection, IEmbeddedDatabaseConnector embeddedDatabaseConnector, DatabaseConnector databaseConnector)
		{
		    Connection = connection;
			EmbeddedDatabaseConnector = embeddedDatabaseConnector;
		    DatabaseConnector = databaseConnector;
		}
		
		protected override void DoInsert(ObjectCloud.DataAccess.User.Blocked_Table.Blocked_Inserter inserter)
		{
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    List<string> columnNames = new List<string>();
		    List<string> arguments = new List<string>();
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    if (inserter.OpenIdorDomain_Changed)
			    {
			        columnNames.Add("OpenIdorDomain");
			        arguments.Add("@OpenIdorDomain");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@OpenIdorDomain";
			        parm.Value = inserter._OpenIdorDomain;
			        command.Parameters.Add(parm);
			    }
			
				string commandString = string.Format("insert into Blocked ({0}) values ({1})",
			        StringGenerator.GenerateCommaSeperatedList(columnNames),
			        StringGenerator.GenerateCommaSeperatedList(arguments));
			
			    command.CommandText = commandString;
			
			    command.ExecuteNonQuery();
			};
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			}
		}
		
		protected override TKey DoInsertAndReturnPrimaryKey<TKey>(ObjectCloud.DataAccess.User.Blocked_Table.Blocked_Inserter inserter)
		{
			object toReturn;
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    List<string> columnNames = new List<string>();
		    List<string> arguments = new List<string>();
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    if (inserter.OpenIdorDomain_Changed)
			    {
			        columnNames.Add("OpenIdorDomain");
			        arguments.Add("@OpenIdorDomain");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@OpenIdorDomain";
			        parm.Value = inserter._OpenIdorDomain;
			        command.Parameters.Add(parm);
			    }
			
				string commandString = string.Format("insert into Blocked ({0}) values ({1});select last_insert_rowid() AS RecordID;",
			        StringGenerator.GenerateCommaSeperatedList(columnNames),
			        StringGenerator.GenerateCommaSeperatedList(arguments));
			
			    command.CommandText = commandString;
			
			    toReturn = command.ExecuteScalar();
			};
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			    return (TKey) toReturn;
			}
		}
			
			
		public override IEnumerable<IBlocked_Readable> Select(ObjectCloud.ORM.DataAccess.WhereConditionals.ComparisonCondition condition, uint? max, OrderBy sortOrder, params ObjectCloud.ORM.DataAccess.Column[] orderBy)
		{
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    StringBuilder commandBuilder = new StringBuilder("select OpenIdorDomain from Blocked");
		
		    using (DbCommand command = Connection.CreateCommand())
		    {
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(Blocked_Table) != ((Column)entity).Table)
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
			            object[] values = new object[1];
			            dataReader.GetValues(values);
			
			            Blocked_Readable toYield = new Blocked_Readable();
			
			            if (System.DBNull.Value != values[0])
			              toYield._OpenIdorDomain = ((System.String)values[0]);
			            else
			              toYield._OpenIdorDomain = default(System.String);

			
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
		    StringBuilder commandBuilder = new StringBuilder("delete from Blocked");
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(Blocked_Table) != ((Column)entity).Table)
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
		
		protected override int DoUpdate(ComparisonCondition condition, Blocked_Inserter inserter)
		{
			int rowsAffected;
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    StringBuilder commandBuilder = new StringBuilder("update Blocked");
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    List<string> setStatements = new List<string>();
			
			    if (inserter.OpenIdorDomain_Changed)
			    {
			        setStatements.Add("OpenIdorDomain = @OpenIdorDomain");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@OpenIdorDomain";
			        parm.Value = inserter._OpenIdorDomain;
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
			                if (typeof(Blocked_Table) != ((Column)entity).Table)
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
	internal class ObjectState_Readable : IObjectState_Readable
	{
		public System.Int32 ObjectState
		{
			get { return _ObjectState; }
		}	
		internal System.Int32 _ObjectState = default(System.Int32);
		
		public System.String ObjectUrl
		{
			get { return _ObjectUrl; }
		}	
		internal System.String _ObjectUrl = default(System.String);
		
	}
	
	public partial class ObjectState_Table : ObjectCloud.DataAccess.User.ObjectState_Table
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

		static ObjectState_Table()
		{
			ObjectCloud.DataAccess.User.ObjectState_Table._ObjectState = ObjectCloud.ORM.DataAccess.Column.Construct<ObjectState_Table, IObjectState_Writable, IObjectState_Readable>("ObjectState");
			ObjectCloud.DataAccess.User.ObjectState_Table._ObjectUrl = ObjectCloud.ORM.DataAccess.Column.Construct<ObjectState_Table, IObjectState_Writable, IObjectState_Readable>("ObjectUrl");
		}
		
		internal DbConnection Connection;
		internal DatabaseConnector DatabaseConnector;
	
		
		internal ObjectState_Table(DbConnection connection, IEmbeddedDatabaseConnector embeddedDatabaseConnector, DatabaseConnector databaseConnector)
		{
		    Connection = connection;
			EmbeddedDatabaseConnector = embeddedDatabaseConnector;
		    DatabaseConnector = databaseConnector;
		}
		
		protected override void DoInsert(ObjectCloud.DataAccess.User.ObjectState_Table.ObjectState_Inserter inserter)
		{
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    List<string> columnNames = new List<string>();
		    List<string> arguments = new List<string>();
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    if (inserter.ObjectState_Changed)
			    {
			        columnNames.Add("ObjectState");
			        arguments.Add("@ObjectState");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@ObjectState";
			        parm.Value = inserter._ObjectState;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.ObjectUrl_Changed)
			    {
			        columnNames.Add("ObjectUrl");
			        arguments.Add("@ObjectUrl");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@ObjectUrl";
			        parm.Value = inserter._ObjectUrl;
			        command.Parameters.Add(parm);
			    }
			
				string commandString = string.Format("insert into ObjectState ({0}) values ({1})",
			        StringGenerator.GenerateCommaSeperatedList(columnNames),
			        StringGenerator.GenerateCommaSeperatedList(arguments));
			
			    command.CommandText = commandString;
			
			    command.ExecuteNonQuery();
			};
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			}
		}
		
		protected override TKey DoInsertAndReturnPrimaryKey<TKey>(ObjectCloud.DataAccess.User.ObjectState_Table.ObjectState_Inserter inserter)
		{
			object toReturn;
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    List<string> columnNames = new List<string>();
		    List<string> arguments = new List<string>();
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    if (inserter.ObjectState_Changed)
			    {
			        columnNames.Add("ObjectState");
			        arguments.Add("@ObjectState");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@ObjectState";
			        parm.Value = inserter._ObjectState;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.ObjectUrl_Changed)
			    {
			        columnNames.Add("ObjectUrl");
			        arguments.Add("@ObjectUrl");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@ObjectUrl";
			        parm.Value = inserter._ObjectUrl;
			        command.Parameters.Add(parm);
			    }
			
				string commandString = string.Format("insert into ObjectState ({0}) values ({1});select last_insert_rowid() AS RecordID;",
			        StringGenerator.GenerateCommaSeperatedList(columnNames),
			        StringGenerator.GenerateCommaSeperatedList(arguments));
			
			    command.CommandText = commandString;
			
			    toReturn = command.ExecuteScalar();
			};
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			    return (TKey) toReturn;
			}
		}
			
			
		public override IEnumerable<IObjectState_Readable> Select(ObjectCloud.ORM.DataAccess.WhereConditionals.ComparisonCondition condition, uint? max, OrderBy sortOrder, params ObjectCloud.ORM.DataAccess.Column[] orderBy)
		{
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    StringBuilder commandBuilder = new StringBuilder("select ObjectState, ObjectUrl from ObjectState");
		
		    using (DbCommand command = Connection.CreateCommand())
		    {
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(ObjectState_Table) != ((Column)entity).Table)
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
			
			            ObjectState_Readable toYield = new ObjectState_Readable();
			
			            if (System.DBNull.Value != values[0])
			              toYield._ObjectState = Convert.ToInt32(values[0]);
			            else
			              toYield._ObjectState = default(System.Int32);

			            if (System.DBNull.Value != values[1])
			              toYield._ObjectUrl = ((System.String)values[1]);
			            else
			              toYield._ObjectUrl = default(System.String);

			
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
		    StringBuilder commandBuilder = new StringBuilder("delete from ObjectState");
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(ObjectState_Table) != ((Column)entity).Table)
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
		
		protected override int DoUpdate(ComparisonCondition condition, ObjectState_Inserter inserter)
		{
			int rowsAffected;
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    StringBuilder commandBuilder = new StringBuilder("update ObjectState");
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    List<string> setStatements = new List<string>();
			
			    if (inserter.ObjectState_Changed)
			    {
			        setStatements.Add("ObjectState = @ObjectState");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@ObjectState";
			        parm.Value = inserter._ObjectState;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.ObjectUrl_Changed)
			    {
			        setStatements.Add("ObjectUrl = @ObjectUrl");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@ObjectUrl";
			        parm.Value = inserter._ObjectUrl;
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
			                if (typeof(ObjectState_Table) != ((Column)entity).Table)
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
	internal class Deleted_Readable : IDeleted_Readable
	{
		public System.String OpenId
		{
			get { return _OpenId; }
		}	
		internal System.String _OpenId = default(System.String);
		
		public System.String ObjectUrl
		{
			get { return _ObjectUrl; }
		}	
		internal System.String _ObjectUrl = default(System.String);
		
	}
	
	public partial class Deleted_Table : ObjectCloud.DataAccess.User.Deleted_Table
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

		static Deleted_Table()
		{
			ObjectCloud.DataAccess.User.Deleted_Table._OpenId = ObjectCloud.ORM.DataAccess.Column.Construct<Deleted_Table, IDeleted_Writable, IDeleted_Readable>("OpenId");
			ObjectCloud.DataAccess.User.Deleted_Table._ObjectUrl = ObjectCloud.ORM.DataAccess.Column.Construct<Deleted_Table, IDeleted_Writable, IDeleted_Readable>("ObjectUrl");
		}
		
		internal DbConnection Connection;
		internal DatabaseConnector DatabaseConnector;
	
		
		internal Deleted_Table(DbConnection connection, IEmbeddedDatabaseConnector embeddedDatabaseConnector, DatabaseConnector databaseConnector)
		{
		    Connection = connection;
			EmbeddedDatabaseConnector = embeddedDatabaseConnector;
		    DatabaseConnector = databaseConnector;
		}
		
		protected override void DoInsert(ObjectCloud.DataAccess.User.Deleted_Table.Deleted_Inserter inserter)
		{
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    List<string> columnNames = new List<string>();
		    List<string> arguments = new List<string>();
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    if (inserter.OpenId_Changed)
			    {
			        columnNames.Add("OpenId");
			        arguments.Add("@OpenId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@OpenId";
			        parm.Value = inserter._OpenId;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.ObjectUrl_Changed)
			    {
			        columnNames.Add("ObjectUrl");
			        arguments.Add("@ObjectUrl");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@ObjectUrl";
			        parm.Value = inserter._ObjectUrl;
			        command.Parameters.Add(parm);
			    }
			
				string commandString = string.Format("insert into Deleted ({0}) values ({1})",
			        StringGenerator.GenerateCommaSeperatedList(columnNames),
			        StringGenerator.GenerateCommaSeperatedList(arguments));
			
			    command.CommandText = commandString;
			
			    command.ExecuteNonQuery();
			};
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			}
		}
		
		protected override TKey DoInsertAndReturnPrimaryKey<TKey>(ObjectCloud.DataAccess.User.Deleted_Table.Deleted_Inserter inserter)
		{
			object toReturn;
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    List<string> columnNames = new List<string>();
		    List<string> arguments = new List<string>();
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    if (inserter.OpenId_Changed)
			    {
			        columnNames.Add("OpenId");
			        arguments.Add("@OpenId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@OpenId";
			        parm.Value = inserter._OpenId;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.ObjectUrl_Changed)
			    {
			        columnNames.Add("ObjectUrl");
			        arguments.Add("@ObjectUrl");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@ObjectUrl";
			        parm.Value = inserter._ObjectUrl;
			        command.Parameters.Add(parm);
			    }
			
				string commandString = string.Format("insert into Deleted ({0}) values ({1});select last_insert_rowid() AS RecordID;",
			        StringGenerator.GenerateCommaSeperatedList(columnNames),
			        StringGenerator.GenerateCommaSeperatedList(arguments));
			
			    command.CommandText = commandString;
			
			    toReturn = command.ExecuteScalar();
			};
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			    return (TKey) toReturn;
			}
		}
			
			
		public override IEnumerable<IDeleted_Readable> Select(ObjectCloud.ORM.DataAccess.WhereConditionals.ComparisonCondition condition, uint? max, OrderBy sortOrder, params ObjectCloud.ORM.DataAccess.Column[] orderBy)
		{
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    StringBuilder commandBuilder = new StringBuilder("select OpenId, ObjectUrl from Deleted");
		
		    using (DbCommand command = Connection.CreateCommand())
		    {
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(Deleted_Table) != ((Column)entity).Table)
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
			
			            Deleted_Readable toYield = new Deleted_Readable();
			
			            if (System.DBNull.Value != values[0])
			              toYield._OpenId = ((System.String)values[0]);
			            else
			              toYield._OpenId = default(System.String);

			            if (System.DBNull.Value != values[1])
			              toYield._ObjectUrl = ((System.String)values[1]);
			            else
			              toYield._ObjectUrl = default(System.String);

			
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
		    StringBuilder commandBuilder = new StringBuilder("delete from Deleted");
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(Deleted_Table) != ((Column)entity).Table)
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
		
		protected override int DoUpdate(ComparisonCondition condition, Deleted_Inserter inserter)
		{
			int rowsAffected;
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    StringBuilder commandBuilder = new StringBuilder("update Deleted");
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    List<string> setStatements = new List<string>();
			
			    if (inserter.OpenId_Changed)
			    {
			        setStatements.Add("OpenId = @OpenId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@OpenId";
			        parm.Value = inserter._OpenId;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.ObjectUrl_Changed)
			    {
			        setStatements.Add("ObjectUrl = @ObjectUrl");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@ObjectUrl";
			        parm.Value = inserter._ObjectUrl;
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
			                if (typeof(Deleted_Table) != ((Column)entity).Table)
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