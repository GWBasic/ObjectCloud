// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
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
    }
}
