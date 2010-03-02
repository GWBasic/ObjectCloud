// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Common;

namespace ObjectCloud.Interfaces.Disk
{
    public interface IFileSystem
    {
        /// <summary>
        /// Returns true if there is a file with the given ID, false otherwise
        /// </summary>
        /// <param name="fileId"></param>
        /// <returns></returns>
        bool IsFilePresent(IFileId fileId);

        /// <summary>
        /// Deletes the file with the given ID
        /// </summary>
        /// <param name="fileId"></param>
        void DeleteFile(IFileId fileId);
    }
}
