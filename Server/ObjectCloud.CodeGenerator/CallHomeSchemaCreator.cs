// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Interfaces.Disk;
using ObjectCloud.ORM.DataAccess.DomainModel;

namespace ObjectCloud.CodeGenerator
{
    class CallHomeSchemaCreator
    {
        public Database Create()
        {
            Database database = new Database();

            Column serversPK = new Column("HostID", NotNull.Long, true);

            Table serversTable = new Table(
                "Servers",
                serversPK,
                new Column[]
                {
                    new Column("Hostname", NotNull.String, ColumnOption.Unique),
                    new Column("LastCheckin", NotNull.TimeStamp, ColumnOption.Indexed),
                    new Column("Version", NotNull.String)
                });

            database.Tables.Add(serversTable);

            database.Tables.Add(
                new Table(
                    "CallhomeLog",
                    new Column[]
                    {
                        new Column("HostID", NotNull.Long, ColumnOption.None, serversTable, serversPK),
                        new Column("Timestamp", NotNull.TimeStamp)
                    }));

            database.Version = 1;

            return database;
        }
    }
}
