// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.DataAccess.Directory;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Javascript;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;


namespace ObjectCloud.Disk.FileHandlers
{
    public class CometSession : ICometSession
    {
        public CometSession(ID<ICometSession, ushort> id)
        {
            _ID = id;
            _DataSent = new DataSentEvent(TimeSpan.FromSeconds(2.5), this);
        }

        public ID<ICometSession, ushort> ID
        {
            get { return _ID; }
        }
        private ID<ICometSession, ushort> _ID;

        internal void Close()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// All of the text waiting to be sent
        /// </summary>
        StringBuilder SendQueue = new StringBuilder();

        /// <summary>
        /// All packets that haven't been acked
        /// </summary>
        private Dictionary<ulong, string> UnAckedPackets = new Dictionary<ulong, string>();

        public void EnqueueToSend(string toSend)
        {
            lock(SendQueue)
                SendQueue.Append(toSend);

            _DataSent.Send(new EventArgs());
        }

        public event EventHandler<ICometSession, EventArgs<string>> DataRecieved;

        protected void OnDataRecieved(string data)
        {
            if (null != DataRecieved)
                DataRecieved(this, new EventArgs<string>(data));
        }

        /// <summary>
        /// The next packet ID to use
        /// </summary>
        private ulong NextSendPacketId = 1;

        /// <summary>
        /// Converts the send queue to a packet
        /// </summary>
        /// <returns>True if there's data to send, false if there isn't data to send</returns>
        private bool ConvertSendQueueToPacket()
        {
            lock (SendQueue)
                if (SendQueue.Length > 0)
                {
                    ulong sendPacketId = NextSendPacketId;
                    NextSendPacketId++;

                    UnAckedPackets[sendPacketId] = SendQueue.ToString();
                    SendQueue.Remove(0, SendQueue.Length);

                    return true;
                }
         
            return false;
        }

        /// <summary>
        /// The next expected packet ID
        /// </summary>
        ulong nextHighestRecievedPacketId = 1;

        /// <summary>
        /// Ensures that only a single incoming packet is processed
        /// </summary>
        object RecieveLock = new object();

        public void RecieveData(ulong recievedPacketId, string recieved)
        {
            lock (RecieveLock)
            {
                if (recievedPacketId < nextHighestRecievedPacketId)

                    // Discard dupes
                    return;

                else if (recievedPacketId == nextHighestRecievedPacketId)
                    if (0 == BufferedRecievedPackets.Count)
                    {
                        OnDataRecieved(recieved);
                        nextHighestRecievedPacketId++;
                    }
                    else
                    {
                        StringBuilder allRecieved = new StringBuilder(recieved);
                        nextHighestRecievedPacketId++;

                        while (BufferedRecievedPackets.ContainsKey(nextHighestRecievedPacketId))
                        {
                            allRecieved.Append(BufferedRecievedPackets[nextHighestRecievedPacketId]);
                            nextHighestRecievedPacketId++;
                        }

                        OnDataRecieved(allRecieved.ToString());
                    }

                else // (recievedPacketId > nextHighestRecievedPacketId)

                    // It seems that a packet was dropped; hold on to this one
                    BufferedRecievedPackets[recievedPacketId] = recieved;
            }
        }

        /// <summary>
        /// All of the packets that have been recieved while there's a missing packet.
        /// </summary>
        Dictionary<ulong, string> BufferedRecievedPackets = new Dictionary<ulong, string>();

        public void AckSentPackets(ulong highestAckedSentPacketId)
        {
            lock (UnAckedPackets)
            {
                foreach (ulong unackedPacketId in new LinkedList<ulong>(UnAckedPackets.Keys))
                    if (unackedPacketId <= highestAckedSentPacketId)
                        UnAckedPackets.Remove(unackedPacketId);
            }
        }

        public bool HasUnAckedSentPackets
        {
            get 
            {
                ConvertSendQueueToPacket();
                return UnAckedPackets.Count > 0; 
            }
        }

        public IEnumerable<KeyValuePair<ulong, string>> UnAckedSentPackets
        {
            get
            {
                ConvertSendQueueToPacket();

                foreach (KeyValuePair<ulong, string> packet in UnAckedPackets)
                    yield return packet;
            }
        }

        public IDictionary<string, string> Variables
        {
            get { return _Variables; }
        }
        private readonly Dictionary<string, string> _Variables = new Dictionary<string, string>();

        public void MergeVariables(IDictionary<string, string> variables)
        {
            foreach (KeyValuePair<string, string> variable in variables)
                _Variables[variable.Key] = variable.Value;
        }

        public MulticastEventWithTimeout<ICometSession, EventArgs> DataSent
        {
            get { return _DataSent; }
        }
        private readonly DataSentEvent _DataSent;

        /// <summary>
        /// Protected wrapper for sending an event
        /// </summary>
        private class DataSentEvent : MulticastEventWithTimeout<ICometSession, EventArgs>
        {
            public DataSentEvent(TimeSpan inactivityAccuracy, ICometSession session) : base(inactivityAccuracy, session) { }

            public void Send(EventArgs eventArgs)
            {
                TriggerEvent(eventArgs);
            }
        }
    }
}
