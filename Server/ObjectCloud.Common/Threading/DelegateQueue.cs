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
    /// Runs all delegates in order asyncronously.  This is an alternative to using the Threadpool in the event that each delegate would block each other, thus
    /// causing contention on the ThreadPool.  Until the delegate queue is disposed, this object will always have a thread, which is suspended when not in use
    /// </summary>
    public class DelegateQueue : IDisposable
    {
        private ILog log = LogManager.GetLogger<DelegateQueue>();

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
            NumThreads = numThreads;
            QueuedDelegates.ItemAddedToEmptyQueue += new EventHandler<LockFreeQueue<QueuedDelegate>, EventArgs>(QueuedDelegates_ItemAddedToEmptyQueue);

            Start();
        }

        private void Start()
        {
            // Compare-exchange used in case multiple threads try to start the sub-threads
            if (null == Interlocked.CompareExchange<Thread[]>(ref Threads, new Thread[NumThreads], null))
            {
                for (int ctr = 0; ctr < NumThreads; ctr++)
                {
                    KeepRunning = new Wrapped<bool>(true);

                    Thread thread = new Thread(Work);

                    if (NumThreads == 1)
                        thread.Name = Name;
                    else
                        thread.Name = Name + ' ' + ctr.ToString();

                    thread.Start();

                    Threads[ctr] = thread;
                }

                GC.ReRegisterForFinalize(this);
            }
        }

        void QueuedDelegates_ItemAddedToEmptyQueue(LockFreeQueue<DelegateQueue.QueuedDelegate> sender, EventArgs e)
        {
            lock (pulser)
                if (NumSuspendedThreads > 0)
                    Monitor.Pulse(pulser);
        }

        LockFreeQueue_WithCount<QueuedDelegate> QueuedDelegates = new LockFreeQueue_WithCount<QueuedDelegate>();

        /// <summary>
        /// This is wrapped so that, if the delegate queue is stopped, old threads can see false but new threads see a different value
        /// </summary>
        private Wrapped<bool> KeepRunning;

        private string Name;

        private int NumThreads;

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
            if (null == Threads)
                Start();

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
        Thread[] Threads = null;

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
            // Local instances used in case the delegate queue is stopped
            Wrapped<bool> keepRunning = KeepRunning;
            LockFreeQueue_WithCount<QueuedDelegate> queuedDelegates = QueuedDelegates;

            while (keepRunning.Value)
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
                while (queuedDelegates.Dequeue(out queuedDelegate))
                    try
                    {
                        queuedDelegate.Callback(queuedDelegate.state);
                    }
                    catch (Exception e)
                    {
                        log.Error("Unhandled exception in queued delegate", e);
                    }

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

        /// <summary>
        /// Stops the delegate queue.  All queued delegates are run prior to this function returning.  This function is thread-safe.  Note that if delegates are queued after Stop is called, the DelegateQueue will restart its threads.  It's possible to re-start new threads prior to old delegates completing.
        /// </summary>
        public void Stop()
        {
            // Compare-exchange used in case multiple threads try to stop
            Thread[] threads = Threads;
            if (null != threads)
                if (threads == Interlocked.CompareExchange<Thread[]>(ref Threads, null, threads))
                {
                    KeepRunning.Value = false;

                    lock (pulser)
                        Monitor.PulseAll(pulser);

                    foreach (Thread thread in threads)
                        thread.Join();

                    GC.SuppressFinalize(this);
                }
        }

        /// <summary>
        /// Stops the delegate queue.  All queued delegates are run prior to this function returning.  This function is thread-safe.  Note that if delegates are queued after Stop is called, the DelegateQueue will restart its threads.  It's possible to re-start new threads prior to old delegates completing.
        /// </summary>
        public void Dispose()
        {
            Stop();
        }

        ~DelegateQueue()
        {
            try
            {
                Stop();
            }
            catch { }
        }

        /// <summary>
        /// Cancels all of the queued delegates, except for ones that are currently running
        /// </summary>
        public void Cancel()
        {
            // The queue is cleared because threads hold a local reference
            QueuedDelegate queuedDelegate;
            while (QueuedDelegates.Dequeue(out queuedDelegate))
            {}
        }
    }
}
