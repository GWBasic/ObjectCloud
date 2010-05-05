// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Common;

namespace ObjectCloud.Interfaces.Disk
{
    /// <summary>
    /// Interface for binary objects on disk, like images
    /// </summary>
    public interface IBinaryHandler : IFileHandler
    {
        /// <summary>
        /// Returns all of the contents of the file
        /// </summary>
        /// <returns></returns>
        byte[] ReadAll();

        /// <summary>
        /// Writes all of the contents into the file
        /// </summary>
        /// <param name="contents"></param>
        void WriteAll(byte[] contents);

        /// <summary>
        /// Occurs whenever the data changes
        /// </summary>
        event EventHandler<IBinaryHandler, EventArgs> ContentsChanged;

        /// <summary>
        /// Returns true if there is a cached precalculated view for the given name
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        bool IsCachedPresent(string key);

        /// <summary>
        /// Sets a named cached view.  Cached views are deleted as soon as the object is overwritten
        /// </summary>
        /// <param name="key"></param>
        /// <param name="view"></param>
        void SetCached(string key, byte[] view);

        /// <summary>
        /// Gets the cached view
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        /// <exception cref="KeyNotFound">Thrown if there is no named cached view</exception>
        byte[] GetCached(string key);

        /// <summary>
        /// Tries to get the cached view for the given key.  Returns true if the cached view is present.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="view"></param>
        /// <returns></returns>
        bool TryGetCached(string key, out byte[] view);
    }
}
