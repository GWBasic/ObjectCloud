// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

// Inspired by this spinning-based reader-writer lock:
//  http://www.bluebytesoftware.com/blog/2009/01/30/ASinglewordReaderwriterSpinLock.aspx

namespace ObjectCloud.Common
{
    /// <summary>
    /// A reader-writer lock that doesn't block at all for reads, and uses traditional monitors for writes.  Spinning occurs while
    /// the writer waits for all readers to end, thus readers shouldn't be long-lived
    /// </summary>
    public class ReaderOrExclusiveLock
    {
        /// <summary>
        /// The number of readers and a bitwise flag that indicates that a write is requested
        /// </summary>
        long NumReadersAndWriteRequested = 0;

        /// <summary>
        /// The object that's locked using System.Monitor for write locking.
        /// </summary>
        object WriterKey = new object();

        /// <summary>
        /// Helps end a read lock by allowing the caller to use the "using" syntax
        /// </summary>
        private struct ReadLockCompleter : IDisposable
        {
            internal ReadLockCompleter(ReaderOrExclusiveLock me)
            {
                Me = me;
            }

            ReaderOrExclusiveLock Me;

            public void Dispose()
            {
                Interlocked.Decrement(ref Me.NumReadersAndWriteRequested);
            }
        }

        /// <summary>
        /// Establishes a non-blocking read lock, unless a writer is active.  Returns an object that must be disposed to end the read lock.  When holding a read lock, you shouldn't make blocking calls, like I/O, database, ect.
        /// </summary>
        /// <returns></returns>
        public IDisposable LockForQuickRead()
        {
            bool loop;

            do
            {
                long numReadersAndWriteRequested = NumReadersAndWriteRequested;

                if (numReadersAndWriteRequested > int.MaxValue)
                {
                    // This queues readers until the writer competes.  A tradeoff is that if writers are queued, they will compete with readers
                    lock (WriterKey) { }

                    // By looping and sleeping after a write lock completes, priority is given to queued writers
                    loop = true;
                    Thread.Sleep(1);
                }
                else if (numReadersAndWriteRequested == int.MaxValue)
                {
                    // Corner case where there are too many readers for the system to handle
                    loop = true;
                    Thread.Sleep(0);
                }
                else
                {
                    long newNumReadersAndWriteRequested = numReadersAndWriteRequested + 1;

                    loop = numReadersAndWriteRequested != Interlocked.CompareExchange(
                        ref NumReadersAndWriteRequested, newNumReadersAndWriteRequested, numReadersAndWriteRequested);
                }
            } while (loop);

            return new ReadLockCompleter(this);
        }

        /// <summary>
        /// The flag that is added to NumReadersAndWriteRequested when a writer has a lock and is waiting for all readers to complete
        /// </summary>
        const long WriteRequestedFlag = 2147483648;

        /// <summary>
        /// Helps end a write lock by allowing the caller to use the "using" syntax
        /// </summary>
        private struct WriteLockCompleter : IDisposable
        {
            internal WriteLockCompleter(ReaderOrExclusiveLock me)
            {
                Me = me;
            }

            ReaderOrExclusiveLock Me;

            public void Dispose()
            {
                // Remove the write requested flag
                long numReadersAndWriteRequested;
                long newNumReadersAndWriteRequested;

                do
                {
                    numReadersAndWriteRequested = Me.NumReadersAndWriteRequested;
                    newNumReadersAndWriteRequested = numReadersAndWriteRequested - WriteRequestedFlag;
                } while (numReadersAndWriteRequested != Interlocked.CompareExchange(
                    ref Me.NumReadersAndWriteRequested, newNumReadersAndWriteRequested, numReadersAndWriteRequested));

                Monitor.Exit(Me.WriterKey);
            }
        }

        /// <summary>
        /// Establishes an exclusive lock that can be used for writing or long-lived locks.  This will spin until all readers are complete
        /// </summary>
        /// <returns></returns>
        public IDisposable LockExclusive()
        {
            return LockExclusive(TimedLock.DefaultAquireLockTimeout);
        }

        /// <summary>
        /// Establishes an exclusive lock that can be used for writing or long-lived locks.  This will spin until all readers are complete
        /// </summary>
        /// <returns></returns>
        public IDisposable LockExclusive(TimeSpan timeout)
        {
            if (!Monitor.TryEnter(WriterKey, timeout))
                throw new TimeoutException("Timeout establishing a lock");

            long numReadersAndWriteRequested;
            long newNumReadersAndWriteRequested;

            do
            {
                numReadersAndWriteRequested = NumReadersAndWriteRequested;
                newNumReadersAndWriteRequested = numReadersAndWriteRequested + WriteRequestedFlag;
            } while (numReadersAndWriteRequested != Interlocked.CompareExchange(
                ref NumReadersAndWriteRequested, newNumReadersAndWriteRequested, numReadersAndWriteRequested));

            // Spin until there are no readers
            while (NumReadersAndWriteRequested > WriteRequestedFlag)
                Thread.Sleep(0);

            return new WriteLockCompleter(this);
        }
    }
}
