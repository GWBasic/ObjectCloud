// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Data.Common;
using System.IO;

using ObjectCloud.CallHomePlugin.DataAccess;
using ObjectCloud.CallHomePlugin.DataAccessBase;
using ObjectCloud.Common;
using ObjectCloud.Disk.Factories;
using ObjectCloud.Disk.FileHandlers;
using ObjectCloud.Interfaces.Database;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.ORM.DataAccess.SQLite;

namespace ObjectCloud.CallHomePlugin
{
    public class CallHomeFileHandlerFactory : FileHandlerFactory<CallHomeFileHandler>
    {
        /// <summary>
        /// Service locator for data access objects
        /// </summary>
        public DataAccessLocator DataAccessLocator
        {
            get { return _DataAccessLocator; }
            set { _DataAccessLocator = value; }
        }
        private DataAccessLocator _DataAccessLocator;

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

            string databaseFilename = CreateDatabaseFilename(path);

            DataAccessLocator.DatabaseCreator.Create(databaseFilename);
        }

        /// <summary>
        /// Creates the database file name
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        internal static string CreateDatabaseFilename(string path)
        {
            return string.Format("{0}{1}db.sqlite", path, Path.DirectorySeparatorChar);
        }

        public override CallHomeFileHandler OpenFile(string path, FileId fileId)
        {
            string databaseFilename = CreateDatabaseFilename(path);

            return new CallHomeFileHandler(
                DataAccessLocator.DatabaseConnectorFactory.CreateConnectorForEmbedded(databaseFilename),
                FileHandlerFactoryLocator);
        }
    }
}
