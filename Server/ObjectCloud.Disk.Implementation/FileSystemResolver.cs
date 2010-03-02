// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.Implementation
{
    /// <summary>
    /// Handles the local filesystem to resolve files on disk to files as presented
    /// </summary>
    public class FileSystemResolver : IFileSystemResolver
    {
        private static ILog log = LogManager.GetLogger<FileSystemResolver>();

        Cache<IFileId, IFileHandler, string> FileHandlers;
        Cache<IFileId, WebHandlers, IFileContainer> WebHandlers;

        public FileSystemResolver()
            : base()
        {
            FileHandlers = new Cache<IFileId, IFileHandler, string>(CreateFileHandlerForCache);
            WebHandlers = new Cache<IFileId, WebHandlers, IFileContainer>(CreateWebHandlersForCache);
        }

        public FileHandlerFactoryLocator FileHandlerFactoryLocator
        {
            get { return _FileHandlerFactoryLocator; }
            set { _FileHandlerFactoryLocator = value; }
        }
        private FileHandlerFactoryLocator _FileHandlerFactoryLocator;

        /// <summary>
        /// The ID of the root object
        /// </summary>
        public IFileId RootDirectoryId
        {
            get { return _RootDirectoryId; }
            set { _RootDirectoryId = value; }
        }
        private IFileId _RootDirectoryId;

        public IDirectoryHandler RootDirectoryHandler
        {
            get { return RootDirectoryContainer.CastFileHandler<IDirectoryHandler>(); }
        }

		public void VerifyNoForbiddenChars(string filename)
		{
            if (0 == filename.Length)
                throw new BadFileName("0-length file names are not allowed");

            foreach (char forbiddenChar in FilenameForbiddenCharacters)
                if (filename.Contains(new string(new char[] { forbiddenChar })))
                    throw new BadFileName("filenames can not contain a " + forbiddenChar);
		}
		
        public string FilenameForbiddenCharacters
        {
            get { return _FilenameForbiddenCharacters; }
            set { _FilenameForbiddenCharacters = value; }
        }
        private string _FilenameForbiddenCharacters;

        /// <summary>
        /// The root directory file
        /// </summary>
        public IFileContainer RootDirectoryContainer
        {
            get 
            {
                if (null == _RootDirectoryContainer)
                    throw new DiskException("Start() must be called prior to accessing the File System");

                return _RootDirectoryContainer;
            }
        }
        private IFileContainer _RootDirectoryContainer;

        public IFileHandlerFactory GetFactoryForFileType(string fileType)
        {
            if (FileHandlerFactoryLocator.FileHandlerFactories.ContainsKey(fileType))
                return FileHandlerFactoryLocator.FileHandlerFactories[fileType];

            throw new UnknownFileType(fileType);
        }

        public IFileContainer ResolveFile(string fileName)
        {
            string[] splitAtDirs = fileName.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            // If no file path is specified, then just return the root directory
            if (0 == splitAtDirs.Length)
                return RootDirectoryContainer;

            IDirectoryHandler dir = RootDirectoryHandler;

            int ctr = 0;
            for (; ctr < splitAtDirs.Length - 1; ctr++)
            {
                IFileContainer subDirFile = dir.OpenFile(splitAtDirs[ctr]);
                IFileHandler subDir = subDirFile.FileHandler;

                if (!(subDir is IDirectoryHandler))
                {
                    // The file isn't a sub-directory
                    string subDirName = "";
                    for (int subCtr = 0; subCtr <= ctr; ctr++)
                        subDirName = subDirName + '/' + splitAtDirs[subCtr];

                    throw new FileIsNotADirectory(subDirName);
                }

                dir = (IDirectoryHandler)subDir;
            }

            try
            {
                IFileContainer toReturn = dir.OpenFile(splitAtDirs[ctr]);
                return toReturn;
            }
            // This catch & re-throw puts the full path into the exception
            catch (FileDoesNotExist)
            {
                throw new FileDoesNotExist(fileName);
            }
        }

        public bool IsFilePresent(string fileName)
        {
            string[] splitAtDirs = fileName.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            // If no file path is specified, then just return the root directory
            if (0 == splitAtDirs.Length)
                return true;

            IDirectoryHandler dir = RootDirectoryHandler;

            int ctr = 0;
            try
            {
                for (; ctr < splitAtDirs.Length - 1; ctr++)
                {
                    if (!dir.IsFilePresent(splitAtDirs[ctr]))
                        return false;

                    IFileContainer subDirFile = dir.OpenFile(splitAtDirs[ctr]);
                    IFileHandler subDir = subDirFile.FileHandler;

                    if (!(subDir is IDirectoryHandler))
                        return false;

                    dir = (IDirectoryHandler)subDir;
                }

                return dir.IsFilePresent(splitAtDirs[ctr]);
            }
            // just in case...
            catch (FileDoesNotExist)
            {
                return false;
            }
        }

        public IFileHandler LoadFile(IFileId id, string fileType)
        {
            return FileHandlers.Get(id, fileType);
        }

        public WebHandlers LoadWebHandlers(IFileContainer fileContainer)
        {
            return WebHandlers.Get(fileContainer.FileId, fileContainer);
        }

        /// <summary>
        /// Called by the cache to load an IFileHandler on an as-needed basis
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private IFileHandler CreateFileHandlerForCache(IFileId id, string filetype)
        {
            if (FileHandlerFactoryLocator.FileSystem.IsFilePresent(id))
            {
                IFileHandlerFactory fileHandlerFactory = this.GetFactoryForFileType(filetype);

                IFileHandler toReturn = fileHandlerFactory.OpenFile(id);

                return toReturn;
            }
            else
                throw new InvalidFileId(id);
        }

        /// <summary>
        /// Called by the cache to load an IWebHandler on an as-needed basis
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private WebHandlers CreateWebHandlersForCache(IFileId id, IFileContainer fileContainer)
        {
            Type webHandlerType = FileHandlerFactoryLocator.WebHandlerClasses[fileContainer.TypeId];
            object webHandlerObj = Activator.CreateInstance(webHandlerType);

            if (!(webHandlerObj is IWebHandler))
                throw new InvalidCastException("WebHandler classes must implement IWebHandler; " + webHandlerType.FullName + " is not an IWebHandler");

            IWebHandler webHandler = (IWebHandler)webHandlerObj;
            webHandler.FileContainer = fileContainer;
            webHandler.FileHandlerFactoryLocator = FileHandlerFactoryLocator;

            return new WebHandlers(
                webHandler,
                new ReadOnlyCollection<IWebHandlerPlugin>(ConstructWebHandlerPlugins(fileContainer)));
        }

        /// <summary>
        /// Constructs WebHandlerPlugins
        /// </summary>
        /// <returns></returns>
        private IList<IWebHandlerPlugin> ConstructWebHandlerPlugins(IFileContainer fileContainer)
        {
            List<IWebHandlerPlugin> toReturn = new List<IWebHandlerPlugin>();

            foreach (Type webHandlerType in FileHandlerFactoryLocator.WebHandlerPlugins)
            {
                object webHandlerObj = Activator.CreateInstance(webHandlerType);

                if (!(webHandlerObj is IWebHandlerPlugin))
                    throw new InvalidCastException("WebHandler classes must implement IWebHandler; " + webHandlerType.FullName + " is not an IWebHandler");

                IWebHandlerPlugin webHandler = (IWebHandlerPlugin)webHandlerObj;
                webHandler.FileContainer = fileContainer;
                webHandler.FileHandlerFactoryLocator = FileHandlerFactoryLocator;

                toReturn.Add(webHandler);
            }

            return toReturn;
        }

        public void DeleteFile(IFileId id)
        {
            FileHandlers.Remove(id);
            WebHandlers.Remove(id);
            FileHandlerFactoryLocator.FileSystem.DeleteFile(id);
        }

        public void CopyFile(string sourceFileName, string destinationFileName, ID<IUserOrGroup, Guid>? ownerID)
        {
            int lastSlashPos = destinationFileName.LastIndexOf('/');
            IDirectoryHandler destinationDirectory;

            string destinationDirectoryName;
            if (lastSlashPos > 0)
                destinationDirectoryName = destinationFileName.Substring(0, lastSlashPos - 1);
            else
                destinationDirectoryName = "/";

            destinationDirectory = ResolveFile(destinationDirectoryName).CastFileHandler<IDirectoryHandler>();

            string newFileName;
            if (lastSlashPos > -1)
                newFileName = destinationFileName.Substring(lastSlashPos + 1);
            else
                newFileName = destinationFileName;

            IFileContainer toCopy = ResolveFile(sourceFileName);

            IUser changer = null;
            if (null != ownerID)
                changer = FileHandlerFactoryLocator.UserManagerHandler.GetUser(ownerID.Value);

            destinationDirectory.CopyFile(changer, toCopy, newFileName, ownerID);
        }

        /// <value>
        /// These threads are all started prior to starting the web server, and stopped when the web server stops
        /// </value>
        public IDictionary<string, IRunnable> ServiceThreadStarts
        {
            get { return _ServiceThreadStarts; }
            set { _ServiceThreadStarts = value; }
        }
        IDictionary<string, IRunnable> _ServiceThreadStarts;

        /// <summary>
        /// Returns true if the file system is started
        /// </summary>
        public bool IsStarted
        {
            get { return _IsStarted; }
        }
        volatile private bool _IsStarted = false;
		
		/// <summary>
		/// When set to false, ObjectCloud will not syncronize to external files when starting up.  This will make startups faster,
		/// but could result in outdated files in the file system 
		/// </summary>
        public bool SyncronizeToDefaultFiles
		{
    			get { return _SyncronizeToDefaultFiles; }
    			set { _SyncronizeToDefaultFiles = value; }
    		}
		private bool _SyncronizeToDefaultFiles = true;

        /// <summary>
        /// All of the service threads
        /// </summary>
        List<Thread> ServiceThreads = new List<Thread>();

        public void Start()
        {
            log.Info("Initializing plugins...");
            foreach (Plugin plugin in FileHandlerFactoryLocator.Plugins)
                plugin.Initialize();

            log.Info("Starting ObjectCloud's File System...");

            if (null == _RootDirectoryContainer)
            {
                DateTime rootDirectoryCreateTime;
                if (FileHandlerFactoryLocator.FileSystem.IsFilePresent(RootDirectoryId))
                    rootDirectoryCreateTime = FileHandlerFactoryLocator.FileSystem.GetDirectoryCreationTime(RootDirectoryId);
                else
                    rootDirectoryCreateTime = DateTime.UtcNow;

                _RootDirectoryContainer = new FileContainer(null, RootDirectoryId, "directory", "", null, FileHandlerFactoryLocator, rootDirectoryCreateTime);

                if (FileHandlerFactoryLocator.FileSystem.IsFilePresent(RootDirectoryId))
                    FileHandlerFactoryLocator.RootDirectoryCreator.Syncronize(RootDirectoryHandler);
                else
                {
                    log.Info("... Creating the Root Directory ...");

                    try
                    {
                        FileHandlerFactoryLocator.RootDirectoryCreator.CreateRootDirectoryHandler(_RootDirectoryContainer);
                    }
                    catch (Exception e)
                    {
                        // If there's an exception, leave the system in a state where it can't run at all
                        _RootDirectoryContainer = null;
                        //RecursiveDelete(RootDirectoryDiskPath);

                        log.Error("Exception when creating the root directory", e);

                        throw e;
                    }
                }
            }
			
			ILoggerFactoryAdapter loggerFactoryAdapter = LogManager.Adapter;
			if (loggerFactoryAdapter is IObjectCloudLoggingFactoryAdapter)
			{
				log.Info("Starting the logger");
				((IObjectCloudLoggingFactoryAdapter)loggerFactoryAdapter).ObjectCloudLogHandler = ResolveFile("/System/Log").CastFileHandler<IObjectCloudLogHandler>();
				log.Info("Logger started");
			}
			
            if (null != ServiceThreadStarts)
                foreach (KeyValuePair<string, IRunnable> threadStartAndName in ServiceThreadStarts)
                {
                    Thread thread = new Thread(threadStartAndName.Value.Run);
                    thread.Name = threadStartAndName.Key;
                    thread.Start();

                    ServiceThreads.Add(thread);
                }

            _IsStarted = true;

            log.Info("...ObjectCloud's File System is started!");
        }

        public void Stop()
        {
            _IsStarted = false;

            foreach (Thread thread in ServiceThreads)
            {
                log.Info("Aborting " + thread.Name);
                thread.Abort();
            }

            foreach (Thread thread in ServiceThreads)
            {
                log.Info("Waiting for " + thread.Name + " to finish");
                thread.Join();
            }

            ServiceThreads.Clear();

			ILoggerFactoryAdapter loggerFactoryAdapter = LogManager.Adapter;
			if (loggerFactoryAdapter is IObjectCloudLoggingFactoryAdapter)
			{
				log.Info("Stopping the logger");
				((IObjectCloudLoggingFactoryAdapter)loggerFactoryAdapter).ObjectCloudLogHandler = null;
				log.Info("Logger stopped");
			}
        }
    }
}