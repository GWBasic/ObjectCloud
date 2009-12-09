// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Interfaces.WebServer
{
    public class NonBlockingShellWebConnection : ShellWebConnection
    {
        public NonBlockingShellWebConnection(
           string url,
           IWebConnection webConnection,
           RequestParameters postParameters,
           CookiesFromBrowser cookiesFromBrowser)
           : base(url, webConnection, postParameters, cookiesFromBrowser, webConnection.CallingFrom) { }

        public NonBlockingShellWebConnection(
            string url,
            IWebConnection webConnection,
            RequestParameters postParameters,
            CookiesFromBrowser cookiesFromBrowser,
            CallingFrom callingFrom)
            : base(url, webConnection, postParameters, cookiesFromBrowser, callingFrom, WebMethod.GET) { }

        public NonBlockingShellWebConnection(
            IWebConnection webConnection,
            WebMethod method,
            string url,
            byte[] content,
            string contentType,
            CookiesFromBrowser cookiesFromBrowser,
            CallingFrom callingFrom)
            : base(webConnection, method, url, content, contentType, cookiesFromBrowser, callingFrom) { }

        public override void SendResults(IWebResults webResults)
        {
            BaseWebConnection.SendResults(webResults);
        }
    }
}
