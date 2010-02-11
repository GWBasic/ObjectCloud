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
    public class Text : WebServerTestBase
    {
        [Test]
        public void TestConcurrent()
        {
            Exception threadException = null;

            List<string> expectedText = new List<string>(new string[] { "" });

            string newfileName = "Text_TestConcurrent" + SRandom.Next().ToString();

            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            CreateFile(WebServer, httpWebClient, "/", newfileName, "text");

            bool keepLooping = true;

            List<Thread> threads = new List<Thread>();

            List<HttpWebClient> openConnections = new List<HttpWebClient>();

            Shared<uint> loops = new Shared<uint>(0);

            for (int ctr = 0; ctr < 10; ctr++)
                threads.Add(new Thread(delegate()
                {
                    try
                    {
                        while (keepLooping)
                        {
                            HttpWebClient threadHttpWebClient = new HttpWebClient();

                            lock (openConnections)
                                openConnections.Add(threadHttpWebClient);

                            try
                            {

                                LoginAsRoot(threadHttpWebClient);

                                HttpResponseHandler threadWebResponse = threadHttpWebClient.Get(
                                    "http://localhost:" + WebServer.Port + "/" + newfileName);

                                Assert.AreEqual(HttpStatusCode.OK, threadWebResponse.StatusCode, "Bad status code");

                                lock (expectedText)
                                    Assert.IsTrue(expectedText.Contains(threadWebResponse.AsString()), "Unexpected response");

                                lock (loops)
                                    loops.Value++;
                            }
                            finally
                            {
                                lock (openConnections)
                                    openConnections.Remove(threadHttpWebClient);
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

                string text = "text written on thread";

                byte[] toWrite = Encoding.UTF8.GetBytes(text);

                lock (expectedText)
                    expectedText.Add(text);

                webRequest.ContentLength = toWrite.Length;

                // Write the request
                webRequest.GetRequestStream().Write(toWrite, 0, toWrite.Length);

                using (HttpWebResponse httpWebResponse = (HttpWebResponse)webRequest.GetResponse())
                    Assert.AreEqual(HttpStatusCode.Accepted, httpWebResponse.StatusCode, "Bad status code");

                uint targetLoops = loops.Value * 2;

                // Wait until all open web connections are complete before chainging the expected text
                List<HttpWebClient> openWebConnectionsToComplete;
                lock (openConnections)
                    openWebConnectionsToComplete = new List<HttpWebClient>(openConnections);

                bool connectionPresent;
                do
                {
                    Thread.Sleep(25);

                    connectionPresent = false;

                    foreach (HttpWebClient webClient in openWebConnectionsToComplete)
                        lock (openConnections)
                            if (openConnections.Contains(webClient))
                                connectionPresent = true;

                } while (connectionPresent);

                expectedText = new List<string>(new string[] { text });

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
        }

        [Test]
        public void TestUseText()
        {
            string newfileName = "TestUseText" + SRandom.Next().ToString();

            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            CreateFile(WebServer, httpWebClient, "/", newfileName, "text");

            HttpWebRequest webRequest = (HttpWebRequest)HttpWebRequest.Create("http://localhost:" + WebServer.Port + "/" + newfileName + "?Method=WriteAll");
            webRequest.Method = "POST";
            webRequest.ContentType = "application/x-www-form-urlencoded";
            webRequest.CookieContainer = httpWebClient.CookieContainer;

            string text = "the\ntext\nto\nsave";

            byte[] toWrite = Encoding.UTF8.GetBytes(text);

            webRequest.ContentLength = toWrite.Length;

            // Write the request
            webRequest.GetRequestStream().Write(toWrite, 0, toWrite.Length);

            using (HttpWebResponse httpWebResponse = (HttpWebResponse)webRequest.GetResponse())
            {
                Assert.AreEqual(HttpStatusCode.Accepted, httpWebResponse.StatusCode, "Bad status code");
            }

            HttpResponseHandler webResponse = httpWebClient.Get(
                "http://localhost:" + WebServer.Port + "/" + newfileName,
                new KeyValuePair<string, string>("Method", "ReadAll"));

            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual(text, webResponse.AsString(), "Unexpected value");
        }

        [Test]
        public void TestLargeTextFile()
        {
            string newfileName = "TestLargeTextFile" + SRandom.Next().ToString();

            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            CreateFile(WebServer, httpWebClient, "/", newfileName, "text");

            StringBuilder textBuilder = new StringBuilder();

            for (int ctr = 0; ctr < 50; ctr++)
                textBuilder.AppendLine("bhvueribksrtbgiusrhgvuigviuosh ' ngilusnbgsnlibghsiougbbhsrliugbhiseuhgusehy aerjgpo\"azerhvgiuSBHgaehriughg9heroyghaesruhgazr78gu7ghauibgyr8a7fageryugfayiubga67hfbayuewgfyaewubfg6i7szrhgysudrgf678aq4btfaewr6ighaseiuyaer578ntgrgyohwe5iug7aer8graeyugfseyukbg6z87rhgiuseras5hgaer67thfegase5iugh7zrdhgzzre5ya7e4iughaeafaewrtfg7uaesy4gf67ae4hfb");

            string text = textBuilder.ToString().Trim();

            byte[] toWrite = Encoding.UTF8.GetBytes(text);

            for (int requestCtr = 0; requestCtr < 30; requestCtr++)
            {
                HttpWebRequest webRequest = (HttpWebRequest)HttpWebRequest.Create("http://localhost:" + WebServer.Port + "/" + newfileName + "?Method=WriteAll");
                webRequest.KeepAlive = true;
                webRequest.UnsafeAuthenticatedConnectionSharing = true;
                webRequest.Method = "POST";
                webRequest.ContentType = "application/x-www-form-urlencoded";
                webRequest.CookieContainer = httpWebClient.CookieContainer;

                webRequest.ContentLength = toWrite.Length;

                // Write the request
                webRequest.GetRequestStream().Write(toWrite, 0, toWrite.Length);

                using (HttpWebResponse httpWebResponse = (HttpWebResponse)webRequest.GetResponse())
                {
                    Assert.AreEqual(HttpStatusCode.Accepted, httpWebResponse.StatusCode, "Bad status code");
                }

                HttpResponseHandler webResponse = httpWebClient.Get(
                    "http://localhost:" + WebServer.Port + "/" + newfileName,
                    new KeyValuePair<string, string>("Method", "ReadAll"));

                Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
                Assert.AreEqual(text, webResponse.AsString(), "Unexpected value");
            }


            /*[Test]
            public void TestLargeTextFileSentSlowly()
            {
			    /*
			     * History on this test
			     * 
			     * When I developed using the Mono / Suse VM, for some reason, large text files would get corrupted when
			     * saved through the browser.  The problem went away after I downloaded the VM's system updates, so I
			     * assume that the problem had nothing to do with ObjectCloud.  This unit test can be safely deleted
			     * after using Weco with large text files is known to work.
			     * 
			     * */

            /*Assert.Fail("This isn't failing yet");
            	
            string newfileName = "TestLargeTextFile" + SRandom.Next().ToString();

            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            HttpResponseHandler webResponse = httpWebClient.Get(
                "http://localhost:" + WebServer.Port + "/",
                new KeyValuePair<string, string>("Method", "CreateFile"),
                new KeyValuePair<string, string>("FileName", newfileName),
                new KeyValuePair<string, string>("FileType", "text"));

            Assert.AreEqual(HttpStatusCode.Created, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual(newfileName + " created as text", webResponse.AsString(), "Unexpected response");

            StringBuilder textBuilder = new StringBuilder();
    			
            for (int ctr = 0; ctr < 50; ctr++)
                textBuilder.AppendLine("bhvueribksrtbgiusrhgvuigviuosh ' ngilusnbgsnlibghsiougbbhsrliugbhiseuhgusehy aerjgpo\"azerhvgiuSBHgaehriughg9heroyghaesruhgazr78gu7ghauibgyr8a7fageryugfayiubga67hfbayuewgfyaewubfg6i7szrhgysudrgf678aq4btfaewr6ighaseiuyaer578ntgrgyohwe5iug7aer8graeyugfseyukbg6z87rhgiuseras5hgaer67thfegase5iugh7zrdhgzzre5ya7e4iughaeafaewrtfg7uaesy4gf67ae4hfb");
    			
            string text = textBuilder.ToString().Trim();

            byte[] toWrite = Encoding.UTF8.GetBytes(text);

            HttpWebRequest webRequest = (HttpWebRequest)HttpWebRequest.Create("http://localhost:" + WebServer.Port + "/" + newfileName + "?Method=WriteAll");
            webRequest.KeepAlive = true;
            webRequest.UnsafeAuthenticatedConnectionSharing = true;
            webRequest.Method = "POST";
            webRequest.ContentType = "application/x-www-form-urlencoded";
            webRequest.CookieContainer = httpWebClient.CookieContainer;

            webRequest.ContentLength = toWrite.Length;

            // Write the request
            /*for (int ctr = 0; ctr < toWrite.Length; ctr++)
            {
                webRequest.GetRequestStream().Write(toWrite, ctr, 1);
                Thread.Sleep(1);
            }*/
            /*webRequest.GetRequestStream().Write(toWrite, 0, toWrite.Length / 2);
            //Thread.Sleep(2000);
            webRequest.GetRequestStream().Write(toWrite, toWrite.Length / 2, toWrite.Length / 2);

            using (HttpWebResponse httpWebResponse = (HttpWebResponse)webRequest.GetResponse())
            {
                Assert.AreEqual(HttpStatusCode.Accepted, httpWebResponse.StatusCode, "Bad status code");
            }

            webResponse = httpWebClient.Get(
                "http://localhost:" + WebServer.Port + "/" + newfileName,
                new KeyValuePair<string, string>("Method", "ReadAll"));

            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual(text, webResponse.AsString(), "Unexpected value");
        }*/
        }
    }
}