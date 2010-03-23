// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
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

namespace ObjectCloud.WebServer.Test
{
    [TestFixture]
    public class WebConnectionTest : WebServerTestBase
    {
        [Test]
        public void TestGarbageCollection()
        {
            try
            {
                IWebConnection webConnection = null;

                WebServer.Stop();
                WebServer.StartServer();

                EventHandler<IWebServer, EventArgs<IWebConnection>> webConnectionStarted = delegate(IWebServer webServer, EventArgs<IWebConnection> e)
                {
                    webConnection = e.Value;
                };

                try
                {
                    WebServer.WebConnectionStarting += webConnectionStarted;

                    HttpWebClient httpWebClient = new HttpWebClient();

                    HttpResponseHandler webResponse = httpWebClient.Get(
                        "http://localhost:" + WebServer.Port + "/");

                    Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
                    Assert.IsNotNull(webResponse.AsString(), "Nothing returned");

                    Assert.IsNotNull(webConnection, "IWebConnection not set by delegate");
                }
                finally
                {
                    WebServer.WebConnectionStarting -= webConnectionStarted;
                }

                WebServer.Stop();

                // Wait for the conneciton to close
                while (webConnection.Connected)
                    Thread.Sleep(25);

                WeakReference weakReference = new WeakReference(webConnection);
                webConnection = null;

                DateTime stopTime = DateTime.Now.AddSeconds(5);
                while (weakReference.IsAlive)
                {
                    Thread.Sleep(25);
                    GC.Collect();
                    Assert.IsTrue(DateTime.Now < stopTime, "Took too long to garbage collect the IWebConnection");
                }
            }
            finally
            {
                WebServer.StartServer();
            }
        }

        [Test]
        public void TestSmallContentLength()
        {
            TestContentLength(WebServer.MaxInMemoryContentSize - 1, TimeSpan.FromMinutes(0.75));
        }

        [Test]
        public void TestLargeContentLength()
        {
            TestContentLength(WebServer.MaxInMemoryContentSize * 10, TimeSpan.FromMinutes(0.75));
        }

        private void TestContentLength(uint contentLength, TimeSpan timeout)
        {
            byte[] contentToSend = new byte[contentLength];
            SRandom.NextBytes(contentToSend);

            IWebConnection webConnection = null;

            WebServer.Stop();
            WebServer.StartServer();

            EventHandler<IWebServer, EventArgs<IWebConnection>> webConnectionStarted = delegate(IWebServer webServer, EventArgs<IWebConnection> e)
            {
                webConnection = e.Value;
            };

            DateTime timeoutDateTime = DateTime.Now + timeout;

            WebServer.WebConnectionStarting += webConnectionStarted;

            try
            {
                HttpWebRequest webRequest = (HttpWebRequest)HttpWebRequest.Create("http://localhost:" + WebServer.Port + "/");
                webRequest.Method = "POST";
                webRequest.ContentType = "application/x-www-form-urlencoded";

                webRequest.ContentLength = contentToSend.Length;

                // Write the request
                webRequest.GetRequestStream().Write(contentToSend, 0, contentToSend.Length);

                // Spin until the content comes
                while (null == webConnection)
                {
                    Assert.IsTrue(DateTime.Now < timeoutDateTime, "Timeout waiting for IWebConnection object");
                    Thread.Sleep(10);
                }

                IWebConnectionContent content = null;
                do
                {
                    content = webConnection.Content;
                    Assert.IsTrue(DateTime.Now < timeoutDateTime, "Timeout waiting for IWebConnectionContent");
                    Thread.Sleep(10);
                } while (null == content);

                byte[] recievedContent = content.AsBytes();

                for (ulong ctr = 0; ctr < contentLength; ctr++)
                    Assert.AreEqual(contentToSend[ctr], recievedContent[ctr], "Mismatch at index " + ctr.ToString());
            }
            finally
            {
                WebServer.WebConnectionStarting -= webConnectionStarted;
            }
        }
    }
}
