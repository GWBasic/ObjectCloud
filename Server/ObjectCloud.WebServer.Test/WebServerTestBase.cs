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
using ObjectCloud.Spring.Config;

namespace ObjectCloud.WebServer.Test
{
    public abstract class WebServerTestBase : ObjectCloud.UnitTestHelpers.UnitTestBase
    {
        public WebServerTestBase() : base ("file://Database.xml", "file://Disk.xml", "file://WebServer.xml") {}

        /// <summary>
        /// The web server object
        /// </summary>
        public IWebServer WebServer
        {
            get { return _WebServer; }
            set { _WebServer = value; }
        }
        private IWebServer _WebServer;

        [TestFixtureSetUp]
        public void SetUpWebServer()
        {
            WebServer = (IWebServer)SpringContext.GetObject("WebServer");
            WebServer.StartServer();

            HttpWebClient.DefaultTimeout = TimeSpan.FromSeconds(45);
            
            DoAdditionalSetup();
        }
        
        protected virtual void DoAdditionalSetup()
        {
        }

        [TestFixtureTearDown]
        public void TearDownWebServer()
        {
            WebServer.Dispose();

            GC.Collect();

            DoAdditionalTearDown();
        }
        
        protected virtual void DoAdditionalTearDown()
        {
        }

        public void LoginAsRoot(HttpWebClient httpWebClient)
        {
            LoginAsRoot(httpWebClient, WebServer);
        }

        public static void LoginAsRoot(HttpWebClient httpWebClient, IWebServer webServer)
        {
            // Log in as root
            HttpResponseHandler webResponse = httpWebClient.Post(
                "http://localhost:" + webServer.Port + "/Users/UserDB?Method=Login",
                new KeyValuePair<string, string>("username", "root"),
                new KeyValuePair<string, string>("password", "root"));

            Assert.AreEqual(HttpStatusCode.Accepted, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual("root logged in", webResponse.AsString(), "Unexpected response");
        }

        public void Logout(HttpWebClient httpWebClient)
        {
            HttpResponseHandler webResponse = httpWebClient.Post(
                "http://localhost:" + WebServer.Port + "/Users/UserDB?Method=Logout");

            Assert.AreEqual(HttpStatusCode.Accepted, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual("logged out", webResponse.AsString(), "Unexpected response");
        }

        public void CreateFile(IWebServer webServer, HttpWebClient httpWebClient, string directory, string filename, string typeid)
        {
            CreateFile(webServer, httpWebClient, directory, filename, typeid, HttpStatusCode.Created);
        }

        public void CreateFile(IWebServer webServer, HttpWebClient httpWebClient, string directory, string filename, string typeid, HttpStatusCode expectedStatusCode)
        {
            HttpResponseHandler webResponse = httpWebClient.Post(
                "http://localhost:" + webServer.Port + directory + "?Method=CreateFile",
                new KeyValuePair<string, string>("FileName", filename),
                new KeyValuePair<string, string>("FileType", typeid));
        
            Assert.AreEqual(expectedStatusCode, webResponse.StatusCode, "Bad status code");
        }

        public FileHandlerFactoryLocator FileHandlerFactoryLocator
        {
            get
            {
                if (null == _FileHandlerFactoryLocator)
                    _FileHandlerFactoryLocator = (FileHandlerFactoryLocator)SpringContext["FileHandlerFactoryLocator"];

                return _FileHandlerFactoryLocator;
            }
            set { _FileHandlerFactoryLocator = value; }
        }
        private FileHandlerFactoryLocator _FileHandlerFactoryLocator = null;
    }
}
