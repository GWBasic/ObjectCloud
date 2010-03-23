// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Interfaces.WebServer
{
    /// <summary>
    /// Encapsulates everything about the current session
    /// </summary>
    public interface ISession
    {
        /// <summary>
        /// The current session Id
        /// </summary>
        ID<ISession, Guid> SessionId { get; }

        /// <summary>
        /// The current user
        /// </summary>
        IUser User { get; set; }

        /// <summary>
        /// The maximum age that the session can live
        /// </summary>
        TimeSpan MaxAge { get; set; }

        /// <summary>
        /// The last time the session was queried
        /// </summary>
        DateTime LastQuery { get; }

        /// <summary>
        /// Indicates that the browser should keep the session cookie alive after it is closed
        /// </summary>
        bool KeepAlive { get; set; }

        /// <summary>
        /// Returns all of the files touched when serving the given URL, or null if the files are unknown
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        Set<IFileContainer> GetFilesTouchedForUrl(string url);

        /// <summary>
        /// Sets all of the files that were touched for a given URL.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="filesTouched"></param>
        void SetFilesTouchedForUrl(string url, Set<IFileContainer> touchedFiles);
    }
}
