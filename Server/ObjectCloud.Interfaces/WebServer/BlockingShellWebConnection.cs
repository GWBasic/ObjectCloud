// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
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
    public class BlockingShellWebConnection : ShellWebConnection
    {
        public BlockingShellWebConnection(
            string url,
            IWebConnection webConnection,
            RequestParameters postParameters,
            CookiesFromBrowser cookiesFromBrowser)
            : base(url, webConnection, postParameters, cookiesFromBrowser, webConnection.CallingFrom) { }

        public BlockingShellWebConnection(
            string url,
            IWebConnection webConnection,
            RequestParameters postParameters,
            CookiesFromBrowser cookiesFromBrowser,
            CallingFrom callingFrom)
            : base(url, webConnection, postParameters, cookiesFromBrowser, callingFrom, WebMethod.GET) { }

        public BlockingShellWebConnection(
            IWebConnection webConnection,
            WebMethod method,
            string url,
            byte[] content,
            string contentType,
            CookiesFromBrowser cookiesFromBrowser,
            CallingFrom callingFrom)
            : base(webConnection, method, url, content, contentType, cookiesFromBrowser, callingFrom) { }

        public BlockingShellWebConnection(
            IWebServer webServer,
            ISession session,
            string url,
            byte[] content,
            string contentType,
            CookiesFromBrowser cookiesFromBrowser,
            CallingFrom callingFrom,
            WebMethod method)
            : base(webServer, session, url, content, contentType, cookiesFromBrowser, callingFrom, method) { }

        public override IWebResults GenerateResultsForClient()
        {
            AsyncWebResults = base.GenerateResultsForClient();

            if (null == AsyncWebResultsPulser)
                using (TimedLock.Lock(AsyncWebResultsPulser))
                    Monitor.Wait(AsyncWebResultsPulser);
            
            return AsyncWebResults;
        }

        IWebResults AsyncWebResults;
        private object AsyncWebResultsPulser = new object();

        public override void SendResults(IWebResults webResults)
        {
            AsyncWebResults = webResults;

            using (TimedLock.Lock(AsyncWebResultsPulser))
                Monitor.Pulse(AsyncWebResultsPulser);
        }
    }
}
