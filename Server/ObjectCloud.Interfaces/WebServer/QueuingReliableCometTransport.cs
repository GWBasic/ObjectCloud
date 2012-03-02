// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.Common.Threading;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Interfaces.WebServer
{
	/// <summary>
	/// Implements a Queuing and Reliable (layer 3) Comet Protocol connection
	/// </summary>
	public class QueuingReliableCometTransport : ICometTransport, IQueuingReliableCometTransport
	{
		private static ILog log = LogManager.GetLogger<QueuingReliableCometTransport>();
		
		public QueuingReliableCometTransport(string url, ISession session)
		{ 
			_Session = session;
			_Url = url;
            _StartSend = new MulticastEventWithTimeout(this);
		}

		/// <value>
		/// The session
		/// </value>
        public ISession Session
		{
        	get { return _Session; }
        }
		private readonly ISession _Session;

		/// <value>
		/// The URL, this is for logging reasons only
		/// </value>
        public string Url 
		{
        	get { return _Url; }
        }
		private readonly string _Url;

		/// <summary>
		/// Occurs whenever data is recieved through the connection.  This is called on the threadpool, and
		/// a handler may block as needed
		/// </summary>
		/// <returns>
		/// A <see cref="System.Object"/>
		/// </returns>
		public event EventHandler<IQueuingReliableCometTransport, EventArgs<Packet>> DataRecieved;
		
		/// <summary>
		/// Represents a packet sent or recieved from a Comet Protocol Quality and Reliable connection
		/// </summary>
		/// <returns>
		/// A <see cref="System.Object"/>
		/// </returns>
		public struct Packet
		{
			//// <value>
			/// The actual data recieved.  This is decoded with the JsonFX.json decoder
			/// </value>
			public object Data 
			{
				get { return _Data;	}
			}
			internal object _Data;

			/// <value>
			/// The packet ID
			/// </value>
			public ulong PacketId 
			{
				get { return _PacketId; }
			}
			internal ulong _PacketId;
		}
		
		/// <value>
		/// The current state of the connection
		/// </value>
        public StateEnum State
		{
        	get { return _State; }
        }
		private StateEnum _State = StateEnum.Connected;
		
		/// <summary>
		/// The different states of connection
		/// </summary>
		/// <returns>
		/// A <see cref="System.Object"/>
		/// </returns>
		public enum StateEnum : int
		{
			/// <summary>
			/// The connection is connected
			/// </summary>
			Connected = 1,
			
			/// <summary>
			/// The connection is attempting to disconnect
			/// </summary>
			StartingToDisconnect = 3,
			
			/// <summary>
			/// An "end" has been sent to the client, waiting for the client to ack all packets and send its end
			/// </summary>
			Disconnecting_Waiting = 5,
			
			/// <summary>
			/// The connection is completely disconnected
			/// </summary>
			Disconnected = 0
		}
		
		public object GetDataToSend()
		{
            if ((StateEnum.Disconnected == State) || (null == _StartSend))
                return null;

			Dictionary<string, object> toReturn;

			using (TimedLock.Lock(UnackedSentPackets))
			{
                // If the connection isn't in the process of disconnecting and there isn't anything to send, don't send anything
                if ((StateEnum.Connected == State) && (0 == UnackedSentPackets.Count))
                    return null;

				toReturn = new Dictionary<string, object>();
				
				List<ulong> sortedPacketIds = new List<ulong>(UnackedSentPackets.Keys);
				sortedPacketIds.Sort();
			
				foreach (ulong packetId in sortedPacketIds)
					toReturn[packetId.ToString()] = UnackedSentPackets[packetId];
			}
			
			if (StateEnum.Connected != State)
			{
				toReturn["end"] = true;
				_State = StateEnum.Disconnecting_Waiting;
			}
			
			return toReturn;
		}
		
		/// <summary>
		/// Thrown when a connection can no longer send data
		/// </summary>
		/// <param name="toSend">
		/// A <see cref="System.Object"/>
		/// </param>
		/// <param name="maxDelay">
		/// A <see cref="TimeSpan"/>
		/// </param>
		public class DisconnectedException : WebServerException
		{
			public DisconnectedException() : base("The connection can no longer send data") {}
		}
		
		/// <summary>
		/// Sends the packet
		/// </summary>
		/// <param name="toSent">
		/// The object to be sent.  This must be compatible with JSON serialization from JsonFx.json
		/// </param>
		/// <exception cref="DisconnectedException">Thrown if the connection is disconnecting or disconnected</exception>
		public void Send(object toSend)
		{
			Send(toSend, TimeSpan.FromMilliseconds(50));
		}
		
		/// <summary>
		/// Sends the packet
		/// </summary>
		/// <param name="toSent">
		/// The object to be sent.  This must be compatible with JSON serialization from JsonFx.json
		/// </param>
		/// <param name="maxDelay">
		/// The maximum delay prior to sending the packet.  0 is reccomended if this is the only packet that will be sent, larger values are reccomended if other packets will be sent soon
		/// </param>
		/// <exception cref="DisconnectedException">Thrown if the connection is disconnecting or disconnected</exception>
		public void Send(object toSend, TimeSpan maxDelay)
		{
			if (StateEnum.Connected != State)
				throw new DisconnectedException();

            if (null == _StartSend)
                throw new ObjectDisposedException(_Url);

#if DEBUG
			try
			{
				// In debug mode, attempt serialization to that if a bonehead tries to send non-serializable data, it can't be sent
				JsonFx.Json.JsonWriter.Serialize(toSend);
			}
			catch (Exception e)
			{
				log.Error("An attempt was made to transmit non-serializable data through a Quality and Reliable (layer 3) Comet Protocol connection", e);
				throw;
			}
#endif
			
			using (TimedLock.Lock(UnackedSentPackets))
			{
				UnackedSentPackets[NextPacketId] = toSend;
				NextPacketId++;
			}
			
			_StartSend.Send(new EventArgs<TimeSpan>(maxDelay));
		}
		
		/// <summary>
		/// Closes the connection
		/// </summary>
		public void Close()
		{
			if (StateEnum.Connected == State)
			{
				_State = StateEnum.StartingToDisconnect;
				_StartSend.Send(new EventArgs<TimeSpan>(TimeSpan.Zero));
			}
		}
		
		/// <summary>
		/// All of the unacked packets that are ready to be sent
		/// </summary>
		/// <param name="incoming">
		/// A <see cref="System.Object"/>
		/// </param>
		private Dictionary<ulong, object> UnackedSentPackets = new Dictionary<ulong, object>();
		
		/// <summary>
		/// The next packet ID to use
		/// </summary>
		/// <param name="incoming">
		/// A <see cref="System.Object"/>
		/// </param>
		private ulong NextPacketId = 0;
		
		public void HandleIncomingData(object incoming)
		{
			// Ignore additional data if the connection ended
			if (StateEnum.Disconnected == State)
				return;
			
			// Trap malformed packets
			if (!(incoming is IDictionary<string, object>))
			{
				log.WarnFormat("Recieved a malformed packet when connected to {0} from {1}", Url, Session.SessionId);
				return;
			}
			
			IDictionary<string, object> packetQueue = (IDictionary<string, object>)incoming;
			
			// Inspect ack
			object ackIdObj = null;
			if (packetQueue.TryGetValue("a", out ackIdObj))
			{
				ulong ack = Convert.ToUInt64(ackIdObj);
				
				using (TimedLock.Lock(UnackedSentPackets))
					foreach (ulong unackedPacketId in new List<ulong>(UnackedSentPackets.Keys))
						if (unackedPacketId <= ack)
							UnackedSentPackets.Remove(unackedPacketId);
			}
			
			object recievedPacketsObj = null;
			if (packetQueue.TryGetValue("d", out recievedPacketsObj))
			{
				// Trap malformed packets
				if (!(recievedPacketsObj is Dictionary<string, object>))
				{
					log.WarnFormat("Recieved a malformed packet when connected to {0} from {1}", Url, Session.SessionId);
					return;
				}
				
				Dictionary<string, object> recievedPackets = (Dictionary<string, object>)recievedPacketsObj;
				
				using (TimedLock.Lock(BufferedPackets))
                    foreach (KeyValuePair<string, object> recievedPacket in recievedPackets)
                    {
                        try
                        {
                            ulong packetId = ulong.Parse(recievedPacket.Key);

                            if (packetId >= ExpectedPacketId)
                                BufferedPackets[packetId] = recievedPacket.Value;
                        }
                        catch (Exception e)
                        {
                            log.WarnFormat("Recieved a malformed packet when connected to {0} from {1}", e, Url, Session.SessionId);
                            return;
                        }
                    }
				
				// Process all of the recieved packets
				ThreadPool.QueueUserWorkItem(delegate(object state)
				{
					ProcessRecievedPackets();
				});
			}

            if (packetQueue.ContainsKey("end"))
                using (TimedLock.Lock(UnackedSentPackets))
                    switch (State)
                    {
                        case (StateEnum.Connected):
                            {
                                if (null != ConnectionDisconnecting)
                                    ConnectionDisconnecting(this, new EventArgs());

                                _State = StateEnum.StartingToDisconnect;

                                break;
                            }

                        case (StateEnum.Disconnecting_Waiting):
                            if (0 == UnackedSentPackets.Count)
                            {
                                _State = StateEnum.Disconnected;

                                // Dispose on the threadpool
                                Dispose();

                                return;
                            }
                            else
                                break;
                    }

		}
		
		/// <summary>
		/// Occurs when the connection ends successfully
		/// </summary>
		public event EventHandler<IQueuingReliableCometTransport, EventArgs> ConnectionEnded;
		
		/// <summary>
		/// Occurs when the client requests to disconnect the connection.  Does not occur when the server requests disconnection
		/// </summary>
		public event EventHandler<IQueuingReliableCometTransport, EventArgs> ConnectionDisconnecting;
		
		/// <value>
		/// The next packet ID that is expected
		/// </value>
		ulong ExpectedPacketId = 0;
		
		/// <value>
		/// All of the packets that were recieved but can not be processed because an earlier packet is missing
		/// </value>
		Dictionary<ulong, object> BufferedPackets = new Dictionary<ulong, object>();
		
		/// <summary>
		/// Makes sure that only one thread at a time is processing recieved packets; no packet should be processed until its prior is complete
		/// </summary>
		private object ProcessRecievedPacketsKey = new object();
		
		/// <summary>
		/// Processes recieved packets
		/// </summary>
		private void ProcessRecievedPackets()
		{
			using (TimedLock.Lock(ProcessRecievedPacketsKey))
				while (true)
				{
					Packet recieved;
					
					using (TimedLock.Lock(BufferedPackets))
					{
						object data = null;
						if (!BufferedPackets.TryGetValue(ExpectedPacketId, out data))
							return;
						
						recieved = new Packet();
						recieved._Data = data;
						recieved._PacketId = ExpectedPacketId;
						
						BufferedPackets.Remove(ExpectedPacketId);
						
						ExpectedPacketId++;
					}
					
					if (null != DataRecieved)
						try
						{
							DataRecieved(this, new EventArgs<Packet>(recieved));
						}
						catch (Exception e)
						{
							log.Error("Unhandled exception occured while processing a packet", e);
						}
				}
		}
		
        public MulticastEventWithTimeout<ICometTransport, EventArgs<TimeSpan>> StartSend
        {
            get 
            {
                if (null == _StartSend)
                    throw new ObjectDisposedException(_Url);

                return _StartSend; 
            }
        }

        /// <summary>
        /// When this is null it implies that the connection is disposed
        /// </summary>
        private MulticastEventWithTimeout _StartSend;

        /// <summary>
        /// Dispose cleans up resources allocated to the connection.  It does not properly close the connection.  Call Close() first!!!
        /// </summary>
        public void Dispose()
        {
            if (null != _StartSend)
            {
                _StartSend.Dispose();
                _StartSend = null;

                ThreadPool.QueueUserWorkItem(delegate(object state)
                {
                    if (null != ConnectionEnded)
                        ConnectionEnded(this, new EventArgs());
                });
            }
        }

        /// <summary>
        /// Returns true if the object is disposed
        /// </summary>
        public bool IsDisposed
        {
            get { return null == _StartSend; }
        }

        /// <summary>
        /// 
        /// </summary>
        private class MulticastEventWithTimeout : MulticastEventWithTimeout<ICometTransport, EventArgs<TimeSpan>>
        {
            /// <summary>
            /// 
            /// </summary>
            /// <param name="ct"></param>
            public MulticastEventWithTimeout(ICometTransport ct) : base(TimeSpan.FromSeconds(2.5), ct) { }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="eventArgs"></param>
            public void Send(EventArgs<TimeSpan> eventArgs)
            {
                TriggerEvent(eventArgs);
            }
        }

		/// <summary>
		/// The hash code
		/// </summary>
		/// <returns>
		/// A <see cref="System.Int32"/>
		/// </returns>
		private int Hash = SRandom.Next<int>();
		
		public override int GetHashCode ()
		{
			return Hash;
		}
		
		public override bool Equals (object obj)
		{
			return this == obj;
		}
		
		public override string ToString ()
		{
			return Session.User.Name + " connected to " + Url;
		}
	}
}
