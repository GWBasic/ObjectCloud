// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Reflection;
using System.Threading;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.WebServer.Implementation
{
	/// <summary>
	/// Use the HttpListener version of this server is discouraged.  It just doesn't set cookies in a reliable manner 
	/// </summary>
    public class HttpListenerWebServer : WebServerBase
    {
        private ILog log = LogManager.GetLogger(typeof(HttpListenerWebServer));

        public HttpListenerWebServer() : base(80) { }

        public HttpListenerWebServer(int port) : base(port) { }

        /// <summary>
        /// The HttpListener
        /// </summary>
        private HttpListener HttpListener = null;

        public override void RunServer()
        {
            try
            {
                log.Info("Starting: " + this.ServerType);

                FileHandlerFactoryLocator.FileSystemResolver.Start();

                _ServerThread = Thread.CurrentThread;

                using (HttpListener = new HttpListener())
                {
                    HttpListener.Prefixes.Add("http://+:" + Port.ToString() + "/");

                    HttpListener.Start();

                    log.Info("Server is waiting for a new connection at http://" + FileHandlerFactoryLocator.HostnameAndPort + "/");

                    _Running = true;
                    AcceptingSockets = true;

                    while (HttpListener.IsListening)
                    {
                        try
                        {
                            HttpListenerContext context = HttpListener.GetContext();

                            if (null != context)
                            {
                                // TODO:  Prior to handling the connection on the thread-pool, use a single-thread to read all incoming data
                                // from all pending requests

                                HttpListenerWebConnection webConnection = new HttpListenerWebConnection(this, context);

                                ThreadPool.QueueUserWorkItem(delegate(object webConnectionObj)
                                {
                                    HttpListenerWebConnection myWebConnection = (HttpListenerWebConnection)webConnectionObj;

                                    base.OnWebConnectionStarting(new EventArgs<IWebConnection>(myWebConnection));
                                    
                                    myWebConnection.Handle();

                                    base.OnWebConnectionComplete(new EventArgs<IWebConnection>(myWebConnection));

                                }, webConnection);
                            }
                        }
                        catch (ObjectDisposedException)
                        {
                            return;
                        }
                    }

                    log.Info("Server terminated without error");
                }
            }
            catch (Exception e)
            {
                log.Fatal("The web server aborted with the following exception", e);
            }
        }

        public override void Stop()
        {
            _Running = false;

            try
            {
                OnWebServerTerminating(new EventArgs());
            }
            catch { }

            log.Info("Stopping");

            HttpListener.Stop();

            log.Info("Web server stopped without error");

            FileHandlerFactoryLocator.FileSystemResolver.Stop();

            try
            {
                OnWebServerTerminated(new EventArgs());
            }
            catch { }
        }
    }
}
