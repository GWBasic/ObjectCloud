// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Common;

namespace ObjectCloud.Interfaces.Security
{
    /// <summary>
    /// Provides a way for external identification systems, like WebFinger, OpenID, iName, ect
    /// </summary>
    public interface IIdentityProvider
    {
        /// <summary>
        /// A code that uniquely identifies the identity provider
        /// </summary>
        int IdentityProviderCode { get; }
    }
}
