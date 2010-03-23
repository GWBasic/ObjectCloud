// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Common;

namespace ObjectCloud.ORM.DataAccess
{
    public interface IDatabaseConnector<TDatabaseConnector, TDatabaseConnection, TDatabaseTransaction>
        where TDatabaseConnector : IDatabaseConnector<TDatabaseConnector, TDatabaseConnection, TDatabaseTransaction>
        where TDatabaseConnection : IDatabaseConnection<TDatabaseTransaction>
        where TDatabaseTransaction : IDatabaseTransaction
    {
        /// <summary>
        /// Occurs after a transaction is committed
        /// </summary>
        event EventHandler<TDatabaseConnector, EventArgs> DatabaseWritten;

        /// <summary>
        /// Returns a connection to the database
        /// </summary>
        /// <returns></returns>
        TDatabaseConnection Connect();

        /// <summary>
        /// The last time a change was committed to the database
        /// </summary>
        DateTime LastModified { get; }

        /// <summary>
        /// Restores the database from an image
        /// </summary>
        /// <param name="pathToRestoreFrom"></param>
        void Restore(string pathToRestoreFrom);
    }
}
