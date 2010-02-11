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
using System.Text;
using System.Threading;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.WebServer.Implementation
{
    public class NonBlockingWebServer : WebServerBase
    {
		private ILog log = LogManager.GetLogger(typeof(NonBlockingWebServer));

        public NonBlockingWebServer() : this(80) { }

        public NonBlockingWebServer(int port) : base(port) { }

		/// <summary>
		/// Actually runs the server 
		/// </summary>
        public override void RunServer()
        {
            log.Info("Starting: " + this.ServerType);

            try
            {
                FileHandlerFactoryLocator.FileSystemResolver.Start();

                _ServerThread = Thread.CurrentThread;

                TcpListener tcpListener = new TcpListener(IPAddress.Any, Port);
                tcpListener.Start(20);

                SocketMonitorThread = new Thread(MonitorSockets);
                SocketMonitorThread.Name = "Socket Reader";
                SocketMonitorThread.Start();

                try
                {
                    log.Info("Server is waiting for a new connection at http://" + FileHandlerFactoryLocator.HostnameAndPort + "/");

                    _Running = true;
                    AcceptingSockets = true;

                    while (Running)
                        try
                        {
                            if (!tcpListener.Pending())
                                Thread.Sleep(TimeSpan.FromMilliseconds(50));
                            else
                            {
                                Socket socket = tcpListener.AcceptSocket();
                                socket.NoDelay = true;

                                if (log.IsInfoEnabled)
                                    log.Info("Accepted connection form: " + socket.RemoteEndPoint);

                                NonBlockingSocketReader socketReader = new NonBlockingSocketReader(this, socket);
                                socketReader.Start();

                                lock (SocketReaders)
                                    SocketReaders.Add(socketReader);

                                ThreadPool.QueueUserWorkItem(
                                    delegate(object socketReaderObj)
                                    {
                                        OnWebConnectionStarting(new EventArgs<IWebConnection>(((NonBlockingSocketReader)socketReaderObj).WebConnection));
                                    },
                                    socketReader);
                            }
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
                finally
                {
                    tcpListener.Stop();
                }

                if (_Running)
                    log.Warn("Web server stopped while it was supposed to be running");
                else
                    log.Info("Web server stopped without error");

                foreach (NonBlockingSocketReader socketReader in SocketReaders)
                    try
                    {
                        socketReader.Socket.Close();
                    }
                    catch (Exception e)
                    {
                        log.Warn("Error closing socket", e);
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

#if DEBUG
            // This is for unit tests...
            GC.Collect(int.MaxValue);
#endif
        }

        /// <summary>
        /// List of web connections where their headers haven't fully been read
        /// </summary>
        Set<NonBlockingSocketReader> SocketReaders = new Set<NonBlockingSocketReader>();

        /// <summary>
        /// Thread for reading headers
        /// </summary>
        Thread SocketMonitorThread;

        /// <summary>
        /// Method that reads headers for WebConnections on a seperate thread
        /// </summary>
        private void MonitorSockets()
        {
            try
            {
                while (_Running)
                {
                    // Create a thread-safe collection to iterate over
                    LinkedList<NonBlockingSocketReader> dupeSocketReaders;
                    lock (SocketReaders)
                        dupeSocketReaders = new LinkedList<NonBlockingSocketReader>(SocketReaders);

                    // Read header data from each socket
                    foreach (NonBlockingSocketReader socketReader in dupeSocketReaders)
                    {
                        switch (socketReader.WebConnectionIOState)
                        {
                            case WebConnectionIOState.ReadingHeader:

                                if (socketReader.ReadStartTime + HeaderTimeout < DateTime.UtcNow)
                                {
                                    log.InfoFormat("Reading header timed out for {0}", socketReader);
                                    socketReader.WebConnection.SendResults(WebResults.FromString(Status._408_Request_Timeout, "Headers time out after: " + HeaderTimeout.ToString()));
                                    socketReader.Socket.Close();
                                }
                                break;

                            case WebConnectionIOState.ReadingContent:

                                if (socketReader.ReadStartTime + ContentTimeout < DateTime.UtcNow)
                                {
                                    log.InfoFormat("Reading content timed out for {0}", socketReader);
                                    socketReader.WebConnection.SendResults(WebResults.FromString(Status._408_Request_Timeout, "Content time out after: " + ContentTimeout.ToString()));
                                    socketReader.Socket.Close();
                                }

                                break;
                        }

                        if (!socketReader.Socket.Connected)
                            socketReader.WebConnectionIOState = WebConnectionIOState.Disconnected;

                        // Remove dead connections
                        if (WebConnectionIOState.Disconnected == socketReader.WebConnectionIOState)
                            lock (SocketReaders)
                            {
                                SocketReaders.Remove(socketReader);

                                ThreadPool.QueueUserWorkItem(
                                    delegate(object socketReaderObj)
                                    {
                                        OnWebConnectionComplete(new EventArgs<IWebConnection>(((NonBlockingSocketReader)socketReaderObj).WebConnection));
                                    },
                                    socketReader);
                            }
                    }

                    Thread.Sleep(TimeSpan.FromSeconds(0.5));
                }

                // TODO:  Have a cleaner shutdown that sends an error message
                ServerThread.Join();
                foreach (NonBlockingSocketReader socketReader in SocketReaders)
                    socketReader.Socket.Close();

                SocketReaders.Clear();

                log.Info("Socket Reading thread shut down without error");
            }
            catch (Exception e)
            {
                log.Fatal("Socket Reading thread aborted in error, attempting recursion", e);
                MonitorSockets();
            }
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
            ServerThread.Join();
            SocketMonitorThread.Join();

            _ServerThread = null;
            SocketMonitorThread = null;

            try
            {
                OnWebServerTerminated(new EventArgs());
            }
            catch { }
        }
    }
}
