// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security;
using System.Text;
using System.Threading;
using System.Xml;

using Common.Logging;
using JsonFx.Json;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.Templating;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Interfaces.Templating
{
    /// <summary>
    /// Allows internal use of the template engine
    /// </summary>
    public interface ITemplateEngine
    {
        IWebResults Evaluate(IWebConnection webConnection, string filename);

        IWebResults Evaluate(IWebConnection webConnection, string filename, IDictionary<string, object> arguments);

        /// <summary>
        /// Evaluates the named template
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="getParameters"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        Stream EvaluateToStream(
            IWebConnection webConnection,
            IDictionary<string, object> arguments,
            string filename);

        //IWebResults EvaluateComponent(IWebConnection webConnection, string filename);

        string EvaluateComponent(IWebConnection webConnection, string filename, object templateInput);
    }
}
