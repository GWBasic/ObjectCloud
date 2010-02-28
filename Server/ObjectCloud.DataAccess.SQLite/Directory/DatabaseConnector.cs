// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Text;

using ObjectCloud.Common;

namespace ObjectCloud.DataAccess.SQLite.Directory
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
@"Create index File_Name on File (Name);
Create index Metadata_Name on Metadata (Name);

PRAGMA user_version = 2;
";

                command.ExecuteNonQuery();
            }

            if (version < 3)
            {
                command = connection.CreateCommand();
                command.CommandText =
@"alter table File add Extension string;

create table Relationships 
(
	FileId			integer references File(FileId),
	ReferencedFileId			integer,
	Relationship			string not null
);

Create index Relationships_ReferencedFileId on Relationships (ReferencedFileId);
Create index Relationships_Relationship on Relationships (Relationship);
Create  index Relationships_FileId_Relationship on Relationships (FileId, Relationship);
Create  index Relationships_ReferencedFileId_Relationship on Relationships (ReferencedFileId, Relationship);
Create unique index Relationships_FileId_ReferencedFileId_Relationship on Relationships (FileId, ReferencedFileId, Relationship);


PRAGMA user_version = 3;
";

                command.ExecuteNonQuery();
            }

            if (version < 4)
            {
                command = connection.CreateCommand();
                command.CommandText = "select FileId, Name from File";

                StringBuilder nextCommandBuilder = new StringBuilder();

                using (IDataReader dr = command.ExecuteReader())
                    while (dr.Read())
                    {
                        long fileId = dr.GetInt64(0);
                        string name = dr.GetValue(0).ToString();

                        string extension;
                        if (name.Contains("."))
                            extension = name.Substring(name.LastIndexOf('.') + 1);
                        else
                            extension = "";

                        nextCommandBuilder.Append("update file set extension='" + extension + "' where fileId=" + fileId.ToString() + ";\n");
                    }

                nextCommandBuilder.Append("PRAGMA user_version = 4;");

                command = connection.CreateCommand();
                command.CommandText = nextCommandBuilder.ToString();

                command.ExecuteNonQuery();
            }

            if (version < 5)
            {
                command = connection.CreateCommand();
                command.CommandText =
@"create table NamedPermission 
(
	FileId			integer references File(FileId),
	NamedPermission			string not null,
	UserOrGroup			guid not null,
	Inherit			boolean not null
);Create index NamedPermission_FileId on NamedPermission (FileId);
Create unique index NamedPermission_FileId_NamedPermission_UserOrGroup on NamedPermission (FileId, NamedPermission, UserOrGroup);

PRAGMA user_version = 5;
";

                command.ExecuteNonQuery();
            }
            /*
            if (version < 6)
            {
                command = connection.CreateCommand();
                command.CommandText = "select fileId, Extension, TypeId from File where Extension = 'group' and TypeId = 'database'";

                using (IDataReader reader = command.ExecuteReader())
                {
                }

                command = connection.CreateCommand();
                command.CommandText = "update File set (TypeId = 'database') where Extension = 'group' and TypeId = 'database'";

                command.ExecuteNonQuery();

                command = connection.CreateCommand();
                command.CommandText =
@"PRAGMA user_version = 6;
";

                command.ExecuteNonQuery();
            }*/
        }
    }
}