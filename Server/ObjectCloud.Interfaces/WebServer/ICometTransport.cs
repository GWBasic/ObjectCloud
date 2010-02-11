// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
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
    /// Interface for an object that wraps either a comet transport (level 1) or a channel in a multiplexed comet transport (level 2)
    /// </summary>
    public interface ICometTransport : IDisposable
    {
        /// <summary>
        /// Called when the transport needs data to send
        /// </summary>
        /// <returns></returns>
        object GetDataToSend();
        
        /// <summary>
        /// Called when there is incoming data
        /// </summary>
        /// <param name="incoming"></param>
        void HandleIncomingData(object incoming);

        /// <summary>
        /// Event that occurs when a CometTransport is ready to send data.  The TimeSpan is how long a blocked long poll should wait for additional data
        /// </summary>
        MulticastEventWithTimeout<ICometTransport, EventArgs<TimeSpan>> StartSend { get; }
    }
}
