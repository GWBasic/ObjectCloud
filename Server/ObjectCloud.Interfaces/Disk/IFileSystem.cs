// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Common;

namespace ObjectCloud.Interfaces.Disk
{
    public interface IFileSystem
    {
        /// <summary>
        /// Returns true if there is a file with the given ID, false otherwise
        /// </summary>
        /// <param name="fileId"></param>
        /// <returns></returns>
        bool IsFilePresent(IFileId fileId);

        /// <summary>
        /// Deletes the file with the given ID
        /// </summary>
        /// <param name="fileId"></param>
        void DeleteFile(IFileId fileId);

        /// <summary>
        /// Returns when the directory was created; intended for use with the Root Directory as it has no parent directory to track its creation time
        /// </summary>
        DateTime GetRootDirectoryCreationTime();

        /// <summary>
        /// The root directory's ID
        /// </summary>
        IFileId RootDirectoryId { get; set; }

        /// <summary>
        /// Returns true if the root directory is present, false otherwise
        /// </summary>
        /// <returns></returns>
        bool IsRootDirectoryPresent();

        /// <summary>
        /// Constructs a FileContainer
        /// </summary>
        /// <param name="fileHandler"></param>
        /// <param name="fileId"></param>
        /// <param name="typeId"></param>
        /// <param name="filename"></param>
        /// <param name="parentDirectoryHandler"></param>
        /// <param name="fileHandlerFactoryLocator"></param>
        /// <param name="created"></param>
        /// <returns></returns>
        IFileContainer ConstructFileContainer(
            IFileHandler fileHandler,
            IFileId fileId,
            string typeId,
            string filename,
            IDirectoryHandler parentDirectoryHandler,
            FileHandlerFactoryLocator fileHandlerFactoryLocator,
            DateTime created);
    }
}
