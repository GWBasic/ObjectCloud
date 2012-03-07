// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Data.Common;
using System.Reflection;
using System.Reflection.Emit;
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

			ConstructorInfo sqliteConnectionConstructor = sqlConnectionType.GetConstructor(new Type[] {typeof(string)});
			
			if (null == sqliteConnectionConstructor)
				throw new TypeLoadException("SQLite connection class does not have a constructor that takes a connection string");
			
			_OpenDelegate = (GenericArgumentReturn<string, DbConnection>)CreateDelegate(
				sqliteConnectionConstructor, typeof(GenericArgumentReturn<string, DbConnection>));

			MethodInfo sqlConnectionCreateFileMethod = sqlConnectionType.GetMethod("CreateFile", BindingFlags.Static | BindingFlags.Public, null, new Type[] {typeof(string)}, null);
			
			if (null == sqlConnectionCreateFileMethod)
				throw new TypeLoadException("Can not find a CreateFile static method for " + sqlConnectionType.Name + " that takes a single argument as a string");
			
			_CreateFileDelegate = (GenericArgument<string>)Delegate.CreateDelegate(
				typeof(GenericArgument<string>),
				sqlConnectionCreateFileMethod);
			
			Type sqlParameterType = null;
			
			if (isMono)
				sqlParameterType = Type.GetType("Mono.Data.Sqlite.SqliteParameter, Mono.Data.Sqlite");
			else
				sqlParameterType = Type.GetType("System.Data.SQLite.SQLiteParameter, System.Data.SQLite");
						
			log.InfoFormat("SqlParameterType: {0}", sqlParameterType.AssemblyQualifiedName);

			if (null == sqlParameterType)
				throw new TypeLoadException("Can not find Mono.Data.Sqlite.SqliteParameter or System.Data.SQLite.SQLiteParameter");

			ConstructorInfo sqlParameterConstructor = sqlParameterType.GetConstructor(new Type[] {typeof(string), typeof(object)});
			
			if (null == sqlParameterConstructor)
				throw new TypeLoadException("Can not find a constructor for " + sqlParameterType.Name + " that takes a parameter name and value");
			
			_ConstructParameterDelegate = (GenericArgumentReturn<string, object, DbParameter>)CreateDelegate(
				sqlParameterConstructor, typeof(GenericArgumentReturn<string, object, DbParameter>));
		}
		
		private static GenericArgumentReturn<string, DbConnection> _OpenDelegate;
	
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
			return _OpenDelegate(connectionString);
		}
		
		private static GenericArgument<string> _CreateFileDelegate;

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
			_CreateFileDelegate(databaseFilename);
		}

		private static GenericArgumentReturn<string, object, DbParameter> _ConstructParameterDelegate;
	
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
			return _ConstructParameterDelegate(parameterName, value);
		}
		
		/// <summary>
		/// From http://blogs.msdn.com/b/zelmalki/archive/2008/12/12/reflection-fast-object-creation.aspx
		/// TODO: Turn this into an extension method when I move to .Net 3 or 4
		/// </summary>
		public static Delegate CreateDelegate(ConstructorInfo constructor, Type delegateType)
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
