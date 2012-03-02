// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

using NUnit.Framework;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Database;
using ObjectCloud.ORM.DataAccess.Test;
using ObjectCloud.Spring.Config;

namespace ObjectCloud.ORM.DataAccess.SQLite.Test
{
    [TestFixture]
    public class ConcurrencyTest : TestBase
    {
        EmbeddedDatabaseCreator EmbeddedDatabaseCreator = new EmbeddedDatabaseCreator();
        DatabaseConnectorFactory DatabaseConnectorFactory = new DatabaseConnectorFactory();

        public ConcurrencyTest()
        {
            EmbeddedDatabaseCreator.EmbeddedDatabaseConnector = (IEmbeddedDatabaseConnector)ContextLoader.GetObjectFromConfigurationFile(
                "Test.ObjectCloudConfig.xml", "EmbeddedDatabaseConnector");
            DatabaseConnectorFactory.EmbeddedDatabaseConnector = (IEmbeddedDatabaseConnector)ContextLoader.GetObjectFromConfigurationFile(
                "Test.ObjectCloudConfig.xml", "EmbeddedDatabaseConnector");
        }

        [Test]
        public void TestConcurrencyWhenOpeningTwoDatabases()
        {
            string filename = Path.GetTempFileName();
            File.Delete(filename);

            try
            {
                EmbeddedDatabaseCreator.Create(filename);

                IDatabaseConnector databaseConnector = DatabaseConnectorFactory.CreateConnectorForEmbedded(filename);

                IDatabaseConnection firstConnection = databaseConnector.Connect();

                bool secondConnectionMade = false;

                ThreadPool.QueueUserWorkItem(delegate(object state)
                {
                    databaseConnector.Connect().Dispose();
                    secondConnectionMade = true;
                });

                Thread.Sleep(100);

                Assert.IsFalse(secondConnectionMade, "Two open connections allowed");

                firstConnection.Dispose();

                Thread.Sleep(100);

                Assert.IsTrue(secondConnectionMade, "Second connection never happened!");
            }
            finally
            {
                bool deleted = false;

                do
                    try
                    {
                        File.Delete(filename);
                        deleted = true;
                    }
                    catch
                    {
                        GC.Collect(int.MaxValue);
                        Thread.Sleep(250);
                    }
                while (!deleted);
            }
        }

        [Test]
        public void TestConcurrencyWhenOpeningMultipleDatabases()
        {
			TestConcurrencyWhenOpeningMultipleDatabases(delegate(IDatabaseConnection connection) { Thread.Sleep(10); });
		}

        [Test]
        public void TestConcurrencyWhenOpeningMultipleDatabasesAndDoingSomething()
        {
            Shared<int> numWrittenRows = new Shared<int>(0);

            TestConcurrencyWhenOpeningMultipleDatabases(delegate(IDatabaseConnection connection)
            {
                connection.TestTable.Insert(delegate(ITestTable_Writable testTableRow)
                {
                    testTableRow.TestColumn = SRandom.Next<long>().ToString();
                });

                numWrittenRows.Value++;

                List<ITestTable_Readable> rows = new List<ITestTable_Readable>(
                    connection.TestTable.Select());

                Assert.AreEqual(numWrittenRows.Value, rows.Count, "Wrong number of rows in test table");
            });
        }
		
        public void TestConcurrencyWhenOpeningMultipleDatabases(GenericArgument<IDatabaseConnection> theDelegate)
        {
            string filename = Path.GetTempFileName();
            File.Delete(filename);

            try
            {
                EmbeddedDatabaseCreator.Create(filename);

                IDatabaseConnector databaseConnector = DatabaseConnectorFactory.CreateConnectorForEmbedded(filename);

                object countLock = new object();
                uint numOpenConnections = 0;

                // Give all threads 1/4th second to start
                DateTime startTime = DateTime.Now.AddSeconds(.25);

                Exception threadException = null;

                List<Thread> threads = new List<Thread>();
                for (int ctr = 0; ctr < 100; ctr++)
                    threads.Add(new Thread(delegate()
                    {
                        try
                        {
                            while (DateTime.Now < startTime) ; // spin

                            IDatabaseConnection connection = databaseConnector.Connect();

                            lock (countLock)
                                numOpenConnections++;
							
							theDelegate(connection);

                            lock (countLock)
                            {
                                connection.Dispose();
                                numOpenConnections--;
                            }

                            // remove from threads
                            lock (threads)
                                threads.Remove(Thread.CurrentThread);
                        }
                        catch (Exception e)
                        {
                            threadException = e;
                        }
                    }));

                foreach (Thread thread in new List<Thread>(threads))
                    thread.Start();

                bool shouldContinue = false;
                do
                {
                    Thread.Sleep(100);

                    // If there is an error, wait for all threads to end and then throw the exception
                    if (null != threadException)
                    {
                        List<Thread> threadsCopy;
                        lock (threads)
                            threadsCopy = new List<Thread>(threads);

                        foreach (Thread thread in threadsCopy)
                            thread.Abort();

                        throw threadException;
                    }

                    Assert.IsTrue(numOpenConnections < 2, "Too many open database connections!");

                    lock (threads)
                    {
                        shouldContinue = threads.Count > 0;
                        Console.WriteLine("Number of threads: " + threads.Count);
                    }

                } while (shouldContinue);
            }
            finally
            {
                bool deleted = false;

                do
                    try
                    {
                        File.Delete(filename);
                        deleted = true;
                    }
                    catch
                    {
                        GC.Collect(int.MaxValue);
                        Thread.Sleep(250);
                    }
                while (!deleted);
            }
        }
    }
}
