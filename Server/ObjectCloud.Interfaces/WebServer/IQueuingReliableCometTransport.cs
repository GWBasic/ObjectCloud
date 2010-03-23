// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Interfaces.WebServer
{
    /// <summary>
    /// Interface for a socket-type connection to a browser; used for level 3 Queuing and Reliable Comet Protocol
    /// </summary>
    public interface IQueuingReliableCometTransport
    {
        /// <summary>
        /// Closes the connection
        /// </summary>
        void Close();

        /// <summary>
        /// Occurs when the client initiates closing the connection
        /// </summary>
        event EventHandler<IQueuingReliableCometTransport, EventArgs> ConnectionDisconnecting;

        /// <summary>
        /// Occurs when the connection ends, either when initiated from the server or client
        /// </summary>
        event EventHandler<IQueuingReliableCometTransport, EventArgs> ConnectionEnded;

        /// <summary>
        /// Occurs whenever data is recieved.  The data sent is the JSON object from the client, decoded through JsonFX.json
        /// </summary>
        event EventHandler<IQueuingReliableCometTransport, EventArgs<QueuingReliableCometTransport.Packet>> DataRecieved;

        /// <summary>
        /// True if the connection is disposed and invalud
        /// </summary>
        bool IsDisposed { get; }

        /// <summary>
        /// Sends the data after a short delay
        /// </summary>
        /// <param name="toSend"></param>
        /// <param name="maxDelay"></param>
        void Send(object toSend);

        /// <summary>
        /// Sends the data.  Use a delay of 0 to send immediately; use a larger delay if more data wil be queued so they can be sent in a batch
        /// </summary>
        /// <param name="toSend"></param>
        /// <param name="maxDelay"></param>
        void Send(object toSend, TimeSpan maxDelay);

        /// <summary>
        /// The session associated with the connection
        /// </summary>
        ISession Session { get; }

        /// <summary>
        /// The state of the connection
        /// </summary>
        QueuingReliableCometTransport.StateEnum State { get; }

        /// <summary>
        /// The url that the client connected to
        /// </summary>
        string Url { get; }
    }
}
