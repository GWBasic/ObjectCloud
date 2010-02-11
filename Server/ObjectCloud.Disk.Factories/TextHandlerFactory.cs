// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
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
        public override ITextHandler CreateFile(string path)
        {
            string subPath = CreateTextFilename(path);

            System.IO.File.WriteAllText(subPath, "");

            return new TextHandler(subPath, FileHandlerFactoryLocator);
        }

        public override ITextHandler OpenFile(string path)
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

        public override IFileHandler CopyFile(IFileHandler sourceFileHandler, ID<IFileContainer, long> fileId, ID<IUserOrGroup, Guid>? ownerID)
        {
            ITextHandler toReturn = CreateFile(fileId);
            toReturn.WriteAll(null, ((ITextHandler)sourceFileHandler).ReadAll());

            return toReturn;
        }

        public override IFileHandler RestoreFile(ID<IFileContainer, long> fileId, string pathToRestoreFrom, ID<IUserOrGroup, Guid> userId)
        {
            ITextHandler toReturn = CreateFile(fileId);

            IUser user = FileHandlerFactoryLocator.UserManagerHandler.GetUserNoException(userId);

            toReturn.WriteAll(user, File.ReadAllText(pathToRestoreFrom));

            return toReturn;
        }
    }
}
