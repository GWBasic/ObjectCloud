// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
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
        /// Parses the result as a string
        /// </summary>
        /// <returns></returns>
        public string AsString()
        {
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
            return StreamFunctions.ReadAllBytes(HttpWebResponse.GetResponseStream());
        }

        /// <summary>
        /// Returns a JsonReader to handle the result
        /// </summary>
        /// <returns></returns>
        public JsonReader AsJsonReader()
        {
            return new JsonReader(HttpWebResponse.GetResponseStream());
        }
    }
}
