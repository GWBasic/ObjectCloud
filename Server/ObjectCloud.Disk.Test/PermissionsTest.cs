// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;

using Spring.Context;
using Spring.Context.Support;

using NUnit.Framework;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Disk.Implementation;

namespace ObjectCloud.Disk.Test
{
    [TestFixture]
    public class PermissionsTest : TestBase
    {
        protected override void DoAdditionalSetup()
        {
            rootDirectoryHandler = FileHandlerFactoryLocator.FileSystemResolver.RootDirectoryHandler;

            IFileContainer userDBFile = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile("/Users/UserDB");
            IUserManagerHandler userManagerHandler = userDBFile.CastFileHandler<IUserManagerHandler>();

            TestUser_1 = userManagerHandler.CreateUser("user1", "user1");
            TestUser_2 = userManagerHandler.CreateUser("user2", "user2");

            TestGroup_1 = userManagerHandler.CreateGroup("group1", TestUser_1.Id, GroupType.Private);
            TestGroup_2 = userManagerHandler.CreateGroup("group2", TestUser_2.Id, GroupType.Private);
        }

        protected override void DoAdditionalTearDown()
        {
            IFileContainer userDBFile = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile("/Users/UserDB");
            IUserManagerHandler userManagerHandler = userDBFile.CastFileHandler<IUserManagerHandler>();

            userManagerHandler.DeleteUser("user1");
            userManagerHandler.DeleteUser("user2");
			
			userManagerHandler.DeleteGroup("group1");
			userManagerHandler.DeleteGroup("group2");
        }

        IUser TestUser_1;
        IUser TestUser_2;
        IDirectoryHandler rootDirectoryHandler;
		IGroup TestGroup_1;
		IGroup TestGroup_2;

        [Test]
        public void TestAddPermission()
        {
            string filename = SRandom.Next<long>().ToString();
            rootDirectoryHandler.CreateFile(filename, "text", TestUser_1.Id);

            rootDirectoryHandler.SetPermission(null, filename, TestUser_2.Id, FilePermissionEnum.Administer, false, false);

            IFileContainer file = rootDirectoryHandler.OpenFile(filename);

            Assert.AreEqual(FilePermissionEnum.Administer, file.LoadPermission(TestUser_2.Id), "Wrong permission persisted");
        }

        [Test]
        public void TestUpdatePermission()
        {
            string filename = SRandom.Next<long>().ToString();
            rootDirectoryHandler.CreateFile(filename, "text", TestUser_2.Id);

            rootDirectoryHandler.SetPermission(null, filename, TestUser_1.Id, FilePermissionEnum.Write, false, false);

            IFileContainer file = rootDirectoryHandler.OpenFile(filename);

            Assert.AreEqual(FilePermissionEnum.Write, file.LoadPermission(TestUser_1.Id), "Wrong permission persisted");

            rootDirectoryHandler.SetPermission(null, filename, TestUser_1.Id, FilePermissionEnum.Read, false, false);

            file = rootDirectoryHandler.OpenFile(filename);

            Assert.AreEqual(FilePermissionEnum.Read, file.LoadPermission(TestUser_1.Id), "Wrong permission persisted");
        }

        [Test]
        public void TestDeletePermission()
        {
            string filename = SRandom.Next<long>().ToString();
            rootDirectoryHandler.CreateFile(filename, "text", TestUser_2.Id);

            rootDirectoryHandler.SetPermission(null, filename, TestUser_1.Id, FilePermissionEnum.Write, false, false);

            IFileContainer file = rootDirectoryHandler.OpenFile(filename);

            Assert.AreEqual(FilePermissionEnum.Write, file.LoadPermission(TestUser_1.Id), "Wrong permission persisted");

            rootDirectoryHandler.RemovePermission(filename, TestUser_1.Id);

            file = rootDirectoryHandler.OpenFile(filename);

            Assert.IsNull(file.LoadPermission(TestUser_1.Id), "Permission not removed for user 2");
        }

        [Test]
        public void TestDeleteAfterAddingPermissions()
        {
            string filename = SRandom.Next<long>().ToString();
            rootDirectoryHandler.CreateFile(filename, "text", TestUser_2.Id);

            rootDirectoryHandler.SetPermission(null, filename, TestUser_1.Id, FilePermissionEnum.Write, false, false);
            rootDirectoryHandler.SetPermission(null, filename, TestUser_2.Id, FilePermissionEnum.Write, false, false);

            // This shouldn't crash
            rootDirectoryHandler.DeleteFile(null, filename);
        }
		
		[Test]
		[ExpectedException(typeof(UserAlreadyExistsException))]
		public void TestCannotCreateUserWithSameNameAsGroup()
		{
			IUserManagerHandler userManagerHandler = FileHandlerFactoryLocator.UserManagerHandler;
			
			string name = "DuplicateUser" + SRandom.Next<long>().ToString();
			
			userManagerHandler.CreateUser(name, "123");
            userManagerHandler.CreateGroup(name, FileHandlerFactoryLocator.UserManagerHandler.Root.Id, GroupType.Private);
		}
		
