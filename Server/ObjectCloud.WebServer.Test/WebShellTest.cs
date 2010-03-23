// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

using NUnit.Framework;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.WebServer;
using ObjectCloud.WebServer.Implementation;

namespace ObjectCloud.WebServer.Test
{
    [TestFixture]
    public class WebShellTest : WebServerTestBase
    {
        [Test]
        public void TestViewTextFile()
        {
            string newfileName = "TestUseText" + SRandom.Next().ToString() + ".txt";

            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            CreateFile(WebServer, httpWebClient, "/", newfileName, "text");

            HttpWebRequest webRequest = (HttpWebRequest)HttpWebRequest.Create("http://localhost:" + WebServer.Port + "/" + newfileName + "?Method=WriteAll");
            webRequest.Method = "POST";
            webRequest.ContentType = "application/x-www-form-urlencoded";
            webRequest.CookieContainer = httpWebClient.CookieContainer;

            string text = "huriownuifeowb,tw89hu8ofryuovrbywoivujrz,fgersykghvyauofho9fnauwielo";

            byte[] toWrite = Encoding.UTF8.GetBytes(text);

            webRequest.ContentLength = toWrite.Length;

            // Write the request
            webRequest.GetRequestStream().Write(toWrite, 0, toWrite.Length);

            using (HttpWebResponse httpWebResponse = (HttpWebResponse)webRequest.GetResponse())
            {
                Assert.AreEqual(HttpStatusCode.Accepted, httpWebResponse.StatusCode, "Bad status code");
            }

            HttpResponseHandler webResponse = httpWebClient.Get("http://localhost:" + WebServer.Port + "/" + newfileName);

            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual(text, webResponse.AsString(), "Unexpected value");
            Assert.AreEqual("text/plain", webResponse.ContentType, "Unexpected content type");
        }

