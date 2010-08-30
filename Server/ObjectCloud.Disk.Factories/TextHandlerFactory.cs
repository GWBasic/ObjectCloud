// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.Disk.FileHandlers;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Disk.Factories
{
    public class TextHandlerFactory : FileHandlerFactory<ITextHandler>
    {
        public override void CreateFile(string path, FileId fileId)
        {
            string subPath = CreateTextFilename(path);

            System.IO.File.WriteAllText(subPath, "");
        }

        public override ITextHandler OpenFile(string path, FileId fileId)
        {
            return new TextHandler(CreateTextFilename(path), FileHandlerFactoryLocator);
        }

        /// <summary>
        /// Creates the text file name
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private string CreateTextFilename(string path)
        {
            return string.Format("{0}{1}file.txt", path, Path.DirectorySeparatorChar);
        }


        public override void CopyFile(IFileHandler sourceFileHandler, IFileId fileId, ID<IUserOrGroup, Guid>? ownerID, IDirectoryHandler parentDirectory)
        {
            CreateFile(fileId);
            System.IO.File.WriteAllText(
                CreateTextFilename(FileSystem.GetFullPath(fileId)),
                sourceFileHandler.FileContainer.CastFileHandler<ITextHandler>().ReadAll());
        }

        public override void RestoreFile(IFileId fileId, string pathToRestoreFrom, ID<IUserOrGroup, Guid> userId, IDirectoryHandler parentDirectory)
        {
            CreateFile(fileId);
            System.IO.File.WriteAllText(
                CreateTextFilename(FileSystem.GetFullPath(fileId)),
                File.ReadAllText(pathToRestoreFrom));
        }
    }
}
