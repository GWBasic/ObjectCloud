// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using JsonFx.Json;

namespace ObjectCloud.Interfaces.WebServer
{
    public partial class WebResults
    {
        /// <summary>
        /// Version of IWebResults for web results that come from a string and can easily be converted to a string
        /// </summary>
        class JSONWebResults : WebResults
        {
            internal JSONWebResults(Status status, object forJSON)
                : base(status)
            {
                ForJSON = forJSON;
                ContentType = "application/json";
            }

            internal JSONWebResults(IDictionary<string, string> headers, Status status, object forJSON)
                : base(headers, status)
            {
                ForJSON = forJSON;
                ContentType = "application/json";
            }

            public override Stream ResultsAsStream
            {
                get
                {
                    if (null == _ResultsAsStream)
                        _ResultsAsStream = new MemoryStream(Encoding.UTF8.GetBytes(ResultsAsString), false);

                    return _ResultsAsStream;
                }
            }
            private Stream _ResultsAsStream = null;

            public override string ResultsAsString
            {
                get 
                {
                    // TODO:  This really should write to a stream, but I just can't seem to get it to work right
                    if (null == _ResultsAsString)
                        _ResultsAsString = JsonWriter.Serialize(ForJSON);

                    return _ResultsAsString; 
                }
            }
            private string _ResultsAsString;

            /// <summary>
            /// The object that is to be serialized to JSON
            /// </summary>
            private readonly object ForJSON;
        }
    }
}
