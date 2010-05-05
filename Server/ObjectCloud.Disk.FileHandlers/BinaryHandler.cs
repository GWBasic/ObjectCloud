// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Data.Common;
using System.Text;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Database;
using ObjectCloud.Interfaces.Disk;

namespace ObjectCloud.Disk.FileHandlers
{
    /// <summary>
    /// Handles binary files
    /// </summary>
	public class BinaryHandler : LastModifiedFileHandler, IBinaryHandler
	{
        /// <summary>
        /// Creates the text file name
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string CreateBinaryFilename(string path)
        {
            return string.Format("{0}{1}file.bin", path, Path.DirectorySeparatorChar);
        }

        public BinaryHandler(string path, FileHandlerFactoryLocator fileHandlerFactoryLocator)
            : base(fileHandlerFactoryLocator, path)
        {
            CachePath = path;
            BinaryFile = CreateBinaryFilename(path);
            ContentsChanged += new EventHandler<IBinaryHandler, EventArgs>(BinaryHandler_ContentsChanged);
        }

        void BinaryHandler_ContentsChanged(IBinaryHandler sender, EventArgs e)
        {
            foreach (string cachedView in Directory.GetFiles(CachePath, "*.cached"))
                File.Delete(cachedView);
        }

        /// <summary>
        /// Allow the data to be cached to lower disk usage
        /// </summary>
        byte[] Cached = null;

        /// <summary>
        /// The folder that's used to store the binary file and all cached views
        /// </summary>
        private readonly string CachePath;

        /// <summary>
        /// The filename on the disk
        /// </summary>
        private readonly string BinaryFile;

        public byte[] ReadAll()
        {
            using (TimedLock.Lock(this))
            {
                if (null == Cached)
                    Cached = System.IO.File.ReadAllBytes(BinaryFile);

                return Array<byte>.ShallowCopy(Cached);
            }
        }

        public void WriteAll(byte[] contents)
        {
            using (TimedLock.Lock(this))
            {
                System.IO.File.WriteAllBytes(BinaryFile, contents);
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

                DateTime thisCreated = File.GetLastWriteTimeUtc(BinaryFile);

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
                if (!File.Exists(BinaryFile))
                    File.Copy(localDiskPath, BinaryFile);

                DateTime authoritativeCreated = File.GetLastWriteTimeUtc(localDiskPath);
                DateTime thisCreated = File.GetLastWriteTimeUtc(BinaryFile);

                if (authoritativeCreated > thisCreated || force)
                {
                    File.Delete(BinaryFile);
                    File.Copy(localDiskPath, BinaryFile);
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

        private string GenerateCachedFilename(string key)
        {
            return CachePath + Path.DirectorySeparatorChar + Convert.ToBase64String(Encoding.UTF8.GetBytes(key)) + ".cached";
        }

        public bool IsCachedPresent(string key)
        {
            return File.Exists(GenerateCachedFilename(key));
        }

        public void SetCached(string key, byte[] view)
        {
            File.WriteAllBytes(
                GenerateCachedFilename(key),
                view);
        }

        public byte[] GetCached(string key)
        {
            byte[] view;
            if (!TryGetCached(key, out view))
                throw new KeyNotFoundException();

            return view;
        }

        public bool TryGetCached(string key, out byte[] view)
        {
            string filename = GenerateCachedFilename(key);

            if (File.Exists(filename))
            {
                view = File.ReadAllBytes(filename);
                return true;
            }
            else
            {
                view = null;
                return false;
            }
        }
    }
}
