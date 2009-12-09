using ObjectCloud.Common;
using ObjectCloud.DataAccess.Directory;
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

namespace ObjectCloud.DataAccess.SQLite.Directory
{
    public partial class EmbeddedDatabaseCreator : ObjectCloud.DataAccess.Directory.IEmbeddedDatabaseCreator
    {
        /// <summary>
        /// Schema creation sql
        /// </summary>
        const string schemaSql =
@"create table File 
(
	Name			string not null unique,
	Extension			string not null,
	TypeId			string not null,
	OwnerId			guid,
	Created			integer not null,
	FileId			integer	primary key
);Create index File_Name on File (Name);
Create index File_Extension on File (Extension);
create table Permission 
(
	FileId			integer references File(FileId),
	UserOrGroupId			guid not null,
	Level			integer not null,
	Inherit			boolean not null,
	SendNotifications			boolean not null
);create table Metadata 
(
	Value			string not null,
	Name			string not null	primary key
);Create index Metadata_Name on Metadata (Name);
create table Relationships 
(
	FileId			integer references File(FileId),
	ReferencedFileId			integer,
	Relationship			string not null
);Create index Relationships_ReferencedFileId on Relationships (ReferencedFileId);
Create index Relationships_Relationship on Relationships (Relationship);
Create  index Relationships_FileId_Relationship on Relationships (FileId, Relationship);
Create  index Relationships_ReferencedFileId_Relationship on Relationships (ReferencedFileId, Relationship);
Create unique index Relationships_FileId_ReferencedFileId_Relationship on Relationships (FileId, ReferencedFileId, Relationship);
create table NamedPermission 
(
	FileId			integer references File(FileId),
	NamedPermission			string not null,
	UserOrGroup			guid not null,
	Inherit			boolean not null
);Create index NamedPermission_FileId on NamedPermission (FileId);
Create unique index NamedPermission_FileId_NamedPermission_UserOrGroup on NamedPermission (FileId, NamedPermission, UserOrGroup);

PRAGMA user_version = 5;
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

	public class DatabaseConnectorFactory : ObjectCloud.DataAccess.Directory.IDatabaseConnectorFactory
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
		
	partial class DatabaseTransaction : ObjectCloud.DataAccess.Directory.IDatabaseTransaction
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
			
			_File_Table = new File_Table(sqlConnection, EmbeddedDatabaseConnector, databaseConnector);
			_Permission_Table = new Permission_Table(sqlConnection, EmbeddedDatabaseConnector, databaseConnector);
			_Metadata_Table = new Metadata_Table(sqlConnection, EmbeddedDatabaseConnector, databaseConnector);
			_Relationships_Table = new Relationships_Table(sqlConnection, EmbeddedDatabaseConnector, databaseConnector);
			_NamedPermission_Table = new NamedPermission_Table(sqlConnection, EmbeddedDatabaseConnector, databaseConnector);
		}
		
		public void Dispose()
		{
			using (TimedLock.Lock(sqlConnection)){
				sqlConnection.Close();
				sqlConnection.Dispose();
			}
		}
		
		public T CallOnTransaction<T>(GenericArgumentReturn<ObjectCloud.DataAccess.Directory.IDatabaseTransaction, T> del)
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
		
		public void CallOnTransaction(GenericArgument<ObjectCloud.DataAccess.Directory.IDatabaseTransaction> del)
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
		
		public ObjectCloud.DataAccess.Directory.File_Table File
		{
			get { return _File_Table; }
		}
		private File_Table _File_Table;
		public ObjectCloud.DataAccess.Directory.Permission_Table Permission
		{
			get { return _Permission_Table; }
		}
		private Permission_Table _Permission_Table;
		public ObjectCloud.DataAccess.Directory.Metadata_Table Metadata
		{
			get { return _Metadata_Table; }
		}
		private Metadata_Table _Metadata_Table;
		public ObjectCloud.DataAccess.Directory.Relationships_Table Relationships
		{
			get { return _Relationships_Table; }
		}
		private Relationships_Table _Relationships_Table;
		public ObjectCloud.DataAccess.Directory.NamedPermission_Table NamedPermission
		{
			get { return _NamedPermission_Table; }
		}
		private NamedPermission_Table _NamedPermission_Table;
		
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
		
	internal class File_Readable : IFile_Readable
	{
		public System.String Name
		{
			get { return _Name; }
		}	
		internal System.String _Name = default(System.String);
		
		public System.String Extension
		{
			get { return _Extension; }
		}	
		internal System.String _Extension = default(System.String);
		
		public System.String TypeId
		{
			get { return _TypeId; }
		}	
		internal System.String _TypeId = default(System.String);
		
		public System.Nullable<ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>> OwnerId
		{
			get { return _OwnerId; }
		}	
		internal System.Nullable<ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>> _OwnerId = default(System.Nullable<ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>>);
		
