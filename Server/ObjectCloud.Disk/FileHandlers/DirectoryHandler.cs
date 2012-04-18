// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Xml;

using Common.Logging;
using JsonFx.Json;

using ObjectCloud.Common;
using ObjectCloud.Common.Threading;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Javascript;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.FileHandlers
{
    public class DirectoryHandler : LastModifiedFileHandler, IDirectoryHandler
    {
        private static ILog log = LogManager.GetLogger<DirectoryHandler>();
		
		/// <summary>
		/// Internally represents a file
		/// </summary>
		[Serializable]
		private class File
		{
			public FileId fileId;
			public string filename;
			public string typeId;
			public ID<IUserOrGroup, Guid>? ownerId;
			public DateTime created;
	        public Dictionary<ID<IUserOrGroup, Guid>, Permission> permissions = new Dictionary<ID<IUserOrGroup, Guid>, Permission>();
	        public Dictionary<ID<IUserOrGroup, Guid>, Dictionary<string, bool>> namedPermissions = new Dictionary<ID<IUserOrGroup, Guid>, Dictionary<string, bool>>();
			public Dictionary<FileId, Dictionary<string, bool>> relationships = new Dictionary<FileId, Dictionary<string, bool>>();
		}
		
		/// <summary>
		/// Stores files in the various different ways of indexing, and other directory data
		/// </summary>
		[Serializable]
		private class DirectoryInformation
		{
			public string indexFile;
			
			public readonly Dictionary<string, File> filesByName = new Dictionary<string, File>();
			public readonly Dictionary<FileId, File> filesById = new Dictionary<FileId, File>();
			
			/// <summary>
			/// Gets the <see cref="ObjectCloud.Disk.FileHandlers.DirectoryHandler.DirectoryInformation"/> with the specified name, throws an exception if it doesnt exist.
			/// </summary>
			/// <exception cref='FileDoesNotExist'>
			/// Is thrown when the file does not exist.
			/// </exception>
			public File this[string name]
			{
				get
		        {
					File file;
					if (filesByName.TryGetValue(name, out file))
						return file;

					throw new FileDoesNotExist(name);
		        }
			}
		}
		
	    /// <summary>
	    /// Details about a permission
	    /// </summary>
	    private class Permission
	    {
	        /// <summary>
	        /// The actual permission
	        /// </summary>
	        public FilePermissionEnum level;
	
	        /// <summary>
	        /// True if the permission is inherited
	        /// </summary>
	        public bool inherit;
	
	        /// <summary>
	        /// True to send notifications
	        /// </summary>
	        public bool sendNotifications;
	    }

        public DirectoryHandler(string path, FileHandlerFactoryLocator fileHandlerFactoryLocator)
            : base(fileHandlerFactoryLocator, path)
        {
			this.persistedDirectories = new PersistedObject<Dictionary<IFileId, DirectoryInformation>>(
				path, () => new Dictionary<IFileId, DirectoryInformation>());
        }
		
		/// <summary>
		/// All information about the entire file system is held in RAM and persisted to disk as a single object graph
		/// </summary>
		private PersistedObject<Dictionary<IFileId, DirectoryInformation>> persistedDirectories;
		
		/// <summary>
		/// Shortcut for reading
		/// </summary>
		private T Read<T>(Func<DirectoryInformation, T> func)
		{
			return this.persistedDirectories.Read<T>(
				directoryInformations => func(directoryInformations[this.FileContainer.FileId]));
		}
		
		/// <summary>
		/// Shortcut for Write
		/// </summary>
		private T Write<T>(Func<DirectoryInformation, T> func)
		{
			return this.persistedDirectories.Write<T>(
				directoryInformations => func(directoryInformations[this.FileContainer.FileId]));
		}
		
		/// <summary>
		/// Shortcut for Write
		/// </summary>
		private void Write(Action<DirectoryInformation> action)
		{
			this.persistedDirectories.Write(
				directoryInformations => action(directoryInformations[this.FileContainer.FileId]));
		}

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

        private IFileHandler CreateFileHelper(
            string filename, string fileType, ID<IUserOrGroup, Guid>? ownerId, CreateFileDelegate createFileDelegate)
        {
			FileHandlerFactoryLocator.FileSystemResolver.VerifyNoForbiddenChars(filename);

            DateTime created = DateTime.UtcNow;

            FileId fileId = default(FileId);

			this.persistedDirectories.Write(directoryInformations =>
			{
				// Determine the file ID
				bool keepLooping;
				do
				{
					keepLooping = false;
                    fileId = new FileId(SRandom.Next<long>());
					
					foreach (var directoryInformation in directoryInformations.Values)
						if (directoryInformation.filesById.ContainsKey(fileId))
						{
							keepLooping = true;
							break;
						}
				} while (keepLooping);
	
	            try
	            {
					var directoryInformation = directoryInformations[this.FileContainer.FileId];
					
					if (directoryInformation.filesByName.ContainsKey(filename))
                        throw new DuplicateFile(filename);
					
					var file = new File()
					{
	                    filename = filename,
	                    fileId = fileId,
	                    typeId = fileType,
	                    ownerId = ownerId,
	                    created = created,
						permissions = new Dictionary<ID<IUserOrGroup, Guid>, Permission>(),
						namedPermissions = new Dictionary<ID<IUserOrGroup, Guid>, Dictionary<string, bool>>(),
						relationships = new Dictionary<FileId, Dictionary<string, bool>>()
					};
					
					directoryInformation.filesById[fileId] = file;
					directoryInformation.filesByName[filename] = file;
	
                    // Create the file within the transaction.  This way, if there's an exception, the transaction
                    // is rolled back
                    createFileDelegate(fileId);
	            }
	            catch (DiskException)
	            {
	                throw;
	            }
	            catch (Exception e)
	            {
	                throw new CanNotCreateFile("Database exception when creating " + filename, e);
	            }
			});

            IFileHandler toReturn = FileHandlerFactoryLocator.FileSystemResolver.LoadFile(fileId, fileType);
            toReturn.FileContainer = new FileContainer(fileId, fileType, filename, this, FileHandlerFactoryLocator, created);

            /*IUser changer = null;
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
            }*/

            OnDirectoryChanged();

            return toReturn;
        }

        private static string GetExtensionFromFilename(string filename)
        {
            string extension;
            int lastDot = filename.LastIndexOf('.');
            if (lastDot >= 0)
                extension = filename.Substring(lastDot + 1);
            else
                extension = "";

            return extension;
        }
		
        public IFileContainer OpenFile(string filename)
        {
            string[] splitAtDirs = filename.Split(new char[] {'/'});
			
			return this.persistedDirectories.Read<IFileContainer>(directoryInformations =>
			{
				var directoryInformation = directoryInformations[this.FileContainer.FileId];
				
				File file;
				
				// Traverse any subpaths
				var ctr = 0;
				for (; ctr < splitAtDirs.Length - 1; ctr++)
				{
					if (!directoryInformation.filesByName.TryGetValue(splitAtDirs[ctr], out file))
					{
						var pathBuilder = new StringBuilder(this.FileContainer.FullPath);
						for (int subCtr = 0; subCtr <= ctr; subCtr++)
							pathBuilder.AppendFormat("/{0}", splitAtDirs[subCtr]);
							
						throw new FileDoesNotExist(pathBuilder.ToString());
					}
					
					if (!directoryInformations.TryGetValue(file.fileId, out directoryInformation))
					{
						var pathBuilder = new StringBuilder(this.FileContainer.FullPath);
						for (int subCtr = 0; subCtr <= ctr; subCtr++)
							pathBuilder.AppendFormat("/{0}", splitAtDirs[subCtr]);
							
						pathBuilder.Append(" is not a directory");
						
						throw new WrongFileType(pathBuilder.ToString());
					}
				}
				
				// Actually get the file
				if (!directoryInformation.filesByName.TryGetValue(splitAtDirs[ctr], out file))
					throw new FileDoesNotExist(filename);
				
				return new FileContainer(file.fileId, file.typeId, splitAtDirs[ctr], this, this.FileHandlerFactoryLocator, file.created);
			});
        }

        public IEnumerable<IFileContainer> Files
        {
            get
            {
				return this.Read<IEnumerable<IFileContainer>>(directoryInformation =>
                {
					var fileContainers = new List<IFileContainer>(directoryInformation.filesById.Count);
					
					foreach (var file in directoryInformation.filesById.Values)
					{
						fileContainers.Add(new FileContainer(
							file.fileId,
							file.typeId,
							file.filename,
							this,
							this.FileHandlerFactoryLocator,
							file.created));
					}
					
					return fileContainers;
				});
            }
        }

        public IEnumerable<IFileContainer> GetNewestFiles(ID<IUserOrGroup, Guid> userId, int maxToReturn)
        {
            var ids = new HashSet<ID<IUserOrGroup, Guid>>();
            ids.Add(userId);
            ids.Add(FileHandlerFactoryLocator.UserFactory.Everybody.Id);
			
			foreach (var id in FileHandlerFactoryLocator.UserManagerHandler.GetGroupIdsThatUserIsIn(userId))
				ids.Add(id);

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
			
			return this.Read<IEnumerable<IFileContainer>>(directoryInformation =>
			{
				var filesWithAccess = new List<File>(directoryInformation.filesById.Count);
				foreach (var file in directoryInformation.filesById.Values)
					if (file.ownerId == userId || file.permissions.Keys.Where(id => ids.Contains(id)).Count() > 0)
						filesWithAccess.Add(file);
				
				var toReturn = new List<IFileContainer>(maxToReturn);
				filesWithAccess.Sort((a,b) => DateTime.Compare(a.created, b.created));
				foreach (var file in filesWithAccess.Take(maxToReturn))
				{
					toReturn.Add(new FileContainer(file.fileId, file.typeId, file.filename, this, this.FileHandlerFactoryLocator, file.created));
				}
				
				return toReturn;
			});
        }

        public void SetPermission(ID<IUserOrGroup, Guid>? assigningPermission, string filename, IEnumerable<ID<IUserOrGroup, Guid>> userOrGroupIds, FilePermissionEnum level, bool inherit, bool sendNotifications)
        {
			this.Write(directoryInformation =>
			{
	            var file = directoryInformation[filename];

				foreach (ID<IUserOrGroup, Guid> userOrGroupId in userOrGroupIds)
                {
                    Permission permission = new Permission();
                    permission.level = level;
                    permission.inherit = inherit;
                    permission.sendNotifications = sendNotifications;

                    file.permissions[userOrGroupId] = permission;
                }
            });

            // If notifications are enabled, then send a notification informing the user of the change
            IFileContainer targetFile = OpenFile(filename);

            if (null != assigningPermission)
            {
                IUser sender = FileHandlerFactoryLocator.UserManagerHandler.GetUser(assigningPermission.Value);
                targetFile.FileHandler.SendShareNotificationFrom(sender);
            }

            OnDirectoryChanged();
        }

        public void RemovePermission(string filename, IEnumerable<ID<IUserOrGroup, Guid>> userOrGroupIds)
        {
			this.Write(directoryInformation =>
			{
	            var file = directoryInformation[filename];

                foreach (ID<IUserOrGroup, Guid> userOrGroupId in userOrGroupIds)
                    file.permissions.Remove(userOrGroupId);
            });

            OnDirectoryChanged();
        }

        public IEnumerable<FilePermission> GetPermissions(string filename)
        {
			return this.Read<IEnumerable<FilePermission>>(directoryInformation =>
			{
	            var file = directoryInformation[filename];
				var permissions = new List<FilePermission>(file.permissions.Count);
				
				foreach (KeyValuePair<ID<IUserOrGroup, Guid>, Permission> permissionKVP in file.permissions)
                {
                    FilePermission permission = new FilePermission();
                    permission.UserOrGroupId = permissionKVP.Key;
                    permission.FilePermissionEnum = permissionKVP.Value.level;
                    permission.Inherit = permissionKVP.Value.inherit;
                    permission.SendNotifications = permissionKVP.Value.sendNotifications;

                    Dictionary<string, bool> namedPermissions;
                    if (file.namedPermissions.TryGetValue(permissionKVP.Key, out namedPermissions))
                        permission.NamedPermissions = DictionaryFunctions.Create<string, bool>(namedPermissions);
                    else
                        permission.NamedPermissions = new Dictionary<string,bool>();
                }
				
				return permissions;
			});
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
			
			this.persistedDirectories.Write(directoryInformations =>
			{
				var directoryInformation = directoryInformations[this.FileContainer.FileId];
                var toDelete = directoryInformation[filename];
				
				foreach (var diItr in directoryInformations.Values)
					foreach (var file in diItr.filesById.Values)
						file.relationships.Remove(toDelete.fileId);

				FileHandlerFactoryLocator.FileSystemResolver.DeleteFile(toDelete.fileId);
            });

            OnDirectoryChanged();
        }

        public IEnumerable<string> GetFilenames()
        {
			return this.Read<IEnumerable<string>>(
				directoryInformation => directoryInformation.filesByName.Keys.ToArray());
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

        public FilePermissionEnum? LoadPermission(string filename, IEnumerable<ID<IUserOrGroup, Guid>> userAndGroupIds, bool onlyReturnInheritedPermissions)
        {
            return LoadPermission(filename, userAndGroupIds, onlyReturnInheritedPermissions, 0);
        }

        public FilePermissionEnum? LoadPermission(string filename, IEnumerable<ID<IUserOrGroup, Guid>> userAndGroupIds, bool onlyReturnInheritedPermissions, uint recurse)
        {
            var file = FileDataByNameCache[filename];

            Permission permission;
            FilePermissionEnum? highestPermission = null;
            foreach (ID<IUserOrGroup, Guid> userAndGroupId in userAndGroupIds)
                if (file.Info.Permissions.TryGetValue(userAndGroupId.Value, out permission))
                    if (!onlyReturnInheritedPermissions | permission.Inherit)
                    {
                        if (null == highestPermission)
                            highestPermission = permission.Level;
                        else if (permission.Level > highestPermission.Value)
                            highestPermission = permission.Level;
                    }

            if (null == highestPermission)
                highestPermission = LoadPermissionFromRelated(
                    new IFileId[] { new FileId(file.FileId.Value) },
                    userAndGroupIds,
                    new HashSet<IFileId>(), recurse);

            if (null != highestPermission)
                return highestPermission;

            if (FileContainer == FileHandlerFactoryLocator.FileSystemResolver.RootDirectoryHandler.FileContainer)
                return null;

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
            HashSet<IFileId> alreadyChecked,
            uint recurse)
        {
            FilePermissionEnum? highestPermission = null;
            List<IFileId> parentIds = new List<IFileId>();
            List<IFileId> inheritedParentIds = new List<IFileId>();

            // Load all of the parent related files
            foreach (IRelationships_Readable relationship in DatabaseConnection.Relationships.Select(
                Relationships_Table.ReferencedFileId.In(fileIds) & Relationships_Table.Inherit == true))
            {
                FileId fileId = relationship.FileId;

                if (!alreadyChecked.Contains(fileId))
                {
                    parentIds.Add(fileId);
                    alreadyChecked.Add(fileId);

                    if (relationship.Inherit)
                        inheritedParentIds.Add(fileId);
                }
            }

            // If there are no parent relationships, just return null
            if (0 == parentIds.Count)
                return null;

            // Deal with explicitly set inherit permissions
            HashSet<string> parentFileNames = new HashSet<string>();
            HashSet<ID<IUserOrGroup, Guid>> parentOwners = new HashSet<ID<IUserOrGroup, Guid>>();
            // TODO:  This is very untested
            if (inheritedParentIds.Count > 0)
                foreach (IFile_Readable file in DatabaseConnection.File.Select(File_Table.FileId.In(inheritedParentIds)))
                {
                    parentFileNames.Add(file.Name);

                    if (null != file.OwnerId)
                        parentOwners.Add(file.OwnerId.Value);
                }

            // If any of the users owns any of the parent files, then permission is granted
            // Else, if any of the users has permission to any of the parent files, then permission is granted
            parentOwners.IntersectWith(userAndGroupIds);
            bool ownsParentFile = parentOwners.Count > 0;
            parentOwners = null;

            if (ownsParentFile)
                highestPermission = FilePermissionEnum.Read;

            else
                foreach (string filename in parentFileNames)
                {
                    FilePermissionEnum? fromFile = LoadPermission(filename, userAndGroupIds, false, recurse + 1);
                    if (fromFile != null)
                    {
                        highestPermission = FilePermissionEnum.Read;
                        break;
                    }
                }

            // Scan the permissions of the parent related files
            Permission permission;
            foreach (IFile_Readable file in DatabaseConnection.File.Select(File_Table.FileId.In(parentIds)))
                foreach (var userAndGroupId in userAndGroupIds)
                    if (file.Info.Permissions.TryGetValue(userAndGroupId.Value, out permission))
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

            List<IFile_Readable> files = new List<IFile_Readable>(DatabaseConnection.File.Select());
            Dictionary<FileId, IFileContainer> fileContainersById = new Dictionary<FileId, IFileContainer>();

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
                                    foreach (KeyValuePair<Guid, Permission> permissionKVP in file.Info.Permissions)
                                    {
                                        try
                                        {
                                            IUserOrGroup userOrGroup = FileHandlerFactoryLocator.UserManagerHandler.GetUserOrGroup(
                                                new ID<IUserOrGroup, Guid>(permissionKVP.Key));

                                            if (userOrGroup.BuiltIn)
                                            {
                                                xmlWriter.WriteStartElement("Permission");

                                                xmlWriter.WriteAttributeString("UserOrGroupId", permissionKVP.Key.ToString());
                                                xmlWriter.WriteAttributeString("Level", permissionKVP.Value.Level.ToString());
                                                xmlWriter.WriteAttributeString("Inherit", permissionKVP.Value.Inherit.ToString());
                                                xmlWriter.WriteAttributeString("SendNotifications", permissionKVP.Value.SendNotifications.ToString());

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
            IFile_Readable oldFile = null;
            DatabaseConnection.CallOnTransaction(delegate(IDatabaseTransaction transaction)
            {
                oldFile = DatabaseConnection.File.SelectSingle(File_Table.Name == oldFilename);
                if (null == oldFile)
                    throw new FileDoesNotExist(FileContainer.FullPath + "/" + oldFilename);

                if (null != DatabaseConnection.File.SelectSingle(File_Table.Name == newFilename))
                    throw new DuplicateFile(FileContainer.FullPath + "/" + newFilename);

                DatabaseConnection.File.Update(
                    File_Table.Name == oldFilename,
                    delegate(IFile_Writable file)
                    {
                        file.Name = newFilename;
                        file.Extension = GetExtensionFromFilename(newFilename);
                    });

                transaction.Commit();
            });

            FileIDCacheByName.Remove(oldFilename);
			FileContainerCache.Remove(oldFilename);
            FileDataByNameCache.Remove(oldFilename);
            FileDataByIdCache.Remove(oldFile.FileId);

            IFileContainer newFileContainer = FileContainerCache[newFilename];
            newFileContainer.WebHandler.FileContainer = newFileContainer;
            newFileContainer.WebHandler.ResetExecutionEnvironment();

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

                    OnDirectoryChanged();

                    return;
                }

            throw new SecurityException("Permission denied");
        }

        public string IndexFile
        {
            get
            {
				return this.Read<string>(
					directoryInformation => directoryInformation.indexFile);
            }
            set
            {
				this.Write(
					directoryInformation => directoryInformation.indexFile = value);

                OnDirectoryChanged();
            }
        }

        public override void SyncFromLocalDisk(string localDiskPath, bool force)
        {
            SyncFromLocalDisk(localDiskPath, force, false);
        }

        public void SyncFromLocalDisk(string localDiskPath, bool force, bool onlyMissing)
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
                                    if (!onlyMissing)
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

                                SetPermission(null, filename, new ID<IUserOrGroup, Guid>[] { userOrGroupId }, level, inherit, sendNotifications);
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
			LinkedList<FileId> allFiles = new LinkedList<FileId>();
			HashSet<FileId> inspectPermission = new HashSet<FileId>();
			
			LinkedList<ComparisonCondition> comparisonConditions = new LinkedList<ComparisonCondition>();

            comparisonConditions.AddLast(Relationships_Table.FileId == parentFileId);

            if (null != relationships)
                comparisonConditions.AddLast(Relationships_Table.Relationship.In(relationships));

            foreach (IRelationships_Readable relationshipInDb in Enumerable<IRelationships_Readable>.FastCopy(
                DatabaseConnection.Relationships.Select(ComparisonCondition.Condense(comparisonConditions))))
            {
                if (!relationshipInDb.Inherit)
                    inspectPermission.Add(relationshipInDb.ReferencedFileId);
				
				allFiles.AddLast(relationshipInDb.ReferencedFileId);
            }
  
			comparisonConditions.Clear();
            comparisonConditions.AddLast(File_Table.FileId.In(allFiles));

            if (null != extensions)
                comparisonConditions.AddLast(File_Table.Extension.In(extensions));

            if (null != newest)
                comparisonConditions.AddLast(File_Table.Created < newest.Value);

            if (null != oldest)
                comparisonConditions.AddLast(File_Table.Created > oldest.Value);

			LinkedList<ID<IUserOrGroup, Guid>> userAndGroupIds = new LinkedList<ID<IUserOrGroup, Guid>>(
				FileHandlerFactoryLocator.UserManagerHandler.GetGroupIdsThatUserIsIn(userId));
			userAndGroupIds.AddLast(userId);
			
			foreach (IFile_Readable file in Enumerable<IFile_Readable>.FastCopy(DatabaseConnection.File.Select(
                ComparisonCondition.Condense(comparisonConditions),
                maxToReturn,
                ObjectCloud.ORM.DataAccess.OrderBy.Desc,
                File_Table.Created)))
            {
				// Slow, but simple
				// TODO: Optimize
				IFileContainer toYield = FileContainerCache[file.Name];
				
				if (inspectPermission.Contains(file.FileId))
				{
					if (null != toYield.LoadPermission(userId))
						yield return toYield;
				}
				else
					yield return toYield;
			}
		}
		
        public virtual LinkNotificationInformation AddRelationship(IFileContainer parentFile, IFileContainer relatedFile, string relationship, bool inheritPermission)
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
                        relationshipInDb.Inherit = inheritPermission;
                    });
                }
                catch (Exception e)
                {
                    transaction.Rollback();
                    throw new DiskException("Relationships in a directory must be unique.  Ensure that the relationship is unique", e);
                }

                transaction.Commit();
            });

            LinkNotificationInformation toReturn = 
                parentFile.FileHandler.SendLinkNotificationFrom(parentFile.Owner, relatedFile);

            parentFile.FileHandler.OnRelationshipAdded(new RelationshipEventArgs(relatedFile, relationship));

            return toReturn;
        }

        public void DeleteRelationship(IFileContainer parentFile, IFileContainer relatedFile, string relationship)
        {
            DatabaseConnection.Relationships.Delete(
                Relationships_Table.FileId == parentFile.FileId & Relationships_Table.ReferencedFileId == relatedFile.FileId & Relationships_Table.Relationship == relationship);

            parentFile.FileHandler.OnRelationshipDeleted(new RelationshipEventArgs(relatedFile, relationship));
        }

        public void Chown(IUser changer, IFileId fileId, ID<IUserOrGroup, Guid>? newOwnerId)
        {
            IFile_Readable oldFile = DatabaseConnection.File.SelectSingle(File_Table.FileId == fileId);
			if (null == oldFile)
				throw new FileDoesNotExist("ID: " + fileId.ToString());

            // Verify that the new owner exists
            if (null != newOwnerId)
                FileHandlerFactoryLocator.UserManagerHandler.GetUser(newOwnerId.Value);

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

            FileContainerCache.Remove(oldFile.Name);
            IFileContainer newFileContainer = FileContainerCache[oldFile.Name];
            newFileContainer.WebHandler.FileContainer = newFileContainer;
            newFileContainer.WebHandler.ResetExecutionEnvironment();


            OnDirectoryChanged();
        }

        public void SetNamedPermission(IFileId fileId, string namedPermission, IEnumerable<ID<IUserOrGroup, Guid>> userOrGroupIds, bool inherit)
        {
            var file = FileDataByIdCache[(FileId)fileId];

            using (TimedLock.Lock(file))
            {
                foreach (ID<IUserOrGroup, Guid> userOrGroupId in userOrGroupIds)
                {
                    Dictionary<string, bool> permissionInfo;
                    if (!file.Info.NamedPermissions.TryGetValue(userOrGroupId.Value, out permissionInfo))
                    {
                        permissionInfo = new Dictionary<string,bool>();
                        file.Info.NamedPermissions[userOrGroupId.Value] = permissionInfo;
                    }

                    permissionInfo[namedPermission] = inherit;
                }

                DatabaseConnection.File.Update(
                    File_Table.FileId == fileId,
                    delegate(IFile_Writable fileW)
                    {
                        fileW.Info = file.Info;
                    });
            }
        }

        public void RemoveNamedPermission(IFileId fileId, string namedPermission, IEnumerable<ID<IUserOrGroup, Guid>> userOrGroupIds)
        {
            var file = FileDataByIdCache[(FileId)fileId];

            using (TimedLock.Lock(file))
            {
                Dictionary<string, bool> permissionInfo;
                foreach (ID<IUserOrGroup, Guid> userOrGroupId in userOrGroupIds)
                    if (file.Info.NamedPermissions.TryGetValue(userOrGroupId.Value, out permissionInfo))
                        permissionInfo.Remove(namedPermission);

                DatabaseConnection.File.Update(
                    File_Table.FileId == fileId,
                    delegate(IFile_Writable fileW)
                    {
                        fileW.Info = file.Info;
                    });
            }
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
            Dictionary<string, bool> permissionInfo;
            foreach (IFile_Readable file in DatabaseConnection.File.Select(File_Table.FileId.In(fileIds)))
                foreach (ID<IUserOrGroup, Guid> userOrGroupId in userOrGroupIds)
                    if (file.Info.NamedPermissions.TryGetValue(userOrGroupId.Value, out permissionInfo))
                        foreach (string namedPermission in namedPermissions)
                            if (permissionInfo.ContainsKey(namedPermission))
                                return true;

            // permission not found, now follow references through any related files
            if (checkInherit)
                if (HasNamedPermissionThroughRelationship(fileIds, namedPermissions, userOrGroupIds, numCalls))
                    return true;

            // As written, this won't work and is illogical
            // It passes FileIDs that are invalid in the parent directory, and there is no way to communicate to only return true
            // if the permission is inherited
            //if (checkInherit && null != FileContainer.ParentDirectoryHandler)
            //    return FileContainer.ParentDirectoryHandler.HasNamedPermissions(fileIds, namedPermissions, userOrGroupIds, checkInherit);

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
            var file = FileDataByIdCache[(FileId)fileId];

            foreach (KeyValuePair<Guid, Dictionary<string, bool>> namedPermissionData in file.Info.NamedPermissions)
                foreach (KeyValuePair<string, bool> namedPermissionValue in namedPermissionData.Value)
                {
                    NamedPermission toYeild = new NamedPermission();
                    toYeild.FileId = fileId;
                    toYeild.Inherit = namedPermissionValue.Value;
                    toYeild.Name = namedPermissionValue.Key;
                    toYeild.UserOrGroupId = new ID<IUserOrGroup, Guid>(namedPermissionData.Key);

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