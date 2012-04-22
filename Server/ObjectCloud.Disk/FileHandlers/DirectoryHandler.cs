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
		private class FileInformation
		{
			public FileId fileId;
			public string filename;
			public string typeId;
			public ID<IUserOrGroup, Guid>? ownerId;
			public DateTime created;
	        public Dictionary<ID<IUserOrGroup, Guid>, Permission> permissions = new Dictionary<ID<IUserOrGroup, Guid>, Permission>();
	        public Dictionary<ID<IUserOrGroup, Guid>, Dictionary<string, bool>> namedPermissions = new Dictionary<ID<IUserOrGroup, Guid>, Dictionary<string, bool>>();
			
			/// <summary>
			/// All of the files that this file is related to, and the relationship
			/// </summary>
			public Dictionary<FileInformation, HashSet<string>> relationships =
				new Dictionary<FileInformation, HashSet<string>>();

			/// <summary>
			/// All of the files that are related to this file, and their relationships
			/// </summary>
			public Dictionary<FileInformation, Dictionary<string, bool>> parentRelationships =
				new Dictionary<FileInformation, Dictionary<string, bool>>();
			
			public override bool Equals (object obj)
			{
				return this == obj;
			}
			
			public override int GetHashCode ()
			{
				return fileId.GetHashCode();
			}
		}
		
		/// <summary>
		/// Stores files in the various different ways of indexing, and other directory data
		/// </summary>
		[Serializable]
		private class DirectoryInformation : FileInformation
		{
			public string indexFile;
			
			public readonly Dictionary<string, FileInformation> files = new Dictionary<string, FileInformation>();
			
			/// <summary>
			/// Gets the <see cref="ObjectCloud.Disk.FileHandlers.DirectoryHandler.DirectoryInformation"/> with the specified name, throws an exception if it doesnt exist.
			/// </summary>
			/// <exception cref='FileDoesNotExist'>
			/// Is thrown when the file does not exist.
			/// </exception>
			public FileInformation this[string name]
			{
				get
		        {
					FileInformation file;
					if (files.TryGetValue(name, out file))
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
			this.persistedDirectories = new PersistedObject<Dictionary<IFileId, FileInformation>>(
				path, () => new Dictionary<IFileId, FileInformation>());
        }
		
		/// <summary>
		/// All information about the entire file system is held in RAM and persisted to disk as a single object graph
		/// </summary>
		private PersistedObject<Dictionary<IFileId, FileInformation>> persistedDirectories;
		
		/// <summary>
		/// Shortcut for reading
		/// </summary>
		private T Read<T>(Func<DirectoryInformation, T> func)
		{
			return this.persistedDirectories.Read<T>(
				fileInformations => func((DirectoryInformation)fileInformations[this.FileContainer.FileId]));
		}
		
		/// <summary>
		/// Shortcut for Write
		/// </summary>
		private T Write<T>(Func<DirectoryInformation, T> func)
		{
			return this.persistedDirectories.Write<T>(
				fileInformations => func((DirectoryInformation)fileInformations[this.FileContainer.FileId]));
		}
		
		/// <summary>
		/// Shortcut for Write
		/// </summary>
		private void Write(Action<DirectoryInformation> action)
		{
			this.persistedDirectories.Write(
				fileInformations => action((DirectoryInformation)fileInformations[this.FileContainer.FileId]));
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
			IFileHandler fileHandler = null;

			this.persistedDirectories.Write(fileInformations =>
			{
				// Determine the file ID
				do
                    fileId = new FileId(SRandom.Next<long>());
				while (!fileInformations.ContainsKey(fileId));
	
	            try
	            {
					var directoryInformation = (DirectoryInformation)fileInformations[this.FileContainer.FileId];
					
					if (directoryInformation.files.ContainsKey(filename))
                        throw new DuplicateFile(filename);

					// Create the file within the transaction.  This way, if there's an exception, the transaction
                    // is rolled back
                    createFileDelegate(fileId);
					
		            fileHandler = FileHandlerFactoryLocator.FileSystemResolver.LoadFile(fileId, fileType);
					
					FileInformation file;
					if (fileHandler is DirectoryHandler)
						file = new DirectoryInformation();
					else
						file = new FileInformation();
					
	                file.filename = filename;
	                file.fileId = fileId;
	                file.typeId = fileType;
	                file.ownerId = ownerId;
	                file.created = created;
					file.permissions = new Dictionary<ID<IUserOrGroup, Guid>, Permission>();
					file.namedPermissions = new Dictionary<ID<IUserOrGroup, Guid>, Dictionary<string, bool>>();
					file.parentRelationships = new Dictionary<FileInformation, Dictionary<string, bool>>();
					
					fileInformations[fileId] = file;
					directoryInformation.files[filename] = file;
	
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

            fileHandler.FileContainer = new FileContainer(fileId, fileType, filename, this, FileHandlerFactoryLocator, created);

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

            return fileHandler;
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
			
			return this.persistedDirectories.Read<IFileContainer>(fileInformations =>
			{
				var directoryInformation = (DirectoryInformation)fileInformations[this.FileContainer.FileId];
				
				FileInformation file;
				
				// Traverse any subpaths
				var ctr = 0;
				for (; ctr < splitAtDirs.Length - 1; ctr++)
				{
					if (!directoryInformation.files.TryGetValue(splitAtDirs[ctr], out file))
					{
						var pathBuilder = new StringBuilder(this.FileContainer.FullPath);
						for (int subCtr = 0; subCtr <= ctr; subCtr++)
							pathBuilder.AppendFormat("/{0}", splitAtDirs[subCtr]);
							
						throw new FileDoesNotExist(pathBuilder.ToString());
					}
					
					directoryInformation = file as DirectoryInformation;
					if (null == directoryInformation)
					{
						var pathBuilder = new StringBuilder(this.FileContainer.FullPath);
						for (int subCtr = 0; subCtr <= ctr; subCtr++)
							pathBuilder.AppendFormat("/{0}", splitAtDirs[subCtr]);
							
						pathBuilder.Append(" is not a directory");
						
						throw new WrongFileType(pathBuilder.ToString());
					}
				}
				
				// Actually get the file
				if (!directoryInformation.files.TryGetValue(splitAtDirs[ctr], out file))
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
					var fileContainers = new List<IFileContainer>(directoryInformation.files.Count);
					
					foreach (var file in directoryInformation.files.Values)
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
				var filesWithAccess = new List<FileInformation>(directoryInformation.files.Count);
				foreach (var file in directoryInformation.files.Values)
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
			
			this.persistedDirectories.Write(fileInformations =>
			{
				var directoryInformation = (DirectoryInformation)fileInformations[this.FileContainer.FileId];
                var toDelete = directoryInformation[filename];
				
				// Remove relationships
				foreach (var relatedFile in toDelete.relationships.Keys)
					relatedFile.parentRelationships.Remove(toDelete);
				foreach (var parentFile in toDelete.parentRelationships.Keys)
					parentFile.relationships.Remove(toDelete);
				
				directoryInformation.files.Remove(filename);
				fileInformations.Remove(toDelete.fileId);

				FileHandlerFactoryLocator.FileSystemResolver.DeleteFile(toDelete.fileId);
            });

            OnDirectoryChanged();
        }

        public IEnumerable<string> GetFilenames()
        {
			return this.Read<IEnumerable<string>>(
				directoryInformation => directoryInformation.files.Keys.ToArray());
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
            var userAndGroupsIds = GetAllUserAndGroupIdsThatApplyToUser(userId);

            return LoadPermission(filename, userAndGroupsIds, false);
        }

        public FilePermissionEnum? LoadPermission(string filename, IEnumerable<ID<IUserOrGroup, Guid>> userAndGroupIds, bool onlyReturnInheritedPermissions)
        {
			return this.persistedDirectories.Read<FilePermissionEnum?>(fileInformations =>
			{
				var directoryInformation = (DirectoryInformation)fileInformations[this.FileContainer.FileId];
		    	var file = directoryInformation[filename];

				return this.LoadPermissionInt(
					fileInformations,
					directoryInformation,
					file,
					userAndGroupIds.ToHashSet(),
					onlyReturnInheritedPermissions);
			});
        }

		private FilePermissionEnum? LoadPermissionInt(
			Dictionary<IFileId, FileInformation> fileInformations,
			DirectoryInformation directoryInformation,
			FileInformation file,
			HashSet<ID<IUserOrGroup, Guid>> userAndGroupIds,
			bool onlyReturnInheritedPermissions)
		{
			IDirectoryHandler directoryHandler = this;
        	FilePermissionEnum? highestPermission = null;
        	     	
	     	do
	     	{
	     		if (null != file.ownerId)
	     			if (userAndGroupIds.Contains(file.ownerId.Value))
	     				return FilePermissionEnum.Administer;
	     	
	     		// Look at all permissions explicity assigned to this file.
	     		// Do any of them match the user id, or any of the user's groups?
				Permission permission;
				foreach (ID<IUserOrGroup, Guid> userAndGroupId in userAndGroupIds)
				    if (file.permissions.TryGetValue(userAndGroupId, out permission))
				        if (!onlyReturnInheritedPermissions | permission.inherit)
				        {
				            if (null == highestPermission)
				                highestPermission = permission.level;
				            else if (permission.level > highestPermission.Value)
				                highestPermission = permission.level;
				        }
	     	
	     		// If no permission found, do any of the related files allow for inheriting permission?
	     		// When permission is inherited through a relationship, then it's only for read
				if (null == highestPermission)
	     		{
	     			var alreadyScanned = new HashSet<FileInformation>();
	     			var parentRelated = file.parentRelationships.Where(
						relationship => relationship.Value.Any(kvp => kvp.Value)).ToList();
	     			
	     			while (parentRelated.Count > 0)
	     			{
	     				var scanning = parentRelated;
	     				parentRelated = new List<KeyValuePair<FileInformation, Dictionary<string, bool>>>(scanning.Count * 2);
	     				
	     				foreach (var fileAndRelationship in scanning)
	     				{
	     					var parentFile = fileAndRelationship.Key;
	     	
	     					if (null != parentFile.ownerId)
	     						if (userAndGroupIds.Contains(parentFile.ownerId.Value))
	     							return FilePermissionEnum.Read;
	     					
	     					alreadyScanned.Add(parentFile);
	     					
	     		            foreach (ID<IUserOrGroup, Guid> userAndGroupId in userAndGroupIds)
	     		                if (parentFile.permissions.TryGetValue(userAndGroupId, out permission))
	     							return FilePermissionEnum.Read;
	     					
	     					parentRelated.AddRange(parentFile.parentRelationships.Where(relationship => 
	                            relationship.Value.Any(kvp => kvp.Value) && 
	                            !alreadyScanned.Contains(relationship.Key)));
	     				}
	     			}
	     		}
	     		
	     		onlyReturnInheritedPermissions = true;
	     		directoryHandler = directoryHandler.FileContainer.ParentDirectoryHandler;
				file = directoryInformation;
	  			directoryInformation = (DirectoryInformation)fileInformations[directoryHandler.FileContainer.FileId];
	     		
	     	} while (null != directoryHandler);
	     	
	     	return highestPermission;
		}

        /// <summary>
        /// Returns true if the file is present, false otherwise
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public bool IsFilePresent(string filename)
        {
			return this.Read<bool>(
				directoryInformation => directoryInformation.files.ContainsKey(filename));
        }

        /// <summary>
        /// Dumps the directory
        /// </summary>
        /// <param name="xmlWriter"></param>
        public override void Dump(string path, ID<IUserOrGroup, Guid> userId)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

			// Keep an immutable copy of all the files
            var fileContainersById = this.Files.ToDictionary(file => file.FileId);
			
			// Dump all files in the folder
			foreach (var fileContainer in fileContainersById.Values)
				fileContainer.FileHandler.Dump(
					Path.Combine(path, fileContainer.Filename),
					userId);
			
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
					var files = new List<IFileContainer>(fileContainersById.Values);
                    files.Sort((a,b) => a.Filename.CompareTo(b.Filename));

                    foreach (var fileContainer in files)
                    {
                        // Load user's permission for file
                        FilePermissionEnum? filePermissionEnum = fileContainer.LoadPermission(userId);

                        if (null != filePermissionEnum)
                            if (filePermissionEnum.Value >= FilePermissionEnum.Read)
                            {
                                // <FileInDirectory>
                                xmlWriter.WriteStartElement("File");

                                // Write the file's attributes
                                xmlWriter.WriteAttributeString("Name", fileContainer.Filename);
                                xmlWriter.WriteAttributeString("TypeId", fileContainer.TypeId);

                                // Only write the ownerId if it's a built-in user.  This might change if Dump is set up to do true backups, but right now
                                // the files aren't supposed to be tied to specific users
                                if (null != fileContainer.Owner)
                                    xmlWriter.WriteAttributeString("OwnerId", fileContainer.OwnerId.ToString());

                                // Write each permission if it applies to a built-in user or group
                                foreach (var permission in this.GetPermissions(fileContainer.Filename))
                                {
                                    try
                                    {
                                        var userOrGroup = this.FileHandlerFactoryLocator.UserManagerHandler.GetUserOrGroup(
											permission.UserOrGroupId);

                                        if (userOrGroup.BuiltIn)
                                        {
                                            xmlWriter.WriteStartElement("Permission");

                                            xmlWriter.WriteAttributeString("UserOrGroupId", userOrGroup.Id.Value.ToString());
                                            xmlWriter.WriteAttributeString("Level", permission.FilePermissionEnum.ToString());
                                            xmlWriter.WriteAttributeString("Inherit", permission.Inherit.ToString());
                                            xmlWriter.WriteAttributeString("SendNotifications", permission.SendNotifications.ToString());

                                            xmlWriter.WriteEndElement();
                                        }
                                    }
                                    // For now, swallow userIds that aren't a valid user
                                    catch (UnknownUser ue) 
									{
										log.Warn("Found permission assigned to an unknown user", ue);
									}
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
			this.Write(directoryInformation =>
			{
				var file = directoryInformation[oldFilename];
				
				if (directoryInformation.files.ContainsKey(newFilename))
                    throw new DuplicateFile(FileContainer.FullPath + "/" + newFilename);
				
				directoryInformation.files.Remove(oldFilename);
				
				file.filename = newFilename;
				
				directoryInformation.files[newFilename] = file;
			});

            var newFileContainer = this.OpenFile(newFilename);
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
			
			var hasWrites = false;

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
								
										hasWrites = true;
										this.persistedDirectories.WriteEventual(directoryInformations =>
								        {
											var directoryInformation = (DirectoryInformation)directoryInformations[this.FileContainer.FileId];
											directoryInformation[filename].ownerId = ownerId;
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
			
			if (hasWrites)
				this.Write(d => {});
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
            HashSet<string> relationships,
            HashSet<string> extensions,
            DateTime? newest,
            DateTime? oldest,
            uint? maxToReturn)
		{
			Func<HashSet<string>, bool> checkRelationship;
			if (null != relationships)
				checkRelationship = relationshipNames =>
				{
					foreach (var relationshipName in relationshipNames)
						if (relationships.Contains(relationshipName))
							return true;
				
					return false;
				};
			else
				checkRelationship = relationshipValues => true;
			
			Func<FileInformation, bool> checkExtension;
			if (null != extensions)
			{
				var filteredExtensions = new List<string>(extensions.Select(extension => "." + extension));

				checkExtension = relatedFileInfo =>
				{
					foreach (var filteredExtension in filteredExtensions)
						if (relatedFileInfo.filename.EndsWith(filteredExtension))
							return true;
					
					return false;
				};
			}
			else
				checkExtension = relatedFileInfo => true;

			var userAndGroupsIds = GetAllUserAndGroupIdsThatApplyToUser(userId).ToHashSet();

			return this.persistedDirectories.Read(fileInformations =>
            {
				var directoryInformation = (DirectoryInformation)fileInformations[this.FileContainer.FileId];

				FileInformation fileInformation;
				if (!fileInformations.TryGetValue(parentFileId, out fileInformation))
					throw new FileDoesNotExist(parentFileId.ToString());
				
				if (!directoryInformation.files.ContainsValue(fileInformation))
					throw new FileDoesNotExist(fileInformation.filename);
				
				// Filter the related files scanned by date
				var relatedFileInformations = new List<FileInformation>(fileInformation.relationships.Keys);
				relatedFileInformations.Sort((a, b) => a.created.CompareTo(b.created));

				if (null != newest)
					relatedFileInformations = relatedFileInformations.Where(
						relatedFileInfo => relatedFileInfo.created < newest.Value).ToList();

				if (null != oldest)
					relatedFileInformations = relatedFileInformations.Where(
						relatedFileInfo => relatedFileInfo.created > oldest.Value).ToList();
				
				var relatedFileContainers = new List<IFileContainer>(relatedFileInformations.Count);

				foreach (var relatedFileInfo in relatedFileInformations)
				{
					var relationshipValues = fileInformation.relationships[relatedFileInfo];
					
					if (checkRelationship(relationshipValues))
						if (checkExtension(relatedFileInfo))
						{
							// At this point we know that the file is properly related to this file, and that the extension matches the query
							// What we don't know is if the user has permission
						
							var permission = this.LoadPermissionInt(
								fileInformations,
								directoryInformation,
								relatedFileInfo,
								userAndGroupsIds,
								false);
						
							if (null != permission)
							{
								relatedFileContainers.Add(new FileContainer(
									relatedFileInfo.fileId,
									relatedFileInfo.typeId,
									relatedFileInfo.filename,
									this,
									this.FileHandlerFactoryLocator,
									relatedFileInfo.created));
								
								if (null != maxToReturn)
									if (relatedFileContainers.Count >= maxToReturn.Value)
										return relatedFileContainers;
							}
						}
				}

				return relatedFileContainers;
			});
		}
		
        public virtual LinkNotificationInformation AddRelationship(IFileContainer parentFile, IFileContainer relatedFile, string relationship, bool inheritPermission)
        {
			this.persistedDirectories.Write(fileInformations =>
			{
				var parentFileInformation = fileInformations[parentFile.FileId];
				var relatedFileInformation = fileInformations[relatedFile.FileId];
				
				var directoryInformation = (DirectoryInformation)fileInformations[this.FileContainer.FileId];
				
				if (!directoryInformation.files.ContainsValue(parentFileInformation))
                    throw new DiskException("Parent file must be in the directory where the relationship exists");
				
				Dictionary<string, bool> parentRelationships;
				if (!relatedFileInformation.parentRelationships.TryGetValue(parentFileInformation, out parentRelationships))
				{
					parentRelationships = new Dictionary<string, bool>();
					relatedFileInformation.parentRelationships[parentFileInformation] = parentRelationships;
				}
				
				if (parentRelationships.ContainsKey(relationship))
                    throw new DiskException("Relationships in a directory must be unique.  Ensure that the relationship is unique");
				
				parentRelationships[relationship] = inheritPermission;
				
				HashSet<string> relationshipNames;
				if (!parentFileInformation.relationships.TryGetValue(relatedFileInformation, out relationshipNames))
				{
					relationshipNames = new HashSet<string>();
					parentFileInformation.relationships[relatedFileInformation] = relationshipNames;
				}
				relationshipNames.Add(relationship);
			});
			
            LinkNotificationInformation toReturn = 
                parentFile.FileHandler.SendLinkNotificationFrom(parentFile.Owner, relatedFile);

            parentFile.FileHandler.OnRelationshipAdded(new RelationshipEventArgs(relatedFile, relationship));

            return toReturn;
        }

        public void DeleteRelationship(IFileContainer parentFile, IFileContainer relatedFile, string relationship)
        {
			this.persistedDirectories.Write(fileInformations =>
			{
				var parentFileInformation = fileInformations[parentFile.FileId];
				var relatedFileInformation = fileInformations[relatedFile.FileId];
				
				var directoryInformation = (DirectoryInformation)fileInformations[this.FileContainer.FileId];
				
				if (!directoryInformation.files.ContainsValue(parentFileInformation))
                    throw new DiskException("Parent file must be in the directory where the relationship exists");
				
				HashSet<string> relationshipNames;
				if (parentFileInformation.relationships.TryGetValue(relatedFileInformation, out relationshipNames))
				{
					relationshipNames.Remove(relationship);
					if (0 == relationshipNames.Count)
						parentFileInformation.relationships.Remove(relatedFileInformation);
				}
				
				Dictionary<string, bool> parentRelationships;
				if (relatedFileInformation.parentRelationships.TryGetValue(parentFileInformation, out parentRelationships))
				{
					parentRelationships.Remove(relationship);
					if (0 == parentRelationships.Count)
						relatedFileInformation.parentRelationships.Remove(parentFileInformation);
				}
			});

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