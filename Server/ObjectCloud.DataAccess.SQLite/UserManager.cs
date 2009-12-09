using ObjectCloud.Common;
using ObjectCloud.DataAccess.UserManager;
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

namespace ObjectCloud.DataAccess.SQLite.UserManager
{
    public partial class EmbeddedDatabaseCreator : ObjectCloud.DataAccess.UserManager.IEmbeddedDatabaseCreator
    {
        /// <summary>
        /// Schema creation sql
        /// </summary>
        const string schemaSql =
@"create table Users 
(
	PasswordMD5			string not null,
	ID			guid not null unique,
	BuiltIn			boolean not null,
	Name			string not null	primary key
);Create index Users_ID on Users (ID);
create table Groups 
(
	ID			guid not null unique,
	OwnerID			guid,
	BuiltIn			boolean not null,
	Automatic			boolean not null,
	Name			string not null	primary key
);Create index Groups_ID on Groups (ID);
create table UserInGroups 
(
	UserID			guid not null,
	GroupID			guid not null
);Create index UserInGroups_UserID on UserInGroups (UserID);
Create index UserInGroups_GroupID on UserInGroups (GroupID);
create table AssociationHandles 
(
	UserID			guid not null,
	AssociationHandle			string not null,
	Timestamp			integer not null
);Create index AssociationHandles_UserID on AssociationHandles (UserID);

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

	public class DatabaseConnectorFactory : ObjectCloud.DataAccess.UserManager.IDatabaseConnectorFactory
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
		
	partial class DatabaseTransaction : ObjectCloud.DataAccess.UserManager.IDatabaseTransaction
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
			
			_Users_Table = new Users_Table(sqlConnection, EmbeddedDatabaseConnector, databaseConnector);
			_Groups_Table = new Groups_Table(sqlConnection, EmbeddedDatabaseConnector, databaseConnector);
			_UserInGroups_Table = new UserInGroups_Table(sqlConnection, EmbeddedDatabaseConnector, databaseConnector);
			_AssociationHandles_Table = new AssociationHandles_Table(sqlConnection, EmbeddedDatabaseConnector, databaseConnector);
		}
		
		public void Dispose()
		{
			using (TimedLock.Lock(sqlConnection)){
				sqlConnection.Close();
				sqlConnection.Dispose();
			}
		}
		
		public T CallOnTransaction<T>(GenericArgumentReturn<ObjectCloud.DataAccess.UserManager.IDatabaseTransaction, T> del)
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
		
		public void CallOnTransaction(GenericArgument<ObjectCloud.DataAccess.UserManager.IDatabaseTransaction> del)
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
		
		public ObjectCloud.DataAccess.UserManager.Users_Table Users
		{
			get { return _Users_Table; }
		}
		private Users_Table _Users_Table;
		public ObjectCloud.DataAccess.UserManager.Groups_Table Groups
		{
			get { return _Groups_Table; }
		}
		private Groups_Table _Groups_Table;
		public ObjectCloud.DataAccess.UserManager.UserInGroups_Table UserInGroups
		{
			get { return _UserInGroups_Table; }
		}
		private UserInGroups_Table _UserInGroups_Table;
		public ObjectCloud.DataAccess.UserManager.AssociationHandles_Table AssociationHandles
		{
			get { return _AssociationHandles_Table; }
		}
		private AssociationHandles_Table _AssociationHandles_Table;
		
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
		
	internal class Users_Readable : IUsers_Readable
	{
		public System.String PasswordMD5
		{
			get { return _PasswordMD5; }
		}	
		internal System.String _PasswordMD5 = default(System.String);
		
		public ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid> ID
		{
			get { return _ID; }
		}	
		internal ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid> _ID = default(ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>);
		
		public System.Boolean BuiltIn
		{
			get { return _BuiltIn; }
		}	
		internal System.Boolean _BuiltIn = default(System.Boolean);
		
		public System.String Name
		{
			get { return _Name; }
		}	
		internal System.String _Name = default(System.String);
		
	}
	
	public partial class Users_Table : ObjectCloud.DataAccess.UserManager.Users_Table
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

