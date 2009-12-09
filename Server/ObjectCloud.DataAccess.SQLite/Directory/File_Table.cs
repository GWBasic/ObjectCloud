// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;

using ObjectCloud.Common;
using ObjectCloud.DataAccess.Directory;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.ORM.DataAccess;

namespace ObjectCloud.DataAccess.SQLite.Directory
{
    public partial class File_Table : ObjectCloud.DataAccess.Directory.File_Table
    {

        public override IEnumerable<IFile_Readable> GetNewestFiles(
            ID<IUserOrGroup, Guid> userId,
            IEnumerable<ID<IUserOrGroup, Guid>> userOrGroupIds,
            long maxToReturn)
        {
            // Duplicate the IEnumerable because it's iterated over twice
            //userOrGroupIds = new List<ID<IUserOrGroup, Guid>>(userOrGroupIds);

            Dictionary<ID<IUserOrGroup, Guid>, string> groupIdsAndParamNames = new Dictionary<ID<IUserOrGroup,Guid>,string>();
            foreach (ID<IUserOrGroup, Guid> groupId in userOrGroupIds)
                groupIdsAndParamNames[groupId] = "@" + groupId.Value.ToString("N");
            
            string query = "select distinct Name, TypeId, OwnerId, Created, file.FileId from File left outer join Permission on file.FileId = Permission.FileId where UserOrGroupId in ("
                + StringGenerator.GenerateCommaSeperatedList(groupIdsAndParamNames.Values) + // (This makes each ID a parameter)
                ") or OwnerId = @UserId order by Created desc limit " + maxToReturn.ToString();

            // This is copied & pasted from the generated text
            // TODO...  Make this be a seperate function that I can call
            using (DbCommand command = Connection.CreateCommand())
            {
                command.CommandText = query;

                DbParameter parameter;

                foreach (ID<IUserOrGroup, Guid> groupId in groupIdsAndParamNames.Keys)
                {
                    parameter = command.CreateParameter();
                    parameter.ParameterName = groupIdsAndParamNames[groupId];
                    parameter.Value = groupId.Value;
                    command.Parameters.Add(parameter);
                }

                parameter = command.CreateParameter();
                parameter.ParameterName = "@UserId";
                parameter.Value = userId.Value;
                command.Parameters.Add(parameter);

                DbDataReader dataReader;
                try
                {
                    dataReader = command.ExecuteReader();
                }
                catch (Exception e)
                {
                    throw new QueryException("Exception when running query", e);
                }

                using (dataReader)
                {
                    while (dataReader.Read())
                    {
                        object[] values = new object[5];
                        dataReader.GetValues(values);

                        File_Readable toYield = new File_Readable();

                        if (System.DBNull.Value != values[0])
                            toYield._Name = ((System.String)values[0]);
                        else
                            toYield._Name = default(System.String);

                        if (System.DBNull.Value != values[1])
                            toYield._TypeId = ((System.String)values[1]);
                        else
                            toYield._TypeId = default(System.String);

                        if (System.DBNull.Value != values[2])
                            toYield._OwnerId = new ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>(((System.Guid)values[2]));
                        else
                            toYield._OwnerId = default(System.Nullable<ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>>);

                        if (System.DBNull.Value != values[3])
                            toYield._Created = new DateTime(((System.Int64)values[3]));
                        else
                            toYield._Created = default(System.DateTime);

                        if (System.DBNull.Value != values[4])
                            toYield._FileId = new ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64>(((System.Int64)values[4]));
                        else
                            toYield._FileId = default(ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64>);


                        yield return toYield;
                    }

                    dataReader.Close();
                }
            }
        }
    }
}
