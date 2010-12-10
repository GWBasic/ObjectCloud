// Copyright 2009, 2010 Andrew Rondeau
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

        private void DoGets()
        {
            try
            {
                HttpWebClient httpWebClient = new HttpWebClient();

                for (int ctr = 0; ctr < NumRequests; ctr++)
                {
                    var webResponse = httpWebClient.Get("http://localhost:" + WebServer.Port + "/API/jquery.js");

                    if (webResponse.StatusCode == HttpStatusCode.OK)
                        Interlocked.Increment(ref CompletedRequests);
                    else
                        return;
                }
            }
            catch (Exception e)
            {
                ExceptionContainer.Value = e;
            }
        }

        private void DoMultithreadedGets(int numThreads, int numRequests)
        {
            NumRequests = numRequests;
            ExceptionContainer = new Wrapped<Exception>(null);
            CompletedRequests = 0;

            List<Thread> threads = new List<Thread>();
            for (int ctr = 1; ctr < numThreads; ctr++)
            {
                Thread thread = new Thread(DoGets);
                thread.Start();

                threads.Add(thread);
            }

            DoGets();

            foreach (Thread thread in threads)
                thread.Join();

            if (null != ExceptionContainer.Value)
                throw ExceptionContainer.Value;

            Assert.AreEqual(numRequests * numThreads, CompletedRequests, "Unexpected number of successes");
        }

        [Test]
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
        }
    }
}
