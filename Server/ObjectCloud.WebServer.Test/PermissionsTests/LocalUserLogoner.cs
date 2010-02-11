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
using ObjectCloud.Spring.Config;

namespace ObjectCloud.WebServer.Test.PermissionsTests
{
    public class LocalUserLogoner : IUserLogoner
    {
        public LocalUserLogoner(string name, string password, IWebServer webServer)
        {
            Name = name;
            Password = password;
            WebServer = webServer;

            HttpWebClient httpWebClient = new HttpWebClient();
            WebServerTestBase.LoginAsRoot(httpWebClient, webServer);

            HttpResponseHandler webResponse;

            webResponse = httpWebClient.Post("http://localhost:" + WebServer.Port + "/Users/UserDB?Method=CreateUser",
                new KeyValuePair<string, string>("username", name),
                new KeyValuePair<string, string>("password", password));
            Assert.AreEqual(HttpStatusCode.Created, webResponse.StatusCode, "Bad status code");
        }

        public void Login(HttpWebClient httpWebClient, IWebServer webServer)
        {
            HttpResponseHandler webResponse = httpWebClient.Post(
                "http://localhost:" + webServer.Port + "/Users/UserDB?Method=Login",
                new KeyValuePair<string, string>("username", Name),
                new KeyValuePair<string, string>("password", Password));

            Assert.AreEqual(HttpStatusCode.Accepted, webResponse.StatusCode, "Bad status code");
        }

        /// <summary>
        /// The user's name
        /// </summary>
        public string Name
        {
            get { return _Name; }
            set { _Name = value; }
        }
        private string _Name;

        /// <summary>
        /// The user's password
        /// </summary>
        public string Password
        {
            get { return _Password; }
            set { _Password = value; }
        }
        private string _Password;

        public string WritableDirectory
        {
            get { return "/Users/" + Name; }
        }

        private IWebServer WebServer;
    }
}
