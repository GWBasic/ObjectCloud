// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
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
        /// <summary>
        /// Service locator for data access objects
        /// </summary>
        public DataAccessLocator DataAccessLocator
        {
            get { return _DataAccessLocator; }
            set { _DataAccessLocator = value; }
        }
        private DataAccessLocator _DataAccessLocator;

        public override UserManagerHandler CreateFile(string path)
        {
            throw new SecurityException("UserManagerHandlers can not be created");
        }

        public override IFileHandler CreateSystemFile(string path)
        {
            Directory.CreateDirectory(path);

            string databaseFilename = CreateDatabaseFilename(path);

            DataAccessLocator.DatabaseCreator.Create(databaseFilename);

            return new UserManagerHandler(CreateDatabaseConnector(databaseFilename), FileHandlerFactoryLocator);
        }

        public override UserManagerHandler OpenFile(string path)
        {
            string databaseFilename = CreateDatabaseFilename(path);

            return new UserManagerHandler(CreateDatabaseConnector(databaseFilename), FileHandlerFactoryLocator);
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
            throw new NotImplementedException("A UserManager can not be copied");
        }

        public override IFileHandler RestoreFile(ID<IFileContainer, long> fileId, string pathToRestoreFrom, ID<IUserOrGroup, Guid> userId)
        {
            IUserManagerHandler toReturn = CreateFile(fileId);

            using (XmlReader xmlReader = XmlReader.Create(pathToRestoreFrom))
            {
                xmlReader.MoveToContent();

                toReturn.Restore(xmlReader, userId);
            }

            return toReturn;
        }
    }
}
