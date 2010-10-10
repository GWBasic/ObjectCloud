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
using ObjectCloud.Common.Threading;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Javascript;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Interfaces.WebServer
{
	/// <summary>
	/// Object that helps a WebHandler handle multiple QueuingReliableChannel layer 3 Comet Protocol connections
	/// </summary>
	public class ChannelEventWebAdaptor : IChannelEventWebAdaptor
	{
		public static ILog log = LogManager.GetLogger<ChannelEventWebAdaptor>();
		
		public ChannelEventWebAdaptor()
		{
		}
		
		/// <summary>
		/// Occurs whenever a client connects
		/// </summary>
		public event EventHandler<IChannelEventWebAdaptor, EventArgs<IQueuingReliableCometTransport>> ClientConnected;
		
		/// <summary>
		/// Occurs whenever a client disconnects
		/// </summary>
		public event EventHandler<IChannelEventWebAdaptor, EventArgs<IQueuingReliableCometTransport>> ClientDisconnected;
		
		/// <summary>
		/// Encapsulates recieved data and its source information
		/// </summary>
		public class DataReceivedEventArgs : EventArgs
		{
			/// <value>
			/// The data sent by the client, as de-serialized by JsonFX.json
			/// </value>
			public object Data
			{
				get { return _Data; }
			}
			internal object _Data;

			/// <value>
			/// The transport that sent the data
			/// </value>
			public IQueuingReliableCometTransport Transport
			{
				get { return _Transport; }
			}
			internal IQueuingReliableCometTransport _Transport;
			
			/// <value>
			/// The user that sent the data
			/// </value>
			public IUser User
			{
				get { return _Transport.Session.User; }
			}
		}
		
		/// <summary>
		/// Occurs whenever data is recieved from any connection
		/// </summary>
		/// <param name="toSend">
		/// A <see cref="System.Object"/>
		/// </param>
		public event EventHandler<IChannelEventWebAdaptor, DataReceivedEventArgs> DataReceived;
		
		/// <summary>
		/// All of the active channels
		/// </summary>
		/// <param name="toSend">
		/// A <see cref="System.Object"/>
		/// </param>
		private HashSet<IQueuingReliableCometTransport> ChannelsInt = new HashSet<IQueuingReliableCometTransport>();

        /// <summary>
        /// Allows iteration over each connected channel, thus each channel can be sent specific targetted data
        /// </summary>
        public IEnumerable<IQueuingReliableCometTransport> Channels
        {
            get 
            {
                foreach (IQueuingReliableCometTransport channel in ChannelsInt)
                    yield return channel;
            }
        }

        /// <summary>
        /// Returns all of the connected sessions, including dupes if the user has multiple tabs / windows open
        /// </summary>
        public IEnumerable<ISession> ConnectedSessions
        {
            get
            {
                foreach (IQueuingReliableCometTransport channel in ChannelsInt)
                    yield return channel.Session;
            }
        }
		
		/// <summary>
		/// Sends data to all connection
		/// </summary>
		/// <param name="toSend">
		/// An object that must serialize to valid JSON through JsonFX.json
		/// </param>
		public void SendAll(object toSend)
		{
			SendAll_ThreadPoolHelper helper = new SendAll_ThreadPoolHelper();
			helper.ToSend = toSend;
			
			using (TimedLock.Lock(ChannelsInt))
				foreach (IQueuingReliableCometTransport qrct in ChannelsInt)
					ThreadPool.QueueUserWorkItem(helper.SendOnThreadPool, qrct);
		}
		
		/// <summary>
		/// Assists with SendAll
		/// </summary>
		/// <param name="channel">
		/// A <see cref="IQueuingReliableCometTransport"/>
		/// </param>
		private struct SendAll_ThreadPoolHelper
		{
			internal object ToSend;
			
			internal void SendOnThreadPool(object queuingReliableCometTransportObj)
			{
				try
				{
					IQueuingReliableCometTransport qrct = (IQueuingReliableCometTransport)queuingReliableCometTransportObj;
					
					if (QueuingReliableCometTransport.StateEnum.Connected == qrct.State)
						qrct.Send(ToSend);
				}
				catch (Exception e)
				{
					log.Warn("Exception when sending data through Comet", e);
				}
			}
		}
		
		/// <summary>
		/// Adds a QueuingReliableCometTransport
		/// </summary>
		/// <param name="channel">
		/// A <see cref="IQueuingReliableCometTransport"/>
		/// </param>
		public void AddChannel(IQueuingReliableCometTransport channel)
		{
			using (TimedLock.Lock(ChannelsInt))
			{
				ChannelsInt.Add(channel);
				
				channel.DataRecieved += new EventHandler<IQueuingReliableCometTransport, EventArgs<QueuingReliableCometTransport.Packet>>(Channel_DataRecieved);
				channel.ConnectionDisconnecting += new EventHandler<IQueuingReliableCometTransport, EventArgs>(Channel_ConnectionEnded);
				channel.ConnectionEnded += new EventHandler<IQueuingReliableCometTransport, EventArgs>(Channel_ConnectionEnded);
			}
			
			if (null != ClientConnected)
				ClientConnected(this, new EventArgs<IQueuingReliableCometTransport>(channel));
		}
		
		private void Channel_DataRecieved(IQueuingReliableCometTransport sender, EventArgs<QueuingReliableCometTransport.Packet> e)
		{
			DataReceivedEventArgs drea = new DataReceivedEventArgs();
			drea._Data = e.Value._Data;
			drea._Transport = sender;
			
			if (null != DataReceived)
				DataReceived(this, drea);
		}
		
		private void Channel_ConnectionEnded(IQueuingReliableCometTransport sender, EventArgs e)
		{
			bool triggerEvent = false;
			
			using (TimedLock.Lock(ChannelsInt))
				if (ChannelsInt.Contains(sender))
				{
					triggerEvent = true;
					ChannelsInt.Remove(sender);
				}
			
			if ((null != ClientDisconnected) && triggerEvent)
				ClientDisconnected(this, new EventArgs<IQueuingReliableCometTransport>(sender));
		}
    }
}
