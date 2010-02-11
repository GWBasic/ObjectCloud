// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;

namespace ObjectCloud.Interfaces.Database
{
    /// <summary>
    /// Abstracts out the database connection and parameter construction logic so it can be wired through Spring.  Also allows for construction of an embedded database within
    /// the local filesystem
    /// </summary>
    public interface IEmbeddedDatabaseConnector : ISqlDatabaseConnector
    {
        /// <summary>
        /// Creates an embedded database at the given path
        /// </summary>
        /// <param name="databaseFilename"></param>
		void CreateFile(string databaseFilename);

        /// <summary>
        /// Opens an embedded database at the given path in the local filesystem
        /// </summary>
        /// <param name="databaseFilename"></param>
        /// <returns></returns>
        DbConnection OpenEmbedded(string databaseFilename);
    }
}
