// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;
using ObjectCloud.ORM.DataAccess.DomainModel;

namespace ObjectCloud.CodeGenerator
{
    public class SessionManagerSchemaCreator
    {
        public Database Create()
        {
            Database database = new Database();

            database.Tables.Add(
                new Table(
                    "Session",
                    new Column("SessionID", IDColumn<ISession, Guid>.NotNullColumnType),
                    new Column[]
                    {
                        new Column("UserID", IDColumn<IUserOrGroup, Guid>.NotNullColumnType),
                        new Column("MaxAge", NotNull.TimeSpan),
                        new Column("WhenToDelete", NotNull.TimeStamp, ColumnOption.Indexed),
                        new Column("KeepAlive", NotNull.Bool)
                    }));

            database.Version = 3;

            return database;
        }
    }
}
