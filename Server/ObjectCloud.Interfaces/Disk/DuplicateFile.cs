// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.Interfaces.Disk
{
    /// <summary>
    /// Thrown when an attempt is made to create a file that already exists
    /// </summary>
    public class DuplicateFile : DiskException
    {
        public DuplicateFile(string fileName)
            : base("\"" + fileName + "\" already exists") { }
    }
}
