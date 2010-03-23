// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.Interfaces.Disk
{
    /// <summary>
    /// Base class for all ObjectCloud plugins
    /// </summary>
    public abstract class Plugin
    {
        /// <summary>
        /// Instructs the plugin to register itself into ObjectCloud
        /// </summary>
        public abstract void Initialize();

        /// <summary>
        /// This is intended to be called from Spring
        /// </summary>
        public FileHandlerFactoryLocator FileHandlerFactoryLocator
        {
            get { return _FileHandlerFactoryLocator; }
            set 
            {
                if (null != _FileHandlerFactoryLocator)
                    _FileHandlerFactoryLocator.Plugins.Remove(this);

                _FileHandlerFactoryLocator = value;
                value.Plugins.Add(this);
            }
        }
        private FileHandlerFactoryLocator _FileHandlerFactoryLocator = null;
    }
}
