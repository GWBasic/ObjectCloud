// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;

using NUnit.Framework;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Disk.Factories;
using ObjectCloud.Disk.Implementation;
using ObjectCloud.Disk.WebHandlers;
using ObjectCloud.ORM.DataAccess.SQLite;

namespace ObjectCloud.Disk.Test
{
    [TestFixture]
    public class FileSystemResolverTest : TestBase
    {
        [Test]
        public void TestCreateFileSystemResolver()
        {
            Assert.AreEqual(
                "." + Path.DirectorySeparatorChar + "FileSystem",
                ((FileSystem)FileHandlerFactoryLocator.FileSystem).ConnectionString);
            Assert.AreEqual("0", FileHandlerFactoryLocator.FileSystem.RootDirectoryId.ToString());

            Assert.IsNotNull(FileHandlerFactoryLocator.DirectoryFactory);
            Assert.IsTrue(FileHandlerFactoryLocator.DirectoryFactory is IFileHandlerFactory);
            Assert.IsTrue(FileHandlerFactoryLocator.DirectoryFactory is DirectoryHandlerFactory);
        }

        [Test]
        public void TestRootDirectoryPresent()
        {
            Assert.IsNotNull(FileHandlerFactoryLocator.FileSystemResolver.RootDirectoryHandler);
        }

        [Test]
        public void TestCache()
        {
            IFileSystemResolver fileSystemResolver = FileHandlerFactoryLocator.FileSystemResolver;

            IDirectoryHandler dh = (IDirectoryHandler)fileSystemResolver.RootDirectoryHandler;

            string fileName = SRandom.Next<long>().ToString();
            IFileHandler fileHandler = dh.CreateFile(fileName, "directory", null);

            Assert.IsNotNull(fileHandler);

            IFileHandler otherFileHandler = fileSystemResolver.ResolveFile(fileName).FileHandler;

            Assert.IsTrue(fileHandler == otherFileHandler);
        }

        [Test]
        public void TestCreateSubdirectory()
        {
            IFileSystemResolver fileSystemResolver = FileHandlerFactoryLocator.FileSystemResolver;

            IDirectoryHandler dh = (IDirectoryHandler)fileSystemResolver.RootDirectoryHandler;

            string fileName = SRandom.Next<long>().ToString();
            IFileHandler fileHandler = dh.CreateFile(fileName, "directory", null);

            Assert.IsNotNull(fileHandler);

            fileHandler = ((IDirectoryHandler)fileHandler).CreateFile("1", "directory", null);
            fileHandler = ((IDirectoryHandler)fileHandler).CreateFile("2", "directory", null);
            fileHandler = ((IDirectoryHandler)fileHandler).CreateFile("3", "directory", null);

            fileHandler = fileSystemResolver.ResolveFile(fileName + "/1/2/3").FileHandler;
            Assert.IsNotNull(fileHandler);
            Assert.IsTrue(fileHandler is IDirectoryHandler);
        }

        [Test]
        public void TestNameValuePairs()
        {
            IFileSystemResolver fileSystemResolver = FileHandlerFactoryLocator.FileSystemResolver;

            IDirectoryHandler dh = (IDirectoryHandler)fileSystemResolver.RootDirectoryHandler;

            string fileName = SRandom.Next<long>().ToString();
            IFileHandler fileHandler = dh.CreateFile(fileName, "name-value", null);

            Assert.IsNotNull(fileHandler);
            Assert.IsInstanceOf<INameValuePairsHandler>(fileHandler);

            INameValuePairsHandler nameValuePairsHandler = (INameValuePairsHandler)fileHandler;

            nameValuePairsHandler.Set(null, "foo", "bar");
            Assert.AreEqual("bar", nameValuePairsHandler["foo"]);

            Assert.IsNull(nameValuePairsHandler["null"]);

            nameValuePairsHandler.Set(null, "abc", "xyz");
            Assert.AreEqual("xyz", nameValuePairsHandler["abc"]);
            Assert.AreEqual("bar", nameValuePairsHandler["foo"]);

            nameValuePairsHandler.Set(null, "abc", null);
            Assert.IsNull(nameValuePairsHandler["abc"]);
        }

