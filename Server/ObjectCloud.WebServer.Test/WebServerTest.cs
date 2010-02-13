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

namespace ObjectCloud.WebServer.Test
{
    [TestFixture]
    public class WebServerTest : WebServerTestBase
    {
        [Test]
        public void TestCreateDirectory()
        {
            string newDirName = "TestCreateDirectory" + SRandom.Next().ToString();

            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            CreateFile(WebServer, httpWebClient, "/", newDirName, "directory");
        }

        [Test]
        public void TestCreateSubdirectory()
        {
            string newDirName = "TestCreateSubdirectory" + SRandom.Next().ToString();

            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            CreateFile(WebServer, httpWebClient, "/", newDirName, "directory");
            CreateFile(WebServer, httpWebClient, "/" + newDirName, "A", "directory");
            CreateFile(WebServer, httpWebClient, "/" + newDirName + "/A", "B", "directory");
            CreateFile(WebServer, httpWebClient, "/" + newDirName + "/A/B", "C", "directory");
        }

        [Test]
        public void TestCreateNameValuePairs()
        {
            string newfileName = "TestCreateNameValuePairs" + SRandom.Next().ToString();

            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            CreateFile(WebServer, httpWebClient, "/", newfileName, "name-value");
        }

        [Test]
        public void TestUseNameValuePairs()
        {
            string newfileName = "TestUseNameValuePairs" + SRandom.Next().ToString();

            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            CreateFile(WebServer, httpWebClient, "/", newfileName, "name-value");

            HttpResponseHandler webResponse = httpWebClient.Post(
                "http://localhost:" + WebServer.Port + "/" + newfileName + "?Method=Set",
                new KeyValuePair<string, string>("Name", "theName"),
                new KeyValuePair<string, string>("Value", "theValue"));

            Assert.AreEqual(HttpStatusCode.Accepted, webResponse.StatusCode, "Bad status code");

            webResponse = httpWebClient.Get(
                "http://localhost:" + WebServer.Port + "/" + newfileName,
                new KeyValuePair<string, string>("Method", "Get"),
                new KeyValuePair<string, string>("Name", "theName"));

            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual("theValue", webResponse.AsString(), "Unexpected value");

            webResponse = httpWebClient.Post(
                "http://localhost:" + WebServer.Port + "/" + newfileName + "?Method=Set",
                new KeyValuePair<string, string>("Name", "theName"),
                new KeyValuePair<string, string>("Value", "theValue222"));

            Assert.AreEqual(HttpStatusCode.Accepted, webResponse.StatusCode, "Bad status code");

            webResponse = httpWebClient.Get(
                "http://localhost:" + WebServer.Port + "/" + newfileName,
                new KeyValuePair<string, string>("Method", "Get"),
                new KeyValuePair<string, string>("Name", "theName"));

            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual("theValue222", webResponse.AsString(), "Unexpected value");
        }

        [Test]
        public void TestGetSetAllNameValuePairs()
        {
            string newfileName = "TestGetSetAllNameValuePairs" + SRandom.Next().ToString();

            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            CreateFile(WebServer, httpWebClient, "/", newfileName, "name-value");

            // set the values

            Dictionary<string, string> nameValuePairs = new Dictionary<string, string>();

            nameValuePairs["one"] = "qwe";
            nameValuePairs["two"] = "rty";
            nameValuePairs["three"] = "uio";

            HttpResponseHandler webResponse = httpWebClient.Post(
                "http://localhost:" + WebServer.Port + "/" + newfileName + "?Method=SetAll",
                nameValuePairs);

            Assert.AreEqual(HttpStatusCode.Accepted, webResponse.StatusCode, "Bad status code");

            webResponse = httpWebClient.Get(
                "http://localhost:" + WebServer.Port + "/" + newfileName,
                new KeyValuePair<string, string>("Method", "GetAll"));

            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
            IDictionary<string, string> nameValuePairsFromServer = webResponse.AsJsonReader().Deserialize<Dictionary<string, string>>();

            Assert.IsTrue(
                DictionaryFunctions.Equals<string, string>(nameValuePairs, nameValuePairsFromServer),
                "Server did not respond with the correct name value pairs");
        }
        
