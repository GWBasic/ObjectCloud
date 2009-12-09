using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.WebServer.Implementation
{
    /// <summary>
    /// Used when reading from a socket for a NonBlockingWebConnection
    /// </summary>
    internal class NonBlockingSocketReader
    {
        private static ILog log = LogManager.GetLogger<NonBlockingSocketReader>();

        private static InterrupThread IOThread = new InterrupThread("IO Thread");

        /// <summary>
        /// Initializes the WebConnection
        /// </summary>
        /// <param name="s"></param>
        /// <param name="webServer"></param>
        public NonBlockingSocketReader(NonBlockingWebServer webServer, Socket socket)
        {
            WebServer = webServer;
            Socket = socket;
            ReadStartTime = DateTime.UtcNow;

            // Create the header buffer
            Buffer = new byte[webServer.HeaderSize];

            BufferBytesRead = 0;

            WebConnection = new WebConnection(webServer, socket.RemoteEndPoint, SendToBrowser);

            //StartReadingSocket();
        }

        /// <summary>
        /// The web server
        /// </summary>
        private NonBlockingWebServer WebServer;

        /// <summary>
        /// The state that the SocketReader is currently in
        /// </summary>
        internal volatile WebConnectionIOState WebConnectionIOState = WebConnectionIOState.ReadingHeader;

        /// <summary>
        /// The socket that this connection is working with
        /// </summary>
        internal Socket Socket;

        /// <summary>
        /// The fixed-size buffer for reading headers
        /// </summary>
        internal volatile byte[] Buffer = null;

#if DEBUG
        public string DEBUG_ONLY_Buffer
        {
            get { return Encoding.UTF8.GetString(Buffer); }
        }
#endif

        /// <summary>
        /// The number of bytes read in the header
        /// </summary>
        internal volatile int BufferBytesRead;

        /// <summary>
        /// The time that reading started
        /// </summary>
        internal DateTime ReadStartTime;

        /// <summary>
        /// The web connection
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

        /*// <summary>
        /// Indicates if the socket is being read
        /// </summary>
        bool ReadingSocket = false;*/

        /*// <summary>
        /// Starts reading the socket
        /// </summary>
        private void StartReadingSocket()
        {
            if (!ReadingSocket)
            {
#if DEBUG
                //LastBuffer = Encoding.UTF8.GetString(Buffer);
                //LastBufferBytesRead = BufferBytesRead;

                /*if (LastBuffer.StartsWith("POST") && LastBuffer.Contains("\r\n\r\n") && BufferBytesRead > 0)
                    System.Diagnostics.Debugger.Break();*/

                /*/ This helps with debugging so there isn't a lot of cruft when viewing as a string
                Array.Clear(Buffer, BufferBytesRead, Buffer.Length - BufferBytesRead);
#endif

                ReadingSocket = true;

                IOThread.QueueItem(delegate()
                {
                    Socket.BeginReceive(Buffer, BufferBytesRead, Buffer.Length - BufferBytesRead, SocketFlags.None, ReadCallback, null);
                });
            }
        }*

/*#if DEBUG
        string LastBuffer;
        int LastBufferBytesRead;
        string DoIOName;
        int Read;
        //static Set<int> ThreadIds = new Set<int>();
#endif*/

        /*// <summary>
        /// Callback for when there's incoming data on the socket
        /// </summary>
        /// <param name="ar"></param>
        private void ReadCallback(IAsyncResult ar)
        {
#if DEBUG
            //ThreadIds.Add(Thread.CurrentThread.ManagedThreadId);
#endif

            if (!Socket.Connected)
            {
                WebConnectionIOState = WebConnectionIOState.Disconnected;
                return;
            }

            try
            {
                ReadingSocket = false;

                int read = Socket.EndReceive(ar);

                if (read > 0)
                {
                    //log.Trace("Contents recieved:\n" + Encoding.UTF8.GetString(Buffer, BufferBytesRead, read));

                    BufferBytesRead = BufferBytesRead + read;

                    /*if (Encoding.UTF8.GetString(Buffer).StartsWith("POST /TestLargeTextFile"))
                        if (System.Diagnostics.Debugger.IsAttached)
                            System.Diagnostics.Debugger.Break();*//*

#if DEBUG
                    //DoIOName = DoIO.Method.Name;
                    //Read = read;
#endif

                    do
                        DoIO();
                    while (BufferBytesRead == Buffer.Length);
                }

                StartReadingSocket();
            }
            catch (Exception e)
            {
                log.Error("Error when reading from a socket", e);

                // Attempt to gracefully close the socket
                try
                {
                    Close(WebResults.FromString(Status._500_Internal_Server_Error, "An unhandled error occured"));
                }
                catch { }
            }
        }*/

        public void Start()
        {
            BufferBytesRead = 0;
            ReadHeader();
        }

        /// <summary>
        /// Instructs the object to read a bit of the header
        /// </summary>
        /// <returns></returns>
        public void ReadHeader()
        {
            GenericVoid startRead = null;

            AsyncCallback readCallback = delegate(IAsyncResult result)
            {
                int read;

                try
                {
                    if (!Socket.Connected)
                        return;

                    read = Socket.EndReceive(result);
                }
                catch (Exception e)
                {
                    log.Error("Exception while reading a header", e);
                    Close();
                    return;
                }

                BufferBytesRead += read;

                int headerEnd = Array<byte>.IndexOf(
                    Buffer,
                    HeaderEndMarker,
                    0,
                    BufferBytesRead);

                // If the header end is found, then stop reading the socket and start handling the connection
                if (-1 != headerEnd)
                {
                    HandleHeader(headerEnd);
                    return;
                }

                else if (BufferBytesRead == WebServer.HeaderSize)
                    // The header is too long
                    Close(WebResults.FromString(Status._414_Request_URI_Too_Long, "Max header length: " + WebServer.HeaderSize.ToString()));

                else if (ReadStartTime + WebServer.HeaderTimeout < DateTime.UtcNow)
                    // The sending header timed out
                    Close(WebResults.FromString(Status._408_Request_Timeout, "Headers time out after: " + WebServer.HeaderTimeout.ToString()));

                else
                    startRead();
            };

            startRead = delegate()
            {
                try
                {
                    Socket.BeginReceive(Buffer, 0, Buffer.Length, SocketFlags.None, readCallback, null);
                }
                catch (Exception e)
                {
                    log.Error("Error reading from socket", e);
                    Close();
                }
            };

            startRead();
        }

        /// <summary>
        /// Parses the incoming header
        /// </summary>
        private void HandleHeader(int headerEnd)
        {
            WebConnectionIOState = WebConnectionIOState.ParsingHeader;

            byte[] headerBytes = new byte[headerEnd];
            Array.Copy(Buffer, headerBytes, headerEnd);
            string header = Encoding.UTF8.GetString(headerBytes);

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
                Close(WebResults.FromString(Status._500_Internal_Server_Error, "An unknown error occured"));

                return;
            }

            switch (WebConnection.Method)
            {
                case WebMethod.GET:
                    
                    if (WebConnection.Headers.ContainsKey("CONTENT-LENGTH"))
                    {
                        Close(WebResults.FromString(Status._406_Not_Acceptable, "Content-Length can not be specified in the headers when performing a GET"));
                        return;
                    }

                    PerformRequest();
                    break;

                case WebMethod.POST:
                    SetUpReadingContent(headerEnd);
                    break;

                default:
                    if (WebConnection.Headers.ContainsKey("CONTENT-LENGTH"))
                        SetUpReadingContent(headerEnd);
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
        private void SetUpReadingContent(int headerEnd)
        {
            // If more data was read into the buffer then the length of the header, then move it to the front of the buffer
            if (BufferBytesRead > headerEnd + HeaderEndMarker.Length)
            {
                byte[] oldBuffer = Buffer;
                Buffer = new byte[oldBuffer.Length];

                Array.Copy(oldBuffer, headerEnd + HeaderEndMarker.Length, Buffer, 0, BufferBytesRead - headerEnd - HeaderEndMarker.Length);
            }

            int bufferBytesRead = BufferBytesRead - (headerEnd + HeaderEndMarker.Length);

            ContentLength = long.Parse(WebConnection.Headers["CONTENT-LENGTH"]);

            if (bufferBytesRead > ContentLength)
                // I'm not sure if HTTP allows headers to be sent before the request is over
                // This situation is just too hard in non-blocking mode, only the multi-threaded versiom handles it
                Close(WebResults.FromString(Status._400_Bad_Request, "This server can't handle sending a header before it completes the request!"));

            if (ContentLength <= WebServer.MaxInMemoryContentSize)
                Content = new WebConnectionContent.InMemory(ContentLength);
            else if (ContentLength <= WebServer.MaxContentSize)
                Content = new WebConnectionContent.OnDisk();
            else
            {
                Close(WebResults.FromString(Status._413_Request_Entity_Too_Large, "Too much data, max size: " + WebServer.MaxContentSize.ToString()));
                return;
            }

            WebConnectionIOState = WebConnectionIOState.ReadingContent;

            ReadStartTime = DateTime.UtcNow;

            GenericVoid startRead = null;

            AsyncCallback readCallback = delegate(IAsyncResult result)
            {
                try
                {
                    if (!Socket.Connected)
                        return;

                    bufferBytesRead = Socket.EndReceive(result);
                }
                catch (Exception e)
                {
                    log.Error("Exception while reading content", e);
                    Close();
                    return;
                }

                startRead();
            };

            startRead = delegate()
            {
                try
                {
                    if (bufferBytesRead > 0)
                    {
                        byte[] toCopy = new byte[bufferBytesRead];
                        Array.Copy(Buffer, 0, toCopy, 0, toCopy.Length);

                        Content.TakeBytes(toCopy);

                        if (Content.BytesRead >= ContentLength)
                        {
                            PerformRequest();
                            return;
                        }
                    }

                    int bytesToRead = Convert.ToInt32(ContentLength - Content.BytesRead);
                    if (bytesToRead > Buffer.Length)
                        bytesToRead = Buffer.Length;

                    if (ReadStartTime + WebServer.ContentTimeout < DateTime.UtcNow)
                    {
                        // The sending header timed out
                        Close(WebResults.FromString(Status._408_Request_Timeout, "Content times out after: " + WebServer.ContentTimeout.ToString()));
                        return;
                    }

                    Socket.BeginReceive(Buffer, 0, bytesToRead, SocketFlags.None, readCallback, null);
                }
                catch (Exception e)
                {
                    log.Error("Error reading from socket", e);
                    Close();
                }
            };

            startRead();

            /*if (0 == BufferBytesRead)
            {
                WebConnectionIOState = WebConnectionIOState.ReadingContent;
                DoIO = ReadContent;

                StartReadingSocket();
            }
            else if (BufferBytesRead <= ContentLength)
            {
            }
            else

            /*

            // Give the reader the additional read bytes
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
                    Buffer = new byte[oldBuffer.Length];

                    Array.Copy(oldBuffer, ContentLength, Buffer, 0, BufferBytesRead - ContentLength);

                    BufferBytesRead = Convert.ToInt32(Convert.ToInt64(BufferBytesRead) - ContentLength);
                }

                DoIO = NoOp;
                WebConnectionIOState = WebConnectionIOState.PerformingRequest;

                PerformRequest();
            }
            else if (0 == BufferBytesRead)
            {
                ReadStartTime = DateTime.UtcNow;

                WebConnectionIOState = WebConnectionIOState.ReadingContent;
                DoIO = ReadContent;

                StartReadingSocket();
            }
            else // Buffer has less bytes then what's needed
            {
                byte[] toCopy = new byte[BufferBytesRead];

                Array.Copy(Buffer, 0, toCopy, 0, BufferBytesRead);
                Content.TakeBytes(toCopy);

                BufferBytesRead = 0;

                ReadStartTime = DateTime.UtcNow;

                WebConnectionIOState = WebConnectionIOState.ReadingContent;
                DoIO = ReadContent;

                StartReadingSocket();
            }*/
        }

        /*// <summary>
        /// Call for when the web connection is reading content
        /// </summary>
        /// <returns></returns>
        private void ReadContent()
        {
            if (Content.BytesRead + BufferBytesRead <= ContentLength)
            {
                byte[] localBuffer = new byte[BufferBytesRead];
                Array.Copy(Buffer, localBuffer, BufferBytesRead);

                BufferBytesRead = 0;

                Content.TakeBytes(localBuffer);
            }
            else
            {
                // Content.BytesRead + BufferBytesRead > ContentLength

                // I'm not sure if HTTP allows headers to be sent before the request is over
                // This situation is just too hard in non-blocking mode, only the multi-threaded versiom handles it
                Close(WebResults.FromString(Status._400_Bad_Request, "This server can't handle sending a header before it completes the request!"));
            }
            /*else
            {
                // Additional data needs to stay on the buffer
                byte[] localBuffer = new byte[ContentLength - Content.BytesRead];
                Array.Copy(Buffer, localBuffer, localBuffer.Length);

                byte[] oldBuffer = Buffer;
                Buffer = new byte[oldBuffer.Length];
                Array.Copy(oldBuffer, localBuffer.Length, Buffer, 0, BufferBytesRead - localBuffer.Length);

                BufferBytesRead = BufferBytesRead - localBuffer.Length;

                Content.TakeBytes(localBuffer);
            }*/

            /*/ If the header end is found, then stop reading the socket and start handling the connection
            if (Content.BytesRead == ContentLength)
                PerformRequest();
            else
                StartReadingSocket();
        }*/

        /// <summary>
        /// Called when the header is completely loaded
        /// </summary>
        /// <param name="socketReader"></param>
        private void PerformRequest()
        {
            WebConnectionIOState = WebConnectionIOState.PerformingRequest;

            RequestThreadPool.RunThreadStart(delegate()
            {
                WebConnection.HandleConnection(Content);
            });
        }

        /*// <summary>
        /// Performs the request.  This must be handled on its own thread
        /// </summary>
        private void PerformRequestOnThread()
        {
            WebConnection.HandleConnection(Content);

            if (!WebServer.KeepAlive)
                Close();
            else
            {
                WebConnectionIOState = WebConnectionIOState.ReadingHeader;
                DoIO = ReadHeader;
                ReadStartTime = DateTime.UtcNow;

                /*while (BufferBytesRead == Buffer.Length)
                    DoIO();*/ /*
            }
        }*/

        /// <summary>
        /// Pool of threads to handle outstanding requests
        /// </summary>
        private static ThreadPoolInstance RequestThreadPool = new ThreadPoolInstance("Request Thread");

        /// <summary>
        /// Queues the result to be sent
        /// </summary>
        /// <param name="toSend"></param>
        private void SendToBrowser(Stream stream)
        {
            IOThread.QueueItem(delegate()
            {
                SendToBrowserFromIOThread(stream);
            });
        }

        /// <summary>
        /// Called from the send thread to start sending data
        /// </summary>
        private void SendToBrowserFromIOThread(Stream stream)
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

                    if (null != Content)
                    {
                        Content.Dispose();
                        Content = null;
                    }

                    if (!WebServer.KeepAlive)
                        Close();
                    else
                    {
                        WebConnectionIOState = WebConnectionIOState.ReadingHeader;
                        ReadStartTime = DateTime.UtcNow;

                        Start();

                    }
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

        /// <summary>
        /// Closes the socket reader after transmitting an error message
        /// </summary>
        private void Close(IWebResults webResults)
        {
            WebConnection.SendResults(webResults);
            Close();
        }

        /// <summary>
        /// Closes the socket reader, for all intents and purposes
        /// </summary>
        private void Close()
        {
            Socket.Close();
            WebConnectionIOState = WebConnectionIOState.Disconnected;
        }

        public override string ToString()
        {
            StringBuilder toReturn = new StringBuilder("Connection from: ");
            toReturn.Append(Socket.RemoteEndPoint.ToString());

            ISession session = WebConnection.Session;

            if (null != session)
                toReturn.AppendFormat(", Session: {0}", session.SessionId);

            return toReturn.ToString();
        }
    }
}
