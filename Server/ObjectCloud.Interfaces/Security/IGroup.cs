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

    /// <summary>
    /// Represents a group and a possible alias that a user assigned to that group
    /// </summary>
    public interface IGroupAndAlias : IGroup
    {
        /// <summary>
        /// The alias that the current user assigned to the group, or null if the user doesn't use an alias
        /// </summary>
        string Alias { get; }
    }
}
