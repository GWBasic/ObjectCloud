// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Interfaces.Javascript
{
    /// <summary>
    /// Used to create Javascript Execution Environments
    /// </summary>
    public interface IExecutionEnvironmentFactory
    {
        /// <summary>
        /// Creates an execution environment for theObject.  Javascript is found in javascriptContainer
        /// </summary>
        /// <param name="fileHandlerFactoryLocator"></param>
        /// <param name="theObject"></param>
        /// <param name="javascriptContainer"></param>
        /// <returns></returns>
        IExecutionEnvironment Create(
            IFileContainer theObject,
            IFileContainer javascriptContainer);

        /// <summary>
        /// Runs the javascript in the file container
        /// </summary>
        /// <param name="javascriptContainer"></param>
        /// <returns></returns>
        IWebResults Run(IWebConnection webConnection, IFileContainer javascriptContainer);
    }
}
