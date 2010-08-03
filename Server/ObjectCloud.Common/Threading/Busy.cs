// Copyright 2009, 2010 Andrew Rondeau
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
        /// Join this thread whenever the server is busy as a means of blocking while other threads "catch up"
        /// </summary>
        private static Thread BlockThread = null;

        /// <summary>
        /// This is locked while the server is busy, leading to easy blocking of all threads
        /// </summary>
        private static object BusyKey = new object();

        /// <summary>
        /// Blocks while the server is under load, thus allowing important queued operations to complete
        /// </summary>
        public static void BlockWhileBusy()
        {
            while (IsBusy)
            {
                Thread.Sleep(0);
                lock (BusyKey) { }
            }
        }

        /// <summary>
        /// Indicates that a thread needs other system operations halted
        /// </summary>
        public static void BeginBusy()
        {
            if (1 == Interlocked.Increment(ref BusyCount))
            {
                // Spin in the event that any other threads are ending the busy thread
                while (null != BlockThread)
                    Thread.Sleep(0);

                // start busy thread
                lock (BusyThreadStartedPulser)
                {
                    Thread thread = new Thread(BusyThread);
                    thread.Name = "Busy blocker";
                    thread.Priority = ThreadPriority.Highest;
                    thread.Start();

                    BlockThread = thread;

                    Monitor.Wait(BusyThreadStartedPulser);
                }
            }
        }

        /// <summary>
        /// Indicates that a thread no longer needs other system operations halted
        /// </summary>
        public static void ExitBusy()
        {
            if (0 == Interlocked.Decrement(ref BusyCount))
            {
                lock (EndBusyThreadPulser)
                {
                    Monitor.Pulse(EndBusyThreadPulser);
                    BlockThread = null;
                }
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
                    Monitor.Wait(EndBusyThreadPulser);
            }
        }
    }
}
