// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Interfaces.Disk
{
    /// <summary>
    /// Handles creation of objects to control files
    /// </summary>
    public interface IFileHandlerFactory
    {
        /// <summary>
        /// Creates a file at the given path 
        /// </summary>
        /// <param name="fileId">The file's unique ID</param>
        /// <returns>An IFileHandler object that handles the file.  This must be closed</returns>
        void CreateFile(IFileId fileId);

        /// <summary>
        /// Opens a file at the given path
        /// </summary>
        /// <param name="fileId">The file's unique ID</param>
        /// <returns>An IFileHandler object that handles the file.  This must be closed</returns>
        IFileHandler OpenFile(IFileId fileId);

        /// <summary>
        /// Copies a file
        /// </summary>
        /// <param name="sourceFileHandler">The source file handler to copy</param>
        /// <param name="fileId">The file's unique ID</param>
        /// <param name="parentDirectory">The parent directory</param>
        void CopyFile(IFileHandler sourceFileHandler, IFileId fileId, ID<IUserOrGroup, Guid>? ownerID, IDirectoryHandler parentDirectory);

        /// <summary>
        /// Restores a file at the given path
        /// </summary>
        /// <param name="fileId">The file's unique ID</param>
        /// <param name="pathToRestoreFrom">The path that contains the dump of a file to restore</param>
        /// <returns>An IFileHandler object that handles the file.  This must be closed</returns>
        /// <param name="parentDirectory">The parent directory</param>
        void RestoreFile(IFileId fileId, string pathToRestoreFrom, ID<IUserOrGroup, Guid> userId, IDirectoryHandler parentDirectory);

        /// <summary>
        /// The service locator
        /// </summary>
        FileHandlerFactoryLocator FileHandlerFactoryLocator { get; set; }

        /// <summary>
        /// Instructs the file handler factory that the file system is shutting down
        /// </summary>
        void Stop();
    }

    public interface IFileHandlerFactory<TFileHandler> : IFileHandlerFactory
        where TFileHandler : IFileHandler
    {
        /// <summary>
        /// Opens a file at the given path
        /// </summary>
        /// <param name="fileId">The file's unique ID</param>
        /// <returns>An IFileHandler object that handles the file.  This must be closed</returns>
        new TFileHandler OpenFile(IFileId fileId);
    }
}