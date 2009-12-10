// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using NUnit.Framework;

using Spring.Context;
using Spring.Context.Support;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Spring.Config;


namespace ObjectCloud.Disk.Test
{
    [TestFixture]
    public class FileRelationshipsTest : TestBase
    {
        [Test]
        public void TestAddRelationshipSanity()
        {
            IDirectoryHandler directory = (IDirectoryHandler)FileHandlerFactoryLocator.FileSystemResolver.RootDirectoryHandler.CreateFile(
                "TestAddRelationshipSanity" + SRandom.Next<uint>(),
                "directory",
                FileHandlerFactoryLocator.UserFactory.RootUser.Id);

            IFileContainer parentContainer = directory.CreateFile("parent", "text", null).FileContainer;
            IFileContainer related1Container = directory.CreateFile("related1.txt", "text", null).FileContainer;
            IFileContainer related2Container = directory.CreateFile("related2.xml", "text", null).FileContainer;
            IFileContainer related3Container = directory.CreateFile("related3.txt", "text", null).FileContainer;
            IFileContainer related4Container = directory.CreateFile("related4.xml", "text", null).FileContainer;

            directory.AddRelationship(parentContainer, related1Container, "aaa");
            directory.AddRelationship(parentContainer, related2Container, "aaa");
            directory.AddRelationship(parentContainer, related3Container, "bbb");
            directory.AddRelationship(parentContainer, related4Container, "bbb");

            List<IFileContainer> fileContainers;

            // Test with just the parent ID
            fileContainers = new List<IFileContainer>(
                directory.GetRelatedFiles(FileHandlerFactoryLocator.UserFactory.RootUser.Id, parentContainer.FileId, null, null, null, null, null));

            Assert.IsTrue(fileContainers.Contains(related1Container), "File missing");
            Assert.IsTrue(fileContainers.Contains(related2Container), "File missing");
            Assert.IsTrue(fileContainers.Contains(related3Container), "File missing");
            Assert.IsTrue(fileContainers.Contains(related4Container), "File missing");

            // Test with "aaa" type relationship
            fileContainers = new List<IFileContainer>(
                directory.GetRelatedFiles(FileHandlerFactoryLocator.UserFactory.RootUser.Id, parentContainer.FileId, new string[] { "aaa" }, null, null, null, null));

            Assert.IsTrue(fileContainers.Contains(related1Container), "File missing");
            Assert.IsTrue(fileContainers.Contains(related2Container), "File missing");

            // Test with both types of relationships
            fileContainers = new List<IFileContainer>(
                directory.GetRelatedFiles(FileHandlerFactoryLocator.UserFactory.RootUser.Id, parentContainer.FileId, new string[] { "aaa", "bbb" }, null, null, null, null));

            Assert.IsTrue(fileContainers.Contains(related1Container), "File missing");
            Assert.IsTrue(fileContainers.Contains(related2Container), "File missing");
            Assert.IsTrue(fileContainers.Contains(related3Container), "File missing");
            Assert.IsTrue(fileContainers.Contains(related4Container), "File missing");

            // Test with "bbb" type of relationship
            fileContainers = new List<IFileContainer>(
                directory.GetRelatedFiles(FileHandlerFactoryLocator.UserFactory.RootUser.Id, parentContainer.FileId, new string[] { "bbb" }, null, null, null, null));

            Assert.IsTrue(fileContainers.Contains(related3Container), "File missing");
            Assert.IsTrue(fileContainers.Contains(related4Container), "File missing");

            // Test with "txt" extension
            fileContainers = new List<IFileContainer>(
                directory.GetRelatedFiles(FileHandlerFactoryLocator.UserFactory.RootUser.Id, parentContainer.FileId, null, new string[] { "txt" }, null, null, null));

            Assert.IsTrue(fileContainers.Contains(related1Container), "File missing");
            Assert.IsTrue(fileContainers.Contains(related3Container), "File missing");

            // Test with "xml" extension
            fileContainers = new List<IFileContainer>(
                directory.GetRelatedFiles(FileHandlerFactoryLocator.UserFactory.RootUser.Id, parentContainer.FileId, null, new string[] { "xml" }, null, null, null));

            Assert.IsTrue(fileContainers.Contains(related2Container), "File missing");
            Assert.IsTrue(fileContainers.Contains(related4Container), "File missing");

            // Test with both types of extensions
            fileContainers = new List<IFileContainer>(
                directory.GetRelatedFiles(FileHandlerFactoryLocator.UserFactory.RootUser.Id, parentContainer.FileId, null, new string[] { "txt", "xml" }, null, null, null));

            Assert.IsTrue(fileContainers.Contains(related1Container), "File missing");
            Assert.IsTrue(fileContainers.Contains(related2Container), "File missing");
            Assert.IsTrue(fileContainers.Contains(related3Container), "File missing");
            Assert.IsTrue(fileContainers.Contains(related4Container), "File missing");

            // Test delete
            directory.DeleteRelationship(parentContainer, related1Container, "aaa");
            directory.DeleteRelationship(parentContainer, related2Container, "aaa");
            directory.DeleteRelationship(parentContainer, related3Container, "bbb");
            directory.DeleteRelationship(parentContainer, related4Container, "bbb");

            fileContainers = new List<IFileContainer>(
                directory.GetRelatedFiles(FileHandlerFactoryLocator.UserFactory.RootUser.Id, parentContainer.FileId, null, null, null, null, null));

            Assert.IsTrue(0 == fileContainers.Count);
        }

