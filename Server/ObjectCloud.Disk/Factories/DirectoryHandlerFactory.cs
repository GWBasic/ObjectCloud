// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
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
							metadataLocation, () => new Dictionary<IFileId, DirectoryHandler.FileInformation>());
					}
			
			return new DirectoryHandler(this.persistedFileInformations, this.FileHandlerFactoryLocator);
        }

        public override void CopyFile(IFileHandler sourceFileHandler, IFileId fileId, ID<IUserOrGroup, Guid>? ownerID, IDirectoryHandler parentDirectory)
        {
            using (IDirectoryHandler target = OpenFile(fileId))
            {
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
            CreateFile(fileId);
            using (IDirectoryHandler target = OpenFile(fileId))
            {
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
                                    filename, typeId, pathToRestoreFrom + Path.DirectorySeparatorChar + filename, ownerId.Value);
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
