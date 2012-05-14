// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.IO;
using System.Xml;

using ObjectCloud.Common;
using ObjectCloud.DataAccess.User;
using ObjectCloud.Disk.FileHandlers;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Disk.Factories
{
    public class UserHandlerFactory : SystemFileHandlerFactory<UserHandler>
    {
        public override void CreateSystemFile(string path, FileId fileId)
        {
            Directory.CreateDirectory(path);
			
			string notificationsPath = this.CreateNotificationsPath(path);
			Directory.CreateDirectory(notificationsPath);			
        }

        public override UserHandler OpenFile(string path, FileId fileId)
        {
			var databaseFilename = this.CreateDatabaseFilename(path);
			string notificationsPath = this.CreateNotificationsPath(path);
			
			return new UserHandler(
				new PersistedBinaryFormatterObject<UserHandler.UserData>(databaseFilename, () => new UserHandler.UserData()),
				new PersistedObjectSequence<UserHandler.Notification>(notificationsPath, 5 * 1024 * 1024, 1024 * 1024 * 1024, this.FileHandlerFactoryLocator),
				this.FileHandlerFactoryLocator);
        }

        /// <summary>
        /// Creates the database file name
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private string CreateDatabaseFilename(string path)
        {
			return Path.Combine(path, "namevaluepairs");
        }

        /// <summary>
        /// Creates the database file name
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private string CreateNotificationsPath(string path)
        {
			return Path.Combine(path, "notifications");
        }

        public override void CopyFile(IFileHandler sourceFileHandler, IFileId fileId, ID<IUserOrGroup, Guid>? ownerID, IDirectoryHandler parentDirectory)
        {
            throw new NotImplementedException("Users can not be copied");
        }

        public override void RestoreFile(IFileId fileId, string pathToRestoreFrom, ID<IUserOrGroup, Guid> userId, IDirectoryHandler parentDirectory)
        {
            throw new NotImplementedException("Users can not be copied");
        }
    }
}