        [Test]
        public void TestPermissions()
        {
            IDirectoryHandler directory = (IDirectoryHandler)FileHandlerFactoryLocator.FileSystemResolver.RootDirectoryHandler.CreateFile(
                "TestAddRelationshipSanity" + SRandom.Next<uint>(),
                "directory",
                FileHandlerFactoryLocator.UserFactory.RootUser.Id);

            IFileContainer parentContainer = directory.CreateFile("parent", "text", null).FileContainer;
            IFileContainer related1Container = directory.CreateFile("related1.txt", "text", null).FileContainer;
            IFileContainer related2Container = directory.CreateFile("related2.xml", "text", null).FileContainer;
            IFileContainer related3Container = directory.CreateFile("related3.txt", "text", null).FileContainer;
            IFileContainer related4Container = directory.CreateFile("related4.xml", "text", null).FileContainer;

            // Create relationships
            directory.AddRelationship(parentContainer, related1Container, "aaa");
            directory.AddRelationship(parentContainer, related2Container, "aaa");
            directory.AddRelationship(parentContainer, related3Container, "bbb");
            directory.AddRelationship(parentContainer, related4Container, "bbb");

            // Create users and group
            IUser user1 = FileHandlerFactoryLocator.UserManagerHandler.CreateUser("User" + SRandom.Next<uint>().ToString(), "pw");
            IUser user2 = FileHandlerFactoryLocator.UserManagerHandler.CreateUser("User" + SRandom.Next<uint>().ToString(), "pw");
            IUser user3 = FileHandlerFactoryLocator.UserManagerHandler.CreateUser("User" + SRandom.Next<uint>().ToString(), "pw");
            IGroup group = FileHandlerFactoryLocator.UserManagerHandler.CreateGroup("Group" + SRandom.Next<uint>().ToString(), null, GroupType.Private);
            FileHandlerFactoryLocator.UserManagerHandler.AddUserToGroup(user1.Id, group.Id);

            directory.SetPermission(null, related1Container.Filename, user1.Id, FilePermissionEnum.Read, false, false);
            directory.SetPermission(null, related2Container.Filename, group.Id, FilePermissionEnum.Read, false, false);
            directory.SetPermission(null, related3Container.Filename, user2.Id, FilePermissionEnum.Read, false, false);
            directory.FileContainer.ParentDirectoryHandler.SetPermission(null, directory.FileContainer.Filename, user3.Id, FilePermissionEnum.Read, true, false);

            List<IFileContainer> fileContainers;

            // Test user1
            fileContainers = new List<IFileContainer>(
                directory.GetRelatedFiles(user1.Id, parentContainer.FileId, null, null, null, null, null));

            Assert.IsTrue(fileContainers.Contains(related1Container), "File missing");
            Assert.IsTrue(fileContainers.Contains(related2Container), "File missing");
            Assert.IsTrue(!fileContainers.Contains(related3Container), "File missing");
            Assert.IsTrue(!fileContainers.Contains(related4Container), "File missing");

            // Test user2
            fileContainers = new List<IFileContainer>(
                directory.GetRelatedFiles(user2.Id, parentContainer.FileId, null, null, null, null, null));

            Assert.IsTrue(!fileContainers.Contains(related1Container), "File missing");
            Assert.IsTrue(!fileContainers.Contains(related2Container), "File missing");
            Assert.IsTrue(fileContainers.Contains(related3Container), "File missing");
            Assert.IsTrue(!fileContainers.Contains(related4Container), "File missing");

            // Test user3
            fileContainers = new List<IFileContainer>(
                directory.GetRelatedFiles(user3.Id, parentContainer.FileId, null, null, null, null, null));

            Assert.IsTrue(fileContainers.Contains(related1Container), "File missing");
            Assert.IsTrue(fileContainers.Contains(related2Container), "File missing");
            Assert.IsTrue(fileContainers.Contains(related3Container), "File missing");
            Assert.IsTrue(fileContainers.Contains(related4Container), "File missing");
        }
    }
}
