// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Data.Common;
using System.Reflection;
using System.Runtime.InteropServices;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Database;

namespace ObjectCloud.ORM.DataAccess.SQLite
{
	/// <summary>
	/// Embedded database connector that dynamically selects Mono.Data.SQLite or System.Data.SQLite at runtime
	/// </summary>
	public class SQLiteConnector : SQLiteConnectorBase
	{
		private static ILog log = LogManager.GetLogger<SQLiteConnector>();
		
		static SQLiteConnector()
		{
			bool isMono = null != Type.GetType ("Mono.Runtime");
			int environmentSize = Marshal.SizeOf(typeof(IntPtr));
			
			//Console.WriteLine(typeof(Mono.Data.Sqlite.SqliteConnection).Assembly.FullName);
			
			if (isMono)
				Assembly.Load("Mono.Data.Sqlite, Version=2.0.0.0, Culture=neutral, PublicKeyToken=0738eb9f132ed756");
			else if (4 == environmentSize)
				Assembly.Load("System.Data.SQLite.Win32.dll");
			else if (8 == environmentSize)
				Assembly.Load("System.Data.SQLite.x64.dll");
			else
				throw new TypeLoadException("Don't know what sqlite library to load");
			
			Type sqlConnectionType;
			
			if (isMono)
				sqlConnectionType = Type.GetType("Mono.Data.Sqlite.SqliteConnection, Mono.Data.Sqlite");
			else
				sqlConnectionType = Type.GetType("System.Data.SQLite.SQLiteConnection, System.Data.SQLite");
			
			if (null == sqlConnectionType)
				throw new TypeLoadException("Can not find Mono.Data.Sqlite.SqliteConnection or System.Data.SQLite.SQLiteConnection");
			
			log.InfoFormat("SqlConnectionType: {0}", sqlConnectionType.AssemblyQualifiedName);

			_SqliteConnectionConstructor = sqlConnectionType.GetConstructor(new Type[] {typeof(string)});
			
			if (null == _SqliteConnectionConstructor)
				throw new TypeLoadException("SQLite connection class does not have a constructor that takes a connection string");

			_SqliteConnectionCreateFileMethod = sqlConnectionType.GetMethod("CreateFile", BindingFlags.Static | BindingFlags.Public, null, new Type[] {typeof(string)}, null);
			
			if (null == _SqliteConnectionCreateFileMethod)
				throw new TypeLoadException("Can not find a CreateFile static method for " + sqlConnectionType.Name + " that takes a single argument as a string");
			
			Type sqlParameterType = null;
			
			if (isMono)
				sqlParameterType = Type.GetType("Mono.Data.Sqlite.SqliteParameter, Mono.Data.Sqlite");
			else
				sqlParameterType = Type.GetType("System.Data.SQLite.SQLiteParameter, System.Data.SQLite");
						
			log.InfoFormat("SqlParameterType: {0}", sqlParameterType.AssemblyQualifiedName);

			if (null == sqlParameterType)
				throw new TypeLoadException("Can not find Mono.Data.Sqlite.SqliteParameter or System.Data.SQLite.SQLiteParameter");

			_SqlParameterConstructor = sqlParameterType.GetConstructor(new Type[] {typeof(string), typeof(object)});
			
			if (null == _SqlParameterConstructor)
				throw new TypeLoadException("Can not find a constructor for " + sqlParameterType.Name + " that takes a parameter name and value");
		}
		
		/*public SQLiteConnector()
		{
			_SqlConnectionType = Type.GetType("Mono.Data.Sqlite.SqliteConnection, Mono.Data.Sqlite, Version=2.0.0.0"); //typeof(Mono.Data.Sqlite.SqliteConnection);
			_SqliteParameterType = typeof(Mono.Data.Sqlite.SqliteParameter);
			
			Console.WriteLine(_SqlConnectionType.FullName);
			Console.WriteLine(_SqlConnectionType.AssemblyQualifiedName);

            // deal with 32-bit versus 64-bit
            string myPath = Assembly.GetExecutingAssembly().Location;
            myPath = Path.GetDirectoryName(myPath);
            int environmentSize = Marshal.SizeOf(typeof(IntPtr));

            if (4 == environmentSize)
                // 32-bit
                File.Copy(
                    Path.Combine(myPath, "SQLite.Interop.Win32.dll"),
                    Path.Combine(myPath, "SQLite.Interop.dll"), true);

            else if (8 == environmentSize)
                // 64-bit
                File.Copy(
                    Path.Combine(myPath, "SQLite.Interop.x64.dll"),
                    Path.Combine(myPath, "SQLite.Interop.dll"), true);
		}*/
		/*
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
		private ConstructorInfo _SqliteConnectionConstructor = null;*/
		
		private static ConstructorInfo _SqliteConnectionConstructor = null;
	
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
			return (DbConnection)_SqliteConnectionConstructor.Invoke(new object[] {connectionString});
		}
		
		/*
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
		}*/
		private static MethodInfo _SqliteConnectionCreateFileMethod = null;

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
			_SqliteConnectionCreateFileMethod.Invoke(null, new object[] {databaseFilename});
		}
		
		/*
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
		}*/
		private static ConstructorInfo _SqlParameterConstructor = null;
	
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
			return (DbParameter)_SqlParameterConstructor.Invoke(new object[] {parameterName, value});
		}
	}
}
