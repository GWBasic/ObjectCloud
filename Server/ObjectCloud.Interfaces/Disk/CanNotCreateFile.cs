// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;

namespace ObjectCloud.Interfaces.Disk
{
    /// <summary>
    /// Thrown when ObjectCloud can not create a file
    /// </summary>
    public class CanNotCreateFile : DiskException
    {
        public CanNotCreateFile(string message)
            : base(message) { }

        public CanNotCreateFile(string message, Exception inner)
            : base(message, inner) { }
    }
}