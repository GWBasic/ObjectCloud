// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Data.Common;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Database;
using ObjectCloud.Interfaces.Disk;

namespace ObjectCloud.Disk.FileHandlers
{
    /// <summary>
    /// Handles binary files
    /// </summary>
	public class BinaryHandler : FileHandler, IBinaryHandler
	{
        public BinaryHandler(string path, FileHandlerFactoryLocator fileHandlerFactoryLocator)
            : base(fileHandlerFactoryLocator, path)
        {
            Path = path;
        }

        /// <summary>
        /// Allow the data to be cached to lower disk usage
        /// </summary>
        byte[] Cached = null;

        /// <summary>
        /// The filename on the disk
        /// </summary>
        private readonly string Path;

        public byte[] ReadAll()
        {
            using (TimedLock.Lock(this))
            {
                if (null == Cached)
                    Cached = System.IO.File.ReadAllBytes(Path);

                return Array<byte>.ShallowCopy(Cached);
            }
        }

        public void WriteAll(byte[] contents)
        {
            using (TimedLock.Lock(this))
            {
                System.IO.File.WriteAllBytes(Path, contents);
                Cached = Array<byte>.ShallowCopy(contents);
            }

            OnContentsChanged();
        }

        public override void Dump(string path, ID<ObjectCloud.Interfaces.Security.IUserOrGroup, Guid> userId)
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

                    File.WriteAllBytes(path, ReadAll());
                }
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
                    Cached = null;
                }
            }
        }

        public event EventHandler<IBinaryHandler, EventArgs> ContentsChanged;

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
