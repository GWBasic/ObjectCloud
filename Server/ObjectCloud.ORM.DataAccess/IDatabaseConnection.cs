// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;

using ObjectCloud.Common;

namespace ObjectCloud.ORM.DataAccess
{
    /// <summary>
    /// Represents a database connection
    /// </summary>
    public interface IDatabaseConnection<TDatabaseTransaction> : IDisposable
        where TDatabaseTransaction : IDatabaseTransaction
    {
        /// <summary>
        /// Calls the delegate with a transaction.  Rolls back if there is an exception.  The delegate must commit the transaction if changes are made
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="del"></param>
        /// <returns></returns>
        T CallOnTransaction<T>(GenericArgumentReturn<TDatabaseTransaction, T> del);

        /// <summary>
        /// Calls the delegate with a transaction.  Rolls back if there is an exception.  The delegate must commit the transaction if changes are made
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="del"></param>
        /// <returns></returns>
        void CallOnTransaction(GenericArgument<TDatabaseTransaction> del);

        /// <summary>
        /// Returns the underlying ADO database connection
        /// </summary>
        DbConnection DbConnection { get; }
		
		/// <summary>
		/// Performs any needed maintenance operations, such as SQLite's Vacuum function
		/// </summary>
		void Vacuum();
    }
}
