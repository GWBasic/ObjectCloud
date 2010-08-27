// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.Common.Threading;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.WebServer.Implementation
{
    /// <summary>
    /// Used when reading from a socket in the Multi-threaded web-server
    /// </summary>
    internal class SocketReader
    {
        private static ILog log = LogManager.GetLogger<SocketReader>();

        /// <summary>
        /// Initializes the WebConnection
        /// </summary>
        /// <param name="s"></param>
        /// <param name="webServer"></param>
        public SocketReader(WebServer webServer, Socket socket)
        {
            WebServer = webServer;
            Socket = socket;
            RemoteEndPoint = socket.RemoteEndPoint;

            BufferBytesRead = 0;

            log.Trace("New socket");

            WebServer.WebServerTerminating += new EventHandler<EventArgs>(WebServer_WebServerTerminating);
        }

        void WebServer_WebServerTerminating(object sender, EventArgs e)
        {
            Close();
        }

        /// <summary>
        /// The web server
        /// </summary>
        private WebServer WebServer;

        /// <summary>
        /// The state that the SocketReader is currently in
        /// </summary>
        internal WebConnectionIOState WebConnectionIOState = WebConnectionIOState.ReadingHeader;

        /// <summary>
        /// The socket that this connection is working with
        /// </summary>
        internal Socket Socket;

        /// <summary>
        /// The socket's remote endpoint
        /// </summary>
        internal EndPoint RemoteEndPoint;

        /// <summary>
        /// The fixed-size buffer for reading headers
        /// </summary>
        internal byte[] Buffer = null;

        /// <summary>
        /// The number of bytes read in the header
        /// </summary>
        internal int BufferBytesRead;

        /// <summary>
        /// The time that reading started
        /// </summary>
        internal DateTime ReadStartTime;

        /// <summary>
        /// The non-blocking web connection
        /// </summary>
        internal WebConnection WebConnection;

        /// <summary>
        /// The current content
        /// </summary>
        internal WebConnectionContent Content = null;

        /// <summary>
        /// Marker for when an HTTP header ends
        /// </summary>
        static byte[] HeaderEndMarker = Encoding.UTF8.GetBytes("\r\n\r\n");

        /// <summary>
        /// Starts reading the HTTP header from the socket
        /// </summary>
        public void Start()
        {
            try
            {
                if (null == Buffer)
                    Buffer = WebServer.RecieveBufferRecycler.Get();

                // Timeout if the client takes a really long time to send bytes
                Socket.ReceiveTimeout = Convert.ToInt32(TimeSpan.FromSeconds(15).TotalMilliseconds);
                ReadStartTime = DateTime.UtcNow;

                WebConnection = new WebConnection(WebServer, Socket.RemoteEndPoint, SendToBrowser);

                WebConnectionIOState = WebConnectionIOState.Idle;

                ReadHeader();
            }
            catch (ThreadAbortException)
            {
                Close();
            }
            // Exceptions that occur when a socket is closed are just swallowed; this keeps the logs clean
            catch (ObjectDisposedException)
            {
                WebConnectionIOState = WebConnectionIOState.Disconnected;
                Close();
            }
            catch (SocketException)// se)
            {
                //log.InfoFormat("Error when performing IO for a connection from {0}", se, RemoteEndPoint);

                WebConnectionIOState = WebConnectionIOState.Disconnected;
                Close();
            }
            catch (Exception e)
            {
                log.ErrorFormat("Fatal error when performing IO for a connection from {0}", e, RemoteEndPoint);

                WebConnectionIOState = WebConnectionIOState.Disconnected;
                Close();
            }
        }

        /// <summary>
        /// Instructs the object to read a bit of the header
        /// </summary>
        /// <returns></returns>
        public void ReadHeader()
        {
#if DEBUG
            int headerHandled = 0;
#endif

            if (BufferBytesRead > HeaderEndMarker.Length)
            {
                int headerEnd = Array<byte>.IndexOf(Buffer, HeaderEndMarker);

                if (headerEnd != -1)
                {
                    WebConnectionIOState = WebConnectionIOState.ParsingHeader;

#if DEBUG
                    headerHandled++;
#endif

                    HandleHeader(headerEnd);
                    return;
                }
            }

            if (BufferBytesRead >= WebServer.HeaderSize)
            {
                // The header is too long
                Close(WebResults.From(Status._414_Request_URI_Too_Long, "Max header length: " + WebServer.HeaderSize.ToString()));
                return;
            }

            else if (ReadStartTime + WebServer.HeaderTimeout < DateTime.UtcNow)
            {
                // The sending header timed out
                Close(WebResults.From(Status._408_Request_Timeout, "Headers time out after: " + WebServer.HeaderTimeout.ToString()));
                return;
            }

            // If the header is incomplete, read more of it
            Socket.BeginReceive(Buffer, BufferBytesRead, Buffer.Length - BufferBytesRead, SocketFlags.None, ReadHeaderCallback, null);
        }

        private void ReadHeaderCallback(IAsyncResult ar)
        {
            WebConnectionIOState = WebConnectionIOState.ReadingHeader;

            try
            {
                SocketError socketError;
                int bytesRead = Socket.EndReceive(ar, out socketError);

                if ((SocketError.Success == socketError) && (bytesRead > 0))
                {
                    BufferBytesRead += bytesRead;
                    ReadHeader();
                }
                else
                {
                    if (log.IsTraceEnabled)
                        log.Trace("Socket closed: " + socketError.ToString());

                    WebConnectionIOState = WebConnectionIOState.Disconnected;
                    Close();
                }
            }
            catch (ObjectDisposedException)
            {
                WebConnectionIOState = WebConnectionIOState.Disconnected;
                Close();
            }
            catch (SocketException)
            {
                WebConnectionIOState = WebConnectionIOState.Disconnected;
                Close();
            }
            catch (Exception e)
            {
                log.ErrorFormat("Fatal error when performing IO for a connection from {0}", e, RemoteEndPoint);

                WebConnectionIOState = WebConnectionIOState.Disconnected;
                Close();
            }
        }

        /// <summary>
        /// Parses the incoming header
        /// </summary>
        private void HandleHeader(int headerEnd)
        {
            string header = Encoding.UTF8.GetString(Buffer, 0, headerEnd);

            // If more data was read into the buffer then the length of the header, then move it to the front of the buffer
            if (BufferBytesRead > headerEnd + HeaderEndMarker.Length)
            {
                byte[] oldBuffer = Buffer;
                Buffer = WebServer.RecieveBufferRecycler.Get();

                Array.Copy(oldBuffer, headerEnd + HeaderEndMarker.Length, Buffer, 0, BufferBytesRead - headerEnd - HeaderEndMarker.Length);
                //System.Buffer.BlockCopy(Buffer, headerEnd + HeaderEndMarker.Length, Buffer, 0, BufferBytesRead - headerEnd - HeaderEndMarker.Length);

                WebServer.RecieveBufferRecycler.Recycle(oldBuffer);
            }

            BufferBytesRead = BufferBytesRead - (headerEnd + HeaderEndMarker.Length);

            try
            {
                WebConnection.ReadHeader(header);
            }
            catch (WebResultsOverrideException wroe)
            {
                log.Error("Exception occured while reading the header", wroe);
                Close(wroe.WebResults);

                return;
            }
            catch (Exception e)
            {
                log.Error("Exception occured while reading the header", e);
                Close(WebResults.From(Status._500_Internal_Server_Error, "An unknown error occured"));

                return;
            }

            switch (WebConnection.Method)
            {
                case WebMethod.GET:

                    if (WebConnection.Headers.ContainsKey("CONTENT-LENGTH"))
                    {
                        Close(WebResults.From(Status._406_Not_Acceptable, "Content-Length can not be specified in the headers when performing a GET"));
                        return;
                    }

                    PerformRequest();
                    break;

                case WebMethod.POST:
                    SetUpReadingContent();
                    break;

                default:
                    if (WebConnection.Headers.ContainsKey("CONTENT-LENGTH"))
                        SetUpReadingContent();
                    else
                        PerformRequest();

                    break;
            }
        }

        /// <summary>
        /// The content-length
        /// </summary>
        private long ContentLength;

        /// <summary>
        /// Sets up the socket reader to read the Content of the HTTP request
        /// </summary>
        private void SetUpReadingContent()
        {
			string contentLenth;
			if (!WebConnection.Headers.TryGetValue("CONTENT-LENGTH", out contentLenth))
			{
                Close(WebResults.From(Status._411_Length_Required, "Content-Lenth required" + WebServer.MaxContentSize.ToString()));
                return;
			}
			
			if (!long.TryParse(contentLenth, out ContentLength))
			{
                Close(WebResults.From(Status._400_Bad_Request, "Invalid Content-Length: " + contentLenth.ToString()));
                return;
			}

            if (ContentLength <= WebServer.MaxInMemoryContentSize)
                Content = new WebConnectionContent.InMemory(ContentLength);
            else if (ContentLength <= WebServer.MaxContentSize)
                Content = new WebConnectionContent.OnDisk();
            else
            {
                Close(WebResults.From(Status._413_Request_Entity_Too_Large, "Too much data, max size: " + WebServer.MaxContentSize.ToString()));
                return;
            }


            if (BufferBytesRead >= ContentLength)
            {
                byte[] toCopy = new byte[ContentLength];
                Array.Copy(Buffer, 0, toCopy, 0, toCopy.Length);

                Content.TakeBytes(toCopy);

                if (BufferBytesRead == ContentLength)
                    BufferBytesRead = 0;
                else
                {
                    byte[] oldBuffer = Buffer;
                    Buffer = WebServer.RecieveBufferRecycler.Get();

                    Array.Copy(oldBuffer, ContentLength, Buffer, 0, BufferBytesRead - ContentLength);
                    //System.Buffer.BlockCopy(Buffer, Convert.ToInt32(ContentLength), Buffer, 0, Convert.ToInt32(BufferBytesRead - ContentLength));

                    WebServer.RecieveBufferRecycler.Recycle(oldBuffer);

                    BufferBytesRead = Convert.ToInt32(Convert.ToInt64(BufferBytesRead) - ContentLength);
                }

                WebConnectionIOState = WebConnectionIOState.PerformingRequest;

                PerformRequest();
                return;
            }
            else if (0 == BufferBytesRead)
            {
                ReadStartTime = DateTime.UtcNow;

                WebConnectionIOState = WebConnectionIOState.ReadingContent;
            }
            else // Buffer has less bytes then what's needed
            {
                byte[] toCopy = new byte[BufferBytesRead];

                Array.Copy(Buffer, 0, toCopy, 0, BufferBytesRead);
                Content.TakeBytes(toCopy);

                BufferBytesRead = 0;

                ReadStartTime = DateTime.UtcNow;

                WebConnectionIOState = WebConnectionIOState.ReadingContent;
            }

            ReadContent();
        }

        /// <summary>
        /// Call for when the web connection is reading content
        /// </summary>
        /// <returns></returns>
        private void ReadContent()
        {
            Socket.BeginReceive(Buffer, BufferBytesRead, Buffer.Length - BufferBytesRead, SocketFlags.None, ReadContentCallback, null);
        }

        private void ReadContentCallback(IAsyncResult ar)
        {
            try
            {
                SocketError socketError;
                int bytesRead = Socket.EndReceive(ar, out socketError);

                if ((SocketError.Success == socketError) && (bytesRead > 0))
                {
                    BufferBytesRead += bytesRead;

                    if (Content.BytesRead + BufferBytesRead <= ContentLength)
                    {
                        byte[] localBuffer = new byte[BufferBytesRead];
                        Array.Copy(Buffer, localBuffer, BufferBytesRead);

                        BufferBytesRead = 0;

                        Content.TakeBytes(localBuffer);
                    }
                    else
                    {
                        // Additional data needs to stay on the buffer
                        byte[] localBuffer = new byte[ContentLength - Content.BytesRead];
                        Array.Copy(Buffer, localBuffer, localBuffer.Length);

                        byte[] oldBuffer = Buffer;
                        Buffer = WebServer.RecieveBufferRecycler.Get();
                        Array.Copy(oldBuffer, localBuffer.Length, Buffer, 0, BufferBytesRead - localBuffer.Length);
                        //System.Buffer.BlockCopy(Buffer, localBuffer.Length, Buffer, 0, BufferBytesRead - localBuffer.Length);

                        WebServer.RecieveBufferRecycler.Recycle(oldBuffer);

                        BufferBytesRead = BufferBytesRead - localBuffer.Length;

                        Content.TakeBytes(localBuffer);
                    }

                    // If the header end is found, then stop reading the socket and start handling the connection
                    if (Content.BytesRead >= ContentLength)
                    {
                        WebConnectionIOState = WebConnectionIOState.PerformingRequest;

                        PerformRequest();
                        return;
                    }
                    else if (ReadStartTime + WebServer.ContentTimeout < DateTime.UtcNow)
                    {
                        // The sending header timed out

                        Content.Dispose();

                        WebConnectionIOState = WebConnectionIOState.Disconnected;

                        WebConnection.SendResults(WebResults.From(Status._408_Request_Timeout, "Content times out after: " + WebServer.HeaderTimeout.ToString()));
                        Socket.Close();

                        return;
                    }

                    ReadContent();
                }
                else
                {
                    if (log.IsTraceEnabled)
                        log.Trace("Socket closed: " + socketError.ToString());

                    WebConnectionIOState = WebConnectionIOState.Disconnected;
                    Close();
                }
            }
            catch (ObjectDisposedException)
            {
                WebConnectionIOState = WebConnectionIOState.Disconnected;
                Close();
            }
            catch (SocketException)
            {
                WebConnectionIOState = WebConnectionIOState.Disconnected;
                Close();
            }
            catch (Exception e)
            {
                log.ErrorFormat("Fatal error when performing IO for a connection from {0}", e, RemoteEndPoint);

                WebConnectionIOState = WebConnectionIOState.Disconnected;
                Close();
            }
        }

        /// <summary>
        /// Called when the header is completely loaded
        /// </summary>
        /// <param name="socketReader"></param>
        protected void PerformRequest()
        {
            if (!WebServer.KeepAlive || !Socket.Connected || !WebServer.Running)
                KeepAlive = false;
            else if (!WebConnection.Headers.ContainsKey("CONNECTION"))
                KeepAlive = false;
            else if ("keep-alive" != WebConnection.Headers["CONNECTION"].ToLower())
                KeepAlive = false;
            else
                KeepAlive = true;

            // (I think) this ensures that the web connection is garbage collected
            // I forget why I did this, but it seems that there shouldn't be a reference to the webconnection while the request is being handled
            WebConnection webConnection = WebConnection;
            WebConnection = null;

            Busy.BlockWhileBusy("Socket Reader after HTTP request read");

            WebServer.RequestDelegateQueue.QueueUserWorkItem(delegate(object state)
            {
                webConnection.HandleConnection((IWebConnectionContent)state);
            }, Content);
        }

        /// <summary>
        /// Caches the results to send
        /// </summary>
        /// <param name="stream"></param>
        private void SendToBrowser(Stream stream)
        {
            if (null == SendBuffer)
                SendBuffer = WebServer.SendBufferRecycler.Get();

            if (Socket.Connected)
            {
                SendEnd = DateTime.UtcNow.AddMinutes(5);
                ResultStream = stream;

                ReadFromResultStream();
            }
            else
            {
                log.Debug("Connection Dropped....");
                stream.Dispose();
                return;
            }
        }

        /// <summary>
        /// This is set to true if a request will support keepalive
        /// </summary>
        private bool KeepAlive;

        /// <summary>
        /// The stream that's used to get the result from the web request
        /// </summary>
        Stream ResultStream;

        /// <summary>
        /// The buffer used for sending to the client
        /// </summary>
        byte[] SendBuffer = null;

        /// <summary>
        /// The number of bytes in the buffer that are being sent
        /// </summary>
        int BytesToSend;

        /// <summary>
        /// The bytes that have been sent
        /// </summary>
        int BytesSent;

        /// <summary>
        /// When sending should end
        /// </summary>
        DateTime SendEnd;

        /// <summary>
        /// Sends the stream to the browser.  This should be called on the same thread that owns this web connection
        /// </summary>
        /// <param name="ResultStream"></param>
        private void ReadFromResultStream()
        {
            try
            {
                BytesToSend = ResultStream.Read(SendBuffer, 0, SendBuffer.Length);
                BytesSent = 0;

                if (BytesToSend > 0)
                    SendToBrowser();

                else
                {
                    WebServer.SendBufferRecycler.Recycle(SendBuffer);
                    SendBuffer = null;

                    ResultStream.Close();
                    ResultStream = null;

                    if (null != Content)
                    {
                        Content.Dispose();
                        Content = null;
                    }

                    if (KeepAlive)
                        Start();
                    else
                        Close();
                }
            }
            catch (Exception e)
            {
                log.Error("Error Occurred", e);
                Close();
            }
        }

        /// <summary>
        /// Starts sending the current buffer contents to the browser
        /// </summary>
        private void SendToBrowser()
        {
            if (DateTime.UtcNow > SendEnd)
            {
                log.Warn("Sending timed out");
                Close();
            }

            Socket.BeginSend(SendBuffer, BytesSent, BytesToSend - BytesSent, SocketFlags.None, SendToBrowserCallback, null);
        }

        private void SendToBrowserCallback(IAsyncResult ar)
        {
            try
            {
                SocketError socketError;
                int bytesSent = Socket.EndSend(ar, out socketError);

                if ((SocketError.Success == socketError) && (bytesSent > 0))
                {
                    BytesSent += bytesSent;

                    if (BytesSent >= BytesToSend)
                        ReadFromResultStream();
                    else
                        SendToBrowser();
                }
                else
                {
                    if (log.IsTraceEnabled)
                        log.Trace("Socket closed: " + socketError.ToString());

                    WebConnectionIOState = WebConnectionIOState.Disconnected;
                    Close();
                }
            }
            catch (ObjectDisposedException)
            {
                WebConnectionIOState = WebConnectionIOState.Disconnected;
                Close();
            }
            catch (SocketException)
            {
                WebConnectionIOState = WebConnectionIOState.Disconnected;
                Close();
            }
            catch (Exception e)
            {
                log.ErrorFormat("Fatal error when performing IO for a connection from {0}", e, RemoteEndPoint);

                WebConnectionIOState = WebConnectionIOState.Disconnected;
                Close();
            }
        }

        /// <summary>
        /// Closes the socket reader after transmitting an error message
        /// </summary>
        internal void Close(IWebResults webResults)
        {
            if (null != WebConnection)
                WebConnection.SendResults(webResults);

            Close();
        }

        /// <summary>
        /// Closes the socket reader, for all intents and purposes
        /// </summary>
        internal void Close()
        {
            if (null != Buffer)
            {
                WebServer.RecieveBufferRecycler.Recycle(Buffer);
                Buffer = null;
            }

            try
            {
                Socket.Shutdown(SocketShutdown.Both);
                Socket.Close();
            }
            catch { }

            WebConnectionIOState = WebConnectionIOState.Disconnected;

            WebServer.WebServerTerminating -= new EventHandler<EventArgs>(WebServer_WebServerTerminating);

            if (null != Closed)
                Closed(this, new EventArgs());
        }

        /// <summary>
        /// Occurs whenever the connection is closed
        /// </summary>
        public event EventHandler<SocketReader, EventArgs> Closed;
    }
}
