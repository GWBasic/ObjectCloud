// Copyright 2009, 2010 Andrew Rondeau
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
		/*// <summary>
		/// Holds information needed to upgrade a query
		/// </summary>
		struct SchemaUpgradeQuery : IComparable<SchemaUpgradeQuery>
		{
			public double Version;
			public Scriptable UpgradeArrayElement;
	
			public int CompareTo (SchemaUpgradeQuery other)
			{
				return Version.CompareTo(other.Version);
			}
		}
		
		public static object setSchema(Scriptable schema, Double version)
		{
			FunctionCallContext functionCallContext = FunctionCallContext.GetCurrentContext();
			
			IDatabaseHandler databaseHandler = functionCallContext.ScopeWrapper.TheObject.CastFileHandler<IDatabaseHandler>();

			// If the version isn't set, then set it to the lowest possible value so it can get every needed version upgrade
			if (null == databaseHandler.Version)
				databaseHandler.Version = double.NegativeInfinity;
			
			if (double.NaN != version.doubleValue())
				if (databaseHandler.Version >= version.doubleValue())
					return null;
			
			List<SchemaUpgradeQuery> schemaUpgradeQueries = new List<SchemaUpgradeQuery>();
			
			foreach (object id in schema.getIds())
				if (id is Number)
				{
					Scriptable upgradeOperation = (Scriptable)schema.get(((Number)id).intValue(), functionCallContext.Scope);
					Double operationToVersion = (Double)upgradeOperation.get("Version", functionCallContext.Scope);
				
					if (databaseHandler.Version < operationToVersion.doubleValue())
					{
						SchemaUpgradeQuery suq = new SchemaUpgradeQuery();
						suq.Version = operationToVersion.doubleValue();
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
						command.CommandText = (string)suq.UpgradeArrayElement.get("Query", functionCallContext.Scope);
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
		}*/
	}
}
