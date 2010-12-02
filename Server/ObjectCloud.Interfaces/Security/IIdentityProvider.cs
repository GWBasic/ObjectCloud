// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;

namespace ObjectCloud.Interfaces.Security
{
    /// <summary>
    /// Provides a way for external identification systems, like WebFinger, OpenID, iName, ect
    /// </summary>
    public interface IIdentityProvider
    {
        /// <summary>
        /// A code that uniquely identifies the identity provider. Codes are tied to the specific implementation and not the protocol; thus competing or forked WebFinger plugins would have different codes. 0 indicates an identity managed by ObjectCloud. Codes under 10000 are reserved for assignment by ObjectCloud. When experimenting with custom IdentityProvider plugins, use a code above 10000 until ObjectCloud assigns a code to you.
        /// </summary>
        int IdentityProviderCode { get; }

        /// <summary>
        /// Creates the user object
        /// </summary>
        /// <param name="builtIn">True if the user is a system-generated user, as opposed to a real person</param>
        /// <param name="identityProviderArgs">Additional arguments for the identity provider, as a string. The exact contents are created by the identity provider</param>
        /// <returns></returns>
        IUser CreateUserObject(
            FileHandlerFactoryLocator FileHandlerFactoryLocator,
            ID<IUserOrGroup, Guid> userId,
            string name,
            bool builtIn,
            string displayName,
            string identityProviderArgs);

        /// <summary>
        /// If the name is an identity that points to a local user, returns the local user. For example, if the host is objectcloud.com, when using WebFinger, changes gwbasic@objectcloud.com to gwbasic
        /// </summary>
        /// <param name="nameOrGroupOrIdentity"></param>
        /// <returns>Either nameOrGroupOrIdentity unmodified, or the local name if the identity points to a local user</returns>
        string FilterIdentityToLocalNameIfNeeded(string nameOrGroupOrIdentity);

        /// <summary>
        /// Gets the user with the OpenID identity. If the user isn't present in the database, adds the user. Note that the identity must be fully-resolved
        /// </summary>
        /// <param name="identity"></param>
        /// <returns></returns>
        IUser GetOrCreateUser(string identity);

        /// <summary>
        /// Gets or creates a user with the appropriate identity if the identity matches the form needed for this identity provider. Returns null if the identity is a form not applicable to this identity provider
        /// </summary>
        /// <param name="identity"></param>
        /// <returns></returns>
        IUser GetOrCreateUserIfCorrectFormOfIdentity(string identity);

        /// <summary>
        /// Searches for users that match the query if an appropriate plugin argument is found
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        IEnumerable<IUserOrGroup> Search(string query, uint? max, IEnumerable<string> pluginArgs);
    }
}
