// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;

using ObjectCloud.Common;

namespace ObjectCloud.DataAccess.SQLite.Log
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

            if (version < 2)
            {
                command = connection.CreateCommand();
                command.CommandText =
@"Create index Classes_Name on Classes (Name);
Create index Log_ClassId on Log (ClassId);
Create index Log_TimeStamp on Log (TimeStamp);
Create index Log_Level on Log (Level);
Create index Log_ThreadId on Log (ThreadId);
Create index Log_SessionId on Log (SessionId);
Create index Log_RemoteEndPoint on Log (RemoteEndPoint);
Create index Log_UserId on Log (UserId);
Create index Log_ExceptionClassId on Log (ExceptionClassId);
PRAGMA user_version = 2;
";

                command.ExecuteNonQuery();
            }

            if (version < 3)
            {
                command = connection.CreateCommand();
                command.CommandText =
@"Create index Log_TimeStamp_Level on Log (TimeStamp, Level);
PRAGMA user_version = 3;
";

                command.ExecuteNonQuery();
            }

            if (version < 4)
            {
                command = connection.CreateCommand();
                command.CommandText =
@"alter table Log add ExceptionStackTrace string;
PRAGMA user_version = 4;
";

                command.ExecuteNonQuery();
            }
        }
    }
}
