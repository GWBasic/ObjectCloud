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
    }
}
