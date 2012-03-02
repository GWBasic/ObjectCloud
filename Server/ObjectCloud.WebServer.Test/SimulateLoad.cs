// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Net;
using System.Text;
using System.Threading;

using NUnit.Framework;

using JsonFx.Json;

using ObjectCloud.Common;
using ObjectCloud.Common.Threading;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.WebServer;
using ObjectCloud.WebServer.Implementation;

namespace ObjectCloud.WebServer.Test
{
    [TestFixture]
    public class SimulateLoad : WebServerTestBase
    {
        int NumRequests;
        Wrapped<Exception> ExceptionContainer;
        int CompletedRequests;
        int NumTimeouts;

        private void DoGets()
        {
            int threadId = Thread.CurrentThread.ManagedThreadId;

            try
            {
                HttpWebClient httpWebClient = new HttpWebClient();

                for (int ctr = 0; ctr < NumRequests && null == ExceptionContainer.Value; ctr++)
                {
                    try
                    {
                        var webResponse = httpWebClient.Get("http://localhost:" + WebServer.Port + "/API/jquery.js?threadID=" + threadId.ToString() + "&ctr=" + ctr.ToString());

                        if (webResponse.StatusCode == HttpStatusCode.OK)
                            Interlocked.Increment(ref CompletedRequests);
                        else
                            return;
                    }
                    catch (Exception e)
                    {
                        if (e.Message == "The operation has timed out")
                            Interlocked.Increment(ref NumTimeouts);
                        else
                            throw;
                    }

                    //if (0 == ctr % 50)
                    //    Thread.Sleep(1500);
                }
            }
            catch (Exception e)
            {
                Assert.IsFalse(Busy.IsBusy, "Server is busy and thus blocking requests");
                ExceptionContainer.Value = e;
            }
        }

        private void DoMultithreadedGets(int numThreads, int numRequests)
        {
            NumRequests = numRequests;
            ExceptionContainer = new Wrapped<Exception>(null);
            CompletedRequests = 0;
            NumTimeouts = 0;

            List<Thread> threads = new List<Thread>();
            for (int ctr = 0; ctr < numThreads; ctr++)
            {
                Thread thread = new Thread(DoGets);
                thread.Name = "GETTER " + ctr.ToString();
                thread.Priority = ThreadPriority.Lowest;
                threads.Add(thread);
            }

            foreach (Thread thread in threads)
                thread.Start();

            foreach (Thread thread in threads)
                thread.Join();

            if (null != ExceptionContainer.Value)
                throw ExceptionContainer.Value;

            Assert.AreEqual(0, NumTimeouts, "Timeouts occured");
            Assert.AreEqual(numRequests * numThreads, CompletedRequests, "Unexpected number of successes");
        }

        [Test]
        public void Test200Single()
        {
            DoMultithreadedGets(1, 200);
        }

        [Test]
        public void Test50Four()
        {
            DoMultithreadedGets(4, 50);
        }

        /*[Test]
        public void Test3000Single()
        {
            DoMultithreadedGets(1, 3000);
        }

        [Test]
        public void Test3000Four()
        {
            DoMultithreadedGets(4, 3000);
        }

        [Test]
        public void Test30000Eight()
        {
            DoMultithreadedGets(8, 30000);
        }*/

        [Test]
        public void TestBlockWhileBusy()
        {
            object busyBlocker = new object();
            DelegateQueue dq = new DelegateQueue("Test busy blocker");

            Thread blocked = new Thread(delegate()
            {
                Busy.BlockWhileBusy("Busy Blocker Unit Test");
            });

            lock (busyBlocker)
            {
                for (int ctr = 0; ctr < dq.BusyThreshold + 5; ctr++)
                    dq.QueueUserWorkItem(delegate(object state)
                    {
                        lock (busyBlocker)
                        { }
                    });

                blocked.Start();

                Thread.Sleep(1500);
            }

            Assert.IsTrue(blocked.Join(250), "Busy thread did not un-suspend");
        }
    }
}
