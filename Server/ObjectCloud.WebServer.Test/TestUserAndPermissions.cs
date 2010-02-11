// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

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
    public class TestUserAndPermissions : WebServerTestBase
    {
        [Test]
        public void TestNewUser()
        {
            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            HttpResponseHandler webResponse;

            string username = "testuser" + SRandom.Next(100000).ToString();

            webResponse = httpWebClient.Post("http://localhost:" + WebServer.Port + "/Users/UserDB?Method=CreateUser",
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("password", "password"),
			    new KeyValuePair<string, string>("assignSession", true.ToString()));
            Assert.AreEqual(HttpStatusCode.Created, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual(username + " created", webResponse.AsString(), "Unexpected response");

            // Verify that the current user is the newly-created user
            webResponse = httpWebClient.Get("http://localhost:" + WebServer.Port + "/Users/[name].user", new KeyValuePair<string, string>("Method", "GetName"));
            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual(username, webResponse.AsString(), "Unexpected response");
        }

        [Test]
        public void TestNewUserCanLogIn()
        {
            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            HttpResponseHandler webResponse;

            string username = "testuser" + SRandom.Next(100000).ToString();

            webResponse = httpWebClient.Post("http://localhost:" + WebServer.Port + "/Users/UserDB?Method=CreateUser",
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("password", "password"),
			    new KeyValuePair<string, string>("assignSession", true.ToString()));
            Assert.AreEqual(HttpStatusCode.Created, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual(username + " created", webResponse.AsString(), "Unexpected response");

            // Verify that the current user is the newly-created user
            webResponse = httpWebClient.Get("http://localhost:" + WebServer.Port + "/Users/[name].user", new KeyValuePair<string, string>("Method", "GetName"));
            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual(username, webResponse.AsString(), "Unexpected response");

            // Log out
            webResponse = httpWebClient.Get(
                "http://localhost:" + WebServer.Port + "/Users/UserDB",
                new KeyValuePair<string, string>("Method", "Logout"));

            // Log in as new user
            webResponse = httpWebClient.Post(
                "http://localhost:" + WebServer.Port + "/Users/UserDB?Method=Login",
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("password", "password"));

            Assert.AreEqual(HttpStatusCode.Accepted, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual(username + " logged in", webResponse.AsString(), "Unexpected response");

            // Verify that the current user is root
            webResponse = httpWebClient.Get("http://localhost:" + WebServer.Port + "/Users/[name].user", new KeyValuePair<string, string>("Method", "GetName"));
            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual(username, webResponse.AsString(), "Unexpected response");
        }
		
		[Test]
		public void TestCanCreateGroup()
		{
            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            HttpResponseHandler webResponse;

            string groupname = "testgroup" + SRandom.Next(100000).ToString();

            webResponse = httpWebClient.Post("http://localhost:" + WebServer.Port + "/Users/UserDB?Method=CreateGroup",
                new KeyValuePair<string, string>("groupname", groupname));
            Assert.AreEqual(HttpStatusCode.Created, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual(groupname + " created", webResponse.AsString(), "Unexpected response");
		}
		
		[Test]
		public void TestCanDeleteGroup()
		{
            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            HttpResponseHandler webResponse;

            string groupname = "testgroup" + SRandom.Next(100000).ToString();

            webResponse = httpWebClient.Post("http://localhost:" + WebServer.Port + "/Users/UserDB?Method=CreateGroup",
                new KeyValuePair<string, string>("groupname", groupname),
                new KeyValuePair<string, string>("username", "root"));
            Assert.AreEqual(HttpStatusCode.Created, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual(groupname + " created", webResponse.AsString(), "Unexpected response");

            webResponse = httpWebClient.Post("http://localhost:" + WebServer.Port + "/Users/UserDB?Method=DeleteGroup",
                new KeyValuePair<string, string>("groupname", groupname));
            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual(groupname + " deleted", webResponse.AsString(), "Unexpected response");
		}
		
		[Test]
		public void TestAddUserToGroup()
		{
            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            HttpResponseHandler webResponse;

            string groupname = "testgroup" + SRandom.Next(100000).ToString();

            webResponse = httpWebClient.Post("http://localhost:" + WebServer.Port + "/Users/UserDB?Method=CreateGroup",
                new KeyValuePair<string, string>("groupname", groupname),
                new KeyValuePair<string, string>("username", "root"));
            Assert.AreEqual(HttpStatusCode.Created, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual(groupname + " created", webResponse.AsString(), "Unexpected response");

            string username = "testuser" + SRandom.Next(100000).ToString();

            webResponse = httpWebClient.Post("http://localhost:" + WebServer.Port + "/Users/UserDB?Method=CreateUser",
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("password", "password"),
			    new KeyValuePair<string, string>("assignSession", false.ToString()));
            Assert.AreEqual(HttpStatusCode.Created, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual(username + " created", webResponse.AsString(), "Unexpected response");

            webResponse = httpWebClient.Post("http://localhost:" + WebServer.Port + "/Users/UserDB?Method=AddUserToGroup",
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("groupname", groupname));
            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual(username + " added to " + groupname, webResponse.AsString(), "Unexpected response");
			
			webResponse = httpWebClient.Get("http://localhost:" + WebServer.Port + "/Users/UserDB",
				new KeyValuePair<string, string>("Method", "GetUsersGroups"),
			    new KeyValuePair<string, string>("username", username));
            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
			
			JsonReader jsonReader = webResponse.AsJsonReader();
			
			object[] result = jsonReader.Deserialize<object[]>();
			
			Assert.AreEqual(1, result.Length, "Wrong number of groups");
			Assert.IsInstanceOfType(typeof(Dictionary<string, object>), result[0], "Wrong type decoded");
			Dictionary<string, object> group = (Dictionary<string, object>)result[0];
			
			Assert.AreEqual(groupname, group["Name"], "Wrong name returned");
		}
		
		[Test]
		public void TestRemoveUserFromGroup()
		{
            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            HttpResponseHandler webResponse;

            string groupname = "testgroup" + SRandom.Next(100000).ToString();

            webResponse = httpWebClient.Post("http://localhost:" + WebServer.Port + "/Users/UserDB?Method=CreateGroup",
                new KeyValuePair<string, string>("groupname", groupname),
                new KeyValuePair<string, string>("username", "root"));
            Assert.AreEqual(HttpStatusCode.Created, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual(groupname + " created", webResponse.AsString(), "Unexpected response");

            string username = "testuser" + SRandom.Next(100000).ToString();

            webResponse = httpWebClient.Post("http://localhost:" + WebServer.Port + "/Users/UserDB?Method=CreateUser",
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("password", "password"),
			    new KeyValuePair<string, string>("assignSession", false.ToString()));
            Assert.AreEqual(HttpStatusCode.Created, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual(username + " created", webResponse.AsString(), "Unexpected response");

            webResponse = httpWebClient.Post("http://localhost:" + WebServer.Port + "/Users/UserDB?Method=AddUserToGroup",
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("groupname", groupname));
            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual(username + " added to " + groupname, webResponse.AsString(), "Unexpected response");
			
			webResponse = httpWebClient.Get("http://localhost:" + WebServer.Port + "/Users/UserDB",
				new KeyValuePair<string, string>("Method", "GetUsersGroups"),
			    new KeyValuePair<string, string>("username", username));
            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
			
			JsonReader jsonReader = webResponse.AsJsonReader();
			
			object[] result = jsonReader.Deserialize<object[]>();
			
			Assert.AreEqual(1, result.Length, "Wrong number of groups");
			Assert.IsInstanceOfType(typeof(Dictionary<string, object>), result[0], "Wrong type decoded");
			Dictionary<string, object> group = (Dictionary<string, object>)result[0];
			
			Assert.AreEqual(groupname, group["Name"], "Wrong name returned");

            webResponse = httpWebClient.Post("http://localhost:" + WebServer.Port + "/Users/UserDB?Method=RemoveUserFromGroup",
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("groupname", groupname));
            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual(username + " removed from " + groupname, webResponse.AsString(), "Unexpected response");
			
			webResponse = httpWebClient.Get("http://localhost:" + WebServer.Port + "/Users/UserDB",
				new KeyValuePair<string, string>("Method", "GetUsersGroups"),
			    new KeyValuePair<string, string>("username", username));
            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
			
			jsonReader = webResponse.AsJsonReader();
			
			result = jsonReader.Deserialize<object[]>();
			
			Assert.AreEqual(0, result.Length, "Wrong number of groups");
		}

        [Test]
        public void TestSetPassword()
        {
            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            HttpResponseHandler webResponse;

            string username = "testuser" + SRandom.Next(100000).ToString();

            webResponse = httpWebClient.Post("http://localhost:" + WebServer.Port + "/Users/UserDB?Method=CreateUser",
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("password", "password"),
                new KeyValuePair<string, string>("assignSession", true.ToString()));
            Assert.AreEqual(HttpStatusCode.Created, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual(username + " created", webResponse.AsString(), "Unexpected response");

            // Verify that the current user is the newly-created user
            webResponse = httpWebClient.Get("http://localhost:" + WebServer.Port + "/Users/[name].user", new KeyValuePair<string, string>("Method", "GetName"));
            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual(username, webResponse.AsString(), "Unexpected response");

            webResponse = httpWebClient.Post("http://localhost:" + WebServer.Port + "/Users/" + username + ".user?Method=SetPassword",
                new KeyValuePair<string, string>("OldPassword", "password"),
                new KeyValuePair<string, string>("NewPassword", "22password"));

            Assert.AreEqual(HttpStatusCode.Accepted, webResponse.StatusCode, "Wrong status code when calling SetPassword");
            Assert.AreEqual("Password changed", webResponse.AsString(), "Wrong response text when changing the password");

            Logout(httpWebClient);

            // Log in as root
            webResponse = httpWebClient.Post(
                "http://localhost:" + WebServer.Port + "/Users/UserDB?Method=Login",
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("password", "22password"));

            Assert.AreEqual(HttpStatusCode.Accepted, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual(username + " logged in", webResponse.AsString(), "Unexpected response");

        }
    }
}
