// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.Interfaces.Disk
{
    /// <summary>
    /// Thrown when an attempt is made to add a duplicate user
    /// </summary>
    public class UserAlreadyExistsException : DiskException
    {
        public UserAlreadyExistsException(string message)
            : base(message) { }

        public UserAlreadyExistsException(string message, Exception inner)
            : base(message, inner) { }
    }
}
