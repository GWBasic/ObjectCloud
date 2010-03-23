// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SQLite;
using System.Text;

using ObjectCloud.Interfaces.Database;

namespace ObjectCloud.ORM.DataAccess.SQLite
{
    /// <summary>
    /// An IEmbeddedDatabaseConnector for SQLite that uses phxsoftware's System.Data.SQLite.  Works on both Mono and .Net
    /// </summary>
    public class PHX_SQLiteDatabaseConnector : SQLiteConnectorBase
    {
        public override void CreateFile(string databaseFilename)
        {
            SQLiteConnection.CreateFile(databaseFilename);
        }

        protected override DbConnection OpenInt(string connectionString)
        {
            return new SQLiteConnection(connectionString);
        }

        public override DbParameter ConstructParameter(string parameterName, object value)
        {
            return new SQLiteParameter(parameterName, value);
        }
    }
}
