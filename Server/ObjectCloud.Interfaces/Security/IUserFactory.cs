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
    public interface IUserFactory
    {
        /// <summary>
        /// The user handler for when the user is anonymous
        /// </summary>
        IUserHandler AnonymousUserHandler { get;}
		
		/// <value>
		/// The user object for the root user 
		/// </value>
		IUser RootUser { get; }

        /// <summary>
        /// The user object for when the user is anonymous
        /// </summary>
        IUser AnonymousUser { get; }
		
		/// <value>
		/// Every user is always a member of this group, including the anonyous user.  Giving a permission to everybody means that *everybody* has permission!
		/// </value>
		IGroup Everybody { get; }
		
		/// <value>
		/// All authenticated users, even those who log in using OpenID, are members of this group.  This is almost as liberal as Everybody, except that it requires the user to get an openID from someone somewhere
		/// </value>
		IGroup AuthenticatedUsers { get; }
		
		/// <value>
		/// All users who have an account that originates from this server 
		/// </value>
		IGroup LocalUsers { get; }
		
		/// <value>
		/// All users in this group have administrative privilages on this server 
		/// </value>
		IGroup Administrators { get; }
		
		/// <summary>
		/// Returns true if the user or group with the given ID is a system user or group, and thus the ID is the same in all ObjectCloud instances 
		/// </summary>
		/// <param name="userOrGroupId">
		/// A <see cref="ID"/>
		/// </param>
		/// <returns>
		/// A <see cref="System.Boolean"/>
		/// </returns>
		bool IsSystemUserOrGroup(ID<IUserOrGroup, Guid> userOrGroupId);
    }
}
