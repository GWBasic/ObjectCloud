using System;
using System.Collections.Generic;
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
    internal class AsyncSocketReader
    {
        private static ILog log = LogManager.GetLogger<AsyncSocketReader>();

        /// <summary>
        /// Initializes the WebConnection
        /// </summary>
        /// <param name="s"></param>
        /// <param name="webServer"></param>
        public AsyncSocketReader(AsyncWebServer webServer, Socket socket)
        {
            WebServer = webServer;
            Socket = socket;
            ReadStartTime = DateTime.UtcNow;

            // Create the header buffer
            Buffer = new byte[webServer.HeaderSize];

            BufferBytesRead = 0;

            WebConnection = new AsyncWebConnection(webServer, socket);

            DoIO = ReadHeader;

            StartReadingSocket();
        }

        /// <summary>
        /// The web server
        /// </summary>
        private AsyncWebServer WebServer;

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
        /// Where the header ends
        /// </summary>
        internal volatile int HeaderEnd;

        /// <summary>
        /// The time that reading started
        /// </summary>
        internal DateTime ReadStartTime;

        /// <summary>
        /// The web connection
        /// </summary>
        internal WebConnection WebConnection;

        /// <summary>
        /// Delegate that performs the currently-needed IO
        /// </summary>
        internal volatile GenericVoid DoIO;

        /// <summary>
        /// The current content
        /// </summary>
        internal volatile AsyncWebConnectionContent Content = null;

        /// <summary>
        /// Marker for when an HTTP header ends
        /// </summary>
        static byte[] HeaderEndMarker = Encoding.UTF8.GetBytes("\r\n\r\n");

        /// <summary>
        /// Indicates if the socket is being read
        /// </summary>
        bool ReadingSocket = false;

        /// <summary>
        /// Starts reading the socket
        /// </summary>
        private void StartReadingSocket()
        {
            if (!ReadingSocket)
            {
#if DEBUG
                LastBuffer = Encoding.UTF8.GetString(Buffer);
                LastBufferBytesRead = BufferBytesRead;

                /*if (LastBuffer.StartsWith("POST") && LastBuffer.Contains("\r\n\r\n") && BufferBytesRead > 0)
                    System.Diagnostics.Debugger.Break();*/

                // This helps with debugging so there isn't a lot of cruft when viewing as a string
                Array.Clear(Buffer, BufferBytesRead, Buffer.Length - BufferBytesRead);
#endif
                Socket.BeginReceive(Buffer, BufferBytesRead, Buffer.Length - BufferBytesRead, SocketFlags.None, ReadCallback, null);
                ReadingSocket = true;
            }
        }

#if DEBUG
        string LastBuffer;
        int LastBufferBytesRead;
        string DoIOName;
        int Read;
        //static Set<int> ThreadIds = new Set<int>();
#endif

        /// <summary>
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
                            System.Diagnostics.Debugger.Break();*/

#if DEBUG
                    DoIOName = DoIO.Method.Name;
                    Read = read;
#endif

                    DoIO();
                }

                //if (BufferBytesRead < Buffer.Length)

                while (Buffer.Length == BufferBytesRead)
                    Thread.Sleep(5);

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
        }

        /// <summary>
        /// Instructs the object to read a bit of the header
        /// </summary>
        /// <returns></returns>
        public void ReadHeader()
        {
                HeaderEnd = Array<byte>.IndexOf(
                    Buffer,
                    HeaderEndMarker,
                    0,
                    BufferBytesRead);

                // If the header end is found, then stop reading the socket and start handling the connection
                if (-1 != HeaderEnd)
                {
                    WebConnectionIOState = WebConnectionIOState.ParsingHeader;
                    DoIO = NoOp;

                    HandleHeader();
                }

                else if (BufferBytesRead == WebServer.HeaderSize)
                    // The header is too long
                    Close(WebResults.FromString(Status._414_Request_URI_Too_Long, "Max header length: " + WebServer.HeaderSize.ToString()));

                else if (ReadStartTime + WebServer.HeaderTimeout < DateTime.UtcNow)
                    // The sending header timed out
                    Close(WebResults.FromString(Status._408_Request_Timeout, "Headers time out after: " + WebServer.HeaderTimeout.ToString()));

                //else
                    //StartReadingSocket();
        }

        /// <summary>
        /// Parses the incoming header
        /// </summary>
        private void HandleHeader()
        {
            byte[] headerBytes = new byte[HeaderEnd];
            Array.Copy(Buffer, headerBytes, HeaderEnd);
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

            /*if (WebConnection.Method == WebMethod.POST)
                Console.WriteLine(header);*/

            /*if (BufferBytesRead > 4000)
                    System.Diagnostics.Debugger.Break();*/

            // If more data was read into the buffer then the length of the header, then move it to the front of the buffer
            if (BufferBytesRead > HeaderEnd + HeaderEndMarker.Length)
            {
                byte[] oldBuffer = Buffer;
                Buffer = new byte[oldBuffer.Length];

                Array.Copy(oldBuffer, HeaderEnd + HeaderEndMarker.Length, Buffer, 0, BufferBytesRead - HeaderEnd - HeaderEndMarker.Length);
            }

            BufferBytesRead = BufferBytesRead - (HeaderEnd + HeaderEndMarker.Length);

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
        /// Used when there is no data to read
        /// </summary>
        /// <returns></returns>
        private void NoOp() { }

        /// <summary>
        /// The content-length
        /// </summary>
        private long ContentLength;

        /// <summary>
        /// Sets up the socket reader to read the Content of the HTTP request
        /// </summary>
        private void SetUpReadingContent()
        {
            ContentLength = long.Parse(WebConnection.Headers["CONTENT-LENGTH"]);

            if (ContentLength <= WebServer.MaxInMemoryContentSize)
                Content = new AsyncWebConnectionContent.InMemory(ContentLength);
            else if (ContentLength <= WebServer.MaxContentSize)
                Content = new AsyncWebConnectionContent.OnDisk();
            else
            {
                Close(WebResults.FromString(Status._413_Request_Entity_Too_Large, "Too much data, max size: " + WebServer.MaxContentSize.ToString()));
                return;
            }

            // TODO: get rid of this
            /*if (BufferBytesRead > 4)
                if (Encoding.UTF8.GetString(Buffer).StartsWith("POST"))
                    System.Diagnostics.Debugger.Break();*/

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

                //StartReadingSocket();
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

                //StartReadingSocket();
            }
        }

        /// <summary>
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
                // Additional data needs to stay on the buffer
                byte[] localBuffer = new byte[ContentLength - Content.BytesRead];
                Array.Copy(Buffer, localBuffer, localBuffer.Length);

                byte[] oldBuffer = Buffer;
                Buffer = new byte[oldBuffer.Length];
                Array.Copy(oldBuffer, localBuffer.Length, Buffer, 0, BufferBytesRead - localBuffer.Length);

                BufferBytesRead = BufferBytesRead - localBuffer.Length;

                Content.TakeBytes(localBuffer);
            }

            // If the header end is found, then stop reading the socket and start handling the connection
            if (Content.BytesRead >= ContentLength)
                PerformRequest();
            //else
                //StartReadingSocket();
        }

        /// <summary>
        /// Called when the header is completely loaded
        /// </summary>
        /// <param name="socketReader"></param>
        private void PerformRequest()
        {
            WebConnectionIOState = WebConnectionIOState.PerformingRequest;
            DoIO = NoOp;

            WebConnection.ResultsSent += new GenericVoid(WebConnection_ResultsSent);

            //WebConnection.HandleConnection(Content);

            ThreadPool.QueueUserWorkItem(delegate(object wcc)
            {
                WebConnection.HandleConnection((IWebConnectionContent)wcc);
            },
            Content);

            /*Thread subThread = new Thread(delegate()
            {
                WebConnection.HandleConnection(Content);
                Thread.Sleep(TimeSpan.FromMinutes(5));
            });
            subThread.Start();*/
        }

        /// <summary>
        /// Handles when the results are sent.  This even works with non-blocking long-polling!
        /// </summary>
        void WebConnection_ResultsSent()
        {
            WebConnection.ResultsSent -= new GenericVoid(WebConnection_ResultsSent);

            if (null != Content)
            {
                Content.Dispose();
                Content = null;
            }

            if (!WebServer.KeepAlive)
                Close();
            else
            {
                /*if (BufferBytesRead > 0)
                {
                    // If there is a complete header in the buffer, use it before switching to the ReadingHeader mode
                    HeaderEnd = Array<byte>.IndexOf(
                        Buffer,
                        HeaderEndMarker,
                        0,
                        BufferBytesRead);

                    if (-1 != HeaderEnd)
                    {
                        WebConnectionIOState = WebConnectionIOState.ParsingHeader;
                        DoIO = NoOp;
                        HandleHeader();

                        return;
                    }
                }*/

                WebConnectionIOState = WebConnectionIOState.ReadingHeader;
                DoIO = ReadHeader;
                //StartReadingSocket();
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
            DoIO = NoOp;
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
