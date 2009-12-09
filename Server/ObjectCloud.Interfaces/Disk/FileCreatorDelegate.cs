// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;

namespace ObjectCloud.Interfaces.Disk
{
    /// <summary>
    /// Delegate for creating a file with a given ID
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public delegate IFileHandler FileCreatorDelegate(ID<IFileContainer, long> id);
}

