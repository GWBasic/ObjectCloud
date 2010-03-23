// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Text;
using System.Xml;

using NUnit.Framework;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Disk.Implementation;
using ObjectCloud.ORM.DataAccess.SQLite;

namespace ObjectCloud.Disk.Test
{
    [TestFixture]
    public class FileDumperTest : TestBase
    {
        protected override void DoAdditionalSetup()
        {
            base.DoAdditionalSetup();

            RootUserId = FileHandlerFactoryLocator.UserManagerHandler.Root.Id;
        }

        /// <summary>
        /// The root user's ID
        /// </summary>
        ID<IUserOrGroup, Guid> RootUserId;

        [Test]
        public void TestDumpSanity()
        {
            IFileContainer rootFolderContainer = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile("/API");

            string dumpDestination = Path.GetTempFileName();
            File.Delete(dumpDestination);

            try
            {
                using (TimedLock.Lock(rootFolderContainer.FileHandler))
                    rootFolderContainer.FileHandler.Dump(dumpDestination, RootUserId);

                Assert.IsTrue(Directory.GetFiles(dumpDestination).Length > 0, "Files not dumped");
                Assert.IsTrue(Directory.GetDirectories(dumpDestination).Length > 0, "Directories not dumped");
            }
            finally
            {
				try
				{
                		Directory.Delete(dumpDestination, true);
				} catch {}
            }
        }

        [Test]
        public void TestDumpRestoreTextFile()
        {
            IFileContainer rootFolderContainer = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile("/");
            IDirectoryHandler rootDirectoryHandler = rootFolderContainer.CastFileHandler<IDirectoryHandler>();

            string filename = "TestDumpTestFile" + SRandom.Next();
            string contents = "bgyonur3q0h h78 gy7 hy78o9 hy7hyuuyhuyhghfrwq3qf   eq3fewferfersa vdewrewq\n\n\nbgyibuybyubuny\t\thbyiuyuviqruw";

            ITextHandler textFile = (ITextHandler)rootDirectoryHandler.CreateFile(filename, "text", null);

            textFile.WriteAll(null, contents);

            IFileContainer fileContainer = rootDirectoryHandler.OpenFile(filename);

            string dumpDestination = Path.GetTempFileName();
            File.Delete(dumpDestination);

            try
            {
                using (TimedLock.Lock(fileContainer.FileHandler))
                    fileContainer.FileHandler.Dump(dumpDestination, RootUserId);

                string dumped = File.ReadAllText(dumpDestination);

                Assert.AreEqual(contents, dumped, "Text file dumped incorrectly");

                ITextHandler restored = (ITextHandler)rootDirectoryHandler.RestoreFile(
                    filename + "restored", "text", dumpDestination, RootUserId);

                Assert.AreEqual(contents, restored.ReadAll(), "Text file restored incorrectly");
            }
            finally
            {
                File.Delete(dumpDestination);
            }
        }

        [Test]
        public void TestDumpRestoreNameValuePairsFile()
        {
            IFileContainer rootFolderContainer = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile("/");
            IDirectoryHandler rootDirectoryHandler = rootFolderContainer.CastFileHandler<IDirectoryHandler>();

            string filename = "TestDumpNameValuePairsFile" + SRandom.Next();
            Dictionary<string, string> contents = new Dictionary<string, string>();
            contents["a"] = "123";
            contents["b"] = "456";
            contents["c"] = "7890";

            INameValuePairsHandler nameValuePairsFile = (INameValuePairsHandler)rootDirectoryHandler.CreateFile(filename, "name-value", null);

            nameValuePairsFile.WriteAll(null, contents, false);

            IFileContainer fileContainer = rootDirectoryHandler.OpenFile(filename);

            string dumpDestination = Path.GetTempFileName();
            File.Delete(dumpDestination);

            try
            {
                using (TimedLock.Lock(fileContainer.FileHandler))
                    fileContainer.FileHandler.Dump(dumpDestination, RootUserId);

                INameValuePairsHandler restored = (INameValuePairsHandler)rootDirectoryHandler.RestoreFile(
                    filename + "restored", "name-value", dumpDestination, RootUserId);

                foreach (string key in new string[] { "a", "b", "c" })
                {
                    Assert.IsTrue(restored.Contains(key), "Name-values missing value");
                    Assert.AreEqual(contents[key], restored[key], "Value not saved correctly");
                }
            }
            finally
            {
                File.Delete(dumpDestination);
            }
        }

        [Test]
        [ExpectedException(typeof(SecurityException))]
        public void TestAnonymousUserCantDumpUserDB()
        {
            IFileContainer userDB = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile("/Users/UserDB");

            string dumpDestination = Path.GetTempFileName();
            File.Delete(dumpDestination);

            try
            {
                using (TimedLock.Lock(userDB.FileHandler))
                    userDB.FileHandler.Dump(dumpDestination, new ID<IUserOrGroup, Guid>(Guid.Empty));
            }
            finally
            {
                File.Delete(dumpDestination);
            }
        }

