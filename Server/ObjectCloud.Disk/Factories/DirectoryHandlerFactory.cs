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
using ObjectCloud.DataAccess.Directory;
using ObjectCloud.Disk.FileHandlers;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Disk.Factories
{
    public class DirectoryHandlerFactory : FileHandlerFactory<IDirectoryHandler>
    {
		private PersistedBinaryFormatterObject<Dictionary<IFileId, DirectoryHandler.FileInformation>> persistedFileInformations = null;

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
				
						this.persistedFileInformations = new PersistedBinaryFormatterObject<Dictionary<IFileId, DirectoryHandler.FileInformation>>(
							metadataLocation, this.CreateInitialFileInformations);
				
						ThreadPool.QueueUserWorkItem(_ => this.RemoveDeadPermissions());
					}
			
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
			var allUserIds = this.FileHandlerFactoryLocator.UserManagerHandler.GetAllLocalUserIds().ToHashSet();
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
		}

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
    }
}
