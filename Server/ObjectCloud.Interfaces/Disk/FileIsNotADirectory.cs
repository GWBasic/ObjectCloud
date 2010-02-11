// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;

namespace ObjectCloud.Interfaces.Disk
{
    /// <summary>
    /// Thrown when a file is accessed as a director
    /// </summary>
    public class FileIsNotADirectory : DiskException
    {
        public FileIsNotADirectory(string fileName)
            : base("\"" + fileName + "\" is not a directory") { }
    }
}