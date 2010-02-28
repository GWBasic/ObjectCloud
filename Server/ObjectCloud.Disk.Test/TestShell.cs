// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;

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
    public class TestShell : TestBase
	{
        protected override void  DoAdditionalSetup()
        {
         	 base.DoAdditionalSetup();

             TestUser = FileHandlerFactoryLocator.UserManagerHandler.CreateUser(
                "testUser" + SRandom.Next<ulong>().ToString(),
                "gerwfaewfaew");
        }

        protected override void DoAdditionalTearDown()
        {
            base.DoAdditionalTearDown();

            FileHandlerFactoryLocator.UserManagerHandler.DeleteUser(TestUser.Name);
        }

        IUser TestUser;

		[Test]
		public void TestAssociationHandleTrue()
		{
			string associationHandle = FileHandlerFactoryLocator.UserManagerHandler.CreateAssociationHandle(TestUser.Id);
            bool isHandleValid = FileHandlerFactoryLocator.UserManagerHandler.VerifyAssociationHandle(TestUser.Id, associationHandle);
			
			Assert.IsTrue(isHandleValid, "Handle not valid");
		}

		[Test]
		public void TestAssociationHandleFalse()
		{
            string associationHandle = FileHandlerFactoryLocator.UserManagerHandler.CreateAssociationHandle(TestUser.Id);
			
			associationHandle = associationHandle + "dne";

            bool isHandleValid = FileHandlerFactoryLocator.UserManagerHandler.VerifyAssociationHandle(TestUser.Id, associationHandle);
			
			Assert.IsFalse(isHandleValid, "Handle not rejected");
		}
	}
}
