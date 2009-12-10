using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.Interfaces.Security
{
    public enum GroupType : int
    {
        /// <summary>
        /// The group is personal and is hidden from other people.  It's for a user's convenience
        /// </summary>
        Personal = 0,

        /// <summary>
        /// The group has a page that (by default) is only visible to members of the group.  Only the owner can add/remove people
        /// </summary>
        Private = 1,

        /// <summary>
        /// The groups is public and anyone can join
        /// </summary>
        Public = 2
    }
}
