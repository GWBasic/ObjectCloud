// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Data.Common;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection;

namespace ObjectCloud.Platform
{
	/// <summary>
	/// Embedded database connector that dynamically selects Mono.Data.SQLite or System.Data.SQLite at runtime
	/// </summary>
	public class SQLitePlatformAdapter
	{
		static SQLitePlatformAdapter()
		{
			if (null != Type.GetType("Mono.Runtime"))
			{
				// Use Mono's version of SQLite
				SQLitePlatformAdapter.sqliteConnectionType = typeof(Mono.Data.Sqlite.SqliteConnection);
				SQLitePlatformAdapter.sqliteParameterType = typeof(Mono.Data.Sqlite.SqliteParameter);
				
				SQLitePlatformAdapter.openConnection = connectionString =>
					new Mono.Data.Sqlite.SqliteConnection(connectionString);
				
				SQLitePlatformAdapter.createFile = databaseFileName =>
					Mono.Data.Sqlite.SqliteConnection.CreateFile(databaseFileName);
				
				SQLitePlatformAdapter.constructParameter = (parameterName, value) =>
					new Mono.Data.Sqlite.SqliteParameter(parameterName, value);
			}
			else
			{
				// Copy the correct interop dll for System.Data.SQLite
				
				var addressSize = Marshal.SizeOf(typeof(int));
				
				string resourceId;
				if (4 == addressSize)
					resourceId = "ObjectCloud.Platform.SQLite.Interop.x86.dll";
				else
					resourceId = "ObjectCloud.Platform.SQLite.Interop.x64.dll";
				
				var assembly = Assembly.GetExecutingAssembly();
				var location = assembly.Location;
				location = Path.GetDirectoryName(location);
				var destinationDll = Path.Combine(location, "SQLite.Interop.dll");
				
				using (BinaryReader reader = new BinaryReader(assembly.GetManifestResourceStream(resourceId)))
				{
					var bytes = reader.ReadBytes(Convert.ToInt32(reader.BaseStream.Length));
					File.WriteAllBytes(destinationDll, bytes);
				}

				// Use the Windows version of SQLite
				SQLitePlatformAdapter.sqliteConnectionType = typeof(Mono.Data.Sqlite.SqliteConnection);
				SQLitePlatformAdapter.sqliteParameterType = typeof(Mono.Data.Sqlite.SqliteParameter);
				
				SQLitePlatformAdapter.openConnection = connectionString =>
					new System.Data.SQLite.SQLiteConnection(connectionString);
				
				SQLitePlatformAdapter.createFile = databaseFileName =>
					System.Data.SQLite.SQLiteConnection.CreateFile(databaseFileName);
				
				SQLitePlatformAdapter.constructParameter = (parameterName, value) =>
					new System.Data.SQLite.SQLiteParameter(parameterName, value);
			}
		}
		
		/// <summary>
		/// The .Net type used to create SQLite connections
		/// </summary>
		public static Type SqliteConnectionType 
		{
			get { return SQLitePlatformAdapter.sqliteConnectionType; }
		}
		private readonly static Type sqliteConnectionType;

		/// <summary>
		/// The .Net type used to create SQLite parameters
		/// </summary>
		public static Type SqliteParameterType 
		{
			get { return SQLitePlatformAdapter.sqliteParameterType; }
		}
		private readonly static Type sqliteParameterType;

		/// <summary>
		/// Opens a connection with the given connection string
		/// </summary>
		public static DbConnection OpenConnection(string connectionString)
		{
			return SQLitePlatformAdapter.openConnection(connectionString);
		}
		private static Func<string, DbConnection> openConnection;
		
		/// <summary>
		/// Constructs a parameter with the given name and value
		/// </summary>
		public static DbParameter ConstructParameter(string name, object value)
		{
			return SQLitePlatformAdapter.constructParameter(name, value);
		}
		private static Func<string, object, DbParameter> constructParameter;
		
		/// <summary>
		/// Creates a file at the given path.
		/// </summary>
		public static void CreateFile(string path)
		{
			SQLitePlatformAdapter.createFile(path);
		}
		private static Action<string> createFile;
	}
}
