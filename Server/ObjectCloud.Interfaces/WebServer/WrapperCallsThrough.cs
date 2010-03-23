// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.Interfaces.WebServer
{
    /// <summary>
    /// Different ways that the wrapper calls into another object
    /// </summary>
    [Flags]
    public enum WrapperCallsThrough
    {
        /// <summary>
        /// The wrapper calls into the object though AJAX; thus calls are asyncronous
        /// </summary>
        AJAX = 1,

        /// <summary>
        /// The wrapper calls into the object through server-side shells; thus the calls are syncronous
        /// </summary>
        ServerSideShells = 2,

        /// <summary>
        /// The wrapper calls into the object through server-side shells that bypass server-side Javscript
        /// </summary>
        BypassServerSideJavascript = 4,
    }
}
