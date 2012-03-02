// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Threading;

namespace ObjectCloud.Common.Threading
{
	/// <summary>
	/// Provides an on-going single thread that can be used to queue items where their starting thread must never die
	/// </summary>
	public class InterrupThread : IDisposable
	{
		public InterrupThread(string name)
		{
            Thread = new Thread(RunThread);
            Thread.IsBackground = true;
            Thread.Name = name;
            Thread.Start();
		}
        
        /// <summary>
        /// The Thread
        /// </summary>
        private readonly Thread Thread;

        /// <summary>
        /// Indicates that the thread should exit
        /// </summary>
        private bool Running = true;

        /// <summary>
        /// Delegates to run
        /// </summary>
        private Queue<GenericVoid> RunQueue = new Queue<GenericVoid>();

        /// <summary>
        /// Signal to look at the queue
        /// </summary>
        private object Pulser = new object();

        /// <summary>
        /// Runs the thread that keeps alive
        /// </summary>
        private void RunThread()
        {
            while (Running)
            {
                GenericVoid toRun = null;

                using (TimedLock.Lock(RunQueue))
                    if (RunQueue.Count > 0)
                        toRun = RunQueue.Dequeue();

                if (null != toRun)
                    toRun();
                else
                    using (TimedLock.Lock(Pulser))
                        Monitor.Wait(Pulser);
            }
        }

        public void Dispose()
        {
            Running = false;

            while (RunQueue.Count > 0)
                Thread.Sleep(100);

            while (ThreadState.Running == Thread.ThreadState)
            {
                using (TimedLock.Lock(Pulser))
                    Monitor.Pulse(Pulser);
                
                Thread.Sleep(100);
            }
        }

        /// <summary>
        /// Queues the item to run
        /// </summary>
        /// <param name="toRun"></param>
        public void QueueItem(GenericVoid toRun)
        {
            if (!Running)
                throw new ObjectDisposedException(Thread.Name + " is already disposed!");

            using (TimedLock.Lock(RunQueue))
                RunQueue.Enqueue(toRun);

            using (TimedLock.Lock(Pulser))
                Monitor.Pulse(Pulser);
        }
    }
}
