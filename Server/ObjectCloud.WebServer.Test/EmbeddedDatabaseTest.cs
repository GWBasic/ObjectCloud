// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
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
	public class EmbeddedDatabaseTest : WebServerTestBase
	{
		[Test]
		public void TestCreateEmbeddedDatabase()
		{
			string newfileName = "TestCreateEmbeddedDatabase" + SRandom.Next().ToString() + ".db";

            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            CreateFile(WebServer, httpWebClient, "/", newfileName, "database");

            HttpResponseHandler webResponse = httpWebClient.Post(
                "http://localhost:" + WebServer.Port + "/" + newfileName + "?Method=PostQuery",
                new KeyValuePair<string, string>("query", "create table testtable (testcol int)"));

            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
			int[] resultInts = webResponse.AsJsonReader().Deserialize<int[]>();
			Assert.IsNotNull(resultInts, "Result deserialized as null");
			Assert.AreEqual(0, resultInts[0], "Incorrect result");

            webResponse = httpWebClient.Post(
                "http://localhost:" + WebServer.Port + "/" + newfileName + "?Method=PostQuery",
                new KeyValuePair<string, string>("query", "insert into testtable (testcol) values (123);insert into testtable (testcol) values (456)"));

            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
			resultInts = webResponse.AsJsonReader().Deserialize<int[]>();
			Assert.AreEqual(2, resultInts[0], "Incorrect result");

            webResponse = httpWebClient.Post(
                "http://localhost:" + WebServer.Port + "/" + newfileName + "?Method=PostQuery",
                new KeyValuePair<string, string>("query", "select * from testtable; select * from testtable"));

            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
			Dictionary<string, object>[][] resultObjss = webResponse.AsJsonReader().Deserialize<Dictionary<string, object>[][]>();

			Assert.AreEqual(2, resultObjss.Length, "Wrong number of queries returned");
			
			foreach (Dictionary<string, object>[] resultObjs in resultObjss)
			{
				List<int> expected = new List<int>(new int[] {123,456});
				
				foreach(Dictionary<string, object> resultObj in resultObjs)
				{
					int value = Convert.ToInt32(resultObj["testcol"]);
					
					Assert.IsTrue(expected.Contains(value), "Unexpected value: " + value);
					expected.Remove(value);
				}
				
				Assert.IsTrue(0 == expected.Count, "Expected values not returned: " + StringGenerator.GenerateCommaSeperatedList(expected));
			}
		}
		
		[Test]
		public void TestParameters()
		{
			string newfileName = "TestParameters" + SRandom.Next().ToString() + ".db";

            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            CreateFile(WebServer, httpWebClient, "/", newfileName, "database");

            HttpResponseHandler webResponse = httpWebClient.Post(
                "http://localhost:" + WebServer.Port + "/" + newfileName + "?Method=PostQuery",
                new KeyValuePair<string, string>("query", "create table testtable (testcol int)"));

            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
			int[] resultInts = webResponse.AsJsonReader().Deserialize<int[]>();
			Assert.AreEqual(0, resultInts[0], "Incorrect result");

            webResponse = httpWebClient.Post(
                "http://localhost:" + WebServer.Port + "/" + newfileName + "?Method=PostQuery&@val=345",
                new KeyValuePair<string, string>("query", "insert into testtable (testcol) values (@val)"),
			    new KeyValuePair<string, string>("@val", "789"));

            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
			resultInts = webResponse.AsJsonReader().Deserialize<int[]>();
			Assert.AreEqual(1, resultInts[0], "Incorrect result");

            webResponse = httpWebClient.Post(
                "http://localhost:" + WebServer.Port + "/" + newfileName + "?Method=PostQuery",
                new KeyValuePair<string, string>("query", "select testcol from testtable"));

            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
			Dictionary<string, object>[][] resultObj = webResponse.AsJsonReader().Deserialize<Dictionary<string, object>[][]>();

			Assert.IsFalse((object)345 == resultObj[0][0]["testcol"], "GET parameter overrulled POST parameter");
			Assert.AreEqual(789, resultObj[0][0]["testcol"], "POST parameter stored incorrectly");
		}
		
		[Test]
		public void TestStoredProcedure()
		{
			string newfileName = "TestStoredProcedure" + SRandom.Next().ToString() + ".db";

            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            CreateFile(WebServer, httpWebClient, "/", newfileName, "database");

            HttpResponseHandler webResponse = httpWebClient.Post(
                "http://localhost:" + WebServer.Port + "/" + newfileName + "?Method=PostQuery",
                new KeyValuePair<string, string>("query", "create table testtable (testcol string)"));

			Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");

			
			// Create the stored proc
			
			string storedProcFilename = "Proc" + SRandom.Next().ToString() + ".DbProc";

            CreateFile(WebServer, httpWebClient, "/", storedProcFilename, "name-value");

			Dictionary<string, string> storedProc = new Dictionary<string, string>();

            storedProc["Query"] = "insert into testtable (testcol) values (@testcol)";

            webResponse = httpWebClient.Post(
                "http://localhost:" + WebServer.Port + "/" + storedProcFilename + "?Method=SetAll",
                storedProc);

            Assert.AreEqual(HttpStatusCode.Accepted, webResponse.StatusCode, "Bad status code");

            webResponse = httpWebClient.Post(
                "http://localhost:" + WebServer.Port + "/" + newfileName + "?Method=PostToStoredProc",
                new KeyValuePair<string, string>("procFile", storedProcFilename),
			    new KeyValuePair<string, string>("@testcol", "The value"));

            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
			int[] resultInts = webResponse.AsJsonReader().Deserialize<int[]>();
			Assert.AreEqual(1, resultInts[0], "Incorrect result");

            webResponse = httpWebClient.Post(
                "http://localhost:" + WebServer.Port + "/" + newfileName + "?Method=PostQuery",
                new KeyValuePair<string, string>("query", "select testcol from testtable"));

            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
			Dictionary<string, object>[][] resultObj = webResponse.AsJsonReader().Deserialize<Dictionary<string, object>[][]>();

			Assert.AreEqual("The value", resultObj[0][0]["testcol"], "Value stored incorrectly");
		}
		
		[Test]
		public void TestStoredProcedurePermissions()
		{
			string newfileName = "TestStoredProcedurePermissions" + SRandom.Next().ToString() + ".db";

            HttpWebClient httpWebClient = new HttpWebClient();
            LoginAsRoot(httpWebClient);

            CreateFile(WebServer, httpWebClient, "/", newfileName, "database");

            HttpResponseHandler webResponse = httpWebClient.Post(
                "http://localhost:" + WebServer.Port + "/" + newfileName + "?Method=PostQuery",
                new KeyValuePair<string, string>("query", "create table testtable (testcol string)"));

			Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
			
			// Create the stored proc that requires administrative permission
			
			string storedProcFilename = "ProcAdminister" + SRandom.Next().ToString() + ".DbProc";

            CreateFile(WebServer, httpWebClient, "/", storedProcFilename, "name-value");

			Dictionary<string, string> storedProc = new Dictionary<string, string>();

            storedProc["Query"] = "insert into testtable (testcol) values (@testcol)";
			storedProc["MinimumPermission"] = "Administer";

            webResponse = httpWebClient.Post(
                "http://localhost:" + WebServer.Port + "/" + storedProcFilename + "?Method=SetAll",
                storedProc);

            Assert.AreEqual(HttpStatusCode.Accepted, webResponse.StatusCode, "Bad status code");
			
			Logout(httpWebClient);

            webResponse = httpWebClient.Post(
                "http://localhost:" + WebServer.Port + "/" + newfileName + "?Method=PostToStoredProc",
                new KeyValuePair<string, string>("procFile", storedProcFilename),
			    new KeyValuePair<string, string>("@testcol", "The value"));

            Assert.AreEqual(HttpStatusCode.Unauthorized, webResponse.StatusCode, "Bad status code");
			
			LoginAsRoot(httpWebClient);

            webResponse = httpWebClient.Post(
                "http://localhost:" + WebServer.Port + "/" + newfileName + "?Method=PostToStoredProc",
                new KeyValuePair<string, string>("procFile", storedProcFilename),
                new KeyValuePair<string, string>("@testcol", "The value"));

            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode, "Bad status code");
		}
	}
}