		public System.DateTime Created
		{
			get { return _Created; }
		}	
		internal System.DateTime _Created = default(System.DateTime);
		
		public ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64> FileId
		{
			get { return _FileId; }
		}	
		internal ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64> _FileId = default(ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64>);
		
	}
	
	public partial class File_Table : ObjectCloud.DataAccess.Directory.File_Table
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

		static File_Table()
		{
			ObjectCloud.DataAccess.Directory.File_Table._Name = ObjectCloud.ORM.DataAccess.Column.Construct<File_Table, IFile_Writable, IFile_Readable>("Name");
			ObjectCloud.DataAccess.Directory.File_Table._Extension = ObjectCloud.ORM.DataAccess.Column.Construct<File_Table, IFile_Writable, IFile_Readable>("Extension");
			ObjectCloud.DataAccess.Directory.File_Table._TypeId = ObjectCloud.ORM.DataAccess.Column.Construct<File_Table, IFile_Writable, IFile_Readable>("TypeId");
			ObjectCloud.DataAccess.Directory.File_Table._OwnerId = ObjectCloud.ORM.DataAccess.Column.Construct<File_Table, IFile_Writable, IFile_Readable>("OwnerId");
			ObjectCloud.DataAccess.Directory.File_Table._Created = ObjectCloud.ORM.DataAccess.Column.Construct<File_Table, IFile_Writable, IFile_Readable>("Created");
			ObjectCloud.DataAccess.Directory.File_Table._FileId = ObjectCloud.ORM.DataAccess.Column.Construct<File_Table, IFile_Writable, IFile_Readable>("FileId");
		}
		
		internal DbConnection Connection;
		internal DatabaseConnector DatabaseConnector;
	
		
		internal File_Table(DbConnection connection, IEmbeddedDatabaseConnector embeddedDatabaseConnector, DatabaseConnector databaseConnector)
		{
		    Connection = connection;
			EmbeddedDatabaseConnector = embeddedDatabaseConnector;
		    DatabaseConnector = databaseConnector;
		}
		
		protected override void DoInsert(ObjectCloud.DataAccess.Directory.File_Table.File_Inserter inserter)
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
			
