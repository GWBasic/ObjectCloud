// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.IO;

using ObjectCloud.Common;
using ObjectCloud.Common.Threading;
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
            set
            {
                _WriteToConsole = value;

                if (!value)
                    NonBlockingConsoleWriter.EndThread();
            }
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
            DelegateQueue delegateQueue = new DelegateQueue("Log Handler");
            DelegateQueues.Enqueue(delegateQueue);

            return new LogHandler(
                DataAccessLocator.DatabaseConnectorFactory.CreateConnectorForEmbedded(databaseFilename),
                FileHandlerFactoryLocator,
                WriteToConsole,
                delegateQueue);
        }

        public override void CopyFile(IFileHandler sourceFileHandler, IFileId fileId, ID<IUserOrGroup, Guid>? ownerID, IDirectoryHandler parentDirectory)
        {
			throw new NotImplementedException();
        }

        public override void RestoreFile(IFileId fileId, string pathToRestoreFrom, ID<IUserOrGroup, Guid> userId, IDirectoryHandler parentDirectory)
        {
			throw new NotImplementedException();
        }

        /// <summary>
        /// All of the started delegate queues
        /// </summary>
        private LockFreeQueue<DelegateQueue> DelegateQueues = new LockFreeQueue<DelegateQueue>();

        public override void Stop()
        {
            LockFreeQueue<DelegateQueue> delegateQueues = DelegateQueues;
            DelegateQueues = new LockFreeQueue<DelegateQueue>();

            DelegateQueue delegateQueue;
            while (delegateQueues.Dequeue(out delegateQueue))
            {
                delegateQueue.Stop();
                DelegateQueues.Enqueue(delegateQueue);
            }
        }
    }
}