		static Users_Table()
		{
			ObjectCloud.DataAccess.UserManager.Users_Table._PasswordMD5 = ObjectCloud.ORM.DataAccess.Column.Construct<Users_Table, IUsers_Writable, IUsers_Readable>("PasswordMD5");
			ObjectCloud.DataAccess.UserManager.Users_Table._ID = ObjectCloud.ORM.DataAccess.Column.Construct<Users_Table, IUsers_Writable, IUsers_Readable>("ID");
			ObjectCloud.DataAccess.UserManager.Users_Table._BuiltIn = ObjectCloud.ORM.DataAccess.Column.Construct<Users_Table, IUsers_Writable, IUsers_Readable>("BuiltIn");
			ObjectCloud.DataAccess.UserManager.Users_Table._Name = ObjectCloud.ORM.DataAccess.Column.Construct<Users_Table, IUsers_Writable, IUsers_Readable>("Name");
		}
		
		internal DbConnection Connection;
		internal DatabaseConnector DatabaseConnector;
	
		
		internal Users_Table(DbConnection connection, IEmbeddedDatabaseConnector embeddedDatabaseConnector, DatabaseConnector databaseConnector)
		{
		    Connection = connection;
			EmbeddedDatabaseConnector = embeddedDatabaseConnector;
		    DatabaseConnector = databaseConnector;
		}
		
