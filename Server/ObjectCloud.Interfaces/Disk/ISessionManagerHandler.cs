// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Interfaces.Disk
{
    /// <summary>
    /// Interface for objects that manages ongoing sessions that are on disk
    /// </summary>
    public interface ISessionManagerHandler : IFileHandler
    {
        /// <summary>
        /// Returns the session associated with the ID
        /// </summary>
        /// <param name="sessionId">The sessionId, usually stored in a cookie</param>
        /// <returns>The ISession associated with the ID, or null if there is no session with the given ID</returns>
        ISession this[ID<ISession, Guid> sessionId] { get; }

        /// <summary>
        /// Creates a new session.
        /// </summary>
        /// <returns></returns>
        ISession CreateSession();

        /// <summary>
        /// Deletes the session with the given ID
        /// </summary>
        /// <param name="sessionId"></param>
        void EndSession(ID<ISession, Guid> sessionId);

        /// <summary>
        /// Removes old sessions.  This is normally called every hour or so automatically
        /// </summary>
        void CleanOldSessions();
		
		/// <summary>
		/// The maximum comet transports allowed until old ones get killed 
		/// </summary>
		int MaxCometTransports {get; set; }
    }
}
