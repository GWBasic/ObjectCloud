// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Disk.FileHandlers
{
    /// <summary>
    /// Provides ObjectCloud-specific FileHandler functionality
    /// </summary>
    public abstract class LastModifiedFileHandler : FileHandler
    {
        public LastModifiedFileHandler(FileHandlerFactoryLocator fileHandlerFactoryLocator, string filename)
            : base(fileHandlerFactoryLocator)
        {
            _Filename = filename;
        }

        /// <summary>
        /// The Filename of the file used on the filesystem.  This is primarily used to determine LastModified.  It can be left as null if LastModified is virtual
        /// </summary>
        public string Filename
        {
            get { return _Filename; }
            set { _Filename = value; }
        }
        private string _Filename;

        public virtual DateTime LastModified
        {
            get
            {
                if (null == Filename)
                {
#if DEBUG
                    if (System.Diagnostics.Debugger.IsAttached)
                        // You should override this property or set Filename
                        System.Diagnostics.Debugger.Break();
#endif

                    return DateTime.MinValue;
                }

                return File.GetLastWriteTimeUtc(Filename);
            }
        }
    }
}
