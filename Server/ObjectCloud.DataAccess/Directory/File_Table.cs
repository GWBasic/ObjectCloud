// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.DataAccess.Directory
{
    public partial class File_Table
    {
        /// <summary>
        /// Returns the newest files that the user has access to
        /// </summary>
        /// <param name="userOrGroupIds"></param>
        /// <param name="maxToReturn"></param>
        /// <returns></returns>
        public abstract IEnumerable<IFile_Readable> GetNewestFiles(
            ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, Guid> userId,
            IEnumerable<ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, Guid>> userOrGroupIds,
            long maxToReturn);
    }

    /// <summary>
    /// Additional information about a file
    /// </summary>
    public class FileData
    {
        /// <summary>
        /// The permissions indexed by user or group id
        /// </summary>
        public Dictionary<Guid, Permission> Permissions { get; set; }

        /// <summary>
        /// The named permissions, indexed by user or group id
        /// </summary>
        public Dictionary<Guid, Dictionary<string, bool>> NamedPermissions { get; set; }

        /*// <summary>
        /// The child relationships
        /// </summary>
        public List<Relationship> Relationships { get; set; }

        /// <summary>
        /// The parent relationships
        /// </summary>
        public List<Relationship> ParentRelationships { get; set; }*/
    }

    /// <summary>
    /// Details about a permission
    /// </summary>
    public class Permission
    {
        /// <summary>
        /// The actual permission
        /// </summary>
        public FilePermissionEnum Level { get; set; }

        /// <summary>
        /// True if the permission is inherited
        /// </summary>
        public bool Inherit { get; set; }

        /// <summary>
        /// True to send notifications
        /// </summary>
        public bool SendNotifications { get; set; }
    }

    /*// <summary>
    /// Details about a relationship
    /// </summary>
    public class Relationship
    {
        /// <summary>
        /// The FileId
        /// </summary>
        public long FileId { get; set; }

        /// <summary>
        /// The name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// True if the relationship is inherited
        /// </summary>
        public bool Inherit { get; set; }
    }*/
}
