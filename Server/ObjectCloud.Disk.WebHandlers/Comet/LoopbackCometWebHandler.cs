// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Threading;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.WebHandlers.Comet
{
    /// <summary>
    /// Web handler that performs loopback comet requests
    /// </summary>
    public class LoopbackCometWebHandler : WebHandler<IFileHandler>
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="session"></param>
        /// <param name="getArguments"></param>
        /// <param name="transportId"></param>
        /// <returns></returns>
        public override ICometTransport ConstructCometTransport(ISession session, IDictionary<string, string> getArguments, long transportId)
        {
            return new LoopbackCometTransport();
        }

        /// <summary>
        /// Comet transport that provides loopback functionality
        /// </summary>
        private class LoopbackCometTransport : ICometTransport
        {
            Timer Timer;

            /// <summary>
            /// 
            /// </summary>
            public LoopbackCometTransport()
            {
                Timer = new Timer(OnTimer, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
                _StartSend = new MulticastEventWithTimeout(this);

                OnTimer(null);
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="state"></param>
            private void OnTimer(object state)
            {
                if (null == _StartSend)
                    return;

                using (TimedLock.Lock(ToSendKey))
                {
                    ToSend = new Dictionary<string, object>();
                    ToSend["ts"] = DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString();
                    ToSend["d"] = MostRecentData;
                }

                try
                {
                    _StartSend.Send(new EventArgs<TimeSpan>(TimeSpan.Zero));
                }
                catch
                {
                    Dispose();
                }
            }

            /// <summary>
            /// The most recent data sent from the client
            /// </summary>
            object MostRecentData = null;

            /// <summary>
            /// This is the results that are sent every 10 seconds
            /// </summary>
            Dictionary<string, object> ToSend = null;

            object ToSendKey = new object();

            /// <summary>
            /// 
            /// </summary>
            /// <returns></returns>
            public object GetDataToSend()
            {
                object toReturn;

                using (TimedLock.Lock(ToSendKey))
                {
                    toReturn = ToSend;
                    ToSend = null;
                }

                return toReturn;
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="incoming"></param>
            public void HandleIncomingData(object incoming)
            {
                MostRecentData = incoming;
                _StartSend.Send(new EventArgs<TimeSpan>(TimeSpan.Zero));
            }

            /// <summary>
            /// 
            /// </summary>
	        public MulticastEventWithTimeout<ICometTransport, EventArgs<TimeSpan>> StartSend
            {
                get { return _StartSend; }
            }
            private MulticastEventWithTimeout _StartSend;

            /// <summary>
            /// 
            /// </summary>
            public void Dispose()
            {
                if (null != _StartSend)
                {
                    StartSend.Dispose();
                    Timer.Dispose();
                    _StartSend = null;
                }
            }
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
    }
}
