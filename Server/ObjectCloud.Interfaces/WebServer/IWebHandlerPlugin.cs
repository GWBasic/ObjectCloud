// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
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
    /// <summary>
    /// Interface for web handler plugins
    /// </summary>
    public interface IWebHandlerPlugin
    {
        /// <summary>
        /// Returns a delegate for handling the request, or throws a WebResultsOverride exception if the method can not be handled
        /// </summary>
        /// <exception cref="WebResultsOverrideException">Thrown if no method can be found</exception>
        /// <returns></returns>
        WebDelegate GetMethod(IWebConnection webConnection);

        /// <value>
        /// The FileContainer.  This is always set directly after construction. 
        /// </value>
        IFileContainer FileContainer { get; set; }

        /// <summary>
        /// The service locator
        /// </summary>
        FileHandlerFactoryLocator FileHandlerFactoryLocator { get; set; }
    }
}
