// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using ObjectCloud.Common;

namespace TimeLocks
{
    class Program
    {
        static void Main(string[] args)
        {
            using ((new FastReadWriteLock()).Read())
            { }

            FastReadWriteLock FastReadWriteLock = new FastReadWriteLock();

            for (int ctr = 0; ctr < 300; ctr++)
            {
                FastReadWriteLock.BeginRead();
                FastReadWriteLock.EndRead();
            }

            long totalTicks = 0;
            for (int ctr = 0; ctr < 10000; ctr++)
            {
                DateTime start = DateTime.UtcNow;
                FastReadWriteLock.BeginRead();
                TimeSpan startTime = DateTime.UtcNow - start;
                FastReadWriteLock.EndRead();

                totalTicks += startTime.Ticks;
            }

            Console.WriteLine("Average ticks for entering a FastReadWriteLock: {0}", Convert.ToDouble(totalTicks) / 10000);

            totalTicks = 0;
            object toMonitor = new object();
            for (int ctr = 0; ctr < 10000; ctr++)
            {
                DateTime start = DateTime.UtcNow;
                Monitor.Enter(toMonitor);
                TimeSpan startTime = DateTime.UtcNow - start;
                Monitor.Exit(toMonitor);

                totalTicks += startTime.Ticks;
            }

            Console.WriteLine("Average ticks for entering a Monitor: {0}", Convert.ToDouble(totalTicks) / 10000);

            Tester noSyncronization = new Tester(new NoSyncronization());
            Tester monitorSyncronization = new Tester(new MonitorSyncronization());
            Tester timedLockSyncronization = new Tester(new TimedLockSynchronization());
            Tester readerOrExclusiveSyncronization = new Tester(new ReaderOrExclusiveSynchronization());
            Tester weakSyncronization = new Tester(new WeakSynchronization());
            Tester fastReadWriteLockSyncronization = new Tester(new FastReadWriteLockSynchronization());
            Tester dotNetReadWriteSynchronization = new Tester(new DotNetReadWriteSyncronization());
            Tester slimReadWriteSynchronization = new Tester(new SlimReadWriteSyncronization());

            Console.WriteLine("\n\n\nTesting read on single thread");
            Console.WriteLine("No Syncronization: {0}ms", noSyncronization.TestReadOnSingleThread().TotalMilliseconds);
            Console.WriteLine("Monitor Synchronization: {0}ms", monitorSyncronization.TestReadOnSingleThread().TotalMilliseconds);
            Console.WriteLine("TimedLock Synchronization: {0}ms", timedLockSyncronization.TestReadOnSingleThread().TotalMilliseconds);
            Console.WriteLine("ReaderOrExclusive Syncronization: {0}ms", readerOrExclusiveSyncronization.TestReadOnSingleThread().TotalMilliseconds);
            Console.WriteLine("Weak Syncronization: {0}ms", weakSyncronization.TestReadOnSingleThread().TotalMilliseconds);
            Console.WriteLine("Fast Syncronization: {0}ms", fastReadWriteLockSyncronization.TestReadOnSingleThread().TotalMilliseconds);
            Console.WriteLine(".Net ReadWriteLock Synchronization: {0}ms", dotNetReadWriteSynchronization.TestReadOnSingleThread().TotalMilliseconds);
            Console.WriteLine(".Net ReadWriteLockSlim Syncronization: {0}ms", slimReadWriteSynchronization.TestReadOnSingleThread().TotalMilliseconds);

            Console.WriteLine("\n\n\nTesting async writes on single thread");
            Console.WriteLine("No Syncronization: {0}ms", noSyncronization.TestWriteOnSingleThread().TotalMilliseconds);
            Console.WriteLine("Monitor Synchronization: {0}ms", monitorSyncronization.TestWriteOnSingleThread().TotalMilliseconds);
            Console.WriteLine("TimedLock Synchronization: {0}ms", timedLockSyncronization.TestWriteOnSingleThread().TotalMilliseconds);
            Console.WriteLine("ReaderOrExclusive Syncronization: {0}ms", readerOrExclusiveSyncronization.TestWriteOnSingleThread().TotalMilliseconds);
            Console.WriteLine("Weak Syncronization: {0}ms", weakSyncronization.TestWriteOnSingleThread().TotalMilliseconds);
            Console.WriteLine("Fast Syncronization: {0}ms", fastReadWriteLockSyncronization.TestWriteOnSingleThread().TotalMilliseconds);
            Console.WriteLine(".Net ReadWriteLock Synchronization: {0}ms", dotNetReadWriteSynchronization.TestWriteOnSingleThread().TotalMilliseconds);
            Console.WriteLine(".Net ReadWriteLockSlim Syncronization: {0}ms", slimReadWriteSynchronization.TestWriteOnSingleThread().TotalMilliseconds);

            int numThreads;


            numThreads = Environment.ProcessorCount;
            Console.WriteLine("\n\n\nTesting read on all cores");
            Console.WriteLine("No Syncronization: {0}ms", noSyncronization.TestReadOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine("Monitor Synchronization: {0}ms", monitorSyncronization.TestReadOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine("TimedLock Synchronization: {0}ms", timedLockSyncronization.TestReadOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine("ReaderOrExclusive Syncronization: {0}ms", readerOrExclusiveSyncronization.TestReadOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine("Weak Syncronization: {0}ms", weakSyncronization.TestReadOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine("Fast Syncronization: {0}ms", fastReadWriteLockSyncronization.TestReadOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine(".Net ReadWriteLock Synchronization: {0}ms", dotNetReadWriteSynchronization.TestReadOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine(".Net ReadWriteLockSlim Syncronization: {0}ms", slimReadWriteSynchronization.TestReadOnMultipleThreads(numThreads).TotalMilliseconds);

            Console.WriteLine("\n\n\nTesting write on all cores");
            Console.WriteLine("No Syncronization: {0}ms", noSyncronization.TestWriteOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine("Monitor Synchronization: {0}ms", monitorSyncronization.TestWriteOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine("TimedLock Synchronization: {0}ms", timedLockSyncronization.TestWriteOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine("ReaderOrExclusive Syncronization: {0}ms", readerOrExclusiveSyncronization.TestWriteOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine("Weak Syncronization: {0}ms", weakSyncronization.TestWriteOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine("Fast Syncronization: {0}ms", fastReadWriteLockSyncronization.TestWriteOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine(".Net ReadWriteLock Synchronization: {0}ms", dotNetReadWriteSynchronization.TestWriteOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine(".Net ReadWriteLockSlim Syncronization: {0}ms", slimReadWriteSynchronization.TestWriteOnMultipleThreads(numThreads).TotalMilliseconds);

            numThreads = Environment.ProcessorCount * 3;
            Console.WriteLine("\n\n\nTesting read on 3 threads per core");
            Console.WriteLine("No Syncronization: {0}ms", noSyncronization.TestReadOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine("Monitor Synchronization: {0}ms", monitorSyncronization.TestReadOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine("TimedLock Synchronization: {0}ms", timedLockSyncronization.TestReadOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine("ReaderOrExclusive Syncronization: {0}ms", readerOrExclusiveSyncronization.TestReadOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine("Weak Syncronization: {0}ms", weakSyncronization.TestReadOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine("Fast Syncronization: {0}ms", fastReadWriteLockSyncronization.TestReadOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine(".Net ReadWriteLock Synchronization: {0}ms", dotNetReadWriteSynchronization.TestReadOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine(".Net ReadWriteLockSlim Syncronization: {0}ms", slimReadWriteSynchronization.TestReadOnMultipleThreads(numThreads).TotalMilliseconds);

            Console.WriteLine("\n\n\nTesting write on 3 threads per core");
            Console.WriteLine("No Syncronization: {0}ms", noSyncronization.TestWriteOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine("Monitor Synchronization: {0}ms", monitorSyncronization.TestWriteOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine("TimedLock Synchronization: {0}ms", timedLockSyncronization.TestWriteOnMultipleThreads(numThreads).TotalMilliseconds);
            //Console.WriteLine("ReaderOrExclusive Syncronization: {0}ms", readerOrExclusiveSyncronization.TestWriteOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine("Weak Syncronization: {0}ms", weakSyncronization.TestWriteOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine("Fast Syncronization: {0}ms", fastReadWriteLockSyncronization.TestWriteOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine(".Net ReadWriteLock Synchronization: {0}ms", dotNetReadWriteSynchronization.TestWriteOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine(".Net ReadWriteLockSlim Syncronization: {0}ms", slimReadWriteSynchronization.TestWriteOnMultipleThreads(numThreads).TotalMilliseconds);

            numThreads = Environment.ProcessorCount * 10;
            Console.WriteLine("\n\n\nTesting read on 10 threads per core");
            Console.WriteLine("No Syncronization: {0}ms", noSyncronization.TestReadOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine("Monitor Synchronization: {0}ms", monitorSyncronization.TestReadOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine("TimedLock Synchronization: {0}ms", timedLockSyncronization.TestReadOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine("Weak Syncronization: {0}ms", weakSyncronization.TestReadOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine("Fast Syncronization: {0}ms", fastReadWriteLockSyncronization.TestReadOnMultipleThreads(numThreads).TotalMilliseconds);
            /*Console.WriteLine("ReaderOrExclusive Syncronization: {0}ms", readerOrExclusiveSyncronization.TestReadOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine(".Net ReadWriteLock Synchronization: {0}ms", dotNetReadWriteSynchronization.TestReadOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine(".Net ReadWriteLockSlim Syncronization: {0}ms", slimReadWriteSynchronization.TestReadOnMultipleThreads(numThreads).TotalMilliseconds);*/

            Console.WriteLine("\n\n\nTesting write 10 threads per core");
            Console.WriteLine("No Syncronization: {0}ms", noSyncronization.TestWriteOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine("Monitor Synchronization: {0}ms", monitorSyncronization.TestWriteOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine("TimedLock Synchronization: {0}ms", timedLockSyncronization.TestWriteOnMultipleThreads(numThreads).TotalMilliseconds);
            //Console.WriteLine("ReaderOrExclusive Syncronization: {0}ms", readerOrExclusiveSyncronization.TestWriteOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine("Weak Syncronization: {0}ms", weakSyncronization.TestWriteOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine("Fast Syncronization: {0}ms", fastReadWriteLockSyncronization.TestWriteOnMultipleThreads(numThreads).TotalMilliseconds);
            //Console.WriteLine(".Net ReadWriteLock Synchronization: {0}ms", dotNetReadWriteSynchronization.TestWriteOnMultipleThreads(numThreads).TotalMilliseconds);
            //Console.WriteLine(".Net ReadWriteLockSlim Syncronization: {0}ms", slimReadWriteSynchronization.TestWriteOnMultipleThreads(numThreads).TotalMilliseconds);
        }
    }
}
