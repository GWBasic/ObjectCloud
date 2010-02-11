// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;


namespace ObjectCloud.Disk.WebHandlers.Comet
{
    /// <summary>
    /// Base web handler for all comet endpoints
    /// </summary>
    public class BaseCometWebHandler : WebHandler<IFileHandler>
    {
        /// <summary>
        /// The comet handler
        /// </summary>
        protected ICometHandler CometHandler
        {
            get
            {
                IDirectoryHandler parent = FileContainer.ParentDirectoryHandler;
                return (ICometHandler)parent;
            }
        }
    }
}
