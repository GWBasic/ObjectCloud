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
    public class WebServer : WebServerBase
    {
		private ILog log = LogManager.GetLogger(typeof(WebServer));

        public WebServer() : base(80) { }

        public WebServer(int port) : base(port){}

        /// <summary>
        /// Actually runs the server 
        /// </summary>
        public override void RunServer()
        {
            log.Info("Starting: " + this.ServerType);

            object numConnectionsLock = new object();
            uint numConnections = 0;

            try
            {
                FileHandlerFactoryLocator.FileSystemResolver.Start();

                _ServerThread = Thread.CurrentThread;

                TcpListener tcpListener = new TcpListener(IPAddress.Any, Port);
                tcpListener.Start(20);

                try
                {
                    log.Info("Server is waiting for a new connection at http://" + FileHandlerFactoryLocator.HostnameAndPort + "/");

                    _Running = true;
                    while (Running)
                    {
                        AcceptingSockets = true;

                        if (!tcpListener.Pending())
                            Thread.Sleep(TimeSpan.FromMilliseconds(50));
                        else
                            try
                            {
                                Socket socket = tcpListener.AcceptSocket();
                                socket.NoDelay = true;

                                if (log.IsInfoEnabled)
                                    log.Info("Accepted connection form:" + socket.RemoteEndPoint);

                                WebConnection webConnection = new WebConnection(this, socket, new NetworkStream(socket, true));

                                // I'm not using ThreadPool because I suspect that idle HTTP connections are blocking incoming connections...
                                Thread webConnectionThread = new Thread(delegate()
                                    {
                                        if (_Running)
                                        {
                                            lock (numConnectionsLock)
                                                numConnections++;

                                            OnWebConnectionStarting(new EventArgs<IWebConnection>(webConnection));

                                            webConnection.HandleConnection();

                                            OnWebConnectionComplete(new EventArgs<IWebConnection>(webConnection));

                                            // The web connection object should be garbage collected at some point...
                                            //MonitorObjectForGarbageCollection(webConnection);

                                            lock (numConnectionsLock)
                                                numConnections--;
                                        }
                                    });

                                webConnectionThread.Name = "Connection from: " + socket.RemoteEndPoint.ToString();
                                webConnectionThread.Start();

                            }
                            catch (ThreadAbortException)
                            {
                                // This will be handled further up, just don't want it to be logged with the general-purpose logger
                                throw;
                            }
                            catch (Exception e1)
                            {
                                log.Error("An Exception Occurred while Listening for incoming sockets", e1);
                            }
                    }
                }
                finally
                {
                    tcpListener.Stop();
                }

                if (_Running)
                    log.Warn("Web server stopped while it was supposed to be running");
                else
                {
                    bool serverThreadComplete = false;

                    // In case there is a queued thread...
                    Thread.Sleep(50);

                    do
                        lock (numConnectionsLock)
                        {

                            if (0 == numConnections)
                                serverThreadComplete = true;
                            else
                            {
                                log.Info("Waiting for connections to complete...");
                                Thread.Sleep(1000);
                            }
                        } while (!serverThreadComplete);

                    log.Info("Web server stopped without error");
                }
            }
            catch (ThreadAbortException)
            {
                log.Info("Web server thread aborted");
            }
            catch (Exception e)
            {
                log.Error("Unhandled exception in web server", e);
            }
            finally
            {
                FileHandlerFactoryLocator.FileSystemResolver.Stop();
            }

            // Do final warning about uncollected objects
            GC.Collect(int.MaxValue);
            //this.CleanMonitoredObjectsAndLogWarnings();
        }


        /// <summary>
        /// Stops the server and releases all resources
        /// </summary>
        public override void Stop()
        {
            log.Info("Shutting down " + ServerType);

            _Running = false;

            try
            {
                OnWebServerTerminating(new EventArgs());
            }
            catch { }

            // Wait for the server to fully terminate, if waiting longer then 3 seconds, abort the thread
            DateTime startTerminationTime = DateTime.Now;
            ServerThread.Join();

            try
            {
                OnWebServerTerminated(new EventArgs());
            }
            catch { }
        }
    }
}
