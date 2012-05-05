// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

using ObjectCloud.Common;
using ObjectCloud.DataAccess.SessionManager;
using ObjectCloud.Disk.FileHandlers;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Disk.Factories
{
    public class SessionManagerHandlerFactory : FileHandlerFactory<ISessionManagerHandler>
    {
        public override void CreateFile(string path, FileId fileId)
        {
            Directory.CreateDirectory(path);
			var databaseFilename = this.CreateDatabaseFilename(path);
			
			// Create an empty persisted session object file
			new PersistedBinaryFormatterObject<Dictionary<ID<ISession, Guid>, SessionData>>(databaseFilename, new Dictionary<ID<ISession, Guid>, SessionData>());
        }

        public override ISessionManagerHandler OpenFile(string path, FileId fileId)
        {
			var databaseFilename = this.CreateDatabaseFilename(path);
			return new SessionManagerHandler(this.FileHandlerFactoryLocator, databaseFilename);
        }
		        
		/// <summary>
        /// Creates the database file name
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private string CreateDatabaseFilename(string path)
        {
            return string.Format("{0}{1}persistedsessions", path, Path.DirectorySeparatorChar);
        }

        public override void CopyFile(IFileHandler sourceFileHandler, IFileId fileId, ID<IUserOrGroup, Guid>? ownerID, IDirectoryHandler parentDirectory)
        {
            throw new NotImplementedException();
        }

        public override void RestoreFile(IFileId fileId, string pathToRestoreFrom, ID<IUserOrGroup, Guid> userId, IDirectoryHandler parentDirectory)
        {
            throw new NotImplementedException();
        }
    }
}