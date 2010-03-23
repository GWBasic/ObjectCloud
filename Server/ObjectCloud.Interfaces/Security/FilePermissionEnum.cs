// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.Interfaces.Security
{
    /// <summary>
    /// The permissions that a user can have on a file
    /// </summary>
    public enum FilePermissionEnum : int
    {
        /// <summary>
        /// User can perform a read operation
        /// </summary>
        Read = 0,

        /// <summary>
        /// User can perform a write operation
        /// </summary>
        Write = 1,

        /// <summary>
        /// User can perform administration operations.  This is more powerful then write
        /// </summary>
        Administer = 3
    }
}