        [Test]
        public void TestErrorInShellSetting()
        {
            string newfileName = "TestUseText" + SRandom.Next().ToString() + ".badDNE";

            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            CreateFile(WebServer, httpWebClient, "/", newfileName, "text");

            HttpResponseHandler webResponse = httpWebClient.Get("http://localhost:" + WebServer.Port + "/" + newfileName);

            Assert.AreEqual(HttpStatusCode.InternalServerError, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual("ObjectCloud is not configured to handle files of type badDNE", webResponse.AsString(), "Unexpected value");
        }

        public void TestMimeType(string mimeType, string extension)
        {
            string newfileName = "TestMimeType" + SRandom.Next().ToString() + "." + extension;

            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            CreateFile(WebServer, httpWebClient, "/", newfileName, "text");

            HttpWebRequest webRequest = (HttpWebRequest)HttpWebRequest.Create("http://localhost:" + WebServer.Port + "/" + newfileName + "?Method=WriteAll");
            webRequest.CookieContainer = httpWebClient.CookieContainer;
            webRequest.Method = "POST";
            webRequest.ContentType = "application/x-www-form-urlencoded";

            string text = "nurw348nuo48h78t40oghw9phq   98fh587ghr7soiyo578whvntronvaeihrfbow45gn98owvs78zrgh78whgnv8ono8q7iehgrb8h78qofhfb78wehgnow8";

            byte[] toWrite = Encoding.UTF8.GetBytes(text);

            webRequest.ContentLength = toWrite.Length;

            // Write the request
            webRequest.GetRequestStream().Write(toWrite, 0, toWrite.Length);

            using (HttpWebResponse httpWebResponse = (HttpWebResponse)webRequest.GetResponse())
            {
                Assert.AreEqual(HttpStatusCode.Accepted, httpWebResponse.StatusCode, "Bad status code");
            }

            HttpResponseHandler webResponse = httpWebClient.Get("http://localhost:" + WebServer.Port + "/" + newfileName);

            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual(text, webResponse.AsString(), "Unexpected value");
            Assert.AreEqual(mimeType, webResponse.ContentType, "Unexpected content type");
        }

        [Test]
        public void TestMimeTypeTXT()
        {
            TestMimeType("text/plain", "txt");
        }

        [Test]
        public void TestMimeTypeTEXT()
        {
            TestMimeType("text/plain", "text");
        }

        [Test]
        public void TestMimeTypeHTM()
        {
            TestMimeType("text/html", "html");
        }

        [Test]
        public void TestMimeTypeHTML()
        {
            TestMimeType("text/html", "html");
        }

        [Test]
        public void TestMimeTypeXHTML()
        {
            TestMimeType("application/xhtml+xml", "xhtml");
        }

        [Test]
        public void TestMimeTypeXML()
        {
            TestMimeType("text/xml", "xml");
        }

        [Test]
        public void TestEnforcementOfActionPermissions()
        {
            HttpWebClient httpWebClient = new HttpWebClient();

            HttpResponseHandler webResponse = httpWebClient.Get(
                "http://localhost:" + WebServer.Port + "/Users/root");

            Assert.AreEqual(HttpStatusCode.Unauthorized, webResponse.StatusCode, "Bad status code");
        }

        [Test]
        public void TestIgnoreUnknownActionPermissions()
        {
            HttpWebClient httpWebClient = new HttpWebClient();

            HttpResponseHandler webResponse = httpWebClient.Get(
                "http://localhost:" + WebServer.Port + "/Users/root?Action=Abc");

            Assert.AreEqual(HttpStatusCode.InternalServerError, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual("ObjectCloud does not support the action \"Abc\" for files of type \"directory\"", webResponse.AsString(), "Unexpected response");
        }

        [Test]
        public void TestBypassOfActionPermissions()
        {
            HttpWebClient httpWebClient = new HttpWebClient();

            HttpResponseHandler webResponse = httpWebClient.Get(
                "http://localhost:" + WebServer.Port + "/Users/root?BypassActionPermission=true");

            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
        }
		
		[Test]
		public void TestCanGetOpenIdLandingPage()
		{
            HttpWebClient httpWebClient = new HttpWebClient();

            HttpResponseHandler webResponse = httpWebClient.Get(
                "http://localhost:" + WebServer.Port + "/Shell/OpenID/OpenIDLandingPage.wchtml?openid.mode=checkid_setup&openid.trust_root=&openid.identity=http%3a%2f%2flocalhost%3a1080%2fUsers%2froot.user&openid.return_to=http%3a%2f%2flocalhost%3a1080%2fUsers%2fUserDB%3fMethod%3dCompleteOpenIdLogin%26esoid.claimed_id%3dhttp%253a%252f%252flocalhost%253a1080%252fUsers%252froot.user");

            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
		}
		
		/*[Test]
		public void TestSelfLoginWithOpenId()
		{
			HttpWebClient httpWebClient = new HttpWebClient();
			
			LoginAsRoot(httpWebClient);
			
			HttpResponseHandler webResponse = httpWebClient.Post(
				"http://localhost:" + WebServer.Port + "/Users/UserDB?Method=OpenIDLogin",
			    new KeyValuePair<string, string>("openid_url", "http://localhost:" + WebServer.Port + "/Users/root"));
			
			Assert.AreEqual(HttpStatusCode.Redirect, webResponse.StatusCode, "Wrong status code");
			
			webResponse = httpWebClient.Get(webResponse.AsString());

			Assert.AreEqual(HttpStatusCode.Redirect, webResponse.StatusCode, "Wrong status code");

			webResponse = httpWebClient.Get(webResponse.AsString());
			
			Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Wrong status code");
		}*/

        [Test]
        public void TestIndexFile()
        {
            string newfileName = "TestIndexFile" + SRandom.Next().ToString();

            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            CreateFile(WebServer, httpWebClient, "/", newfileName, "directory");
            CreateFile(WebServer, httpWebClient, "/" + newfileName, "text.txt", "text");

            HttpWebRequest webRequest = (HttpWebRequest)HttpWebRequest.Create("http://localhost:" + WebServer.Port + "/" + newfileName + "/text.txt?Method=WriteAll");
            webRequest.Method = "POST";
            webRequest.ContentType = "application/x-www-form-urlencoded";
            webRequest.CookieContainer = httpWebClient.CookieContainer;

            string text = "huriownuifeowb,tw89hu8ofryuovrbywoivujrz,fgersykghvyauofho9fnauwielo";

            byte[] toWrite = Encoding.UTF8.GetBytes(text);

            webRequest.ContentLength = toWrite.Length;

            // Write the request
            webRequest.GetRequestStream().Write(toWrite, 0, toWrite.Length);

            using (HttpWebResponse httpWebResponse = (HttpWebResponse)webRequest.GetResponse())
            {
                Assert.AreEqual(HttpStatusCode.Accepted, httpWebResponse.StatusCode, "Bad status code");
            }

            HttpResponseHandler webResponse = httpWebClient.Post(
                "http://localhost:" + WebServer.Port + "/" + newfileName + "?Method=SetIndexFile",
                new KeyValuePair<string, string>("IndexFile", "text.txt"));

            Assert.AreEqual(HttpStatusCode.Accepted, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual("Index file is now: " + "text.txt", webResponse.AsString(), "Unexpected response");


            webResponse = httpWebClient.Get("http://localhost:" + WebServer.Port + "/" + newfileName);

            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual(text, webResponse.AsString(), "Unexpected value");
            Assert.AreEqual("text/plain", webResponse.ContentType, "Unexpected content type");
        }

        [Test]
        public void TestNoIndexFile()
        {
            string newfileName = "TestNoIndexFile" + SRandom.Next().ToString();

            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            CreateFile(WebServer, httpWebClient, "/", newfileName, "directory");

            HttpResponseHandler webResponse = httpWebClient.Post(
                "http://localhost:" + WebServer.Port + "/" + newfileName + "?Method=SetIndexFile");

            Assert.AreEqual(HttpStatusCode.Accepted, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual("Index file disabled", webResponse.AsString(), "Unexpected response");

            webResponse = httpWebClient.Get(
                "http://localhost:" + WebServer.Port + "/" + newfileName,
                new KeyValuePair<string, string>("Method", "GetIndexFile"));

            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual("No index file", webResponse.AsString(), "Unexpected response");
        }
    }
}