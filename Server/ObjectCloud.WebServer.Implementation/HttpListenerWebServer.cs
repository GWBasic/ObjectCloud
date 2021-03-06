// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
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

                HttpListener = new HttpListener();
                HttpListener.Prefixes.Add("http://*:" + Port.ToString() + "/");
                HttpListener.Start();

                log.Info("Server is waiting for a new connection at http://" + FileHandlerFactoryLocator.HostnameAndPort + "/");

                _Running = true;

                HttpListener.BeginGetContext(HttpListenerCallback, null);
            }
            catch (Exception e)
            {
                log.Fatal("Error starting server", e);
                throw;
            }
        }

        private void HttpListenerCallback(IAsyncResult ar)
        {
            try
            {
                HttpListenerContext context = (HttpListenerContext)HttpListener.EndGetContext(ar);
                RequestDelegateQueue.QueueUserWorkItem(HandleHttpListenerContext, context);
            }
            catch (Exception e)
            {
                if (!Running)
                    return;
                else
                    log.Error("Exception accepting an incoming HTTP request", e);
            }

            if (Running)
                try
                {
                    HttpListener.BeginGetContext(HttpListenerCallback, null);
                }
                catch (Exception e)
                {
                    log.Error("Exception waiting for another incoming HTTP request", e);

                    if (Running)
                        try
                        {
                            Stop();
                        }
                        catch (Exception e2)
                        {
                            log.Error("Exception stopping", e2);
                        }
                }
        }

        private void HandleHttpListenerContext(object state)
        {
            HttpListenerWebConnection webConnection = new HttpListenerWebConnection(this, (HttpListenerContext)state);
            webConnection.Handle();
        }

        protected override void StopImpl()
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
