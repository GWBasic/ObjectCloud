// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;

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
    public class Binary : WebServerTestBase
    {
        /*// <summary>
        /// Used to allow comparison with Equals()
        /// </summary>
        private class ByteWrapper : Shared<IEnumerable<byte>>
        {
            public ByteWrapper(IEnumerable<byte> value) : base(value) { }

            public override bool Equals(object obj)
            {
                if (obj is ByteWrapper)
                    return Enumerable.Equals(Value, ((ByteWrapper)obj).Value);

                return false;
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
            }
        }

        [Test]
        public void TestConcurrent()
        {
            Exception threadException = null;

            List<ByteWrapper> expectedBinary = new List<ByteWrapper>();
            expectedBinary.Add(new ByteWrapper(new byte[0]));

            string newfileName = "Binary_TestConcurrent" + SRandom.Next().ToString();

            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            HttpResponseHandler webResponse = httpWebClient.Get(
                "http://localhost:" + WebServer.Port + "/",
                new KeyValuePair<string, string>("Method", "CreateFile"),
                new KeyValuePair<string, string>("FileName", newfileName),
                new KeyValuePair<string, string>("FileType", "binary"));

            Assert.AreEqual(HttpStatusCode.Created, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual(newfileName + " created as binary", webResponse.AsString(), "Unexpected response");

            bool keepLooping = true;

            List<Thread> threads = new List<Thread>();

            List<uint> openConnections = new List<uint>();

            Shared<uint> loops = new Shared<uint>(0);

            Shared<uint> loopIdCtr = new Shared<uint>(0);

            for (int ctr = 0; ctr < 10; ctr++)
                threads.Add(new Thread(delegate()
                {
                    try
                    {
                        while (keepLooping)
                        {
                            HttpWebClient threadHttpWebClient = new HttpWebClient();

                            uint loopId;
                            lock (loopIdCtr)
                            {
                                loopId = loopIdCtr.Value;
                                loopIdCtr.Value++;
                            }

                            lock (openConnections)
                                openConnections.Add(loopId);

                            try
                            {

                                LoginAsRoot(threadHttpWebClient);

                                HttpResponseHandler threadWebResponse = threadHttpWebClient.Get(
                                    "http://localhost:" + WebServer.Port + "/" + newfileName);

                                Assert.AreEqual(HttpStatusCode.OK, threadWebResponse.StatusCode, "Bad status code");

                                byte[] response = threadWebResponse.AsBytes();

                                lock (expectedBinary)
                                    Assert.IsTrue(expectedBinary.Contains(new ByteWrapper(response)), "Unexpected response");

                                lock (loops)
                                    loops.Value++;
                            }
                            finally
                            {
                                lock (openConnections)
                                    openConnections.Remove(loopId);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        threadException = e;
                    }
                }));

            foreach (Thread thread in threads)
                thread.Start();

            try
            {
                while (loops.Value < 4)
                {
                    Thread.Sleep(25);

                    if (null != threadException)
                        throw threadException;
                }

                HttpWebRequest webRequest = (HttpWebRequest)HttpWebRequest.Create("http://localhost:" + WebServer.Port + "/" + newfileName + "?Method=WriteAll");
                webRequest.Method = "POST";
                webRequest.ContentType = "application/x-www-form-urlencoded";
                webRequest.CookieContainer = httpWebClient.CookieContainer;

                byte[] binary = SRandom.NextBytes(100);

                lock (expectedBinary)
                    expectedBinary.Add(new ByteWrapper(binary));

                webRequest.ContentLength = binary.Length;

                // Write the request
                webRequest.GetRequestStream().Write(binary, 0, binary.Length);

                using (HttpWebResponse httpWebResponse = (HttpWebResponse)webRequest.GetResponse())
                    Assert.AreEqual(HttpStatusCode.Accepted, httpWebResponse.StatusCode, "Bad status code");

                uint targetLoops = loops.Value * 2;

                // Wait until all open web connections are complete before chainging the expected binary
                List<uint> openWebConnectionsToComplete;
                lock (openConnections)
                    openWebConnectionsToComplete = new List<uint>(openConnections);

                bool connectionPresent;
                do
                {
                    Thread.Sleep(25);

                    connectionPresent = false;

                    foreach (uint webClient in openWebConnectionsToComplete)
                        lock (openConnections)
                            if (openConnections.Contains(webClient))
                                connectionPresent = true;

                } while (connectionPresent);

                expectedBinary = new List<ByteWrapper>(new ByteWrapper[] { new ByteWrapper(binary) });

                while (loops.Value < targetLoops)
                {
                    Thread.Sleep(25);

                    if (null != threadException)
                        throw threadException;
                }
            }
            finally
            {
                keepLooping = false;

                foreach (Thread thread in threads)
                    thread.Join();
            }
        }*/

        [Test]
        public void TestUseBinary()
        {
            string newfileName = "TestUseBinary" + SRandom.Next().ToString();

            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            CreateFile(WebServer, httpWebClient, "/", newfileName, "binary");

            HttpWebRequest webRequest = (HttpWebRequest)HttpWebRequest.Create("http://localhost:" + WebServer.Port + "/" + newfileName + "?Method=WriteAll");
            webRequest.Method = "POST";
            webRequest.ContentType = "application/x-www-form-urlencoded";
            webRequest.CookieContainer = httpWebClient.CookieContainer;

            byte[] binary = SRandom.NextBytes(2048);

            webRequest.ContentLength = binary.Length;

            // Write the request
            webRequest.GetRequestStream().Write(binary, 0, binary.Length);

            using (HttpWebResponse httpWebResponse = (HttpWebResponse)webRequest.GetResponse())
            {
                Assert.AreEqual(HttpStatusCode.Accepted, httpWebResponse.StatusCode, "Bad status code");
            }

            HttpResponseHandler webResponse = httpWebClient.Get(
                "http://localhost:" + WebServer.Port + "/" + newfileName,
                new KeyValuePair<string, string>("Method", "ReadAll"));

            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
            Assert.IsTrue(Enumerable.Equals(binary, webResponse.AsBytes()), "Unexpected value");
        }

        [Test]
        public void TestLargeBinaryFile()
        {
            string newfileName = "TestLargeBinaryFile" + SRandom.Next().ToString();

            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            CreateFile(WebServer, httpWebClient, "/", newfileName, "binary");

            byte[] toWrite = SRandom.NextBytes(1024 * 1024 * 3);

            for (int requestCtr = 0; requestCtr < 3; requestCtr++)
            {
                HttpWebRequest webRequest = (HttpWebRequest)HttpWebRequest.Create("http://localhost:" + WebServer.Port + "/" + newfileName + "?Method=WriteAll");
                webRequest.KeepAlive = true;
                webRequest.UnsafeAuthenticatedConnectionSharing = true;
                webRequest.Method = "POST";
                webRequest.ContentType = "application/binary";
                webRequest.CookieContainer = httpWebClient.CookieContainer;

                webRequest.ContentLength = toWrite.Length;

                // Write the request
                webRequest.GetRequestStream().Write(toWrite, 0, toWrite.Length);

                using (HttpWebResponse httpWebResponse = (HttpWebResponse)webRequest.GetResponse())
                {
                    Assert.AreEqual(HttpStatusCode.Accepted, httpWebResponse.StatusCode, "Bad status code");
                }

                byte[] written = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile("/" + newfileName).CastFileHandler<IBinaryHandler>().ReadAll();

                Assert.IsTrue(Enumerable.Equals(toWrite, written), "Data written incorrectly");

                HttpResponseHandler webResponse = httpWebClient.Get(
                    "http://localhost:" + WebServer.Port + "/" + newfileName,
                    new KeyValuePair<string, string>("Method", "ReadAll"));

                Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
                Assert.IsTrue(Enumerable.Equals(toWrite, webResponse.AsBytes()), "Unexpected value");
            }
        }
    }
}