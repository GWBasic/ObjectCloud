// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;

using Common.Logging;
using JsonFx.Json;

using ObjectCloud.Common;
using ObjectCloud.Common.Threading;
using ObjectCloud.DataAccess.Directory;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Javascript;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;
using ObjectCloud.ORM.DataAccess.WhereConditionals;

namespace ObjectCloud.Disk.FileHandlers
{
    public class DirectoryHandler : HasDatabaseFileHandler<IDatabaseConnector, IDatabaseConnection, IDatabaseTransaction>, IDirectoryHandler
    {
        private static ILog log = LogManager.GetLogger<DirectoryHandler>();

        public DirectoryHandler(IDatabaseConnector databaseConnector, FileHandlerFactoryLocator fileHandlerFactoryLocator)
            : base(databaseConnector, fileHandlerFactoryLocator)
        {
            DatabaseConnector.DatabaseWritten += new EventHandler<IDatabaseConnector, EventArgs>(DatabaseConnector_TransactionCommitted);

            FileHandlerCache = new Cache<string, IFileContainer>(CreateForCache);
            OwnerIdCache = new Cache<IFileId, Wrapped<ID<IUserOrGroup, Guid>?>>(LoadOwnerIdForCache);
            FileIDCacheByName = new Cache<string, Wrapped<FileId>>(GetFileIdForCache);
            PermissionsCacheWithInherit = new Cache<string, Wrapped<FilePermissionEnum?>, LoadPermissionArgs>(LoadPermissionForCache);
            PermissionsCacheWithoutInherit = new Cache<string, Wrapped<FilePermissionEnum?>, LoadPermissionArgs>(LoadPermissionForCache);

            // TODO:  This can eventually go away, it's just to support old schemas
            foreach (IFile_Readable groupFile in new List<IFile_Readable>(
                DatabaseConnection.File.Select(File_Table.Extension == "group" & File_Table.TypeId == "database")))
            {
                DeleteFile(null, groupFile.Name);

                string groupName = groupFile.Name.Substring(0, groupFile.Name.Length - 6);

                IGroup group = FileHandlerFactoryLocator.UserManagerHandler.GetGroup(groupName);

                INameValuePairsHandler groupHandler = (INameValuePairsHandler)CreateFile(groupFile.Name, "group", groupFile.OwnerId);

                groupHandler.Set(null, "GroupId", group.Id.Value.ToString());
            }
        }

        /// <summary>
        /// The cache is cleared whenever the database is written to
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void DatabaseConnector_TransactionCommitted(IDatabaseConnector sender, EventArgs e)
        {
            FileHandlerCache.Clear();
        }

        /// <summary>
        /// Cache of pre-opened FileHandlers.  This is cleared whenever the database is written to
        /// </summary>
        private Cache<string, IFileContainer> FileHandlerCache;

        public IFileHandler CreateFile(string filename, string fileType, ID<IUserOrGroup, Guid>? ownerID)
        {
            IFileSystemResolver fileSystemResolver = FileHandlerFactoryLocator.FileSystemResolver;
            IFileHandlerFactory fileHandlerFactory = fileSystemResolver.GetFactoryForFileType(fileType);

            return CreateFileHelper(
                filename, fileType, ownerID, new CreateFileDelegate(fileHandlerFactory.CreateFile));
        }

        public IFileHandler RestoreFile(string filename, string fileType, string pathToRestoreFrom, ID<IUserOrGroup, Guid> userId)
        {
            FileHandlerFactoryLocator.FileSystemResolver.VerifyNoForbiddenChars(filename);

            IFileSystemResolver fileSystemResolver = FileHandlerFactoryLocator.FileSystemResolver;
            IFileHandlerFactory fileHandlerFactory = fileSystemResolver.GetFactoryForFileType(fileType);

            return CreateFileHelper(
                filename, fileType, userId, delegate(IFileId fileId)
                {
                    fileHandlerFactory.RestoreFile(fileId, pathToRestoreFrom, userId, this);
                });
        }

        public TFileHandler CreateSystemFile<TFileHandler>(string filename, string fileType, ID<IUserOrGroup, Guid>? ownerID)
            where TFileHandler : IFileHandler
        {
            IFileSystemResolver fileSystemResolver = FileHandlerFactoryLocator.FileSystemResolver;
            IFileHandlerFactory fileHandlerFactory = fileSystemResolver.GetFactoryForFileType(fileType);

            if (!(fileHandlerFactory is ISystemFileHandlerFactory))
                throw new SecurityException(fileType + " is not a System file");

            ISystemFileHandlerFactory systemFileHandlerFactory = (ISystemFileHandlerFactory)fileHandlerFactory;

            IFileHandler toReturn = CreateFileHelper(
                filename, fileType, ownerID, new CreateFileDelegate(systemFileHandlerFactory.CreateSystemFile));

            if (toReturn is TFileHandler)
                return (TFileHandler)toReturn;

            throw new SystemFileException(
                filename + " was supposed to be a "
                + typeof(TFileHandler).ToString()
                + ", but the created type was a " + toReturn.GetType().ToString());

            // TODO:  Delete file
        }

        // Ensures that only one file can be created at a time, thus preventing an unlikely collision of file IDs
        private static object CreateFileKey = new object();

        private IFileHandler CreateFileHelper(
            string filename, string fileType, ID<IUserOrGroup, Guid>? ownerId, CreateFileDelegate createFileDelegate)
        {
			FileHandlerFactoryLocator.FileSystemResolver.VerifyNoForbiddenChars(filename);

            DateTime created = DateTime.UtcNow;

            FileId fileId = default(FileId);

            int ctr = 0;

            using (TimedLock.Lock(CreateFileKey))
            {
                do
                {
                    fileId = new FileId(SRandom.Next<long>());

                    ctr++;

                    if (ctr > 500)
                        throw new CanNotCreateFile("Tried too many times to create a file");

                } while (FileHandlerFactoryLocator.FileSystem.IsFilePresent(fileId));

            }

            try
            {
                DatabaseConnection.CallOnTransaction(delegate(IDatabaseTransaction transaction)
                {
                    IFile_Readable existingFile = DatabaseConnection.File.SelectSingle(File_Table.Name == filename);

                    if (null != existingFile)
                        throw new DuplicateFile(filename);

                    // Insert database record for file
                    DatabaseConnection.File.Insert(delegate(IFile_Writable file)
                    {
                        file.Name = filename;
                        file.FileId = fileId;
                        file.TypeId = fileType;
                        file.OwnerId = ownerId;
                        file.Created = created;
                    });

                    // Create the file within the transaction.  This way, if there's an exception, the transaction
                    // is rolled back
                    createFileDelegate(fileId);

                    transaction.Commit();

                    OwnerIdCache[fileId] = ownerId;
                });
            }
            catch (DiskException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new CanNotCreateFile("Database exception when creating " + filename, e);
            }

            IFileHandler toReturn = FileHandlerFactoryLocator.FileSystemResolver.LoadFile(fileId, fileType);
            toReturn.FileContainer = new FileContainer(fileId, fileType, filename, this, FileHandlerFactoryLocator, created);

            IUser changer = null;
            if (null != ownerId)
                changer = FileHandlerFactoryLocator.UserManagerHandler.GetUserNoException(ownerId.Value);

            // TODO:  Some change data would be cool
            SendNotification(changer, filename + " created", null);

            // Send the owner a notification so that the object shows up in the notification window
            if (null != ownerId)
            {
                Dictionary<string, object> changeData = new Dictionary<string, object>();
                changeData["action"] = "created";
                string actionData = JsonFx.Json.JsonWriter.Serialize(changeData);
                toReturn.SendNotification(changer, actionData, null);
            }

            OnDirectoryChanged();

            return toReturn;
        }

