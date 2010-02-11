// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Threading;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Database;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.ORM.DataAccess.SQLite;

namespace ObjectCloud.Disk.FileHandlers
{
	public class DatabaseHandler : FileHandler, IDatabaseHandler
	{
        public DatabaseHandler(string databaseFilename, IEmbeddedDatabaseConnector embeddedDatabaseConnector, FileHandlerFactoryLocator fileHandlerFactoryLocator)
            : base(fileHandlerFactoryLocator, databaseFilename)
		{
			_DatabaseFilename = databaseFilename;
            _EmbeddedDatabaseConnector = embeddedDatabaseConnector;
		}

		/// <summary>
		/// The filename of the embedded database
		/// </summary>
		public string DatabaseFilename 
		{
			get { return _DatabaseFilename; }
		}		
		private string _DatabaseFilename;

        public IEmbeddedDatabaseConnector EmbeddedDatabaseConnector
        {
            get { return _EmbeddedDatabaseConnector; }
            set { _EmbeddedDatabaseConnector = value; }
        }
        private IEmbeddedDatabaseConnector _EmbeddedDatabaseConnector;

        public override void Dump(string path, ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, Guid> userId)
        {
            // This logic is to ensure that this call has exclusive access to the database file
            using (TimedLock.Lock(ConnectionAccessLock))
            {
                DateTime destinationCreated = DateTime.MinValue;

                if (File.Exists(path))
                    destinationCreated = File.GetLastWriteTimeUtc(path);

                DateTime thisCreated = File.GetLastWriteTimeUtc(_DatabaseFilename);

                if (destinationCreated < thisCreated)
                {
                    ConnectionLastAccessed = DateTime.MinValue;
                    DeleteConnectionIfNeeded(null);

                    if (File.Exists(path))
                        File.Delete(path);

                    File.Copy(_DatabaseFilename, path);
                }
            }
        }
		
		/// <value>
		/// This is stored by bit-converting a 32-bit float to an int and then shoving it into SQLite's user_version.  -Infinity implies null
		/// </value>
		public double? Version
		{
            get
            {
                DbCommand command = Connection.CreateCommand();
                command.CommandText = "PRAGMA user_version;";

                object versionObject = command.ExecuteScalar();
                int versionInt = Convert.ToInt32(versionObject);

                float version = BitConverter.ToSingle(BitConverter.GetBytes(versionInt), 0);

                if (float.IsNegativeInfinity(version))
                    return null;

                return version;
            }
			set
			{
				float toSet;
				if (null == value)
					toSet = float.NegativeInfinity;
				else
					toSet = Convert.ToSingle(value);

				int toSetInt = BitConverter.ToInt32(BitConverter.GetBytes(toSet), 0);
				
				DbCommand command = Connection.CreateCommand();
				command.CommandText = "PRAGMA user_version = " + toSetInt.ToString() + ";";
				
				command.ExecuteNonQuery();
			}
		}
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
        public DbConnection Connection
        {
            get 
            {
                using (TimedLock.Lock(ConnectionAccessLock))
                {
                    ConnectionLastAccessed = DateTime.UtcNow;

                    if (null == _Connection)
                    {
                        Timer = new Timer(DeleteConnectionIfNeeded, null, 30000, 30000);
                        _Connection = EmbeddedDatabaseConnector.OpenEmbedded(DatabaseFilename);
                        _Connection.Open();
                    }

                    return _Connection;
                }
            }
        }
        private DbConnection _Connection = null;

        /// <summary>
        /// Disposes the DatabaseConnection if it hasn't been accessed in 3 minutes
        /// </summary>
        /// <param name="state"></param>
        public void DeleteConnectionIfNeeded(object state)
        {
            using (TimedLock.Lock(ConnectionAccessLock))
                if (null != _Connection)
                    if (ConnectionLastAccessed.AddMinutes(3) <= DateTime.UtcNow)
                    {
                        _Connection.Close();
                        _Connection.Dispose();
                        _Connection = null;

                        if (null != Timer)
                        {
                            Timer.Dispose();
                            Timer = null;
                        }
                    }
        }

        /// <summary>
        /// Closes the connection
        /// </summary>
        ~DatabaseHandler()
        {
            Dispose();
        }

        public override void Dispose()
        {
            try
            {
                using (TimedLock.Lock(this))
                {
                    ConnectionLastAccessed = DateTime.MinValue;
                    DeleteConnectionIfNeeded(null);
                }
            }
            finally
            {
                base.Dispose();
            }
        }

        public override void SyncFromLocalDisk(string localDiskPath, bool force)
        {
            using (TimedLock.Lock(this))
            {
                if (!File.Exists(DatabaseFilename))
                    File.Copy(localDiskPath, DatabaseFilename);

                DateTime authoritativeCreated = File.GetLastWriteTimeUtc(localDiskPath);
                DateTime thisCreated = File.GetLastWriteTimeUtc(DatabaseFilename);

                if (authoritativeCreated > thisCreated || force)
                {
                    File.Delete(DatabaseFilename);
                    File.Copy(localDiskPath, DatabaseFilename);
                }
            }
        }
    }
}
