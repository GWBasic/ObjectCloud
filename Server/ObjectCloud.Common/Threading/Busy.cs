// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using Common.Logging;

namespace ObjectCloud.Common.Threading
{
    /// <summary>
    /// Helps to block while the server is busy and needs to throttle requests
    /// </summary>
    public static class Busy
    {
		private static ILog log = LogManager.GetLogger(typeof(Busy));
		
        /// <summary>
        /// The number of threads that indicate that the server is busy
        /// </summary>
        private static int BusyCount = 0;

        /// <summary>
        /// Set to true whenever the server is busy
        /// </summary>
        public static bool IsBusy
        {
            get { return BusyCount > 0; }
        }

        /// <summary>
        /// This is locked while the server is busy, leading to easy blocking of all threads
        /// </summary>
        private static object BusyKey = new object();

        /// <summary>
        /// Blocks while the server is under load, thus allowing important queued operations to complete
        /// </summary>
        public static void BlockWhileBusy(string message)
        {
            while (IsBusy)
            {
                Thread.Sleep(0);
				
				log.Warn("Blocking " + message + " while busy");
				
                lock (BusyKey) { }
            }
        }

        /// <summary>
        /// Syncronizes starting / stopping of busy. Not sure if it's needed
        /// </summary>
        private static object BeginEndKey = new object();

        /// <summary>
        /// Indicates that a thread needs other system operations halted
        /// </summary>
        public static void BeginBusy()
        {
            lock (BeginEndKey)
            {
                if (1 == Interlocked.Increment(ref BusyCount))
                    // start busy thread
                    lock (BusyThreadStartedPulser)
                    {
                        Thread thread = new Thread(BusyThread);
                        thread.Name = "Busy blocker";
                        //thread.Priority = ThreadPriority.Highest;
                        thread.Start();

                        Monitor.Wait(BusyThreadStartedPulser);
                    }
            }
        }

        /// <summary>
        /// Indicates that a thread no longer needs other system operations halted
        /// </summary>
        public static void ExitBusy()
        {
            lock (BeginEndKey)
            {
                if (0 == Interlocked.Decrement(ref BusyCount))
                    // If no more threads are busy, end the busy thread
                    lock (EndBusyThreadPulser)
                        Monitor.Pulse(EndBusyThreadPulser);
            }
        }

        /// <summary>
        /// Signal to indicate that the busy thread is started
        /// </summary>
        private static object BusyThreadStartedPulser = new object();

        // Signal to indicate that the busy thread should end
        private static object EndBusyThreadPulser = new object();

        /// <summary>
        /// This method runs the busy thread
        /// </summary>
        private static void BusyThread()
        {
            lock (EndBusyThreadPulser)
            {
                lock (BusyThreadStartedPulser)
                    Monitor.Pulse(BusyThreadStartedPulser);

                lock (BusyKey)
				{
					log.Warn("Server is busy");
                    Monitor.Wait(EndBusyThreadPulser);
					log.Warn("Server is no longer busy");
				}
            }
        }
    }
}
