// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Common;

namespace ObjectCloud.Interfaces.Security
{
    /// <summary>
    /// Base interface of users and groups
    /// </summary>
    public interface IUserOrGroup
    {
        /// <value>
        /// Returns the user or group's identity. This can be a URL or a WebFinger
        /// </value>
        string Identity { get; }

        /// <summary>
        /// The user or group's URL
        /// </summary>
        string Url { get; }

        /// <summary>
        /// The user or group's URL of an avatar
        /// </summary>
        string AvatarUrl { get; }

        /// <summary>
        /// The user's ID
        /// </summary>
        ID<IUserOrGroup, Guid> Id { get;}

        /// <summary>
        /// The user or group's name
        /// </summary>
        string Name { get;}

        /// <summary>
        /// The user or group's display name
        /// </summary>
        string DisplayName { get; }
        
        /// <value>
        /// True if the user or group is hardcoded into the system.  This means that it can not be deleted; and implies other special handling rules. 
        /// </value>
        bool BuiltIn { get;}
    }
}
