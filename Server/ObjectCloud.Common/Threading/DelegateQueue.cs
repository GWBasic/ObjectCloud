// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ObjectCloud.Common.Threading
{
    /// <summary>
    /// Runs all delegates in order asyncronously.  This is an alternative to using the Threadpool in the event that each delegate would block each other, thus
    /// causing contention on the ThreadPool
    /// </summary>
    public class DelegateQueue
    {
        /// <summary>
        /// Holds a delegate and its state in the queue
        /// </summary>
        private struct QueuedDelegate
        {
            public WaitCallback Callback;
            public object state;
        }

        int Running = 0;
        LockFreeQueue<QueuedDelegate> QueuedDelegates = new LockFreeQueue<QueuedDelegate>();

        /// <summary>
        /// Prints the text to the console.  Does not block.  All text is queued up to be printed
        /// </summary>
        /// <param name="task"></param>
        public void QueueUserWorkItem(WaitCallback callback)
        {
            QueueUserWorkItem(callback, null);
        }

        /// <summary>
        /// Prints the text to the console.  Does not block.  All text is queued up to be printed
        /// </summary>
        /// <param name="task"></param>
        public void QueueUserWorkItem(WaitCallback callback, object State)
        {
            QueuedDelegate queuedDelegate = new QueuedDelegate();
            queuedDelegate.Callback = callback;
            queuedDelegate.state = State;

            QueuedDelegates.Enqueue(queuedDelegate);

            if (0 == Running)
                if (0 == Interlocked.CompareExchange(ref Running, 1, 0))
                {
                    Thread thread = new Thread(Work);
                    thread.Start();
                }
        }

        /// <summary>
        /// Runs on the Thread to keep printing on the console
        /// </summary>
        void Work()
        {
            QueuedDelegate queuedDelegate;

            while (QueuedDelegates.Dequeue(out queuedDelegate))
                queuedDelegate.Callback(queuedDelegate.state);

            Running = 0;
        }
    }
}
