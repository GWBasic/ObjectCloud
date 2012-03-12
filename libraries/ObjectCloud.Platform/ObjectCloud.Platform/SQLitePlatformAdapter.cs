// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Data.Common;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Reflection.Emit;

namespace ObjectCloud.Platform
{
	/// <summary>
	/// Embedded database connector that dynamically selects Mono.Data.SQLite or System.Data.SQLite at runtime
	/// </summary>
	public static class SQLitePlatformAdapter
	{
		static SQLitePlatformAdapter()
		{
			if (null != Type.GetType("Mono.Runtime"))
			{
				UseMonoDataSqlite();
				
				/*/ Use Mono's version of SQLite
				SQLitePlatformAdapter.sqliteConnectionType = Type.GetType(
					"Mono.Data.Sqlite.SqliteConnection, Mono.Data.Sqlite, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0738eb9f132ed756");
				if (null == SQLitePlatformAdapter.sqliteConnectionType)
					throw new TypeLoadException("Can not find Mono.Data.Sqlite.SqliteConnection");
				
				SQLitePlatformAdapter.sqliteParameterType = Type.GetType(
					"Mono.Data.Sqlite.SqliteParameter, Mono.Data.Sqlite, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0738eb9f132ed756");
				if (null == SQLitePlatformAdapter.sqliteParameterType)
					throw new TypeLoadException("Can not find Mono.Data.Sqlite.SqliteParameter");
				
				var sqliteConnectionConstructor = SQLitePlatformAdapter.sqliteConnectionType.GetConstructor(new Type[] {typeof(string)});
				if (null == sqliteConnectionConstructor)
					throw new TypeLoadException("Mono.Data.Sqlite.SqliteConnection does not have a constructor that takes a connection string");				
				
				SQLitePlatformAdapter.openConnection = (Func<string, DbConnection>)sqliteConnectionConstructor.CreateDelegate(
					typeof(Func<string, DbConnection>));
				
				var sqlConnectionCreateFileMethod = SQLitePlatformAdapter.sqliteConnectionType.GetMethod(
					"CreateFile", BindingFlags.Static | BindingFlags.Public, null, new Type[] {typeof(string)}, null);
			
				if (null == sqlConnectionCreateFileMethod)
					throw new TypeLoadException("Can not find a CreateFile static method for Mono.Data.Sqlite.SqliteConnection that takes a single argument as a string");
				
				SQLitePlatformAdapter.createFile = (Action<string>)Delegate.CreateDelegate(
					typeof(Action<string>),
					sqlConnectionCreateFileMethod);
				
				var sqliteParameterConstructor = SQLitePlatformAdapter.sqliteParameterType.GetConstructor(new Type[] {typeof(string), typeof(object)});
				
				if (null == sqliteParameterConstructor)
					throw new TypeLoadException("Can not find a constructor for Mono.Data.Sqlite.SqliteParameter that takes a parameter name and value");
				
				SQLitePlatformAdapter.constructParameter = (Func<string, object, DbParameter>)sqliteParameterConstructor.CreateDelegate(
					typeof(Func<string, object, DbParameter>));*/
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
				SQLitePlatformAdapter.sqliteConnectionType = typeof(System.Data.SQLite.SQLiteConnection);
				SQLitePlatformAdapter.sqliteParameterType = typeof(System.Data.SQLite.SQLiteParameter);
				
				SQLitePlatformAdapter.openConnection = connectionString =>
					new System.Data.SQLite.SQLiteConnection(connectionString);
				
				SQLitePlatformAdapter.createFile = databaseFileName =>
					System.Data.SQLite.SQLiteConnection.CreateFile(databaseFileName);
				
				SQLitePlatformAdapter.constructParameter = (parameterName, value) =>
					new System.Data.SQLite.SQLiteParameter(parameterName, value);
			}
		}

		static void UseMonoDataSqlite()
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
		
		/// <summary>
		/// The .Net type used to create SQLite connections
		/// </summary>
		public static Type SqliteConnectionType 
		{
			get { return SQLitePlatformAdapter.sqliteConnectionType; }
		}
		private static Type sqliteConnectionType;

		/// <summary>
		/// The .Net type used to create SQLite parameters
		/// </summary>
		public static Type SqliteParameterType 
		{
			get { return SQLitePlatformAdapter.sqliteParameterType; }
		}
		private static Type sqliteParameterType;

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
		
		/// <summary>
		/// From http://blogs.msdn.com/b/zelmalki/archive/2008/12/12/reflection-fast-object-creation.aspx
		/// TODO: Turn this into an extension method when I move to .Net 3 or 4
		/// </summary>
		public static Delegate CreateDelegate(this ConstructorInfo constructor, Type delegateType)
        {
            if (constructor == null)
            {
                throw new ArgumentNullException("constructor"); 
            }
            if (delegateType == null)
            {
                throw new ArgumentNullException("delegateType");
            }

            // Validate the delegate return type
            MethodInfo delMethod = delegateType.GetMethod("Invoke");
            /*if (delMethod.ReturnType != constructor.DeclaringType)
            {
                throw new InvalidOperationException("The return type of the delegate must match the constructors delclaring type");
            }*/

            // Validate the signatures
            ParameterInfo[] delParams = delMethod.GetParameters();
            ParameterInfo[] constructorParam = constructor.GetParameters();
            if (delParams.Length != constructorParam.Length)
            {
                throw new InvalidOperationException("The delegate signature does not match that of the constructor");
            }
            for (int i = 0; i < delParams.Length; i++)
            {
                if (delParams[i].ParameterType != constructorParam[i].ParameterType ||  // Probably other things we should check ??
                    delParams[i].IsOut)
                {
                    throw new InvalidOperationException("The delegate signature does not match that of the constructor");
                }
            }
            // Create the dynamic method
            DynamicMethod method =
                new DynamicMethod(
                    string.Format("{0}__{1}", constructor.DeclaringType.Name, Guid.NewGuid().ToString().Replace("-","")),
                    delMethod.ReturnType,
                    Array.ConvertAll<ParameterInfo, Type>(constructorParam, p => p.ParameterType),
                    true
                    );

            // Create the il
            ILGenerator gen = method.GetILGenerator();
            for (int i = 0; i < constructorParam.Length; i++)
            {
                if (i < 4)
                {
                    switch (i)
                    {
                        case 0:
                            gen.Emit(OpCodes.Ldarg_0);
                            break;
                        case 1:
                            gen.Emit(OpCodes.Ldarg_1);
                            break;
                        case 2:
                            gen.Emit(OpCodes.Ldarg_2);
                            break;
                        case 3:
                            gen.Emit(OpCodes.Ldarg_3);
                            break;
                    }
                }
                else
                {
                    gen.Emit(OpCodes.Ldarg_S, i);   
                }
            }
            gen.Emit(OpCodes.Newobj, constructor);
            gen.Emit(OpCodes.Ret);

            // Return the delegate :)
            return method.CreateDelegate(delegateType);
        }
	}
}
