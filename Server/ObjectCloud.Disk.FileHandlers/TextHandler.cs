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
                System.IO.File.WriteAllText(Path, contents);

                // In case the write mungs data, we never cache on write.  This way, the user will see munged data sooner
                Cached = null;
                CachedEnumerable = null;
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
            using (TimedLock.Lock(this))
            {
                if (null == CachedEnumerable)
                    CachedEnumerable = File.ReadAllLines(Path);

                foreach (string s in CachedEnumerable)
                    yield return s;
            }
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