        /// <summary>
        /// Called when the cache is missing a file
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private IFileContainer CreateForCache(string key)
        {
            IFile_Readable file = DatabaseConnection.File.SelectSingle(File_Table.Name == key);

            if (null == file)
                throw new FileDoesNotExist(FileContainer.FullPath + "/" + key);

            IFileContainer toReturn = new FileContainer(new FileId(file.FileId.Value), file.TypeId, key, this, FileHandlerFactoryLocator, file.Created);
            OwnerIdCache[toReturn.FileId] = file.OwnerId;

            return toReturn;
        }


        public IFileContainer OpenFile(string filename)
        {
            string[] splitAtDirs = filename.Split(new char[] {'/'}, 2);

            // If no directory seperators are passed, just return the file
            if (splitAtDirs.Length == 1)
                return FileHandlerCache[filename];

            // A file in a sub directory was requested
            IFileContainer subdirContainer = FileHandlerCache[splitAtDirs[0]];

            return subdirContainer.CastFileHandler<IDirectoryHandler>().OpenFile(splitAtDirs[1]);
        }

        public IEnumerable<IFileContainer> Files
        {
            get
            {
                foreach (IFile_Readable file in DatabaseConnection.File.Select())
                {
                    IFileContainer toYield = new FileContainer(new FileId(file.FileId.Value), file.TypeId, file.Name, this, FileHandlerFactoryLocator, file.Created);
                    OwnerIdCache[toYield.FileId] = file.OwnerId;
                    yield return toYield;
                }
            }
        }

        public IEnumerable<IFileContainer> GetNewestFiles(ID<IUserOrGroup, Guid> userId, long maxToReturn)
        {
            List<ID<IUserOrGroup, Guid>> ids = new List<ID<IUserOrGroup, Guid>>();
            ids.Add(userId);
            ids.AddRange(FileHandlerFactoryLocator.UserManagerHandler.GetGroupIdsThatUserIsIn(userId));
            ids.Add(FileHandlerFactoryLocator.UserFactory.Everybody.Id);

            IUserOrGroup user = FileHandlerFactoryLocator.UserManagerHandler.GetUserNoException(userId);
            if (null != user)
            {
                if (user != FileHandlerFactoryLocator.UserFactory.AnonymousUser)
                {
                    ids.Add(FileHandlerFactoryLocator.UserFactory.AuthenticatedUsers.Id);

                    if (!user.Name.StartsWith("http://"))
                        ids.Add(FileHandlerFactoryLocator.UserFactory.LocalUsers.Id);
                }
            }
  
            foreach (IFile_Readable file in DatabaseConnection.File.GetNewestFiles(
                userId,
                ids,
                maxToReturn))
            {
                IFileContainer toYield = new FileContainer(new FileId(file.FileId.Value), file.TypeId, file.Name, this, FileHandlerFactoryLocator, file.Created);
                OwnerIdCache[toYield.FileId] = file.OwnerId;
                yield return toYield;
            }
        }

        /// <summary>
        /// Cache of file IDs by their name
        /// </summary>
        private Cache<string, Wrapped<FileId>> FileIDCacheByName;

        /// <summary>
        /// Returns the file ID for the given file
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        private Wrapped<FileId> GetFileIdForCache(string filename)
        {
            IFile_Readable file = DatabaseConnection.File.SelectSingle(File_Table.Name == filename);

            if (null == file)
                throw new FileDoesNotExist(FileContainer.FullPath + "/" + filename);

            FileId fileId = new FileId(file.FileId.Value);

            OwnerIdCache[fileId] = file.OwnerId;

            return new Wrapped<FileId>(fileId);
        }

        public void SetPermission(ID<IUserOrGroup, Guid>? assigningPermission, string filename, ID<IUserOrGroup, Guid> userOrGroupId, FilePermissionEnum level, bool inherit, bool sendNotifications)
        {
            bool updated = false;

            DatabaseConnection.CallOnTransaction(delegate(IDatabaseTransaction transaction)
            {
                updated = SetPermissionOnTransaction(assigningPermission, filename, userOrGroupId, level, inherit, sendNotifications, transaction);
                transaction.Commit();
            });

            // If notifications are enabled, then send a notification informing the user of the change
            if (sendNotifications)
            {
                IFileContainer targetFile = OpenFile(filename);

                if (null == assigningPermission)
                    assigningPermission = targetFile.OwnerId;

                if (null != assigningPermission)
                {
                    IUser from = FileHandlerFactoryLocator.UserManagerHandler.GetUser(assigningPermission.Value);

                    IUserOrGroup targetUserOrGroup = FileHandlerFactoryLocator.UserManagerHandler.GetUserOrGroup(userOrGroupId);
                    IEnumerable<IUser> targetUsers = FileHandlerFactoryLocator.UserManagerHandler.GetUsersAndResolveGroupsToUsers(new ID<IUserOrGroup, Guid>[] { userOrGroupId });

                    string messageSummary;
                    if (updated)
                        messageSummary = "Permission updated: " + level.ToString() + " for " + targetUserOrGroup.Name;
                    else
                        messageSummary = "Permission granted: " + level.ToString() + " for " + targetUserOrGroup.Name;

                    targetFile.FileHandler.SendNotification(from, targetUsers, messageSummary, null);
                }
            }

            OnDirectoryChanged();
        }

