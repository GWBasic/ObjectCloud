// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Data;
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
	identity			string not null unique,
	senderToken			string not null unique,
	loginURL			string not null,
	loginURLOpenID			string not null,
	loginURLWebFinger			string not null,
	loginURLRedirect			string not null,
	senderID			integer not null	primary key AUTOINCREMENT
);

create table Recipient 
(
	userID			guid not null,
	receiveNotificationEndpoint			string not null,
	senderToken			string not null
);Create unique index Recipient_userID_receiveNotificationEndpoint on Recipient (userID, receiveNotificationEndpoint);

PRAGMA user_version = 6;";

                command.ExecuteNonQuery();
            }

            if (version < 7)
            {
                command = connection.CreateCommand();
                command.CommandText =
@"alter table Users add column DisplayName string not null default Name;
alter table Users add column IdentityProvider integer not null default 0;
alter table Groups add column DisplayName string not null default Name;

update Users set DisplayName = Name;
update Groups set DisplayName = Name;

PRAGMA user_version = 7;";

                command.ExecuteNonQuery();
            }

            if (version < 8)
            {
                command = connection.CreateCommand();
                command.CommandText =
@"alter table Users add column IdentityProviderArgs string;

update Users set IdentityProvider = 1 where PasswordMD5 = 'openid';

PRAGMA user_version = 8;";

                command.ExecuteNonQuery();
            }

            /*if (version < 9)
            {
                command = connection.CreateCommand();
                command.CommandText =
@"
drop index Users_ID;
alter table Users rename to OldUsers;
create table Users 
(
	PasswordMD5			string not null,
	ID			guid not null unique,
	BuiltIn			boolean not null,
	IdentityProviderCode			integer not null,
	DisplayName			string not null,
	IdentityProviderArgs			string,
	Name			string not null
);Create index Users_ID on Users (ID);
Create unique index Users_Name_IdentityProviderCode on Users (Name, IdentityProviderCode);
select Name, PasswordMD5, ID, BuiltIn, IdentityProvider, DisplayName, IdentityProviderArgs from OldUsers;";

                LinkedList<object[]> results = new LinkedList<object[]>();

                using (IDataReader reader = command.ExecuteReader())
                    while (reader.Read())
                    {
                        object[] values = new object[7];
                        results.AddLast(values);
                        reader.GetValues(values);
                    }

                foreach (object[] values in results)
                {
                    command = connection.CreateCommand();
                    command.CommandText =
@"insert into Users (Name, PasswordMD5, ID, BuiltIn, IdentityProviderCode, DisplayName, IdentityProviderArgs)
values (@Name, @PasswordMD5, @ID, @BuiltIn, @IdentityProviderCode, @DisplayName, @IdentityProviderArgs);";

                    DbParameter parameter;

                    parameter = command.CreateParameter();
                    command.Parameters.Add(parameter);
                    parameter.ParameterName = "@Name";
                    parameter.Value = values[0];

                    parameter = command.CreateParameter();
                    command.Parameters.Add(parameter);
                    parameter.ParameterName = "@PasswordMD5";
                    parameter.Value = values[1];

                    parameter = command.CreateParameter();
                    command.Parameters.Add(parameter);
                    parameter.ParameterName = "@ID";
                    parameter.Value = values[2];

                    parameter = command.CreateParameter();
                    command.Parameters.Add(parameter);
                    parameter.ParameterName = "@BuiltIn";
                    parameter.Value = values[3];

                    parameter = command.CreateParameter();
                    command.Parameters.Add(parameter);
                    parameter.ParameterName = "@IdentityProviderCode";
                    parameter.Value = values[4];

                    parameter = command.CreateParameter();
                    command.Parameters.Add(parameter);
                    parameter.ParameterName = "@DisplayName";
                    parameter.Value = values[5];

                    parameter = command.CreateParameter();
                    command.Parameters.Add(parameter);
                    parameter.ParameterName = "@IdentityProviderArgs";
                    parameter.Value = values[6];

                    command.ExecuteNonQuery();
                }

                command = connection.CreateCommand();
                command.CommandText =
@"
drop table OldUsers;
PRAGMA user_version = 9;
vacuum;";

                command.ExecuteNonQuery();
            }*/
        }
    }
}
