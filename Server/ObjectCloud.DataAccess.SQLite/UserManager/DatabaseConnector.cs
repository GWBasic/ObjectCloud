// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;

using ObjectCloud.Common;

namespace ObjectCloud.DataAccess.SQLite.UserManager
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
@"alter table Groups add column Type integer not null default 2;

PRAGMA user_version = 3;";

                command.ExecuteNonQuery();
            }

            if (version < 4)
            {
                command = connection.CreateCommand();
                command.CommandText =
@"Create unique index UserInGroups_UserID_GroupID on UserInGroups (UserID, GroupID);

PRAGMA user_version = 4;";

                command.ExecuteNonQuery();
            }

            if (version < 5)
            {
                command = connection.CreateCommand();
                command.CommandText =
@"create table GroupAliases 
(
	UserID			guid not null references Users(ID),
	GroupID			guid not null references Groups(ID),
	Alias			string not null
);Create index GroupAliases_UserID on GroupAliases (UserID);
Create index GroupAliases_GroupID on GroupAliases (GroupID);
Create unique index GroupAliases_GroupID_UserID on GroupAliases (GroupID, UserID);
Create unique index GroupAliases_UserID_Alias on GroupAliases (UserID, Alias);

PRAGMA user_version = 5;";

                command.ExecuteNonQuery();
            }

            if (version < 6)
            {
                command = connection.CreateCommand();
                command.CommandText =
@"create table Sender 
(
	name			string not null unique,
	senderToken			string not null unique,
	loginURL			string not null,
	loginURLOpenID			string not null,
	loginURLWebFinger			string not null,
	loginURLRedirect			string not null,
	senderID			integer not null	primary key AUTOINCREMENT
);create table Recipient 
(
	userID			guid not null,
	receiveNotificationEndpoint			string not null,
	senderToken			string not null
);Create unique index Recipient_userID_receiveNotificationEndpoint on Recipient (userID, receiveNotificationEndpoint);

PRAGMA user_version = 6;";

                command.ExecuteNonQuery();
            }
        }
    }
}
