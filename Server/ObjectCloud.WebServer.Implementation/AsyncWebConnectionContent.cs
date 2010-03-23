// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
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
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.WebServer.Implementation
{
    public abstract class AsyncWebConnectionContent : IWebConnectionContent
    {
        public abstract string AsString();

        public abstract byte[] AsBytes();

        public abstract Stream AsStream();

        public abstract void WriteToFile(string filename);

        public virtual void Dispose()
        {
        }

        /// <summary>
        /// Called when there are bytes ready for input
        /// </summary>
        /// <param name="input"></param>
        abstract public void TakeBytes(byte[] input);

        /// <summary>
        /// The number of bytes read
        /// </summary>
        public abstract long BytesRead { get; }

        /// <summary>
        /// Holds the connection content in memory instead of caching it to disk
        /// </summary>
        public class InMemory : AsyncWebConnectionContent
        {
            /// <summary>
            /// The content
            /// </summary>
            readonly byte[] Content;

            public InMemory(long contentLength) : this(Convert.ToInt32(contentLength)) {}

            public InMemory(int contentLength)
            {
                Content = new byte[contentLength];
            }

            public override string AsString()
            {
                return Encoding.UTF8.GetString(Content);
            }

            public override byte[] AsBytes()
            {
                return Content;
            }

            public override Stream AsStream()
            {
                return new MemoryStream(Content, false);
            }

            public override void WriteToFile(string filename)
            {
                File.WriteAllBytes(filename, Content);
            }

            /// <summary>
            /// The total number of bytes taken
            /// </summary>
            public override long BytesRead
            {
                get { return _BytesRead; }
            }
            long _BytesRead = 0;

            public override void TakeBytes(byte[] input)
            {
#if DEBUG

                /*string text = Encoding.UTF8.GetString(input);
                if (text.StartsWith("POST"))
                    if (System.Diagnostics.Debugger.IsAttached)
                        System.Diagnostics.Debugger.Break();*/

#endif
                Array.Copy(input, 0, Content, _BytesRead, input.Length);
                _BytesRead += input.Length;
            }
        }

        /// <summary>
        /// Puts the contents of the web connection onto disk
        /// </summary>
        public class OnDisk : AsyncWebConnectionContent
        {
            private static ILog log = LogManager.GetLogger<OnDisk>();

            public OnDisk()
            {
                ContentFilename = Path.GetTempFileName();
                File.Delete(ContentFilename);
            }

            /// <summary>
            /// Where the content is stored on disk
            /// </summary>
            string ContentFilename;

            public override string AsString()
            {
                Flush();
                return File.ReadAllText(ContentFilename);
            }

            public override byte[] AsBytes()
            {
                Flush();
                return File.ReadAllBytes(ContentFilename);
            }

            public override Stream AsStream()
            {
                Flush();
                return File.OpenRead(ContentFilename);
            }

            public override void WriteToFile(string filename)
            {
                Flush();
                File.Copy(ContentFilename, filename);
            }

            public override void Dispose()
            {
                try
                {
                    File.Delete(ContentFilename);
                }
                catch (Exception e)
                {
                    log.Error("Exception when deleting large content sent from browser", e);
                }

                base.Dispose();
            }

            object BufferKey = new object();

            /// <summary>
            /// The buffer of incoming data
            /// </summary>
            LinkedList<Byte[]> Buffer = new LinkedList<byte[]>();

            /// <summary>
            /// The total bytes read
            /// </summary>
            long BytesInBuffer = 0;

            public override long BytesRead
            {
                get { return _BytesRead; }
            }
            long _BytesRead = 0;

            public override void TakeBytes(byte[] input)
            {
                lock (BufferKey)
                {
                    Buffer.AddLast(input);
                    BytesInBuffer += input.Length;
                    _BytesRead += input.Length;
                }

                if (BytesInBuffer > 1024 * 70)
                    ThreadPool.QueueUserWorkItem(delegate(object o)
                    {
                        try
                        {
                            Flush();
                        }
                        catch (Exception e)
                        {
                            log.Warn("Exception when asyncronously writing incoming data", e);
                        }
                    });
            }

            /// <summary>
            /// Flushes the buffer to disk
            /// </summary>
            public void Flush()
            {
                if (0 == BytesInBuffer)
                    return;

                LinkedList<byte[]> myBuffer;

                lock (BufferKey)
                {
                    myBuffer = Buffer;
                    Buffer = new LinkedList<byte[]>();
                    BytesInBuffer = 0;
                }

                using (FileStream fs = File.OpenWrite(ContentFilename))
                {
                    foreach (byte[] toWrite in myBuffer)
                        fs.Write(toWrite, 0, toWrite.Length);

                    fs.Flush();
                    fs.Close();
                }
            }
        }
    }
}
