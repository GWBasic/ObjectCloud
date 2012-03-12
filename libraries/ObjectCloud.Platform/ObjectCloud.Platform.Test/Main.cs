// Copyright 2009 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;

using ObjectCloud.Platform;

namespace ObjectCloud.Platform.Test
{
	class MainClass
	{
		public static void Main(string[] args)
		{
			Console.WriteLine(SQLitePlatformAdapter.SqliteConnectionType.AssemblyQualifiedName);
			Console.WriteLine(SQLitePlatformAdapter.SqliteParameterType.AssemblyQualifiedName);
			
			Console.WriteLine("Blocking...");
			
			object result = Blocker.Block();
			
			Console.WriteLine("Unblocked on " + result.ToString());
		}
	}
}