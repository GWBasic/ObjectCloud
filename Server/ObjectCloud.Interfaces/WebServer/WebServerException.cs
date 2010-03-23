// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;

namespace ObjectCloud.Interfaces.WebServer
{
    /// <summary>
    /// Class for various exceptions that come from the WebServer
    /// </summary>
    public class WebServerException : Exception
    {
        public WebServerException(string message)
            : base(message) { }

        public WebServerException(string message, Exception inner)
            : base(message, inner) { }
    }
}