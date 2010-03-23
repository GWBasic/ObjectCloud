// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.Interfaces.Disk
{
    public class GlobalWebHandlerPlugin : Plugin
    {
        public override void Initialize()
        {
            FileHandlerFactoryLocator.WebHandlerPlugins.AddRange(WebHandlerPluginTypes);
        }

        /// <summary>
        /// Plugin types for global webhandler methods
        /// </summary>
        public List<Type> WebHandlerPluginTypes
        {
            get { return _WebHandlerPluginTypes; }
            set { _WebHandlerPluginTypes = value; }
        }
        private List<Type> _WebHandlerPluginTypes;
    }
}
