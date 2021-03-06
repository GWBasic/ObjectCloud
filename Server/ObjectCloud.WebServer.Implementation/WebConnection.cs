// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;	
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.Common.Threading;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

using File = ObjectCloud.Interfaces.Disk.IFileContainer;

namespace ObjectCloud.WebServer.Implementation
{
    public class WebConnection : WebConnectionBase
	{
		private static ILog log = LogManager.GetLogger(typeof(WebConnection));

        /// <summary>
        /// Initializes the WebConnection
        /// </summary>
        /// <param name="s"></param>
        /// <param name="webServer"></param>
        public WebConnection(WebServer webServer, EndPoint remoteEndPoint, Action<Stream> sendToBrowser)
            : base(webServer, CallingFrom.Web)
        {
            _RemoteEndPoint = remoteEndPoint;
            SendToBrowser = sendToBrowser;
        }

        public override EndPoint RemoteEndPoint
        {
            get { return _RemoteEndPoint; }
        }
        private EndPoint _RemoteEndPoint;

        /// <summary>
        /// Reads the client's header.  Does not read POST parameters
        /// </summary>
        /// <returns>True if the connection should be handled, false if the connection should be discontinued</returns>
        /// <exception cref="WebResultsOverrideException">Thrown if the header is malformed</exception>
        public void ReadHeader(string header)
        {
            log.Info("-------\nIncoming request from client:\n" + header);

            // Clear out old header info
            _Headers.Clear();

            string[] headerLines = header.Split(new string[] { "\r\n" }, StringSplitOptions.None);

            if (headerLines.Length < 1)
                throw new WebResultsOverrideException(WebResults.From(Status._400_Bad_Request, "No request!!!"));

            // Read request type and filename
            string requestTypeAndName = headerLines[0];

            // Determine the request type
            string protocolString = requestTypeAndName.Split(new char[] { ' ', '\t' })[0];

            try
            {
                _Method = Enum<WebMethod>.Parse(protocolString);
            }
            catch
            {
				log.Warn(protocolString + " isn't supported");
                _Method = WebMethod.other;
            }

            // Look for HTTP request
            int HTTP_Pos = requestTypeAndName.IndexOf("HTTP", 1);

            // Get the HTTP text and version e.g. it will return "HTTP/1.1"
            string httpVersion = requestTypeAndName.Substring(HTTP_Pos, 8);
            _HttpVersion = 1;
            double.TryParse(httpVersion.Substring(5), out _HttpVersion);

            // Extract the Requested Type and Requested file/directory
            string requestString = requestTypeAndName.Substring(0, HTTP_Pos - 1);
            requestString = requestString.Substring(_Method.ToString().Length + 1);

            DetermineRequestedFileAndGetParameters(requestString);

            // now, read each line of the rest of the header
            for (int lineCtr = 1; lineCtr < headerLines.Length; lineCtr++)
            {
                string[] lineTypeAndParms = headerLines[lineCtr].Split(new char[] { ':' }, 2);

                if (lineTypeAndParms.Length < 2)
                    throw new WebResultsOverrideException(WebResults.From(Status._406_Not_Acceptable, "Bad header:\n" + headerLines[lineCtr]));

                _Headers[lineTypeAndParms[0].ToUpper()] = lineTypeAndParms[1].Trim();
            }

            // Read additional properties that come from the header, including cookies
            ReadPropertiesFromHeader();
        }

		/// <summary>
		/// The number of active web connections 
		/// </summary>
		private static int NumActiveConnections = 0;
		
		/// <summary>
		/// The number of requests since a full GC was run 
		/// </summary>
		private static int NumRequestsSinceLastGC = 0;

        /// <summary>
        /// Entry-point to handle the connection that's established on the socket
        /// </summary>
        /// <param name="state"></param>
        public virtual void HandleConnection(IWebConnectionContent content)
        {
            Thread thread = Thread.CurrentThread;

            // Signal that this was the abort to catch
            bool abortedHere = false;

            bool completed = false;

            TimerCallback timerCallback = delegate(object state)
            {
                if (completed)
                    return;

                if (System.Diagnostics.Debugger.IsAttached)
                    System.Diagnostics.Debugger.Break();
                else
                {
                    log.Warn(thread.Name + " is blocked, aborting, ThreadID: " + thread.ManagedThreadId.ToString());

                    try
                    {
                        abortedHere = true;
                        thread.Abort();
                    }
                    catch { }
                }
            };

            try
            {
                using (var timer = new Timer(timerCallback, null, 10000, System.Threading.Timeout.Infinite))
                    HandleConnectionInt(content);

                completed = true;
            }
            catch (ThreadAbortException tae)
            {
                if (abortedHere)
                {
                    log.Warn("Thread was aborted", tae);
                    Thread.ResetAbort();
                }
            }
        }
        