        [Test]
        public void TestCopyFile()
        {
            string sourceFilename = "TestCopyFile" + SRandom.Next().ToString();
            string destinationFilename = "Destination" + SRandom.Next().ToString();

            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            CreateFile(WebServer, httpWebClient, "/", sourceFilename, "text");

            HttpWebRequest webRequest = (HttpWebRequest)HttpWebRequest.Create("http://localhost:" + WebServer.Port + "/" + sourceFilename + "?Method=WriteAll");
            webRequest.Method = "POST";
            webRequest.ContentType = "application/x-www-form-urlencoded";
            webRequest.CookieContainer = httpWebClient.CookieContainer;

            string text = "the\ntext\nto\nsave";

            byte[] toWrite = Encoding.UTF8.GetBytes(text);

            webRequest.ContentLength = toWrite.Length;

            // Write the request
            webRequest.GetRequestStream().Write(toWrite, 0, toWrite.Length);

            using (HttpWebResponse httpWebResponse = (HttpWebResponse)webRequest.GetResponse())
            {
                Assert.AreEqual(HttpStatusCode.Accepted, httpWebResponse.StatusCode, "Bad status code");
            }

            HttpResponseHandler webResponse = httpWebClient.Post(
                "http://localhost:" + WebServer.Port + "/?Method=CopyFile",
                new KeyValuePair<string, string>("SourceFilename", sourceFilename),
                new KeyValuePair<string, string>("DestinationFilename", destinationFilename));

            Assert.AreEqual(HttpStatusCode.Created, webResponse.StatusCode, "Wrong status code");
            Assert.AreEqual("Copied", webResponse.AsString(), "Unexpected response");

            webResponse = httpWebClient.Get(
                "http://localhost:" + WebServer.Port + "/" + destinationFilename,
                new KeyValuePair<string, string>("Method", "ReadAll"));

            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual(text, webResponse.AsString(), "Unexpected value");
        }

