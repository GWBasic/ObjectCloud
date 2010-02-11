// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
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

		public override IDatabaseHandler CreateFile(string path)
        {
            Directory.CreateDirectory(path);

            string databaseFilename = CreateDatabaseFilename(path);
			
			EmbeddedDatabaseConnector.CreateFile(databaseFilename);

            DatabaseHandler toReturn = new DatabaseHandler(databaseFilename, EmbeddedDatabaseConnector, FileHandlerFactoryLocator);
			toReturn.Version = null;
			
			return toReturn;
        }

        public override IDatabaseHandler OpenFile(string path)
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

        public override IFileHandler CopyFile(IFileHandler sourceFileHandler, ID<IFileContainer, long> fileId, ID<IUserOrGroup, Guid>? ownerID)
		{
            string path = FileSystem.GetFullPath(fileId);

			// This would be so much better if SqlLite allowed dumping to SQL
			DatabaseHandler sourceDatabaseHandler = (DatabaseHandler)sourceFileHandler;

            Directory.CreateDirectory(path);
			
			File.Copy(sourceDatabaseHandler.DatabaseFilename, CreateDatabaseFilename(path));
			
            IDatabaseHandler toReturn = OpenFile(path);
            toReturn.Version = sourceDatabaseHandler.Version;

            return toReturn;
		}

        public override IFileHandler RestoreFile(ID<IFileContainer, long> fileId, string pathToRestoreFrom, ID<IUserOrGroup, Guid> userId)
		{
            string path = FileSystem.GetFullPath(fileId);

            Directory.CreateDirectory(path);

			File.Copy(pathToRestoreFrom, CreateDatabaseFilename(path));
			return OpenFile(path);
		}
    }
}
