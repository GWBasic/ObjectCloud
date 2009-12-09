// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;

namespace ObjectCloud.Interfaces.Disk
{
    /// <summary>
    /// Thrown when a file doesn't exist
    /// </summary>
    public class FileDoesNotExist : DiskException
    {
        public FileDoesNotExist(string fileName)
            : base("\"" + fileName + "\" does not exist") { }
    }
}
