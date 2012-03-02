// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

using JsonFx.Json;

namespace ObjectCloud.Common
{
    /// <summary>
    /// Wraps and simplifies handling Http responses
    /// </summary>
    public class HttpResponseHandler
    {
        public HttpResponseHandler(HttpWebResponse httpWebResponse, HttpWebRequest httpWebRequest)
        {
            _HttpWebRequest = httpWebRequest;
            _HttpWebResponse = httpWebResponse;
        }

        /// <summary>
        /// The wrapped HttpWebResponse
        /// </summary>
        public HttpWebResponse HttpWebResponse
        {
            get { return _HttpWebResponse; }
        }
        private readonly HttpWebResponse _HttpWebResponse;

        /// <summary>
        /// The originating HttpWebRequest
        /// </summary>
        public HttpWebRequest HttpWebRequest
        {
            get { return _HttpWebRequest; }
        }
        private readonly HttpWebRequest _HttpWebRequest;

        /// <summary>
        /// The Http Status Code
        /// </summary>
        public HttpStatusCode StatusCode
        {
            get { return HttpWebResponse.StatusCode; }
        }

        /// <summary>
        /// The content-type
        /// </summary>
        public string ContentType
        {
            get { return HttpWebResponse.ContentType; }
        }

        /// <summary>
        /// Flag to prevent double-reading the stream
        /// </summary>
        bool returned = false;

        private void PreventDoubleRead()
        {
            if (returned)
                throw new InvalidOperationException("Can not read from the stream twice!");

            returned = true;
        }

        /// <summary>
        /// Parses the result as a string
        /// </summary>
        /// <returns></returns>
        public string AsString()
        {
            PreventDoubleRead();

            StreamReader reader = new StreamReader(HttpWebResponse.GetResponseStream());
            string response = reader.ReadToEnd().Trim();

            return response;
        }

        /// <summary>
        /// Parses the result as an array of bytes
        /// </summary>
        /// <returns></returns>
        public byte[] AsBytes()
        {
            PreventDoubleRead();

            return StreamFunctions.ReadAllBytes(HttpWebResponse.GetResponseStream());
        }

        /// <summary>
        /// Returns a JsonReader to handle the result
        /// </summary>
        /// <returns></returns>
        public JsonReader AsJsonReader()
        {
            PreventDoubleRead();

            return new JsonReader(HttpWebResponse.GetResponseStream());
        }
    }
}
