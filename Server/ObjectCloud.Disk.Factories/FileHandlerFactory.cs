// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
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

        void IFileHandlerFactory.CreateFile(IFileId fileId)
		{
			CreateFile(fileId);
        }

        IFileHandler IFileHandlerFactory.OpenFile(IFileId fileId)
		{
            return OpenFile(fileId);
		}

        public abstract void CopyFile(IFileHandler sourceFileHandler, IFileId fileId, ID<IUserOrGroup, Guid>? ownerID);

        public abstract void RestoreFile(IFileId fileId, string pathToRestoreFrom, ID<IUserOrGroup, Guid> userId);

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

        public DateTime EstimateCreationTime(IFileId fileId)
        {
            return Directory.GetCreationTime(FileSystem.GetFullPath(fileId));
        }
    }

    /// <summary>
    /// Factory when the file handler does nothing and all logic is in the web handler
    /// </summary>
    public class FileHandlerFactory : FileHandlerFactory<FileHandlerFactory.DoNothingFileHandler>
    {
        public override void CopyFile(IFileHandler sourceFileHandler, IFileId fileId, ID<IUserOrGroup, Guid>? ownerID)
        {
            throw new NotImplementedException("The method or operation is not implemented.");
        }

        public override void CreateFile(string path, FileId fileId) {}

        public override FileHandlerFactory.DoNothingFileHandler OpenFile(string path, FileId fileId)
        {
            return new DoNothingFileHandler();
        }

        public override void RestoreFile(IFileId fileId, string pathToRestoreFrom, ID<IUserOrGroup, Guid> userId)
        {
            throw new NotImplementedException("The method or operation is not implemented.");
        }

        public class DoNothingFileHandler : FileHandler
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
