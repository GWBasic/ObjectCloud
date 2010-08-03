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
    public class MultithreadedWebServer : WebServerBase
    {
		private static ILog log = LogManager.GetLogger(typeof(MultithreadedWebServer));

        public MultithreadedWebServer() : this(80) { }

        public MultithreadedWebServer(int port) : base(port) { }

		/// <summary>
		/// Actually runs the server 
		/// </summary>
        public override void RunServer()
        {
            log.Info("Starting: " + this.ServerType);

            try
            {
                FileHandlerFactoryLocator.FileSystemResolver.Start();

                _Running = true;

                _ServerThread = Thread.CurrentThread;

                TcpListener tcpListener = new TcpListener(IPAddress.Any, Port);
                tcpListener.Start(20);

                log.Info("Server is waiting for a new connection at http://" + FileHandlerFactoryLocator.HostnameAndPort + "/");

                AcceptingSockets = true;

                try
                {
                    while (Running)
                        try
                        {

                            if (!tcpListener.Pending())
                                // TODO...  Not sure how long to wait
                                Thread.Sleep(TimeSpan.FromMilliseconds(50));
                            else
                            {
                                Busy.BlockWhileBusy();
                                Socket socket = tcpListener.AcceptSocket();

                                if (log.IsInfoEnabled)
                                    log.Info("Accepted connection form: " + socket.RemoteEndPoint);

                                BlockingSocketReader socketReader = new BlockingSocketReader(this, socket);//, SocketReaderDelegateQueue);

                                socketReader.Start();
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
            }
            catch (ThreadAbortException t)
            {
                log.Info("Web server thread aborted");
                TerminatingException = t;
            }
            catch (Exception e)
            {
                log.Error("Unhandled exception in web server", e);
                TerminatingException = e;
            }
            finally
            {
                FileHandlerFactoryLocator.FileSystemResolver.Stop();
            }

#if DEBUG
            // This is for unit tests...
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
#endif
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

            ServerThread.Join();
            _ServerThread = null;

            try
            {
                OnWebServerTerminated(new EventArgs());
            }
            catch { }
        }
    }
}
