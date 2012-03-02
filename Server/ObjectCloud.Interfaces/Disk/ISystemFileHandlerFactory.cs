// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Common;

namespace ObjectCloud.Interfaces.Disk
{
    /// <summary>
    /// Interface for factories that create system files
    /// </summary>
    public interface ISystemFileHandlerFactory : IFileHandlerFactory
    {
        /// <summary>
        /// Creates a system file
        /// </summary>
        /// <param name="path"></param>
        void CreateSystemFile(IFileId fileId);
    }
}