        [Test]
        public void TestCopyNameValuePairs()
        {
            IFileSystemResolver fileSystemResolver = FileHandlerFactoryLocator.FileSystemResolver;

            IDirectoryHandler dh = (IDirectoryHandler)fileSystemResolver.RootDirectoryHandler;

            string sourceFilename = SRandom.Next<long>().ToString();
            IFileHandler fileHandler = dh.CreateFile(sourceFilename, "name-value", null);

            Assert.IsNotNull(fileHandler);
            Assert.IsInstanceOf<INameValuePairsHandler>(fileHandler);

            INameValuePairsHandler nameValuePairsHandler = (INameValuePairsHandler)fileHandler;

            nameValuePairsHandler.Set(null, "foo", "bar");
            nameValuePairsHandler.Set(null, "abc", "xyz");

            string destinationFilename = "/" + SRandom.Next<long>().ToString();

            fileSystemResolver.CopyFile(sourceFilename, destinationFilename, null);

            nameValuePairsHandler = fileSystemResolver.ResolveFile(destinationFilename).CastFileHandler<INameValuePairsHandler>();

            Assert.AreEqual("xyz", nameValuePairsHandler["abc"]);
            Assert.AreEqual("bar", nameValuePairsHandler["foo"]);
        }

        [Test]
        public void TestText()
        {
            IFileSystemResolver fileSystemResolver = FileHandlerFactoryLocator.FileSystemResolver;

            IDirectoryHandler dh = (IDirectoryHandler)fileSystemResolver.RootDirectoryHandler;

            string fileName = SRandom.Next<long>().ToString();
            IFileHandler fileHandler = dh.CreateFile(fileName, "text", null);

            Assert.IsNotNull(fileHandler);
            Assert.IsInstanceOf<ITextHandler>(fileHandler);

            ITextHandler textHandler = (ITextHandler)fileHandler;

            textHandler.WriteAll(null, "foo\nbar\n\n\n");

            Assert.AreEqual("foo\nbar\n\n\n", textHandler.ReadAll());
        }

        [Test]
        public void TestCopyText()
        {
            IFileSystemResolver fileSystemResolver = FileHandlerFactoryLocator.FileSystemResolver;

            IDirectoryHandler dh = (IDirectoryHandler)fileSystemResolver.RootDirectoryHandler;

            string sourceFilename = SRandom.Next<long>().ToString();
            IFileHandler fileHandler = dh.CreateFile(sourceFilename, "text", null);

            Assert.IsNotNull(fileHandler);
            Assert.IsInstanceOf<ITextHandler>(fileHandler);

            ITextHandler textHandler = (ITextHandler)fileHandler;

            textHandler.WriteAll(null, "foo\nbar\n\n\n");

            string destinationFilename = SRandom.Next<long>().ToString();

            fileSystemResolver.CopyFile(sourceFilename, destinationFilename, null);

            textHandler = fileSystemResolver.ResolveFile(destinationFilename).CastFileHandler<ITextHandler>();

            Assert.AreEqual("foo\nbar\n\n\n", textHandler.ReadAll());
        }

        [Test]
        public void TestBinary()
        {
            IFileSystemResolver fileSystemResolver = FileHandlerFactoryLocator.FileSystemResolver;

            IDirectoryHandler dh = (IDirectoryHandler)fileSystemResolver.RootDirectoryHandler;

            string fileName = SRandom.Next<long>().ToString();
            IFileHandler fileHandler = dh.CreateFile(fileName, "binary", null);

            Assert.IsNotNull(fileHandler);
            Assert.IsInstanceOf<IBinaryHandler>(fileHandler);

            IBinaryHandler binaryHandler = (IBinaryHandler)fileHandler;

            byte[] content = SRandom.NextBytes(2048);

            binaryHandler.WriteAll(content);

            Assert.IsTrue(Enumerable.Equals(content, binaryHandler.ReadAll()), "Content not writtent correctly");
        }

        [Test]
        public void TestCopyBinary()
        {
            IFileSystemResolver fileSystemResolver = FileHandlerFactoryLocator.FileSystemResolver;

            IDirectoryHandler dh = (IDirectoryHandler)fileSystemResolver.RootDirectoryHandler;

            string sourceFilename = SRandom.Next<long>().ToString();
            IFileHandler fileHandler = dh.CreateFile(sourceFilename, "binary", null);

            Assert.IsNotNull(fileHandler);
            Assert.IsInstanceOf<IBinaryHandler>(fileHandler);

            IBinaryHandler binaryHandler = (IBinaryHandler)fileHandler;

            byte[] content = SRandom.NextBytes(2048);

            binaryHandler.WriteAll(content);

            string destinationFilename = SRandom.Next<long>().ToString();

            fileSystemResolver.CopyFile(sourceFilename, destinationFilename, null);

            binaryHandler = fileSystemResolver.ResolveFile(destinationFilename).CastFileHandler<IBinaryHandler>();

            Assert.IsTrue(Enumerable.Equals(content, binaryHandler.ReadAll()), "Content not writtent correctly");
        }

