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

namespace ObjectCloud.WebServer.Test.WebServerTestClasses
{
    [TestFixture]
    public class CreateFile : WebServerTestBase
    {
        [Test]
        public void TestCreateFileConcurrent()
        {
            string newfileName = "CreateFile_TestCreateFileConcurrent" + SRandom.Next().ToString();

            Shared<uint> numSuccess = new Shared<uint>(0);
            Shared<uint> numConflict = new Shared<uint>(0);

            List<Thread> threads = new List<Thread>();

            Exception threadException = null;

            DateTime startTime = DateTime.Now.AddMilliseconds(100);

            for (int ctr = 0; ctr < 10; ctr++)
                threads.Add(new Thread(delegate()
                {
                    while (DateTime.Now < startTime) { } // spin until it's time to start

                    try
                    {
                        HttpWebClient httpWebClient = new HttpWebClient();
                        LoginAsRoot(httpWebClient);

                        HttpResponseHandler webResponse = httpWebClient.Post(
                                        "http://localhost:" + WebServer.Port + "/?Method=CreateFile",
                                        new KeyValuePair<string, string>("FileName", newfileName),
                                        new KeyValuePair<string, string>("FileType", "text"));

                        switch (webResponse.StatusCode)
                        {
                            case HttpStatusCode.Created:
                                    lock (numSuccess)
                                        numSuccess.Value++;

                                    break;

                            case HttpStatusCode.Conflict:
                                    lock (numConflict)
                                        numConflict.Value++;

                                    break;

                            default:
                                    Assert.Fail("Unexpected status when creating duplicate files");
                                    break;
                        }
                    }
                    catch (Exception e)
                    {
                        threadException = e;
                    }
                }));

            foreach (Thread thread in threads)
                thread.Start();

            foreach (Thread thread in threads)
                thread.Join();

            if (null != threadException)
                throw threadException;

            Assert.AreEqual(1, numSuccess.Value, "Wrong number of successfully created files");
            Assert.AreEqual(threads.Count - 1, numConflict.Value, "Wrong number of conflicts");
        }

		[Test]
        public void TestCreateMultipleFilesConcurrent()
        {

            List<Thread> threads = new List<Thread>();

            Exception threadException = null;

            DateTime startTime = DateTime.Now.AddMilliseconds(100);

            for (int ctr = 0; ctr < 10; ctr++)
                threads.Add(new Thread(delegate()
                {
                    while (DateTime.Now < startTime) { } // spin until it's time to start

                    try
                    {
			            string newfileName = "CreateFile_TestCreateMultipleFilesConcurrent" + SRandom.Next().ToString();
            
						HttpWebClient httpWebClient = new HttpWebClient();
                        LoginAsRoot(httpWebClient);

                        CreateFile(WebServer, httpWebClient, "/", newfileName, "text");
                    }
                    catch (Exception e)
                    {
                        threadException = e;
                    }
                }));

            foreach (Thread thread in threads)
                thread.Start();

            foreach (Thread thread in threads)
                thread.Join();

            if (null != threadException)
                throw threadException;
        }
    }
}
