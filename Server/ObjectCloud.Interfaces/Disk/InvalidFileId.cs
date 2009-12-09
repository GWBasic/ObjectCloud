// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;

using ObjectCloud.Common;

namespace ObjectCloud.Interfaces.Disk
{
    /// <summary>
    /// Thrown when a filename is bad
    /// </summary>
    public class InvalidFileId : DiskException
    {
        public InvalidFileId(ID<IFileContainer, long> id)
            :
            base("\"" + id.ToString() + "\" is an invalid file id") { }
    }
}