// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Text;
using System.Threading;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.Common.Threading;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.ORM.DataAccess;

namespace ObjectCloud.Disk.FileHandlers
{
    /// <summary>
    /// Base class for file handlers that keep a database connection open during their lifetime.  Automatically closes the connection
    /// when the object is finalized
    /// </summary>
    /// <typeparam name="TDatabaseConnection"></typeparam>
    /// <typeparam name="TDatabaseTransaction"></typeparam>
    public abstract class HasDatabaseFileHandler<TDatabaseConnector, TDatabaseConnection, TDatabaseTransaction> : LastModifiedFileHandler, IDatabaseHandler
        where TDatabaseConnector : IDatabaseConnector<TDatabaseConnector, TDatabaseConnection, TDatabaseTransaction>
        where TDatabaseConnection : IDatabaseConnection<TDatabaseTransaction>
        where TDatabaseTransaction : IDatabaseTransaction
    {
        ILog log = LogManager.GetLogger(typeof(HasDatabaseFileHandler<,,>));

        public HasDatabaseFileHandler(TDatabaseConnector databaseConnector, FileHandlerFactoryLocator fileHandlerFactoryLocator)
            : base(fileHandlerFactoryLocator, null) 
        {
            _DatabaseConnector = databaseConnector;
        }

        public override DateTime LastModified
        {
            get { return DatabaseConnector.LastModified; }
        }

        public TDatabaseConnector DatabaseConnector
        {
            get { return _DatabaseConnector; }
        }
        private readonly TDatabaseConnector _DatabaseConnector;

        /// <summary>
        /// The timer
        /// </summary>
        private Timer Timer;

        /// <summary>
        /// The time that the connection was last accessed
        /// </summary>
        DateTime ConnectionLastAccessed;

        /// <summary>
        /// Used to syncronize deleting old connections
        /// </summary>
        object ConnectionAccessLock = new object();

        /// <summary>
        /// The database connection
        /// </summary>
        public TDatabaseConnection DatabaseConnection
        {
            get 
            {
                using (TimedLock.Lock(ConnectionAccessLock))
                {
                    ConnectionLastAccessed = DateTime.UtcNow;

                    if (null == _DatabaseConnection)
                    {
                        Timer = new Timer(DeleteConnectionIfNeeded, null, 5000, 5000);
                        _DatabaseConnection = DatabaseConnector.Connect();
                        _DatabaseConnection.DbConnection.StateChange += new System.Data.StateChangeEventHandler(DbConnection_StateChange);
                        HaveOpenConnection.Add(this);
                    }

                    return _DatabaseConnection;
                }
            }
        }
        private TDatabaseConnection _DatabaseConnection = default(TDatabaseConnection);

        /// <summary>
        /// All of the objects with an open database connection, this prevents them from being garbage collected
        /// </summary>
        private static HashSet<HasDatabaseFileHandler<TDatabaseConnector, TDatabaseConnection, TDatabaseTransaction>> 
            HaveOpenConnection = new HashSet<HasDatabaseFileHandler<TDatabaseConnector, TDatabaseConnection, TDatabaseTransaction>>();

        void DbConnection_StateChange(object sender, System.Data.StateChangeEventArgs e)
        {
            if ((System.Data.ConnectionState.Broken == e.CurrentState) || (System.Data.ConnectionState.Closed == e.CurrentState))
                using (TimedLock.Lock(ConnectionAccessLock))
                {
                    _DatabaseConnection.DbConnection.StateChange -= new System.Data.StateChangeEventHandler(DbConnection_StateChange);
                    _DatabaseConnection = default(TDatabaseConnection);
                    HaveOpenConnection.Remove(this);
                }
        }

        /// <summary>
        /// Disposes the DatabaseConnection if it hasn't been accessed in 15 seconds
        /// </summary>
        /// <param name="state"></param>
        public void DeleteConnectionIfNeeded(object state)
        {
            try
            {
                using (TimedLock.Lock(ConnectionAccessLock))
                    if (null != _DatabaseConnection)
                    {
                        if (ConnectionLastAccessed.AddSeconds(15) <= DateTime.UtcNow)
                        {
                            // Don't close the database on a long-running transaction
                            object toMonitor = _DatabaseConnection.DbConnection;
                            if (!Monitor.TryEnter(toMonitor))
                                return;

                            try
                            {
                                _DatabaseConnection.Dispose();
                                _DatabaseConnection = default(TDatabaseConnection);

                                if (null != Timer)
                                {
                                    Timer.Dispose();
                                    Timer = null;
                                }
                            }
                            finally
                            {
                                Monitor.Exit(toMonitor);
                            }
                        }
                    }
                    else
                        if (null != Timer)
                        {
                            Timer.Dispose();
                            Timer = null;
                        }
            }
            catch (Exception e)
            {
                log.Warn("Exception while trying to see if the database needs to be closed", e);
            }
        }

        /// <summary>
        /// Closes the connection
        /// </summary>
        ~HasDatabaseFileHandler()
        {
            Dispose();
        }

        public override void Dispose()
        {
            try
            {
                using (TimedLock.Lock(ConnectionAccessLock))
                    if (null != _DatabaseConnection)
                    {
                        //_DatabaseConnection.DbConnection.Close();
                        _DatabaseConnection.Dispose();
                        _DatabaseConnection = default(TDatabaseConnection);
                    }

                GC.SuppressFinalize(this);
            }
            finally
            {
                base.Dispose();
            }
        }

        public override void OnDelete(ObjectCloud.Interfaces.Security.IUser changer)
        {
            Dispose();

            base.OnDelete(changer);
        }

        public override void SyncFromLocalDisk(string localDiskPath, bool force)
        {
            using (TimedLock.Lock(this))
            {
                DateTime authoritativeCreated = File.GetLastWriteTimeUtc(localDiskPath);
                DateTime thisCreated = DatabaseConnector.LastModified;

                if (authoritativeCreated > thisCreated  || force)
                    DatabaseConnector.Restore(localDiskPath);
            }
        }

        public DbConnection Connection
        {
            get { return DatabaseConnection.DbConnection; }
        }

        public double? Version
        {
            get { return null; }
            set { throw new NotImplementedException("Setting the version isn't supported for this file type"); }
        }
		
		public override void Vacuum()
		{
            log.Info("Vacuuming " + FileContainer.FullPath);

			DatabaseConnection.Vacuum();

            log.Info("Finished Vacuuming " + FileContainer.FullPath);
            
            base.Vacuum();
		}
    }
}
