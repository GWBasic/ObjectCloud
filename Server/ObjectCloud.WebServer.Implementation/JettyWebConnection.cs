// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

using Common.Logging;

using javax.servlet.http;
using org.eclipse.jetty.server;
using org.eclipse.jetty.server.handler;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.WebServer.Implementation
{
    public class JettyWebConnection : WebConnectionCommon
    {
        private static ILog log = LogManager.GetLogger(typeof(JettyWebConnection));

        public JettyWebConnection(IWebServer webServer, String target, Request baseRequest, HttpServletRequest request, HttpServletResponse response)
            : base(webServer, CallingFrom.Web)
        {
            Response = response;
            BaseRequest = baseRequest;
            Request = request;

            _RequestedFile = target;

            org.eclipse.jetty.io.EndPoint jettyEndpoint = baseRequest.getConnection().getEndPoint();
            string remoteAddress = jettyEndpoint.getRemoteAddr();
            int remotePort = jettyEndpoint.getRemotePort();

            _RemoteEndPoint = new IPEndPoint(IPAddress.Parse(remoteAddress), remotePort);
        }

        HttpServletResponse Response;
        Request BaseRequest;
        HttpServletRequest Request;

        public override bool Connected
        {
            get { return true; }
        }

        public override EndPoint RemoteEndPoint
        {
            get { return _RemoteEndPoint; }
        }
        private EndPoint _RemoteEndPoint = null;

        public void Handle()
        {
            ILoggerFactoryAdapter loggerFactoryAdapter = LogManager.Adapter;
            if (loggerFactoryAdapter is IObjectCloudLoggingFactoryAdapter)
                ((IObjectCloudLoggingFactoryAdapter)loggerFactoryAdapter).RemoteEndPoint = RemoteEndPoint;

            log.Info("File Requested : " + _RequestedFile + "\n===================\n");

            try
            {
                try
                {
                    _Method = Enum<WebMethod>.Parse(Request.getMethod());
                }
                catch
                {
                    log.Warn(Request.getMethod() + " isn't supported");
                    _Method = WebMethod.other;
                }

                /*
                 AuthType: is null
Cookies: is null
HeaderNames: org.eclipse.jetty.http.HttpFields$3@16e4abc
Method: GET
PathInfo: /abc
PathTranslated: is null
ContextPath: is null
QueryString: a=b
RemoteUser: is null
UserPrincipal: is null
RequestedSessionId: is null
RequestURI: /abc
RequestURL: http://localhost:1080/abc
ServletPath:
                 */

                IWebResults webResults;

                try
                {
                    _HttpVersion = null;

                    string queryString = Request.getQueryString();

                    if (null != queryString)
                        if (queryString.Length > 0)
                            _GetParameters = new RequestParameters(queryString);
                        else
                            _GetParameters = new RequestParameters();
                    else
                        _GetParameters = new RequestParameters();

                    StringBuilder headers = new StringBuilder("Headers:\n");

                    for (java.util.Enumeration headernameEnumerator = Request.getHeaderNames(); headernameEnumerator.hasMoreElements(); )
                    {
                        string headerName = (string)headernameEnumerator.nextElement();
                        string headerValue = Request.getHeader(headerName);
                        _Header[headerName.ToUpper()] = headerValue;

                        headers.AppendLine(string.Format("\t{0}: {1}", headerName, headerValue));

                        log.Info(headers.ToString());
                    }

                    ReadPropertiesFromHeader();

                    LoadSession();

                    javax.servlet.ServletInputStream inStream = Request.getInputStream();

                    List<byte[]> dataRecieved = new List<byte[]>();
                    byte[] buffer = new byte[0x10000];
                    int bytesRead;
                    int totalBytesRead = 0;

                    do
                    {
                        bytesRead = inStream.read(buffer, 0, buffer.Length);

                        if (bytesRead > 0)
                        {
                            byte[] copy = new byte[bytesRead];
                            Array.Copy(buffer, copy, bytesRead);

                            dataRecieved.Add(copy);

                            totalBytesRead += bytesRead;
                        }
                    }
                    while (bytesRead >= 0);

                    byte[] payload = new byte[totalBytesRead];
                    int bytesCopied = 0;
                    foreach (byte[] copy in dataRecieved)
                    {
                        Array.Copy(copy, 0, payload, bytesCopied, copy.Length);
                        bytesCopied += copy.Length;
                    }

                    _Content = new WebConnectionContent.InMemory(payload);
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

                Response.setStatus((int)webResults.Status);
                Response.setContentLength(webResults.Body.Length);

                foreach (KeyValuePair<string, string> header in webResults.Headers)
                    Response.setHeader(header.Key, header.Value);

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

                foreach (CookieToSet cookie in CookiesToSet)
                {
                    javax.servlet.http.Cookie jCookie = new javax.servlet.http.Cookie(
                        HTTPStringFunctions.EncodeRequestParametersForBrowser(cookie.Name),
                        HTTPStringFunctions.EncodeRequestParametersForBrowser(cookie.Value));

                    jCookie.setPath(cookie.Path);

                    if (null != cookie.Expires)
                    {
                        TimeSpan maxAge = cookie.Expires.Value - DateTime.UtcNow;
                        jCookie.setMaxAge(Convert.ToInt32(maxAge.TotalSeconds));
                    }

                    jCookie.setSecure(cookie.Secure);

                    Response.addCookie(jCookie);
                }

                Response.setHeader("Server", WebServer.ServerType);
                Response.getOutputStream().write(webResults.Body);
            }
            finally
            {
                if (loggerFactoryAdapter is IObjectCloudLoggingFactoryAdapter)
                {
                    IObjectCloudLoggingFactoryAdapter loggerFactoryAdapterOC = (IObjectCloudLoggingFactoryAdapter)loggerFactoryAdapter;
                    loggerFactoryAdapterOC.RemoteEndPoint = null;
                    loggerFactoryAdapterOC.Session = null;
                }
            }
        }
    }
}