			    if (inserter.Extension_Changed)
			    {
			        columnNames.Add("Extension");
			        arguments.Add("@Extension");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Extension";
			        parm.Value = inserter._Extension;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.TypeId_Changed)
			    {
			        columnNames.Add("TypeId");
			        arguments.Add("@TypeId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@TypeId";
			        parm.Value = inserter._TypeId;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.OwnerId_Changed)
			    {
			        columnNames.Add("OwnerId");
			        arguments.Add("@OwnerId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@OwnerId";
			        parm.Value = ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>.GetValueOrNull(inserter._OwnerId);
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
			
			    if (inserter.FileId_Changed)
			    {
			        columnNames.Add("FileId");
			        arguments.Add("@FileId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@FileId";
			        parm.Value = inserter._FileId.Value;
			        command.Parameters.Add(parm);
			    }
			
				string commandString = string.Format("insert into File ({0}) values ({1})",
			        StringGenerator.GenerateCommaSeperatedList(columnNames),
			        StringGenerator.GenerateCommaSeperatedList(arguments));
			
			    command.CommandText = commandString;
			
			    command.ExecuteNonQuery();
			};
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			}
		}
		
		protected override TKey DoInsertAndReturnPrimaryKey<TKey>(ObjectCloud.DataAccess.Directory.File_Table.File_Inserter inserter)
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
			
			    if (inserter.Extension_Changed)
			    {
			        columnNames.Add("Extension");
			        arguments.Add("@Extension");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Extension";
			        parm.Value = inserter._Extension;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.TypeId_Changed)
			    {
			        columnNames.Add("TypeId");
			        arguments.Add("@TypeId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@TypeId";
			        parm.Value = inserter._TypeId;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.OwnerId_Changed)
			    {
			        columnNames.Add("OwnerId");
			        arguments.Add("@OwnerId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@OwnerId";
			        parm.Value = ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>.GetValueOrNull(inserter._OwnerId);
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
			
			    if (inserter.FileId_Changed)
			    {
			        columnNames.Add("FileId");
			        arguments.Add("@FileId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@FileId";
			        parm.Value = inserter._FileId.Value;
			        command.Parameters.Add(parm);
			    }
			
				string commandString = string.Format("insert into File ({0}) values ({1});select last_insert_rowid() AS RecordID;",
			        StringGenerator.GenerateCommaSeperatedList(columnNames),
			        StringGenerator.GenerateCommaSeperatedList(arguments));
			
			    command.CommandText = commandString;
			
			    toReturn = command.ExecuteScalar();
			};
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			    return (TKey) toReturn;
			}
		}
			
			
		public override IEnumerable<IFile_Readable> Select(ObjectCloud.ORM.DataAccess.WhereConditionals.ComparisonCondition condition, uint? max, OrderBy sortOrder, params ObjectCloud.ORM.DataAccess.Column[] orderBy)
		{
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    StringBuilder commandBuilder = new StringBuilder("select Name, Extension, TypeId, OwnerId, Created, FileId from File");
		
		    using (DbCommand command = Connection.CreateCommand())
		    {
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(File_Table) != ((Column)entity).Table)
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
			            object[] values = new object[6];
			            dataReader.GetValues(values);
			
			            File_Readable toYield = new File_Readable();
			
			            if (System.DBNull.Value != values[0])
			              toYield._Name = ((System.String)values[0]);
			            else
			              toYield._Name = default(System.String);

			            if (System.DBNull.Value != values[1])
			              toYield._Extension = ((System.String)values[1]);
			            else
			              toYield._Extension = default(System.String);

			            if (System.DBNull.Value != values[2])
			              toYield._TypeId = ((System.String)values[2]);
			            else
			              toYield._TypeId = default(System.String);

			            if (System.DBNull.Value != values[3])
			              toYield._OwnerId = new ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>(((System.Guid)values[3]));
			            else
			              toYield._OwnerId = default(System.Nullable<ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>>);

			            if (System.DBNull.Value != values[4])
			              toYield._Created = new DateTime(((System.Int64)values[4]));
			            else
			              toYield._Created = default(System.DateTime);

			            if (System.DBNull.Value != values[5])
			              toYield._FileId = new ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64>(((System.Int64)values[5]));
			            else
			              toYield._FileId = default(ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64>);

			
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
		    StringBuilder commandBuilder = new StringBuilder("delete from File");
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(File_Table) != ((Column)entity).Table)
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
		
		protected override int DoUpdate(ComparisonCondition condition, File_Inserter inserter)
		{
			int rowsAffected;
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    StringBuilder commandBuilder = new StringBuilder("update File");
		
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
			
			    if (inserter.Extension_Changed)
			    {
			        setStatements.Add("Extension = @Extension");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Extension";
			        parm.Value = inserter._Extension;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.TypeId_Changed)
			    {
			        setStatements.Add("TypeId = @TypeId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@TypeId";
			        parm.Value = inserter._TypeId;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.OwnerId_Changed)
			    {
			        setStatements.Add("OwnerId = @OwnerId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@OwnerId";
			        parm.Value = ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>.GetValueOrNull(inserter._OwnerId);
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
			
			    if (inserter.FileId_Changed)
			    {
			        setStatements.Add("FileId = @FileId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@FileId";
			        parm.Value = inserter._FileId.Value;
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
			                if (typeof(File_Table) != ((Column)entity).Table)
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
	internal class Permission_Readable : IPermission_Readable
	{
		public ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64> FileId
		{
			get { return _FileId; }
		}	
		internal ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64> _FileId = default(ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64>);
		
		public ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid> UserOrGroupId
		{
			get { return _UserOrGroupId; }
		}	
		internal ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid> _UserOrGroupId = default(ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>);
		
		public ObjectCloud.Interfaces.Security.FilePermissionEnum Level
		{
			get { return _Level; }
		}	
		internal ObjectCloud.Interfaces.Security.FilePermissionEnum _Level = default(ObjectCloud.Interfaces.Security.FilePermissionEnum);
		
		public System.Boolean Inherit
		{
			get { return _Inherit; }
		}	
		internal System.Boolean _Inherit = default(System.Boolean);
		
		public System.Boolean SendNotifications
		{
			get { return _SendNotifications; }
		}	
		internal System.Boolean _SendNotifications = default(System.Boolean);
		
	}
	
	public partial class Permission_Table : ObjectCloud.DataAccess.Directory.Permission_Table
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

		static Permission_Table()
		{
			ObjectCloud.DataAccess.Directory.Permission_Table._FileId = ObjectCloud.ORM.DataAccess.Column.Construct<Permission_Table, IPermission_Writable, IPermission_Readable>("FileId");
			ObjectCloud.DataAccess.Directory.Permission_Table._UserOrGroupId = ObjectCloud.ORM.DataAccess.Column.Construct<Permission_Table, IPermission_Writable, IPermission_Readable>("UserOrGroupId");
			ObjectCloud.DataAccess.Directory.Permission_Table._Level = ObjectCloud.ORM.DataAccess.Column.Construct<Permission_Table, IPermission_Writable, IPermission_Readable>("Level");
			ObjectCloud.DataAccess.Directory.Permission_Table._Inherit = ObjectCloud.ORM.DataAccess.Column.Construct<Permission_Table, IPermission_Writable, IPermission_Readable>("Inherit");
			ObjectCloud.DataAccess.Directory.Permission_Table._SendNotifications = ObjectCloud.ORM.DataAccess.Column.Construct<Permission_Table, IPermission_Writable, IPermission_Readable>("SendNotifications");
		}
		
		internal DbConnection Connection;
		internal DatabaseConnector DatabaseConnector;
	
		
		internal Permission_Table(DbConnection connection, IEmbeddedDatabaseConnector embeddedDatabaseConnector, DatabaseConnector databaseConnector)
		{
		    Connection = connection;
			EmbeddedDatabaseConnector = embeddedDatabaseConnector;
		    DatabaseConnector = databaseConnector;
		}
		
		protected override void DoInsert(ObjectCloud.DataAccess.Directory.Permission_Table.Permission_Inserter inserter)
		{
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    List<string> columnNames = new List<string>();
		    List<string> arguments = new List<string>();
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    if (inserter.FileId_Changed)
			    {
			        columnNames.Add("FileId");
			        arguments.Add("@FileId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@FileId";
			        parm.Value = inserter._FileId.Value;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.UserOrGroupId_Changed)
			    {
			        columnNames.Add("UserOrGroupId");
			        arguments.Add("@UserOrGroupId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@UserOrGroupId";
			        parm.Value = inserter._UserOrGroupId.Value;
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
			
			    if (inserter.Inherit_Changed)
			    {
			        columnNames.Add("Inherit");
			        arguments.Add("@Inherit");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Inherit";
			        parm.Value = inserter._Inherit ? 1 : 0;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.SendNotifications_Changed)
			    {
			        columnNames.Add("SendNotifications");
			        arguments.Add("@SendNotifications");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@SendNotifications";
			        parm.Value = inserter._SendNotifications ? 1 : 0;
			        command.Parameters.Add(parm);
			    }
			
				string commandString = string.Format("insert into Permission ({0}) values ({1})",
			        StringGenerator.GenerateCommaSeperatedList(columnNames),
			        StringGenerator.GenerateCommaSeperatedList(arguments));
			
			    command.CommandText = commandString;
			
			    command.ExecuteNonQuery();
			};
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			}
		}
		
		protected override TKey DoInsertAndReturnPrimaryKey<TKey>(ObjectCloud.DataAccess.Directory.Permission_Table.Permission_Inserter inserter)
		{
			object toReturn;
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    List<string> columnNames = new List<string>();
		    List<string> arguments = new List<string>();
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    if (inserter.FileId_Changed)
			    {
			        columnNames.Add("FileId");
			        arguments.Add("@FileId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@FileId";
			        parm.Value = inserter._FileId.Value;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.UserOrGroupId_Changed)
			    {
			        columnNames.Add("UserOrGroupId");
			        arguments.Add("@UserOrGroupId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@UserOrGroupId";
			        parm.Value = inserter._UserOrGroupId.Value;
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
			
			    if (inserter.Inherit_Changed)
			    {
			        columnNames.Add("Inherit");
			        arguments.Add("@Inherit");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Inherit";
			        parm.Value = inserter._Inherit ? 1 : 0;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.SendNotifications_Changed)
			    {
			        columnNames.Add("SendNotifications");
			        arguments.Add("@SendNotifications");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@SendNotifications";
			        parm.Value = inserter._SendNotifications ? 1 : 0;
			        command.Parameters.Add(parm);
			    }
			
				string commandString = string.Format("insert into Permission ({0}) values ({1});select last_insert_rowid() AS RecordID;",
			        StringGenerator.GenerateCommaSeperatedList(columnNames),
			        StringGenerator.GenerateCommaSeperatedList(arguments));
			
			    command.CommandText = commandString;
			
			    toReturn = command.ExecuteScalar();
			};
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			    return (TKey) toReturn;
			}
		}
			
			
		public override IEnumerable<IPermission_Readable> Select(ObjectCloud.ORM.DataAccess.WhereConditionals.ComparisonCondition condition, uint? max, OrderBy sortOrder, params ObjectCloud.ORM.DataAccess.Column[] orderBy)
		{
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    StringBuilder commandBuilder = new StringBuilder("select FileId, UserOrGroupId, Level, Inherit, SendNotifications from Permission");
		
		    using (DbCommand command = Connection.CreateCommand())
		    {
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(Permission_Table) != ((Column)entity).Table)
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
			
			            Permission_Readable toYield = new Permission_Readable();
			
			            if (System.DBNull.Value != values[0])
			              toYield._FileId = new ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64>(((System.Int64)values[0]));
			            else
			              toYield._FileId = default(ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64>);

			            if (System.DBNull.Value != values[1])
			              toYield._UserOrGroupId = new ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>(((System.Guid)values[1]));
			            else
			              toYield._UserOrGroupId = default(ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>);

			            if (System.DBNull.Value != values[2])
			              toYield._Level = ((ObjectCloud.Interfaces.Security.FilePermissionEnum)Convert.ToInt32(values[2]));
			            else
			              toYield._Level = default(ObjectCloud.Interfaces.Security.FilePermissionEnum);

			            if (System.DBNull.Value != values[3])
			              toYield._Inherit = 1 == Convert.ToInt32(values[3]);
			            else
			              toYield._Inherit = default(System.Boolean);

			            if (System.DBNull.Value != values[4])
			              toYield._SendNotifications = 1 == Convert.ToInt32(values[4]);
			            else
			              toYield._SendNotifications = default(System.Boolean);

			
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
		    StringBuilder commandBuilder = new StringBuilder("delete from Permission");
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(Permission_Table) != ((Column)entity).Table)
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
		
		protected override int DoUpdate(ComparisonCondition condition, Permission_Inserter inserter)
		{
			int rowsAffected;
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    StringBuilder commandBuilder = new StringBuilder("update Permission");
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    List<string> setStatements = new List<string>();
			
			    if (inserter.FileId_Changed)
			    {
			        setStatements.Add("FileId = @FileId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@FileId";
			        parm.Value = inserter._FileId.Value;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.UserOrGroupId_Changed)
			    {
			        setStatements.Add("UserOrGroupId = @UserOrGroupId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@UserOrGroupId";
			        parm.Value = inserter._UserOrGroupId.Value;
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
			
			    if (inserter.Inherit_Changed)
			    {
			        setStatements.Add("Inherit = @Inherit");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Inherit";
			        parm.Value = inserter._Inherit ? 1 : 0;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.SendNotifications_Changed)
			    {
			        setStatements.Add("SendNotifications = @SendNotifications");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@SendNotifications";
			        parm.Value = inserter._SendNotifications ? 1 : 0;
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
			                if (typeof(Permission_Table) != ((Column)entity).Table)
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
	internal class Metadata_Readable : IMetadata_Readable
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
	
	public partial class Metadata_Table : ObjectCloud.DataAccess.Directory.Metadata_Table
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

		static Metadata_Table()
		{
			ObjectCloud.DataAccess.Directory.Metadata_Table._Value = ObjectCloud.ORM.DataAccess.Column.Construct<Metadata_Table, IMetadata_Writable, IMetadata_Readable>("Value");
			ObjectCloud.DataAccess.Directory.Metadata_Table._Name = ObjectCloud.ORM.DataAccess.Column.Construct<Metadata_Table, IMetadata_Writable, IMetadata_Readable>("Name");
		}
		
		internal DbConnection Connection;
		internal DatabaseConnector DatabaseConnector;
	
		
		internal Metadata_Table(DbConnection connection, IEmbeddedDatabaseConnector embeddedDatabaseConnector, DatabaseConnector databaseConnector)
		{
		    Connection = connection;
			EmbeddedDatabaseConnector = embeddedDatabaseConnector;
		    DatabaseConnector = databaseConnector;
		}
		
		protected override void DoInsert(ObjectCloud.DataAccess.Directory.Metadata_Table.Metadata_Inserter inserter)
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
			
				string commandString = string.Format("insert into Metadata ({0}) values ({1})",
			        StringGenerator.GenerateCommaSeperatedList(columnNames),
			        StringGenerator.GenerateCommaSeperatedList(arguments));
			
			    command.CommandText = commandString;
			
			    command.ExecuteNonQuery();
			};
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			}
		}
		
		protected override TKey DoInsertAndReturnPrimaryKey<TKey>(ObjectCloud.DataAccess.Directory.Metadata_Table.Metadata_Inserter inserter)
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
			
				string commandString = string.Format("insert into Metadata ({0}) values ({1});select last_insert_rowid() AS RecordID;",
			        StringGenerator.GenerateCommaSeperatedList(columnNames),
			        StringGenerator.GenerateCommaSeperatedList(arguments));
			
			    command.CommandText = commandString;
			
			    toReturn = command.ExecuteScalar();
			};
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			    return (TKey) toReturn;
			}
		}
			
			
		public override IEnumerable<IMetadata_Readable> Select(ObjectCloud.ORM.DataAccess.WhereConditionals.ComparisonCondition condition, uint? max, OrderBy sortOrder, params ObjectCloud.ORM.DataAccess.Column[] orderBy)
		{
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    StringBuilder commandBuilder = new StringBuilder("select Value, Name from Metadata");
		
		    using (DbCommand command = Connection.CreateCommand())
		    {
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(Metadata_Table) != ((Column)entity).Table)
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
			
			            Metadata_Readable toYield = new Metadata_Readable();
			
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
		    StringBuilder commandBuilder = new StringBuilder("delete from Metadata");
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(Metadata_Table) != ((Column)entity).Table)
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
		
		protected override int DoUpdate(ComparisonCondition condition, Metadata_Inserter inserter)
		{
			int rowsAffected;
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    StringBuilder commandBuilder = new StringBuilder("update Metadata");
		
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
			                if (typeof(Metadata_Table) != ((Column)entity).Table)
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
	internal class Relationships_Readable : IRelationships_Readable
	{
		public ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64> FileId
		{
			get { return _FileId; }
		}	
		internal ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64> _FileId = default(ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64>);
		
		public ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64> ReferencedFileId
		{
			get { return _ReferencedFileId; }
		}	
		internal ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64> _ReferencedFileId = default(ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64>);
		
		public System.String Relationship
		{
			get { return _Relationship; }
		}	
		internal System.String _Relationship = default(System.String);
		
	}
	
	public partial class Relationships_Table : ObjectCloud.DataAccess.Directory.Relationships_Table
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

		static Relationships_Table()
		{
			ObjectCloud.DataAccess.Directory.Relationships_Table._FileId = ObjectCloud.ORM.DataAccess.Column.Construct<Relationships_Table, IRelationships_Writable, IRelationships_Readable>("FileId");
			ObjectCloud.DataAccess.Directory.Relationships_Table._ReferencedFileId = ObjectCloud.ORM.DataAccess.Column.Construct<Relationships_Table, IRelationships_Writable, IRelationships_Readable>("ReferencedFileId");
			ObjectCloud.DataAccess.Directory.Relationships_Table._Relationship = ObjectCloud.ORM.DataAccess.Column.Construct<Relationships_Table, IRelationships_Writable, IRelationships_Readable>("Relationship");
		}
		
		internal DbConnection Connection;
		internal DatabaseConnector DatabaseConnector;
	
		
		internal Relationships_Table(DbConnection connection, IEmbeddedDatabaseConnector embeddedDatabaseConnector, DatabaseConnector databaseConnector)
		{
		    Connection = connection;
			EmbeddedDatabaseConnector = embeddedDatabaseConnector;
		    DatabaseConnector = databaseConnector;
		}
		
		protected override void DoInsert(ObjectCloud.DataAccess.Directory.Relationships_Table.Relationships_Inserter inserter)
		{
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    List<string> columnNames = new List<string>();
		    List<string> arguments = new List<string>();
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    if (inserter.FileId_Changed)
			    {
			        columnNames.Add("FileId");
			        arguments.Add("@FileId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@FileId";
			        parm.Value = inserter._FileId.Value;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.ReferencedFileId_Changed)
			    {
			        columnNames.Add("ReferencedFileId");
			        arguments.Add("@ReferencedFileId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@ReferencedFileId";
			        parm.Value = inserter._ReferencedFileId.Value;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.Relationship_Changed)
			    {
			        columnNames.Add("Relationship");
			        arguments.Add("@Relationship");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Relationship";
			        parm.Value = inserter._Relationship;
			        command.Parameters.Add(parm);
			    }
			
				string commandString = string.Format("insert into Relationships ({0}) values ({1})",
			        StringGenerator.GenerateCommaSeperatedList(columnNames),
			        StringGenerator.GenerateCommaSeperatedList(arguments));
			
			    command.CommandText = commandString;
			
			    command.ExecuteNonQuery();
			};
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			}
		}
		
		protected override TKey DoInsertAndReturnPrimaryKey<TKey>(ObjectCloud.DataAccess.Directory.Relationships_Table.Relationships_Inserter inserter)
		{
			object toReturn;
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    List<string> columnNames = new List<string>();
		    List<string> arguments = new List<string>();
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    if (inserter.FileId_Changed)
			    {
			        columnNames.Add("FileId");
			        arguments.Add("@FileId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@FileId";
			        parm.Value = inserter._FileId.Value;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.ReferencedFileId_Changed)
			    {
			        columnNames.Add("ReferencedFileId");
			        arguments.Add("@ReferencedFileId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@ReferencedFileId";
			        parm.Value = inserter._ReferencedFileId.Value;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.Relationship_Changed)
			    {
			        columnNames.Add("Relationship");
			        arguments.Add("@Relationship");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Relationship";
			        parm.Value = inserter._Relationship;
			        command.Parameters.Add(parm);
			    }
			
				string commandString = string.Format("insert into Relationships ({0}) values ({1});select last_insert_rowid() AS RecordID;",
			        StringGenerator.GenerateCommaSeperatedList(columnNames),
			        StringGenerator.GenerateCommaSeperatedList(arguments));
			
			    command.CommandText = commandString;
			
			    toReturn = command.ExecuteScalar();
			};
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			    return (TKey) toReturn;
			}
		}
			
			
		public override IEnumerable<IRelationships_Readable> Select(ObjectCloud.ORM.DataAccess.WhereConditionals.ComparisonCondition condition, uint? max, OrderBy sortOrder, params ObjectCloud.ORM.DataAccess.Column[] orderBy)
		{
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    StringBuilder commandBuilder = new StringBuilder("select FileId, ReferencedFileId, Relationship from Relationships");
		
		    using (DbCommand command = Connection.CreateCommand())
		    {
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(Relationships_Table) != ((Column)entity).Table)
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
			
			            Relationships_Readable toYield = new Relationships_Readable();
			
			            if (System.DBNull.Value != values[0])
			              toYield._FileId = new ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64>(((System.Int64)values[0]));
			            else
			              toYield._FileId = default(ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64>);

			            if (System.DBNull.Value != values[1])
			              toYield._ReferencedFileId = new ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64>(((System.Int64)values[1]));
			            else
			              toYield._ReferencedFileId = default(ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64>);

			            if (System.DBNull.Value != values[2])
			              toYield._Relationship = ((System.String)values[2]);
			            else
			              toYield._Relationship = default(System.String);

			
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
		    StringBuilder commandBuilder = new StringBuilder("delete from Relationships");
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(Relationships_Table) != ((Column)entity).Table)
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
		
		protected override int DoUpdate(ComparisonCondition condition, Relationships_Inserter inserter)
		{
			int rowsAffected;
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    StringBuilder commandBuilder = new StringBuilder("update Relationships");
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    List<string> setStatements = new List<string>();
			
			    if (inserter.FileId_Changed)
			    {
			        setStatements.Add("FileId = @FileId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@FileId";
			        parm.Value = inserter._FileId.Value;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.ReferencedFileId_Changed)
			    {
			        setStatements.Add("ReferencedFileId = @ReferencedFileId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@ReferencedFileId";
			        parm.Value = inserter._ReferencedFileId.Value;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.Relationship_Changed)
			    {
			        setStatements.Add("Relationship = @Relationship");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Relationship";
			        parm.Value = inserter._Relationship;
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
			                if (typeof(Relationships_Table) != ((Column)entity).Table)
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
	internal class NamedPermission_Readable : INamedPermission_Readable
	{
		public ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64> FileId
		{
			get { return _FileId; }
		}	
		internal ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64> _FileId = default(ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64>);
		
		public System.String NamedPermission
		{
			get { return _NamedPermission; }
		}	
		internal System.String _NamedPermission = default(System.String);
		
		public ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid> UserOrGroup
		{
			get { return _UserOrGroup; }
		}	
		internal ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid> _UserOrGroup = default(ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>);
		
		public System.Boolean Inherit
		{
			get { return _Inherit; }
		}	
		internal System.Boolean _Inherit = default(System.Boolean);
		
	}
	
	public partial class NamedPermission_Table : ObjectCloud.DataAccess.Directory.NamedPermission_Table
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

		static NamedPermission_Table()
		{
			ObjectCloud.DataAccess.Directory.NamedPermission_Table._FileId = ObjectCloud.ORM.DataAccess.Column.Construct<NamedPermission_Table, INamedPermission_Writable, INamedPermission_Readable>("FileId");
			ObjectCloud.DataAccess.Directory.NamedPermission_Table._NamedPermission = ObjectCloud.ORM.DataAccess.Column.Construct<NamedPermission_Table, INamedPermission_Writable, INamedPermission_Readable>("NamedPermission");
			ObjectCloud.DataAccess.Directory.NamedPermission_Table._UserOrGroup = ObjectCloud.ORM.DataAccess.Column.Construct<NamedPermission_Table, INamedPermission_Writable, INamedPermission_Readable>("UserOrGroup");
			ObjectCloud.DataAccess.Directory.NamedPermission_Table._Inherit = ObjectCloud.ORM.DataAccess.Column.Construct<NamedPermission_Table, INamedPermission_Writable, INamedPermission_Readable>("Inherit");
		}
		
		internal DbConnection Connection;
		internal DatabaseConnector DatabaseConnector;
	
		
		internal NamedPermission_Table(DbConnection connection, IEmbeddedDatabaseConnector embeddedDatabaseConnector, DatabaseConnector databaseConnector)
		{
		    Connection = connection;
			EmbeddedDatabaseConnector = embeddedDatabaseConnector;
		    DatabaseConnector = databaseConnector;
		}
		
		protected override void DoInsert(ObjectCloud.DataAccess.Directory.NamedPermission_Table.NamedPermission_Inserter inserter)
		{
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    List<string> columnNames = new List<string>();
		    List<string> arguments = new List<string>();
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    if (inserter.FileId_Changed)
			    {
			        columnNames.Add("FileId");
			        arguments.Add("@FileId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@FileId";
			        parm.Value = inserter._FileId.Value;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.NamedPermission_Changed)
			    {
			        columnNames.Add("NamedPermission");
			        arguments.Add("@NamedPermission");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@NamedPermission";
			        parm.Value = inserter._NamedPermission;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.UserOrGroup_Changed)
			    {
			        columnNames.Add("UserOrGroup");
			        arguments.Add("@UserOrGroup");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@UserOrGroup";
			        parm.Value = inserter._UserOrGroup.Value;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.Inherit_Changed)
			    {
			        columnNames.Add("Inherit");
			        arguments.Add("@Inherit");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Inherit";
			        parm.Value = inserter._Inherit ? 1 : 0;
			        command.Parameters.Add(parm);
			    }
			
				string commandString = string.Format("insert into NamedPermission ({0}) values ({1})",
			        StringGenerator.GenerateCommaSeperatedList(columnNames),
			        StringGenerator.GenerateCommaSeperatedList(arguments));
			
			    command.CommandText = commandString;
			
			    command.ExecuteNonQuery();
			};
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			}
		}
		
		protected override TKey DoInsertAndReturnPrimaryKey<TKey>(ObjectCloud.DataAccess.Directory.NamedPermission_Table.NamedPermission_Inserter inserter)
		{
			object toReturn;
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    List<string> columnNames = new List<string>();
		    List<string> arguments = new List<string>();
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    if (inserter.FileId_Changed)
			    {
			        columnNames.Add("FileId");
			        arguments.Add("@FileId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@FileId";
			        parm.Value = inserter._FileId.Value;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.NamedPermission_Changed)
			    {
			        columnNames.Add("NamedPermission");
			        arguments.Add("@NamedPermission");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@NamedPermission";
			        parm.Value = inserter._NamedPermission;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.UserOrGroup_Changed)
			    {
			        columnNames.Add("UserOrGroup");
			        arguments.Add("@UserOrGroup");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@UserOrGroup";
			        parm.Value = inserter._UserOrGroup.Value;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.Inherit_Changed)
			    {
			        columnNames.Add("Inherit");
			        arguments.Add("@Inherit");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Inherit";
			        parm.Value = inserter._Inherit ? 1 : 0;
			        command.Parameters.Add(parm);
			    }
			
				string commandString = string.Format("insert into NamedPermission ({0}) values ({1});select last_insert_rowid() AS RecordID;",
			        StringGenerator.GenerateCommaSeperatedList(columnNames),
			        StringGenerator.GenerateCommaSeperatedList(arguments));
			
			    command.CommandText = commandString;
			
			    toReturn = command.ExecuteScalar();
			};
			DatabaseConnector.OnDatabaseWritten(new EventArgs());
			    return (TKey) toReturn;
			}
		}
			
			
		public override IEnumerable<INamedPermission_Readable> Select(ObjectCloud.ORM.DataAccess.WhereConditionals.ComparisonCondition condition, uint? max, OrderBy sortOrder, params ObjectCloud.ORM.DataAccess.Column[] orderBy)
		{
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    StringBuilder commandBuilder = new StringBuilder("select FileId, NamedPermission, UserOrGroup, Inherit from NamedPermission");
		
		    using (DbCommand command = Connection.CreateCommand())
		    {
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(NamedPermission_Table) != ((Column)entity).Table)
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
			
			            NamedPermission_Readable toYield = new NamedPermission_Readable();
			
			            if (System.DBNull.Value != values[0])
			              toYield._FileId = new ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64>(((System.Int64)values[0]));
			            else
			              toYield._FileId = default(ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64>);

			            if (System.DBNull.Value != values[1])
			              toYield._NamedPermission = ((System.String)values[1]);
			            else
			              toYield._NamedPermission = default(System.String);

			            if (System.DBNull.Value != values[2])
			              toYield._UserOrGroup = new ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>(((System.Guid)values[2]));
			            else
			              toYield._UserOrGroup = default(ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>);

			            if (System.DBNull.Value != values[3])
			              toYield._Inherit = 1 == Convert.ToInt32(values[3]);
			            else
			              toYield._Inherit = default(System.Boolean);

			
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
		    StringBuilder commandBuilder = new StringBuilder("delete from NamedPermission");
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(NamedPermission_Table) != ((Column)entity).Table)
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
		
		protected override int DoUpdate(ComparisonCondition condition, NamedPermission_Inserter inserter)
		{
			int rowsAffected;
			using (TimedLock.Lock(Connection, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), delegate(Thread thread) { Connection.Close(); }))
		{
		    StringBuilder commandBuilder = new StringBuilder("update NamedPermission");
		
		    using (DbCommand command = Connection.CreateCommand())
			{
			    List<string> setStatements = new List<string>();
			
			    if (inserter.FileId_Changed)
			    {
			        setStatements.Add("FileId = @FileId");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@FileId";
			        parm.Value = inserter._FileId.Value;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.NamedPermission_Changed)
			    {
			        setStatements.Add("NamedPermission = @NamedPermission");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@NamedPermission";
			        parm.Value = inserter._NamedPermission;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.UserOrGroup_Changed)
			    {
			        setStatements.Add("UserOrGroup = @UserOrGroup");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@UserOrGroup";
			        parm.Value = inserter._UserOrGroup.Value;
			        command.Parameters.Add(parm);
			    }
			
			    if (inserter.Inherit_Changed)
			    {
			        setStatements.Add("Inherit = @Inherit");
			        DbParameter parm = command.CreateParameter();
			        parm.ParameterName = "@Inherit";
			        parm.Value = inserter._Inherit ? 1 : 0;
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
			                if (typeof(NamedPermission_Table) != ((Column)entity).Table)
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