		[Test]
		[ExpectedException(typeof(UserAlreadyExistsException))]
		public void TestCannotCreateGroupWithSameNameAsUser()
		{
			IUserManagerHandler userManagerHandler = FileHandlerFactoryLocator.UserManagerHandler;
			
			string name = "DuplicateGroup" + SRandom.Next<long>().ToString();

            userManagerHandler.CreateGroup(name, FileHandlerFactoryLocator.UserManagerHandler.Root.Id, GroupType.Private);
			userManagerHandler.CreateUser(name, "123");
		}
		
		[Test]
		public void TestCreateAndGetGroup()
		{
			IUserManagerHandler userManagerHandler = FileHandlerFactoryLocator.UserManagerHandler;
			
			string name = "Group" + SRandom.Next<long>().ToString();
			ID<IUserOrGroup, Guid> groupId = new ID<IUserOrGroup, Guid>(Guid.NewGuid());

            userManagerHandler.CreateGroup(name, TestUser_1.Id, groupId, false, false, GroupType.Private);
			
			IUserOrGroup groupObj = userManagerHandler.GetUserOrGroupOrOpenId(name);
			
			Assert.IsInstanceOfType(typeof(IGroup), groupObj, "Wrong type returned for a group");
			
			IGroup group = (IGroup)groupObj;
			
			Assert.AreEqual(name.ToLowerInvariant(), group.Name, "Mismatch on name");
			Assert.AreEqual(groupId, group.Id, "Mismatch on ID");
			Assert.AreEqual(TestUser_1.Id, group.OwnerId, "Mismatch on owner");
		}

			
		[Test]
		public void TestDeleteGroup()
		{
			IUserManagerHandler userManagerHandler = FileHandlerFactoryLocator.UserManagerHandler;
			
			string name = "GroupToDelete" + SRandom.Next<long>().ToString();
			ID<IUserOrGroup, Guid> groupId = new ID<IUserOrGroup, Guid>(Guid.NewGuid());

            userManagerHandler.CreateGroup(name, TestUser_1.Id, groupId, false, false, GroupType.Private);
			
			IUserOrGroup groupObj = userManagerHandler.GetUserOrGroupOrOpenId(name);
			
			Assert.IsInstanceOfType(typeof(IGroup), groupObj, "Wrong type returned for a group");
			
			userManagerHandler.DeleteGroup(name);
			
			try
			{
				userManagerHandler.GetUserOrGroupOrOpenId(name);
				Assert.Fail("Group not deleted");
			}
			catch (UnknownUser) { }
		}
		
		[Test]
		public void TestAddUserToGroups()
		{
			IUserManagerHandler userManagerHandler = FileHandlerFactoryLocator.UserManagerHandler;

			userManagerHandler.AddUserToGroup(TestUser_1.Id, TestGroup_1.Id);
			userManagerHandler.AddUserToGroup(TestUser_1.Id, TestGroup_2.Id);
			
			List<ID<IUserOrGroup, Guid>> groupIds = new List<ID<IUserOrGroup, Guid>>(
				userManagerHandler.GetGroupIdsThatUserIsIn(TestUser_1.Id));
			
			Assert.IsTrue(groupIds.Contains(TestGroup_1.Id), "User was not added to test group 1");
			Assert.IsTrue(groupIds.Contains(TestGroup_2.Id), "User was not added to test group 2");
			Assert.AreEqual(2, groupIds.Count, "Wrong number of GroupIDs returned");
			
			userManagerHandler.RemoveUserFromGroup(TestUser_1.Id, TestGroup_1.Id);
			userManagerHandler.RemoveUserFromGroup(TestUser_1.Id, TestGroup_2.Id);
			
			groupIds = new List<ID<IUserOrGroup, Guid>>(
				userManagerHandler.GetGroupIdsThatUserIsIn(TestUser_1.Id));

			Assert.AreEqual(0, groupIds.Count, "Wrong number of GroupIDs returned");
		}
		
		[Test]
		public void TestInheritPermissions()
		{
			string dirname = "TestInheritPermissions" + SRandom.Next<long>().ToString();
			
			IDirectoryHandler dir = (IDirectoryHandler)rootDirectoryHandler.CreateFile(dirname, "directory", TestUser_1.Id);
			
			string filename = "file.txt";
			
            dir.CreateFile(filename, "text", TestUser_1.Id);

			IFileContainer fileContainer = dir.OpenFile(filename);
			
			Assert.IsNull(fileContainer.LoadPermission(TestUser_2.Id), "User should not have any permissions to the file");
			
            rootDirectoryHandler.SetPermission(null, dirname, TestUser_2.Id, FilePermissionEnum.Read, true, false);

			FilePermissionEnum? filePermission = fileContainer.LoadPermission(TestUser_2.Id);
			Assert.IsNotNull(filePermission, "User should have read permissions to the file");
			Assert.AreEqual(FilePermissionEnum.Read, filePermission.Value, "User should have read permission to the file");
		}
		
