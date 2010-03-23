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
    public abstract class WebConnectionContent : IWebConnectionContent
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
        public class InMemory : WebConnectionContent
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
                Array.Copy(input, 0, Content, _BytesRead, input.Length);

                _BytesRead += input.Length;
            }
        }

        /// <summary>
        /// Puts the contents of the web connection onto disk
        /// </summary>
        public class OnDisk : WebConnectionContent
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
                return File.ReadAllText(ContentFilename);
            }

            public override byte[] AsBytes()
            {
                return File.ReadAllBytes(ContentFilename);
            }

            public override Stream AsStream()
            {
                return File.OpenRead(ContentFilename);
            }

            public override void WriteToFile(string filename)
            {
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

            public override long BytesRead
            {
                get { return _BytesRead; }
            }
            long _BytesRead = 0;

            public override void TakeBytes(byte[] input)
            {
                using (FileStream fs = File.OpenWrite(ContentFilename))
                {
                    fs.Seek(0, SeekOrigin.End);

                    fs.Write(input, 0, input.Length);

                    fs.Flush();
                    fs.Close();
                }

                _BytesRead += input.Length;
            }
        }
    }
}
