// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.IO;
using System.Xml;

using ObjectCloud.Common;
using ObjectCloud.DataAccess.Directory;
using ObjectCloud.Disk.FileHandlers;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Disk.Factories
{
    public abstract class CometHandlerFactory : FileHandlerFactory<ICometHandler>
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

        public override ICometHandler CreateFile(string path)
        {
            Directory.CreateDirectory(path);

            string databaseFilename = DirectoryHandlerFactory.CreateDatabaseFilename(path);

            DataAccessLocator.DatabaseCreator.Create(databaseFilename);

            CometHandler toReturn = new CometHandler(
                DirectoryHandlerFactory.CreateDatabaseConnector(databaseFilename, DataAccessLocator),
                FileHandlerFactoryLocator,
                CallOnNewSession);

            toReturn.CreateFile("comet", "cometcomet", null);
            toReturn.CreateFile("handshake", "comethandshake", null);
            toReturn.CreateFile("close", "cometclose", null);
            toReturn.CreateFile("send", "cometsend", null);
            toReturn.CreateFile("reflect", "cometreflect", null);
            toReturn.CreateFile("static", "directory", null);
            toReturn.CreateFile("streamtest", "cometstreamtest", null);

            return toReturn;
        }

        public override ICometHandler OpenFile(string path)
        {
            string databaseFilename = DirectoryHandlerFactory.CreateDatabaseFilename(path);

            return new CometHandler(
                DirectoryHandlerFactory.CreateDatabaseConnector(databaseFilename, DataAccessLocator),
                FileHandlerFactoryLocator,
                CallOnNewSession);
        }

        /// <summary>
        /// Called on a new session
        /// </summary>
        public abstract GenericArgument<ICometSession> CallOnNewSession { get; }

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