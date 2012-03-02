// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.IO;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.Disk.FileHandlers;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Disk.Factories
{
	/// <summary>
	/// Provides a rudimentary default implementation of IFileHandlerFactory
	/// </summary>
	public abstract class FileHandlerFactory<TFileHandler> : IFileHandlerFactory<TFileHandler>
		where TFileHandler : IFileHandler
	{
        protected readonly ILog log;

        public FileHandlerFactory()
        {
            log = LogManager.GetLogger(GetType());
        }

        IFileHandler IFileHandlerFactory.OpenFile(IFileId fileId)
		{
            return OpenFile(fileId);
		}

        public abstract void CopyFile(IFileHandler sourceFileHandler, IFileId fileId, ID<IUserOrGroup, Guid>? ownerID, IDirectoryHandler parentDirectory);

        public abstract void RestoreFile(IFileId fileId, string pathToRestoreFrom, ID<IUserOrGroup, Guid> userId, IDirectoryHandler parentDirectory);

        public void CreateFile(IFileId fileId)
        {
            string path = FileSystem.GetFullPath(fileId);

            bool success = null != Directory.CreateDirectory(path);

            if (!success)
                throw new CanNotCreateFile("Could not create " + path);

            try
            {
                CreateFile(path, (FileId)fileId);
            }
            catch (DiskException de)
            {
                // Attempt to delete missing files
                FileSystem.RecursiveDelete(path);

                throw de;
            }
        }

        public TFileHandler OpenFile(IFileId fileId)
        {
            return OpenFile(FileSystem.GetFullPath(fileId), (FileId)fileId);
        }

        public abstract void CreateFile(string path, FileId fileId);

        public abstract TFileHandler OpenFile(string path, FileId fileId);

        /// <summary>
        /// The service locator.  This should be set in Spring so that these assemblies aren't dependant on Spring
        /// </summary>
        public FileHandlerFactoryLocator FileHandlerFactoryLocator
        {
            get 
            {
                if (null == _FileHandlerFactoryLocator)
                    log.Warn("FileHandlerFactoryLocator is not set.  This must be set in Spring");

                return _FileHandlerFactoryLocator; 
            }
            set { _FileHandlerFactoryLocator = value; }
        }
        private FileHandlerFactoryLocator _FileHandlerFactoryLocator;

        protected FileSystem FileSystem
        {
            get { return (FileSystem)FileHandlerFactoryLocator.FileSystem; }
        }

        public virtual void Stop()
        {
        }
    }

    /// <summary>
    /// Factory when the file handler does nothing and all logic is in the web handler
    /// </summary>
    public class FileHandlerFactory : FileHandlerFactory<FileHandlerFactory.DoNothingFileHandler>
    {
        public override void CopyFile(IFileHandler sourceFileHandler, IFileId fileId, ID<IUserOrGroup, Guid>? ownerID, IDirectoryHandler parentDirectory)
        {
            throw new NotImplementedException("The method or operation is not implemented.");
        }

        public override void CreateFile(string path, FileId fileId) {}

        public override FileHandlerFactory.DoNothingFileHandler OpenFile(string path, FileId fileId)
        {
            return new DoNothingFileHandler();
        }

        public override void RestoreFile(IFileId fileId, string pathToRestoreFrom, ID<IUserOrGroup, Guid> userId, IDirectoryHandler parentDirectory)
        {
            throw new NotImplementedException("The method or operation is not implemented.");
        }

        public class DoNothingFileHandler : LastModifiedFileHandler
        {
            public DoNothingFileHandler() : base(null, null) { }

            public override void Dump(string path, ID<IUserOrGroup, Guid> userId) { }

            public override DateTime LastModified
            {
                get { return FileContainer.Created; }
            }
        }
    }
}
