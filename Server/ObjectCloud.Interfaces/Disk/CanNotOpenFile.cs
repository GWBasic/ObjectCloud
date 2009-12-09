// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;

namespace ObjectCloud.Interfaces.Disk
{
    /// <summary>
    /// Thrown when ObjectCloud can not open a file
    /// </summary>
    public class CanNotOpenFile : DiskException
    {
        public CanNotOpenFile(string message)
            : base(message) { }

        public CanNotOpenFile(string message, Exception inner)
            : base(message, inner) { }
    }
}