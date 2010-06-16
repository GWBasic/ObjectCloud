// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

﻿using System;
﻿using System.Collections.Generic;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Interfaces.Javascript
{
    public interface IExecutionEnvironment
    {
        /// <summary>
        /// Generates a Javscript wrapper for the browser that calls functions in this javascript.
        /// </summary>
        /// <returns></returns>
        IEnumerable<string> GenerateJavascriptWrapper(IWebConnection webConnection);

        /// <summary>
        /// Returns a delegate to handle the incoming request
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        WebDelegate GetMethod(ObjectCloud.Interfaces.WebServer.IWebConnection webConnection);

        /// <summary>
        /// The file container that has the javascript
        /// </summary>
        IFileContainer JavascriptContainer { get; }

        /// <summary>
        /// When the in-memory javascript was last modified
        /// </summary>
        DateTime JavascriptLastModified { get; }

        /// <summary>
        /// Any syntax errors in the javascript execution environment
        /// </summary>
        string ExecutionEnvironmentErrors { get; }

        /// <summary>
        /// Returns true if access to the underlying web methods should be disabled
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        bool BlockWebMethods { get; }
    }
}
