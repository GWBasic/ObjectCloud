// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

/*
Copyright (c) 2007, Andrew Rondeau

All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions
are met:

Redistributions of source code must retain the above copyright notice,
this list of conditions and the following disclaimer.

Redistributions in binary form must reproduce the above copyright
notice, this list of conditions and the following disclaimer in the
documentation and/or other materials provided with the distribution.

Neither the name of the Andrew Rondeau nor the names of its contributors
may be used to endorse or promote products derived from this software
without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
"AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Interfaces.WebServer
{
    /// <summary>
    /// When an exception of this type is thrown, it overrides the web results
    /// that normally would be returned
    /// </summary>
    public class WebResultsOverrideException : Exception, IHasWebResults
    {
        public WebResultsOverrideException(IWebResults webResults, string message)
            : base(message)
        {
            _WebResults = webResults;
        }

		public WebResultsOverrideException(IWebResults webResults)
            : base(webResults.ResultsAsString)
        {
            _WebResults = webResults;
        }

		public WebResultsOverrideException(IWebResults webResults, Exception inner)
            : base(webResults.ResultsAsString, inner)
        {
            _WebResults = webResults;
        }

        public WebResultsOverrideException(IWebResults webResults, string message, Exception inner)
            : base(message, inner)
        {
            _WebResults = webResults;
        }

        /// <summary>
        /// The web results that are to override the current code path
        /// </summary>
        public IWebResults WebResults
        {
            get { return _WebResults; }
        }
        private IWebResults _WebResults;
    }
}
