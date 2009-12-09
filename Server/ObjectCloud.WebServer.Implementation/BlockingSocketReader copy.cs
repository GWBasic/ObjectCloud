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
    /// Used when reading from a socket in the Multi-threaded web-server
    /// </summary>
    internal class BlockingSocketReader
    {
        private static ILog log = LogManager.GetLogger<BlockingSocketReader>();

        /// <summary>
        /// Initializes the WebConnection
        /// </summary>
        /// <param name="s"></param>
        /// <param name="webServer"></param>
        public BlockingSocketReader(IWebServer webServer, Socket socket)
        {
            WebServer = webServer;
            Socket = socket;

            socket.NoDelay = true;
            socket.ReceiveTimeout = Convert.ToInt32(WebServer.HeaderTimeout.TotalMilliseconds);

            // Create the header buffer
            Buffer = new byte[webServer.HeaderSize];
            Array.Clear(Buffer, 0, webServer.HeaderSize);

            BufferBytesRead = 0;

            WebConnection = new WebConnection(webServer, socket.RemoteEndPoint, SendToBrowser);

            DoIO = ReadHeader;
        }

        /// <summary>
        /// The web server
        /// </summary>
        private IWebServer WebServer;

        /// <summary>
        /// The state that the SocketReader is currently in
        /// </summary>
        internal WebConnectionIOState WebConnectionIOState = WebConnectionIOState.ReadingHeader;

        /// <summary>
        /// The socket that this connection is working with
        /// </summary>
        internal Socket Socket;

        /// <summary>
        /// The fixed-size buffer for reading headers
        /// </summary>
        internal byte[] Buffer = null;

        /// <summary>
        /// The number of bytes read in the header
        /// </summary>
        internal int BufferBytesRead;

        /*// <summary>
        /// Helper object to read from the socket
        /// </summary>
        internal NetworkStream NetworkStream;*/

        /// <summary>
        /// The time that reading started
        /// </summary>
        internal DateTime ReadStartTime = DateTime.UtcNow;

        /// <summary>
        /// The non-blocking web connection
        /// </summary>
        internal WebConnection WebConnection;

        /// <summary>
        /// Delegate that performs the currently-needed IO
        /// </summary>
        internal GenericReturn<bool> DoIO;

        /// <summary>
        /// The current content
        /// </summary>
        internal WebConnectionContent Content = null;

        /// <summary>
        /// Marker for when an HTTP header ends
        /// </summary>
        static byte[] HeaderEndMarker = Encoding.UTF8.GetBytes("\r\n\r\n");

        /// <summary>
        /// Starts running the socket reader
        /// </summary>
        public void Start()
        {
            Thread socketReaderThread = new Thread(MonitorSocket);
            socketReaderThread.Name = Socket.RemoteEndPoint.ToString();
            socketReaderThread.IsBackground = true;
            socketReaderThread.Start();
        }

        /// <summary>
        /// Overall driver for watching the socket.  This is started on its own thread
        /// </summary>
        public void MonitorSocket(object state)
        {
            try
            {
                while (Socket.Connected & WebServer.Running)
                {
                    try
                    {
                        bool connectionOpen = DoIO();

                        if (!connectionOpen)
                            return;
                    }
                    catch (SocketException se)
                    {
                        log.Info("The connection with " + WebConnection.RemoteEndPoint.ToString() + " ended", se);

                        WebConnectionIOState = WebConnectionIOState.Disconnected;
                        Close();

                        return;
                    }
                    catch (Exception e)
                    {
                        log.FatalFormat("Fatal error when performing IO for a connection from {0}", e, Socket.RemoteEndPoint);

                        WebConnectionIOState = WebConnectionIOState.Disconnected;
                        Close();

                        return;
                    }

                    Thread.Sleep(25);
                }
            }
            catch (Exception e)
            {
                log.Warn("Socket thread aborted in error", e);
            }
            finally
            {
                if (null != Content)
                {
                    Content.Dispose();
                    Content = null;
                }

                // Make sure socket is closed
                if ((!WebServer.Running) && Socket.Connected)
                    Close();
            }
        }

        /// <summary>
        /// Instructs the object to read a bit of the header
        /// </summary>
        /// <returns></returns>
        public bool ReadHeader()
        {
            //int bytesRead = NetworkStream.Read(Buffer, BufferBytesRead, Buffer.Length - BufferBytesRead);
            int bytesRead = Socket.Receive(Buffer, BufferBytesRead, Buffer.Length - BufferBytesRead, SocketFlags.None);

            if (0 == bytesRead)
                return false;

            BufferBytesRead += bytesRead;

            if (BufferBytesRead > HeaderEndMarker.Length)
            {
                int headerEnd = Array<byte>.IndexOf(Buffer, HeaderEndMarker);

                if (headerEnd != -1)
                {
                    WebConnectionIOState = WebConnectionIOState.ParsingHeader;
                    DoIO = NoOp;

                    return HandleHeader(headerEnd);
                }
            }

            if (BufferBytesRead == WebServer.HeaderSize)
                // The header is too long
                Close(WebResults.FromString(Status._414_Request_URI_Too_Long, "Max header length: " + WebServer.HeaderSize.ToString()));

            else if (ReadStartTime + WebServer.HeaderTimeout < DateTime.UtcNow)
                // The sending header timed out
                Close(WebResults.FromString(Status._408_Request_Timeout, "Headers time out after: " + WebServer.HeaderTimeout.ToString()));

            return true;
        }

        /// <summary>
        /// Parses the incoming header
        /// </summary>
        private bool HandleHeader(int headerEnd)
        {
            byte[] headerBytes = new byte[headerEnd];
            Array.Copy(Buffer, headerBytes, headerBytes.Length);
            string header = Encoding.UTF8.GetString(headerBytes);
            
            // If more data was read into the buffer then the length of the header, then move it to the front of the buffer
            if (BufferBytesRead > headerEnd + HeaderEndMarker.Length)
            {
                byte[] oldBuffer = Buffer;
                Buffer = new byte[oldBuffer.Length];

                Array.Copy(oldBuffer, headerEnd + HeaderEndMarker.Length, Buffer, 0, BufferBytesRead - headerEnd - HeaderEndMarker.Length);
                //System.Buffer.BlockCopy(Buffer, headerEnd + HeaderEndMarker.Length, Buffer, 0, BufferBytesRead - headerEnd - HeaderEndMarker.Length);
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

                return false;
            }
            catch (Exception e)
            {
                log.Error("Exception occured while reading the header", e);
                Close(WebResults.FromString(Status._500_Internal_Server_Error, "An unknown error occured"));

                return false;
            }

            switch (WebConnection.Method)
            {
                case WebMethod.GET:

                    if (WebConnection.Headers.ContainsKey("CONTENT-LENGTH"))
                    {
                        Close(WebResults.FromString(Status._406_Not_Acceptable, "Content-Length can not be specified in the headers when performing a GET"));
                        return false; ;
                    }

                    return PerformRequest();

                case WebMethod.POST:
                    return SetUpReadingContent();

                default:
                    if (WebConnection.Headers.ContainsKey("CONTENT-LENGTH"))
                        return SetUpReadingContent();
                    else
                        return PerformRequest();
            }
        }

        /// <summary>
        /// Used when there is no data to read
        /// </summary>
        /// <returns></returns>
        private bool NoOp() 
        {
            return true;
        }

        /// <summary>
        /// The content-length
        /// </summary>
        private long ContentLength;

        /// <summary>
        /// Sets up the socket reader to read the Content of the HTTP request
        /// </summary>
        private bool SetUpReadingContent()
        {
            ContentLength = long.Parse(WebConnection.Headers["CONTENT-LENGTH"]);

            if (ContentLength <= WebServer.MaxInMemoryContentSize)
                Content = new WebConnectionContent.InMemory(ContentLength);
            else if (ContentLength <= WebServer.MaxContentSize)
                Content = new WebConnectionContent.OnDisk();
            else
            {
                Close(WebResults.FromString(Status._413_Request_Entity_Too_Large, "Too much data, max size: " + WebServer.MaxContentSize.ToString()));
                return false;
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
                    Buffer = new byte[oldBuffer.Length];

                    Array.Copy(oldBuffer, ContentLength, Buffer, 0, BufferBytesRead - ContentLength);
                    //System.Buffer.BlockCopy(Buffer, Convert.ToInt32(ContentLength), Buffer, 0, Convert.ToInt32(BufferBytesRead - ContentLength));

                    BufferBytesRead = Convert.ToInt32(Convert.ToInt64(BufferBytesRead) - ContentLength);
                }

                DoIO = NoOp;
                WebConnectionIOState = WebConnectionIOState.PerformingRequest;

                return PerformRequest();
            }
            else if (0 == BufferBytesRead)
            {
                ReadStartTime = DateTime.UtcNow;

                WebConnectionIOState = WebConnectionIOState.ReadingContent;
                DoIO = ReadContent;

                return true;
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

                return true;
            }
        }

        /// <summary>
        /// Call for when the web connection is reading content
        /// </summary>
        /// <returns></returns>
        private bool ReadContent()
        {
            //int bytesRead = NetworkStream.Read(Buffer, BufferBytesRead, Buffer.Length - BufferBytesRead);
            int bytesRead = Socket.Receive(Buffer, BufferBytesRead, Buffer.Length - BufferBytesRead, SocketFlags.None);

            if (0 == bytesRead)
                return false;

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
                Buffer = new byte[oldBuffer.Length];
                Array.Copy(oldBuffer, localBuffer.Length, Buffer, 0, BufferBytesRead - localBuffer.Length);
                //System.Buffer.BlockCopy(Buffer, localBuffer.Length, Buffer, 0, BufferBytesRead - localBuffer.Length);

                BufferBytesRead = BufferBytesRead - localBuffer.Length;

                Content.TakeBytes(localBuffer);
            }

            // If the header end is found, then stop reading the socket and start handling the connection
            if (Content.BytesRead >= ContentLength)
            {
                WebConnectionIOState = WebConnectionIOState.PerformingRequest;
                DoIO = NoOp;

                return PerformRequest();
            }
            else if (ReadStartTime + WebServer.ContentTimeout < DateTime.UtcNow)
            {
                // The sending header timed out

                Content.Dispose();

                WebConnectionIOState = WebConnectionIOState.Disconnected;

                WebConnection.SendResults(WebResults.FromString(Status._408_Request_Timeout, "Content times out after: " + WebServer.HeaderTimeout.ToString()));
                Socket.Close();

                return false;
            }

            return true;
        }

        /// <summary>
        /// Called when the header is completely loaded
        /// </summary>
        /// <param name="socketReader"></param>
        protected bool PerformRequest()
        {
            Thread = Thread.CurrentThread;
            StreamToReturn = null;

            WebConnection.HandleConnection(Content);

            if (null == StreamToReturn)
                lock (StreamReadySignal)
                    if (null == StreamToReturn)
                        Monitor.Wait(StreamReadySignal);

            SendToBrowserInt(StreamToReturn);

            if (!WebServer.KeepAlive)
                return false;
            else
            {
                WebConnectionIOState = WebConnectionIOState.ReadingHeader;
                ReadStartTime = DateTime.UtcNow;
                DoIO = ReadHeader;

                return true;
            }
        }

        /// <summary>
        /// The thread that the WebConnection is running on
        /// </summary>
        private Thread Thread;

        /// <summary>
        /// The stream to return to the browser
        /// </summary>
        private volatile Stream StreamToReturn;

        /// <summary>
        /// Signals when there is data ready to return to the browser
        /// </summary>
        private object StreamReadySignal = new object();

        /// <summary>
        /// Caches the results to send
        /// </summary>
        /// <param name="stream"></param>
        private void SendToBrowser(Stream stream)
        {
            StreamToReturn = stream;

            if (Thread.CurrentThread != Thread)
                lock (StreamReadySignal)
                    Monitor.Pulse(StreamReadySignal);
        }

        /// <summary>
        /// Sends the stream to the browser.  This should be called on the same thread that owns this web connection
        /// </summary>
        /// <param name="stream"></param>
        private void SendToBrowserInt(Stream stream)
        {
            byte[] buffer = new byte[4096];

            using (stream)
                try
                {
                    if (Socket.Connected)
                    {
                        int bytesRead;
                        do
                        {
                            bytesRead = stream.Read(buffer, 0, buffer.Length);

                            if (bytesRead > 0)
                                //NetworkStream.Write(buffer, 0, bytesRead);
                                Socket.Send(buffer, 0, bytesRead, SocketFlags.None);

                        } while (bytesRead > 0);

                        //NetworkStream.Flush();
                    }
                    else
                        log.Debug("Connection Dropped....");

                    stream.Close();
                }
                catch (Exception e)
                {
                    log.Error("Error Occurred", e);
                }
        }


        /// <summary>
        /// Closes the socket reader after transmitting an error message
        /// </summary>
        internal void Close(IWebResults webResults)
        {
            WebConnection.SendResults(webResults);
            Close();
        }

        /// <summary>
        /// Closes the socket reader, for all intents and purposes
        /// </summary>
        internal void Close()
        {
            try
            {
                Socket.Close();
            }
            catch { }

            WebConnectionIOState = WebConnectionIOState.Disconnected;
            DoIO = NoOp;
        }
    }
}
