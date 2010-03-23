// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

using NUnit.Framework;

using JsonFx.Json;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.WebServer;
using ObjectCloud.WebServer.Implementation;

namespace ObjectCloud.WebServer.Test
{
    [TestFixture]
    public class WebComponentTest : WebServerTestBase
    {
        [Test]
        public void TestWebComponentSanity()
        {
            string newfileName = "TestWebComponentSanity" + SRandom.Next().ToString();

            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            CreateFile(WebServer, httpWebClient, "/", newfileName, "text");

            HttpWebRequest webRequest = (HttpWebRequest)HttpWebRequest.Create("http://localhost:" + WebServer.Port + "/" + newfileName + "?Method=WriteAll");
            webRequest.Method = "POST";
            webRequest.ContentType = "application/x-www-form-urlencoded";
            webRequest.CookieContainer = httpWebClient.CookieContainer;

            string text = "inside the component";

            byte[] toWrite = Encoding.UTF8.GetBytes(text);

            webRequest.ContentLength = toWrite.Length;

            // Write the request
            webRequest.GetRequestStream().Write(toWrite, 0, toWrite.Length);

            using (HttpWebResponse httpWebResponse = (HttpWebResponse)webRequest.GetResponse())
            {
                Assert.AreEqual(HttpStatusCode.Accepted, httpWebResponse.StatusCode, "Bad status code");
            }

            string componentfileName = "TestWebComponentSanity" + SRandom.Next().ToString();

            httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            CreateFile(WebServer, httpWebClient, "/", componentfileName, "text");

            webRequest = (HttpWebRequest)HttpWebRequest.Create("http://localhost:" + WebServer.Port + "/" + componentfileName + "?Method=WriteAll");
            webRequest.Method = "POST";
            webRequest.ContentType = "application/x-www-form-urlencoded";
            webRequest.CookieContainer = httpWebClient.CookieContainer;

            text = "xxx <? WebComponent(\"/" + newfileName + "?Method=ReadAll\") ?> xxx";

            toWrite = Encoding.UTF8.GetBytes(text);

            webRequest.ContentLength = toWrite.Length;

            // Write the request
            webRequest.GetRequestStream().Write(toWrite, 0, toWrite.Length);

            using (HttpWebResponse httpWebResponse = (HttpWebResponse)webRequest.GetResponse())
            {
                Assert.AreEqual(HttpStatusCode.Accepted, httpWebResponse.StatusCode, "Bad status code");
            }

            HttpResponseHandler webResponse = httpWebClient.Get(
                "http://localhost:" + WebServer.Port + "/" + componentfileName,
                new KeyValuePair<string, string>("Method", "ResolveComponents"));

            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual("xxx inside the component xxx", webResponse.AsString(), "Unexpected value");
        }

        [Test]
        public void TestWebComponent_GET()
        {
            string newfileName = "TestWebComponent_GET" + SRandom.Next().ToString();

            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            CreateFile(WebServer, httpWebClient, "/", newfileName, "text");

            HttpWebRequest webRequest = (HttpWebRequest)HttpWebRequest.Create("http://localhost:" + WebServer.Port + "/" + newfileName + "?Method=WriteAll");
            webRequest.Method = "POST";
            webRequest.ContentType = "application/x-www-form-urlencoded";
            webRequest.CookieContainer = httpWebClient.CookieContainer;

            string text = "inside the component";

            byte[] toWrite = Encoding.UTF8.GetBytes(text);

            webRequest.ContentLength = toWrite.Length;

            // Write the request
            webRequest.GetRequestStream().Write(toWrite, 0, toWrite.Length);

            using (HttpWebResponse httpWebResponse = (HttpWebResponse)webRequest.GetResponse())
            {
                Assert.AreEqual(HttpStatusCode.Accepted, httpWebResponse.StatusCode, "Bad status code");
            }

            string componentfileName = "TestWebComponentSanity" + SRandom.Next().ToString();

            httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            CreateFile(WebServer, httpWebClient, "/", componentfileName, "text");

            webRequest = (HttpWebRequest)HttpWebRequest.Create("http://localhost:" + WebServer.Port + "/" + componentfileName + "?Method=WriteAll");
            webRequest.Method = "POST";
            webRequest.ContentType = "application/x-www-form-urlencoded";
            webRequest.CookieContainer = httpWebClient.CookieContainer;

            text = "xxx <? WebComponent($_GET[\"Filename\"] . \"?Method=ReadAll\") ?> xxx";

            toWrite = Encoding.UTF8.GetBytes(text);

            webRequest.ContentLength = toWrite.Length;

            // Write the request
            webRequest.GetRequestStream().Write(toWrite, 0, toWrite.Length);

            using (HttpWebResponse httpWebResponse = (HttpWebResponse)webRequest.GetResponse())
            {
                Assert.AreEqual(HttpStatusCode.Accepted, httpWebResponse.StatusCode, "Bad status code");
            }

            HttpResponseHandler webResponse = httpWebClient.Get(
                "http://localhost:" + WebServer.Port + "/" + componentfileName,
                new KeyValuePair<string, string>("Method", "ResolveComponents"),
                new KeyValuePair<string, string>("Filename", newfileName));

            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual("xxx inside the component xxx", webResponse.AsString(), "Unexpected value");
        }

