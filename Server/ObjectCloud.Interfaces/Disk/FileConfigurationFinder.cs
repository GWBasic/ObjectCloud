// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Common;

namespace ObjectCloud.Interfaces.Disk
{
    /// <summary>
    /// Returns the appropriate FileConfigurationManager for a file
    /// </summary>
    public class FileConfigurationFinder : HasFileHandlerFactoryLocator
    {
        public FileConfigurationFinder()
        {
            FileConfigurationManagersByType = new Cache<string, FileConfigurationManager>(GetFileConfigurationManagerForType);
            FileConfigurationManagersByExtension = new Cache<string, FileConfigurationManager>(GetFileConfigurationManagerForExtension);
        }

        /// <summary>
        /// Returns the appropriate FileConfigurationManager
        /// </summary>
        /// <param name="fileContainer"></param>
        /// <returns></returns>
        public FileConfigurationManager GetFileConfigurationManager(IFileContainer fileContainer)
        {
            string extension = fileContainer.Extension;

            if (null == extension)
                return FileConfigurationManagersByType[fileContainer.TypeId];
            else
                return FileConfigurationManagersByExtension[extension];
        }

        /// <summary>
        /// Returns the FileConfigurationManager for the given extension
        /// </summary>
        /// <param name="extension"></param>
        /// <returns></returns>
        public FileConfigurationManager this[string extension]
        {
            get { return FileConfigurationManagersByExtension[extension]; }
        }

        private Cache<string, FileConfigurationManager> FileConfigurationManagersByType;
        private Cache<string, FileConfigurationManager> FileConfigurationManagersByExtension;

        private FileConfigurationManager GetFileConfigurationManagerForType(string name)
        {
            FileConfigurationManager toReturn = GetFileConfigurationManager(
                name,
                FileHandlerFactoryLocator.FileSystemResolver.ResolveFile("/Config/ByType").CastFileHandler<IDirectoryHandler>());

            if (null != toReturn)
                return toReturn;

            return new FileConfigurationManager(name);
        }

        private FileConfigurationManager GetFileConfigurationManagerForExtension(string name)
        {
            FileConfigurationManager toReturn = GetFileConfigurationManager(
                name,
                FileHandlerFactoryLocator.FileSystemResolver.ResolveFile("/Config/ByExtension").CastFileHandler<IDirectoryHandler>());

            if (null != toReturn)
                return toReturn;

            return new FileConfigurationManager("text");
        }

        private FileConfigurationManager GetFileConfigurationManager(string name, IDirectoryHandler directory)
        {
            string configurationFilename = name + ".json";
            if (!directory.IsFilePresent(configurationFilename))
                return null;

            ITextHandler configurationFileHandler = directory.OpenFile(configurationFilename).CastFileHandler<ITextHandler>();
            return new FileConfigurationManager(configurationFileHandler);
        }
    }
}
