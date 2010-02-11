// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;

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
	}
}
