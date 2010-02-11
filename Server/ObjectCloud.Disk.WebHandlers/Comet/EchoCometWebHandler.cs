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
    public class EchoCometWebHandler : WebHandler<IFileHandler>
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
            return new EchoCometTransport();
        }

        /// <summary>
        /// Comet transport that provides loopback functionality
        /// </summary>
        private class EchoCometTransport : ICometTransport
        {
            /// <summary>
            /// 
            /// </summary>
            public EchoCometTransport()
            {
                _StartSend = new MulticastEventWithTimeout(this);
            }

            /// <summary>
            /// This is the results that are sent every 10 seconds
            /// </summary>
            object ToSend = null;

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
                ToSend = incoming;
                _StartSend.Send(new EventArgs<TimeSpan>(TimeSpan.Zero));
            }

        public MulticastEventWithTimeout<ICometTransport, EventArgs<TimeSpan>> StartSend
            {
                get { return _StartSend; }
            }
            private MulticastEventWithTimeout _StartSend;

            public void Dispose()
            {
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