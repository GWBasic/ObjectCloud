// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer.UserAgent;

namespace ObjectCloud.Interfaces.WebServer
{
    /// <summary>
    /// Psuedo-web-connection when defering handling an object to another object via a shell method
    /// </summary>
    public abstract class ShellWebConnection : WebConnectionBase
    {
        public ShellWebConnection(
            string url,
            IWebConnection webConnection,
            RequestParameters postParameters,
            CookiesFromBrowser cookiesFromBrowser)
            : this(url, webConnection, postParameters, cookiesFromBrowser, webConnection.CallingFrom)
        { }

        public ShellWebConnection(
            string url,
            IWebConnection webConnection,
            RequestParameters postParameters,
            CookiesFromBrowser cookiesFromBrowser,
            CallingFrom callingFrom)
            : this(url, webConnection, postParameters, cookiesFromBrowser, callingFrom, WebMethod.GET) { }

        public ShellWebConnection(
            IWebConnection webConnection,
            WebMethod method,
            string url,
            byte[] content,
            string contentType,
            CookiesFromBrowser cookiesFromBrowser,
            CallingFrom callingFrom)
            : base(webConnection.WebServer, callingFrom, webConnection.Generation + 1)
        {
            _Content = new WebConnectionContent.InMemory(content);
            _ContentType = contentType;
            _Session = webConnection.Session;
            _Method = method;
            _CookiesFromBrowser = cookiesFromBrowser;
            _CookiesToSet = webConnection.CookiesToSet;
            _HttpVersion = webConnection.HttpVersion;
            _RequestedHost = webConnection.RequestedHost;
            _Headers = new Dictionary<string, string>(webConnection.Headers);
            _MimeReader = webConnection.MimeReader;

            BaseWebConnection = webConnection;

            DetermineRequestedFileAndGetParameters(url);
            TryDecodePostParameters();
        }

        public ShellWebConnection(
            IWebConnection webConnection,
            ISession session,
            string requestedFile,
            RequestParameters getParameters,
            byte[] content,
            string contentType,
            CookiesFromBrowser cookiesFromBrowser,
            CallingFrom callingFrom)
            : base(webConnection.WebServer, callingFrom, webConnection.Generation + 1)
        {
            _Content = new WebConnectionContent.InMemory(content);
            _ContentType = contentType;
            _Session = session;
            _Method = WebMethod.GET;
            _CookiesFromBrowser = cookiesFromBrowser;
            _CookiesToSet = webConnection.CookiesToSet;
            _HttpVersion = webConnection.HttpVersion;
            _RequestedHost = webConnection.RequestedHost;
            _Headers = new Dictionary<string, string>(webConnection.Headers);
            _MimeReader = webConnection.MimeReader;

            BaseWebConnection = webConnection;

            _RequestedFile = requestedFile;
            _GetParameters = getParameters;
            _PostParameters = null;
        }

        public ShellWebConnection(
            string url,
            IWebConnection webConnection,
            RequestParameters postParameters,
            CookiesFromBrowser cookiesFromBrowser,
            CallingFrom callingFrom,
            WebMethod method)
            : base(webConnection.WebServer, callingFrom, webConnection.Generation + 1)
        {
            _PostParameters = postParameters;
            _Content = webConnection.Content;
            _ContentType = webConnection.ContentType;
            _Session = webConnection.Session;
            _Method = method;
            _CookiesFromBrowser = cookiesFromBrowser;
            _CookiesToSet = webConnection.CookiesToSet;
            _HttpVersion = webConnection.HttpVersion;
            _RequestedHost = webConnection.RequestedHost;
            _Headers = new Dictionary<string, string>(webConnection.Headers);
            _MimeReader = webConnection.MimeReader;

            BaseWebConnection = webConnection;

            DetermineRequestedFileAndGetParameters(url);
        }

        /// <summary>
        /// Constructor for when a web request is generated publicly instead of externally
        /// </summary>
        /// <param name="webServer"></param>
        /// <param name="session"></param>
        /// <param name="url"></param>
        /// <param name="content"></param>
        /// <param name="contentType"></param>
        /// <param name="cookiesFromBrowser"></param>
        /// <param name="callingFrom"></param>
        /// <param name="method"></param>
        public ShellWebConnection(
            IWebServer webServer,
            ISession session,
            string url,
            byte[] content,
            string contentType,
            CookiesFromBrowser cookiesFromBrowser,
            CallingFrom callingFrom,
            WebMethod method)
            : base(webServer, callingFrom, 0)
        {
            _Content = new WebConnectionContent.InMemory(content);
            _ContentType = contentType;
            _Session = session;
            _Method = method;
            _CookiesFromBrowser = cookiesFromBrowser;
            _CookiesToSet = new List<CookieToSet>();
            _HttpVersion = 1;
            _RequestedHost = null;
            _Headers = new Dictionary<string, string>();

            DetermineRequestedFileAndGetParameters(url);

            TryDecodePostParameters();

            if (null == BaseWebConnection)
                BaseWebConnection = this;
        }

        protected IWebConnection BaseWebConnection = null;

        public override bool Connected
        {
            get
            {
                if (null != BaseWebConnection)
                    return BaseWebConnection.Connected;
                else
                    // If this is an publicly-generated event, then there is always a connection!
                    return true;
            }
        }

        public override EndPoint RemoteEndPoint
        {
            get
            {
                if (null != BaseWebConnection)
                    return BaseWebConnection.RemoteEndPoint;

                return null;
            }
        }

        public override HashSet<ObjectCloud.Interfaces.Disk.IFileContainer> TouchedFiles
        {
            get
            {
                if (this != BaseWebConnection)
                    return BaseWebConnection.TouchedFiles;

                if (null == _TouchedFiles)
                    _TouchedFiles = new HashSet<ObjectCloud.Interfaces.Disk.IFileContainer>();

                return _TouchedFiles;
            }
        }
        private HashSet<ObjectCloud.Interfaces.Disk.IFileContainer> _TouchedFiles;

        public override HashSet<string> Scripts
        {
            get
            {
                if (this != BaseWebConnection)
                    return BaseWebConnection.Scripts;

                if (null == _Scripts)
                    _Scripts = new HashSet<string>();

                return _Scripts;
            }
        }
        private HashSet<string> _Scripts;

        public override string GetBrowserCacheUrl(string url)
        {
            if ((null == BaseWebConnection) || (this == BaseWebConnection))
                return base.GetBrowserCacheUrl(url);
            else
                return BaseWebConnection.GetBrowserCacheUrl(url);
        }

        public override IBrowser UserAgent
        {
            get { return BaseWebConnection.UserAgent; }
        }
    }
}
