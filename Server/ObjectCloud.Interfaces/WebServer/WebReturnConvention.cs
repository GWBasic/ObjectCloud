// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.Interfaces.WebServer
{
    /// <summary>
    /// Declares the different ways of returning data to the client
    /// </summary>
    public enum WebReturnConvention
    {
        /// <summary>
        /// A javascript primitive, such as string, boolean, or a number
        /// </summary>
        Primitive,

        /// <summary>
        /// A JSON-encoded data structure.  Can contain unsafe data.  Parsed using slow-but-safe parsers.  If the data can be garanteed to be safe, then  JavaScriptObject will be faster.
        /// </summary>
        JSON,

        /// <summary>
        /// A JavaScript object that can have executable code.  Similar to JSON, except that functions can be declared.  Parses faster then JSON on older browsers.  Can be used in place of JSON if the data is garanteed not be malicious.
        /// </summary>
        JavaScriptObject,

        /// <summary>
        /// The method doesn't return any data; everything returned is for informational purposes only
        /// </summary>
        Status,

        /// <summary>
        /// No automatic parsing of the results can occur; the caller must get a raw transport object and handle it
        /// </summary>
        Naked
    }
}
