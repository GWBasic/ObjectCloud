// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Xml;

namespace ObjectCloud.Interfaces.Disk
{
    public abstract class HasFileHandlerFactoryLocator
    {
        public FileHandlerFactoryLocator FileHandlerFactoryLocator
        {
            get { return _FileHandlerFactoryLocator; }
            set 
            {
                _FileHandlerFactoryLocator = value;
                FileHandlerFactoryLocatorSet();
            }
        }
        private FileHandlerFactoryLocator _FileHandlerFactoryLocator;

        protected virtual void FileHandlerFactoryLocatorSet()
        { }
    }
}
