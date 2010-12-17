// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Text;

using MongoDB.Bson;

using ObjectCloud.Common;
using ObjectCloud.DataAccess.Directory;
using ObjectCloud.Interfaces.Security;

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

            if (version < 6)
            {
                command = connection.CreateCommand();
                command.CommandText =
@"alter table Relationships add Inherit boolean;
Update Relationships set Inherit = 'false';
PRAGMA user_version = 6;";
                command.ExecuteNonQuery();
            }

            if (version < 7)
            {
                command = connection.CreateCommand();
                command.CommandText = "select FileId from File";

                Dictionary<long, FileData> fileDatas = new Dictionary<long, FileData>();

                using (IDataReader dr = command.ExecuteReader())
                    while (dr.Read())
                    {
                        FileData fileData = new FileData();
                        fileData.Permissions = new Dictionary<Guid, Permission>();
                        fileData.NamedPermissions = new Dictionary<Guid, Dictionary<string, bool>>();

                        fileDatas[dr.GetInt64(0)] = fileData;
                    }

                command = connection.CreateCommand();
                command.CommandText = "select FileId, UserOrGroupId, Level, Inherit, SendNotifications from Permission";

                using (IDataReader dr = command.ExecuteReader())
                    while (dr.Read())
                    {
                        FileData fileInfo;
                        if (fileDatas.TryGetValue(dr.GetInt64(0), out fileInfo))
                        {
                            Guid userOrGroupId = dr.GetGuid(1);

                            Permission permission = new Permission();
                            permission.Level = (FilePermissionEnum)dr.GetInt32(2);
                            permission.Inherit = dr.GetBoolean(3);
                            permission.SendNotifications = dr.GetBoolean(4);

                            fileInfo.Permissions[userOrGroupId] = permission;
                        }
                    }

                command = connection.CreateCommand();
                command.CommandText = "select FileId, NamedPermission, UserOrGroup, Inherit from NamedPermission";

                using (IDataReader dr = command.ExecuteReader())
                    while (dr.Read())
                    {
                        FileData fileData;
                        if (fileDatas.TryGetValue(dr.GetInt64(0), out fileData))
                        {
                            Guid userOrGroupId = dr.GetGuid(2);

                            Dictionary<string, bool> namedPermissions;
                            if (!fileData.NamedPermissions.TryGetValue(userOrGroupId, out namedPermissions))
                            {
                                namedPermissions = new Dictionary<string, bool>();
                                fileData.NamedPermissions[userOrGroupId] = namedPermissions;
                            }

                            namedPermissions[dr.GetString(1)] = dr.GetBoolean(3);
                        }
                    }

                command = connection.CreateCommand();
                command.CommandText =
@"alter table File add Info string;";
                command.ExecuteNonQuery();

                foreach (KeyValuePair<long, FileData> idAndInfo in fileDatas)
                {
                    command = connection.CreateCommand();
                    command.CommandText = "update File set Info=@Info where FileId=@FileId";

                    DbParameter parameter;

                    parameter = command.CreateParameter();
                    command.Parameters.Add(parameter);
                    parameter.ParameterName = "@Info";
                    parameter.Value = Convert.ToBase64String(idAndInfo.Value.ToBson());

                    parameter = command.CreateParameter();
                    command.Parameters.Add(parameter);
                    parameter.ParameterName = "@FileId";
                    parameter.Value = idAndInfo.Key;

                    command.ExecuteNonQuery();
                }

                command = connection.CreateCommand();
                command.CommandText =
@"drop table Permission;
drop table NamedPermission;
vacuum;
PRAGMA user_version = 7;";
                command.ExecuteNonQuery();
            }
        }
    }
}