// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.Javascript.SubProcess
{
    /// <summary>
    /// Class for exceptions that originate from the Javascript execution engine
    /// </summary>
    public class JavascriptException : Exception
    {
        internal JavascriptException(string message) : base(message) { }
        internal JavascriptException(string message, Exception inner) : base(message, inner) { }
    }
}
