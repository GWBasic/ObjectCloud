// Copyright 2009, 2010 Andrew Rondeau
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

            Column userIdColumn = new Column("ID", IDColumn<IUserOrGroup, Guid>.NotNullColumnType, ColumnOption.Indexed | ColumnOption.Unique);

            Table userTable = new Table(
                    "Users",
                    new Column("Name", NotNull.String),
                    new Column[]
                    {
                        new Column("PasswordMD5", NotNull.String),
                        userIdColumn,
                        new Column("BuiltIn", NotNull.Bool)
                    });

            database.Tables.Add(userTable);

            Column groupIdColumn = new Column("ID", IDColumn<IUserOrGroup, Guid>.NotNullColumnType, ColumnOption.Indexed | ColumnOption.Unique);

            Table groupsTable = new Table(
                    "Groups",
                    new Column("Name", NotNull.String),
                    new Column[]
                    {
                        groupIdColumn,
                        new Column("OwnerID", IDColumn<IUserOrGroup, Guid>.NullColumnType, ColumnOption.Indexed, userTable, userIdColumn),
                        new Column("BuiltIn", NotNull.Bool),
                        new Column("Automatic", NotNull.Bool),
                        new Column("Type", EnumColumn<GroupType>.NotNullColumnType)
                    });

            database.Tables.Add(groupsTable);

            Table userInGroupsTable = new Table(
                    "UserInGroups",
                    new Column[]
                    {
                        new Column("UserID", IDColumn<IUserOrGroup, Guid>.NotNullColumnType, ColumnOption.Indexed, userTable, userIdColumn),
                        new Column("GroupID", IDColumn<IUserOrGroup, Guid>.NotNullColumnType, ColumnOption.Indexed, groupsTable, groupIdColumn)
                    });

            userInGroupsTable.CompoundIndexes.Add(new Index(userInGroupsTable.Columns, true));

            database.Tables.Add(userInGroupsTable);

            database.Tables.Add(
                new Table(
                    "AssociationHandles",
                    new Column[]
                    {
                        new Column("UserID", IDColumn<IUser, Guid>.NotNullColumnType, ColumnOption.Indexed),
					    new Column("AssociationHandle", NotNull.String),
						new Column("Timestamp", NotNull.TimeStamp)
                    }));

            Column groupAliasesUserIDColumn = new Column("UserID", IDColumn<IUserOrGroup, Guid>.NotNullColumnType, ColumnOption.Indexed, userTable, userIdColumn);
            Column groupAliasesGroupIDColumn = new Column("GroupID", IDColumn<IUserOrGroup, Guid>.NotNullColumnType, ColumnOption.Indexed, groupsTable, groupIdColumn);
            Column groupAliasesAliasColumn = new Column("Alias", NotNull.String);

            Table groupAliasesTable = new Table(
                    "GroupAliases",
                    new Column[]
                    {
                        groupAliasesUserIDColumn,
                        groupAliasesGroupIDColumn,
                        groupAliasesAliasColumn
                    });

            groupAliasesTable.CompoundIndexes.Add(new Index(new Column[] { groupAliasesGroupIDColumn, groupAliasesUserIDColumn }, true));
            groupAliasesTable.CompoundIndexes.Add(new Index(new Column[] { groupAliasesUserIDColumn, groupAliasesAliasColumn }, true));

            database.Tables.Add(groupAliasesTable);

            database.Version = 5;

            return database;
        }
    }
}
