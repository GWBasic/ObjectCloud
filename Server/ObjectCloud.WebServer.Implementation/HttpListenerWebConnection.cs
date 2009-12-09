// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.WebServer.Implementation
{
    public class HttpListenerWebConnection : WebConnectionCommon
    {
        private static ILog log = LogManager.GetLogger(typeof(HttpListenerWebConnection));

        public HttpListenerWebConnection(IWebServer webServer, HttpListenerContext context)
            : base(webServer, CallingFrom.Web)
        {
            Context = context;
            Request = Context.Request;
            Response = Context.Response;
        }

        /// <summary>
        /// The context
        /// </summary>
        HttpListenerContext Context;
        HttpListenerRequest Request;
        HttpListenerResponse Response;

        public override bool Connected
        {
            get { return _Connected; }
        }
        private bool _Connected;

        public void Handle()
        {
            DetermineRequestedFileAndGetParameters(Request.RawUrl);
        
            ILoggerFactoryAdapter loggerFactoryAdapter = LogManager.Adapter;
            if (loggerFactoryAdapter is IObjectCloudLoggingFactoryAdapter)
                ((IObjectCloudLoggingFactoryAdapter)loggerFactoryAdapter).RemoteEndPoint = RemoteEndPoint;

            log.Info("File Requested : " + _RequestedFile + "\n===================\n");

            try
            {
                try
                {
                    _Method = Enum<WebMethod>.Parse(Request.HttpMethod);
                }
                catch
                {
                    log.Warn(Request.HttpMethod + " isn't supported");
                    _Method = WebMethod.other;
                }

                IWebResults webResults;

                try
                {
                    _HttpVersion = null;

                    StringBuilder headers = new StringBuilder("Headers:\n");

                    NameValueCollection Headers = Request.Headers;
                    foreach (string headerName in Headers.AllKeys)
                    {
                        string headerValue = Headers[headerName];
                        _Headers[headerName.ToUpper()] = headerValue;

                        headers.AppendLine(string.Format("\t{0}: {1}", headerName, headerValue));
                    }

                    log.Info(headers.ToString());

                    ReadPropertiesFromHeader();

                    LoadSession();

                    // TODO:  Offload the reading to some kind of a read queue that runs on a single thread
                    // This current system allows a DOS if 25-100 slow sockets are opened on the server

                    byte[] payload = StreamFunctions.ReadAllBytes(Request.InputStream);
                    _Content = new ObjectCloud.Interfaces.WebServer.WebConnectionContent.InMemory(payload);
                    TryDecodePostParameters();

                    if (WebServer.FileHandlerFactoryLocator.HostnameAndPort.Equals(RequestedHost))
                    {
                        // Generate the results for the client.  The action taken can vary, depending on file name and arguments
                        DateTime generateResultsStartTime = DateTime.UtcNow;
                        webResults = GenerateResultsForClient();
                        if (log.IsDebugEnabled)
                            log.Debug(string.Format("GenerateResultsForClient() handled in time: {0}", DateTime.UtcNow - generateResultsStartTime));
                    }
                    else
                    {
                        // The user requested the wrong host; redirect

                        string redirectUrl = "http://" + WebServer.FileHandlerFactoryLocator.HostnameAndPort + RequestedFile;

                        if (GetParameters.Count > 0)
                            redirectUrl += "&" + GetParameters.ToURLEncodedString();

                        webResults = WebResults.Redirect(redirectUrl);
                    }
                }
                catch (Exception e)
                {
                    log.Error("Exception occured while handling a web request", e);

                    webResults = WebResults.FromString(Status._500_Internal_Server_Error, "An unhandled error occured");
                }

                if (null != webResults)
                    SendResults(webResults);
            }
            catch (Exception ex)
            {
                log.Error("Unhandled exception when handling a web connection:", ex);
            }
            finally
            {
                try
                {
                    Response.Close();
                }
                catch { }

                if (loggerFactoryAdapter is IObjectCloudLoggingFactoryAdapter)
                {
                    IObjectCloudLoggingFactoryAdapter loggerFactoryAdapterOC = (IObjectCloudLoggingFactoryAdapter)loggerFactoryAdapter;
                    loggerFactoryAdapterOC.RemoteEndPoint = null;
                    loggerFactoryAdapterOC.Session = null;
                }

                _Connected = false;
            }
        }

        public override void SendResults(IWebResults webResults)
        {
            Response.KeepAlive = WebServer.KeepAlive;
            Response.StatusCode = (int)webResults.Status;
            Response.ContentLength64 = webResults.Body.LongLength;

            foreach (KeyValuePair<string, string> header in webResults.Headers)
                Response.Headers[header.Key] = header.Value;

            if (null != _Session)
            {
                // Make sure that the session cookie is sent...
                // It is set last to make sure that all changes to the session are persisted
                CookieToSet sessionCookie = new CookieToSet("SESSION");
                sessionCookie.Path = "/";
                sessionCookie.Value = _Session.SessionId.ToString();

                if (_Session.KeepAlive)
                {
                    TimeSpan maxAge = _Session.MaxAge;
                    sessionCookie.Expires = DateTime.UtcNow + maxAge;
                    sessionCookie.Value = sessionCookie.Value + ", " + maxAge.TotalDays.ToString(CultureInfo.InvariantCulture);
                }

                CookiesToSet.Add(sessionCookie);
            }

            // Use the HttpListener version of this server is discouraged.  It just doesn't set cookies in a reliable manner
            CookieCollection cookies = new CookieCollection();

            foreach (CookieToSet cookie in CookiesToSet)
            {
                Cookie nCookie = new Cookie(
                    HTTPStringFunctions.EncodeRequestParametersForBrowser(cookie.Name),
                    HTTPStringFunctions.EncodeRequestParametersForBrowser(cookie.Value));

                cookies.Add(nCookie);

                if (null != cookie.Expires)
                    nCookie.Expires = cookie.Expires.Value;

                nCookie.Secure = cookie.Secure;

                nCookie.Path = cookie.Path;
                nCookie.Version = 2;
            }

            Response.Cookies = cookies;

            Response.Headers["Server"] = WebServer.ServerType;

            // TODO:  Move these to some kind of a writer thread
            Response.OutputStream.Write(webResults.Body, 0, webResults.Body.Length);
            Response.OutputStream.Flush();
        }

        public override EndPoint RemoteEndPoint
        {
            get { return Request.RemoteEndPoint; }
        }
    }
}