        /// <summary>
        /// Helper for setting a permission once a transaction is entered
        /// </summary>
        /// <param name="assigningPermission"></param>
        /// <param name="filename"></param>
        /// <param name="userOrGroupId"></param>
        /// <param name="level"></param>
        /// <param name="inherit"></param>
        /// <param name="transaction"></param>
        private bool SetPermissionOnTransaction(ID<IUserOrGroup, Guid>? assigningPermission, string filename, ID<IUserOrGroup, Guid> userOrGroupId, FilePermissionEnum level, bool inherit, bool sendNotifications, IDatabaseTransaction transaction)
        {
            bool updated = false;

            FileId fileId = FileIDCacheByName[filename].Value;

            // If there is already a permission in the DB, update it, else, create a new entry

            IPermission_Readable permission = DatabaseConnection.Permission.SelectSingle(
                Permission_Table.FileId == fileId & Permission_Table.UserOrGroupId == userOrGroupId);

            if (null == permission)
            {
                updated = true;

                DatabaseConnection.Permission.Insert(delegate(IPermission_Writable newPermission)
                {
                    newPermission.FileId = fileId;
                    newPermission.Level = level;
                    newPermission.UserOrGroupId = userOrGroupId;
                    newPermission.Inherit = inherit;
                    newPermission.SendNotifications = sendNotifications;
                });
            }
            else
            {
                DatabaseConnection.Permission.Update(
                    Permission_Table.FileId == fileId & Permission_Table.UserOrGroupId == userOrGroupId,
                    delegate(IPermission_Writable newPermission)
                    {
                        newPermission.Level = level;
                        newPermission.Inherit = inherit;
                        newPermission.SendNotifications = sendNotifications;
                    });
            }

            PermissionsCacheWithInherit.Clear();
            PermissionsCacheWithoutInherit.Clear();

            return updated;
        }

        public void RemovePermission(string filename, ObjectCloud.Common.ID<IUserOrGroup, Guid> userId)
        {
            DatabaseConnection.CallOnTransaction(delegate(IDatabaseTransaction transaction)
            {
                IFileId fileId = FileIDCacheByName[filename].Value;

                DatabaseConnection.Permission.Delete(Permission_Table.FileId == fileId & Permission_Table.UserOrGroupId == userId);

                transaction.Commit();
            });

            PermissionsCacheWithInherit.Clear();
            PermissionsCacheWithoutInherit.Clear();

            OnDirectoryChanged();
        }

        public IEnumerable<FilePermission> GetPermissions(string filename)
        {
            IFileId fileId = FileIDCacheByName[filename].Value;

            foreach (IPermission_Readable permission in
                new List<IPermission_Readable>(DatabaseConnection.Permission.Select(Permission_Table.FileId == fileId)))
            {
                FilePermission toYield = new FilePermission();
                toYield.UserOrGroupId = permission.UserOrGroupId;
                toYield.FilePermissionEnum = permission.Level;
                toYield.Inherit = permission.Inherit;
                toYield.SendNotifications = permission.SendNotifications;
				
				Dictionary<string, bool> namedPermissions = new Dictionary<string, bool>();
				foreach(INamedPermission_Readable namedPermission in DatabaseConnection.NamedPermission.Select(NamedPermission_Table.FileId == fileId & NamedPermission_Table.UserOrGroup == permission.UserOrGroupId))
					namedPermissions[namedPermission.NamedPermission] = namedPermission.Inherit;
				
				toYield.NamedPermissions = namedPermissions;

                yield return toYield;
            }
        }

        public void DeleteFile(IUser changer, string filename)
        {
            IFileContainer fileContainer = this.OpenFile(filename);

            try
            {
                fileContainer.FileHandler.OnDelete(changer);

                try
                {
                    if (fileContainer.FileHandler is IDisposable)
                        ((IDisposable)fileContainer.FileHandler).Dispose();
                }
                catch (Exception e)
                {
                    log.Error("Error occured while disposing " + fileContainer.FullPath + " during deletion", e);
                }
            }
            catch (Exception e)
            {
                log.Error("Error occured while " + fileContainer.FullPath + " that it's being deleted", e);
            }

            DatabaseConnection.CallOnTransaction(delegate(IDatabaseTransaction transaction)
            {
                IFile_Readable toDelete = DatabaseConnection.File.SelectSingle(File_Table.Name == filename);

                if (null == toDelete)
                    throw new FileDoesNotExist(FileContainer.FullPath + "/" + filename);

                DatabaseConnection.Relationships.Delete(
                    Relationships_Table.FileId == toDelete.FileId | Relationships_Table.ReferencedFileId == toDelete.FileId);

                DatabaseConnection.Permission.Delete(Permission_Table.FileId == toDelete.FileId);
                DatabaseConnection.File.Delete(File_Table.FileId == toDelete.FileId);

                FileId fileId = toDelete.FileId;

                FileHandlerFactoryLocator.FileSystemResolver.DeleteFile(fileId);

                OwnerIdCache.Remove(fileId);

                transaction.Commit();
            });

            FileIDCacheByName.Remove(filename);
            PermissionsCacheWithInherit.Clear();
            PermissionsCacheWithoutInherit.Clear();

            // TODO:  changeData would be cool
            SendNotification(changer, filename + " deleted", null);

            OnDirectoryChanged();
        }

        public IEnumerable<string> GetFilenames()
        {
            foreach (IFile_Readable file in DatabaseConnection.File.Select())
                yield return file.Name;
        }

        /// <summary>
        /// Returns all of the user and group IDs that apply to a user
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        protected IEnumerable<ID<IUserOrGroup, Guid>> GetAllUserAndGroupIdsThatApplyToUser(ID<IUserOrGroup, Guid> userId)
        {
            List<ID<IUserOrGroup, Guid>> userAndGroupsIds = new List<ID<IUserOrGroup, Guid>>();
            userAndGroupsIds.Add(userId);

            IEnumerable<ID<IUserOrGroup, Guid>> groupIds = FileHandlerFactoryLocator.UserManagerHandler.GetGroupIdsThatUserIsIn(userId);
            userAndGroupsIds.AddRange(groupIds);

            // Make sure that permissions that apply to everybody are present
            ID<IUserOrGroup, Guid> everybodyId = FileHandlerFactoryLocator.UserFactory.Everybody.Id;
            if (!userAndGroupsIds.Contains(everybodyId))
                userAndGroupsIds.Add(everybodyId);

            // If the user is authenticated, make sure that authenticated permissions apply
            if (userId != FileHandlerFactoryLocator.UserFactory.AnonymousUser.Id)
            {
                ID<IUserOrGroup, Guid> authenticatedId = FileHandlerFactoryLocator.UserFactory.AuthenticatedUsers.Id;
                if (!userAndGroupsIds.Contains(authenticatedId))
                    userAndGroupsIds.Add(authenticatedId);

                // If the user is local, make sure that the local permissions apply
                try
                {
                    IUser userOrGroup = FileHandlerFactoryLocator.UserManagerHandler.GetUser(userId);

                    if (FileHandlerFactoryLocator.FileSystemResolver.IsFilePresent("/Users/" + userOrGroup.Name + ".user"))
                    {
                        ID<IUserOrGroup, Guid> localId = FileHandlerFactoryLocator.UserFactory.LocalUsers.Id;
                        if (!userAndGroupsIds.Contains(localId))
                            userAndGroupsIds.Add(localId);
                    }
                }
                catch (UnknownUser)
                {
                    // Unknown users are swallowed, for now
                }
            }

            return userAndGroupsIds;
        }

