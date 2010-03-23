// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.Interfaces.Disk
{
    /// <summary>
    /// Creates a root directory
    /// </summary>
    public interface IRootDirectoryCreator
    {
        /// <summary>
        /// Creates the root directory and the rest of the file system if it does not exist
        /// </summary>
        /// <param name="rootDirectoryDiskPath">The filesystem path to create the root directory in</param>
        /// <returns></returns>
        void CreateRootDirectoryHandler(IFileContainer rootDirectoryContainer);

        /// <summary>
        /// Syncronizes the root directory with the template as a means of obtaining updates
        /// </summary>
        /// <param name="rootDirectoryHandler"></param>
        void Syncronize(IDirectoryHandler rootDirectoryHandler);
    }
}