        [Test]
        public void TestWebComponent_POST()
        {
            string newfileName = "TestWebComponent_POST" + SRandom.Next().ToString();

            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            CreateFile(WebServer, httpWebClient, "/", newfileName, "text");

            HttpWebRequest webRequest = (HttpWebRequest)HttpWebRequest.Create("http://localhost:" + WebServer.Port + "/" + newfileName + "?Method=WriteAll");
            webRequest.Method = "POST";
            webRequest.ContentType = "application/x-www-form-urlencoded";
            webRequest.CookieContainer = httpWebClient.CookieContainer;

            string text = "inside the component";

            byte[] toWrite = Encoding.UTF8.GetBytes(text);

            webRequest.ContentLength = toWrite.Length;

            // Write the request
            webRequest.GetRequestStream().Write(toWrite, 0, toWrite.Length);

            using (HttpWebResponse httpWebResponse = (HttpWebResponse)webRequest.GetResponse())
            {
                Assert.AreEqual(HttpStatusCode.Accepted, httpWebResponse.StatusCode, "Bad status code");
            }

            string componentfileName = "TestWebComponentSanity" + SRandom.Next().ToString();

            httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            CreateFile(WebServer, httpWebClient, "/", componentfileName, "text");

            webRequest = (HttpWebRequest)HttpWebRequest.Create("http://localhost:" + WebServer.Port + "/" + componentfileName + "?Method=WriteAll");
            webRequest.Method = "POST";
            webRequest.ContentType = "application/x-www-form-urlencoded";
            webRequest.CookieContainer = httpWebClient.CookieContainer;

            text = "xxx <? WebComponent($_POST[\"Filename\"] . \"?Method=ReadAll\") ?> xxx";

            toWrite = Encoding.UTF8.GetBytes(text);

            webRequest.ContentLength = toWrite.Length;

            // Write the request
            webRequest.GetRequestStream().Write(toWrite, 0, toWrite.Length);

            using (HttpWebResponse httpWebResponse = (HttpWebResponse)webRequest.GetResponse())
            {
                Assert.AreEqual(HttpStatusCode.Accepted, httpWebResponse.StatusCode, "Bad status code");
            }

            HttpResponseHandler webResponse = httpWebClient.Post(
                "http://localhost:" + WebServer.Port + "/" + componentfileName + "?Method=ResolveComponents",
                new KeyValuePair<string, string>("Filename", newfileName));

            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual("xxx inside the component xxx", webResponse.AsString(), "Unexpected value");
        }

        [Test]
        public void TestWebComponent_COOKIE()
        {
            string newfileName = "TestWebComponent_COOKIE" + SRandom.Next().ToString();

            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            CreateFile(WebServer, httpWebClient, "/", newfileName, "text");

            HttpWebRequest webRequest = (HttpWebRequest)HttpWebRequest.Create("http://localhost:" + WebServer.Port + "/" + newfileName + "?Method=WriteAll");
            webRequest.Method = "POST";
            webRequest.ContentType = "application/x-www-form-urlencoded";
            webRequest.CookieContainer = httpWebClient.CookieContainer;

            string text = "inside the component";

            byte[] toWrite = Encoding.UTF8.GetBytes(text);

            webRequest.ContentLength = toWrite.Length;

            // Write the request
            webRequest.GetRequestStream().Write(toWrite, 0, toWrite.Length);

            using (HttpWebResponse httpWebResponse = (HttpWebResponse)webRequest.GetResponse())
            {
                Assert.AreEqual(HttpStatusCode.Accepted, httpWebResponse.StatusCode, "Bad status code");
            }

            string componentfileName = "TestWebComponentSanity" + SRandom.Next().ToString();

            httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            CreateFile(WebServer, httpWebClient, "/", componentfileName, "text");

            webRequest = (HttpWebRequest)HttpWebRequest.Create("http://localhost:" + WebServer.Port + "/" + componentfileName + "?Method=WriteAll");
            webRequest.Method = "POST";
            webRequest.ContentType = "application/x-www-form-urlencoded";
            webRequest.CookieContainer = httpWebClient.CookieContainer;

            text = "xxx <? WebComponent($_COOKIE[\"Filename\"] . \"?Method=ReadAll\") ?> xxx";

            toWrite = Encoding.UTF8.GetBytes(text);

            webRequest.ContentLength = toWrite.Length;

            // Write the request
            webRequest.GetRequestStream().Write(toWrite, 0, toWrite.Length);

            using (HttpWebResponse httpWebResponse = (HttpWebResponse)webRequest.GetResponse())
            {
                Assert.AreEqual(HttpStatusCode.Accepted, httpWebResponse.StatusCode, "Bad status code");
            }

            Cookie cookie = new Cookie("Filename", newfileName);
            cookie.Domain = "localhost";
            httpWebClient.CookieContainer.Add(cookie);

            HttpResponseHandler webResponse = httpWebClient.Get(
                "http://localhost:" + WebServer.Port + "/" + componentfileName,
                new KeyValuePair<string, string>("Method", "ResolveComponents"));

            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual("xxx inside the component xxx", webResponse.AsString(), "Unexpected value");
        }