        /// <summary>
        /// Loads the user's permission for the file
        /// </summary>
        /// <returns>The user's permission for the file, or null if the user has no access to the file</returns>
        public virtual FilePermissionEnum? LoadPermission(string filename, ID<IUserOrGroup, Guid> userId)
        {
            IEnumerable<ID<IUserOrGroup, Guid>> userAndGroupsIds = GetAllUserAndGroupIdsThatApplyToUser(userId);

            return LoadPermission(filename, userAndGroupsIds, false);
        }

        /// <summary>
        /// Cache of permissions where inherit is allowed
        /// </summary>
        private Cache<string, Wrapped<FilePermissionEnum?>, LoadPermissionArgs> PermissionsCacheWithInherit;

        /// <summary>
        /// Cache of permissions where inherit is forbidden
        /// </summary>
        private Cache<string, Wrapped<FilePermissionEnum?>, LoadPermissionArgs> PermissionsCacheWithoutInherit;

        /// <summary>
        /// Args for determining what a user's permission for a file is
        /// </summary>
        private struct LoadPermissionArgs
        {
            internal IFileId FileId;
            internal IEnumerable<ID<IUserOrGroup, Guid>> UserAndGroupIds;
            internal bool OnlyReturnInheritedPermissions;
        }

        /// <summary>
        /// Loads a permission for use in a permissions cache
        /// </summary>
        /// <param name="key"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        private Wrapped<FilePermissionEnum?> LoadPermissionForCache(string key, LoadPermissionArgs args)
        {
            FilePermissionEnum? highestPermission = null;
            foreach (IPermission_Readable permission in DatabaseConnection.Permission.Select(
                Permission_Table.FileId == args.FileId & Permission_Table.UserOrGroupId.In(args.UserAndGroupIds)))
            {
                if (!args.OnlyReturnInheritedPermissions | permission.Inherit)
                {
                    if (null == highestPermission)
                        highestPermission = permission.Level;
                    else if (permission.Level > highestPermission.Value)
                        highestPermission = permission.Level;
                }
            }

            if (null != highestPermission)
                return new Wrapped<FilePermissionEnum?>(highestPermission);
            else if (FileContainer == FileHandlerFactoryLocator.FileSystemResolver.RootDirectoryHandler.FileContainer)
                // No permission found
                return new Wrapped<FilePermissionEnum?>(null);
            else
                return new Wrapped<FilePermissionEnum?>(
                    LoadPermissionFromRelated(new IFileId[] { args.FileId }, args.UserAndGroupIds, new Set<IFileId>(), 0));
        }

        /// <summary>
        /// Returns a string that uniquely identifies the user and group IDs with the fileId.  Used for caching
        /// </summary>
        /// <param name="userAndGroupIds"></param>
        /// <returns></returns>
        private string ConvertUserOrGroupIdsToString(IFileId fileId, IEnumerable<ID<IUserOrGroup, Guid>> userAndGroupIds)
        {
            List<string> idsAsString = new List<string>();

            foreach (ID<IUserOrGroup, Guid> userOrGroupId in userAndGroupIds)
                idsAsString.Add(userOrGroupId.ToString());

            idsAsString.Sort();

            StringBuilder toReturn = new StringBuilder(fileId.ToString());
            toReturn.Append("_");
            foreach (string idAsString in idsAsString)
                toReturn.Append(idAsString);

            return toReturn.ToString();
        }

        public FilePermissionEnum? LoadPermission(string filename, IEnumerable<ID<IUserOrGroup, Guid>> userAndGroupIds, bool onlyReturnInheritedPermissions)
        {
            IFileId fileId = FileIDCacheByName[filename].Value;
            string cacheKey = ConvertUserOrGroupIdsToString(fileId, userAndGroupIds);

            LoadPermissionArgs args = new LoadPermissionArgs();
            args.FileId = fileId;
            args.UserAndGroupIds = userAndGroupIds;
            args.OnlyReturnInheritedPermissions = onlyReturnInheritedPermissions;

            FilePermissionEnum? toReturn;
            if (onlyReturnInheritedPermissions)
                toReturn = PermissionsCacheWithInherit.Get(cacheKey, args).Value;
            else
                toReturn = PermissionsCacheWithoutInherit.Get(cacheKey, args).Value;

            if (null != toReturn)
                return toReturn;
            else if (FileContainer == FileHandlerFactoryLocator.FileSystemResolver.RootDirectoryHandler.FileContainer)
                // No permission found
                return null;
            else
                return FileContainer.ParentDirectoryHandler.LoadPermission(FileContainer.Filename, userAndGroupIds, true);
        }

        /// <summary>
        /// Helper function to load permissions from files that are related
        /// </summary>
        /// <param name="fileIds"></param>
        /// <param name="userAndGroupIds"></param>
        /// <returns></returns>
        private FilePermissionEnum? LoadPermissionFromRelated(
            IEnumerable<IFileId> fileIds,
            IEnumerable<ID<IUserOrGroup, Guid>> userAndGroupIds,
            Set<IFileId> alreadyChecked,
            uint recurse)
        {
            FilePermissionEnum? highestPermission = null;
            List<IFileId> parentIds = new List<IFileId>();

            // Load all of the parent related files
            foreach (IRelationships_Readable relationship in DatabaseConnection.Relationships.Select(Relationships_Table.ReferencedFileId.In(fileIds)))
            {
                FileId fileId = relationship.FileId;

                if (!alreadyChecked.Contains(fileId))
                {
                    parentIds.Add(fileId);
                    alreadyChecked.Add(fileId);
                }
            }

            // If there are no parent relationships, just return null
            if (0 == parentIds.Count)
                return null;

            // Scan the permissions of the parent related files
            foreach (IPermission_Readable permission in DatabaseConnection.Permission.Select(
                Permission_Table.FileId.In(parentIds) & Permission_Table.UserOrGroupId.In(userAndGroupIds) & (Permission_Table.Inherit == true)))
            {
                if (null == highestPermission)
                    highestPermission = permission.Level;
                else if (permission.Level > highestPermission.Value)
                    highestPermission = permission.Level;
            }

            if (null != highestPermission)
                return highestPermission;

            // Prevent infinate recursion
            if (recurse >= 150)
                return null;

            return LoadPermissionFromRelated(parentIds, userAndGroupIds, alreadyChecked, recurse + 1);
        }

        /// <summary>
        /// Returns true if the file is present, false otherwise
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public bool IsFilePresent(string filename)
        {
            IFile_Readable file = DatabaseConnection.File.SelectSingle(File_Table.Name == filename);

            return null != file;
        }

