// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

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
    public class LocalUserLogonerForAccessThroughGroup : IUserLogoner
    {
        public LocalUserLogonerForAccessThroughGroup(string username, string password, string groupname, IWebServer webServer)
        {
            UserName = username;
            Password = password;
            GroupName = groupname;
            WebServer = webServer;

            HttpWebClient httpWebClient = new HttpWebClient();
            WebServerTestBase.LoginAsRoot(httpWebClient, webServer);

            HttpResponseHandler webResponse;

            webResponse = httpWebClient.Post("http://localhost:" + WebServer.Port + "/Users/UserDB?Method=CreateUser",
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("password", password),
                new KeyValuePair<string, string>("assignSession", false.ToString()));
            Assert.AreEqual(HttpStatusCode.Created, webResponse.StatusCode, "Bad status code");

            webResponse = httpWebClient.Post("http://localhost:" + WebServer.Port + "/Users/UserDB?Method=CreateGroup",
                new KeyValuePair<string, string>("groupname", groupname),
                new KeyValuePair<string, string>("username", "root"));
            Assert.AreEqual(HttpStatusCode.Created, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual(groupname + " created", webResponse.AsString(), "Unexpected response");

            webResponse = httpWebClient.Get("http://localhost:" + WebServer.Port + "/Users/UserDB",
                new KeyValuePair<string, string>("Method", "AddUserToGroup"),
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("groupname", groupname));
            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual(username + " added to " + groupname, webResponse.AsString(), "Unexpected response");
        }

        public void Login(HttpWebClient httpWebClient, IWebServer webServer)
        {
            HttpResponseHandler webResponse = httpWebClient.Post(
                "http://localhost:" + webServer.Port + "/Users/UserDB?Method=Login",
                new KeyValuePair<string, string>("username", UserName),
                new KeyValuePair<string, string>("password", Password));

            Assert.AreEqual(HttpStatusCode.Accepted, webResponse.StatusCode, "Bad status code");
        }

        /// <summary>
        /// The user's name
        /// </summary>
        public string Name
        {
            get { return GroupName; }
        }

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

        public string UserName
        {
            get { return _UserName; }
            set { _UserName = value; }
        }
        private string _UserName;

        public string GroupName
        {
            get { return _GroupName; }
            set { _GroupName = value; }
        }
        private string _GroupName;

        private IWebServer WebServer;
    }
}
