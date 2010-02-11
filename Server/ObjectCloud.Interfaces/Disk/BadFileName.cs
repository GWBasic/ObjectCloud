// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;

namespace ObjectCloud.Interfaces.Disk
{
    /// <summary>
    /// Thrown when a filename is bad
    /// </summary>
    public class BadFileName : DiskException
    {
        public BadFileName(string fileName)
            :
            base("\"" + fileName + "\" is an invalid file name") { }
    }
}