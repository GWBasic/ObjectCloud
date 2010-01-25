using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.Interfaces.Disk
{
    /// <summary>
    /// Plugin for a file type
    /// </summary>
    public class FileTypePlugin : Plugin
    {
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
            FileHandlerFactoryLocator.FileHandlerFactories[this.FileType] = FileHandlerFactory;
            FileHandlerFactoryLocator.WebHandlerClasses[this.FileType] = WebHandlerType;
        }
    }
}
