// Copyright 2009, 2010 Andrew Rondeau
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
                        new Column("Sender", NotNull.String, ColumnOption.Indexed),
                        new Column("ObjectUrl", NotNull.String, ColumnOption.Indexed),
                        new Column("Title", NotNull.String, ColumnOption.Indexed),
                        new Column("DocumentType", NotNull.String, ColumnOption.Indexed),
                        new Column("MessageSummary", NotNull.String),
                        new Column("State", EnumColumn<NotificationState>.NotNullColumnType, ColumnOption.Indexed)
                    });

            database.Tables.Add(NotificationTable);

            Table ChangeDataTable =
                new Table(
                    "ChangeData",
                    new Column("NotificationId", NotNull.Long, ColumnOption.None, NotificationTable, NotificationTable.PrimaryKey),
                    new Column[]
                    {
                        new Column("ChangeData", NotNull.String),
                    });

            database.Tables.Add(ChangeDataTable);

            database.Tables.Add(
                new Table(
                    "Sender",
                    new Column("OpenID", NotNull.String),
                    new Column[]
                    {
                        new Column("SenderToken", Null.String, ColumnOption.Indexed),
                        new Column("RecipientToken", Null.String, ColumnOption.Indexed)
                    }));

            database.Tables.Add(
                new Table(
                    "Token",
                    new Column("OpenId", NotNull.String),
                    new Column[]
                    {
                        new Column("Token", NotNull.String, ColumnOption.Indexed),
                        new Column("Created", NotNull.TimeStamp)
                    }));

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

            database.Version = 2;

            return database;
        }
    }
}
