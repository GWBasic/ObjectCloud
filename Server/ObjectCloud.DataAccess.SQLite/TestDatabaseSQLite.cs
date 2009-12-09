using ObjectCloud.Common;
using ObjectCloud.Interfaces.Database;
using ObjectCloud.ORM.DataAccess;
using ObjectCloud.ORM.DataAccess.SQLite;
using ObjectCloud.ORM.DataAccess.WhereConditionals;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using System.Threading;

namespace ObjectCloud.ORM.DataAccess.SQLite.Test
{
    public partial class EmbeddedDatabaseCreator : ObjectCloud.ORM.DataAccess.SQLite.Test.IEmbeddedDatabaseCreator
    {
        /// <summary>
        /// Schema creation sql
        /// </summary>
        const string schemaSql =
@"create table TestTable 
(
	Column			string not null	primary key
);";

        public void Create(string filename)
        {
            DatabaseConnectorLocator.EmbeddedDatabaseConnector.CreateFile(filename);

            DbConnection connection = DatabaseConnectorLocator.EmbeddedDatabaseConnector.OpenEmbedded(filename);
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

	public class DatabaseConnectorFactory : ObjectCloud.ORM.DataAccess.SQLite.Test.IDatabaseConnectorFactory
	{
		public IDatabaseConnector CreateConnectorForEmbedded(string path)
		{
			return CreateConnector("Data Source=\"" + path + "\"");
		}
		
		public IDatabaseConnector CreateConnector(string connectionString)
		{
			return new DatabaseConnector(connectionString);
		}
	}
		
	public class DatabaseConnector : IDatabaseConnector
	{
		private string connectionString;
		internal bool IsConnectionOpen = false;
		private object ConnectLock = new object();
		
		public DatabaseConnector(string connectionString)
		{
			this.connectionString = connectionString;
		}
		
	public IDatabaseConnection Connect()
	{
	    while (IsConnectionOpen)
	        Thread.Sleep(10);
	
	    do
	    {
	        lock (ConnectLock)
	        {
	            if (!IsConnectionOpen)
	            {
	                IsConnectionOpen = true;
	
	                DbConnection connection = DatabaseConnectorLocator.EmbeddedDatabaseConnector.Open(connectionString);
	
	                try
	                {
	                    connection.Open();
	
	                    return new DatabaseConnection(connection, this);
	                }
	                catch
	                {
	                    connection.Close();
	                    connection.Dispose();
	
	                    IsConnectionOpen = false;
	
	                    throw;
	                }
	            }
	        }
	
	        Thread.Sleep(10);
	
	    } while (true);
	}
	}
		
	class DatabaseTransaction : ObjectCloud.ORM.DataAccess.SQLite.Test.IDatabaseTransaction
	{
		internal DbConnection connection;
		internal DbTransaction transaction;
		
		internal DatabaseTransaction(DbConnection connection)
		{
			this.connection = connection;
			transaction = connection.BeginTransaction();
		
			_TestTable_Table = new TestTable_Table(transaction);
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
		
		public ObjectCloud.ORM.DataAccess.SQLite.Test.TestTable_Table TestTable
		{
			get { return _TestTable_Table; }
		}
		private TestTable_Table _TestTable_Table;
	}
		
	public class DatabaseConnection : IDatabaseConnection
	{
	    DbConnection sqlConnection;
	
	    DatabaseConnector DatabaseConnector;
	
	    public DatabaseConnection(DbConnection sqlConnection, DatabaseConnector databaseConnector)
	    {
	        this.sqlConnection = sqlConnection;
	        DatabaseConnector = databaseConnector;
	    }
	
	    public void Dispose()
	    {
	        sqlConnection.Close();
	        sqlConnection.Dispose();
	
	        DatabaseConnector.IsConnectionOpen = false;
	    }
		
		public ObjectCloud.ORM.DataAccess.SQLite.Test.IDatabaseTransaction BeginTransaction()
		{
			return new DatabaseTransaction(sqlConnection);
		}
	}
		
	internal class TestTable_Readable : ITestTable_Readable
	{
		public System.String Column
		{
			get { return _Column; }
		}	
		internal System.String _Column = default(System.String);
		
	}
	
	public class TestTable_Table : ObjectCloud.ORM.DataAccess.SQLite.Test.TestTable_Table
	{
		static TestTable_Table()
		{
			ObjectCloud.ORM.DataAccess.SQLite.Test.TestTable_Table._Column = ObjectCloud.ORM.DataAccess.Column.Construct<TestTable_Table, ITestTable_Writable, ITestTable_Readable, ITestTable>("Column");
		}
		
		internal DbTransaction transaction;
	
		
		internal TestTable_Table(DbTransaction transaction)
		{
		    this.transaction = transaction;
		}
		
		protected override void DoInsert(ObjectCloud.ORM.DataAccess.SQLite.Test.TestTable_Table.TestTable_Inserter inserter)
		{
		    List<string> columnNames = new List<string>();
		    List<string> arguments = new List<string>();
		
		    using (DbCommand command = transaction.Connection.CreateCommand())
			{
			    if (inserter.Column_Changed)
			    {
			        columnNames.Add("Column");
			        arguments.Add("@Column");
			        DbParameter parm = DatabaseConnectorLocator.EmbeddedDatabaseConnector.ConstructParameter("@Column", inserter._Column);
			        command.Parameters.Add(parm);
			    }
			
				string commandString = string.Format("insert into TestTable ({0}) values ({1})",
			        StringGenerator.GenerateCommaSeperatedList(columnNames),
			        StringGenerator.GenerateCommaSeperatedList(arguments));
			
			    command.CommandText = commandString;
			
			    command.ExecuteNonQuery();
			}
		}
			
			
		public override IEnumerable<T_Select> Select<T_Select>(ObjectCloud.ORM.DataAccess.WhereConditionals.ComparisonCondition condition)
		{
		    // Make sure this is a supported type
		    if (typeof(ITestTable_Readable) != typeof(T_Select))
		    {
		        throw new NotImplementedException("Currently only ITestTable_Readable is supported");
		    }
		
		    StringBuilder commandBuilder = new StringBuilder("select Column from TestTable");
		
		    using (DbCommand command = transaction.Connection.CreateCommand())
		    {
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(TestTable_Table) != ((Column)entity).Table)
			                    throw new InvalidWhereClause("Only columns from the table selected on are valid");
			
			        string whereClause;
			        List<DbParameter> parameters = new List<DbParameter>(DatabaseConnectorLocator.EmbeddedDatabaseConnector.Build(condition, out whereClause));
			
			        commandBuilder.Append(whereClause);
			        command.Parameters.AddRange(parameters.ToArray());
			    }
			
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
			
			            TestTable_Readable toYield = new TestTable_Readable();
			
			            if (System.DBNull.Value != values[0])
			              toYield._Column = ((System.String)values[0]);
			            else
			              toYield._Column = default(System.String);

			
			            object o = toYield;
			            yield return (T_Select)o;
			        }
			
			        dataReader.Close();
			    }
			}
		}
		
