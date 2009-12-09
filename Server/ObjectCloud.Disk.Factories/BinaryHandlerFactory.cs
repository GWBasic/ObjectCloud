// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
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
        public override IBinaryHandler CreateFile(string path)
        {
            string subPath = CreateBinaryFilename(path);

            System.IO.File.WriteAllBytes(subPath, new byte[0]);

            return new BinaryHandler(subPath, FileHandlerFactoryLocator);
        }

        public override IBinaryHandler OpenFile(string path)
        {
            return new BinaryHandler(CreateBinaryFilename(path), FileHandlerFactoryLocator);
        }

        /// <summary>
        /// Creates the text file name
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private string CreateBinaryFilename(string path)
        {
            return string.Format("{0}{1}file.bin", path, Path.DirectorySeparatorChar);
        }

        public override IFileHandler CopyFile(IFileHandler sourceFileHandler, ID<IFileContainer, long> fileId, ID<IUserOrGroup, Guid>? ownerID)
        {
            IBinaryHandler toReturn = CreateFile(fileId);
            toReturn.WriteAll(((IBinaryHandler)sourceFileHandler).ReadAll());

            return toReturn;
        }

        public override IFileHandler RestoreFile(ID<IFileContainer, long> fileId, string pathToRestoreFrom, ID<IUserOrGroup, Guid> userId)
        {
            IBinaryHandler toReturn = CreateFile(fileId);

            toReturn.WriteAll(File.ReadAllBytes(pathToRestoreFrom));

            return toReturn;
        }
    }
}
