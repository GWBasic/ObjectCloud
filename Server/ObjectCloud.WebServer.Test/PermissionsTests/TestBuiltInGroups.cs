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
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;
using ObjectCloud.WebServer.Implementation;
using ObjectCloud.WebServer.Test;
using ObjectCloud.Spring.Config;

namespace ObjectCloud.WebServer.Test.PermissionsTests
{
    [TestFixture]
    public class TestBuiltInGroups : HasSecondServer
    {
        protected IUserLogoner LocalUser
        {
            get
            {
                if (null == _LocalUser)
                    _LocalUser = new LocalUserLogoner("local" + SRandom.Next().ToString(), SRandom.Next<long>().ToString(), WebServer);

                return _LocalUser;
            }
        }
        private LocalUserLogoner _LocalUser = null;

        protected IUserLogoner OpenIdUser
        {
            get
            {
                if (null == _OpenIdUser)
                    _OpenIdUser = new OpenIDLogonerThroughObjectCloud("accessor" + SRandom.Next().ToString(), SRandom.Next<long>().ToString(), SecondWebServer);

                return _OpenIdUser;
            }
        }
        private IUserLogoner _OpenIdUser = null;

        public void SetFilePermission(HttpWebClient httpWebClient, string directory, string filename, string username, FilePermissionEnum filePermission)
        {
            HttpResponseHandler webResponse = httpWebClient.Post(
                "http://localhost:" + WebServer.Port + directory + "?Method=SetFilePermission",
                new KeyValuePair<string, string>("FileName", filename),
                new KeyValuePair<string, string>("UserOrGroup", username),
                new KeyValuePair<string, string>("FilePermission", filePermission.ToString()));

            Assert.AreEqual(HttpStatusCode.Accepted, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual("Permission set to " + filePermission.ToString(), webResponse.AsString(), "Unexpected response");
        }

        public void TryRead(HttpWebClient httpWebClient, string directory, string filename, IUserLogoner userLogoner, HttpStatusCode expectedStatus)
        {
            string userName;

            if (null != userLogoner)
            {
                userLogoner.Login(httpWebClient, WebServer);
                userName = userLogoner.Name;
            }
            else
            {
                Logout(httpWebClient);
                userName = "anonymous";
            }

            HttpResponseHandler webResponse = httpWebClient.Get(
                "http://localhost:" + WebServer.Port + directory + "/" + filename);

            Assert.AreEqual(expectedStatus, webResponse.StatusCode, "Bad status code for user: " + userName);
        }

        [Test]
        public void TestEverybody()
        {
            TestPermissions(
                "BuiltInGroups_Everybody" + SRandom.Next().ToString(),
                "everybody",
                HttpStatusCode.OK,
                HttpStatusCode.OK,
                HttpStatusCode.OK);
        }

        [Test]
        public void TestAuthenticatedUsers()
        {
            TestPermissions(
                "BuiltInGroups_AuthenticatedUsers" + SRandom.Next().ToString(),
                "AuthenticatedUsers",
                HttpStatusCode.Unauthorized,
                HttpStatusCode.OK,
                HttpStatusCode.OK);
        }

        [Test]
        public void TestLocalUsers()
        {
            TestPermissions(
                "BuiltInGroups_LocalUsers" + SRandom.Next().ToString(),
                "LocalUsers",
                HttpStatusCode.Unauthorized,
                HttpStatusCode.Unauthorized,
                HttpStatusCode.OK);
        }

        private void TestPermissions(string filename, string groupName, HttpStatusCode anonymousStatus, HttpStatusCode openIdStatus, HttpStatusCode localStatus)
        {
            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            CreateFile(WebServer, httpWebClient, "/", filename, "text");

            SetFilePermission(httpWebClient, "/", filename, groupName, FilePermissionEnum.Read);

            TryRead(httpWebClient, "/", filename, null, anonymousStatus);
            TryRead(httpWebClient, "/", filename, LocalUser, localStatus);
            TryRead(httpWebClient, "/", filename, OpenIdUser, openIdStatus);
        }
    }
}
