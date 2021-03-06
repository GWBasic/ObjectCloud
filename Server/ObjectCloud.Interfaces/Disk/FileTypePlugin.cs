// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using Common.Logging;

namespace ObjectCloud.Interfaces.Disk
{
    /// <summary>
    /// Plugin for a file type
    /// </summary>
    public class FileTypePlugin : Plugin
    {
        ILog log = LogManager.GetLogger<FileTypePlugin>();

        /// <summary>
        /// The factory for this kind of file type
        /// </summary>
        protected IFileHandlerFactory FileHandlerFactory
        {
            get { return _FileHandlerFactory; }
            set { _FileHandlerFactory = value; }
        }
        private IFileHandlerFactory _FileHandlerFactory;

        /// <summary>
        /// The web handler for this kind of file type
        /// </summary>
        protected Type WebHandlerType
        {
            get { return _WebHandlerType; }
            set { _WebHandlerType = value; }
        }
        private Type _WebHandlerType;

        /// <summary>
        /// The file type's name
        /// </summary>
        public string FileType
        {
            get { return _FileType; }
            set { _FileType = value; }
        }
        private string _FileType;

        public override void Initialize()
        {
            log.InfoFormat("Initializing plugin for file type: {0}", FileType);

            if (null != FileHandlerFactory)
            {
                log.InfoFormat("Set FileHandlerFactory for file type {0} to be of type {1}", FileType, FileHandlerFactory.GetType().FullName);
                FileHandlerFactoryLocator.FileHandlerFactories[this.FileType] = FileHandlerFactory;
            }

            if (null != WebHandlerType)
            {
                log.InfoFormat("Set WebHandlerType for file type {0} to be of type {1}", FileType, WebHandlerType.FullName);
                FileHandlerFactoryLocator.WebHandlerClasses[this.FileType] = WebHandlerType;
            }
        }
    }
}
