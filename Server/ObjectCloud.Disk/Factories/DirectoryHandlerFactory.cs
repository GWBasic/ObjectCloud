// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;
using System.Xml;

using ObjectCloud.Common;
using ObjectCloud.Common.StreamEx;
using ObjectCloud.Disk.FileHandlers;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Disk.Factories
{
    public class DirectoryHandlerFactory : FileHandlerFactory<IDirectoryHandler>
    {
		private PersistedObject<Dictionary<IFileId, DirectoryHandler.FileInformation>> persistedFileInformations = null;

        public override void CreateFile(string path, FileId fileId)
        {
        }

        public override IDirectoryHandler OpenFile(string path, FileId fileId)
        {
			if (null == this.persistedFileInformations)
				lock (this)
					if (null == this.persistedFileInformations)
					{
						var metadataLocation = Path.Combine(
							((FileSystem)this.FileHandlerFactoryLocator.FileSystem).ConnectionString,
							"metadata");
				
						this.persistedFileInformations = new PersistedObject<Dictionary<IFileId, DirectoryHandler.FileInformation>>(
							metadataLocation, this.CreateInitialFileInformations, this.Deserialize, this.Serialize);
				
						ThreadPool.QueueUserWorkItem(_ => this.RemoveDeadPermissions());
					}
			
			this.persistedFileInformations.Read(fileInformations =>
			{
				DirectoryHandler.FileInformation fi;
				if (fileInformations.TryGetValue(fileId, out fi))
					if (fi is DirectoryHandler.DirectoryInformation)
						return;
					else
						throw new InvalidFileId(fileId);
				
				throw new InvalidFileId(fileId);
			});
			
			return new DirectoryHandler(this.persistedFileInformations, this.FileHandlerFactoryLocator);
        }
		
		private Dictionary<IFileId, DirectoryHandler.FileInformation> CreateInitialFileInformations()
		{
			var initialFileInformations = new Dictionary<IFileId, DirectoryHandler.FileInformation>();
			
			initialFileInformations[this.FileHandlerFactoryLocator.FileSystem.RootDirectoryId] = new DirectoryHandler.DirectoryInformation()
			{
				fileId = (FileId)this.FileHandlerFactoryLocator.FileSystem.RootDirectoryId,
				filename = string.Empty,
				typeId = "directory",
				ownerId = null,
                created = DateTime.UtcNow,
				permissions = new Dictionary<ID<IUserOrGroup, Guid>, DirectoryHandler.Permission>(),
				namedPermissions = new Dictionary<ID<IUserOrGroup, Guid>, Dictionary<string, bool>>(),
				parentRelationships = new Dictionary<DirectoryHandler.FileInformation, Dictionary<string, bool>>()
			};
			
			return initialFileInformations;
		}
		
		/// <summary>
		/// Cleans up dead permissions. Runs on a background thread because it relies on complete initialization
		/// </summary>
		private void RemoveDeadPermissions()
		{
			var knownUserAndGroupIds = new HashSet<ID<IUserOrGroup, Guid>>();
			var missingUserAndGroupIds = new HashSet<ID<IUserOrGroup, Guid>>();
			
			// Clean up permissions for deleted users
			// This really should be event-driven
			this.persistedFileInformations.Read(fileInformations =>
			{
				foreach (var fileInformation in fileInformations.Values)
					foreach (var userOrGroupId in fileInformation.permissions.Keys.Union(
						fileInformation.namedPermissions.Keys))
							knownUserAndGroupIds.Add(userOrGroupId);
			});

			foreach (var userOrGroupId in knownUserAndGroupIds)
				if (null == this.FileHandlerFactoryLocator.UserManagerHandler.GetUserOrGroupNoException(userOrGroupId))
					missingUserAndGroupIds.Add(userOrGroupId);
			
			if (missingUserAndGroupIds.Count > 0)
				this.persistedFileInformations.Write(fileInformations =>
		        {
					foreach (var fileInformation in fileInformations.Values)
						foreach (var userOrGroupId in missingUserAndGroupIds)
						{
							fileInformation.namedPermissions.Remove(userOrGroupId);
							fileInformation.permissions.Remove(userOrGroupId);
						}
				});
		}
		
		/*// <summary>
		/// Cleans up dead permissions. Runs on a background thread because it relies on complete initialization
		/// </summary>
		private void RemoveDeadPermissions()
		{
			HashSet<ID<IUserOrGroup, Guid>> missingUserAndGroupIds;
			
			do
			{
				missingUserAndGroupIds = new HashSet<ID<IUserOrGroup, Guid>>();
	
				// Clean up permissions for deleted users
				// This really should be event-driven
				this.persistedFileInformations.Read(fileInformations =>
				{
					foreach (var fileInformation in fileInformations.Values)
						foreach (var userOrGroupId in fileInformation.permissions.Keys.Union(
							fileInformation.namedPermissions.Keys))
						{
							if (!allUserIds.Contains(userOrGroupId))
								missingUserAndGroupIds.Add(userOrGroupId);
						}
				});

				allUserIds = this.FileHandlerFactoryLocator.UserManagerHandler.GetAllLocalUserIds().ToHashSet();
			} while (allUserIds.ContainsAny(missingUserAndGroupIds));
			
			if (missingUserAndGroupIds.Count > 0)
				this.persistedFileInformations.Write(fileInformations =>
		        {
					foreach (var fileInformation in fileInformations.Values)
						foreach (var userOrGroupId in missingUserAndGroupIds)
						{
							fileInformation.namedPermissions.Remove(userOrGroupId);
							fileInformation.permissions.Remove(userOrGroupId);
						}
				});
		}*/

        public override void CopyFile(IFileHandler sourceFileHandler, IFileId fileId, ID<IUserOrGroup, Guid>? ownerID, IDirectoryHandler parentDirectory)
        {
            using (IDirectoryHandler target = OpenFile(fileId))
            {
				target.FileContainer = new FileContainer(
					target, fileId, "directory", parentDirectory, this.FileHandlerFactoryLocator, DateTime.UtcNow);
			
                foreach (IFileContainer toCopy in ((IDirectoryHandler)sourceFileHandler).Files)
                    try
                    {
                        target.CopyFile(null, toCopy, toCopy.Filename, ownerID);
                    }
                    // If the user doesn't have permission, just bypass
                    catch (SecurityException) { }
            }
        }

        public override void RestoreFile(IFileId fileId, string pathToRestoreFrom, ID<IUserOrGroup, Guid> userId, IDirectoryHandler parentDirectory)
        {
            this.CreateFile(fileId);
            using (IDirectoryHandler target = this.OpenFile(fileId))
            {
				target.FileContainer = new FileContainer(target, fileId, "directory", parentDirectory, this.FileHandlerFactoryLocator, DateTime.UtcNow);
				
                string metadataPath = Path.GetFullPath(pathToRestoreFrom + Path.DirectorySeparatorChar + "metadata.xml");

                using (TextReader tr = File.OpenText(metadataPath))
                using (XmlReader xmlReader = XmlReader.Create(tr))
                {
                    xmlReader.MoveToContent();

                    target.IndexFile = xmlReader.GetAttribute("IndexFile");

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
                                    ownerId = userId;

                                target.RestoreFile(
                                    filename,
									typeId,
									Path.Combine(pathToRestoreFrom, filename),
									ownerId.Value);
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

                                target.SetPermission(
                                    null,
                                    filename,
                                    new ID<IUserOrGroup, Guid>[] {userOrGroupId},
                                    level,
                                    inherit,
                                    sendNotifications);
                            }
                }
            }
        }
		
		/// <summary>
		/// Represents a relationship while loading
		/// </summary>
		private struct Relationship
		{
			public DirectoryHandler.FileInformation relatedFile;
			public FileId parentFileId;
			public string name;
			public bool inherit;
		}
		
		/// <summary>
		/// Deserializes all metadata about the filesystem
		/// </summary>
		private Dictionary<IFileId, DirectoryHandler.FileInformation> Deserialize(Stream readStream)
		{
			var fileInformations = new Dictionary<IFileId, DirectoryHandler.FileInformation>();
			
			// version
			readStream.Read<int>();
			
			var relationships = new List<Relationship>(readStream.Read<int>());
			
			this.ReadDirectoryInformation(fileInformations, readStream, relationships);
			
			foreach (var relationship in relationships)
			{
				var parentFile = fileInformations[relationship.parentFileId];
				var relatedFile = relationship.relatedFile;
				
				HashSet<string> names;
				if (!parentFile.relationships.TryGetValue(relatedFile, out names))
				{
					parentFile.relationships[relatedFile] = names = new HashSet<string>();
				}
				names.Add(relationship.name);
				
				Dictionary<string, bool> namesAndInherit;				
				if (!relatedFile.parentRelationships.TryGetValue(parentFile, out namesAndInherit))
				{
					relatedFile.parentRelationships[parentFile] = namesAndInherit = new Dictionary<string, bool>();
				}
				namesAndInherit[relationship.name] = relationship.inherit;
			}
			
			return fileInformations;
		}
		
		/// <summary>
		/// Deserializes a directory from the read stream, including its files and its subdirectories
		/// </summary>
		private DirectoryHandler.DirectoryInformation ReadDirectoryInformation(
			Dictionary<IFileId, DirectoryHandler.FileInformation> fileInformations,
			Stream readStream,
			List<Relationship> relationships)
		{
			var directoryInformation = new DirectoryHandler.DirectoryInformation();
			
			this.ReadFileInformation(fileInformations, readStream, relationships, directoryInformation);
			
			directoryInformation.indexFile = readStream.ReadString();

			var numFiles = readStream.Read<int>();
			for (var ctr = 0; ctr < numFiles; ctr++)
			{
				DirectoryHandler.FileInformation fileInformation;
				
				// if is a directory...
				if (readStream.Read<bool>())
					fileInformation = this.ReadDirectoryInformation(fileInformations, readStream, relationships);
				else
					fileInformation = this.ReadFileInformation(fileInformations, readStream, relationships);
				
				directoryInformation.files[fileInformation.filename] = fileInformation;
			}
			
			return directoryInformation;
		}
		
		/// <summary>
		/// Reads a FileInformation from a stream.
		/// </summary>
		private DirectoryHandler.FileInformation ReadFileInformation (
			Dictionary<IFileId, DirectoryHandler.FileInformation> fileInformations,
			Stream readStream,
			List<Relationship> relationships)
		{
			var fileInformation = new DirectoryHandler.FileInformation();
			this.ReadFileInformation(fileInformations, readStream, relationships, fileInformation);
			
			return fileInformation;
		}
		
		/// <summary>
		/// Reads a FileInformation from a stream.
		/// </summary>
		private void ReadFileInformation (
			Dictionary<IFileId, DirectoryHandler.FileInformation> fileInformations,
			Stream readStream,
			List<Relationship> relationships,
			DirectoryHandler.FileInformation fileInformation)
		{
			fileInformation.fileId = new FileId(readStream.Read<long>());
			fileInformation.typeId = readStream.ReadString();
			fileInformation.filename = readStream.ReadString();
			fileInformation.ownerId = readStream.ReadNullable<ID<IUserOrGroup, Guid>>();
			fileInformation.created = readStream.ReadDateTime();
			fileInformation.permissions = new Dictionary<ID<IUserOrGroup, Guid>, DirectoryHandler.Permission>();
			fileInformation.namedPermissions = new Dictionary<ID<IUserOrGroup, Guid>, Dictionary<string, bool>>();
			fileInformation.relationships = new Dictionary<DirectoryHandler.FileInformation, HashSet<string>>();
			fileInformation.parentRelationships = new Dictionary<DirectoryHandler.FileInformation, Dictionary<string, bool>>();
			
			int numPermissions = readStream.Read<int>();
			for (var ctr = 0; ctr < numPermissions; ctr++)
				fileInformation.permissions[readStream.Read<ID<IUserOrGroup, Guid>>()] =
					new DirectoryHandler.Permission()
					{
						level = (FilePermissionEnum)readStream.Read<int>(),
						inherit = readStream.Read<bool>(),
						sendNotifications = readStream.Read<bool>()
					};
			
			int numNamedPermissions = readStream.Read<int>();
			for (var ctr = 0; ctr < numNamedPermissions; ctr++)
			{
				var numNamedPermissionsForUser = readStream.Read<int>();
				var namedPermissions = new Dictionary<string, bool>();
				
				fileInformation.namedPermissions[readStream.Read<ID<IUserOrGroup, Guid>>()] = namedPermissions;
				
				for (var npCtr = 0; npCtr < numNamedPermissionsForUser; npCtr++)
					namedPermissions[readStream.ReadString()] = readStream.Read<bool>();
			}
			
			int numRelationships = readStream.Read<int>();
			for (var ctr = 0; ctr < numRelationships; ctr++)
			{
				var parentFileId = readStream.Read<FileId>();
				var numRelationshipsForParentFile = readStream.Read<int>();
				
				for (var relCtr = 0; relCtr < numRelationshipsForParentFile; relCtr++)
					relationships.Add(new Relationship()
					{
						parentFileId = parentFileId,
						name = readStream.ReadString(),
						inherit = readStream.Read<bool>(),
						relatedFile = fileInformation
					});				
			}
			
			fileInformations[fileInformation.fileId] = fileInformation;
		}
		
		private void Serialize(Stream writeStream, Dictionary<IFileId, DirectoryHandler.FileInformation> fileInformations)
		{
			// The version
			writeStream.Write(0);
			
			// The total number of relationships. Keeps memory allocations lower
			int numRelationships = fileInformations.Values.Sum(fileInformation => fileInformation.relationships.Values.Sum(relationships => relationships.Count));
			writeStream.Write(numRelationships);
			
			this.SerializeDirectory(
				writeStream,
				(DirectoryHandler.DirectoryInformation)fileInformations[this.FileHandlerFactoryLocator.FileSystem.RootDirectoryId]);
		}
		
		private void SerializeDirectory(Stream writeStream, DirectoryHandler.DirectoryInformation directoryInformation)
		{
			this.SerializeFile(writeStream, directoryInformation);
			
			writeStream.Write(directoryInformation.indexFile);
			
			writeStream.Write(directoryInformation.files.Count);
			foreach (var subFileInformation in directoryInformation.files.Values)
				if (subFileInformation is DirectoryHandler.DirectoryInformation)
				{
					writeStream.Write(true);
					this.SerializeDirectory(writeStream, (DirectoryHandler.DirectoryInformation)subFileInformation);
				}
				else
				{
					writeStream.Write(false);
					this.SerializeFile(writeStream, subFileInformation);
				}
		}
		
		private void SerializeFile(Stream writeStream, DirectoryHandler.FileInformation fileInformation)
		{
			writeStream.Write(fileInformation.fileId);
			writeStream.Write(fileInformation.typeId);
			writeStream.Write(fileInformation.filename);
			writeStream.WriteNullable(fileInformation.ownerId);
			writeStream.Write(fileInformation.created);
			
			// Permissions
			writeStream.Write(fileInformation.permissions.Count);
			foreach (var permissionKVP in fileInformation.permissions)
			{
				writeStream.Write(permissionKVP.Key);
				
				var permission = permissionKVP.Value;
				writeStream.Write((int)permission.level);
				writeStream.Write(permission.inherit);
				writeStream.Write(permission.sendNotifications);
			}
			
			writeStream.Write(fileInformation.namedPermissions.Count);
			foreach (var namedPermissionKVP in fileInformation.namedPermissions)
			{
				var namedPermissionsForUser = namedPermissionKVP.Value;
				writeStream.Write(namedPermissionsForUser.Count);
				writeStream.Write(namedPermissionKVP.Key);
				
				foreach (var namedPermissionForUserKVP in namedPermissionsForUser)
				{
					writeStream.Write(namedPermissionForUserKVP.Key);
					writeStream.Write(namedPermissionForUserKVP.Value);
				}
			}
			
			writeStream.Write(fileInformation.parentRelationships.Count);
			foreach (var parentRelationshipKVP in fileInformation.parentRelationships)
			{
				var parentFile = parentRelationshipKVP.Key;
				writeStream.Write(parentFile.fileId);
				
				var relationshipsAndInhert = parentRelationshipKVP.Value;
				writeStream.Write(relationshipsAndInhert.Count);
				
				foreach (var relationshipAndInhert in relationshipsAndInhert)
				{
					writeStream.Write(relationshipAndInhert.Key);
					writeStream.Write(relationshipAndInhert.Value);
				}
			}
		}
	}
}