        /// <summary>
        /// Dumps the directory
        /// </summary>
        /// <param name="xmlWriter"></param>
        public override void Dump(string path, ID<IUserOrGroup, Guid> userId)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            List<IFile_Readable> files;

            Dictionary<FileId, List<IPermission_Readable>> permissionsByFileId =
                new Dictionary<FileId, List<IPermission_Readable>>();

            Dictionary<FileId, IFileContainer> fileContainersById = new Dictionary<FileId, IFileContainer>();

            // Load all of the files into memory.  This is because as the files are iterated, additional queries will run
            files = new List<IFile_Readable>(DatabaseConnection.File.Select());

            // Load all of the permissions that apply to system users and groups into memory
            foreach (IPermission_Readable permission in DatabaseConnection.Permission.Select())
                if (FileHandlerFactoryLocator.UserFactory.IsSystemUserOrGroup(permission.UserOrGroupId))
                {
                    if (!permissionsByFileId.ContainsKey(permission.FileId))
                        permissionsByFileId[permission.FileId] = new List<IPermission_Readable>();

                    permissionsByFileId[permission.FileId].Add(permission);
                }

            // Write out each file
            foreach (IFile_Readable file in files)
            {
                IFileContainer fileContainer = OpenFile(file.Name);

                using (TimedLock.Lock(fileContainer.FileHandler))
                {
                    fileContainer.FileHandler.Dump(
                        path + Path.DirectorySeparatorChar + file.Name, userId);
                }

                fileContainersById[file.FileId] = fileContainer;
            }

            string metadataFile = path + Path.DirectorySeparatorChar + "metadata.xml";

            DateTime destinationCreated = DateTime.MinValue;

            if (File.Exists(metadataFile))
                destinationCreated = File.GetLastWriteTimeUtc(metadataFile);

            if (destinationCreated < LastModified)
            {
                XmlWriterSettings settings = new XmlWriterSettings();
                settings.Indent = true;
                settings.IndentChars = "\t";
                settings.NewLineChars = "\n";
                settings.NewLineHandling = NewLineHandling.Entitize;
                settings.NewLineOnAttributes = true;

                // Write metadata to XML
                using (XmlWriter xmlWriter = XmlWriter.Create(metadataFile, settings))
                {
                    xmlWriter.WriteStartDocument();

                    xmlWriter.WriteStartElement("Directory");

                    string indexFile = IndexFile;
                    if (null != indexFile)
                        xmlWriter.WriteAttributeString("IndexFile", indexFile);

                    // make sure the files are always written in the same order.  This assists with diffing changes
                    files.Sort(delegate(IFile_Readable a, IFile_Readable b)
                    {
                        return a.Name.CompareTo(b.Name);
                    });

                    foreach (IFile_Readable file in files)
                    {
                        IFileContainer fileContainer = fileContainersById[file.FileId];

                        // Load user's permission for file
                        FilePermissionEnum? filePermissionEnum = fileContainer.LoadPermission(userId);

                        if (null != filePermissionEnum)
                            if (filePermissionEnum.Value >= FilePermissionEnum.Read)
                            {
                                // <FileInDirectory>
                                xmlWriter.WriteStartElement("File");

                                // Write the file's attributes
                                xmlWriter.WriteAttributeString("Name", file.Name);
                                xmlWriter.WriteAttributeString("TypeId", file.TypeId);

                                // Only write the ownerId if it's a built-in user.  This might change if Dump is set up to do true backups, but right now
                                // the files aren't supposed to be tied to specific users
                                ID<IUserOrGroup, Guid>? ownerId = null;
                                if (null != file.OwnerId)
                                {
                                    IUser owner = FileHandlerFactoryLocator.UserManagerHandler.GetUserNoException(file.OwnerId.Value);

                                    if (null != owner)
                                        if (owner.BuiltIn)
                                            ownerId = owner.Id;
                                }

                                if (null != ownerId)
                                    xmlWriter.WriteAttributeString("OwnerId", ownerId.Value.ToString());

                                // Write each permission if it applies to a built-in user or group
                                if (permissionsByFileId.ContainsKey(file.FileId))
                                    foreach (IPermission_Readable permission in permissionsByFileId[file.FileId])
                                    {
                                        try
                                        {
                                            IUserOrGroup userOrGroup = FileHandlerFactoryLocator.UserManagerHandler.GetUserOrGroup(permission.UserOrGroupId);

                                            if (userOrGroup.BuiltIn)
                                            {
                                                xmlWriter.WriteStartElement("Permission");

                                                xmlWriter.WriteAttributeString("UserOrGroupId", permission.UserOrGroupId.Value.ToString());
                                                xmlWriter.WriteAttributeString("Level", permission.Level.ToString());
                                                xmlWriter.WriteAttributeString("Inherit", permission.Inherit.ToString());
                                                xmlWriter.WriteAttributeString("SendNotifications", permission.SendNotifications.ToString());

                                                xmlWriter.WriteEndElement();
                                            }
                                        }
                                        // For now, swallow userIds that aren't a valid user
                                        catch (UnknownUser) { }
                                    }

                                // </FileInDirectory>
                                xmlWriter.WriteEndElement();
                            }
                    }

                    // </Directory>
                    xmlWriter.WriteEndElement();

                    xmlWriter.WriteEndDocument();

                    xmlWriter.Flush();
                    xmlWriter.Close();
                }

                log.Info("Successfully wrote " + FileContainer.FullPath);
            }
        }

        public override void OnDelete(IUser changer)
        {
            foreach (IFileContainer fileContainer in new List<IFileContainer>(Files))
                DeleteFile(changer, fileContainer.Filename);

            base.OnDelete(changer);
        }

        public void Rename(IUser changer, string oldFilename, string newFilename)
        {
            DatabaseConnection.CallOnTransaction(delegate(IDatabaseTransaction transaction)
            {
                if (null == DatabaseConnection.File.SelectSingle(File_Table.Name == oldFilename))
                    throw new FileDoesNotExist(FileContainer.FullPath + "/" + oldFilename);

                if (null != DatabaseConnection.File.SelectSingle(File_Table.Name == newFilename))
                    throw new DuplicateFile(FileContainer.FullPath + "/" + newFilename);

                DatabaseConnection.File.Update(
                    File_Table.Name == oldFilename,
                    delegate(IFile_Writable file)
                    {
                        file.Name = newFilename;
                    });

                transaction.Commit();
            });

            FileIDCacheByName.Remove(oldFilename);
			FileHandlerCache.Remove(oldFilename);

            IFileContainer newFileContainer = FileHandlerCache[newFilename];
            newFileContainer.WebHandler.FileContainer = newFileContainer;
            newFileContainer.WebHandler.ResetExecutionEnvironment();

            // TODO:  changeData would be cool
            SendNotification(changer, oldFilename + " renamed to " + newFilename, null);

            OnDirectoryChanged();
        }

