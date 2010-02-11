// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

using Spring.Context;
using Spring.Context.Support;

using NUnit.Framework;

using JsonFx.Json;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.WebServer;
using ObjectCloud.WebServer.Implementation;
using ObjectCloud.WebServer.Test;

namespace ObjectCloud.WebServer.Test.WebServerTestClasses
{
    [TestFixture]
    public class Delete : WebServerTestBase
    {
        [Test]
        public void TestDelete()
        {
            string newfileName = "TestDelete" + SRandom.Next().ToString();

            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            CreateFile(WebServer, httpWebClient, "/", newfileName, "text");

            HttpResponseHandler webResponse = httpWebClient.Post(
                "http://localhost:" + WebServer.Port + "/?Method=DeleteFile",
                new KeyValuePair<string, string>("FileName", newfileName));

            Assert.AreEqual(HttpStatusCode.Accepted, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual(newfileName + " deleted", webResponse.AsString(), "Unexpected response");

            webResponse = httpWebClient.Get(
                "http://localhost:" + WebServer.Port + "/" + newfileName,
                new KeyValuePair<string, string>("Method", "ReadAll"));

            Assert.AreEqual(HttpStatusCode.NotFound, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual("/" + newfileName + " does not exist", webResponse.AsString(), "Unexpected response");
        }

        [Test]
        public void TestDelete404OnMissing()
        {
            string newfileName = "TestDelete404OnMissing" + SRandom.Next().ToString();

            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            HttpResponseHandler webResponse = httpWebClient.Post(
                "http://localhost:" + WebServer.Port + "/?Method=DeleteFile",
                new KeyValuePair<string, string>("FileName", newfileName));

            Assert.AreEqual(HttpStatusCode.NotFound, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual(newfileName + " does not exist", webResponse.AsString(), "Unexpected response");
        }

        /// <summary>
        /// Helper for TestDeleteConcurrent
        /// </summary>
        private class TestDeleteConcurrentHelper
        {
            public string NewfileName = "TestDeleteConcurrent" + SRandom.Next().ToString();
            public Shared<uint> NumAccepted = new Shared<uint>(0);
            public Shared<uint> Num404 = new Shared<uint>(0);
            DateTime StartTime = DateTime.Now.AddMilliseconds(100);
            public Exception ThreadException = null;
            public IWebServer WebServer;

            public void TryDeleteFile()
            {
                // Spin until the start time
                while (DateTime.Now < StartTime) { }

                try
                {
                    HttpWebClient threadHttpWebClient = new HttpWebClient();
                    LoginAsRoot(threadHttpWebClient, WebServer);

                    HttpResponseHandler threadWebResponse = threadHttpWebClient.Post(
                        "http://localhost:" + WebServer.Port + "/?Method=DeleteFile",
                        new KeyValuePair<string, string>("FileName", NewfileName));

                    switch (threadWebResponse.StatusCode)
                    {
                        case HttpStatusCode.Accepted:
                            {
                                Assert.AreEqual(NewfileName + " deleted", threadWebResponse.AsString(), "Unexpected response");

                                lock (NumAccepted)
                                    NumAccepted.Value++;

                                break;
                            }

                        case HttpStatusCode.NotFound:
                            {
                                Assert.AreEqual(NewfileName + " does not exist", threadWebResponse.AsString(), "Unexpected response");

                                lock (Num404)
                                    Num404.Value++;

                                break;
                            }

                        default:
                            {
                                Assert.Fail("Unanticipated status code: " + threadWebResponse.StatusCode.ToString());
                                break;
                            }
                    }
                }
                catch (Exception e)
                {
                    ThreadException = e;
                }
            }
        }

        [Test]
        public void TestDeleteConcurrent()
        {
            TestDeleteConcurrentHelper tdch = new TestDeleteConcurrentHelper();
            tdch.WebServer = WebServer;

            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            CreateFile(WebServer, httpWebClient, "/", tdch.NewfileName, "text");

            List<Thread> threads = new List<Thread>();

            for (int ctr = 0; ctr < 10; ctr++)
                threads.Add(new Thread(tdch.TryDeleteFile));

            foreach (Thread thread in threads)
                thread.Start();

            foreach (Thread thread in threads)
                thread.Join();

            if (null != tdch.ThreadException)
                throw tdch.ThreadException;

            Assert.AreEqual(1, tdch.NumAccepted.Value, "File is only supposed to be deleted one time");
            Assert.AreEqual(threads.Count - 1, tdch.Num404.Value, "File is supposed to be missing many times");

            HttpResponseHandler webResponse = httpWebClient.Get(
                "http://localhost:" + WebServer.Port + "/" + tdch.NewfileName,
                new KeyValuePair<string, string>("Method", "ReadAll"));

            Assert.AreEqual(HttpStatusCode.NotFound, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual("/" + tdch.NewfileName + " does not exist", webResponse.AsString(), "Unexpected response");
        }
    }
}
