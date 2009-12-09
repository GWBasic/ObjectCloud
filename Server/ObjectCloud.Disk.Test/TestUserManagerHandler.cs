// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Threading;

using Spring.Context;
using Spring.Context.Support;

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
    }
}
