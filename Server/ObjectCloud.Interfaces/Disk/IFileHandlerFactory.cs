// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
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
        IFileHandler CreateFile(ID<IFileContainer, long> fileId);

        /// <summary>
        /// Opens a file at the given path
        /// </summary>
        /// <param name="fileId">The file's unique ID</param>
        /// <returns>An IFileHandler object that handles the file.  This must be closed</returns>
        IFileHandler OpenFile(ID<IFileContainer, long> fileId);

        /// <summary>
        /// Copies a file
        /// </summary>
        /// <param name="sourceFileHandler">The source file handler to copy</param>
        /// <param name="fileId">The file's unique ID</param>
        IFileHandler CopyFile(IFileHandler sourceFileHandler, ID<IFileContainer, long> fileId, ID<IUserOrGroup, Guid>? ownerID);

        /// <summary>
        /// Restores a file at the given path
        /// </summary>
        /// <param name="fileId">The file's unique ID</param>
        /// <param name="pathToRestoreFrom">The path that contains the dump of a file to restore</param>
        /// <returns>An IFileHandler object that handles the file.  This must be closed</returns>
        IFileHandler RestoreFile(ID<IFileContainer, long> fileId, string pathToRestoreFrom, ID<IUserOrGroup, Guid> userId);

        /// <summary>
        /// The service locator
        /// </summary>
        FileHandlerFactoryLocator FileHandlerFactoryLocator { get; set; }

        /// <summary>
        /// Returns when a file was created
        /// </summary>
        /// <param name="fileId"></param>
        /// <returns></returns>
        DateTime EstimateCreationTime(ID<IFileContainer, long> fileId);
    }

    public interface IFileHandlerFactory<TFileHandler> : IFileHandlerFactory
        where TFileHandler : IFileHandler
    {
        /// <summary>
        /// Creates a file at the given path 
        /// </summary>
        /// <param name="fileId">The file's unique ID</param>
        /// <returns>An IFileHandler object that handles the file.  This must be closed</returns>
        new TFileHandler CreateFile(ID<IFileContainer, long> fileId);

        /// <summary>
        /// Opens a file at the given path
        /// </summary>
        /// <param name="fileId">The file's unique ID</param>
        /// <returns>An IFileHandler object that handles the file.  This must be closed</returns>
        new TFileHandler OpenFile(ID<IFileContainer, long> fileId);
    }
}