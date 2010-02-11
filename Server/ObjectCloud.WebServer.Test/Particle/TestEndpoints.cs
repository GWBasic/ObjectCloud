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
using ObjectCloud.Disk.FileHandlers.Particle;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.WebServer;
using ObjectCloud.WebServer.Implementation;

namespace ObjectCloud.WebServer.Test.Particle
{
    [TestFixture]
    public class TestEndpoints : WebServerTestBase
    {
        [Test]
        public void TestEndpointSanity()
        {
            Assert.IsNotNull(Endpoints.GetEndpoints("http://localhost:" + WebServer.Port.ToString() + "/Users/root.user"), "Could not load endpoints");
        }

        private void TestEndpoint(string endpoint, string expectedValue)
        {
            Endpoints endpoints = Endpoints.GetEndpoints("http://localhost:" + WebServer.Port.ToString() + "/Users/root.user");
            Assert.AreEqual(expectedValue, endpoints[endpoint], "Wrong endpoint for " + endpoint);
        }

        [Test]
        public void EstablishTrust()
        {
            TestEndpoint("establishTrust", "http://localhost:" + WebServer.Port.ToString() + "/Users/root.user?Method=EstablishTrust");
        }

        [Test]
        public void RespondTrust()
        {
            TestEndpoint("respondTrust", "http://localhost:" + WebServer.Port.ToString() + "/Users/root.user?Method=RespondTrust");
        }

        [Test]
        public void ReceiveNotification()
        {
            TestEndpoint("receiveNotification", "http://localhost:" + WebServer.Port.ToString() + "/Users/root.user?Method=ReceiveNotification");
        }

        [Test]
        public void EstablishSession()
        {
            TestEndpoint("establishSession", "http://localhost:" + WebServer.Port.ToString() + "/Users/root.user?Method=EstablishSession");
        }

        [Test]
        public void GetNotifications()
        {
            TestEndpoint("getNotifications", "http://localhost:" + WebServer.Port.ToString() + "/Users/root.user?Method=GetNotifications");
        }

        [Test]
        public void SendNotification()
        {
            TestEndpoint("sendNotification", "http://localhost:" + WebServer.Port.ToString() + "/Users/root.user?Method=SendNotification");
        }

        [Test]
        public void UpdateNotificationState()
        {
            TestEndpoint("updateNotificationState", "http://localhost:" + WebServer.Port.ToString() + "/Users/root.user?Method=UpdateNotificationState");
        }

        [Test]
        public void UpdateObjectState()
        {
            TestEndpoint("updateObjectState", "http://localhost:" + WebServer.Port.ToString() + "/Users/root.user?Method=UpdateObjectState");
        }

        [Test]
        public void Block()
        {
            TestEndpoint("block", "http://localhost:" + WebServer.Port.ToString() + "/Users/root.user?Method=Block");
        }

        [Test]
        public void UnBlock()
        {
            TestEndpoint("unBlock", "http://localhost:" + WebServer.Port.ToString() + "/Users/root.user?Method=UnBlock");
        }

        [Test]
        public void GetBlocked()
        {
            TestEndpoint("getBlocked", "http://localhost:" + WebServer.Port.ToString() + "/Users/root.user?Method=GetBlocked");
        }
    }
}
