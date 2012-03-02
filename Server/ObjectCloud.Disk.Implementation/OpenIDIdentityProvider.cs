// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;

using ExtremeSwank.OpenId;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Disk.Implementation
{
    public class OpenIDIdentityProvider : HasFileHandlerFactoryLocator, IIdentityProvider
    {
        public int IdentityProviderCode
        {
            get { return 1; }
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
                false,
                FileHandlerFactoryLocator,
                displayName,
                this);

            return toReturn;
        }


        public string FilterIdentityToLocalNameIfNeeded(string nameOrGroupOrIdentity)
        {
            // Convert OpenID identities for local users to their appropriate local user name

            string localIdentityPrefix = string.Format("http://{0}/Users/", FileHandlerFactoryLocator.HostnameAndPort);
            if (
                nameOrGroupOrIdentity.StartsWith(localIdentityPrefix)
                && nameOrGroupOrIdentity.EndsWith(".user"))
            {
                nameOrGroupOrIdentity = nameOrGroupOrIdentity.Substring(
                    localIdentityPrefix.Length,
                    nameOrGroupOrIdentity.Length - localIdentityPrefix.Length - 5);
            }

            // fix urls if it is an openID
            if (nameOrGroupOrIdentity.StartsWith("http://") || nameOrGroupOrIdentity.StartsWith("https://"))
            {
                Uri openIdUri = new Uri(nameOrGroupOrIdentity);
                nameOrGroupOrIdentity = openIdUri.AbsoluteUri;
            }

            return nameOrGroupOrIdentity;
        }

        /// <summary>
        /// Gets the user with the corresponding OpenID identity. If the user isn't present in the database, adds the user. Note that the identity must be fully-resolved
        /// </summary>
        /// <param name="identity"></param>
        /// <returns></returns>
        public IUser GetOrCreateUser(string identity)
        {
            Uri openIdUri = new Uri(identity);
            identity = openIdUri.AbsoluteUri;

            IUser user = FileHandlerFactoryLocator.UserManagerHandler.GetUserNoException(identity);

            if (null != user)
                return user;

            return FileHandlerFactoryLocator.UserManagerHandler.CreateUser(
                identity,
                identity,
                null,
                this);
        }


        public IUser GetOrCreateUserIfCorrectFormOfIdentity(string identity)
        {
            if (identity.StartsWith("http://") || identity.StartsWith("https://"))
            {
                NameValueCollection openIdClientArgs = new NameValueCollection();

                OpenIdClient openIdClient = new OpenIdClient(openIdClientArgs);
                openIdClient.Identity = identity;
                openIdClient.TrustRoot = null;

                openIdClient.ReturnUrl = new Uri(string.Format("http://{0}", FileHandlerFactoryLocator.HostnameAndPort));

                // The proper identity is encoded in the URL
                Uri requestUri = openIdClient.CreateRequest(false, false);

                if (openIdClient.ErrorState == ErrorCondition.NoErrors)
                    if (openIdClient.IsValidIdentity())
                    {
                        RequestParameters openIdRequestParameters = new RequestParameters(requestUri.Query.Substring(1));
                        identity = openIdRequestParameters["openid.identity"];

                        return GetOrCreateUser(identity);
                    }
            }

            return null;
        }

        public IEnumerable<IUserOrGroup> Search(string query, uint? max, IEnumerable<string> pluginArgs)
        {
            return new IUserOrGroup[0];
        }
    }
}
