// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;

using ObjectCloud.Common;
using ObjectCloud.Disk.Factories;
using ObjectCloud.Disk.FileHandlers;
using ObjectCloud.Interfaces.Database;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.CallHomePlugin
{
    public class CallHomeFileHandlerFactory : FileHandlerFactory<CallHomeFileHandler>
    {
        public override void CopyFile(IFileHandler sourceFileHandler, IFileId fileId, ID<IUserOrGroup, Guid>? ownerID, IDirectoryHandler parentDirectory)
        {
            throw new NotImplementedException();
        }

        public override void RestoreFile(IFileId fileId, string pathToRestoreFrom, ID<IUserOrGroup, Guid> userId, IDirectoryHandler parentDirectory)
        {
            throw new NotImplementedException();
        }

        public override void CreateFile(string path, FileId fileId)
        {
            Directory.CreateDirectory(path);
			var databaseFilename = this.CreateDatabaseFilename(path);
			this.ConstructCallHomeFileHander(databaseFilename);
        }

        public override CallHomeFileHandler OpenFile(string path, FileId fileId)
        {
            Directory.CreateDirectory(path);
			var databaseFilename = this.CreateDatabaseFilename(path);
			return this.ConstructCallHomeFileHander(databaseFilename);
        }

        /// <summary>
        /// Creates the database file name
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private string CreateDatabaseFilename(string path)
        {
            return string.Format("{0}{1}callhome", path, Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// Constructs the NameValuePairsHandler to return
        /// </summary>
        /// <param name="databaseFilename"></param>
        /// <returns></returns>
        private CallHomeFileHandler ConstructCallHomeFileHander(string databaseFilename)
        {
			var persistedServers = new PersistedBinaryFormatterObject<Dictionary<string, CallHomeFileHandler.Server>>(
				databaseFilename,
				() => new Dictionary<string, CallHomeFileHandler.Server>());
			
			return new CallHomeFileHandler(persistedServers, this.FileHandlerFactoryLocator);
        }
    }
}