		[Test]
		public void TestEverybodyInheritPermissions()
		{
			string dirname = "TestEverybodyInheritPermissions" + SRandom.Next<long>().ToString();
			
			IDirectoryHandler dir = (IDirectoryHandler)rootDirectoryHandler.CreateFile(dirname, "directory", TestUser_1.Id);
			
			string filename = "file.txt";
			
            dir.CreateFile(filename, "text", TestUser_1.Id);

			IFileContainer fileContainer = dir.OpenFile(filename);
			
			Assert.IsNull(fileContainer.LoadPermission(TestUser_2.Id), "User should not have any permissions to the file");

            rootDirectoryHandler.SetPermission(null, dirname, FileHandlerFactoryLocator.UserFactory.Everybody.Id, FilePermissionEnum.Read, true, false);

			FilePermissionEnum? filePermission = fileContainer.LoadPermission(TestUser_2.Id);
			Assert.IsNotNull(filePermission, "User should have read permissions to the file");
			Assert.AreEqual(FilePermissionEnum.Read, filePermission.Value, "User should have read permission to the file");
		}
		
		[Test]
		public void TestEverybodyReadUserWritePermissions()
		{
			string dirname = "TestEverybodyReadUserWritePermissions" + SRandom.Next<long>().ToString();
			
			IDirectoryHandler dir = (IDirectoryHandler)rootDirectoryHandler.CreateFile(dirname, "directory", TestUser_1.Id);
			
			string filename = "file.txt";
			
            dir.CreateFile(filename, "text", TestUser_1.Id);

			IFileContainer fileContainer = dir.OpenFile(filename);
			
			Assert.IsNull(fileContainer.LoadPermission(TestUser_2.Id), "User should not have any permissions to the file");

            dir.SetPermission(null, filename, FileHandlerFactoryLocator.UserFactory.Everybody.Id, FilePermissionEnum.Read, true, false);
            dir.SetPermission(null, filename, TestUser_2.Id, FilePermissionEnum.Write, true, false);

			FilePermissionEnum? filePermission = fileContainer.LoadPermission(TestUser_2.Id);
			Assert.IsNotNull(filePermission, "User should have write permissions to the file");
			Assert.AreEqual(FilePermissionEnum.Write, filePermission.Value, "User should have write permission to the file");
		}
		
		[Test]
		public void TestEverybodyReadUserWriteInheritPermissions()
		{
			string dirname = "TestEverybodyReadUserWriteInheritPermissions" + SRandom.Next<long>().ToString();
			
			IDirectoryHandler dir = (IDirectoryHandler)rootDirectoryHandler.CreateFile(dirname, "directory", TestUser_1.Id);
			
			string filename = "file.txt";
			
            dir.CreateFile(filename, "text", TestUser_1.Id);

			IFileContainer fileContainer = dir.OpenFile(filename);
			
			Assert.IsNull(fileContainer.LoadPermission(TestUser_2.Id), "User should not have any permissions to the file");

            rootDirectoryHandler.SetPermission(null, dirname, FileHandlerFactoryLocator.UserFactory.Everybody.Id, FilePermissionEnum.Read, true, false);
            rootDirectoryHandler.SetPermission(null, dirname, TestUser_2.Id, FilePermissionEnum.Write, true, false);

			FilePermissionEnum? filePermission = fileContainer.LoadPermission(TestUser_2.Id);
			Assert.IsNotNull(filePermission, "User should have write permissions to the file");
			Assert.AreEqual(FilePermissionEnum.Write, filePermission.Value, "User should have write permission to the file");
		}
		
		[Test]
		[ExpectedException(typeof(CanNotDeleteBuiltInUserOrGroup))]
		public void TestCanNotDeleteBuiltInUser()
		{
			FileHandlerFactoryLocator.UserManagerHandler.DeleteUser("root");
		}
		
		[Test]
		[ExpectedException(typeof(CanNotDeleteBuiltInUserOrGroup))]
		public void TestCanNotDeleteBuiltInGroup()
		{
			FileHandlerFactoryLocator.UserManagerHandler.DeleteGroup("everybody");
		}
		
		[Test]
		[ExpectedException(typeof(CanNotEditMembershipOfSystemGroup))]
		public void TestCanNotAddUserToSystemGroup()
		{
			FileHandlerFactoryLocator.UserManagerHandler.AddUserToGroup(
				new ID<IUserOrGroup, Guid>(Guid.NewGuid()),
				FileHandlerFactoryLocator.UserFactory.AuthenticatedUsers.Id);
		}
		
		[Test]
		[ExpectedException(typeof(CanNotEditMembershipOfSystemGroup))]
		public void TestCanNotRemoveUserFromSystemGroup()
		{
			FileHandlerFactoryLocator.UserManagerHandler.AddUserToGroup(
				new ID<IUserOrGroup, Guid>(Guid.NewGuid()),
				FileHandlerFactoryLocator.UserFactory.LocalUsers.Id);
		}
	}
}
