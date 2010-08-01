// Copyright 2009, 2010 Andrew Rondeau
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
using System.Text;
using System.Threading;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.Common.Threading;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.WebServer.Implementation
{
    public class NonBlockingWebServer : WebServerBase
    {
        private static ILog log = LogManager.GetLogger(typeof(NonBlockingWebServer));

        public NonBlockingWebServer() : this(80) { }

        public NonBlockingWebServer(int port) : base(port) { }

        /// <summary>
        /// The TcpListener
        /// </summary>
        TcpListener TcpListener;

        /// <summary>
        /// Actually runs the server 
        /// </summary>
        public override void RunServer()
        {
            log.Info("Starting: " + this.ServerType);

            FileHandlerFactoryLocator.FileSystemResolver.Start();

            _Running = true;

            _ServerThread = Thread.CurrentThread;

            TcpListener = new TcpListener(IPAddress.Any, Port);
            TcpListener.Start(20);

            log.Info("Server is waiting for a new connection at http://" + FileHandlerFactoryLocator.HostnameAndPort + "/");

            AcceptingSockets = true;

            TcpListener.BeginAcceptSocket(AcceptSocket, null);
        }

        private void AcceptSocket(IAsyncResult ar)
        {
            TcpListener tcpListener = TcpListener;

            if (null != tcpListener)
                try
                {
                    Socket socket = tcpListener.EndAcceptSocket(ar);

                    log.Info("Accepted connection form: " + socket.RemoteEndPoint);

                    NonBlockingSocketReader socketReader = new NonBlockingSocketReader(this, socket);

                    socketReader.Start();

                    tcpListener = TcpListener;
                    if (null != tcpListener)
                        tcpListener.BeginAcceptSocket(AcceptSocket, null);
                }
                // This is in case the listener is disposed
                catch (ObjectDisposedException) { }
        }

        /// <summary>
        /// Stops the server and releases all resources
        /// </summary>
        protected override void StopImpl()
        {
            log.Info("Shutting down " + ServerType);

            _Running = false;

            try
            {
                OnWebServerTerminating(new EventArgs());
            }
            catch { }


            TcpListener tcpListener = TcpListener;
            TcpListener = null;
            tcpListener.Stop();

            ServerThread.Join();
            _ServerThread = null;

            try
            {
                OnWebServerTerminated(new EventArgs());
            }
            catch { }

            FileHandlerFactoryLocator.FileSystemResolver.Stop();
        }
    }
}
