// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Javascript;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Interfaces.WebServer
{
    public class WebHandlerPlugin : IWebHandlerPlugin
    {
        public virtual WebDelegate GetMethod(IWebConnection webConnection)
        {
            string method = webConnection.GetArgumentOrException("Method");
            return FileHandlerFactoryLocator.WebMethodCache[MethodNameAndFileContainer.New(method, FileContainer, this)];
        }

        /// <value>
        /// The FileHandler, pre-casted
        /// </value>
        public IFileHandler FileHandler
        {
            get { return _FileHandler; }
        }
        private IFileHandler _FileHandler;

        /// <value>
        /// The FileContainer 
        /// </value>
        public IFileContainer FileContainer
        {
            get { return _FileContainer; }
            set
            {
                _FileContainer = value;
                _FileHandler = value.FileHandler;
            }
        }
        private IFileContainer _FileContainer;

        /// <summary>
        /// The FileHandlerFactoryLocator
        /// </summary>
        public FileHandlerFactoryLocator FileHandlerFactoryLocator
        {
            get { return _FileHandlerFactoryLocator; }
            set { _FileHandlerFactoryLocator = value; }
        }
        private FileHandlerFactoryLocator _FileHandlerFactoryLocator;
    }
}