        [Test]
        public void TestDumpRestoreDirectory()
        {
            IDirectoryHandler rootDir = FileHandlerFactoryLocator.FileSystemResolver.RootDirectoryHandler;

            string filename = "TestDumpRestoreDirectory" + SRandom.Next();
            IDirectoryHandler sourceDir = (IDirectoryHandler)rootDir.CreateFile(filename, "directory", RootUserId);

            ITextHandler textFile = (ITextHandler)sourceDir.CreateFile("text", "text", RootUserId);

            string textContents = "buyo r3wfrw hu78ho8h vrwfqg 6uyt6duydt fr3qwf34\n\n\n\t\t\tggfiwgfugfugfu";
            textFile.WriteAll(null, textContents);

            INameValuePairsHandler nameValuePairs = (INameValuePairsHandler)sourceDir.CreateFile("nvps", "name-value", RootUserId);

            Dictionary<string, string> pairs = new Dictionary<string, string>();
            pairs["h"] = "esrgthgtr";
            pairs["q"] = "hun8onni8n";

            sourceDir.IndexFile = "nvps";

            nameValuePairs.WriteAll(null, pairs, false);

            IDirectoryHandler subDirectory = (IDirectoryHandler)sourceDir.CreateFile("subdir", "directory", RootUserId);
            ITextHandler subTextHandler = (ITextHandler)subDirectory.CreateFile("subtext", "text", RootUserId);

            string subText = "hugr5w4hv78w4ogh8ow7hgfo78swhgo78shgo87swhfgwhgo8wsghogt87aeh8gt7hswgtsgh7sgsehg9stuhgoiserthg8rswhgo78sw4";

            subTextHandler.WriteAll(null, subText);

            string dumpDestination = Path.GetTempFileName();
            File.Delete(dumpDestination);

            IDirectoryHandler restoredDir;
            try
            {
                using (TimedLock.Lock(sourceDir))
                    sourceDir.Dump(dumpDestination, RootUserId);

                string restoredFileName = "Restored_" + filename;

                restoredDir = (IDirectoryHandler)rootDir.RestoreFile(
                    restoredFileName, "directory", dumpDestination, RootUserId);
            }
            finally
            {
				try
				{
                		Directory.Delete(dumpDestination, true);
				} catch {}
            }

            Dictionary<string, IFileContainer> restoredFiles = new Dictionary<string, IFileContainer>();
            foreach (IFileContainer fileContainer in restoredDir.Files)
                restoredFiles[fileContainer.Filename] = fileContainer;

            Assert.AreEqual(3, restoredFiles.Count, "Wrong number of files resored");

            Assert.IsTrue(restoredFiles.ContainsKey("text"), "text file not present");
            string restoredTextContents = restoredFiles["text"].CastFileHandler<ITextHandler>().ReadAll();
            Assert.AreEqual(textContents, restoredTextContents, "Text file saved incorrectly");

            Assert.IsTrue(restoredFiles.ContainsKey("nvps"), "Name-value pairs not restored correctly");
            Dictionary<string, string> restoredPairs = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> kvp in restoredFiles["nvps"].CastFileHandler<INameValuePairsHandler>())
                restoredPairs.Add(kvp.Key, kvp.Value);
            Assert.AreEqual(pairs, restoredPairs, "Name-value pairs restored incorrectly");

            Assert.IsTrue(restoredFiles.ContainsKey("subdir"), "Subdirectory not present");
            IDirectoryHandler restoredSubDirectory = restoredFiles["subdir"].CastFileHandler<IDirectoryHandler>();
            Assert.IsTrue(restoredSubDirectory.IsFilePresent("subtext"), "Subdirectory text file not present");
            ITextHandler restoredSubTextHander = restoredSubDirectory.OpenFile("subtext").CastFileHandler<ITextHandler>();
            Assert.AreEqual(subText, restoredSubTextHander.ReadAll(), "Subdirectory text file saved incorrectly");

            Assert.AreEqual(sourceDir.IndexFile, restoredDir.IndexFile, "Index file not properly restored");
        }

