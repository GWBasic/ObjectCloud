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
    public class UserSchemaCreator
    {
        public Database Create()
        {
            Database database = new Database();

            database.Tables.Add(
                new Table(
                    "Pairs",
                    new Column("Name", NotNull.String),
                    new Column[]
                    {
                        new Column("Value", NotNull.String),
                    }));

            Table NotificationTable = 
                new Table(
                    "Notification",
                    new Column("NotificationId", NotNull.Long, true),
                    new Column[]
                    {
                        new Column("TimeStamp", NotNull.TimeStamp, ColumnOption.Indexed),
                        new Column("SenderIdentity", NotNull.String, ColumnOption.Indexed),
                        new Column("ObjectUrl", NotNull.String, ColumnOption.Indexed),
                        new Column("SummaryView", NotNull.String),
                        new Column("DocumentType", NotNull.String, ColumnOption.Indexed),
                        new Column("Verb", NotNull.String),
                        new Column("ChangeData", Null.String),
                        new Column("LinkedSenderIdentity", Null.String, ColumnOption.Indexed)
                    });

            database.Tables.Add(NotificationTable);

            database.Tables.Add(
                new Table(
                    "Blocked",
                    new Column("OpenIdorDomain", NotNull.String)));

            database.Tables.Add(
                new Table(
                    "ObjectState",
                    new Column("ObjectUrl", NotNull.String),
                    new Column[]
                    {
                        new Column("ObjectState", NotNull.Int)
                    }));

            database.Tables.Add(
                new Table(
                    "Deleted",
                    new Column("ObjectUrl", NotNull.String),
                    new Column[]
                    {
                        new Column("OpenId", NotNull.String)
                    }));

            database.Tables.Add(
                new Table(
                    "Trusted",
                    new Column("Domain", NotNull.String),
                    new Column[]
                    {
                        new Column("Login", Null.Bool),
                        new Column("Link", Null.Bool)
                    }));

            database.Version = 4;

            return database;
        }
    }
}
