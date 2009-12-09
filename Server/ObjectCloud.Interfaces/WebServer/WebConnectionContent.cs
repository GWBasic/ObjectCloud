// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;

namespace ObjectCloud.Interfaces.WebServer
{
    public class WebConnectionContent
    {
        public class SocketDisconnected : Exception { }

        /// <summary>
        /// Holds the connection content in memory instead of caching it to disk
        /// </summary>
        public class InMemory : IWebConnectionContent
        {
            public InMemory(ulong contentLength, NetworkStream networkStream, Socket socket)
            {
                Content = new byte[contentLength];

                int sleepTime = 10;

                for (uint ctr = 0; ctr < contentLength; )
                {
                    if (!socket.Connected)
                        throw new SocketDisconnected();

                    int notByte = networkStream.ReadByte();

                    if (notByte >= byte.MinValue || notByte <= byte.MaxValue)
                    {
                        Content[ctr] = Convert.ToByte(notByte);
                        ctr++;

                        sleepTime = 10;
                    }
                    else
                    {
                        Thread.Sleep(sleepTime);
                        sleepTime = sleepTime * 2;

                        if (sleepTime >= 200)
                            sleepTime = 200;
                    }
                }
            }

            public InMemory(byte[] content)
            {
                Content = content;
            }

            /// <summary>
            /// The content
            /// </summary>
            readonly byte[] Content;

            public string AsString()
            {
                return Encoding.UTF8.GetString(Content);
            }

            public byte[] AsBytes()
            {
                return Content;
            }

            public Stream AsStream()
            {
                return new MemoryStream(Content, false);
            }

            public void WriteToFile(string filename)
            {
                File.WriteAllBytes(filename, Content);
            }

            public void Dispose()
            {
            }
        }

        /// <summary>
        /// Puts the contents of the web connection onto disk
        /// </summary>
        public class OnDisk : IWebConnectionContent
        {
            private static ILog log = LogManager.GetLogger<OnDisk>();

            public OnDisk(ulong contentLength, NetworkStream networkStream, Socket socket)
            {
                ContentFilename = Path.GetTempFileName();
                File.Delete(ContentFilename);

                FileStream stream = File.OpenWrite(ContentFilename);
                uint ctr = 0;

                try
                {
                    int sleepTime = 10;

                    for (; ctr < contentLength; )
                    {
                        if (!socket.Connected)
                            throw new SocketDisconnected();

                        int notByte = networkStream.ReadByte();

                        if (notByte >= byte.MinValue || notByte <= byte.MaxValue)
                        {
                            if (null == stream)
                            {
                                stream = File.OpenWrite(ContentFilename);
                                stream.Seek(0, SeekOrigin.End);
                            }

                            stream.WriteByte(Convert.ToByte(notByte));
                            ctr++;

                            sleepTime = 10;
                        }
                        else
                        {
                            stream.Flush();
                            stream.Close();
                            stream.Dispose();
                            stream = null;

                            Thread.Sleep(sleepTime);
                            sleepTime = sleepTime * 2;

                            if (sleepTime >= 200)
                                sleepTime = 200;
                        }
                    }
                }
                finally
                {
                    if (null != stream)
                    {
                        stream.Flush();
                        stream.Close();
                        stream.Dispose();
                    }
                }
            }

            /// <summary>
            /// Where the content is stored on disk
            /// </summary>
            string ContentFilename;

            public string AsString()
            {
                return File.ReadAllText(ContentFilename);
            }

            public byte[] AsBytes()
            {
                return File.ReadAllBytes(ContentFilename);
            }

            public Stream AsStream()
            {
                return File.OpenRead(ContentFilename);
            }

            public void WriteToFile(string filename)
            {
                File.Copy(ContentFilename, filename);
            }

            public void Dispose()
            {
                try
                {
                    File.Delete(ContentFilename);
                }
                catch (Exception e)
                {
                    log.Error("Exception when deleting large content sent from browser", e);
                }
            }
        }
    }
}
