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
    public class TextHandler : LastModifiedFileHandler, ITextHandler, Cache.IAware
    {
        string Path;

        public TextHandler(string path, FileHandlerFactoryLocator fileHandlerFactoryLocator)
            : base(fileHandlerFactoryLocator, path)
        {
            Path = path;
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
                {
                    Cached = System.IO.File.ReadAllText(Path);
                    Cache.ManageMemoryUse(Encoding.Default.GetByteCount(Cached));
                }

                return Cached;
            }
        }

        public void WriteAll(IUser changer, string contents)
        {
            using (TimedLock.Lock(this))
            {
                ReleaseMemory();

                System.IO.File.WriteAllText(Path, contents);

                // set cached to null to test round trip
                Cached = contents;
                Cache.ManageMemoryUse(Encoding.Default.GetByteCount(Cached));
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

                DateTime thisCreated = File.GetLastWriteTimeUtc(Path);

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
                        cachedEnumerable = File.ReadAllLines(Path);
                        CachedEnumerable = cachedEnumerable;

                        long size = 0;
                        foreach (string s in CachedEnumerable)
                            size += Encoding.Default.GetByteCount(s);

                        Cache.ManageMemoryUse(size);
                    }
                }

            foreach (string s in cachedEnumerable)
                yield return s;
        }

        public override void SyncFromLocalDisk(string localDiskPath, bool force)
        {
            using (TimedLock.Lock(this))
            {
                if (!File.Exists(Path))
                    File.Copy(localDiskPath, Path);

                DateTime authoritativeCreated = File.GetLastWriteTimeUtc(localDiskPath);
                DateTime thisCreated = File.GetLastWriteTimeUtc(Path);

                if (authoritativeCreated > thisCreated || force)
                {
                    File.Delete(Path);
                    File.Copy(localDiskPath, Path);
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

                File.AppendAllText(Path, toAppend);

                if (null != cached)
                {
                    Cached = cached + toAppend;
                    Cache.ManageMemoryUse(Encoding.Default.GetByteCount(Cached));
                }
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

        /// <summary>
        /// Number of cache references
        /// </summary>
        private int CacheCount = 0;

        public void IncrementCacheCount()
        {
            Interlocked.Increment(ref CacheCount);
        }

        public void DecrementCacheCount()
        {
            if (Interlocked.Decrement(ref CacheCount) <= 0)
                ReleaseMemory();
        }

        ~TextHandler()
        {
            ReleaseMemory();
        }

        private void ReleaseMemory()
        {
            using (TimedLock.Lock(this))
            {
                long size = 0;

                if (null != Cached)
                    size += Encoding.Default.GetByteCount(Cached);

                if (null != CachedEnumerable)
                    foreach (string s in CachedEnumerable)
                        size += Encoding.Default.GetByteCount(s);

                Cached = null;
                CachedEnumerable = null;

                Cache.ManageMemoryUse(-1 * size);
            }
        }
    }
}
