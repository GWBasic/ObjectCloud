// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;

using ObjectCloud.Common;

namespace ObjectCloud.Interfaces.Security
{
	/// <summary>
	/// Represents a group
	/// </summary>
	public interface IGroup : IUserOrGroup
	{
		//// <value>
		/// The group's owner
		/// </value>
		ID<IUserOrGroup, Guid>? OwnerId { get; }
		
		/// <value>
		/// True if membership in the group is determined automatically by ObjectCloud.  This means that no one can be added or removed to the group; membership is determined at runtime 
		/// </value>
		bool Automatic { get; }

        /// <summary>
        /// The group type
        /// </summary>
        GroupType Type { get; }
	}
}
