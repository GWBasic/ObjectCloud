// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ObjectCloud.Interfaces.WebServer
{
    public partial class WebResults
    {
        /// <summary>
        /// Version of IWebResults for web results that come from a string and can easily be converted to a string
        /// </summary>
        class StringWebResults : WebResults
        {
            internal StringWebResults(Status status, string resultsAsString)
                : base(status)
            {
                _ResultsAsString = resultsAsString;
                ContentType = "text/plain";
            }

            internal StringWebResults(IDictionary<string, string> headers, Status status, string resultsAsString)
                : base(headers, status)
            {
                _ResultsAsString = resultsAsString;
                ContentType = "text/plain";
            }

            public override Stream ResultsAsStream
            {
                get
                {
                    if (null == _ResultsAsStream)
                        _ResultsAsStream = new MemoryStream(Encoding.UTF8.GetBytes(_ResultsAsString), false);

                    return _ResultsAsStream;
                }
            }
            private Stream _ResultsAsStream = null;

            public override string ResultsAsString
            {
                get { return _ResultsAsString; }
            }
            private readonly string _ResultsAsString;
        }
    }
}
