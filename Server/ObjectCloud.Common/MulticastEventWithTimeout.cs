// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using Common.Logging;

namespace ObjectCloud.Common
{
    /// <summary>
    /// Provides multicase event functionality with a timeout so that handlers can gracefully de-register themselves
    /// </summary>
    /// <typeparam name="TSender"></typeparam>
    /// <typeparam name="TEventArgs"></typeparam>
    /// <typeparam name="T"></typeparam>
    public abstract class MulticastEventWithTimeout<TSender, TEventArgs> : IDisposable
        where TEventArgs : EventArgs
    {
        private static ILog log = LogManager.GetLogger(typeof(MulticastEventWithTimeout<,>));

        /// <summary>
        /// Creates the event with timeout
        /// </summary>
        /// <param name="inactivityAccuracy">How often the listeners will be checked to see if they are expired.</param>
        public MulticastEventWithTimeout(TimeSpan inactivityAccuracy, TSender sender)
        {
            Sender = sender;
            Timer = new Timer(DoCleanup, null, inactivityAccuracy, inactivityAccuracy);
        }

        /// <summary>
        /// Listener used for multicase events with a timeout
        /// </summary>
        public struct Listener
        {
            public Listener(TimeSpan timeout, EventHandler<TSender, TEventArgs> handler, GenericArgument<TSender> timeoutHandler)
            {
                if (timeout > TimeSpan.FromDays(90000))
                    ExpireDateTime = DateTime.MaxValue;
                else
                    ExpireDateTime = DateTime.UtcNow + timeout;
                
                Handler = handler;
                TimeoutHandler = timeoutHandler;
                Hash = SRandom.Next<int>();
            }

            public DateTime ExpireDateTime;
            public EventHandler<TSender, TEventArgs> Handler;
            public GenericArgument<TSender> TimeoutHandler;

            /// <summary>
            /// GetHashCode() doesn't garantee uniqueness; this is causing issues on mono
            /// </summary>
            public int? Hash;

            public override int GetHashCode()
            {
                if (null == Hash)
                    Hash = SRandom.Next<int>();
				
                return Hash.Value;
            }
			
			public override bool Equals (object obj)
			{
				if (!(obj is Listener))
					return false;
				
				Listener other = (Listener)obj;
				
				bool toReturn = TimeoutHandler.Equals(other.TimeoutHandler) && Handler.Equals(other.Handler);
				return toReturn;
			}
		}

        /// <summary>
        /// The timer
        /// </summary>
        private Timer Timer;

        /// <summary>
        /// All of the listeners
        /// </summary>
        private Set<Listener> Listeners = new Set<Listener>();

        /// <summary>
        /// The sender
        /// </summary>
        private readonly TSender Sender;

        /// <summary>
        /// Periodically cleans up expired listeners
        /// </summary>
        /// <param name="state"></param>
        private void DoCleanup(object state)
        {
            if (null == Listeners)
                return;

            using (TimedLock.Lock(Listeners))
            {
                if (null == Listeners)
                    return;

                foreach (Listener listener in new List<Listener>(Listeners))
                    if (listener.ExpireDateTime <= DateTime.UtcNow)
                    {
                        listener.TimeoutHandler(Sender);
                        Listeners.Remove(listener);
                    }
            }
        }

        /// <summary>
        /// Signals that the event is no longer in use.  Stops the timer and releases all of the listeners
        /// </summary>
        public void Dispose()
        {
            if (null != Listeners)
            {
                Timer.Dispose();

                using (TimedLock.Lock(Listeners))
                    Listeners = null;
            }
        }

        /// <summary>
        /// Calls all of the listeners
        /// </summary>
        /// <param name="eventArgs"></param>
        protected void TriggerEvent(TEventArgs eventArgs)
        {
            if (null == Listeners)
                throw new ObjectDisposedException(Sender.ToString(), "Multicast event with timeout was disposed");

            using (TimedLock.Lock(Listeners))
            {
                if (null == Listeners)
                    throw new ObjectDisposedException(Sender.ToString(), "Multicast event with timeout was disposed");

                foreach (Listener listener in new List<Listener>(Listeners))
                    if (listener.ExpireDateTime > DateTime.UtcNow)
                        listener.Handler(Sender, eventArgs);
                    else
                    {
                        listener.TimeoutHandler(Sender);
                        Listeners.Remove(listener);
                    }
            }
        }

        /// <summary>
        /// Adds the listener
        /// </summary>
        /// <param name="listener"></param>
        public void AddListener(Listener listener)
        {
            using (TimedLock.Lock(Listeners))
                Listeners.Add(listener);
        }

        /// <summary>
        /// Removes the listener
        /// </summary>
        /// <param name="listener"></param>
        public bool RemoveListener(Listener listener)
        {
            using (TimedLock.Lock(Listeners))
                if (!Listeners.Remove(listener))
                {
                    log.Warn("An attempt was made to remove a listener that isn't registered, total listeners: " + Listeners.Count.ToString());
                    return false;
                }

            return true;
        }
    }
}
