// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Common;

namespace ObjectCloud.Interfaces.Disk
{
    /// <summary>
    /// Base interface for the object that routes comet events to a Comet Session
    /// </summary>
    public interface ICometHandler : IDirectoryHandler
    {
        /// <summary>
        /// Creates a new session
        /// </summary>
        /// <returns></returns>
        /// <exception cref="TooManyCometSessions">Thrown if there are too many comet sessions open</exception>
        ICometSession CreateNewSession();

        /// <summary>
        /// Gets the comet session with the given id
        /// </summary>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        /// <exception cref="BadCometSessionId">Thrown if the id is invalid</exception>
        ICometSession this[ID<ICometSession, ushort> sessionId] { get; }

        /// <summary>
        /// Closes the session with the given id
        /// </summary>
        /// <param name="sessionId"></param>
        /// <exception cref="BadCometSessionId">Thrown if the id is invalid</exception>
        void Close(ID<ICometSession, ushort> sessionId);
    }
}
