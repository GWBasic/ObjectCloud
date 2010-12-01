// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Disk.Implementation
{
    public class LocalIdentityProvider : HasFileHandlerFactoryLocator, IIdentityProvider
    {
        public int IdentityProviderCode
        {
            get { return 0; }
        }

        public IUser CreateUserObject(
            FileHandlerFactoryLocator FileHandlerFactoryLocator,
            ID<IUserOrGroup, Guid> userId,
            string name,
            bool builtIn,
            string displayName,
            string identityProviderArgs)
        {
            IUser toReturn = new User(
                userId,
                name,
                builtIn,
                true,
                FileHandlerFactoryLocator,
                displayName,
                this);

            return toReturn;
        }


        public string FilterIdentityToLocalNameIfNeeded(string nameOrGroupOrIdentity)
        {
            // Allow /Users/[username].user
            if (nameOrGroupOrIdentity.StartsWith("/Users/") && nameOrGroupOrIdentity.EndsWith(".user"))
            {
                nameOrGroupOrIdentity = nameOrGroupOrIdentity.Substring(7);
                nameOrGroupOrIdentity = nameOrGroupOrIdentity.Substring(0, nameOrGroupOrIdentity.Length - 5);
            }

            return nameOrGroupOrIdentity;
        }


        public IUser GetOrCreateUserIfCorrectFormOfIdentity(string identity)
        {
            return null;
        }


        public IUser GetOrCreateUser(string identity)
        {
            return null;
        }
    }
}