        [Test]
        public void TestWebComponent_REQUEST()
        {
            string newfileName = "TestWebComponent_REQUEST" + SRandom.Next().ToString();

            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            CreateFile(WebServer, httpWebClient, "/", newfileName, "text");

            HttpWebRequest webRequest = (HttpWebRequest)HttpWebRequest.Create("http://localhost:" + WebServer.Port + "/" + newfileName + "?Method=WriteAll");
            webRequest.Method = "POST";
            webRequest.ContentType = "application/x-www-form-urlencoded";
            webRequest.CookieContainer = httpWebClient.CookieContainer;

            string text = "inside the component";

            byte[] toWrite = Encoding.UTF8.GetBytes(text);

            webRequest.ContentLength = toWrite.Length;

            // Write the request
            webRequest.GetRequestStream().Write(toWrite, 0, toWrite.Length);

            using (HttpWebResponse httpWebResponse = (HttpWebResponse)webRequest.GetResponse())
            {
                Assert.AreEqual(HttpStatusCode.Accepted, httpWebResponse.StatusCode, "Bad status code");
            }

            string componentfileName = "TestWebComponentSanity" + SRandom.Next().ToString();

            httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            CreateFile(WebServer, httpWebClient, "/", componentfileName, "text");

            webRequest = (HttpWebRequest)HttpWebRequest.Create("http://localhost:" + WebServer.Port + "/" + componentfileName + "?Method=WriteAll");
            webRequest.Method = "POST";
            webRequest.ContentType = "application/x-www-form-urlencoded";
            webRequest.CookieContainer = httpWebClient.CookieContainer;

            text = "xxx <? WebComponent($_REQUEST[\"Filename\"] . \"?Method=ReadAll\") ?> xxx";

            toWrite = Encoding.UTF8.GetBytes(text);

            webRequest.ContentLength = toWrite.Length;

            // Write the request
            webRequest.GetRequestStream().Write(toWrite, 0, toWrite.Length);

            using (HttpWebResponse httpWebResponse = (HttpWebResponse)webRequest.GetResponse())
            {
                Assert.AreEqual(HttpStatusCode.Accepted, httpWebResponse.StatusCode, "Bad status code");
            }

            Cookie cookie = new Cookie("Filename", newfileName);
            cookie.Domain = "localhost";
            httpWebClient.CookieContainer.Add(cookie);

            HttpResponseHandler webResponse = httpWebClient.Get(
                "http://localhost:" + WebServer.Port + "/" + componentfileName,
                new KeyValuePair<string, string>("Method", "ResolveComponents"));

            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual("xxx inside the component xxx", webResponse.AsString(), "Unexpected value");

            cookie.Value = "";

            webResponse = httpWebClient.Post(
                "http://localhost:" + WebServer.Port + "/" + componentfileName + "?Method=ResolveComponents",
                new KeyValuePair<string, string>("Filename", newfileName));

            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual("xxx inside the component xxx", webResponse.AsString(), "Unexpected value");

            webResponse = httpWebClient.Post(
                "http://localhost:" + WebServer.Port + "/" + componentfileName + "?Method=ResolveComponents&Filename=" + newfileName,
                new KeyValuePair<string, string>("Filename", "xyz"));

            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
            Assert.AreEqual("xxx inside the component xxx", webResponse.AsString(), "Unexpected value");
        }
    }
}
