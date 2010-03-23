// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.Interfaces.WebServer
{
    /// <summary>
    /// Interface for objects returned from a web method
    /// </summary>
    public interface IWebResults
    {
        /// <summary>
        /// The headers that are returned to the browser
        /// </summary>
        IDictionary<string, string> Headers { get; }

        /// <summary>
        /// The status to return to the web browser
        /// </summary>
        Status Status { get; set; }

        /// <summary>
        /// The Content-Type value in the header
        /// </summary>
        string ContentType { get; set; }

        /// <summary>
        /// The stream that contains the contents of the web result
        /// </summary>
        Stream ResultsAsStream { get; }

        /// <summary>
        /// The results as a string
        /// </summary>
        string ResultsAsString { get; }
    }
}
