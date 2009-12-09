// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
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
        public override UserHandler CreateFile(string path)
        {
            throw new SecurityException("Users can not be created");
        }

        public override IFileHandler CreateSystemFile(string path)
        {
            Directory.CreateDirectory(path);

            string databaseFilename = CreateDatabaseFilename(path);

            DataAccessLocator.DatabaseCreator.Create(databaseFilename);

            return ConstructNameValuePairsHander(databaseFilename);
        }

        /// <summary>
        /// Service locator for data access objects
        /// </summary>
        public DataAccessLocator DataAccessLocator
        {
            get { return _DataAccessLocator; }
            set { _DataAccessLocator = value; }
        }
        private DataAccessLocator _DataAccessLocator;

        public override UserHandler OpenFile(string path)
        {
            return ConstructNameValuePairsHander(CreateDatabaseFilename(path));
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
        /// Constructs the NameValuePairsHandler to return
        /// </summary>
        /// <param name="databaseFilename"></param>
        /// <returns></returns>
        private UserHandler ConstructNameValuePairsHander(string databaseFilename)
        {
            UserHandler toReturn = new UserHandler(
                DataAccessLocator.DatabaseConnectorFactory.CreateConnectorForEmbedded(databaseFilename),
                FileHandlerFactoryLocator);

            return toReturn;
        }

        public override IFileHandler CopyFile(IFileHandler sourceFileHandler, ID<IFileContainer, long> fileId, ID<IUserOrGroup, Guid>? ownerID)
        {
            throw new NotImplementedException("Users can not be copied");
        }

        public override IFileHandler RestoreFile(ID<IFileContainer, long> fileId, string pathToRestoreFrom, ID<IUserOrGroup, Guid> userId)
        {
            throw new NotImplementedException("Users can not be copied");
        }
    }
}
