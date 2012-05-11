// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using NUnit.Framework;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Spring.Config;


namespace ObjectCloud.Disk.Test
{
    [TestFixture]
    public class FileRelationshipsTest : TestBase
    {
        protected override void DoAdditionalSetup()
        {
            base.DoAdditionalSetup();

            User1 = FileHandlerFactoryLocator.UserManagerHandler.CreateUser("User" + SRandom.Next<uint>().ToString(), "pw", "test user");
            User2 = FileHandlerFactoryLocator.UserManagerHandler.CreateUser("User" + SRandom.Next<uint>().ToString(), "pw", "test user");
            User3 = FileHandlerFactoryLocator.UserManagerHandler.CreateUser("User" + SRandom.Next<uint>().ToString(), "pw", "test user");
            Group = FileHandlerFactoryLocator.UserManagerHandler.CreateGroup("Group" + SRandom.Next<uint>().ToString(), "test user", null, GroupType.Private);
        }

        protected override void DoAdditionalTearDown()
        {
            FileHandlerFactoryLocator.UserManagerHandler.DeleteUser(User1.Name);
            FileHandlerFactoryLocator.UserManagerHandler.DeleteUser(User2.Name);
            FileHandlerFactoryLocator.UserManagerHandler.DeleteUser(User3.Name);
            FileHandlerFactoryLocator.UserManagerHandler.DeleteGroup(Group.Name);

            base.DoAdditionalTearDown();
        }

        IUser User1;
        IUser User2;
        IUser User3;
        IGroup Group;

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

            directory.AddRelationship(parentContainer, related1Container, "aaa", false);
            directory.AddRelationship(parentContainer, related2Container, "aaa", false);
            directory.AddRelationship(parentContainer, related3Container, "bbb", false);
            directory.AddRelationship(parentContainer, related4Container, "bbb", false);

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
                directory.GetRelatedFiles(FileHandlerFactoryLocator.UserFactory.RootUser.Id, parentContainer.FileId, new string[] { "aaa" }.ToHashSet(), null, null, null, null));

            Assert.IsTrue(fileContainers.Contains(related1Container), "File missing");
            Assert.IsTrue(fileContainers.Contains(related2Container), "File missing");

            // Test with both types of relationships
            fileContainers = new List<IFileContainer>(
                directory.GetRelatedFiles(FileHandlerFactoryLocator.UserFactory.RootUser.Id, parentContainer.FileId, new string[] { "aaa", "bbb" }.ToHashSet(), null, null, null, null));

            Assert.IsTrue(fileContainers.Contains(related1Container), "File missing");
            Assert.IsTrue(fileContainers.Contains(related2Container), "File missing");
            Assert.IsTrue(fileContainers.Contains(related3Container), "File missing");
            Assert.IsTrue(fileContainers.Contains(related4Container), "File missing");

            // Test with "bbb" type of relationship
            fileContainers = new List<IFileContainer>(
                directory.GetRelatedFiles(FileHandlerFactoryLocator.UserFactory.RootUser.Id, parentContainer.FileId, new string[] { "bbb" }.ToHashSet(), null, null, null, null));

            Assert.IsTrue(fileContainers.Contains(related3Container), "File missing");
            Assert.IsTrue(fileContainers.Contains(related4Container), "File missing");

            // Test with "txt" extension
            fileContainers = new List<IFileContainer>(
                directory.GetRelatedFiles(FileHandlerFactoryLocator.UserFactory.RootUser.Id, parentContainer.FileId, null, new string[] { "txt" }.ToHashSet(), null, null, null));

            Assert.IsTrue(fileContainers.Contains(related1Container), "File missing");
            Assert.IsTrue(fileContainers.Contains(related3Container), "File missing");

            // Test with "xml" extension
            fileContainers = new List<IFileContainer>(
                directory.GetRelatedFiles(FileHandlerFactoryLocator.UserFactory.RootUser.Id, parentContainer.FileId, null, new string[] { "xml" }.ToHashSet(), null, null, null));