		public override int Delete(ObjectCloud.ORM.DataAccess.WhereConditionals.ComparisonCondition condition)
		{
		    StringBuilder commandBuilder = new StringBuilder("delete from TestTable");
		
		    using (DbCommand command = transaction.Connection.CreateCommand())
			{
			    // A null condition just avoids the where clause
			    if (null != condition)
			    {
			        // For now, avoid where clauses with foriegn columns
			        foreach (object entity in condition.Entities)
			            if (entity is Column)
			                if (typeof(TestTable_Table) != ((Column)entity).Table)
			                    throw new InvalidWhereClause("Only columns from the table selected on are valid");
			
			        string whereClause;
			        List<DbParameter> parameters = new List<DbParameter>(DatabaseConnectorLocator.EmbeddedDatabaseConnector.Build(condition, out whereClause));
			
			        commandBuilder.Append(whereClause);
			        command.Parameters.AddRange(parameters.ToArray());
			    }
			
			    command.CommandText = commandBuilder.ToString();
			    int rowsAffected = command.ExecuteNonQuery();
			
			    return rowsAffected;
			}
		}
		
		protected override int DoUpdate(ComparisonCondition condition, TestTable_Inserter inserter)
		{
		    StringBuilder commandBuilder = new StringBuilder("update TestTable");
		
		    using (DbCommand command = transaction.Connection.CreateCommand())
			{
			    List<string> setStatements = new List<string>();
			
			    if (inserter.Column_Changed)
			    {
			        setStatements.Add("Column = @Column");
			        DbParameter parm = DatabaseConnectorLocator.EmbeddedDatabaseConnector.ConstructParameter("@Column", inserter._Column);
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
			                if (typeof(TestTable_Table) != ((Column)entity).Table)
			                    throw new InvalidWhereClause("Only columns from the table selected on are valid");
			
			        string whereClause;
			        List<DbParameter> parameters = new List<DbParameter>(DatabaseConnectorLocator.EmbeddedDatabaseConnector.Build(condition, out whereClause));
			
			        commandBuilder.Append(whereClause);
			        command.Parameters.AddRange(parameters.ToArray());
			    }
			
			    command.CommandText = commandBuilder.ToString();
			    int rowsAffected = command.ExecuteNonQuery();
			
			    return rowsAffected;
			}
		}
	}

}