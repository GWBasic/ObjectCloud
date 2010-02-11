// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Common;

namespace ObjectCloud.Interfaces.Disk
{
    /// <summary>
    /// Interface for binary objects on disk, like images
    /// </summary>
    public interface IBinaryHandler : IFileHandler
    {
        /// <summary>
        /// Returns all of the contents of the file
        /// </summary>
        /// <returns></returns>
        byte[] ReadAll();

        /// <summary>
        /// Writes all of the contents into the file
        /// </summary>
        /// <param name="contents"></param>
        void WriteAll(byte[] contents);

        /// <summary>
        /// Occurs whenever the data changes
        /// </summary>
        event EventHandler<IBinaryHandler, EventArgs> ContentsChanged;
    }
}
