// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Text;

using JsonFx.Json;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Javascript.SubProcess
{
	/// <summary>
	/// Functions that are exposed to objects that handle a database
	/// </summary>
	public static class JavascriptDatabaseFunctions
	{
		/// <summary>
		/// Holds information needed to upgrade a query
		/// </summary>
		struct SchemaUpgradeQuery : IComparable<SchemaUpgradeQuery>
		{
			public double Version;
            public Dictionary<string, object> UpgradeArrayElement;
	
			public int CompareTo (SchemaUpgradeQuery other)
			{
				return Version.CompareTo(other.Version);
			}
		}
		
		public static object setSchema(object[] schema, double version)
		{
			FunctionCallContext functionCallContext = FunctionCallContext.GetCurrentContext();
			
			IDatabaseHandler databaseHandler = functionCallContext.ScopeWrapper.FileContainer.CastFileHandler<IDatabaseHandler>();

			// If the version isn't set, then set it to the lowest possible value so it can get every needed version upgrade
			if (null == databaseHandler.Version)
				databaseHandler.Version = double.NegativeInfinity;
			
			if (double.NaN != version)
				if (databaseHandler.Version >= version)
					return null;
			
			List<SchemaUpgradeQuery> schemaUpgradeQueries = new List<SchemaUpgradeQuery>();
			foreach (Dictionary<string, object> upgradeOperation in schema)
            {
					double operationToVersion = Convert.ToDouble(upgradeOperation["Version"]);
				
					if (databaseHandler.Version < operationToVersion)
					{
						SchemaUpgradeQuery suq = new SchemaUpgradeQuery();
						suq.Version = operationToVersion;
						suq.UpgradeArrayElement = upgradeOperation;

                        schemaUpgradeQueries.Add(suq);
					}
            }
			
			// If the schema is up-to-date, return
			if (schemaUpgradeQueries.Count == 0)
				return null;
			
			schemaUpgradeQueries.Sort();
			
			DbConnection connection = databaseHandler.Connection;
			
			double upgradedVersion = double.NegativeInfinity;
			
			using (DbTransaction transaction = connection.BeginTransaction())
				try
				{
					foreach (SchemaUpgradeQuery suq in schemaUpgradeQueries)
					{
						DbCommand command = connection.CreateCommand();
                        command.CommandText = suq.UpgradeArrayElement["Query"].ToString();
						command.ExecuteNonQuery();
					
						upgradedVersion = suq.Version;
					}
					
					transaction.Commit();
				 	databaseHandler.Version = upgradedVersion;
				}
				catch
				{
					transaction.Rollback();
                    throw;
				}
			
			return null;
		}
	}
}