		protected override void DoInsert(ObjectCloud.DataAccess.UserManager.Users_Table.Users_Inserter inserter)
		{
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    List<string> columnNames = new List<string>();
		    List<string> arguments = new List<string>();
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    if (inserter.PasswordMD5_Changed)
			    {
			        columnNames.Add("PasswordMD5");
			        arguments.Add("@PasswordMD5");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@PasswordMD5";
			        parm.Value = inserter._PasswordMD5;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.ID_Changed)
			    {
			        columnNames.Add("ID");
			        arguments.Add("@ID");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@ID";
			        parm.Value = inserter._ID.Value;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.BuiltIn_Changed)
			    {
			        columnNames.Add("BuiltIn");
			        arguments.Add("@BuiltIn");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@BuiltIn";
			        parm.Value = inserter._BuiltIn ? 1 : 0;
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
			
				string commandString = string.Format("insert into Users ({0}) values ({1})",
			        StringGenerator.GenerateCommaSeperatedList(columnNames),
			        StringGenerator.GenerateCommaSeperatedList(arguments));
			
			    command.CommandText = commandString;
			
			    command.ExecuteNonQuery();
			};
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			}
		}
		
		protected override TKey DoInsertAndReturnPrimaryKey<TKey>(ObjectCloud.DataAccess.UserManager.Users_Table.Users_Inserter inserter)
		{
			object toReturn;
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    List<string> columnNames = new List<string>();
		    List<string> arguments = new List<string>();
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    if (inserter.PasswordMD5_Changed)
			    {
			        columnNames.Add("PasswordMD5");
			        arguments.Add("@PasswordMD5");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@PasswordMD5";
			        parm.Value = inserter._PasswordMD5;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.ID_Changed)
			    {
			        columnNames.Add("ID");
			        arguments.Add("@ID");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@ID";
			        parm.Value = inserter._ID.Value;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.BuiltIn_Changed)
			    {
			        columnNames.Add("BuiltIn");
			        arguments.Add("@BuiltIn");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@BuiltIn";
			        parm.Value = inserter._BuiltIn ? 1 : 0;
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
			
				string commandString = string.Format("insert into Users ({0}) values ({1});select last_insert_rowid() AS RecordID;",
			        StringGenerator.GenerateCommaSeperatedList(columnNames),
			        StringGenerator.GenerateCommaSeperatedList(arguments));
			
			    command.CommandText = commandString;
			
			    toReturn = command.ExecuteScalar();
			};
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			    return (TKey) toReturn;
			}
		}
			
			
		public override IEnumerable<IUsers_Readable> Select(ObjectCloud.ORM.DataAccess.WhereConditionals.ComparisonCondition condition, uint? max, OrderBy sortOrder, params ObjectCloud.ORM.DataAccess.Column[] orderBy)
		{
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    StringBuilder commandBuilder = new StringBuilder("select PasswordMD5, ID, BuiltIn, Name from Users");
		
		    using (DbCommand command = Connection.CreateCommand())
		    {
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(Users_Table) != ((Column)entity).Table)
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
			            object[] values = new object[4];
			            dataReader.GetValues(values);
			
			            Users_Readable toYield = new Users_Readable();
			
			            if (System.DBNull.Value != values[0])
			              toYield._PasswordMD5 = ((System.String)values[0]);
			            else
			              toYield._PasswordMD5 = default(System.String);

			            if (System.DBNull.Value != values[1])
			              toYield._ID = new ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>(((System.Guid)values[1]));
			            else
			              toYield._ID = default(ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>);

			            if (System.DBNull.Value != values[2])
			              toYield._BuiltIn = 1 == Convert.ToInt32(values[2]);
			            else
			              toYield._BuiltIn = default(System.Boolean);

			            if (System.DBNull.Value != values[3])
			              toYield._Name = ((System.String)values[3]);
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
		    StringBuilder commandBuilder = new StringBuilder("delete from Users");
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(Users_Table) != ((Column)entity).Table)
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
		
		protected override int DoUpdate(ComparisonCondition condition, Users_Inserter inserter)
		{
			int rowsAffected;
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    StringBuilder commandBuilder = new StringBuilder("update Users");
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    List<string> setStatements = new List<string>();
			
			    if (inserter.PasswordMD5_Changed)
			    {
			        setStatements.Add("PasswordMD5 = @PasswordMD5");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@PasswordMD5";
			        parm.Value = inserter._PasswordMD5;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.ID_Changed)
			    {
			        setStatements.Add("ID = @ID");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@ID";
			        parm.Value = inserter._ID.Value;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.BuiltIn_Changed)
			    {
			        setStatements.Add("BuiltIn = @BuiltIn");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@BuiltIn";
			        parm.Value = inserter._BuiltIn ? 1 : 0;
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
			                if (typeof(Users_Table) != ((Column)entity).Table)
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
	internal class Groups_Readable : IGroups_Readable
	{
		public ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid> ID
		{
			get { return _ID; }
		}	
		internal ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid> _ID = default(ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>);
		
		public System.Nullable<ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>> OwnerID
		{
			get { return _OwnerID; }
		}	
		internal System.Nullable<ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>> _OwnerID = default(System.Nullable<ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>>);
		
		public System.Boolean BuiltIn
		{
			get { return _BuiltIn; }
		}	
		internal System.Boolean _BuiltIn = default(System.Boolean);
		
		public System.Boolean Automatic
		{
			get { return _Automatic; }
		}	
		internal System.Boolean _Automatic = default(System.Boolean);
		
		public System.String Name
		{
			get { return _Name; }
		}	
		internal System.String _Name = default(System.String);
		
	}
	
	public partial class Groups_Table : ObjectCloud.DataAccess.UserManager.Groups_Table
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

		static Groups_Table()
		{
			ObjectCloud.DataAccess.UserManager.Groups_Table._ID = ObjectCloud.ORM.DataAccess.Column.Construct<Groups_Table, IGroups_Writable, IGroups_Readable>("ID");
			ObjectCloud.DataAccess.UserManager.Groups_Table._OwnerID = ObjectCloud.ORM.DataAccess.Column.Construct<Groups_Table, IGroups_Writable, IGroups_Readable>("OwnerID");
			ObjectCloud.DataAccess.UserManager.Groups_Table._BuiltIn = ObjectCloud.ORM.DataAccess.Column.Construct<Groups_Table, IGroups_Writable, IGroups_Readable>("BuiltIn");
			ObjectCloud.DataAccess.UserManager.Groups_Table._Automatic = ObjectCloud.ORM.DataAccess.Column.Construct<Groups_Table, IGroups_Writable, IGroups_Readable>("Automatic");
			ObjectCloud.DataAccess.UserManager.Groups_Table._Name = ObjectCloud.ORM.DataAccess.Column.Construct<Groups_Table, IGroups_Writable, IGroups_Readable>("Name");
		}
		
		internal DbConnection Connection;
		internal DatabaseConnector DatabaseConnector;
	
		
		internal Groups_Table(DbConnection connection, IEmbeddedDatabaseConnector embeddedDatabaseConnector, DatabaseConnector databaseConnector)
		{
		    Connection = connection;
			EmbeddedDatabaseConnector = embeddedDatabaseConnector;
		    DatabaseConnector = databaseConnector;
		}
		
		protected override void DoInsert(ObjectCloud.DataAccess.UserManager.Groups_Table.Groups_Inserter inserter)
		{
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    List<string> columnNames = new List<string>();
		    List<string> arguments = new List<string>();
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    if (inserter.ID_Changed)
			    {
			        columnNames.Add("ID");
			        arguments.Add("@ID");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@ID";
			        parm.Value = inserter._ID.Value;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.OwnerID_Changed)
			    {
			        columnNames.Add("OwnerID");
			        arguments.Add("@OwnerID");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@OwnerID";
			        parm.Value = ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>.GetValueOrNull(inserter._OwnerID);
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.BuiltIn_Changed)
			    {
			        columnNames.Add("BuiltIn");
			        arguments.Add("@BuiltIn");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@BuiltIn";
			        parm.Value = inserter._BuiltIn ? 1 : 0;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.Automatic_Changed)
			    {
			        columnNames.Add("Automatic");
			        arguments.Add("@Automatic");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Automatic";
			        parm.Value = inserter._Automatic ? 1 : 0;
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
			
				string commandString = string.Format("insert into Groups ({0}) values ({1})",
			        StringGenerator.GenerateCommaSeperatedList(columnNames),
			        StringGenerator.GenerateCommaSeperatedList(arguments));
			
			    command.CommandText = commandString;
			
			    command.ExecuteNonQuery();
			};
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			}
		}
		
		protected override TKey DoInsertAndReturnPrimaryKey<TKey>(ObjectCloud.DataAccess.UserManager.Groups_Table.Groups_Inserter inserter)
		{
			object toReturn;
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    List<string> columnNames = new List<string>();
		    List<string> arguments = new List<string>();
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    if (inserter.ID_Changed)
			    {
			        columnNames.Add("ID");
			        arguments.Add("@ID");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@ID";
			        parm.Value = inserter._ID.Value;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.OwnerID_Changed)
			    {
			        columnNames.Add("OwnerID");
			        arguments.Add("@OwnerID");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@OwnerID";
			        parm.Value = ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>.GetValueOrNull(inserter._OwnerID);
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.BuiltIn_Changed)
			    {
			        columnNames.Add("BuiltIn");
			        arguments.Add("@BuiltIn");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@BuiltIn";
			        parm.Value = inserter._BuiltIn ? 1 : 0;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.Automatic_Changed)
			    {
			        columnNames.Add("Automatic");
			        arguments.Add("@Automatic");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Automatic";
			        parm.Value = inserter._Automatic ? 1 : 0;
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
			
				string commandString = string.Format("insert into Groups ({0}) values ({1});select last_insert_rowid() AS RecordID;",
			        StringGenerator.GenerateCommaSeperatedList(columnNames),
			        StringGenerator.GenerateCommaSeperatedList(arguments));
			
			    command.CommandText = commandString;
			
			    toReturn = command.ExecuteScalar();
			};
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			    return (TKey) toReturn;
			}
		}
			
			
		public override IEnumerable<IGroups_Readable> Select(ObjectCloud.ORM.DataAccess.WhereConditionals.ComparisonCondition condition, uint? max, OrderBy sortOrder, params ObjectCloud.ORM.DataAccess.Column[] orderBy)
		{
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    StringBuilder commandBuilder = new StringBuilder("select ID, OwnerID, BuiltIn, Automatic, Name from Groups");
		
		    using (DbCommand command = Connection.CreateCommand())
		    {
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(Groups_Table) != ((Column)entity).Table)
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
			
			            Groups_Readable toYield = new Groups_Readable();
			
			            if (System.DBNull.Value != values[0])
			              toYield._ID = new ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>(((System.Guid)values[0]));
			            else
			              toYield._ID = default(ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>);

			            if (System.DBNull.Value != values[1])
			              toYield._OwnerID = new ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>(((System.Guid)values[1]));
			            else
			              toYield._OwnerID = default(System.Nullable<ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>>);

			            if (System.DBNull.Value != values[2])
			              toYield._BuiltIn = 1 == Convert.ToInt32(values[2]);
			            else
			              toYield._BuiltIn = default(System.Boolean);

			            if (System.DBNull.Value != values[3])
			              toYield._Automatic = 1 == Convert.ToInt32(values[3]);
			            else
			              toYield._Automatic = default(System.Boolean);

			            if (System.DBNull.Value != values[4])
			              toYield._Name = ((System.String)values[4]);
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
		    StringBuilder commandBuilder = new StringBuilder("delete from Groups");
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(Groups_Table) != ((Column)entity).Table)
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
		
		protected override int DoUpdate(ComparisonCondition condition, Groups_Inserter inserter)
		{
			int rowsAffected;
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    StringBuilder commandBuilder = new StringBuilder("update Groups");
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    List<string> setStatements = new List<string>();
			
			    if (inserter.ID_Changed)
			    {
			        setStatements.Add("ID = @ID");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@ID";
			        parm.Value = inserter._ID.Value;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.OwnerID_Changed)
			    {
			        setStatements.Add("OwnerID = @OwnerID");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@OwnerID";
			        parm.Value = ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>.GetValueOrNull(inserter._OwnerID);
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.BuiltIn_Changed)
			    {
			        setStatements.Add("BuiltIn = @BuiltIn");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@BuiltIn";
			        parm.Value = inserter._BuiltIn ? 1 : 0;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.Automatic_Changed)
			    {
			        setStatements.Add("Automatic = @Automatic");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Automatic";
			        parm.Value = inserter._Automatic ? 1 : 0;
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
			                if (typeof(Groups_Table) != ((Column)entity).Table)
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
	internal class UserInGroups_Readable : IUserInGroups_Readable
	{
		public ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid> UserID
		{
			get { return _UserID; }
		}	
		internal ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid> _UserID = default(ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>);
		
		public ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid> GroupID
		{
			get { return _GroupID; }
		}	
		internal ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid> _GroupID = default(ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>);
		
	}
	
	public partial class UserInGroups_Table : ObjectCloud.DataAccess.UserManager.UserInGroups_Table
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

		static UserInGroups_Table()
		{
			ObjectCloud.DataAccess.UserManager.UserInGroups_Table._UserID = ObjectCloud.ORM.DataAccess.Column.Construct<UserInGroups_Table, IUserInGroups_Writable, IUserInGroups_Readable>("UserID");
			ObjectCloud.DataAccess.UserManager.UserInGroups_Table._GroupID = ObjectCloud.ORM.DataAccess.Column.Construct<UserInGroups_Table, IUserInGroups_Writable, IUserInGroups_Readable>("GroupID");
		}
		
		internal DbConnection Connection;
		internal DatabaseConnector DatabaseConnector;
	
		
		internal UserInGroups_Table(DbConnection connection, IEmbeddedDatabaseConnector embeddedDatabaseConnector, DatabaseConnector databaseConnector)
		{
		    Connection = connection;
			EmbeddedDatabaseConnector = embeddedDatabaseConnector;
		    DatabaseConnector = databaseConnector;
		}
		
		protected override void DoInsert(ObjectCloud.DataAccess.UserManager.UserInGroups_Table.UserInGroups_Inserter inserter)
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
			
			    if (inserter.GroupID_Changed)
			    {
			        columnNames.Add("GroupID");
			        arguments.Add("@GroupID");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@GroupID";
			        parm.Value = inserter._GroupID.Value;
			        command.Parameters.Add(parm);
			    }
			
				string commandString = string.Format("insert into UserInGroups ({0}) values ({1})",
			        StringGenerator.GenerateCommaSeperatedList(columnNames),
			        StringGenerator.GenerateCommaSeperatedList(arguments));
			
			    command.CommandText = commandString;
			
			    command.ExecuteNonQuery();
			};
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			}
		}
		
		protected override TKey DoInsertAndReturnPrimaryKey<TKey>(ObjectCloud.DataAccess.UserManager.UserInGroups_Table.UserInGroups_Inserter inserter)
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
			
			    if (inserter.GroupID_Changed)
			    {
			        columnNames.Add("GroupID");
			        arguments.Add("@GroupID");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@GroupID";
			        parm.Value = inserter._GroupID.Value;
			        command.Parameters.Add(parm);
			    }
			
				string commandString = string.Format("insert into UserInGroups ({0}) values ({1});select last_insert_rowid() AS RecordID;",
			        StringGenerator.GenerateCommaSeperatedList(columnNames),
			        StringGenerator.GenerateCommaSeperatedList(arguments));
			
			    command.CommandText = commandString;
			
			    toReturn = command.ExecuteScalar();
			};
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			    return (TKey) toReturn;
			}
		}
			
			
		public override IEnumerable<IUserInGroups_Readable> Select(ObjectCloud.ORM.DataAccess.WhereConditionals.ComparisonCondition condition, uint? max, OrderBy sortOrder, params ObjectCloud.ORM.DataAccess.Column[] orderBy)
		{
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    StringBuilder commandBuilder = new StringBuilder("select UserID, GroupID from UserInGroups");
		
		    using (DbCommand command = Connection.CreateCommand())
		    {
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(UserInGroups_Table) != ((Column)entity).Table)
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
			
			            UserInGroups_Readable toYield = new UserInGroups_Readable();
			
			            if (System.DBNull.Value != values[0])
			              toYield._UserID = new ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>(((System.Guid)values[0]));
			            else
			              toYield._UserID = default(ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>);

			            if (System.DBNull.Value != values[1])
			              toYield._GroupID = new ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>(((System.Guid)values[1]));
			            else
			              toYield._GroupID = default(ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>);

			
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
		    StringBuilder commandBuilder = new StringBuilder("delete from UserInGroups");
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(UserInGroups_Table) != ((Column)entity).Table)
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
		
		protected override int DoUpdate(ComparisonCondition condition, UserInGroups_Inserter inserter)
		{
			int rowsAffected;
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    StringBuilder commandBuilder = new StringBuilder("update UserInGroups");
		
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
			
			    if (inserter.GroupID_Changed)
			    {
			        setStatements.Add("GroupID = @GroupID");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@GroupID";
			        parm.Value = inserter._GroupID.Value;
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
			                if (typeof(UserInGroups_Table) != ((Column)entity).Table)
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
	internal class AssociationHandles_Readable : IAssociationHandles_Readable
	{
		public ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUser, System.Guid> UserID
		{
			get { return _UserID; }
		}	
		internal ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUser, System.Guid> _UserID = default(ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUser, System.Guid>);
		
		public System.String AssociationHandle
		{
			get { return _AssociationHandle; }
		}	
		internal System.String _AssociationHandle = default(System.String);
		
		public System.DateTime Timestamp
		{
			get { return _Timestamp; }
		}	
		internal System.DateTime _Timestamp = default(System.DateTime);
		
	}
	
	public partial class AssociationHandles_Table : ObjectCloud.DataAccess.UserManager.AssociationHandles_Table
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

		static AssociationHandles_Table()
		{
			ObjectCloud.DataAccess.UserManager.AssociationHandles_Table._UserID = ObjectCloud.ORM.DataAccess.Column.Construct<AssociationHandles_Table, IAssociationHandles_Writable, IAssociationHandles_Readable>("UserID");
			ObjectCloud.DataAccess.UserManager.AssociationHandles_Table._AssociationHandle = ObjectCloud.ORM.DataAccess.Column.Construct<AssociationHandles_Table, IAssociationHandles_Writable, IAssociationHandles_Readable>("AssociationHandle");
			ObjectCloud.DataAccess.UserManager.AssociationHandles_Table._Timestamp = ObjectCloud.ORM.DataAccess.Column.Construct<AssociationHandles_Table, IAssociationHandles_Writable, IAssociationHandles_Readable>("Timestamp");
		}
		
		internal DbConnection Connection;
		internal DatabaseConnector DatabaseConnector;
	
		
		internal AssociationHandles_Table(DbConnection connection, IEmbeddedDatabaseConnector embeddedDatabaseConnector, DatabaseConnector databaseConnector)
		{
		    Connection = connection;
			EmbeddedDatabaseConnector = embeddedDatabaseConnector;
		    DatabaseConnector = databaseConnector;
		}
		
		protected override void DoInsert(ObjectCloud.DataAccess.UserManager.AssociationHandles_Table.AssociationHandles_Inserter inserter)
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
			
			    if (inserter.AssociationHandle_Changed)
			    {
			        columnNames.Add("AssociationHandle");
			        arguments.Add("@AssociationHandle");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@AssociationHandle";
			        parm.Value = inserter._AssociationHandle;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.Timestamp_Changed)
			    {
			        columnNames.Add("Timestamp");
			        arguments.Add("@Timestamp");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Timestamp";
			        parm.Value = inserter._Timestamp.Ticks;
			        command.Parameters.Add(parm);
			    }
			
				string commandString = string.Format("insert into AssociationHandles ({0}) values ({1})",
			        StringGenerator.GenerateCommaSeperatedList(columnNames),
			        StringGenerator.GenerateCommaSeperatedList(arguments));
			
			    command.CommandText = commandString;
			
			    command.ExecuteNonQuery();
			};
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			}
		}
		
		protected override TKey DoInsertAndReturnPrimaryKey<TKey>(ObjectCloud.DataAccess.UserManager.AssociationHandles_Table.AssociationHandles_Inserter inserter)
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
			
			    if (inserter.AssociationHandle_Changed)
			    {
			        columnNames.Add("AssociationHandle");
			        arguments.Add("@AssociationHandle");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@AssociationHandle";
			        parm.Value = inserter._AssociationHandle;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.Timestamp_Changed)
			    {
			        columnNames.Add("Timestamp");
			        arguments.Add("@Timestamp");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Timestamp";
			        parm.Value = inserter._Timestamp.Ticks;
			        command.Parameters.Add(parm);
			    }
			
				string commandString = string.Format("insert into AssociationHandles ({0}) values ({1});select last_insert_rowid() AS RecordID;",
			        StringGenerator.GenerateCommaSeperatedList(columnNames),
			        StringGenerator.GenerateCommaSeperatedList(arguments));
			
			    command.CommandText = commandString;
			
			    toReturn = command.ExecuteScalar();
			};
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			    return (TKey) toReturn;
			}
		}
			
			
		public override IEnumerable<IAssociationHandles_Readable> Select(ObjectCloud.ORM.DataAccess.WhereConditionals.ComparisonCondition condition, uint? max, OrderBy sortOrder, params ObjectCloud.ORM.DataAccess.Column[] orderBy)
		{
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    StringBuilder commandBuilder = new StringBuilder("select UserID, AssociationHandle, Timestamp from AssociationHandles");
		
		    using (DbCommand command = Connection.CreateCommand())
		    {
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(AssociationHandles_Table) != ((Column)entity).Table)
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
			
			            AssociationHandles_Readable toYield = new AssociationHandles_Readable();
			
			            if (System.DBNull.Value != values[0])
			              toYield._UserID = new ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUser, System.Guid>(((System.Guid)values[0]));
			            else
			              toYield._UserID = default(ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUser, System.Guid>);

			            if (System.DBNull.Value != values[1])
			              toYield._AssociationHandle = ((System.String)values[1]);
			            else
			              toYield._AssociationHandle = default(System.String);

			            if (System.DBNull.Value != values[2])
			              toYield._Timestamp = new DateTime(((System.Int64)values[2]));
			            else
			              toYield._Timestamp = default(System.DateTime);

			
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
		    StringBuilder commandBuilder = new StringBuilder("delete from AssociationHandles");
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(AssociationHandles_Table) != ((Column)entity).Table)
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
		
		protected override int DoUpdate(ComparisonCondition condition, AssociationHandles_Inserter inserter)
		{
			int rowsAffected;
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    StringBuilder commandBuilder = new StringBuilder("update AssociationHandles");
		
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
			
			    if (inserter.AssociationHandle_Changed)
			    {
			        setStatements.Add("AssociationHandle = @AssociationHandle");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@AssociationHandle";
			        parm.Value = inserter._AssociationHandle;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.Timestamp_Changed)
			    {
			        setStatements.Add("Timestamp = @Timestamp");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Timestamp";
			        parm.Value = inserter._Timestamp.Ticks;
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
			                if (typeof(AssociationHandles_Table) != ((Column)entity).Table)
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