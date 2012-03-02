// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
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
        class StreamWebResults : WebResults
        {
            internal StreamWebResults(Status status, Stream resultsAsStream)
                : base(status)
            {
                _ResultsAsStream = resultsAsStream;
                ContentType = "application/octet-stream";
            }

            internal StreamWebResults(IDictionary<string, string> headers, Status status, Stream resultsAsStream)
                : base(headers, status)
            {
                _ResultsAsStream = resultsAsStream;
                ContentType = "application/octet-stream";
            }

            public override Stream ResultsAsStream
            {
                get { return _ResultsAsStream; }
            }
            private readonly Stream _ResultsAsStream;

            public override string ResultsAsString
            {
                get 
                {
                    if (null == _ResultsAsString)
                    {
                        ResultsAsStream.Seek(0, SeekOrigin.Begin);

                        StreamReader streamReader = new StreamReader(ResultsAsStream);
                        _ResultsAsString = streamReader.ReadToEnd();
                    }

                    return _ResultsAsString; 
                }
            }
            private string _ResultsAsString = null;
        }
    }
}
