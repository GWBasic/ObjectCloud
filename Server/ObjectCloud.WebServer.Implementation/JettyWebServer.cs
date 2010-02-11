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

using javax.servlet.http;
using org.eclipse.jetty.server;
using org.eclipse.jetty.server.handler;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.WebServer.Implementation
{
    public class JettyWebServer : WebServerBase
    {
        private ILog log = LogManager.GetLogger(typeof(JettyWebServer));

        public JettyWebServer() : base(80) { }

        public JettyWebServer(int port) : base(port) { }

        /// <summary>
        /// The Jetty server
        /// </summary>
        private Server Server = null;

        public override void RunServer()
        {
            log.Info("Starting: " + this.ServerType);

            object numConnectionsLock = new object();

            FileHandlerFactoryLocator.FileSystemResolver.Start();

            _ServerThread = Thread.CurrentThread;

            Server = new Server(Port);

            ConnectionHandler connectionHandler = new ConnectionHandler();
            connectionHandler.WebServer = this;

            Server.setHandler(connectionHandler);

            Server.start();

            _Running = true;
            AcceptingSockets = true;

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

            Server.stop();

            log.Info("Web server stopped without error");

            FileHandlerFactoryLocator.FileSystemResolver.Stop();

            try
            {
                OnWebServerTerminated(new EventArgs());
            }
            catch { }
        }

        private class ConnectionHandler : AbstractHandler
        {
            internal IWebServer WebServer;

            public override void handle(String target, Request baseRequest, HttpServletRequest request, HttpServletResponse response)
            {
                new JettyWebConnection(WebServer, target, baseRequest, request, response).Handle();
            }
        }
    }
}
