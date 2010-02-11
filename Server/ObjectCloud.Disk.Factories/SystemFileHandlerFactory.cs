// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;

namespace ObjectCloud.Disk.Factories
{
	public abstract class SystemFileHandlerFactory<TFileHandler> : FileHandlerFactory<TFileHandler>, ISystemFileHandlerFactory
		where TFileHandler : IFileHandler
	{
        public IFileHandler CreateSystemFile(ID<IFileContainer, long> fileId)
        {
            return CreateSystemFile(FileSystem.GetFullPath(fileId));
        }
    
        public abstract IFileHandler CreateSystemFile(string path);
    }
}