        [Test]
        public void TestDefaultDirectorySetup()
        {
            IFileSystemResolver fileSystemResolver = FileHandlerFactoryLocator.FileSystemResolver;

            IUserManagerHandler userManager = fileSystemResolver.ResolveFile("Users/UserDB").CastFileHandler<IUserManagerHandler>();
            Assert.IsNotNull(userManager, "User manager object doesn't exist");

            IUserHandler user = fileSystemResolver.ResolveFile("Users/root.user").CastFileHandler<IUserHandler>();
            Assert.IsNotNull(user, "root user does not exist");

            IDirectoryHandler userDir = fileSystemResolver.ResolveFile("Users/root").CastFileHandler<IDirectoryHandler>();
            Assert.IsNotNull(userDir, "root directory not created");

            Assert.AreEqual("root", user.Name, "Username for root isn't root");

            user = fileSystemResolver.ResolveFile("Users/anonymous.user").CastFileHandler<IUserHandler>();
            Assert.IsNotNull(user, "anonymous user does not exist");

            Assert.AreEqual("anonymous", user.Name, "Username for anonymous isn't anonymous");
        }

        [Test]
        [ExpectedException(typeof(BadFileName))]
        public void TestOpenBraceIsInvalid()
        {
            IFileSystemResolver fileSystemResolver = FileHandlerFactoryLocator.FileSystemResolver;

            fileSystemResolver.RootDirectoryHandler.CreateFile("vfas[frea", "text", null);
        }

        [Test]
        [ExpectedException(typeof(BadFileName))]
        public void TestCloseBraceIsInvalid()
        {
            IFileSystemResolver fileSystemResolver = FileHandlerFactoryLocator.FileSystemResolver;

            fileSystemResolver.RootDirectoryHandler.CreateFile("vfas]frea", "text", null);
        }

        [Test]
        public void TestDelete()
        {
            IFileSystemResolver fileSystemResolver = FileHandlerFactoryLocator.FileSystemResolver;

            IDirectoryHandler dh = (IDirectoryHandler)fileSystemResolver.RootDirectoryHandler;

            string fileName = SRandom.Next<long>().ToString();
            dh.CreateFile(fileName, "name-value", null);

            // Make sure that the file was actually created
            dh.OpenFile(fileName);

            dh.DeleteFile(null, fileName);

            try
            {
                dh.OpenFile(fileName);
                Assert.Fail("File not deleted");
            }
            catch (FileDoesNotExist) { }
        }

