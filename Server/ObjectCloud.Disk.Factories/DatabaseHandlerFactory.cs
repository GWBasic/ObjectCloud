// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Data.Common;
using System.IO;

using ObjectCloud.Common;
using ObjectCloud.Disk.FileHandlers;
using ObjectCloud.Interfaces.Database;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.ORM.DataAccess.SQLite;

namespace ObjectCloud.Disk.Factories
{
	public class DatabaseHandlerFactory : FileHandlerFactory<IDatabaseHandler>
	{
        /// <summary>
        /// The embedded database connector, wired through Spring
        /// </summary>
        public IEmbeddedDatabaseConnector EmbeddedDatabaseConnector
        {
            get { return _EmbeddedDatabaseConnector; }
            set { _EmbeddedDatabaseConnector = value; }
        }
        private IEmbeddedDatabaseConnector _EmbeddedDatabaseConnector;

        public override void CreateFile(string path, FileId fileId)
        {
            Directory.CreateDirectory(path);

            string databaseFilename = CreateDatabaseFilename(path);
			
			EmbeddedDatabaseConnector.CreateFile(databaseFilename);

            using (DatabaseHandler toReturn = new DatabaseHandler(databaseFilename, EmbeddedDatabaseConnector, FileHandlerFactoryLocator))
			    toReturn.Version = null;
        }

        public override IDatabaseHandler OpenFile(string path, FileId fileId)
        {
            string databaseFilename = CreateDatabaseFilename(path);

            return new DatabaseHandler(databaseFilename, EmbeddedDatabaseConnector, FileHandlerFactoryLocator);
        }

        /// <summary>
        /// Creates the database file name
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private string CreateDatabaseFilename(string path)
        {
            return string.Format("{0}{1}embedded.sqlite", path, Path.DirectorySeparatorChar);
        }

        public override void CopyFile(IFileHandler sourceFileHandler, IFileId fileId, ID<IUserOrGroup, Guid>? ownerID, IDirectoryHandler parentDirectory)
		{
            string path = FileSystem.GetFullPath(fileId);

			// This would be so much better if SqlLite allowed dumping to SQL
			DatabaseHandler sourceDatabaseHandler = (DatabaseHandler)sourceFileHandler;

            Directory.CreateDirectory(path);
			
			File.Copy(sourceDatabaseHandler.DatabaseFilename, CreateDatabaseFilename(path));
			
            using (IDatabaseHandler toReturn = OpenFile(fileId))
                toReturn.Version = sourceDatabaseHandler.Version;
		}

        public override void RestoreFile(IFileId fileId, string pathToRestoreFrom, ID<IUserOrGroup, Guid> userId, IDirectoryHandler parentDirectory)
		{
            string path = FileSystem.GetFullPath(fileId);

            Directory.CreateDirectory(path);

			File.Copy(pathToRestoreFrom, CreateDatabaseFilename(path));
		}
    }
}
