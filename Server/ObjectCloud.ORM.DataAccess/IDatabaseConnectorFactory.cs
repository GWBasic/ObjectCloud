// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.ORM.DataAccess
{
    public interface IDatabaseConnectorFactory<TDatabaseConnector, TDatabaseConnection, TDatabaseTransaction>
        where TDatabaseConnector : IDatabaseConnector<TDatabaseConnector, TDatabaseConnection, TDatabaseTransaction>
        where TDatabaseConnection : IDatabaseConnection<TDatabaseTransaction>
        where TDatabaseTransaction : IDatabaseTransaction
    {
        /// <summary>
        /// Creates a database connector for an embedded database
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        TDatabaseConnector CreateConnectorForEmbedded(string path);

        /*// <summary>
        /// Creates a database connector for the given database connection string
        /// </summary>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        TDatabaseConnector CreateConnector(string connectionString);*/
    }
}
