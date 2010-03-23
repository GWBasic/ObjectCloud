// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

using Common.Logging;

using JsonFx.Json;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Javascript;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Interfaces.WebServer
{
    /// <summary>
    /// Interface for objects that helps a WebHandler handle multiple QueuingReliableChannel layer 3 Comet Protocol connections
    /// </summary>
    public interface IChannelEventWebAdaptor
    {
        /// <summary>
        /// Adds a QueuingReliableCometTransport
        /// </summary>
        /// <param name="channel">
        /// A <see cref="IQueuingReliableCometTransport"/>
        /// </param>
        void AddChannel(IQueuingReliableCometTransport channel);

        /// <summary>
        /// Occurs whenever a client connects
        /// </summary>
        event EventHandler<IChannelEventWebAdaptor, EventArgs<IQueuingReliableCometTransport>> ClientConnected;

        /// <summary>
        /// Occurs whenever a client disconnects
        /// </summary>
        event EventHandler<IChannelEventWebAdaptor, EventArgs<IQueuingReliableCometTransport>> ClientDisconnected;

        /// <summary>
        /// Occurs whenever data is recieved from any connection
        /// </summary>
        /// <param name="toSend">
        /// A <see cref="System.Object"/>
        /// </param>
        event EventHandler<IChannelEventWebAdaptor, ChannelEventWebAdaptor.DataReceivedEventArgs> DataReceived;
        
        /// <summary>
        /// Sends data to all connection
        /// </summary>
        /// <param name="toSend">
        /// An object that must serialize to valid JSON through JsonFX.json
        /// </param>
        void SendAll(object toSend);

        /// <summary>
        /// Returns all of the connected sessions, including dupes if the user has multiple tabs / windows open
        /// </summary>
        IEnumerable<ISession> ConnectedSessions { get; }

        /// <summary>
        /// Allows enumeration over all channels; this allows data specific to each channel to be sent
        /// </summary>
        IEnumerable<IQueuingReliableCometTransport> Channels { get; }
    }
}
