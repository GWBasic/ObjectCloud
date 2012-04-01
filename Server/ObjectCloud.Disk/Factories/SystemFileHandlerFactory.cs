// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;

using ObjectCloud.Common;
using ObjectCloud.Disk.FileHandlers;
using ObjectCloud.Interfaces.Disk;

namespace ObjectCloud.Disk.Factories
{
	public abstract class SystemFileHandlerFactory<TFileHandler> : FileHandlerFactory<TFileHandler>, ISystemFileHandlerFactory
		where TFileHandler : IFileHandler
	{
        public void CreateSystemFile(IFileId fileId)
        {
            CreateSystemFile(FileSystem.GetFullPath(fileId), (FileId)fileId);
        }
    
        public abstract void CreateSystemFile(string path, FileId fileId);

        public override void CreateFile(string path, FileId fileId)
        {
            throw new SecurityException("This kind of file is a system file and can not be created");
        }
    }
}