        [Test]
        public void TestRecursiveDelete()
        {
            IFileSystemResolver fileSystemResolver = FileHandlerFactoryLocator.FileSystemResolver;

            IDirectoryHandler dh = (IDirectoryHandler)fileSystemResolver.RootDirectoryHandler;

            string fileName = SRandom.Next<long>().ToString();
            IFileHandler fileHandler = dh.CreateFile(fileName, "directory", null);

            Assert.IsNotNull(fileHandler);

            fileHandler = ((IDirectoryHandler)fileHandler).CreateFile("1", "directory", null);
            fileHandler = ((IDirectoryHandler)fileHandler).CreateFile("2", "directory", null);
            fileHandler = ((IDirectoryHandler)fileHandler).CreateFile("3", "directory", null);

            // Make sure that the subdirectory can be created
            IFileContainer fc_1 = fileSystemResolver.ResolveFile(fileName + "/1");
            IFileContainer fc_2 = fileSystemResolver.ResolveFile(fileName + "/1/2");
            IFileContainer fc_3 = fileSystemResolver.ResolveFile(fileName + "/1/2/3");
            fileHandler = fc_3.FileHandler;

            // Create a token file
            IDirectoryHandler subDirHandler = (IDirectoryHandler)fileHandler;
            subDirHandler.CreateFile("flah", "name-value", null);

            IFileContainer lastFile = subDirHandler.OpenFile("flah");

            dh.DeleteFile(null, fileName);

            try
            {
                fileSystemResolver.ResolveFile(fileName + "/1/2/3");
                Assert.Fail("File not deleted");
            }
            catch (FileDoesNotExist) { }

            try
            {
                fileSystemResolver.ResolveFile(fileName + "/1/2/3/flah");
                Assert.Fail("A deeply-nested file was not deleted from disk when its parent directory was deleted");
            }
            catch (FileDoesNotExist) { }

            try
            {
                fileSystemResolver.LoadFile(fc_1.FileId, "directory");
                Assert.Fail("A deeply-nested file was not deleted from disk when its parent directory was deleted");
            }
            catch (InvalidFileId) { }

            try
            {
                fileSystemResolver.LoadFile(fc_2.FileId, "directory");
                Assert.Fail("A deeply-nested file was not deleted from disk when its parent directory was deleted");
            }
            catch (InvalidFileId) { }

            try
            {
                fileSystemResolver.LoadFile(fc_3.FileId, "directory");
                Assert.Fail("A deeply-nested file was not deleted from disk when its parent directory was deleted");
            }
            catch (InvalidFileId) { }

            try
            {
                fileSystemResolver.LoadFile(lastFile.FileId, "name-value");
                Assert.Fail("A deeply-nested file was not deleted from disk when its parent directory was deleted");
            }
            catch (InvalidFileId) { }
        }

        [Test]
        [ExpectedException(typeof(DuplicateFile))]
        public void TestCreateDuplicateFile()
        {
            IFileSystemResolver fileSystemResolver = FileHandlerFactoryLocator.FileSystemResolver;

            IDirectoryHandler dh = (IDirectoryHandler)fileSystemResolver.RootDirectoryHandler;

            string fileName = SRandom.Next<long>().ToString();
            dh.CreateFile(fileName, "directory", null);

            try
            {
                dh.CreateFile(fileName, "directory", null);
            }
            finally
            {
                dh.DeleteFile(null, fileName);
            }
        }

        [Test]
        public void TestEnumerateFilesInDirectory()
        {
            IFileSystemResolver fileSystemResolver = FileHandlerFactoryLocator.FileSystemResolver;

            IDirectoryHandler dh = (IDirectoryHandler)fileSystemResolver.RootDirectoryHandler;

            string fileName = SRandom.Next<long>().ToString();
            IDirectoryHandler directoryHandler = (IDirectoryHandler)dh.CreateFile(fileName, "directory", null);

            Assert.IsNotNull(directoryHandler);

            directoryHandler.CreateFile("aaa", "directory", null);
            directoryHandler.CreateFile("ggg", "directory", null);
            directoryHandler.CreateFile("tyjhu", "directory", null);

            directoryHandler = dh.OpenFile(fileName).CastFileHandler<IDirectoryHandler>();

            Dictionary<string, IFileContainer> filesInFolder = new Dictionary<string, IFileContainer>();

            foreach (IFileContainer fileContainer in directoryHandler.Files)
                filesInFolder[fileContainer.Filename] = fileContainer;

            Assert.AreEqual(3, filesInFolder.Count, "Wrong number of files returned");
            Assert.IsNotNull(filesInFolder["aaa"], "aaa missing");
            Assert.IsNotNull(filesInFolder["ggg"], "ggg missing");
            Assert.IsNotNull(filesInFolder["tyjhu"], "tyjhu missing");
        }

        [Test]
        public void TestCopyDirectory()
        {
            IFileSystemResolver fileSystemResolver = FileHandlerFactoryLocator.FileSystemResolver;

            IDirectoryHandler dh = (IDirectoryHandler)fileSystemResolver.RootDirectoryHandler;

            string sourceFilename = SRandom.Next<long>().ToString();
            IDirectoryHandler directoryHandler = (IDirectoryHandler)dh.CreateFile(sourceFilename, "directory", null);

            ITextHandler textHandler = (ITextHandler)directoryHandler.CreateFile("text", "text", null);
            textHandler.WriteAll(null, "the contents");

            INameValuePairsHandler nameValueHandler = (INameValuePairsHandler)directoryHandler.CreateFile("name-value", "name-value", null);
            nameValueHandler.Set(null, "foo", "bar");

            string destinationFilename = SRandom.Next<long>().ToString();

            fileSystemResolver.CopyFile(sourceFilename, destinationFilename, null);

            directoryHandler = fileSystemResolver.ResolveFile(destinationFilename).CastFileHandler<IDirectoryHandler>();

            textHandler = directoryHandler.OpenFile("text").CastFileHandler<ITextHandler>();
            Assert.AreEqual("the contents", textHandler.ReadAll(), "Text copied improperly");

            nameValueHandler = directoryHandler.OpenFile("name-value").CastFileHandler<INameValuePairsHandler>();
            Assert.AreEqual("bar", nameValueHandler["foo"], "Name-values copied incorrectly");
        }

