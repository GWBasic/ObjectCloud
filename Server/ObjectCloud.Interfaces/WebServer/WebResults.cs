// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using JsonFx.Json;

namespace ObjectCloud.Interfaces.WebServer
{
    public abstract partial class WebResults : IWebResults
    {
        protected WebResults(Status status)
            : this(new Dictionary<string, string>(), status) {}

        protected WebResults(IDictionary<string, string> headers, Status status)
        {
            _Headers = headers;
            _Status = status;
        }

        public static IWebResults FromString(Status status, string text)
        {
            return new StringWebResults(status, text);
        }

        public static IWebResults FromStream(Status status, Stream stream)
        {
            return new StreamWebResults(status, stream);
        }

        public static IWebResults FromStatus(Status status)
        {
            return new StringWebResults(status, "");
        }

        public static IWebResults ToJson(Status status, object toWrite)
        {
            return new JSONWebResults(status, toWrite);
        }

        public static IWebResults ToJson(object toWrite)
        {
            return new JSONWebResults(Status._200_OK, toWrite);
        }

        /// <summary>
        /// Redirects the user to the given URL 
        /// </summary>
        /// <param name="uri">
        /// A <see cref="Uri"/>
        /// </param>
        /// <returns>
        /// A <see cref="WebResults"/>
        /// </returns>
        public static IWebResults Redirect(Uri uri)
        {
            return Redirect(uri.AbsoluteUri);
        }

        /// <summary>
        /// Redirects the user to the given URL 
        /// </summary>
        /// <param name="url">
        /// A <see cref="System.String"/>
        /// </param>
        /// <returns>
        /// A <see cref="WebResults"/>
        /// </returns>
        public static IWebResults Redirect(string url)
        {
            IWebResults toReturn = FromString(
                Status._303_See_Other,
                "<html><head><title>Redirect</title></head><body><a href=\"" + url + "\">click here</a></body></html>");

            toReturn.Headers["Location"] = url;

            return toReturn;
        }
        public IDictionary<string, string> Headers
        {
            get { return _Headers; }
        }
        private IDictionary<string, string> _Headers;

        /// <summary>
        /// The Content-Type value in the header
        /// </summary>
        public string ContentType
        {
            get { return Headers["Content-Type"]; }
            set { Headers["Content-Type"] = value; }
        }

        public Status Status
        {
            get { return _Status; }
            set { _Status = value; }
        }
        private Status _Status;

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

        public override string ToString()
        {
            return Headers.ToString() + ResultsAsString;
        }

        public abstract Stream ResultsAsStream { get; }

        public abstract string ResultsAsString { get; }

        private class DeleteMeWebResults : WebResults
        {
            internal DeleteMeWebResults(Status status, byte[] body)
                : this(new Dictionary<string, string>(), status, body) { }

            internal DeleteMeWebResults(IDictionary<string, string> headers, Status status, byte[] body)
                : base(headers, status)
            {
                _Body = body;
            }

            private byte[] _Body;

            public override Stream ResultsAsStream
            {
                get
                {
                    if (null == _ResultsAsStream)
                        _ResultsAsStream = new MemoryStream(_Body, false);

                    return _ResultsAsStream;
                }
            }
            private Stream _ResultsAsStream = null;

            public override string ResultsAsString
            {
                get
                {
                    if (null == _ResultsAsStream)
                    {
                        using (MemoryStream stream = new MemoryStream(_Body))
                        {
                            using (StreamReader streamReader = new StreamReader(stream))
                            {
                                _ResultsAsString = streamReader.ReadToEnd();
                                streamReader.Close();
                            }

                            stream.Close();
                        }
                    }

                    return _ResultsAsString;
                }
            }
            private string _ResultsAsString = null;
        }
    }
}