        [Test]
        public void TestLoginLogout()
        {
            HttpWebClient httpWebClient = new HttpWebClient();
            HttpResponseHandler webResponse;

            // Check that the default user is anonymous
            webResponse = httpWebClient.Get("http://localhost:" + WebServer.Port + "/Users/[name].user", new KeyValuePair<string, string>("Method", "GetName"));
            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual("anonymous", webResponse.AsString(), "Unexpected response");

            // Log in as root
            webResponse = httpWebClient.Post(
                "http://localhost:" + WebServer.Port + "/Users/UserDB?Method=Login",
                new KeyValuePair<string, string>("username", "root"),
                new KeyValuePair<string, string>("password", "root"));

            Assert.AreEqual(HttpStatusCode.Accepted, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual("root logged in", webResponse.AsString(), "Unexpected response");

            // Verify that the current user is root
            webResponse = httpWebClient.Get("http://localhost:" + WebServer.Port + "/Users/[name].user", new KeyValuePair<string, string>("Method", "GetName"));
            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual("root", webResponse.AsString(), "Unexpected response");

            Logout(httpWebClient);

            webResponse = httpWebClient.Get("http://localhost:" + WebServer.Port + "/Users/[name].user", new KeyValuePair<string, string>("Method", "GetName"));
            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual("anonymous", webResponse.AsString(), "Unexpected response");
        }

        [Test]
        public void TestBadPassword()
        {
            HttpWebClient httpWebClient = new HttpWebClient();

            HttpResponseHandler webResponse = httpWebClient.Post(
                    "http://localhost:" + WebServer.Port + "/Users/UserDB?Method=Login",
                    new KeyValuePair<string, string>("username", "root"),
                    new KeyValuePair<string, string>("password", "bad"));

            Assert.AreEqual(HttpStatusCode.Unauthorized, webResponse.StatusCode, "Wrong status");
            Assert.AreEqual("Bad Password", webResponse.AsString(), "Unexpected response");
        }

        [Test]
        public void TestUnknownUser()
        {
            HttpWebClient httpWebClient = new HttpWebClient();

            HttpResponseHandler webResponse = httpWebClient.Post(
                "http://localhost:" + WebServer.Port + "/Users/UserDB?Method=Login",
                new KeyValuePair<string, string>("username", "unknown"),
                new KeyValuePair<string, string>("password", "bad"));

            Assert.AreEqual(HttpStatusCode.NotFound, webResponse.StatusCode, "Wrong status");
            Assert.AreEqual("Unknown user", webResponse.AsString(), "Unexpected response");
        }

        [Test]
        public void TestErrorWhenCreatingDuplicateFile()
        {
            string newfileName = "TestDuplicate" + SRandom.Next().ToString();

            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            CreateFile(WebServer, httpWebClient, "/", newfileName, "text");

            CreateFile(WebServer, httpWebClient, "/", newfileName, "text", HttpStatusCode.Conflict);
        }

        [Test]
        public void Test404OnBadFile()
        {
            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            HttpResponseHandler webResponse = httpWebClient.Get(
                "http://localhost:" + WebServer.Port + "/doesnotexist.html");

            Assert.AreEqual(HttpStatusCode.NotFound, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual("/doesnotexist.html does not exist", webResponse.AsString(), "Unexpected response");
        }

        [Test]
        public void TestListFilesInDirectory()
        {
            string newDirName = "TestListFilesInDirectory" + SRandom.Next().ToString();

            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            CreateFile(WebServer, httpWebClient, "/", newDirName, "directory");

            CreateFile(WebServer, httpWebClient, "/" + newDirName, "A", "directory");

            CreateFile(WebServer, httpWebClient, "/" + newDirName, "B", "directory");

            CreateFile(WebServer, httpWebClient, "/" + newDirName, "C", "directory");

            HttpResponseHandler webResponse = httpWebClient.Get(
                "http://localhost:" + WebServer.Port + "/" + newDirName,
                new KeyValuePair<string, string>("Method", "ListFiles"));

            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
            //string contents = webResponse.AsString();
            //File.WriteAllText("Z:\\andrewrondeau On My Mac\\dircontents.json", contents);
            JsonReader jsonReader = webResponse.AsJsonReader();

            Dictionary<string, object>[] files = jsonReader.Deserialize<Dictionary<string, object>[]>();

            Assert.AreEqual(3, files.Length, "Wrong number of files returned");

            // index by name
            Dictionary<string, Dictionary<string, object>> filesByName = new Dictionary<string, Dictionary<string, object>>();
            foreach (Dictionary<string, object> file in files)
                filesByName[file["Filename"].ToString()] = file;

            Assert.IsTrue(filesByName.ContainsKey("A"), "File A missing");
            Assert.AreEqual("directory", filesByName["A"]["TypeId"], "wrong file type");
            Assert.IsNotNull(filesByName["A"]["FileId"], "file ID is missing");
            Assert.IsNotNull(filesByName["A"]["OwnerId"], "owner ID is missing");

            Assert.IsTrue(filesByName.ContainsKey("B"), "File C missing");
            Assert.AreEqual("directory", filesByName["B"]["TypeId"], "wrong file type");
            Assert.IsNotNull(filesByName["B"]["FileId"], "file ID is missing");
            Assert.IsNotNull(filesByName["B"]["OwnerId"], "owner ID is missing");

            Assert.IsTrue(filesByName.ContainsKey("C"), "File C missing");
            Assert.AreEqual("directory", filesByName["C"]["TypeId"], "wrong file type");
            Assert.IsNotNull(filesByName["C"]["FileId"], "file ID is missing");
            Assert.IsNotNull(filesByName["C"]["OwnerId"], "owner ID is missing");
        }

        [Test]
        public void TestIsFilePresent()
        {
            string newfileName = "TestIsFilePresent" + SRandom.Next().ToString();

            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            CreateFile(WebServer, httpWebClient, "/", newfileName, "text");

            HttpResponseHandler webResponse = httpWebClient.Get(
                "http://localhost:" + WebServer.Port + "/",
                new KeyValuePair<string, string>("Method", "IsFilePresent"),
                new KeyValuePair<string, string>("FileName", newfileName));

            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual("true", webResponse.AsString(), "Unexpected response");

            webResponse = httpWebClient.Get(
                "http://localhost:" + WebServer.Port + "/",
                new KeyValuePair<string, string>("Method", "IsFilePresent"),
                new KeyValuePair<string, string>("FileName", "dne" + newfileName));

            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual("false", webResponse.AsString(), "Unexpected response");
        }

        [Test]
        public void TestSetFilePermission()
        {
            string newfileName = "TestSetFilePermission" + SRandom.Next().ToString();

            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            CreateFile(WebServer, httpWebClient, "/", newfileName, "text");
            
            HttpWebRequest webRequest = (HttpWebRequest)HttpWebRequest.Create("http://localhost:" + WebServer.Port + "/" + newfileName + "?Method=WriteAll");
            webRequest.Method = "POST";
            webRequest.ContentType = "application/x-www-form-urlencoded";
            webRequest.CookieContainer = httpWebClient.CookieContainer;

            string text = "the\ntext\nto\nsave";

            byte[] toWrite = Encoding.UTF8.GetBytes(text);

            webRequest.ContentLength = toWrite.Length;

            // Write the request
            Stream requestStream = webRequest.GetRequestStream();
			requestStream.Write(toWrite, 0, toWrite.Length);
			requestStream.Flush();
			requestStream.Close();

            HttpResponseHandler webResponse = new HttpResponseHandler((HttpWebResponse)webRequest.GetResponse(), webRequest);
			Assert.AreEqual(HttpStatusCode.Accepted, webResponse.StatusCode, "Wrong status code for saving a text file");
			Assert.AreEqual("Saved", webResponse.AsString(), "Text file not saved");

			// Make sure that the file was written correctly
            webResponse = httpWebClient.Get(
                "http://localhost:" + WebServer.Port + "/" + newfileName,
                new KeyValuePair<string, string>("Method", "ReadAll"));

            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Wrong status code");
            Assert.AreEqual(text, webResponse.AsString(), "Wrong text sent");

            Logout(httpWebClient);

            webResponse = httpWebClient.Get(
                "http://localhost:" + WebServer.Port + "/" + newfileName,
                new KeyValuePair<string, string>("Method", "ReadAll"));

            Assert.AreEqual(HttpStatusCode.Unauthorized, webResponse.StatusCode, "Wrong status code");
            
            LoginAsRoot(httpWebClient);

            webResponse = httpWebClient.Post(
                "http://localhost:" + WebServer.Port + "/?Method=SetFilePermission",
                new KeyValuePair<string, string>("FileName", newfileName),
                new KeyValuePair<string, string>("UserOrGroupId", Guid.Empty.ToString()),
                new KeyValuePair<string, string>("FilePermission", "Read"));

            Assert.AreEqual(HttpStatusCode.Accepted, webResponse.StatusCode, "Wrong status code");

            Logout(httpWebClient);

            webResponse = httpWebClient.Get(
                "http://localhost:" + WebServer.Port + "/" + newfileName,
                new KeyValuePair<string, string>("Method", "ReadAll"));

            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Wrong status code");
            Assert.AreEqual(text, webResponse.AsString(), "Wrong text sent");
        }


        [Test]
        public void TestRename()
        {
            string oldFileName = "TestRename" + SRandom.Next().ToString();
            string newFileName = oldFileName + "rened";

            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            CreateFile(WebServer, httpWebClient, "/", oldFileName, "text");

            HttpResponseHandler webResponse = httpWebClient.Post(
                "http://localhost:" + WebServer.Port + "/?Method=RenameFile",
                new KeyValuePair<string, string>("OldFileName", oldFileName),
                new KeyValuePair<string, string>("NewFileName", newFileName));

            Assert.AreEqual(HttpStatusCode.Accepted, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual("File Renamed", webResponse.AsString(), "Unexpected response");

            webResponse = httpWebClient.Get(
                "http://localhost:" + WebServer.Port + "/",
                new KeyValuePair<string, string>("Method", "IsFilePresent"),
                new KeyValuePair<string, string>("FileName", newFileName));

            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual("true", webResponse.AsString(), "Unexpected response");

            webResponse = httpWebClient.Get(
                "http://localhost:" + WebServer.Port + "/",
                new KeyValuePair<string, string>("Method", "IsFilePresent"),
                new KeyValuePair<string, string>("FileName", oldFileName));

            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual("false", webResponse.AsString(), "Unexpected response");
        }
    }
}