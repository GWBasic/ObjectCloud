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
		[Test]
		public void TestAssociationHandleTrue()
		{
			ID<IUser, Guid> userId = new ID<IUser, Guid>(Guid.NewGuid());
			string associationHandle = FileHandlerFactoryLocator.UserManagerHandler.CreateAssociationHandle(userId);
			bool isHandleValid = FileHandlerFactoryLocator.UserManagerHandler.VerifyAssociationHandle(userId, associationHandle);
			
			Assert.IsTrue(isHandleValid, "Handle not valid");
		}

		[Test]
		public void TestAssociationHandleFalse()
		{
			ID<IUser, Guid> userId = new ID<IUser, Guid>(Guid.NewGuid());
			string associationHandle = FileHandlerFactoryLocator.UserManagerHandler.CreateAssociationHandle(userId);
			
			associationHandle = associationHandle + "dne";
			
			bool isHandleValid = FileHandlerFactoryLocator.UserManagerHandler.VerifyAssociationHandle(userId, associationHandle);
			
			Assert.IsFalse(isHandleValid, "Handle not rejected");
		}
	}
}
