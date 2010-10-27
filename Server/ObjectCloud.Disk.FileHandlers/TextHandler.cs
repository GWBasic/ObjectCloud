// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

using ObjectCloud.Common;
using ObjectCloud.Common.Threading;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Disk.FileHandlers
{
    public class TextHandler : LastModifiedFileHandler, ITextHandler
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
                    Cached = System.IO.File.ReadAllText(Path);

                return Cached;
            }
        }

        public void WriteAll(IUser changer, string contents)
        {
            using (TimedLock.Lock(this))
            {
                // Setting everything to null forces readers to block while writing occurs
                Cached = null;
                CachedEnumerable = null;

                System.IO.File.WriteAllText(Path, contents);

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

                CachedEnumerable = null;
                Cached = null;
            }
        }

        public void Append(IUser changer, string toAppend)
        {
            using (TimedLock.Lock(this))
            {
                // Again, we'll force a round-trip to disk so that we really know if something is getting munged
                CachedEnumerable = null;
                Cached = null;

                File.AppendAllText(Path, toAppend);
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
    }
}
