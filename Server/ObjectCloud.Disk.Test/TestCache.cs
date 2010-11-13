// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using NUnit.Framework;

using ObjectCloud.Common;
using ObjectCloud.Common.Threading;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Spring.Config;

namespace ObjectCloud.Disk.Test
{
    [TestFixture]
    public class TestCache
    {
        private HashSet<long> CreatedObjects;

        /// <summary>
        /// Object that holds some memory in the cache
        /// </summary>
        private class CachedObject
        {
            public long Val;
            public byte[] Memory = new byte[1024 * 1024];
        }

        long MaxIterations = long.MinValue;
        long NumIterations;

        Cache<long, CachedObject> Cache;

        Exception Exception;

        [Test]
        public void RunTest1000()
        {
            MaxIterations = 1000;
            RunTest();
        }

        /*[Test]
        public void RunTest100000()
        {
            MaxIterations = 100000;
            RunTest();
        }*/

        /*[Test]
        public void RunTest10000000()
        {
            MaxIterations = 10000000;
            RunTest();
        }*/

        private void RunTest()
        {
            NumIterations = 0;

            Cache = new Cache<long, CachedObject>(CreateForCache);

            Exception = null;

            CreatedObjects = new HashSet<long>();

            List<Thread> threads = new List<Thread>();

            for (int ctr = 0; ctr < Environment.ProcessorCount; ctr++)
            {
                Thread thread = new Thread(RunTestThread);
                thread.Name = "Cache test thread " + ctr.ToString();
                thread.Start();

                threads.Add(thread);
            }

            foreach (Thread thread in threads)
                thread.Join();

            if (null != Exception)
                throw Exception;
        }

        CachedObject CreateForCache(long val)
        {
            Assert.IsFalse(CreatedObjects.Contains(val));
            CreatedObjects.Add(val);

            CachedObject toReturn = new CachedObject();
            toReturn.Val = val;

            return toReturn;
        }

        private void RunTestThread()
        {
            try
            {
                do
                {
                    Busy.BlockWhileBusy("test thread");

                    long val = Interlocked.Increment(ref NumIterations) / 4;
                    CachedObject cacheVal = Cache[val];
                    Assert.AreEqual(val, cacheVal.Val);
					Assert.IsNotNull(cacheVal.Memory);
                }
                while (NumIterations < MaxIterations);
            }
            catch (Exception e)
            {
                Exception = e;
                NumIterations = long.MaxValue;
            }
        }
    }
}
