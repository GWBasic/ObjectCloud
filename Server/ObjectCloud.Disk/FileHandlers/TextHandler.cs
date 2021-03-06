// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;

using ObjectCloud.Common;
using ObjectCloud.Common.Threading;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Disk.FileHandlers
{
    public class TextHandler : LastModifiedFileHandler, ITextHandler
    {
        string path;

        public TextHandler(string path, FileHandlerFactoryLocator fileHandlerFactoryLocator)
            : base(fileHandlerFactoryLocator, path)
        {
            this.path = path;
        }
				
        /// <summary>
        /// The cached text to minimize disk usage
        /// </summary>
        string Cached = null;

        /// <summary>
        /// The cached text to minimize disk usage
        /// </summary>
        IEnumerable<string> CachedEnumerable = null;

        public string ReadAll()
        {
            string cached = Cached;
            if (null != cached)
                return cached;

            using (TimedLock.Lock(this))
            {
                if (null == Cached)
                    Cached = System.IO.File.ReadAllText(path);

                return Cached;
            }
        }

        public void WriteAll(IUser changer, string contents)
        {
            using (TimedLock.Lock(this))
            {
                ReleaseMemory();

                System.IO.File.WriteAllText(path, contents);

                // set cached to null to test round trip
                Cached = contents;
            }

            if (null != FileContainer)
                SendUpdateNotificationFrom(changer);

            OnContentsChanged();
        }

        public override void Dump(string path, ID<IUserOrGroup, Guid> userId)
        {
            using (TimedLock.Lock(this))
            {
                DateTime destinationCreated = DateTime.MinValue;

                if (File.Exists(path))
                    destinationCreated = File.GetLastWriteTimeUtc(path);

                DateTime thisCreated = File.GetLastWriteTimeUtc(path);

                if (destinationCreated < thisCreated)
                {
                    if (File.Exists(path))
                        File.Delete(path);

                    File.WriteAllText(path, ReadAll());
                }
            }
        }

        public IEnumerable<string> ReadLines()
        {
            IEnumerable<string> cachedEnumerable = CachedEnumerable;

            if (null == cachedEnumerable)
                using (TimedLock.Lock(this))
                {
                    cachedEnumerable = CachedEnumerable;

                    if (null == cachedEnumerable)
                    {
                        cachedEnumerable = File.ReadAllLines(path);
                        CachedEnumerable = cachedEnumerable;

                        long size = 0;
                        foreach (string s in CachedEnumerable)
                            size += Encoding.Default.GetByteCount(s);
                    }
                }

            foreach (string s in cachedEnumerable)
                yield return s;
        }

        public override void SyncFromLocalDisk(string localDiskPath, bool force, DateTime lastModified)
        {
            using (TimedLock.Lock(this))
            {
                if (!File.Exists(path))
                    File.Copy(localDiskPath, path);

                DateTime thisCreated = File.GetLastWriteTimeUtc(path);

                if (lastModified > thisCreated || force)
                {
                    File.Delete(path);
                    File.Copy(localDiskPath, path);
                }

                ReleaseMemory();
            }
        }

        public void Append(IUser changer, string toAppend)
        {
            using (TimedLock.Lock(this))
            {
                string cached = Cached;

                ReleaseMemory();

                File.AppendAllText(path, toAppend);

                if (null != cached)
                    Cached = cached + toAppend;
            }

            // TODO:  diffing between the old and new text would be cool to include in the changedata
            if (null != FileContainer)
                SendUpdateNotificationFrom(changer);

            OnContentsChanged();
        }

        public event EventHandler<ITextHandler, EventArgs> ContentsChanged;

        /// <summary>
        /// Calls ContentsChanged
        /// </summary>
        protected void OnContentsChanged()
        {
            if (null != ContentsChanged)
                ContentsChanged(this, new EventArgs());
        }

        private void ReleaseMemory()
        {
            Cached = null;
            CachedEnumerable = null;
        }
    }
}
