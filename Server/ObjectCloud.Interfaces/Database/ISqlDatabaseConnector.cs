// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;

using ObjectCloud.ORM.DataAccess.WhereConditionals;

namespace ObjectCloud.Interfaces.Database
{
    /// <summary>
    /// Abstracts out the database connection and parameter construction logic so it can be wired through Spring
    /// </summary>
    public interface ISqlDatabaseConnector
    {
        // TODO:  Opening a database that uses a connection string should be a seperate interface, however, these DBs aren't used yet!
        /// <summary>
        /// Opens a database connection using the given connection string
        /// </summary>
        /// <param name="connectionString"></param>
        /// <returns></returns>
		DbConnection Open(string connectionString);

        /*// <summary>
        /// Returns a new DbParameter object for the specific database library
        /// </summary>
        /// <param name="parameterName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        DbParameter ConstructParameter(string parameterName, object value);*/

        /// <summary>
        /// Builds a where clause from a ComparisonCondition object
        /// </summary>
        /// <param name="comparisonCondition"></param>
        /// <param name="whereClause"></param>
        /// <returns></returns>
        DbParameter[] Build(ComparisonCondition comparisonCondition, out string whereClause);
    }
}