        public void CopyFile(IUser changer, IFileContainer toCopy, string newFileName, ID<IUserOrGroup, Guid>? ownerID)
        {
			FileHandlerFactoryLocator.FileSystemResolver.VerifyNoForbiddenChars(newFileName);

			FilePermissionEnum? permission;

            if (null != ownerID)
                permission = toCopy.LoadPermission(ownerID.Value);
            else
                permission = FilePermissionEnum.Administer;

            if (null != permission)
                if (permission.Value >= FilePermissionEnum.Read)
                {
                    IFileHandlerFactory fileHandlerFactory = FileHandlerFactoryLocator.FileSystemResolver.GetFactoryForFileType(toCopy.TypeId);

                    this.CreateFileHelper(newFileName, toCopy.TypeId, ownerID, delegate(IFileId fileId)
                    {
                        fileHandlerFactory.CopyFile(toCopy.FileHandler, fileId, ownerID, this);
                    });

                    // TODO:  changeData would be cool
                    SendNotification(changer, toCopy.FullPath + "/" + toCopy.Filename + " copied to " + Filename, null);

                    OnDirectoryChanged();

                    return;
                }

            throw new SecurityException("Permission denied");
        }

        public string IndexFile
        {
            get
            {
                IMetadata_Readable meta = DatabaseConnection.Metadata.SelectSingle(Metadata_Table.Name == "IndexFile");

                if (null != meta)
                    return meta.Value;

                return null;
            }
            set
            {
                DatabaseConnection.CallOnTransaction(delegate(IDatabaseTransaction transaction)
                {
                    DatabaseConnection.Metadata.Delete(Metadata_Table.Name == "IndexFile");

                    if (null != value)
                        DatabaseConnection.Metadata.Insert(delegate(IMetadata_Writable meta)
                        {
                            meta.Name = "IndexFile";
                            meta.Value = value;
                        });

                    transaction.Commit();
                });

                OnDirectoryChanged();
            }
        }

        public override void SyncFromLocalDisk(string localDiskPath, bool force)
        {
			log.Trace("Syncing " + FileContainer.FullPath);
			
            string metadataPath = Path.GetFullPath(localDiskPath + Path.DirectorySeparatorChar + "metadata.xml");

            if (File.Exists(metadataPath))
                using (TextReader tr = File.OpenText(metadataPath))
                using (XmlReader xmlReader = XmlReader.Create(tr))
                {
                    xmlReader.MoveToContent();

                    IndexFile = xmlReader.GetAttribute("IndexFile");

                    // Note:  Permission tags are contained within File tags
                    // The nature of the XmlReader makes it such that trying to read sub-tags gets tripped up
                    // when reading <File ... /> tags, as trying to read the next tag to look for permissions
                    // ends up skipping the next file tag.
                    string filename = null;

                    while (xmlReader.Read())
                        if (xmlReader.NodeType == XmlNodeType.Element)
                            if ("File".Equals(xmlReader.Name))
                            {
                                string typeId = xmlReader.GetAttribute("TypeId");
                                filename = xmlReader.GetAttribute("Name");

                                ID<IUserOrGroup, Guid>? ownerId = null;
                                string ownerIdString = xmlReader.GetAttribute("OwnerId");

                                if (null != ownerIdString)
                                    ownerId = new ID<IUserOrGroup, Guid>(new Guid(ownerIdString));
                                else
                                    ownerId = FileContainer.OwnerId;

                                if (IsFilePresent(filename))
                                {
                                    // If the file is already present, just update it
									string fileToSync = localDiskPath + Path.DirectorySeparatorChar + filename;
									fileToSync = Path.GetFullPath(fileToSync);
							
									IFileContainer toSync = OpenFile(filename);
							
									log.Trace("Jumping into " + toSync.FullPath);

                                    try
                                    {
                                        toSync.FileHandler.SyncFromLocalDisk(fileToSync, force);
                                    }
                                    catch (Exception e)
                                    {
                                        log.Error("Error syncing " + toSync.FullPath, e);
                                        throw;
                                    }

                                    DatabaseConnection.File.Update((File_Table.Name == filename) & (File_Table.OwnerId != ownerId),
                                        delegate(IFile_Writable file)
                                        {
                                            file.OwnerId = ownerId;
                                        });
                                }
                                else
                                    RestoreFile(
                                        filename, typeId, localDiskPath + Path.DirectorySeparatorChar + filename, ownerId.Value);
                            }
                            else if ("Permission".Equals(xmlReader.Name))
                            {
                                string userOrGroupIdString = xmlReader.GetAttribute("UserOrGroupId");
                                string levelString = xmlReader.GetAttribute("Level");
                                string inheritString = xmlReader.GetAttribute("Inherit");
                                string sendNotificationsString = xmlReader.GetAttribute("SendNotifications");

                                ID<IUserOrGroup, Guid> userOrGroupId = new ID<IUserOrGroup, Guid>(new Guid(userOrGroupIdString));
                                FilePermissionEnum level = Enum<FilePermissionEnum>.Parse(levelString);

                                bool inherit = false;
                                bool.TryParse(inheritString, out inherit);

                                bool sendNotifications = false;
                                bool.TryParse(sendNotificationsString, out sendNotifications);

                                SetPermission(null, filename, userOrGroupId, level, inherit, sendNotifications);
                            }
                }

            // Delete old files
            string deleteListPath = Path.GetFullPath(localDiskPath + Path.DirectorySeparatorChar + "____deletelist.txt");

            if (File.Exists(deleteListPath))
                foreach (string toDelete in File.ReadAllLines(deleteListPath))
                    if (IsFilePresent(toDelete))
                        DeleteFile(null, toDelete);
        }

        public override string Title
        {
            get
            {
                return FileContainer.FullPath;
            }
        }

        public override void Vacuum()
        {
            foreach (IFileContainer fileContainer in Files)
                ThreadPool.QueueUserWorkItem(delegate(object fileContainerObj)
                {
                    ((IFileContainer)fileContainerObj).FileHandler.Vacuum();
                }, fileContainer);

            base.Vacuum();
        }

        /// <summary>
        /// Occurs whenever the directory changes
        /// </summary>
        public event EventHandler<IDirectoryHandler, EventArgs> DirectoryChanged;

        /// <summary>
        /// Calls DirectoryChanged
        /// </summary>
        protected void OnDirectoryChanged()
        {
            if (null != DirectoryChanged)
                DirectoryChanged(this, new EventArgs());
        }

