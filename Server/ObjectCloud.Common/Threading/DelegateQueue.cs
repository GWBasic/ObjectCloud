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
    /// causing contention on the ThreadPool.  Until the delegate queue is disposed, this object will always have a thread, which is suspended when not in use
    /// </summary>
    public class DelegateQueue : IDisposable
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
            QueuedDelegates.ItemAddedToEmptyQueue += new EventHandler<LockFreeQueue<QueuedDelegate>, EventArgs>(QueuedDelegates_ItemAddedToEmptyQueue);

            Thread = new Thread(Work);
            Thread.Name = name;
            Thread.Start();
        }

        void QueuedDelegates_ItemAddedToEmptyQueue(LockFreeQueue<DelegateQueue.QueuedDelegate> sender, EventArgs e)
        {
            lock (pulser)
                if (Suspended)
                    Monitor.Pulse(pulser);
        }

        LockFreeQueue<QueuedDelegate> QueuedDelegates = new LockFreeQueue<QueuedDelegate>();

        private bool KeepRunning = true;

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
            if (!KeepRunning)
                throw new ObjectDisposedException(Thread.Name);

            QueuedDelegate queuedDelegate = new QueuedDelegate();
            queuedDelegate.Callback = callback;
            queuedDelegate.state = State;

            QueuedDelegates.Enqueue(queuedDelegate);

            if (Suspended)
                lock (pulser)
                    if (Suspended)
                        Monitor.Pulse(pulser);
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
        /// This is used to communicate when the delegate queue is suspended
        /// </summary>
        private volatile bool Suspended;

        /// <summary>
        /// Runs on the Thread to keep printing on the console
        /// </summary>
        void Work()
        {
            while (KeepRunning)
            {
                Thread.IsBackground = true;

                lock (pulser)
                {
                    Suspended = true;
                    Monitor.Wait(pulser, 180000);
                    Suspended = false;
                }

                Thread.IsBackground = false;

                QueuedDelegate queuedDelegate;
                while (QueuedDelegates.Dequeue(out queuedDelegate))
                    queuedDelegate.Callback(queuedDelegate.state);
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            KeepRunning = false;

            lock (pulser)
                if (Suspended)
                    Monitor.Pulse(pulser);

            Thread.Join();
        }

        ~DelegateQueue()
        {
            KeepRunning = false;

            lock (pulser)
                if (Suspended)
                    Monitor.Pulse(pulser);

            Thread.Join();
        }
    }
}
