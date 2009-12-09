// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Interfaces.Security;
using ObjectCloud.ORM.DataAccess.DomainModel;

namespace ObjectCloud.CodeGenerator
{
    public class UserManagerSchemaCreator
    {
        public Database Create()
        {
            Database database = new Database();

            database.Tables.Add(
                new Table(
                    "Users",
                    new Column("Name", NotNull.String),
                    new Column[]
                    {
                        new Column("PasswordMD5", NotNull.String),
                        new Column("ID", IDColumn<IUserOrGroup, Guid>.NotNullColumnType, ColumnOption.Indexed | ColumnOption.Unique),
                        new Column("BuiltIn", NotNull.Bool)
                    }));

            database.Tables.Add(
                new Table(
                    "Groups",
                    new Column("Name", NotNull.String),
                    new Column[]
                    {
                        new Column("ID", IDColumn<IUserOrGroup, Guid>.NotNullColumnType, ColumnOption.Indexed | ColumnOption.Unique),
                        new Column("OwnerID", IDColumn<IUserOrGroup, Guid>.NullColumnType),
                        new Column("BuiltIn", NotNull.Bool),
                        new Column("Automatic", NotNull.Bool)
                    }));

            database.Tables.Add(
                new Table(
                    "UserInGroups",
                    new Column[]
                    {
                        new Column("UserID", IDColumn<IUserOrGroup, Guid>.NotNullColumnType, ColumnOption.Indexed),
                        new Column("GroupID", IDColumn<IUserOrGroup, Guid>.NotNullColumnType, ColumnOption.Indexed)
                    }));

            database.Tables.Add(
                new Table(
                    "AssociationHandles",
                    new Column[]
                    {
                        new Column("UserID", IDColumn<IUser, Guid>.NotNullColumnType, ColumnOption.Indexed),
					    new Column("AssociationHandle", NotNull.String),
						new Column("Timestamp", NotNull.TimeStamp)
                    }));

            database.Version = 2;

            return database;
        }
    }
}
