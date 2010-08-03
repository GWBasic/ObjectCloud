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
        public DelegateQueue(string name) : this(name, 1) {}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name">The name of the thread that handles delegates</param>
        public DelegateQueue(string name, int numThreads)
        {
            Name = name;
            QueuedDelegates.ItemAddedToEmptyQueue += new EventHandler<LockFreeQueue<QueuedDelegate>, EventArgs>(QueuedDelegates_ItemAddedToEmptyQueue);

            Threads = new Thread[numThreads];

            for (int ctr = 0; ctr < numThreads; ctr++)
            {
                Thread thread = new Thread(Work);

                if (numThreads == 1)
                    thread.Name = name;
                else
                    thread.Name = name + ' ' + ctr.ToString();

                thread.Start();

                Threads[ctr] = thread;
            }
        }

        void QueuedDelegates_ItemAddedToEmptyQueue(LockFreeQueue<DelegateQueue.QueuedDelegate> sender, EventArgs e)
        {
            lock (pulser)
                if (NumSuspendedThreads > 0)
                    Monitor.Pulse(pulser);
        }

        LockFreeQueue_WithCount<QueuedDelegate> QueuedDelegates = new LockFreeQueue_WithCount<QueuedDelegate>();

        private bool KeepRunning = true;

        private string Name;

        /// <summary>
        /// If there are more queued delegates then this threshold, the server will be marked as busy and requests throttled
        /// </summary>
        public int BusyThreshold
        {
            get { return _BusyThreshold; }
            set { _BusyThreshold = value; }
        }
        private int _BusyThreshold = 1000;

        /// <summary>
        /// Set to 1 if BeginBusy was ever called
        /// </summary>
        private int BeganBusy = 0;

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
                throw new ObjectDisposedException(Name);

            QueuedDelegate queuedDelegate = new QueuedDelegate();
            queuedDelegate.Callback = callback;
            queuedDelegate.state = State;

            QueuedDelegates.Enqueue(queuedDelegate);

            if (NumSuspendedThreads > 0)
                lock (pulser)
                    if (NumSuspendedThreads > 0)
                        Monitor.Pulse(pulser);

            if (QueuedDelegates.Count > BusyThreshold)
                if (0 == Interlocked.CompareExchange(ref BeganBusy, 1, 0))
                {
                    Busy.BeginBusy();

                    foreach (Thread thread in Threads)
                        thread.Priority = ThreadPriority.Highest;
                }
        }

        /// <summary>
        /// The threads that run the delegates
        /// </summary>
        Thread[] Threads;

        /// <summary>
        /// Used to indicate new delegates when a thread is running
        /// </summary>
        private object pulser = new object();

        /// <summary>
        /// This is used to communicate when the delegate queue is suspended
        /// </summary>
        private int NumSuspendedThreads = 0;

        /// <summary>
        /// Runs on the Thread to keep printing on the console
        /// </summary>
        void Work()
        {
            //Thread thread = Thread.CurrentThread;

            while (KeepRunning)
            {
                //thread.IsBackground = true;

                // Wait until a new request comes in
                // There's an automatic free to ensure that a request isn't left unfulfilled
                // It's a random time period for the case when there's many threads handling the queue
                lock (pulser)
                {
                    Interlocked.Increment(ref NumSuspendedThreads);
                    Monitor.Wait(pulser, SRandom.Next(150000, 200000));
                    Interlocked.Decrement(ref NumSuspendedThreads);
                }

                //thread.IsBackground = false;

                QueuedDelegate queuedDelegate;
                while (QueuedDelegates.Dequeue(out queuedDelegate))
                    queuedDelegate.Callback(queuedDelegate.state);

                // If throttling requests was started, end throttling requests
                if (BeganBusy > 0)
                    if (1 == Interlocked.CompareExchange(ref BeganBusy, 0, 1))
                    {
                        Busy.ExitBusy();

                        foreach (Thread thread in Threads)
                            thread.Priority = ThreadPriority.Normal;
                    }
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            KeepRunning = false;

            lock (pulser)
                if (NumSuspendedThreads > 0)
                    Monitor.PulseAll(pulser);

            foreach (Thread thread in Threads)
                thread.Join();
        }

        ~DelegateQueue()
        {
            KeepRunning = false;

            lock (pulser)
                if (NumSuspendedThreads > 0)
                    Monitor.PulseAll(pulser);

            foreach (Thread thread in Threads)
                thread.Join();
        }

        /// <summary>
        /// Cancels all of the queued delegates, except for ones that are currently running
        /// </summary>
        public void Cancel()
        {
            QueuedDelegates = new LockFreeQueue_WithCount<QueuedDelegate>();
        }
    }
}
