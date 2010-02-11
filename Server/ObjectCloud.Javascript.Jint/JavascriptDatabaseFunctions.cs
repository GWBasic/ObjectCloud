// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Text;

using Jint;
using Jint.Delegates;
using Jint.Native;
using JsonFx.Json;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Javascript.Jint
{
    /// <summary>
    /// Functions exposed to Javascript
    /// </summary>
    public static class JavascriptDatabaseFunctions
    {
        /// <summary>
        /// All of the functions that are exposed to Javascript
        /// </summary>
        public static IEnumerable<Delegate> Delegates
        {
            get
            {
                yield return new Func<JsObject, double, object>(setSchema);
            }
        }

		/// <summary>
		/// Holds information needed to upgrade a query
		/// </summary>
		struct SchemaUpgradeQuery : IComparable<SchemaUpgradeQuery>
		{
			public double Version;
			public JsObject UpgradeArrayElement;
	
			public int CompareTo (SchemaUpgradeQuery other)
			{
				return Version.CompareTo(other.Version);
			}
		}
		
		public static object setSchema(JsObject schema, double version)
		{
			FunctionCallContext functionCallContext = FunctionCallContext.GetCurrentContext();
			
			IDatabaseHandler databaseHandler = functionCallContext.ScopeWrapper.TheObject.CastFileHandler<IDatabaseHandler>();

			// If the version isn't set, then set it to the lowest possible value so it can get every needed version upgrade
			if (null == databaseHandler.Version)
				databaseHandler.Version = double.NegativeInfinity;
			
			if (double.NaN != version)
				if (databaseHandler.Version >= version)
					return null;
			
			List<SchemaUpgradeQuery> schemaUpgradeQueries = new List<SchemaUpgradeQuery>();
			
			foreach (string id in schema.GetKeys())
            {
                double idDouble = default(double);
				if (double.TryParse(id, out idDouble))
				{
                    JsObject upgradeOperation = (JsObject)schema[id];
                    JsInstance operationToVersionInstance = upgradeOperation["Version"];
                    double operationToVersion = Convert.ToDouble(operationToVersionInstance.Value);
				
					if (databaseHandler.Version < operationToVersion)
					{
						SchemaUpgradeQuery suq = new SchemaUpgradeQuery();
						suq.Version = operationToVersion;
						suq.UpgradeArrayElement = upgradeOperation;

                        schemaUpgradeQueries.Add(suq);
					}
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
                        command.CommandText = (string)suq.UpgradeArrayElement["Query"].Value.ToString();
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
