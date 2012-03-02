// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.Interfaces.WebServer
{
    /// <summary>
    /// Exception that implements IHasWebResults
    /// </summary>
    public class HasWebResultsException : Exception, IHasWebResults
    {
        public HasWebResultsException() : base() { }

        public HasWebResultsException(string message, IWebResults webResults)
            : base(message)
        {
            _WebResults = webResults;
        }

        public HasWebResultsException(string message, Exception inner, IWebResults webResults)
            : base(message, inner)
        {
            _WebResults = webResults;
        }

        /// <summary>
        /// The Web Results
        /// </summary>
        public IWebResults WebResults
        {
            get { return _WebResults; }
            set { _WebResults = value; }
        }
        private IWebResults _WebResults;
    }
}
