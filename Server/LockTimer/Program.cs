using System;
using System.Collections.Generic;
using System.Threading;

namespace LockTimer
{
    class Program
    {
        static void Main(string[] args)
        {
            TestLock[] lockDelegates = new TestLock[] { NoLock, Lock, TimedLock, ReaderWriterLock_Read, ReaderWriterLock_Write, ReaderWriterLockSlim_Read, ReaderWriterLockSlim_Write };

            ObjectCloud.Common.Enumerable<TestLock>.MultithreadedEach(
                1,
                lockDelegates,
                TimeLock);

            /*TimeLock(NoLock);
            TimeLock(Lock);
            TimeLock(TimedLock);

            TimeLock(ReaderWriterLock_Read);
            TimeLock(ReaderWriterLock_Write);

            TimeLock(ReaderWriterLockSlim_Read);
            TimeLock(ReaderWriterLockSlim_Write);*/
        }

        private static TimeSpan TestLength = TimeSpan.FromSeconds(5);

        private static object ConsoleLock = new object();

        private static void TimeLock(TestLock lockDelegate)
        {
            DateTime end = DateTime.UtcNow + TestLength;

            int iterations = 0;

            do
            {
                lockDelegate();
                iterations++;
            } while (DateTime.UtcNow < end);

            double averageMSperIteration = TestLength.TotalMilliseconds / Convert.ToDouble(iterations);
            double averageTicksPerIteration = Convert.ToDouble(TestLength.Ticks) / Convert.ToDouble(iterations);

            lock (ConsoleLock)
            {
                Console.WriteLine("Results for {0}", lockDelegate.Method.Name);
                Console.WriteLine("Total iterations: {0}", iterations);

                Console.WriteLine("Averate time per iteration: {0:0.0000000000} ms, {1} ticks", averageMSperIteration, averageTicksPerIteration);
                Console.WriteLine();
            }
        }

        private static object key = new object();

        private static long NoLock()
        {
            return 1;
        }

        private static long Lock()
        {
            lock (key)
                return 1;
        }

        private static long TimedLock()
        {
            using (ObjectCloud.Common.Threading.TimedLock.Lock(key))
                return 1;
        }

        private static ReaderWriterLock ReaderWriterLock = new ReaderWriterLock();

        private static long ReaderWriterLock_Read()
        {
            ReaderWriterLock.AcquireReaderLock(250);

            try
            {
                return 1;
            }
            finally
            {
                ReaderWriterLock.ReleaseReaderLock();
            }
        }

        private static long ReaderWriterLock_Write()
        {
            ReaderWriterLock.AcquireWriterLock(250);

            try
            {
                return 1;
            }
            finally
            {
                ReaderWriterLock.ReleaseWriterLock();
            }
        }

        private static ReaderWriterLockSlim ReaderWriterLockSlim = new ReaderWriterLockSlim();

        private static long ReaderWriterLockSlim_Read()
        {
            ReaderWriterLockSlim.EnterReadLock();

            try
            {
                return 1;
            }
            finally
            {
                ReaderWriterLockSlim.ExitReadLock();
            }
        }

        private static long ReaderWriterLockSlim_Write()
        {
            ReaderWriterLockSlim.EnterWriteLock();

            try
            {
                return 1;
            }
            finally
            {
                ReaderWriterLockSlim.ExitWriteLock();
            }
        }
    }

    delegate long TestLock();
}
