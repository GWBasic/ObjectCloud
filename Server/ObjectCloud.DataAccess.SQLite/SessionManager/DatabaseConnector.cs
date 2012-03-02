// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;

using ObjectCloud.Common;

namespace ObjectCloud.DataAccess.SQLite.SessionManager
{
    public partial class DatabaseConnector
    {
        public void DoUpgradeIfNeeded(DbConnection connection)
        {
            DbCommand command;

            command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version;";

            object versionObject = command.ExecuteScalar();
            int version = Convert.ToInt32(versionObject);

            if (version < 3)
            {
                command = connection.CreateCommand();
                command.CommandText =
@"drop index if exists Session_MaxAge;
drop index if exists Session_KeepAlive;
drop table if exists Session;

create table Session 
(
	UserID			guid not null,
	MaxAge			integer not null,
	WhenToDelete			integer not null,
	KeepAlive			boolean not null,
	SessionID			guid not null	primary key
);Create index Session_WhenToDelete on Session (WhenToDelete);

PRAGMA user_version = 3;";

                command.ExecuteNonQuery();
            }

        }
    }
}
