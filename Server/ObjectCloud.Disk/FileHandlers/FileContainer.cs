// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.IO;
using System.Xml;

using ObjectCloud.Common;
using ObjectCloud.DataAccess.Directory;
using ObjectCloud.Disk.FileHandlers;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Disk.FileHandlers
{
    public class FileContainer : FileContainerBase
    {
        public FileContainer(
            IFileHandler fileHandler,
            IFileId fileId,
            string typeId,
            string filename,
            IDirectoryHandler parentDirectoryHandler,
            FileHandlerFactoryLocator fileHandlerFactoryLocator,
            DateTime created)
            : base(fileHandler, fileId, typeId, filename, parentDirectoryHandler, fileHandlerFactoryLocator, created) { }

        public FileContainer(
            IFileId fileId, 
            string typeId, 
            string filename, 
            IDirectoryHandler parentDirectoryHandler,
            FileHandlerFactoryLocator fileHandlerFactoryLocator,
            DateTime created)
            : base(fileId, typeId, filename, parentDirectoryHandler, fileHandlerFactoryLocator, created) {}

        public override DateTime  LastModified
        {
        	get { return ((LastModifiedFileHandler)FileHandler).LastModified; }
        }
    }
}
