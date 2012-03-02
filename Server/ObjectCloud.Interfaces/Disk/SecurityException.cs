// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;

namespace ObjectCloud.Interfaces.Disk
{
    /// <summary>
    /// Base of all security errors
    /// </summary>
    public class SecurityException : Exception
    {
        public SecurityException(string message)
            : base(message) { }

        public SecurityException(string message, Exception inner)
            : base(message, inner) { }
    }
}