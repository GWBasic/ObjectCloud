// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;
using ObjectCloud.ORM.DataAccess.SQLite;

namespace ObjectCloud.Disk.WebHandlers
{
    /// <summary>
    /// Web-callable wrapper for embedded databasess
    /// </summary>
    public class DatabaseWebHandler : DatabaseWebHandler<IDatabaseHandler, DatabaseWebHandler> { }

    /// <summary>
    /// Base class for web handlers that exposes an underlying database to the web if the file supports a database
    /// </summary>
    /// <typeparam name="TFileHandler"></typeparam>
    /// <typeparam name="TWebHandler"></typeparam>
    public class DatabaseWebHandler<TFileHandler, TWebHandler> : WebHandler<TFileHandler>
        where TFileHandler : IFileHandler
        where TWebHandler : DatabaseWebHandler<TFileHandler, TWebHandler>
    {
        /// <summary>
        /// Returns the results of the query.  Results are committed to the database.  Inteded for writes.
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="query">The query to run.</param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.JSON, FilePermissionEnum.Administer)]
        public IWebResults PostQuery(IWebConnection webConnection, string query)
        {
            return RunQuery(query, webConnection);
        }

        /// <summary>
        /// Runs the stored procedure and returns the results.  Intended for queries that write.  Changes are committed.
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="procFile">This file contains the stored procedure to run</param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.JSON, FilePermissionEnum.Read)]
        // Minimum permission set in the stored proc
        public IWebResults PostToStoredProc(IWebConnection webConnection, string procFile)
        {
            return RunStoredProc(webConnection, procFile);
        }

        /// <summary>
        /// Returns the results of the stored procedure.  TODO:  Allow stored procs to declare their prefered HTTP verb (GET, POST...), and allow queries to declare if they need to be committed.  (GETs might still want to log.)
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="procFile"></param>
        /// <returns></returns>
        private IWebResults RunStoredProc(IWebConnection webConnection, string procFile)
        {
            if (null == procFile)
                return WebResults.FromString(Status._412_Precondition_Failed, "procFile is missing");

            IFileContainer procContainer = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(procFile);

            // Make sure the user has permission to view the stored procedure...  This might be disabled at a later time; not sure...
            FilePermissionEnum? permissionToReadProc = procContainer.LoadPermission(webConnection.Session.User.Id);
            if (null == permissionToReadProc)
                throw new SecurityException("You do not have permission to access " + procFile);

            INameValuePairsHandler procHandler = procContainer.CastFileHandler<INameValuePairsHandler>();

            // Make sure the user has the minimum permission specified in the stored procedure
            // Ommitting the minimum permission means that anyone can call the procedure
            if (procHandler.Contains("MinimumPermission"))
            {
                string minimumPermissionString = procHandler["MinimumPermission"];

                FilePermissionEnum? minimumPermission = Enum<FilePermissionEnum>.Parse(minimumPermissionString);

                if (null == minimumPermission)
                    throw new SecurityException("Unsupported Permission: " + minimumPermissionString);

                // TODO, figure out user's permission for DB
                FilePermissionEnum? userPermission = webConnection.UserPermission;

                if (null == userPermission)
                    throw new SecurityException("You do not have permission to access " + procFile);

                if (userPermission.Value < minimumPermission.Value)
                    throw new SecurityException("You do not have permission to access " + procFile);
            }

            if (!procHandler.Contains("Query"))
                return WebResults.FromString(Status._412_Precondition_Failed, procFile + " does not have a Query");

            string query = procHandler["Query"];

            return RunQuery(query, webConnection);
        }

        /// <summary>
        /// Helper method for running a query.  TODO:  An error should occur if an attempt is made to write when commit is set to false
        /// </summary>
        /// <param name="query">
        /// A <see cref="System.String"/>
        /// </param>
        /// <param name="webConnection">
        /// A <see cref="IWebConnection"/>
        /// </param>
        /// <returns>
        /// A <see cref="IWebResults"/>
        /// </returns>
        private IWebResults RunQuery(string query, IWebConnection webConnection)
        {
            IDatabaseHandler databaseHandler = DatabaseHandler;

            if (null == query)
                return WebResults.FromString(Status._412_Precondition_Failed, "query is missing");

            // Decode any parameters for the query
            // POST parameters have priority, if present
            Dictionary<string, string> parameters = new Dictionary<string, string>();

            foreach (string argName in webConnection.GetParameters.Keys)
                if (argName.StartsWith("@"))
                    parameters[argName] = webConnection.GetParameters[argName];

            if (null != webConnection.PostParameters)
                foreach (string argName in webConnection.PostParameters.Keys)
                    if (argName.StartsWith("@"))
                        parameters[argName] = webConnection.PostParameters[argName];

            // Compound queries are supported
            List<object> compoundResults = new List<object>();

            try
            {
                DbCommand command = databaseHandler.Connection.CreateCommand();

                // Add parameters
                foreach (KeyValuePair<string, string> parameter in parameters)
                {
                    DbParameter dbParameter = command.CreateParameter();
                    dbParameter.ParameterName = parameter.Key;
                    dbParameter.Value = parameter.Value;
                    command.Parameters.Add(dbParameter);
                }

                command.CommandText = query;

                using (IDataReader reader = command.ExecuteReader())
                {
                    do
                    {
                        if (reader.FieldCount > 0)
                        {
                            // If this query has fields, such as a select statement, then read...

                            List<Dictionary<string, object>> results = new List<Dictionary<string, object>>();

                            while (reader.Read())
                            {
                                // Turn each row into a Dictionary, which will be turned into a JSON object
                                Dictionary<string, object> result = new Dictionary<string, object>();

                                for (int colCtr = 0; colCtr < reader.FieldCount; colCtr++)
                                {
                                    string name = reader.GetName(colCtr);
                                    object value = reader.GetValue(colCtr);
                                    result[name] = value;
                                }

                                results.Add(result);
                            }

                            compoundResults.Add(results);
                        }
                        else
                        {
                            // else the query doesn't have rows, return the fields effected
                            compoundResults.Add(reader.RecordsAffected);
                        }

                    } while (reader.NextResult());
                }
            }
            catch (DbException dbException)
            {
                throw new WebResultsOverrideException(
                    WebResults.FromString(Status._400_Bad_Request, dbException.Message));
            }

            // By serializing to JSON outside of the using block, the database is blocked for less time!
            return WebResults.ToJson(compoundResults);
        }

        /// <summary>
        /// Gets the version
        /// </summary>
        /// <param name="connection">
        /// A <see cref="IWebConnection"/>
        /// </param>
        /// <returns>
        /// A <see cref="IWebResults"/>
        /// </returns>
        [WebCallable(WebCallingConvention.GET, WebReturnConvention.JSON, FilePermissionEnum.Read)]
        public IWebResults GetVersion(IWebConnection connection)
        {
            return WebResults.ToJson(DatabaseHandler.Version);
        }

        /// <summary>
        /// Sets the version
        /// </summary>
        /// <param name="connection">
        /// A <see cref="IWebConnection"/>
        /// </param>
        /// <param name="version">
        /// A <see cref="System.Nullable"/>
        /// </param>
        /// <returns>
        /// A <see cref="IWebResults"/>
        /// </returns>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Status, FilePermissionEnum.Administer)]
        public IWebResults SetVersion(IWebConnection connection, double? version)
        {
            DatabaseHandler.Version = version;

            return WebResults.FromStatus(Status._202_Accepted);
        }

        /// <summary>
        /// The database web handler, or an exception if this object doesn't support database-style access
        /// </summary>
        public IDatabaseHandler DatabaseHandler
        {
            get
            {
                if (!(FileHandler is IDatabaseHandler))
                    throw new WebResultsOverrideException(
                        WebResults.FromString(Status._412_Precondition_Failed, "The implementation of " + FileContainer.TypeId + " does not support database-style queries"));

                return (IDatabaseHandler)FileHandler;
            }
        }
    }
}