        /// <summary>
        /// Entry-point to handle the connection that's established on the socket
        /// </summary>
        /// <param name="state"></param>
        private void HandleConnectionInt(IWebConnectionContent content)
        {
            _Content = content;

            DateTime startTime = DateTime.UtcNow;

            // Decode POST parameters if they are present
            TryDecodePostParameters();

            ILoggerFactoryAdapter loggerFactoryAdapter = LogManager.Adapter;
            if (loggerFactoryAdapter is IObjectCloudLoggingFactoryAdapter)
                ((IObjectCloudLoggingFactoryAdapter)loggerFactoryAdapter).RemoteEndPoint = RemoteEndPoint;

			Interlocked.Increment(ref NumActiveConnections);
			
            try
            {
                try
                {
                    if (log.IsDebugEnabled)
                        log.Debug("File Requested : " + _RequestedFile + "\n===================\n");

                    // Load the session object or create a new one
                    LoadSession();

                    // Try to support some caching
                    if (WebMethod.GET == Method && WebServer.CachingEnabled && 0 == GetParameters.Count && Headers.ContainsKey("IF-MODIFIED-SINCE"))
                        if (WebServer.FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(RequestedFile).LoadPermission(Session.User.Id) >=
                            FilePermissionEnum.Read)
                        {
                            HashSet<IFileContainer> touchedFiles = Session.GetFilesTouchedForUrl(RequestedFile);

                            if (null != touchedFiles)
                            {
                                DateTime ifModifiedSince = DateTime.Parse(Headers["IF-MODIFIED-SINCE"]);

                                bool useCached = true;

                                foreach (IFileContainer fileContainer in touchedFiles)
                                    if (WebServer.FileHandlerFactoryLocator.SessionManagerHandler.FileContainer != fileContainer)
                                        if (fileContainer.LastModified > ifModifiedSince)
                                        {
                                            useCached = false;
                                            break;
                                        }

                                if (useCached)
                                {
                                    SendResults(WebResults.From(Status._304_Not_Modified));
                                    return;
                                }
                            }
                        }

                    IWebResults webResults = null;

                    try
                    {
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
                                if (redirectUrl.Contains("?"))
                                    redirectUrl += "&" + GetParameters.ToURLEncodedString();
                                else
                                    redirectUrl += "?" + GetParameters.ToURLEncodedString();

                            webResults = WebResults.Redirect(redirectUrl);
                        }
                    }
                    catch (WebResultsOverrideException wroe)
                    {
                        webResults = wroe.WebResults;
                    }
                    catch (Exception e)
                    {
                        log.Error("Exception occured while handling a web request", e);
                        webResults = WebResults.From(Status._500_Internal_Server_Error, "An unhandled error occured");
                    }

                    if (log.IsDebugEnabled)
                        log.Debug(string.Format("Request handled in time: {0}", DateTime.UtcNow - startTime));

                    // Finally, send the resuts to the browser
                    if (null != webResults)
                    {
                        // For static information, include information that supports caching
                        if (WebMethod.GET == Method && 0 == GetParameters.Count && WebServer.CachingEnabled)
                            if ((!webResults.Headers.ContainsKey("Expires")) && (!webResults.Headers.ContainsKey("Cache-Control")))
                            {
                                Session.SetFilesTouchedForUrl(RequestedFile, TouchedFiles);

                                // Figure out the most recent file touched
                                DateTime mostRecentChange = DateTime.MinValue;
                                foreach (IFileContainer fileContainer in TouchedFiles)
                                    if (fileContainer.LastModified > mostRecentChange)
                                        mostRecentChange = fileContainer.LastModified;

                                webResults.Headers["Last-Modified"] = mostRecentChange.ToString("r");
                                webResults.Headers["Cache-Control"] = "private, must-revalidate";
                            }

                        SendResults(webResults);
                    }
                }
                catch (WebResultsOverrideException wroe)
                {
                    log.Error("WebResultsOverrideException exception while handling a web connection", wroe);

                    using (TimedLock.Lock(_Connected))
                        if (_Connected.Value)
                            SendResults(wroe.WebResults);
                }
                catch (NotImplementedException ne)
                {
                    log.Error("NotImplementedUnhandled exception while handling a web connection", ne);

                    using (TimedLock.Lock(_Connected))
                        if (_Connected.Value)
                            SendResults(WebResults.From(Status._501_Not_Implemented));
                }
                catch (Exception e)
                {
                    log.Error("Unhandled exception while handling a web connection", e);

                    using (TimedLock.Lock(_Connected))
                        if (_Connected.Value)
                            SendResults(WebResults.From(Status._500_Internal_Server_Error, "An unhandled error occured"));
                }
            }
            finally
            {
                _Content = null;

                // Do cleanup in case the object is reused
                CookiesToSet.Clear();
                _MimeReader = null;

                // Make sure that the logger doesn't hold the session
                _Session = null;

                if (loggerFactoryAdapter is IObjectCloudLoggingFactoryAdapter)
                    ((IObjectCloudLoggingFactoryAdapter)loggerFactoryAdapter).Session = null;

                if (loggerFactoryAdapter is IObjectCloudLoggingFactoryAdapter)
                    ((IObjectCloudLoggingFactoryAdapter)loggerFactoryAdapter).RemoteEndPoint = null;
				
				// Decrement the number of active connections
				int numActiveConnections = Interlocked.Decrement(ref NumActiveConnections);
				
				// If there are no running web connections and we've hit the MinRequestsBeforeGarbageCollection threshold, force a garbage collection
				// Else, if we've hit the MaxRequestsBeforeGarbageCollection threshold, force a garbage collection
				int numRequestsSinceLastGC = NumRequestsSinceLastGC;
				if (((0 == numActiveConnections) && (numRequestsSinceLastGC > WebServer.MinRequestsBeforeGarbageCollection))
				    || (numRequestsSinceLastGC > WebServer.MaxRequestsBeforeGarbageCollection))
				{
					// Interlocked makes sure that we only schedule the GC if this is the thread that sets NumRequestsSinceLastGC
                    if (numRequestsSinceLastGC == Interlocked.CompareExchange(ref NumRequestsSinceLastGC, 0, numRequestsSinceLastGC))
                        GC.Collect();
				}
				else
					Interlocked.Increment(ref NumRequestsSinceLastGC);
            }
        }

