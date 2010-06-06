using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TimeLocks
{
    class Program
    {
        static void Main(string[] args)
        {
            Tester noSyncronization = new Tester(new NoSyncronization());
            Tester monitorSyncronization = new Tester(new MonitorSyncronization());
            Tester timedLockSyncronization = new Tester(new TimedLockSynchronization());
            Tester readerOrExclusiveSyncronization = new Tester(new ReaderOrExclusiveSynchronization());
            Tester dotNetReadWriteSynchronization = new Tester(new DotNetReadWriteSyncronization());
            Tester slimReadWriteSynchronization = new Tester(new SlimReadWriteSyncronization());

            Console.WriteLine("Testing read on single thread");
            Console.WriteLine("No Syncronization: {0}ms", noSyncronization.TestReadOnSingleThread().TotalMilliseconds);
            Console.WriteLine("Monitor Synchronization: {0}ms", monitorSyncronization.TestReadOnSingleThread().TotalMilliseconds);
            Console.WriteLine("TimedLock Synchronization: {0}ms", timedLockSyncronization.TestReadOnSingleThread().TotalMilliseconds);
            Console.WriteLine("ReaderOrExclusive Syncronization: {0}ms", readerOrExclusiveSyncronization.TestReadOnSingleThread().TotalMilliseconds);
            Console.WriteLine(".Net ReadWriteLock Synchronization: {0}ms", dotNetReadWriteSynchronization.TestReadOnSingleThread().TotalMilliseconds);
            Console.WriteLine(".Net ReadWriteLockSlim Syncronization: {0}ms", slimReadWriteSynchronization.TestReadOnSingleThread().TotalMilliseconds);

            int numThreads;


            numThreads = Environment.ProcessorCount;
            Console.WriteLine("\n\n\nTesting read on all cores");
            Console.WriteLine("No Syncronization: {0}ms", noSyncronization.TestReadOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine("Monitor Synchronization: {0}ms", monitorSyncronization.TestReadOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine("TimedLock Synchronization: {0}ms", timedLockSyncronization.TestReadOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine("ReaderOrExclusive Syncronization: {0}ms", readerOrExclusiveSyncronization.TestReadOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine(".Net ReadWriteLock Synchronization: {0}ms", dotNetReadWriteSynchronization.TestReadOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine(".Net ReadWriteLockSlim Syncronization: {0}ms", slimReadWriteSynchronization.TestReadOnMultipleThreads(numThreads).TotalMilliseconds);

            numThreads = Environment.ProcessorCount * 3;
            Console.WriteLine("\n\n\nTesting read on 3 threads per core");
            Console.WriteLine("No Syncronization: {0}ms", noSyncronization.TestReadOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine("Monitor Synchronization: {0}ms", monitorSyncronization.TestReadOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine("TimedLock Synchronization: {0}ms", timedLockSyncronization.TestReadOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine("ReaderOrExclusive Syncronization: {0}ms", readerOrExclusiveSyncronization.TestReadOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine(".Net ReadWriteLock Synchronization: {0}ms", dotNetReadWriteSynchronization.TestReadOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine(".Net ReadWriteLockSlim Syncronization: {0}ms", slimReadWriteSynchronization.TestReadOnMultipleThreads(numThreads).TotalMilliseconds);

            numThreads = Environment.ProcessorCount * 10;
            Console.WriteLine("\n\n\nTesting read on 10 threads per core");
            Console.WriteLine("No Syncronization: {0}ms", noSyncronization.TestReadOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine("Monitor Synchronization: {0}ms", monitorSyncronization.TestReadOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine("TimedLock Synchronization: {0}ms", timedLockSyncronization.TestReadOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine("ReaderOrExclusive Syncronization: {0}ms", readerOrExclusiveSyncronization.TestReadOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine(".Net ReadWriteLock Synchronization: {0}ms", dotNetReadWriteSynchronization.TestReadOnMultipleThreads(numThreads).TotalMilliseconds);
            Console.WriteLine(".Net ReadWriteLockSlim Syncronization: {0}ms", slimReadWriteSynchronization.TestReadOnMultipleThreads(numThreads).TotalMilliseconds);
        }
    }
}
