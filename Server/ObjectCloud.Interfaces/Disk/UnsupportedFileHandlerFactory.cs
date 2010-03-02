// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Interfaces.Disk
{
    /// <summary>
    /// Intended for use as a placeholder when a file type isn't supported
    /// </summary>
    public class UnsupportedFileHandlerFactory : IFileHandlerFactory
    {
        public void CreateFile(IFileId fileId)
        {
            throw new NotImplementedException("File type isn't supported");
        }

        public IFileHandler OpenFile(IFileId fileId)
        {
            throw new NotImplementedException("File type isn't supported");
        }

        public void CopyFile(IFileHandler sourceFileHandler, IFileId fileId, ID<IUserOrGroup, Guid>? ownerID)
        {
            throw new NotImplementedException("File type isn't supported");
        }

        public void RestoreFile(IFileId fileId, string pathToRestoreFrom, ID<IUserOrGroup, Guid> userId)
        {
            throw new NotImplementedException("File type isn't supported");
        }

        public FileHandlerFactoryLocator FileHandlerFactoryLocator
        {
            get { return _FileHandlerFactoryLocator; }
            set { _FileHandlerFactoryLocator = value; }
        }
        private FileHandlerFactoryLocator _FileHandlerFactoryLocator;
    }
}