        [Test]
        public void TestIsFilePresent()
        {
            IFileSystemResolver fileSystemResolver = FileHandlerFactoryLocator.FileSystemResolver;

            IDirectoryHandler dh = (IDirectoryHandler)fileSystemResolver.RootDirectoryHandler;

            string fileName = SRandom.Next<long>().ToString();
            dh.CreateFile(fileName, "text", null);

            bool isFilePresent = dh.IsFilePresent(fileName);

            Assert.IsTrue(isFilePresent, "File is not present");
        }

        [Test]
        public void TestIsFileNotPresent()
        {
            IFileSystemResolver fileSystemResolver = FileHandlerFactoryLocator.FileSystemResolver;

            IDirectoryHandler dh = (IDirectoryHandler)fileSystemResolver.RootDirectoryHandler;

            string fileName = SRandom.Next<long>().ToString();

            bool isFilePresent = dh.IsFilePresent(fileName);

            Assert.IsFalse(isFilePresent, "File is present");
        }

        [Test]
        public void TestRename()
        {
            IFileSystemResolver fileSystemResolver = FileHandlerFactoryLocator.FileSystemResolver;

            IDirectoryHandler dh = (IDirectoryHandler)fileSystemResolver.RootDirectoryHandler;

            string newFileName = SRandom.Next<long>().ToString();
            string oldFileName = SRandom.Next<long>().ToString();
            dh.CreateFile(oldFileName, "name-value", null);

            dh.Rename(null, oldFileName, newFileName);

            IFileContainer fileContainer = dh.OpenFile(newFileName);

            Assert.IsNotNull(fileContainer, "File not renamed");

            try
            {
                dh.OpenFile(oldFileName);
                Assert.Fail("Can still open old file");
            }
            catch (FileDoesNotExist) { }
        }

        [Test]
        public void TestCreateEmbeddedDatabase()
        {
            IFileSystemResolver fileSystemResolver = FileHandlerFactoryLocator.FileSystemResolver;

            IDirectoryHandler dh = (IDirectoryHandler)fileSystemResolver.RootDirectoryHandler;

            string fileName = SRandom.Next<long>().ToString();
            IFileHandler fileHandler = dh.CreateFile(fileName, "database", null);

            Assert.IsNotNull(fileHandler);
            Assert.IsInstanceOf<IDatabaseHandler>(fileHandler);

            IDatabaseHandler databaseHandler = (IDatabaseHandler)fileHandler;

            DbCommand command = databaseHandler.Connection.CreateCommand();

            command.CommandText = "create table testtable (testcol int)";
            command.ExecuteNonQuery();

            int testVal = SRandom.Next<int>();

            command = databaseHandler.Connection.CreateCommand();
            command.CommandText = "insert into testtable (testcol) values (@testVal)";

            DbParameter dbParameter = command.CreateParameter();
            dbParameter.ParameterName = "@testVal";
            dbParameter.Value = testVal;
            command.Parameters.Add(dbParameter);

            int rowsAffected = command.ExecuteNonQuery();

            Assert.AreEqual(1, rowsAffected, "Wrong number of rows inserted");

            command = databaseHandler.Connection.CreateCommand();
            command.CommandText = "select testcol from testtable";
            using (IDataReader reader = command.ExecuteReader())
            {
                Assert.IsTrue(reader.Read(), "No data returned");

                int intResult = reader.GetInt32(0);

                Assert.AreEqual(testVal, intResult, "Wrong result returned from RunQueryForDataReader");
                Assert.IsFalse(reader.Read(), "Too much data returned");

                reader.Close();
            }

            command = databaseHandler.Connection.CreateCommand();
            command.CommandText = "select testcol from testtable";
            object scalarResult = command.ExecuteScalar();

            Assert.AreEqual(testVal, scalarResult, "Unexpected result from RunQueryForScalar");
        }

