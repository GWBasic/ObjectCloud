// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;

using ObjectCloud.Common;

namespace ObjectCloud.Interfaces.Security
{
	public struct FilePermission
	{
		/// <summary>
		/// The user or group ID that the permission applies to
		/// </summary>
		public ID<IUserOrGroup, Guid> UserOrGroupId;
		
		/// <summary>
		/// The assigned permission
		/// </summary>
		public FilePermissionEnum FilePermissionEnum;
		
		/// <summary>
		/// True if the permission can apply by default to sub-objects contained within this object, aka, files contained within a directory
		/// </summary>
		public bool Inherit;

        /// <summary>
        /// True if the target user or group recieves notifications when the file changes, false otherwise
        /// </summary>
        public bool SendNotifications;
		
		/// <summary>
		/// All of the user's named permissions.  If the name is present in the dictionary, the user has the named permission.  If the value associated with the name is true, then the permission is inheritable
		/// </summary>
		public Dictionary<string, bool> NamedPermissions;
	}
}
