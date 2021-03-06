// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Interfaces.Security
{
    /// <summary>
    /// Provides internal user-management functionality
    /// </summary>
    public interface IUser : IUserOrGroup
    {
		/// <value>
		/// The user handler
		/// </value>
		IUserHandler UserHandler { get; }

        /// <summary>
        /// True if the user is local, false if the user is logged in through OpenID
        /// </summary>
        bool Local { get; }

        /// <summary>
        /// The user's identity provider
        /// </summary>
        IIdentityProvider IdentityProvider { get; }
    }
}
