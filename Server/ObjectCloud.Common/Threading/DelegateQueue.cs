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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name">The name of the thread that handles delegates</param>
        public DelegateQueue(string name)
        {
            Name = name;
            QueuedDelegates.ItemAddedToEmptyQueue += new EventHandler<LockFreeQueue<QueuedDelegate>, EventArgs>(QueuedDelegates_ItemAddedToEmptyQueue);
        }

        void QueuedDelegates_ItemAddedToEmptyQueue(LockFreeQueue<DelegateQueue.QueuedDelegate> sender, EventArgs e)
        {
            lock (pulser)
            {
                if (null == Thread)
                {
                    Thread = new Thread(Work);
                    Thread.Name = Name;
                    Thread.Start();
                }
                else
                    Monitor.Pulse(pulser);
            }
        }

        LockFreeQueue<QueuedDelegate> QueuedDelegates = new LockFreeQueue<QueuedDelegate>();

        private string Name;

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
        }

        /// <summary>
        /// The thread that runs the delegates
        /// </summary>
        Thread Thread;

        /// <summary>
        /// Used to indicate new delegates when a thread is running
        /// </summary>
        private object pulser = new object();

        /// <summary>
        /// Runs on the Thread to keep printing on the console
        /// </summary>
        void Work()
        {
            while (true)
            {
                QueuedDelegate queuedDelegate;

                while (QueuedDelegates.Dequeue(out queuedDelegate))
                    queuedDelegate.Callback(queuedDelegate.state);

                lock (pulser)
                    if (!Monitor.Wait(pulser, 10000))
                    {
                        Thread = null;
                        return;
                    }
            }
        }
    }
}
