// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ObjectCloud.Common
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

        volatile bool Running = false;
        object Key = new object();
        Queue<QueuedDelegate> QueuedDelegates = new Queue<QueuedDelegate>();

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

            using (TimedLock.Lock(Key))
            {
                QueuedDelegates.Enqueue(queuedDelegate);

                if (!Running)
                {
                    Running = true;

                    Thread thread = new Thread(Work);
                    thread.Start();
                }
            }
        }

        /// <summary>
        /// Runs on the Thread to keep printing on the console
        /// </summary>
        void Work()
        {
            bool keepRunning = true;

            do
            {
                QueuedDelegate queuedDelegate;

                using (TimedLock.Lock(Key))
                    queuedDelegate = QueuedDelegates.Dequeue();

                queuedDelegate.Callback(queuedDelegate.state);

                // If the queue was emptied, then end the loop
                using (TimedLock.Lock(Key))
                    if (QueuedDelegates.Count <= 0)
                    {
                        keepRunning = false;
                        Running = false;
                    }

            } while (keepRunning);
        }
    }
}