        public virtual IEnumerable<IFileContainer> GetRelatedFiles(
            ID<IUserOrGroup, Guid> userId,
            IFileId parentFileId,
            IEnumerable<string> relationships,
            IEnumerable<string> extensions,
            DateTime? newest,
            DateTime? oldest,
            uint? maxToReturn)
        {
            Set<FileId> filesToInspect = new Set<FileId>();

            // First get the related files

			List<ComparisonCondition> comparisonConditions = new List<ComparisonCondition>();

            comparisonConditions.Add(Relationships_Table.FileId == parentFileId);

            if (null != relationships)
                comparisonConditions.Add(Relationships_Table.Relationship.In(relationships));

            foreach (IRelationships_Readable relationshipInDb in
                DatabaseConnection.Relationships.Select(ComparisonCondition.Condense(comparisonConditions)))
            {
                filesToInspect.Add(relationshipInDb.ReferencedFileId);
            }

            // Now filer these files by permission if the user didn't inherit permission to this folder, or didn't inherit permission through a direct
            // permission on the parent file
            bool inspectPermissions = true;

            if (null != FileContainer.ParentDirectoryHandler)
                if (null != FileContainer.ParentDirectoryHandler.LoadPermission(FileContainer.Filename, new ID<IUserOrGroup, Guid>[] { userId }, true))
                    inspectPermissions = false;

            if (FileHandlerFactoryLocator.UserFactory.RootUser.Id == userId || FileContainer.OwnerId == userId)
                inspectPermissions = false;

            IEnumerable<ID<IUserOrGroup, Guid>> userOrGroupIds = null;

            // Only check parent relationships for an inherited permission if we haven't already determined that the user has a permission
            if (inspectPermissions)
            {
                userOrGroupIds = GetAllUserAndGroupIdsThatApplyToUser(userId);

                if (null != LoadPermissionFromRelated(new IFileId[] { parentFileId }, userOrGroupIds, new Set<IFileId>(), 0))
                    inspectPermissions = false;
            }

            // Check to see if the user has named permissions with the relationship name, if so, then permissions do not need to be inspected
            /*if (inspectPermissions)
            {
                if (HasNamedPermissions(parentFileId, relationships, userId))
                    inspectPermissions = false;
            }*/

            if (inspectPermissions)
            {
                IEnumerable<IPermission_Readable> permissions = DatabaseConnection.Permission.Select(
                    Permission_Table.FileId.In(filesToInspect) & Permission_Table.UserOrGroupId.In(userOrGroupIds));

                filesToInspect = new Set<FileId>();

                foreach (IPermission_Readable permission in permissions)
                    filesToInspect.Add(permission.FileId);
            }

            comparisonConditions = new List<ComparisonCondition>();
            comparisonConditions.Add(File_Table.FileId.In(filesToInspect));

            if (null != extensions)
                comparisonConditions.Add(File_Table.Extension.In(extensions));

            if (null != newest)
                comparisonConditions.Add(File_Table.Created < newest.Value);

            if (null != oldest)
                comparisonConditions.Add(File_Table.Created > oldest.Value);

            foreach (IFile_Readable file in DatabaseConnection.File.Select(
                ComparisonCondition.Condense(comparisonConditions),
                maxToReturn,
                ObjectCloud.ORM.DataAccess.OrderBy.Desc,
                File_Table.Created))
            {
                IFileContainer toYield = new FileContainer(new FileId(file.FileId.Value), file.TypeId, file.Name, this, FileHandlerFactoryLocator, file.Created);
                OwnerIdCache[toYield.FileId] = toYield.OwnerId;
                yield return toYield;
            }
        }

        public virtual void AddRelationship(IFileContainer parentFile, IFileContainer relatedFile, string relationship)
        {
            DatabaseConnection.CallOnTransaction(delegate(IDatabaseTransaction transaction)
            {
                if (null == DatabaseConnection.File.Select(File_Table.FileId == parentFile.FileId))
                    throw new DiskException("Parent file must be in the directory where the relationship exists");

                if (null == DatabaseConnection.File.Select(File_Table.FileId == relatedFile.FileId))
                    throw new DiskException("Related file must be in the directory where the relationship exists");

                try
                {
                    DatabaseConnection.Relationships.Insert(delegate(IRelationships_Writable relationshipInDb)
                    {
                        relationshipInDb.FileId = (FileId)(parentFile.FileId);
                        relationshipInDb.ReferencedFileId = (FileId)relatedFile.FileId;
                        relationshipInDb.Relationship = relationship;
                    });
                }
                catch (Exception e)
                {
                    transaction.Rollback();
                    throw new DiskException("Relationships in a directory must be unique.  Ensure that the relationship is unique", e);
                }

                transaction.Commit();
            });

            Dictionary<string, object> changeData = new Dictionary<string, object>();
            changeData["Add"] = true;
            changeData["Relationship"] = relationship;
            changeData["RelatedFile"] = relatedFile.ObjectUrl;

            parentFile.FileHandler.SendNotification(
                parentFile.Owner,
                "Related file added: " + relatedFile.FullPath + ", relationship: " + relationship,
                JsonWriter.Serialize(changeData));

            parentFile.FileHandler.OnRelationshipAdded(new RelationshipEventArgs(relatedFile, relationship));
        }

        public void DeleteRelationship(IFileContainer parentFile, IFileContainer relatedFile, string relationship)
        {
            DatabaseConnection.Relationships.Delete(
                Relationships_Table.FileId == parentFile.FileId & Relationships_Table.ReferencedFileId == relatedFile.FileId & Relationships_Table.Relationship == relationship);

            Dictionary<string, object> changeData = new Dictionary<string, object>();
            changeData["Delete"] = true;
            changeData["Relationship"] = relationship;
            changeData["RelatedFile"] = relatedFile.ObjectUrl;

            parentFile.FileHandler.SendNotification(
                parentFile.Owner,
                "Related file removed: " + relatedFile.FullPath + ", relationship: " + relationship,
                JsonWriter.Serialize(changeData));

            parentFile.FileHandler.OnRelationshipDeleted(new RelationshipEventArgs(relatedFile, relationship));
        }

        public void Chown(IUser changer, IFileId fileId, ID<IUserOrGroup, Guid>? newOwnerId)
        {
            IFile_Readable oldFile = DatabaseConnection.File.SelectSingle(File_Table.FileId == fileId);
			if (null == oldFile)
				throw new FileDoesNotExist("ID: " + fileId.ToString());

            // Verify that the new owner exists
            IUser newOwner = null;
            if (null != newOwnerId)
                newOwner = FileHandlerFactoryLocator.UserManagerHandler.GetUser(newOwnerId.Value);

            using (TimedLock.Lock(this))
            {
                DatabaseConnection.File.Update(
                    File_Table.FileId == fileId,
                    delegate(IFile_Writable file)
                    {
                        file.OwnerId = newOwnerId;
                    });

                OwnerIdCache[fileId] = newOwnerId;
            }

            if (null != newOwner)
                SendNotification(changer, "Owner Changed", "New owner: " + newOwner.Name);
            else
                SendNotification(changer, "Owner Changed", "");

            OnDirectoryChanged();
        }

