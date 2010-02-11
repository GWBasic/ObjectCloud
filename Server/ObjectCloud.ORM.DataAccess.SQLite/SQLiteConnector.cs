// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Data.Common;
using System.Reflection;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Database;

namespace ObjectCloud.ORM.DataAccess.SQLite
{
	/// <summary>
	/// Embedded database connector that dynamically selects Mono.Data.SQLite or System.Data.SQLite at runtime
	/// </summary>
	public class SQLiteConnector : SQLiteConnectorBase
	{
		/// <value>
		/// The Type object for SqlConnection
		/// </value>
		public Type SqlConnectionType
		{
			get
			{
                if (null == _SqlConnectionType)
                    _SqlConnectionType = TypeFunctions.LoadType(
                        "Mono.Data.Sqlite.SqliteConnection, Mono.Data.Sqlite",
                        "System.Data.SQLite.SQLiteConnection, System.Data.SQLite");
			
				return _SqlConnectionType;
			}
		}
		private Type _SqlConnectionType = null;
		
		/// <value>
		/// SqlConnection's constructor that takes a connection string
		/// </value>
		public ConstructorInfo SqliteConnectionConstructor
		{
			get
			{
				if (null == _SqliteConnectionConstructor)
				{
					_SqliteConnectionConstructor = SqlConnectionType.GetConstructor(new Type[] {typeof(string)});
					
					if (null == _SqliteConnectionConstructor)
						throw new TypeLoadException("Can not find a constructor for " + SqlConnectionType.Name + " that takes a single argument as a string");
				}
					
				return _SqliteConnectionConstructor;
			}
		}
		private ConstructorInfo _SqliteConnectionConstructor = null;
	
		/// <summary>
		/// Opens a connection to the SQLite database with the given connection string.  Automatically chooses System.Data.SQLite or Mono.Data.Sqlite
		/// </summary>
		/// <param name="connectionString">
		/// A <see cref="System.String"/>
		/// </param>
		/// <returns>
		/// A <see cref="DbConnection"/>
		/// </returns>
		protected override DbConnection OpenInt(string connectionString)
		{
			return (DbConnection)SqliteConnectionConstructor.Invoke(new object[] {connectionString});
		}

		/// <value>
		/// SqliteConnection's CreateFile method
		/// </value>
		public MethodInfo SqliteConnectionCreateFileMethod
		{
			get
			{
				if (null == _SqliteConnectionCreateFileMethod)
				{
					_SqliteConnectionCreateFileMethod = SqlConnectionType.GetMethod("CreateFile", BindingFlags.Static | BindingFlags.Public, null, new Type[] {typeof(string)}, null);
					
					if (null == _SqliteConnectionCreateFileMethod)
						throw new TypeLoadException("Can not find a CreateFile static method for " + SqlConnectionType.Name + " that takes a single argument as a string");
				}
				
				return _SqliteConnectionCreateFileMethod;
			}
		}
		private MethodInfo _SqliteConnectionCreateFileMethod = null;

		/// <summary>
		/// Creates a new SQLite database.  Automatically chooses System.Data.SQLite or Mono.Data.Sqlite
		/// </summary>
		/// <param name="connectionString">
		/// A <see cref="System.String"/>
		/// </param>
		/// <returns>
		/// A <see cref="DbConnection"/>
		/// </returns>
		public override void CreateFile(string databaseFilename)
		{
			SqliteConnectionCreateFileMethod.Invoke(null, new object[] {databaseFilename});
		}
		
		/// <value>
		/// The Type object for SqliteParameter
		/// </value>
		public Type SqliteParameterType
		{
			get
			{
				if (null == _SqliteParameterType)
					_SqliteParameterType = TypeFunctions.LoadType(
						"Mono.Data.Sqlite.SqliteParameter, Mono.Data.Sqlite",
					    "System.Data.SQLite.SQLiteParameter, System.Data.SQLite");
			
				return _SqliteParameterType;
			}
		}
		private Type _SqliteParameterType = null;
		
		/// <value>
		/// SqliteParameter's constructor that takes a connection string
		/// </value>
		public ConstructorInfo SqliteParameterConstructor
		{
			get
			{
				if (null == _SqliteParameterConstructor)
				{
					_SqliteParameterConstructor = SqliteParameterType.GetConstructor(new Type[] {typeof(string), typeof(object)});
					
					if (null == SqliteParameterConstructor)
						throw new TypeLoadException("Can not find a constructor for " + SqliteParameterType.Name + " that takes a parameter name and value");
				}
					
				return _SqliteParameterConstructor;
			}
		}
		private ConstructorInfo _SqliteParameterConstructor = null;
	
		/// <summary>
		/// Constructs the appropriate DbParameter.  Automatically chooses System.Data.SQLite or Mono.Data.Sqlite
		/// </summary>
		/// <param name="paramName">
		/// A <see cref="System.String"/>
		/// </param>
		/// <param name="value">
		/// A <see cref="System.Object"/>
		/// </param>
		/// <returns>
		/// A <see cref="DbParameter"/>
		/// </returns>
		public override DbParameter ConstructParameter(string parameterName, object value)
		{
			return (DbParameter)SqliteParameterConstructor.Invoke(new object[] {parameterName, value});
		}
	}
}
