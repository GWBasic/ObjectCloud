// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
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
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;


namespace ObjectCloud.WebServer.Implementation
{
    public class AsyncWebConnection : WebConnection, IAsyncWebConnection
    {
		private static ILog log = LogManager.GetLogger(typeof(AsyncWebConnection));

        /// <summary>
        /// Initializes the WebConnection
        /// </summary>
        /// <param name="s"></param>
        /// <param name="webServer"></param>
        public AsyncWebConnection(IWebServer webServer, Socket socket)
            : base(webServer, socket) { }
                
        protected override void SendToBrowser(Stream stream)
        {
            byte[] buffer = new byte[4096];
            byte[] oldBuffer = new byte[buffer.Length];
            int bytesRead = 0;
            int unsentStart = 0;
            GenericVoid send = null;
            AsyncCallback callback = null;

            callback = delegate(IAsyncResult result)
            {
                try
                {
                    if (!Socket.Connected)
                        return;

                    unsentStart = Socket.EndSend(result);

                    // Keep sending parts that aren't sent
                    if (unsentStart < bytesRead)
                    {
                        byte[] swap = oldBuffer;
                        oldBuffer = buffer;
                        buffer = swap;

                        Array.Copy(oldBuffer, unsentStart, buffer, 0, bytesRead - unsentStart);
                        unsentStart = bytesRead - unsentStart;
                    }
                    else
                        unsentStart = 0;

                    send();
                }
                catch (Exception e)
                {
                    log.Error("Error when sending data", e);
                }
            };

            send = delegate()
            {
                bytesRead = unsentStart + stream.Read(buffer, unsentStart, buffer.Length - unsentStart);

                if (bytesRead > 0)
                    Socket.BeginSend(buffer, 0, bytesRead, SocketFlags.None, callback, null);
                else
                {
                    stream.Close();
                    stream.Dispose();

                    OnResultsSent();
                }
            };

            try
            {
                send();
            }
            catch (Exception e)
            {
                log.Error("Error when sending data", e);
            }
        }

        /*private Stream ToSendStream;

        protected override void SendToBrowser(Stream stream)
        {
            ToSendStream = stream;

            lock (ToSend)
                ToSend.Enqueue(this);
        }

        static Queue<AsyncWebConnection> ToSend = new Queue<AsyncWebConnection>();

        static AsyncWebConnection()
        {
            for (int ctr = 0; ctr < 10; ctr++)
            {
                Thread sendThread = new Thread(delegate()
                {
                    while (true)
                    {
                        AsyncWebConnection me = null;

                        lock (ToSend)
                            if (ToSend.Count > 0)
                                me = ToSend.Dequeue();

                        if (null != me)
                            SendToBrowserOnSingleThread(me);

                        Thread.Sleep(5);
                    }
                });

                sendThread.Name = "Socket Writer " + ctr.ToString();
                sendThread.IsBackground = true;
                sendThread.Start();
            }
        }
        
        private static void SendToBrowserOnSingleThread(AsyncWebConnection me)
        {
            Stream stream = me.ToSendStream;

            byte[] buffer = new byte[4096];
            byte[] oldBuffer = new byte[buffer.Length];
            int bytesRead = 0;
            int unsentStart = 0;
            GenericVoid send = null;
            AsyncCallback callback = null;

            callback = delegate(IAsyncResult result)
            {
                try
                {
                    if (!me.Socket.Connected)
                        return;

                    unsentStart = me.Socket.EndSend(result);

                    // Keep sending parts that aren't sent
                    if (unsentStart < bytesRead)
                    {
                        byte[] swap = oldBuffer;
                        oldBuffer = buffer;
                        buffer = swap;

                        Array.Copy(oldBuffer, unsentStart, buffer, 0, bytesRead - unsentStart);
                        unsentStart = bytesRead - unsentStart;
                    }
                    else
                        unsentStart = 0;

                    send();
                }
                catch (Exception e)
                {
                    log.Error("Error when sending data", e);
                }
            };

            send = delegate()
            {
                bytesRead = unsentStart + stream.Read(buffer, unsentStart, buffer.Length - unsentStart);

                if (bytesRead > 0)
                    me.Socket.BeginSend(buffer, 0, bytesRead, SocketFlags.None, callback, null);
                else
                {
                    stream.Close();
                    stream.Dispose();
                    me.ToSendStream = null;

                    me.OnResultsSent();
                }
            };

            try
            {
                send();
            }
            catch (Exception e)
            {
                log.Error("Error when sending data", e);
            }
        }*/
    }
}
