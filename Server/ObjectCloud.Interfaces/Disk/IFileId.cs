// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.Interfaces.Disk
{
    /// <summary>
    /// Opaque ID for a file.  Specific implemenations must use their own FileId implementations with types that match their data schemas
    /// </summary>
    public interface IFileId { }
}
