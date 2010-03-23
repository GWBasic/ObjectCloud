// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Xml;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Interfaces.Disk
{
    public interface IDirectoryHandler : IFileHandler
    {
        /// <summary>
        /// Creates a file
        /// </summary>
        /// <param name="fileName">The file name to create</param>
        /// <param name="fileType">The type of file to create</param>
        /// <param name="owner">The user that owns the file, or null if no one owns the file</param>
        /// <returns></returns>
        /// <exception cref="DuplicateFile">Thrown if a file with the given name already exists</exception>
        IFileHandler CreateFile(string fileName, string fileType, ID<IUserOrGroup, Guid>? ownerID);

        /// <summary>
        /// Restores a file from a dump on disk
        /// </summary>
        /// <param name="fileName">The file name to create</param>
        /// <param name="fileType">The type of file to create</param>
        /// <param name="pathToRestoreFrom">The path on disk that contains the dump of the file</param>
        /// <param name="userId">The user that owns the file</param>
        /// <returns></returns>
        /// <exception cref="DuplicateFile">Thrown if a file with the given name already exists</exception>
        IFileHandler RestoreFile(string fileName, string fileType, string pathToRestoreFrom, ID<IUserOrGroup, Guid> userId);

        /// <summary>
        /// Creates a system file.  This should not be exposed to the web API...  Ever!
        /// </summary>
        /// <param name="fileName">The file name to create</param>
        /// <param name="fileType">The type of file to create</param>
        /// <param name="owner">The user that owns the file, or null if no one owns the file</param>
        /// <returns></returns>
        /// <exception cref="SystemFileException">Thrown if there are errors creating the file, such as if the wrong file is created</exception>
        /// <exception cref="DuplicateFile">Thrown if a file with the given name already exists</exception>
        TFileHandler CreateSystemFile<TFileHandler>(string fileName, string fileType, ID<IUserOrGroup, Guid>? ownerID)
            where TFileHandler : IFileHandler;

        /// <summary>
        /// Gets an IFileHandler given the file name
        /// </summary>
        /// <param name="fileName">the file name</param>
        /// <exception cref="FileDoesNotExist">Thrown if the file does not exist</exception>
        /// <returns></returns>
        IFileContainer OpenFile(string fileName);

        /// <summary>
        /// Returns all of the files in the directory
        /// </summary>
        IEnumerable<IFileContainer> Files { get;}
        
        /// <summary>
        /// Returns the files that the user has access to, sorted by create date
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="maxToReturn"></param>
        /// <returns></returns>
        IEnumerable<IFileContainer> GetNewestFiles(ID<IUserOrGroup, Guid> userId, long maxToReturn);

        /// <summary>
        /// Returns all files that match the given relationships, set a parameter to null to match all.  Only files that the user can read are returned
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <param name="parentFileIds">The parent file ID</param>
        /// <param name="relatedFileIds">The related file IDs</param>
        /// <param name="relationships">The named relationship</param>
        /// <param name="extensions">The extension</param>
        /// <returns></returns>
        IEnumerable<IFileContainer> GetRelatedFiles(
            ID<IUserOrGroup, Guid> userId,
            IFileId parentFileId,
            IEnumerable<string> relationships,
            IEnumerable<string> extensions,
            DateTime? newest,
            DateTime? oldest,
            uint? maxToReturn);

        // TODO:  Work from related files instead of parent files

        /// <summary>
        /// Creates the relationship in the directory
        /// </summary>
        /// <param name="parentFileId">The parent file's ID.  This must be present in the directory</param>
        /// <param name="relatedFileId">The related file's ID.  This must be present in the directory</param>
        /// <param name="relationship">The name of the relationship</param>
        void AddRelationship(
            IFileContainer parentFile,
            IFileContainer relatedFile,
            string relationship);

        /// <summary>
        /// Creates the relationship in the directory
        /// </summary>
        /// <param name="parentFileId">The parent file's ID.  This must be present in the directory</param>
        /// <param name="relatedFileId">The related file's ID.  This must be present in the directory</param>
        /// <param name="relationship">The name of the relationship</param>
        void DeleteRelationship(
            IFileContainer parentFile,
            IFileContainer relatedFile,
            string relationship);

        /// <summary>
        /// Sets the permission for the given user
        /// </summary>
        /// <param name="assigningPermission">The user that is assigning permission.  This is the "from" when a notification is sent</param>
        /// <param name="userId">The user to set the permission for</param>
        /// <param name="filePermission">The permission value</param>
        /// <exception cref="FileDoesNotExist">Thrown if the file does not exist</exception>
        void SetPermission(ID<IUserOrGroup, Guid>? assigningPermission, string filename, ObjectCloud.Common.ID<IUserOrGroup, Guid> userOrGroupId, FilePermissionEnum level, bool inherit, bool sendNotifications);

        /// <summary>
        /// Removes a permission for a given user
        /// </summary>
        /// <param name="userId">The user to remove the permission for</param>
        /// <exception cref="FileDoesNotExist">Thrown if the file does not exist</exception>
        void RemovePermission(string filename, ID<IUserOrGroup, Guid> userOrGroupId);
		
		/// <summary>
		/// Returns all permissions for the file 
		/// </summary>
		/// <returns>
		/// An <see cref="IEnumerable"/> that contains each user ID and permission level
		/// </returns>
		IEnumerable<FilePermission> GetPermissions(string filename);

        /// <summary>
        /// Loads the user's permission for the file.  Associations with groups are traveresed.  The permission returned is either
        /// the permission directly assigned to the user, or the highest permission assigned to any group that the user is a member of
        /// </summary>
        /// <returns>The user's permission for the file, or null if the user has no access to the file</returns>
        FilePermissionEnum? LoadPermission(string filename, ID<IUserOrGroup, Guid> userId);

        /// <summary>
        /// Loads the highest permission for the file for the given user or group IDs.  Traverses up the directory tree to find inherited
        /// permissions if no immediate permission is found
        /// </summary>
        /// <param name="onlyReturnInheritedPermissions">When set to true, only returns parameters that are set to inherit</param>
        /// <returns>The user's permission for the file, or null if the user has no access to the file</returns>
        FilePermissionEnum? LoadPermission(string filename, IEnumerable<ID<IUserOrGroup, Guid>> userAndGroupIds, bool onlyReturnInheritedPermissions);

        /// <summary>
        /// Deletes a file
        /// </summary>
        /// <exception cref="FileDoesNotExist">Thrown if the file does not exist</exception>
        /// <param name="changer">User making the change</param>
        void DeleteFile(IUser changer, string filename);

        /// <summary>
        /// Returns all of the filenames in the directory
        /// </summary>
        /// <returns></returns>
        IEnumerable<string> GetFilenames();

        /// <summary>
        /// Returns true if the file is present, false otherwise
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        bool IsFilePresent(string filename);

        /// <summary>
        /// Renames the given file
        /// </summary>
        /// <param name="changer">User making the change</param>
        /// <param name="oldFilename">The name of the file to rename</param>
        /// <param name="newFilename">The new name</param>
        /// <exception cref="FileDoesNotExist">Thrown if the file does not exist</exception>
        /// <exception cref="DuplicateFile">Thrown if a file with the given name already exists</exception>
        void Rename(IUser changer, string oldFilename, string newFilename);

        /// <summary>
        /// Copies the file
        /// </summary>
        /// <param name="changer">User making the change</param>
        /// <param name="toCopy">The source file container to copy</param>
        /// <param name="newFileName">Thew new file name to use</param>
        void CopyFile(IUser changer, IFileContainer toCopy, string newFileName, ID<IUserOrGroup, Guid>? ownerID);

        /// <summary>
        /// The file to display if there is no Method or Action specified when navigating to the directory
        /// </summary>
        string IndexFile { get; set; }

        /// <summary>
        /// Occurs whenever the directory changes
        /// </summary>
        event EventHandler<IDirectoryHandler, EventArgs> DirectoryChanged;

        /// <summary>
        /// Changes a file's owner
        /// </summary>
        /// <param name="changer"></param>
        /// <param name="newOwner"></param>
        void Chown(IUser changer, IFileId fileId, ID<IUserOrGroup, Guid>? newOwner);

        /// <summary>
        /// Returns the owner of the file
        /// </summary>
        /// <param name="fileId"></param>
        /// <returns></returns>
        ID<IUserOrGroup, Guid>? GetOwnerId(IFileId fileId);

        /// <summary>
        /// Sets a named permission
        /// </summary>
        /// <param name="fileId"></param>
        /// <param name="namedPermission"></param>
        /// <param name="userOrGroupId"></param>
        /// <param name="inherit"></param>
        void SetNamedPermission(IFileId fileId, string namedPermission, ID<IUserOrGroup, Guid> userOrGroupId, bool inherit);

        /// <summary>
        /// Removes a named permission
        /// </summary>
        /// <param name="fileId"></param>
        /// <param name="namedPermission"></param>
        /// <param name="userOrGroupId"></param>
        void RemoveNamedPermission(IFileId fileId, string namedPermission, ID<IUserOrGroup, Guid> userOrGroupId);

        /// <summary>
        /// True if any of the users or groups has the named permission
        /// </summary>
        /// <param name="fileId"></param>
        /// <param name="namedPermission"></param>
        /// <param name="userOrGroupIds"></param>
        /// <param name="checkInherit">True to check parent directories</param>
        /// <returns></returns>
        bool HasNamedPermissions(IEnumerable<IFileId> fileIds, IEnumerable<string> namedPermission, IEnumerable<ID<IUserOrGroup, Guid>> userOrGroupIds, bool checkInherit);

        /// <summary>
        /// True if the user has the named permission
        /// </summary>
        /// <param name="fileId"></param>
        /// <param name="namedPermission"></param>
        /// <param name="userOrGroupIds"></param>
        /// <param name="checkInherit">True to check parent directories</param>
        /// <returns></returns>
        bool HasNamedPermissions(IFileId fileId, IEnumerable<string> namedPermission, ID<IUserOrGroup, Guid> userId);

        /// <summary>
        /// Returns all of the named permissions for the given file with the given name
        /// </summary>
        /// <param name="fileId"></param>
        /// <param name="namedPermission"></param>
        /// <returns></returns>
        IEnumerable<NamedPermission> GetNamedPermissions(IFileId fileId, string namedPermission);
    }

    /// <summary>
    /// Represents a named permission
    /// </summary>
    public struct NamedPermission
    {
        /// <summary>
        /// The file ID
        /// </summary>
        public IFileId FileId
        {
            get { return _FileId; }
            set { _FileId = value; }
        }
        IFileId _FileId;

        /// <summary>
        /// The named permission
        /// </summary>
        public string Name
        {
            get { return _Name; }
            set { _Name = value; }
        }
        string _Name;

        /// <summary>
        /// The user or group that the named permission applies to
        /// </summary>
        public ID<IUserOrGroup, Guid> UserOrGroupId
        {
            get { return _UserOrGroupId; }
            set { _UserOrGroupId = value; }
        }
        ID<IUserOrGroup, Guid> _UserOrGroupId;

        /// <summary>
        /// True if the named permission can be inherited
        /// </summary>
        public bool Inherit
        {
            get { return _Inherit; }
            set { _Inherit = value; }
        }
        bool _Inherit;
    }
}