// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.IO;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.Disk.FileHandlers;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Disk.Factories
{
    /// <summary>
    /// The file system
    /// </summary>
    public class FileSystem : IFileSystem
    {
		private static ILog log = LogManager.GetLogger<FileSystem>();
		
        public bool IsFilePresent(IFileId fileId)
        {
            string pathToCheck = GetFullPath(fileId);

            return Directory.Exists(pathToCheck) || File.Exists(pathToCheck);
        }

        /// <summary>
        /// The actual path to the folder used on disk or network drive
        /// </summary>
        public string ConnectionString
        {
            get { return _ConnectionString; }
            set
            {
                _ConnectionString = value;

                // If this is a relative path, assume it's a default and that Windows / Unix path seperators need to be fixed
                if (_ConnectionString.StartsWith("."))
                {
                    _ConnectionString = _ConnectionString.Replace('/', Path.DirectorySeparatorChar);
                    _ConnectionString = _ConnectionString.Replace('\\', Path.DirectorySeparatorChar);
                }
				
				_ConnectionString = Path.GetFullPath(_ConnectionString);
            }
        }
        private string _ConnectionString;

        /// <summary>
        /// Returns the full path to a given ID
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public string GetFullPath(IFileId id)
        {
            return Path.Combine(ConnectionString, id.ToString());
        }

        public void DeleteFile(IFileId fileId)
        {
            string pathToDelete = GetFullPath(fileId);
            RecursiveDelete(pathToDelete);
        }

        /// <summary>
        /// Recursively deletes the given directory, does not let exceptions leak through
        /// </summary>
        /// <param name="pathToDelete"></param>
        public void RecursiveDelete(string pathToDelete)
        {
            foreach (string subDirectory in System.IO.Directory.GetDirectories(pathToDelete))
                RecursiveDelete(subDirectory);

            foreach (string filename in System.IO.Directory.GetFiles(pathToDelete))
                try
                {
	                System.IO.File.Delete(filename);
                }
                catch (Exception e)
                {
                    log.Error("Could not delete " + filename, e);
                }

            try
            {
	            System.IO.Directory.Delete(pathToDelete);
            }
            catch (Exception e)
            {
                log.Error("Could not delete " + pathToDelete, e);
            }

            if (Directory.Exists(pathToDelete) || File.Exists(pathToDelete))
            	log.Warn(pathToDelete + " not deleted");
        }

        public DateTime GetRootDirectoryCreationTime()
        {
            return Directory.GetCreationTime(GetFullPath(RootDirectoryId));
        }

        /// <summary>
        /// The ID of the root object
        /// </summary>
        public IFileId RootDirectoryId
        {
            get { return _RootDirectoryId; }
            set { _RootDirectoryId = value; }
        }
        private IFileId _RootDirectoryId;


        public bool IsRootDirectoryPresent()
        {
            return IsFilePresent(RootDirectoryId);
        }

        public IFileContainer ConstructFileContainer(IFileHandler fileHandler, IFileId fileId, string typeId, IDirectoryHandler parentDirectoryHandler, FileHandlerFactoryLocator fileHandlerFactoryLocator, DateTime created)
        {
            return new FileContainer(fileHandler, fileId, typeId, parentDirectoryHandler, fileHandlerFactoryLocator, created);
        }
    }
}
