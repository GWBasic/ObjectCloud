// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Xml;

using NUnit.Framework;

using JsonFx.Json;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;
using ObjectCloud.WebServer.Implementation;
using ObjectCloud.WebServer.Test;
using ObjectCloud.Spring.Config;

namespace ObjectCloud.WebServer.Test.PermissionsTests
{
    public class OpenIDLogonerThroughObjectCloud : IUserLogoner
    {
        public OpenIDLogonerThroughObjectCloud(string name, string password, IWebServer webServer, IWebServer secondWebServer)
            : this(name, password, secondWebServer)
        {
            HttpWebClient httpWebClient = new HttpWebClient();
            WebServerTestBase.LoginAsRoot(httpWebClient, webServer);

            HttpResponseHandler webResponse = httpWebClient.Post(
                "http://localhost:" + webServer.Port + "/?Method=CreateFile",
                new KeyValuePair<string, string>("FileName", name),
                new KeyValuePair<string, string>("FileType", "directory"));

            Assert.AreEqual(HttpStatusCode.Created, webResponse.StatusCode, "Bad status code");

            webResponse = httpWebClient.Post(
                "http://localhost:" + webServer.Port + "/?Method=SetFilePermission",
                new KeyValuePair<string, string>("FileName", name),
                new KeyValuePair<string, string>("UserOrGroup", Name),
                new KeyValuePair<string, string>("FilePermission", FilePermissionEnum.Administer.ToString()));

            Assert.AreEqual(HttpStatusCode.Accepted, webResponse.StatusCode, "Bad status code");

            _WritableDirectory = "/" + _Name + "/";
        }

        public OpenIDLogonerThroughObjectCloud(string name, string password, IWebServer secondWebServer)
        {
            Name = name;
            Password = password;
            SecondWebServer = secondWebServer;

            HttpWebClient httpWebClient = new HttpWebClient();
            WebServerTestBase.LoginAsRoot(httpWebClient, SecondWebServer);

            HttpResponseHandler webResponse;

            webResponse = httpWebClient.Post("http://localhost:" + SecondWebServer.Port + "/Users/UserDB?Method=CreateUser",
                new KeyValuePair<string, string>("username", name),
                new KeyValuePair<string, string>("password", password));
            Assert.AreEqual(HttpStatusCode.Created, webResponse.StatusCode, "Bad status code");

            webResponse = httpWebClient.Get("http://localhost:" + SecondWebServer.Port + "/Users/" + name + ".user");
            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");

            Thread.Sleep(2000);
        }

        public void Login(HttpWebClient httpWebClient, IWebServer webServer)
        {
            HttpResponseHandler webResponse = httpWebClient.Post(
                "http://localhost:" + webServer.Port + "/Users/UserDB?Method=OpenIDLogin",
                new KeyValuePair<string, string>("openid_url", Name));

            // The returned page should just have a "Submit" button in order to continue OpenID
            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");

            Uri responseUri = webResponse.HttpWebResponse.ResponseUri;
            RequestParameters openIdGetParameters = new RequestParameters(responseUri.Query.Substring(1));

            Dictionary<string, string> formArgs = new Dictionary<string, string>(openIdGetParameters);
            formArgs["password"] = Password;
            formArgs.Remove("Method");

            webResponse = httpWebClient.Post(
                "http://localhost:" + SecondWebServer.Port + "/Users/UserDB?Method=ProvideOpenID", formArgs);

            Assert.AreEqual(HttpStatusCode.Accepted, webResponse.StatusCode);
        }

        /// <summary>
        /// The user's name
        /// </summary>
        public string Name
        {
            get { return "localhost:" + SecondWebServer.Port + "/Users/" + _Name + ".user"; }
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
            get { return _WritableDirectory; }
        }
        private string _WritableDirectory = null;

        private IWebServer SecondWebServer;
    }
}
