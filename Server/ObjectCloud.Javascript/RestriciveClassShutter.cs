// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using org.mozilla.javascript;

namespace ObjectCloud.Javascript
{
    /// <summary>
    /// Implements security by restricting which classes can be used in JavaScript
    /// </summary>
    internal class RestriciveClassShutter : org.mozilla.javascript.ClassShutter
    {
        public bool visibleToScripts(string str)
        {
            return false;
        }

        /// <summary>
        /// Static instance
        /// </summary>
        internal static readonly RestriciveClassShutter Instance = new RestriciveClassShutter();
    }
}
