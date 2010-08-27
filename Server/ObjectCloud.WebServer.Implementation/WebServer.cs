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
    public class WebServer : WebServerBase
    {
        private static ILog log = LogManager.GetLogger(typeof(WebServer));

        public WebServer() : this(80) { }

        public WebServer(int port) : base(port) { }

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

            RecieveBufferRecycler = new BufferRecycler(HeaderSize);
            SendBufferRecycler = new BufferRecycler(SendBufferSize);

            FileHandlerFactoryLocator.FileSystemResolver.Start();

            _Running = true;

            _ServerThread = Thread.CurrentThread;

            TcpListener = new TcpListener(IPAddress.Any, Port);
            TcpListener.Start(20);

            log.Info("Server is waiting for a new connection at http://" + FileHandlerFactoryLocator.HostnameAndPort + "/");

            AcceptingSockets = true;
			
            TcpListener.BeginAcceptSocket(AcceptSocket, null);

			System.Runtime.GCSettings.LatencyMode = System.Runtime.GCLatencyMode.Batch;
        }

        /// <summary>
        /// Allows re-using buffers for recieving data from clients
        /// </summary>
        internal Recycler<byte[]> RecieveBufferRecycler;

        /// <summary>
        /// Allows re-using buffers for sending data to clients
        /// </summary>
        internal Recycler<byte[]> SendBufferRecycler;

        private void AcceptSocket(IAsyncResult ar)
        {
            try
            {
                Socket socket = TcpListener.EndAcceptSocket(ar);

                Busy.BlockWhileBusy("Socket accepter on incoming socket");

                log.Info("Accepted connection form: " + socket.RemoteEndPoint);

                SocketReader socketReader = new SocketReader(this, socket);

                socketReader.Start();

                if (Running)
                    TcpListener.BeginAcceptSocket(AcceptSocket, null);
            }
            // This is in case the listener is disposed
            catch (ObjectDisposedException) { }
            catch (Exception e)
            {
                log.Error("Exception accepting a socket", e);
            }
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

            TcpListener.Stop();

            ServerThread.Join();
            _ServerThread = null;

            try
            {
                OnWebServerTerminated(new EventArgs());
            }
            catch { }

            FileHandlerFactoryLocator.FileSystemResolver.Stop();
        }
		
		/// <summary>
		/// The maximum number of requests before a garbage collection is forced
		/// </summary>
		public int MaxRequestsBeforeGarbageCollection
		{
			get { return _MaxRequestsBeforeGarbageCollection; }
			set { _MaxRequestsBeforeGarbageCollection = value; }
		}
		private int _MaxRequestsBeforeGarbageCollection = 100000;

		/// <summary>
		/// The minimum number of requests before a garbage collection is forced 
		/// </summary>
		public int MinRequestsBeforeGarbageCollection
		{
			get { return _MinRequestsBeforeGarbageCollection; }
			set { _MinRequestsBeforeGarbageCollection = value; }
		}
		private int _MinRequestsBeforeGarbageCollection = 3000;

        /// <summary>
        /// Assists in conserving memory while reading HTTP headers
        /// </summary>
        private class BufferRecycler : Recycler<byte[]>
        {
            public BufferRecycler(int headerSize)
            {
                HeaderSize = headerSize;
            }

            private int HeaderSize;

            public override byte[] Construct()
            {
                byte[] toReturn = new byte[HeaderSize];
                Array.Clear(toReturn, 0, HeaderSize);

                return toReturn;
            }

            protected override void RecycleInt(byte[] toRecycle)
            {
                Array.Clear(toRecycle, 0, HeaderSize);
            }
        }
	}
}
