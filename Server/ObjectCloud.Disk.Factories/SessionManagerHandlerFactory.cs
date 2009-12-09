// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
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
        /// <summary>
        /// Service locator for data access objects
        /// </summary>
        public DataAccessLocator DataAccessLocator
        {
            get { return _DataAccessLocator; }
            set { _DataAccessLocator = value; }
        }
        private DataAccessLocator _DataAccessLocator;

        public override ISessionManagerHandler CreateFile(string path)
        {
            Directory.CreateDirectory(path);

            string databaseFilename = CreateDatabaseFilename(path);

            DataAccessLocator.DatabaseCreator.Create(databaseFilename);

            return new SessionManagerHandler(CreateDatabaseConnector(databaseFilename), FileHandlerFactoryLocator);
        }

        public override ISessionManagerHandler OpenFile(string path)
        {
            string databaseFilename = CreateDatabaseFilename(path);

            return new SessionManagerHandler(CreateDatabaseConnector(databaseFilename), FileHandlerFactoryLocator);
        }

        /// <summary>
        /// Creates the database file name
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private string CreateDatabaseFilename(string path)
        {
            return string.Format("{0}{1}db.sqlite", path, Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// Creates a database connector given a path
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private IDatabaseConnector CreateDatabaseConnector(string path)
        {
            return DataAccessLocator.DatabaseConnectorFactory.CreateConnectorForEmbedded(path);
        }

        public override IFileHandler CopyFile(IFileHandler sourceFileHandler, ID<IFileContainer, long> fileId, ID<IUserOrGroup, Guid>? ownerID)
        {
            throw new NotImplementedException();
        }

        public override IFileHandler RestoreFile(ID<IFileContainer, long> fileId, string pathToRestoreFrom, ID<IUserOrGroup, Guid> userId)
        {
            throw new NotImplementedException();
        }
    }
}