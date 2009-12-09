// Copyright 2009 Andrew Rondeau
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
using ObjectCloud.Disk.FileHandlers.Particle;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.WebServer;
using ObjectCloud.WebServer.Implementation;

namespace ObjectCloud.WebServer.Test.Particle
{
    [TestFixture]
    public class TestTrust : HasSecondServer
    {
        [Test]
        public void TestEstablishTrustSanity()
        {
            TestEstablishTrustSanityReturnToken();
        }

        public string TestEstablishTrustSanityReturnToken()
        {
            HttpWebClient httpWebClient = new HttpWebClient();

            LoginAsRoot(httpWebClient);

            HttpResponseHandler webResponse = httpWebClient.Get("http://localhost:" + WebServer.Port.ToString() + "/Users/root.user",
                new KeyValuePair<string, string>("Method", "GetSenderToken"),
                new KeyValuePair<string, string>("openId", "http://localhost:" + SecondWebServer.Port.ToString() + "/Users/root.user"));

            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode);

            string senderToken = webResponse.AsString();
            Assert.IsNotNull(senderToken);
            Assert.IsTrue(senderToken.Length > 40, "Sender token is too short: " + senderToken);

            return senderToken;
        }

        [Test]
        public void TestEstablishTrust()
        {
            string senderToken = TestEstablishTrustSanityReturnToken();

            IFileContainer recipientContainer = SecondFileHandlerFactoryLocator.FileSystemResolver.ResolveFile("/Users/root.user");
            IUserHandler recipientHandler = recipientContainer.CastFileHandler<IUserHandler>();

            string senderOpenId = recipientHandler.GetOpenIdFromSenderToken(senderToken);
            Assert.AreEqual("http://localhost:" + WebServer.Port.ToString() + "/Users/root.user", senderOpenId, "Sender OpenID not saved correctly");

            /*IFileContainer senderContainer = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile("/Users/root.user");
            IUserHandler senderHandler = senderContainer.CastFileHandler<IUserHandler>();*/
        }
    }
}
