// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;

using ObjectCloud.Common;

namespace ObjectCloud.DataAccess.SQLite.User
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
@"Create index Notification_TimeStamp on Notification (TimeStamp);
Create index Notification_Sender on Notification (Sender);
Create index Notification_ObjectUrl on Notification (ObjectUrl);
Create index Notification_Title on Notification (Title);
Create index Notification_DocumentType on Notification (DocumentType);
Create index Notification_State on Notification (State);
Create index Sender_SenderToken on Sender (SenderToken);
Create index Sender_RecipientToken on Sender (RecipientToken);
Create index Token_Token on Token (Token);

PRAGMA user_version = 2;
";

                command.ExecuteNonQuery();
            }
        }
    }
}