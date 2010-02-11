// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
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
    public class TestSession : WebServerTestBase
    {
        [Test]
        public void TestKeepAliveTrue()
        {
            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            HttpResponseHandler webResponse = httpWebClient.Post(
                "http://localhost:" + WebServer.Port + "/System/SessionManager?Method=SetKeepAlive",
                new KeyValuePair<string, string>("KeepAlive", true.ToString(CultureInfo.InvariantCulture)));

            Assert.AreEqual(HttpStatusCode.Accepted, webResponse.StatusCode, "Wrong status code");

            Cookie sessionCookie = webResponse.HttpWebResponse.Cookies["SESSION"];

            Assert.IsNotNull(sessionCookie, "Session cookie not found");

            string sessionCookieContents = HTTPStringFunctions.DecodeRequestParametersFromBrowser(sessionCookie.Value);

            Assert.IsTrue(sessionCookieContents.Contains(","), "Session not set to keepalive");
        }

        [Test]
        public void TestKeepAliveFalse()
        {
            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            HttpResponseHandler webResponse = httpWebClient.Post(
                "http://localhost:" + WebServer.Port + "/System/SessionManager?Method=SetKeepAlive",
                new KeyValuePair<string, string>("KeepAlive", false.ToString(CultureInfo.InvariantCulture)));

            Assert.AreEqual(HttpStatusCode.Accepted, webResponse.StatusCode, "Wrong status code");

            Cookie sessionCookie = webResponse.HttpWebResponse.Cookies["SESSION"];

            Assert.IsNotNull(sessionCookie, "Session cookie not found");

            string sessionCookieContents = HTTPStringFunctions.DecodeRequestParametersFromBrowser(sessionCookie.Value);

            Assert.IsFalse(sessionCookieContents.Contains(","), "Session set to keepalive");
        }
    }
}
