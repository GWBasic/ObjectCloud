// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.Interfaces.Disk
{
    /// <summary>
    /// Utility methods for handling IFileContainers
    /// </summary>
    public static class FileContainerUtils
    {
        public static string GetFullPath(IFileContainer fileContainer)
        {
            /*if (null == fileContainer.ParentDirectoryHandler)
                return "";
            else
                return string.Format("{0}/{1}", GetFullPath(fileContainer.ParentDirectoryHandler), fileContainer.Filename);*/

            throw new NotImplementedException();
        }
    }
}
