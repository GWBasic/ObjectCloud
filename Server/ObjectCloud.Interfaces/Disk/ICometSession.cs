// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Common;

namespace ObjectCloud.Interfaces.Disk
{
    /// <summary>
    /// Interface for comet sessions
    /// </summary>
    public interface ICometSession
    {
        /// <summary>
        /// The session ID.  This can be represented as 4 hex letters
        /// </summary>
        ID<ICometSession, ushort> ID { get; }

        /// <summary>
        /// Adds the given string to send to the client
        /// </summary>
        /// <param name="toSend"></param>
        void EnqueueToSend(string toSend);

        /// <summary>
        /// Occurs when data is recieved from the client
        /// </summary>
        event EventHandler<ICometSession, EventArgs<string>> DataRecieved;

        /// <summary>
        /// Occurs when data is sent to the client
        /// </summary>
        MulticastEventWithTimeout<ICometSession, EventArgs> DataSent { get; }

        /// <summary>
        /// Marks packets as acked
        /// </summary>
        /// <param name="packetId"></param>
        void AckSentPackets(ulong highestAckedSentPacketId);

        /// <summary>
        /// Processes all recieved data
        /// </summary>
        /// <param name="recieved"></param>
        void RecieveData(ulong recievedPacketId, string recieved);

        /// <summary>
        /// Returns true if there is unaknowledged sent data
        /// </summary>
        bool HasUnAckedSentPackets { get; }

        /// <summary>
        /// All of the unacked sent packets
        /// </summary>
        IEnumerable<KeyValuePair<ulong, string>> UnAckedSentPackets { get; }

        /// <summary>
        /// The variables in the current comet session
        /// </summary>
        IDictionary<string, string> Variables { get; }

        /// <summary>
        /// Merges the session's variables with the ones passed in
        /// </summary>
        /// <param name="variables"></param>
        void MergeVariables(IDictionary<string, string> variables);
    }
}
