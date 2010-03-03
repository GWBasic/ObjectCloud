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
    public abstract class PermissionTest : WebServerTestBase
    {
        protected override void DoAdditionalSetup()
        {
            // Make sure both users are fully-constructed
            Console.WriteLine(Owner.Name);
            Console.WriteLine(Accessor.Name);

            base.DoAdditionalSetup();
        }

        /// <summary>
        /// This user will always own the files
        /// </summary>
        protected abstract IUserLogoner Owner { get; }

        /// <summary>
        /// This user will always read the files
        /// </summary>
        protected abstract IUserLogoner Accessor { get; }

        public void CreateFile(HttpWebClient httpWebClient, string directory, string filename, string text)
        {
            HttpResponseHandler webResponse = httpWebClient.Post(
                "http://localhost:" + WebServer.Port + directory + "?Method=CreateFile",
                new KeyValuePair<string, string>("FileName", filename),
                new KeyValuePair<string, string>("FileType", "text"));

            Assert.AreEqual(HttpStatusCode.Created, webResponse.StatusCode, "Bad status code");

            WriteFile(httpWebClient, directory, filename, text);
        }

        public void WriteFile(HttpWebClient httpWebClient, string directory, string filename, string text)
        {
            HttpWebRequest webRequest = (HttpWebRequest)HttpWebRequest.Create("http://localhost:" + WebServer.Port + "/" + directory + "/" + filename + "?Method=WriteAll");
            webRequest.Method = "POST";
            webRequest.ContentType = "application/x-www-form-urlencoded";
            webRequest.CookieContainer = httpWebClient.CookieContainer;

            byte[] toWrite = Encoding.UTF8.GetBytes(text);
            webRequest.ContentLength = toWrite.Length;

            // Write the request
            webRequest.GetRequestStream().Write(toWrite, 0, toWrite.Length);

            using (HttpWebResponse httpWebResponse = (HttpWebResponse)webRequest.GetResponse())
                Assert.AreEqual(HttpStatusCode.Accepted, httpWebResponse.StatusCode, "Bad status code");
        }

        public void SetFilePermission(HttpWebClient httpWebClient, string directory, string filename, string username, FilePermissionEnum filePermission)
        {
            HttpResponseHandler webResponse = httpWebClient.Post(
                "http://localhost:" + WebServer.Port + "" + directory + "?Method=SetFilePermission",
                new KeyValuePair<string, string>("FileName", filename),
                new KeyValuePair<string, string>("UserOrGroup", username),
                new KeyValuePair<string, string>("FilePermission", filePermission.ToString()));

            Assert.AreEqual(HttpStatusCode.Accepted, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual("Permission set to " + filePermission.ToString(), webResponse.AsString(), "Unexpected response");
        }

        public void TestRead(HttpWebClient httpWebClient, string directory, string filename, string text)
        {
            HttpResponseHandler webResponse = httpWebClient.Get("http://localhost:" + WebServer.Port + "/" + directory + "/" + filename);

            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual(text, webResponse.AsString(), "Unexpected response");
        }

		// These are supposed to be directly called by NUnit, but some weird behavior in Mono means that each class has to address them directly
        //[Test]
        public void TestRead()
        {
            // Create the file
            string newfileName = "Permissions_TestRead" + SRandom.Next().ToString();
            string text = "test for permissions";

            HttpWebClient httpWebClient = new HttpWebClient();
            Owner.Login(httpWebClient, WebServer);

            CreateFile(httpWebClient, Owner.WritableDirectory, newfileName, text);
            SetFilePermission(httpWebClient, Owner.WritableDirectory, newfileName, Accessor.Name, FilePermissionEnum.Read);

            httpWebClient = new HttpWebClient();
            Accessor.Login(httpWebClient, WebServer);

            TestRead(httpWebClient, Owner.WritableDirectory, newfileName, text);
        }

        //[Test]
        public void TestWrite()
        {
            // Create the file
            string newfileName = "Permissions_TestWrite" + SRandom.Next().ToString();
            string text = "test for permissions";

            HttpWebClient httpWebClient = new HttpWebClient();
            Owner.Login(httpWebClient, WebServer);

            CreateFile(httpWebClient, Owner.WritableDirectory, newfileName, text);
            SetFilePermission(httpWebClient, Owner.WritableDirectory, newfileName, Accessor.Name, FilePermissionEnum.Write);

            httpWebClient = new HttpWebClient();
            Accessor.Login(httpWebClient, WebServer);

            TestRead(httpWebClient, Owner.WritableDirectory, newfileName, text);

            text = "changed text";

            WriteFile(httpWebClient, Owner.WritableDirectory, newfileName, text);

            Owner.Login(httpWebClient, WebServer);
            TestRead(httpWebClient, Owner.WritableDirectory, newfileName, text);
        }
    }
}