        /// <summary>
        /// This is set to false when results are sent
        /// </summary>
        public override bool Connected
        {
            get { return _Connected.Value; }
        }
        private Wrapped<bool> _Connected = true;

        /// <summary>
        /// Generates the header
        /// </summary>
        public override void SendResults(IWebResults webResults)
        {
            using (TimedLock.Lock(_Connected))
            {
                if (!_Connected.Value)
                    throw new ResultsAlreadySent("Can not send results twice for the same web connection");

                _Connected = false;
            }

            StringBuilder header = new StringBuilder();

            // Convert the status enum into something that a web browser can handle
            string statusString = webResults.Status.ToString().Substring(1);
            statusString = statusString.Replace('_', ' ');

            header.AppendFormat("HTTP/1.1 {0}\r\n", statusString);

            Dictionary<string, string> headers = new Dictionary<string, string>(webResults.Headers);
            headers["Server"] = WebServer.ServerType;

            foreach (string headerKey in headers.Keys)
                header.AppendFormat("{0}: {1}\r\n", headerKey, headers[headerKey]);

            // set cookies
            string formattedCookies = FormatCookiesToSend();
            header.Append(formattedCookies);

            Stream webResultStream = webResults.ResultsAsStream;

            header.AppendFormat("Content-Length: {0}\r\n", webResultStream.Length);

            if (log.IsDebugEnabled)
                log.Debug("Results sent to browser:\n" + header.ToString());

            header.Append("\r\n");

            MultiStream stream = new MultiStream();
            stream.AddStream(new MemoryStream(Encoding.UTF8.GetBytes(header.ToString())));
            stream.AddStream(webResultStream);

            SendToBrowser(stream);
        }

        /// <summary>
        /// Sends the stream of data to the browser.  The recipient must close and dispose the stream
        /// </summary>
        /// <param name="stream"></param>
        private Action<Stream> SendToBrowser;

        /// <summary>
        /// Formats the cookies to send for the header of the response to the client
        /// </summary>
        /// <param name="cookiesToSend"></param>
        /// <param name="cookiesToSendExpiration"></param>
        /// <returns></returns>
        protected string FormatCookiesToSend()
        {
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

            StringBuilder toReturn = new StringBuilder();

            foreach (CookieToSet cookieToSet in CookiesToSet)
            {
                toReturn.AppendFormat(
                    "Set-Cookie: {0}={1}; ",
                    HTTPStringFunctions.EncodeRequestParametersForBrowser(cookieToSet.Name),
                    HTTPStringFunctions.EncodeRequestParametersForBrowser(cookieToSet.Value));

                if (null != cookieToSet.Expires)
                    toReturn.AppendFormat(
                        "expires={0} GMT; ",
                        cookieToSet.Expires.Value.ToString("ddd, dd-MMM-yyyy HH:mm:ss"));

                if (cookieToSet.Secure)
                    toReturn.Append("Secure; ");

                if (null != cookieToSet.Path)
                    toReturn.AppendFormat("Path={0}; ", cookieToSet.Path);

                toReturn.Append("\r\n");
            }

            return toReturn.ToString();
        }

        public override HashSet<IFileContainer> TouchedFiles
        {
            get { return _TouchedFiles; }
        }
        private readonly HashSet<IFileContainer> _TouchedFiles = new HashSet<IFileContainer>();
		
		/// <summary>
		/// The web server 
		/// </summary>
		public new WebServer WebServer
		{
			get { return (WebServer)base.WebServer; }
		}
    }
}


