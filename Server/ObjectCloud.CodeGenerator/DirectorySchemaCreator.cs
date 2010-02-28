// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.ORM.DataAccess.DomainModel;

namespace ObjectCloud.CodeGenerator
{
    public class DirectorySchemaCreator
    {
        public Database Create()
        {
            Database database = new Database();

            Table fileTable = new Table(
                    "File",
                    new Column("FileId", IDColumn<IFileContainer, long>.NotNullColumnType),
                    new Column[]
                    {
                        new Column("Name", NotNull.String, ColumnOption.Indexed | ColumnOption.Unique),

                        // The extension is stored in de-normalized form to improve searching for user-defined objects
                        new Column("Extension", NotNull.String, ColumnOption.Indexed),
                        
                        new Column("TypeId", NotNull.String),
                        new Column("OwnerId", IDColumn<IUserOrGroup, Guid>.NullColumnType),
                        new Column("Created", NotNull.TimeStamp)
                    });

            fileTable.RunPriorToInsertOrUpdate =
@"
                    if ({0}.Name_Changed)
                        if ({0}.Name.Contains(" + "\".\"))" + @"
                            {0}.Extension = {0}.Name.Substring({0}.Name.LastIndexOf('.') + 1);
                        else
                            {0}.Extension = " + "\"\"" + @";
";

            database.Tables.Add(fileTable);

            database.Tables.Add(
                new Table(
                    "Permission",
                    new Column[]
                    {
	                    fileTable.CreateNullableForiegnKeyColumn(),
                        new Column("UserOrGroupId", IDColumn<IUserOrGroup, Guid>.NotNullColumnType),
                        new Column("Level", EnumColumn<FilePermissionEnum>.NotNullColumnType),
                        new Column("Inherit", NotNull.Bool),
                        new Column("SendNotifications", NotNull.Bool)
                    }));

            // TODO:  Maintain foriegn key constraint between permissions and file

            database.Tables.Add(
                new Table(
                    "Metadata",
                    new Column("Name", NotNull.String, ColumnOption.Indexed),
                    new Column[]
                    {
                        new Column("Value", NotNull.String),
                    }));

            Column originalFileForiengKeyColumn = fileTable.CreateForiegnKeyColumn();
            Column referenceFileIdColumn = new Column("ReferencedFileId", IDColumn<IFileContainer, long>.NotNullColumnType, ColumnOption.Indexed);
            Column relationshipColumn = new Column("Relationship", NotNull.String, ColumnOption.Indexed);

            Table relationshipTable =
                new Table(
                    "Relationships",
                    new Column[]
                    {
                        originalFileForiengKeyColumn,
                        referenceFileIdColumn,
                        relationshipColumn
                    });

            relationshipTable.CompoundIndexes.Add(new Column[] { originalFileForiengKeyColumn, relationshipColumn });
            relationshipTable.CompoundIndexes.Add(new Column[] { referenceFileIdColumn, relationshipColumn });
            relationshipTable.CompoundIndexes.Add(new Index(relationshipTable.Columns, true));

            database.Tables.Add(relationshipTable);

            Column namedPermissionFileId = fileTable.CreateForiegnKeyColumn();
            namedPermissionFileId.Indexed = true;

            Column namedPermissionName = new Column("NamedPermission", NotNull.String);
            Column namedPermissionUserOrGroup = new Column("UserOrGroup", IDColumn<IUserOrGroup, Guid>.NotNullColumnType);
            Column namedPermissionInherit = new Column("Inherit", NotNull.Bool);


            Table namedPermissionTable =
                new Table(
                    "NamedPermission",
                    new Column[]
                    {
	                    namedPermissionFileId,
                        namedPermissionName,
                        namedPermissionUserOrGroup,
                        namedPermissionInherit
                    });

            namedPermissionTable.CompoundIndexes.Add(
                new Index(new Column[] { namedPermissionFileId, namedPermissionName, namedPermissionUserOrGroup}, true));

            database.Tables.Add(namedPermissionTable);

            database.Version = 5; // 6;

            return database;
        }
    }
}