        [Test]
        public void TestEmbeddedDatabaseRoundTrip()
        {
            IFileSystemResolver fileSystemResolver = FileHandlerFactoryLocator.FileSystemResolver;

            IDirectoryHandler dh = (IDirectoryHandler)fileSystemResolver.RootDirectoryHandler;

            string fileName = SRandom.Next<long>().ToString();
            IFileHandler fileHandler = dh.CreateFile(fileName, "database", null);

            Assert.IsNotNull(fileHandler);
            Assert.IsInstanceOf<IDatabaseHandler>(fileHandler);

            IDatabaseHandler databaseHandler = (IDatabaseHandler)fileHandler;

            DbCommand command = databaseHandler.Connection.CreateCommand();

            command.CommandText = "create table testtable (testcol int)";
            command.ExecuteNonQuery();

            databaseHandler = dh.OpenFile(fileName).CastFileHandler<IDatabaseHandler>();

            int testVal = SRandom.Next<int>();

            command = databaseHandler.Connection.CreateCommand();
            command.CommandText = "insert into testtable (testcol) values (@testVal)";

            DbParameter dbParameter = command.CreateParameter();
            dbParameter.ParameterName = "@testVal";
            dbParameter.Value = testVal;
            command.Parameters.Add(dbParameter);
            
            int rowsAffected = command.ExecuteNonQuery();

            Assert.AreEqual(1, rowsAffected, "Wrong number of rows inserted");

            command = databaseHandler.Connection.CreateCommand();
            command.CommandText = "select testcol from testtable";
            object scalarResult = command.ExecuteScalar();

            Assert.AreEqual(testVal, scalarResult, "Unexpected result from RunQueryForScalar");
        }

        [Test]
        public void TestCopyEmbeddedDatabase()
        {
            IFileSystemResolver fileSystemResolver = FileHandlerFactoryLocator.FileSystemResolver;

            IDirectoryHandler dh = (IDirectoryHandler)fileSystemResolver.RootDirectoryHandler;

            string sourceFilename = SRandom.Next<long>().ToString();
            IFileHandler fileHandler = dh.CreateFile(sourceFilename, "database", null);

            Assert.IsNotNull(fileHandler);
            Assert.IsInstanceOf<IDatabaseHandler>(fileHandler);

            IDatabaseHandler databaseHandler = (IDatabaseHandler)fileHandler;

            DbCommand command = databaseHandler.Connection.CreateCommand();

            command.CommandText = "create table testtable (testcol int)";
            command.ExecuteNonQuery();

            string destinationFilename = SRandom.Next<long>().ToString();

            fileSystemResolver.CopyFile(sourceFilename, destinationFilename, null);

            databaseHandler = dh.OpenFile(destinationFilename).CastFileHandler<IDatabaseHandler>();

            int testVal = SRandom.Next<int>();

            command = databaseHandler.Connection.CreateCommand();
            command.CommandText = "insert into testtable (testcol) values (@testVal)";
            
            DbParameter dbParameter = command.CreateParameter();
            dbParameter.ParameterName = "@testVal";
            dbParameter.Value = testVal;
            command.Parameters.Add(dbParameter);

            int rowsAffected = command.ExecuteNonQuery();

            Assert.AreEqual(1, rowsAffected, "Wrong number of rows inserted");

            command = databaseHandler.Connection.CreateCommand();
            command.CommandText = "select testcol from testtable";
            object scalarResult = command.ExecuteScalar();

            Assert.AreEqual(testVal, scalarResult, "Unexpected result from RunQueryForScalar");
        }

        [Test]
        public void TestIndexFile()
        {
            IFileSystemResolver fileSystemResolver = FileHandlerFactoryLocator.FileSystemResolver;

            IDirectoryHandler dh = (IDirectoryHandler)fileSystemResolver.RootDirectoryHandler;

            string oldIndex = dh.IndexFile;

            try
            {
                dh.IndexFile = "uhnpiuhnirgwuvbsjl";

                Assert.AreEqual("uhnpiuhnirgwuvbsjl", dh.IndexFile, "Index not properly stored");
            }
            finally
            {
                dh.IndexFile = oldIndex;
            }
        }
    }
}