            Assert.IsTrue(fileContainers.Contains(related2Container), "File missing");
            Assert.IsTrue(fileContainers.Contains(related4Container), "File missing");

            // Test with both types of extensions
            fileContainers = new List<IFileContainer>(
                directory.GetRelatedFiles(FileHandlerFactoryLocator.UserFactory.RootUser.Id, parentContainer.FileId, null, new string[] { "txt", "xml" }.ToHashSet(), null, null, null));

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

            Assert.AreEqual(0, fileContainers.Count, "Unexpected file containers returned");
        }

        [Test]
        public void TestPermissions()
        {
            IDirectoryHandler directory = (IDirectoryHandler)FileHandlerFactoryLocator.FileSystemResolver.RootDirectoryHandler.CreateFile(
                "TestPermissions" + SRandom.Next<uint>(),
                "directory",
                FileHandlerFactoryLocator.UserFactory.RootUser.Id);

            IFileContainer parentContainer = directory.CreateFile("parent", "text", null).FileContainer;
            IFileContainer related1Container = directory.CreateFile("related1.txt", "text", null).FileContainer;
            IFileContainer related2Container = directory.CreateFile("related2.xml", "text", null).FileContainer;
            IFileContainer related3Container = directory.CreateFile("related3.txt", "text", null).FileContainer;
            IFileContainer related4Container = directory.CreateFile("related4.xml", "text", null).FileContainer;

            // Create relationships
            directory.AddRelationship(parentContainer, related1Container, "aaa", false);
            directory.AddRelationship(parentContainer, related2Container, "aaa", false);
            directory.AddRelationship(parentContainer, related3Container, "bbb", false);
            directory.AddRelationship(parentContainer, related4Container, "bbb", false);

            // Create users and group
            FileHandlerFactoryLocator.UserManagerHandler.AddUserToGroup(User1.Id, Group.Id);

            directory.SetPermission(null, related1Container.Filename, new ID<IUserOrGroup, Guid>[] { User1.Id, Group.Id, User2.Id }, FilePermissionEnum.Read, false, false);
            directory.FileContainer.ParentDirectoryHandler.SetPermission(null, directory.FileContainer.Filename, new ID<IUserOrGroup, Guid>[] { User3.Id }, FilePermissionEnum.Read, true, false);

            // Test user1
            var fileContainers = directory.GetRelatedFiles(User1.Id, parentContainer.FileId, null, null, null, null, null).Select(f => f.FileId);

            Assert.IsTrue(fileContainers.Contains(related1Container.FileId), "File missing");
            Assert.IsTrue(!fileContainers.Contains(related2Container.FileId), "File missing");
            Assert.IsTrue(!fileContainers.Contains(related3Container.FileId), "File missing");
            Assert.IsTrue(!fileContainers.Contains(related4Container.FileId), "File missing");

            // Test user2
            fileContainers = 
                directory.GetRelatedFiles(User2.Id, parentContainer.FileId, null, null, null, null, null).Select(f => f.FileId);

            Assert.IsTrue(fileContainers.Contains(related1Container.FileId), "File missing");
            Assert.IsTrue(!fileContainers.Contains(related2Container.FileId), "File missing");
            Assert.IsTrue(!fileContainers.Contains(related3Container.FileId), "File missing");
            Assert.IsTrue(!fileContainers.Contains(related4Container.FileId), "File missing");

            // Test user3
            fileContainers = 
                directory.GetRelatedFiles(User3.Id, parentContainer.FileId, null, null, null, null, null).Select(f => f.FileId);

            Assert.IsTrue(fileContainers.Contains(related1Container.FileId), "File missing");
            Assert.IsTrue(fileContainers.Contains(related2Container.FileId), "File missing");
            Assert.IsTrue(fileContainers.Contains(related3Container.FileId), "File missing");
            Assert.IsTrue(fileContainers.Contains(related4Container.FileId), "File missing");
        }
    }
}
