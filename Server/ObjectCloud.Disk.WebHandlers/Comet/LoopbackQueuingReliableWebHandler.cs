// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Threading;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.WebHandlers.Comet
{
    /// <summary>
    /// Simple test of the Quality and Reliable (layer 3) Comet Protocol
    /// </summary>
	public class LoopbackQueuingReliableWebHandler : WebHandler<IFileHandler>
	{
        /// <summary>
        /// 
        /// </summary>
		public LoopbackQueuingReliableWebHandler()
		{
		}

		/// <summary>
        /// 
        /// </summary>
        /// <param name="session"></param>
        /// <param name="getArguments"></param>
        /// <param name="transportId"></param>
        /// <returns></returns>
        public override ICometTransport ConstructCometTransport(ISession session, IDictionary<string, string> getArguments, long transportId)
        {
            QueuingReliableCometTransport toReturn = new QueuingReliableCometTransport(FileContainer.FullPath, session);
			
			ConnectionHandler ch = new ConnectionHandler();
			ch.QueuingReliableCometTransport = toReturn;
			
			toReturn.DataRecieved += new EventHandler<IQueuingReliableCometTransport, EventArgs<QueuingReliableCometTransport.Packet>>(ch.OnDataRecieved);

            Thread thread = new Thread(ch.HandleConnection);
            thread.IsBackground = true;
            thread.Name = "Queuing and Reliable test from: " + session.User.Name;
            thread.Start();
			
			return toReturn;
        }
		
		private class ConnectionHandler
		{
            private static ILog log = LogManager.GetLogger<ConnectionHandler>();

			public QueuingReliableCometTransport QueuingReliableCometTransport;
			
			List<object> Recieved = new List<object>();

            public void HandleConnection()
            {
                try
                {
                    for (int ctr = 0; ctr < 20; ctr++)
                    {
                        Dictionary<string, object> toSend = new Dictionary<string, object>();

                        toSend["time"] = DateTime.UtcNow.ToLongTimeString();
                        toSend["data"] = Recieved.ToArray();

                        using (TimedLock.Lock(QueuingReliableCometTransport))
                        {
                            // If the connection disconnects, return
                            if (QueuingReliableCometTransport.StateEnum.Connected != QueuingReliableCometTransport.State)
                                return;

                            QueuingReliableCometTransport.Send(toSend, TimeSpan.Zero);
                        }

                        Thread.Sleep(2500);
                    }

                    QueuingReliableCometTransport.Close();
                }
                /*catch (QualityReliableCometTransport.DisconnectedException)
                {
                    log.Info("Test connection disconnected prematurely.  This indicates that the client successfully disconnected the connection");
                }*/
                catch (Exception e)
                {
                    log.Error("Error when testing the Queueing and Reliable Comet Protocol", e);
                }
                finally
                {
                    QueuingReliableCometTransport.DataRecieved -= new EventHandler<IQueuingReliableCometTransport, EventArgs<QueuingReliableCometTransport.Packet>>(OnDataRecieved);
                }
            }

			public void OnDataRecieved(IQueuingReliableCometTransport qrct, EventArgs<QueuingReliableCometTransport.Packet> e)
			{
				Recieved.Add(e.Value.Data);
			}
		}
	}
}