        /// <summary>
        /// Cache of owner IDs
        /// </summary>
        private Cache<IFileId, Wrapped<ID<IUserOrGroup, Guid>?>> OwnerIdCache;

        public ID<IUserOrGroup, Guid>? GetOwnerId(IFileId fileId)
        {
            return OwnerIdCache[fileId].Value;
        }

        /// <summary>
        /// Called when the cache doesn't know the owner of a file
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private Wrapped<ID<IUserOrGroup, Guid>?> LoadOwnerIdForCache(IFileId fileId)
        {
            return DatabaseConnection.File.SelectSingle(File_Table.FileId == fileId).OwnerId;
        }

        public void SetNamedPermission(IFileId fileId, string namedPermission, ID<IUserOrGroup, Guid> userOrGroupId, bool inherit)
        {
            DatabaseConnection.CallOnTransaction(delegate(IDatabaseTransaction transaction)
            {
                INamedPermission_Readable np = DatabaseConnection.NamedPermission.SelectSingle(
                    NamedPermission_Table.FileId == fileId & NamedPermission_Table.NamedPermission == namedPermission & NamedPermission_Table.UserOrGroup == userOrGroupId);

                if (null == np)
                {
                    DatabaseConnection.NamedPermission.Insert(delegate(INamedPermission_Writable np_w)
                    {
                        np_w.FileId = (FileId)fileId;
                        np_w.Inherit = inherit;
                        np_w.NamedPermission = namedPermission;
                        np_w.UserOrGroup = userOrGroupId;
                    });
                }
                else
                {
                    DatabaseConnection.NamedPermission.Update(
                        NamedPermission_Table.FileId == fileId & NamedPermission_Table.NamedPermission == namedPermission & NamedPermission_Table.UserOrGroup == userOrGroupId,
                        delegate(INamedPermission_Writable np_w)
                        {
                            np_w.FileId = (FileId)fileId;
                            np_w.Inherit = inherit;
                            np_w.NamedPermission = namedPermission;
                            np_w.UserOrGroup = userOrGroupId;
                        });
                }

                transaction.Commit();
            });
        }

        public void RemoveNamedPermission(IFileId fileId, string namedPermission, ID<IUserOrGroup, Guid> userOrGroupId)
        {
            DatabaseConnection.NamedPermission.Delete(
                NamedPermission_Table.FileId == fileId & NamedPermission_Table.NamedPermission == namedPermission & NamedPermission_Table.UserOrGroup == userOrGroupId);
        }

        public bool HasNamedPermissions(IFileId fileId, IEnumerable<string> namedPermissions, ID<IUserOrGroup, Guid> userId)
        {
            IEnumerable<ID<IUserOrGroup, Guid>> userAndGroupsIds = GetAllUserAndGroupIdsThatApplyToUser(userId);
            return HasNamedPermissions(new IFileId[] { fileId }, namedPermissions, userAndGroupsIds, true);
        }

        public bool HasNamedPermissions(IEnumerable<IFileId> fileIds, IEnumerable<string> namedPermissions, IEnumerable<ID<IUserOrGroup, Guid>> userOrGroupIds, bool checkInherit)
        {
            return HasNamedPermissions(fileIds, namedPermissions, userOrGroupIds, checkInherit, 0);
        }

        private bool HasNamedPermissions(
            IEnumerable<IFileId> fileIds,
            IEnumerable<string> namedPermissions,
            IEnumerable<ID<IUserOrGroup, Guid>> userOrGroupIds,
            bool checkInherit,
            uint numCalls)
        {
            bool toReturn = false;

			using (IEnumerator<INamedPermission_Readable> np_enum = DatabaseConnection.NamedPermission.Select(
                NamedPermission_Table.FileId.In(fileIds) & NamedPermission_Table.NamedPermission.In(namedPermissions) & NamedPermission_Table.UserOrGroup.In(userOrGroupIds)).GetEnumerator())
            {
                toReturn = np_enum.MoveNext();
				
				// Clean out the rest of the iterator
				if (toReturn)
				{
					while (np_enum.MoveNext()) {}
	                return true;
				}
            }

            // permission not found, now follow references through any related files
            if (checkInherit)
                if (HasNamedPermissionThroughRelationship(fileIds, namedPermissions, userOrGroupIds, numCalls))
                    return true;

            if (checkInherit && null != FileContainer.ParentDirectoryHandler)
                return FileContainer.ParentDirectoryHandler.HasNamedPermissions(fileIds, namedPermissions, userOrGroupIds, checkInherit);

            return false;
        }

        /// <summary>
        /// Returns true if the user has the named permission in any parent relationships where the named permission is the relationship
        /// </summary>
        /// <param name="fileId"></param>
        /// <param name="namedPermission"></param>
        /// <param name="userOrGroupIds"></param>
        /// <returns></returns>
        private bool HasNamedPermissionThroughRelationship(
            IEnumerable<IFileId> fileIds, IEnumerable<string> namedPermissions, IEnumerable<ID<IUserOrGroup, Guid>> userOrGroupIds, uint numCalls)
        {
            List<IRelationships_Readable> relationships = new List<IRelationships_Readable>(
                DatabaseConnection.Relationships.Select(Relationships_Table.ReferencedFileId.In(fileIds) & Relationships_Table.Relationship.In(namedPermissions)));

            List<IFileId> parentFileIds = new List<IFileId>();

            foreach (IRelationships_Readable relationship in relationships)
                parentFileIds.Add(new FileId(relationship.FileId.Value));

            if (parentFileIds.Count > 0 && numCalls < 150) // prevent infinate recursion
                return HasNamedPermissions(parentFileIds, namedPermissions, userOrGroupIds, false, numCalls++);

            return false;
        }

        public IEnumerable<NamedPermission> GetNamedPermissions(IFileId fileId, string namedPermission)
        {
            foreach (INamedPermission_Readable np in DatabaseConnection.NamedPermission.Select(
                NamedPermission_Table.FileId == fileId & NamedPermission_Table.NamedPermission == namedPermission))
            {
                NamedPermission toYeild = new NamedPermission();
                toYeild.FileId = (FileId)np.FileId;
                toYeild.Inherit = np.Inherit;
                toYeild.Name = np.NamedPermission;
                toYeild.UserOrGroupId = np.UserOrGroup;

                yield return toYeild;
            }
        }
    }

    /// <summary>
    /// Delegate for methods that create an IFileHandler
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    internal delegate void CreateFileDelegate(IFileId fileId);
}