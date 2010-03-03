// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Threading;

using NUnit.Framework;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;
using ObjectCloud.Disk.Implementation;

namespace ObjectCloud.Disk.Test
{
    [TestFixture]
    public class TestUserManagerHandler : TestBase
    {
        [Test]
        public void TestSetPassword()
        {
            IUserManagerHandler userManager = FileHandlerFactoryLocator.UserManagerHandler;

            IUser testUser = userManager.CreateUser("TestSetPassword" + SRandom.Next<long>().ToString(), "password");

            userManager.SetPassword(testUser.Id, "newpassword");

            // reload the user after the password change
            testUser = userManager.GetUser(testUser.Name, "newpassword");

            Assert.IsNotNull(testUser, "Error re-loading user with changed password");
        }

        [Test]
        public void TestGroupAliases()
        {
            IUserManagerHandler userManager = FileHandlerFactoryLocator.UserManagerHandler;

            IUser testUser = userManager.CreateUser("TestGroupAliases" + SRandom.Next<long>().ToString(), "password");

            IGroup groupA = userManager.CreateGroup("TestGroupAliases_A_" + SRandom.Next<long>().ToString(), testUser.Id, GroupType.Public);
            userManager.AddUserToGroup(testUser.Id, groupA.Id);
            IGroup groupB = userManager.CreateGroup("TestGroupAliases_B_" + SRandom.Next<long>().ToString(), testUser.Id, GroupType.Public);
            userManager.AddUserToGroup(testUser.Id, groupB.Id);
            IGroup groupC = userManager.CreateGroup("TestGroupAliases_C_" + SRandom.Next<long>().ToString(), testUser.Id, GroupType.Public);
            userManager.AddUserToGroup(testUser.Id, groupC.Id);

            userManager.SetGroupAlias(testUser.Id, groupB.Id, "TheAlias");

            foreach (IGroupAndAlias groupAndAlias in userManager.GetGroupsThatUserIsIn(testUser.Id))
                if (groupAndAlias.Id == groupB.Id)
                    Assert.AreEqual("TheAlias", groupAndAlias.Alias, "Alias set incorrectly");

            userManager.SetGroupAlias(testUser.Id, groupB.Id, "SecondAlias");

            foreach (IGroupAndAlias groupAndAlias in userManager.GetGroupsThatUserIsIn(testUser.Id))
                if (groupAndAlias.Id == groupB.Id)
                    Assert.AreEqual("SecondAlias", groupAndAlias.Alias, "Alias set incorrectly");

            userManager.SetGroupAlias(testUser.Id, groupB.Id, null);

            foreach (IGroupAndAlias groupAndAlias in userManager.GetGroupsThatUserIsIn(testUser.Id))
                if (groupAndAlias.Id == groupB.Id)
                    Assert.AreEqual(null, groupAndAlias.Alias, "Alias set incorrectly");
        }
    }
}
