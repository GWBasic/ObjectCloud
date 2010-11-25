// Copyright 2009, 2010 Andrew Rondeau
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
        /// <summary>
        /// Service locator for data access objects
        /// </summary>
        public DataAccessLocator DataAccessLocator
        {
            get { return _DataAccessLocator; }
            set { _DataAccessLocator = value; }
        }
        private DataAccessLocator _DataAccessLocator;

        public override void CreateSystemFile(string path, FileId fileId)
        {
            Directory.CreateDirectory(path);

            string databaseFilename = CreateDatabaseFilename(path);

            DataAccessLocator.DatabaseCreator.Create(databaseFilename);
        }

        public override UserManagerHandler OpenFile(string path, FileId fileId)
        {
            string databaseFilename = CreateDatabaseFilename(path);

            return new UserManagerHandler(CreateDatabaseConnector(databaseFilename), FileHandlerFactoryLocator, MaxLocalUsers);
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
