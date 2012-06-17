// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

using ObjectCloud.Common;
using ObjectCloud.DataAccess.UserManager;
using ObjectCloud.Disk.FileHandlers;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.Factories
{
    public class UserManagerHandlerFactory : SystemFileHandlerFactory<UserManagerHandler>
    {

        public override void CreateSystemFile(string path, FileId fileId)
        {
            Directory.CreateDirectory(path);
			var databaseFilename = this.CreateDatabaseFilename(path);
			this.ConstructUserManagerHandler(databaseFilename);
        }

        public override UserManagerHandler OpenFile(string path, FileId fileId)
        {
			var databaseFilename = this.CreateDatabaseFilename(path);
			return this.ConstructUserManagerHandler(databaseFilename);
		}
		
		private UserManagerHandler ConstructUserManagerHandler(string databaseFilename)
		{
			var persistedUserManagerData = new PersistedBinaryFormatterObject<UserManagerHandler.UserManagerData>(databaseFilename);
        	return new UserManagerHandler(persistedUserManagerData, this.FileHandlerFactoryLocator, this.MaxLocalUsers);
		}

        /// <summary>
        /// Creates the database file name
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private string CreateDatabaseFilename(string path)
        {
            return string.Format("{0}{1}users", path, Path.DirectorySeparatorChar);
        }

        public override void CopyFile(IFileHandler sourceFileHandler, IFileId fileId, ID<IUserOrGroup, Guid>? ownerID, IDirectoryHandler parentDirectory)
        {
            throw new NotImplementedException("A UserManager can not be copied");
        }

        public override void RestoreFile(IFileId fileId, string pathToRestoreFrom, ID<IUserOrGroup, Guid> userId, IDirectoryHandler parentDirectory)
        {
            throw new NotImplementedException();
            /*IUserManagerHandler toReturn = CreateFile(fileId);

            using (XmlReader xmlReader = XmlReader.Create(pathToRestoreFrom))
            {
                xmlReader.MoveToContent();

                toReturn.Restore(xmlReader, userId);
            }

            return toReturn;*/
        }

        /// <summary>
        /// The maximum number of local users allowed in the database
        /// </summary>
        public int? MaxLocalUsers { get; set; }
    }
}