        [Test]
        public void TestDumpRestoreDirectoryWithPermissions()
        {
            IDirectoryHandler rootDir = FileHandlerFactoryLocator.FileSystemResolver.RootDirectoryHandler;

            string filename = "TestDumpRestoreDirectoryWithPermissions" + SRandom.Next();
            IDirectoryHandler sourceDir = (IDirectoryHandler)rootDir.CreateFile(filename, "directory", RootUserId);

            ITextHandler textFile = (ITextHandler)sourceDir.CreateFile("text", "text", RootUserId);

            string textContents = "buyo r3wfrw hu78ho8h vrwfqg 6uyt6duydt fr3qwf34\n\n\n\t\t\tggfiwgfugfugfu";
            textFile.WriteAll(null, textContents);

            ID<IUserOrGroup, Guid> trashId = new ID<IUserOrGroup, Guid>(Guid.NewGuid());

            IUserFactory userFactory = FileHandlerFactoryLocator.UserFactory;
            sourceDir.SetPermission(null, "text", userFactory.AnonymousUser.Id, FilePermissionEnum.Read, true, false);
            sourceDir.SetPermission(null, "text", userFactory.AuthenticatedUsers.Id, FilePermissionEnum.Write, false, false);
            sourceDir.SetPermission(null, "text", userFactory.LocalUsers.Id, FilePermissionEnum.Administer, true, false);
            sourceDir.SetPermission(null, "text", trashId, FilePermissionEnum.Read, true, false);

            string dumpDestination = Path.GetTempFileName();
            File.Delete(dumpDestination);

            IDirectoryHandler restoredDir;
            try
            {
                using (TimedLock.Lock(sourceDir))
                    sourceDir.Dump(dumpDestination, RootUserId);

                string restoredFileName = "Restored_" + filename;

                restoredDir = (IDirectoryHandler)rootDir.RestoreFile(
                    restoredFileName, "directory", dumpDestination, RootUserId);

                Dictionary<string, IFileContainer> restoredFiles = new Dictionary<string, IFileContainer>();
                foreach (IFileContainer fileContainer in restoredDir.Files)
                    restoredFiles[fileContainer.Filename] = fileContainer;

                Assert.AreEqual(1, restoredFiles.Count, "Wrong number of files resored");

                Assert.IsTrue(restoredFiles.ContainsKey("text"), "text file not present");
                string restoredTextContents = restoredFiles["text"].CastFileHandler<ITextHandler>().ReadAll();
                Assert.AreEqual(textContents, restoredTextContents, "Text file saved incorrectly");

                Dictionary<ID<IUserOrGroup, Guid>, FilePermission> permissionsByUserId = new Dictionary<ID<IUserOrGroup, Guid>, FilePermission>();
                foreach (FilePermission permission in restoredDir.GetPermissions("text"))
                    permissionsByUserId[permission.UserOrGroupId] = permission;

                Assert.IsFalse(permissionsByUserId.ContainsKey(trashId), "Non-system userIds aren't supposed to be dumped");

                Assert.Contains(userFactory.AnonymousUser.Id, permissionsByUserId.Keys, "Anonymous user ID missing");
                Assert.AreEqual(true, permissionsByUserId[userFactory.AnonymousUser.Id].Inherit, "Wrong value for inherit saved");

                Assert.Contains(userFactory.AuthenticatedUsers.Id, permissionsByUserId.Keys, "Authenticated user ID missing");
                Assert.AreEqual(false, permissionsByUserId[userFactory.AuthenticatedUsers.Id].Inherit, "Wrong value for inherit saved");

                Assert.Contains(userFactory.LocalUsers.Id, permissionsByUserId.Keys, "Local user ID missing");
                Assert.AreEqual(true, permissionsByUserId[userFactory.LocalUsers.Id].Inherit, "Wrong value for inherit saved");

                Assert.AreEqual(3, permissionsByUserId.Count, "Wrong number of permissions saved");
            }
            finally
            {
                try
                {
                    Directory.Delete(dumpDestination, true);
                }
                catch { }
            }
        }

        [Test]
        public void TestDumpRestoreDatabase()
        {
            IDirectoryHandler rootDir = FileHandlerFactoryLocator.FileSystemResolver.RootDirectoryHandler;

            string filename = "TestDumpRestoreDatabase" + SRandom.Next();
            IDatabaseHandler databaseHandler = (IDatabaseHandler)rootDir.CreateFile(filename, "database", RootUserId);

            int testVal = SRandom.Next<int>();

            DbCommand command = databaseHandler.Connection.CreateCommand();

            command.CommandText = "create table testtable (testcol int)";
            command.ExecuteNonQuery();

            command = databaseHandler.Connection.CreateCommand();
            command.CommandText = "insert into testtable (testcol) values (@testVal)";

            DbParameter dbParameter = command.CreateParameter();
            dbParameter.ParameterName = "@testVal";
            dbParameter.Value = testVal;
            command.Parameters.Add(dbParameter);
            
            command.ExecuteNonQuery();

            string dumpDestination = Path.GetTempFileName();
            File.Delete(dumpDestination);

            IDatabaseHandler restoredDatabase;

            try
            {
                using (TimedLock.Lock(databaseHandler))
                    databaseHandler.Dump(dumpDestination, RootUserId);

                string restoredFileName = "Restored_" + filename;

                restoredDatabase = (IDatabaseHandler)rootDir.RestoreFile(
                    restoredFileName, "database", dumpDestination, RootUserId);
				
				Assert.IsNotNull(restoredDatabase);

            }
            finally
            {
                try
                {
                    File.Delete(dumpDestination);
                }
                catch { }
            }

            command = databaseHandler.Connection.CreateCommand();
            command.CommandText = "select testcol from testtable";
            object scalarResult = command.ExecuteScalar();

            Assert.AreEqual(testVal, scalarResult, "Unexpected result from RunQueryForScalar");
        }
    }
}