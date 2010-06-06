// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using JmBucknall.Structures;

namespace ObjectCloud.Common
{
    /// <summary>
    /// A fast reader-writer lock that uses a lot of memory instead of spinning for syncronization
    /// </summary>
    public class FastReadWriteLock
    {
        /// <summary>
        /// Weak references to all threads that are holding IDs, indexed by their ID
        /// </summary>
        static WeakReference[] ThreadsHoldingIds = new WeakReference[Environment.ProcessorCount * 128];

        /// <summary>
        /// All of the free IDs
        /// </summary>
        static LockFreeQueue<int> FreeIds = new LockFreeQueue<int>();

        /// <summary>
        /// This is set to an object and locked while idle keys are being cleaned up
        /// </summary>
        static Thread CleaningUpKeys = null;

        /// <summary>
        /// The thread's numeric ID
        /// </summary>
        static public int ThreadId
        {
            get
            {
                if (null == _ThreadId)
                {
                    int threadId;
                    while (!FreeIds.Dequeue(out threadId))
                    {
                        // If there is a thread cleaning up the IDs, wait a bit in hopes that something will be in the queue, else reclaim old IDs
                        if (null != CleaningUpKeys)
                            Thread.Sleep(0);
                        else
                            if (null == Interlocked.CompareExchange<Thread>(ref CleaningUpKeys, Thread.CurrentThread, null))
                            {
                                // At this point, this is the only thread that can clean up keys

                                // Sanity check for ABA
                                if (FreeIds.Dequeue(out threadId))
                                    FreeIds.Enqueue(threadId);
                                else
                                {
                                    for (threadId = 0; threadId < ThreadsHoldingIds.Length; threadId++)
                                    {
                                        WeakReference wr = ThreadsHoldingIds[threadId];

                                        if (null == wr)
                                            FreeIds.Enqueue(threadId);
                                        else if (!wr.IsAlive)
                                        {
                                            FreeIds.Enqueue(threadId);
                                            ThreadsHoldingIds[threadId] = null;
                                        }
                                    }
                                }

                                CleaningUpKeys = null;
                            }
                    }

                    ThreadsHoldingIds[threadId] = new WeakReference(Thread.CurrentThread);
                    _ThreadId = threadId;
                }

                return _ThreadId.Value;
            }
        }
        [ThreadStatic]
        static int? _ThreadId = null;

        /// <summary>
        /// Re-entrant count of readers
        /// </summary>
        byte[] ReaderCount = new byte[Environment.ProcessorCount * 128];

        /// <summary>
        /// Set to true when there is a writer lock and the readers should be blocked
        /// </summary>
        bool WriterLockAquired = false;

        /// <summary>
        /// A key for writing
        /// </summary>
        object WriteKey = new object();

        /// <summary>
        /// Begins a read lock
        /// </summary>
        public void BeginRead()
        {
            /*Thread currentThread = Thread.CurrentThread;
            ThreadPriority oldPriority = currentThread.Priority;
            currentThread.Priority = ThreadPriority.Highest;

            try
            {*/
                Thread.MemoryBarrier();

                // Block while there is a writer
                while (WriterLockAquired)
                    lock (WriteKey)
                    { }

                Thread.MemoryBarrier();

                ReaderCount[ThreadId]++;

                //for (int ctr = 0; ctr < 10; ctr++)
                    if (WriterLockAquired)
                    {
                        ReaderCount[ThreadId]--;
                        BeginRead();
                    }

                Thread.MemoryBarrier();
            /*}
            finally
            {
                currentThread.Priority = oldPriority;
            }*/
        }

        /// <summary>
        /// Ends a read
        /// </summary>
        public void EndRead()
        {
            ReaderCount[ThreadId]--;
        }

        /// <summary>
        /// Helps end a read lock by allowing the caller to use the "using" syntax
        /// </summary>
        private struct ReadLockCompleter : IDisposable
        {
            internal ReadLockCompleter(FastReadWriteLock me)
            {
                Me = me;
            }

            FastReadWriteLock Me;

            public void Dispose()
            {
                Me.EndRead();
            }
        }

        /// <summary>
        /// Establishes a non-blocking read lock, blocking while any writer is active.  Returns an object that must be disposed to end the read lock.
        /// </summary>
        /// <returns></returns>
        public IDisposable Read()
        {
            BeginRead();
            return new ReadLockCompleter(this);
        }

        /// <summary>
        /// Starts a write lock
        /// </summary>
        public void BeginWrite()
        {
            BeginWrite(TimedLock.DefaultAquireLockTimeout);
        }

        /// <summary>
        /// Starts a write lock.  Works if reader locks are established
        /// </summary>
        public void BeginWrite(TimeSpan timeout)
        {
            if (!Monitor.TryEnter(WriteKey, timeout))
                throw new TimeoutException("Could not establish a lock");

            WriterLockAquired = true;

            Thread.MemoryBarrier();

            int threadId = ThreadId;

            // Wait for all readers to complete
            // All readers are checked 10 times to account for syncronization issues
            //for (int ctr = 0; ctr < 10; ctr++)
                for (int threadIdItr = 0; threadIdItr < ThreadsHoldingIds.Length; threadIdItr++)
                    if (threadId != threadIdItr) // (Allow re-entry into a writer lock when a thread has a reader lock)
                        while (ReaderCount[ThreadId] > 0)
                            Thread.Sleep(0);

            Thread.MemoryBarrier();
        }

        /// <summary>
        /// Ends a write lock
        /// </summary>
        public void EndWrite()
        {
            WriterLockAquired = false;
            Monitor.Exit(WriteKey);
        }

        /// <summary>
        /// Helps end a read lock by allowing the caller to use the "using" syntax
        /// </summary>
        private struct WriteLockCompleter : IDisposable
        {
            internal WriteLockCompleter(FastReadWriteLock me)
            {
                Me = me;
            }

            FastReadWriteLock Me;

            public void Dispose()
            {
                Me.EndWrite();
            }
        }

        /// <summary>
        /// Establishes a blocking write lock, blocking until all readers are complete.  Returns an object that must be disposed to end the write lock.
        /// </summary>
        /// <returns></returns>
        public IDisposable Write()
        {
            BeginWrite();
            return new WriteLockCompleter(this);
        }
    }
}
