// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;

using ObjectCloud.Common;
using ObjectCloud.Disk.FileHandlers;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Disk.Factories
{
    public class BinaryHandlerFactory : FileHandlerFactory<IBinaryHandler>
    {
        public override void CreateFile(string path, FileId fileId)
        {
            System.IO.File.WriteAllBytes(BinaryHandler.CreateBinaryFilename(path), new byte[0]);
        }

        public override IBinaryHandler OpenFile(string path, FileId fileId)
        {
            return new BinaryHandler(path, FileHandlerFactoryLocator);
        }

        public override void CopyFile(IFileHandler sourceFileHandler, IFileId fileId, ID<IUserOrGroup, Guid>? ownerID)
        {
            CreateFile(fileId);
            System.IO.File.WriteAllBytes(
                BinaryHandler.CreateBinaryFilename(FileSystem.GetFullPath(fileId)),
                sourceFileHandler.FileContainer.CastFileHandler<IBinaryHandler>().ReadAll());
        }

        public override void RestoreFile(IFileId fileId, string pathToRestoreFrom, ID<IUserOrGroup, Guid> userId)
        {
            CreateFile(fileId);
            System.IO.File.WriteAllBytes(
                BinaryHandler.CreateBinaryFilename(FileSystem.GetFullPath(fileId)),
                File.ReadAllBytes(pathToRestoreFrom));
        }
    }
}
