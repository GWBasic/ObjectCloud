// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.Interfaces.Disk
{
    public class WrongFileType : DiskException
    {
        public WrongFileType(string message)
            : base(message) { }

        public WrongFileType(string message, Exception inner)
            : base(message, inner) { }
    }
}
