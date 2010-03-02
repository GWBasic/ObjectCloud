using System;
using System.IO;

using ObjectCloud.Common;
using ObjectCloud.DataAccess.Log;
using ObjectCloud.Disk.FileHandlers;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Disk.Factories
{
	public class LogHandlerFactory : FileHandlerFactory<LogHandler>
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

        /// <summary>
        /// Enables writing to the console.  Defaults to false
        /// </summary>
        public bool WriteToConsole
        {
            get { return _WriteToConsole; }
            set { _WriteToConsole = value; }
        }
        private bool _WriteToConsole = false;

        public override void CreateFile(string path, FileId fileId)
        {
            Directory.CreateDirectory(path);

            string databaseFilename = CreateDatabaseFilename(path);

            DataAccessLocator.DatabaseCreator.Create(databaseFilename);

            using (LogHandler logHandler = ConstructLogHander(databaseFilename))
                foreach (LoggingLevel level in Enum<LoggingLevel>.Values)
                    logHandler.DatabaseConnection.Lifespan.Insert(delegate(ILifespan_Writable lifespan)
                    {
                        lifespan.Level = level;
                        lifespan.Timespan = TimeSpan.FromDays(14);
                    });
        }

        public override LogHandler OpenFile(string path, FileId fileId)
        {
            return ConstructLogHander(CreateDatabaseFilename(path));
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
        /// Constructs the LogHandler to return
        /// </summary>
        /// <param name="databaseFilename"></param>
        /// <returns></returns>
        private LogHandler ConstructLogHander(string databaseFilename)
        {
            LogHandler toReturn = new LogHandler(
                DataAccessLocator.DatabaseConnectorFactory.CreateConnectorForEmbedded(databaseFilename),
                FileHandlerFactoryLocator,
                WriteToConsole);

            return toReturn;
        }

        public override void CopyFile(IFileHandler sourceFileHandler, IFileId fileId, ID<IUserOrGroup, Guid>? ownerID)
        {
			throw new NotImplementedException();
        }

        public override void RestoreFile(IFileId fileId, string pathToRestoreFrom, ID<IUserOrGroup, Guid> userId)
        {
			throw new NotImplementedException();
        }
    }
}
