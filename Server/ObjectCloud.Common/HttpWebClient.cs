// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace ObjectCloud.Common
{
    /// <summary>
    /// Simplifies using HttpWebRequest and HttpWebResponse.  Encapsualtes cookie handling and some arguments
    /// </summary>
    public class HttpWebClient
    {
        /// <summary>
        /// The cookies for the web connection
        /// </summary>
        public CookieContainer CookieContainer
        {
            get { return _CookieContainer; }
            set { _CookieContainer = value; }
        }
        private CookieContainer _CookieContainer = new CookieContainer();

        /// <summary>
        /// The default timeout
        /// </summary>
        public static TimeSpan? DefaultTimeout
        {
            get { return HttpWebClient._DefaultTimeout; }
            set { HttpWebClient._DefaultTimeout = value; }
        }
        private static TimeSpan? _DefaultTimeout = null;

        /// <summary>
        /// The timeout
        /// </summary>
        public TimeSpan? Timeout
        {
            get
            {
                if (null != _Timeout)
                    return _Timeout;
                else
                    return DefaultTimeout;
            }
            set { _Timeout = value; }
        }
        private TimeSpan? _Timeout = null;

        /// <summary>
        /// Performs a GET request to the given URL
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public HttpResponseHandler Get(string url)
        {
            return Get(url, null);
        }

        /// <summary>
        /// Performs a GET request to the given URL
        /// </summary>
        /// <param name="url"></param>
        /// <param name="arguments">The arugments, This can be a Dictionary indexed by name</param>
        /// <returns></returns>
        public HttpResponseHandler Get(string url, ICollection<KeyValuePair<string, string>> arguments)
        {
            StringBuilder urlBuilder = new StringBuilder(url);

            if (null != arguments)
                if (arguments.Count > 0)
                    urlBuilder.AppendFormat("?{0}", BuildArguments(arguments));

            HttpWebRequest webRequest = (HttpWebRequest)HttpWebRequest.Create(urlBuilder.ToString());
            webRequest.CookieContainer = CookieContainer;
			webRequest.KeepAlive = false;
			webRequest.UnsafeAuthenticatedConnectionSharing = true;

            if (null != Timeout)
                webRequest.Timeout = Convert.ToInt32(Timeout.Value.TotalMilliseconds);

            try
            {
                return new HttpResponseHandler((HttpWebResponse)webRequest.GetResponse(), webRequest);
            }
            catch (WebException webException)
            {
                if (null != webException.Response)
                    return new HttpResponseHandler((HttpWebResponse)webException.Response, webRequest);

                throw;
            }
        }

        /// <summary>
        /// Performs a GET request to the given URL
        /// </summary>
        /// <param name="url"></param>
        /// <param name="getArguments">The get arugments</param>
        /// <returns></returns>
        public HttpResponseHandler Get(string url, params KeyValuePair<string, string>[] getArguments)
        {
            if (null == getArguments)
                getArguments = new KeyValuePair<string, string>[0];

            return Get(url, new List<KeyValuePair<string, string>>(getArguments));
        }

        /// <summary>
        /// Performs a POST request to the given URL
        /// </summary>
        /// <param name="url"></param>
        /// <param name="arguments">The arugments, This can be a Dictionary indexed by name</param>
        /// <returns></returns>
        public HttpResponseHandler Post(string url, ICollection<KeyValuePair<string, string>> arguments)
        {
            HttpWebRequest webRequest = (HttpWebRequest)HttpWebRequest.Create(url);
            webRequest.Method = "POST";
            webRequest.ContentType = "application/x-www-form-urlencoded";
            webRequest.CookieContainer = CookieContainer;
            webRequest.KeepAlive = false;
            webRequest.UnsafeAuthenticatedConnectionSharing = true;

            if (null != Timeout)
                webRequest.Timeout = Convert.ToInt32(Timeout.Value.TotalMilliseconds);

            string postArgumentsString = BuildArguments(arguments);

            byte[] toWrite = Encoding.UTF8.GetBytes(postArgumentsString);

            webRequest.ContentLength = toWrite.Length;

            // Write the request
            webRequest.GetRequestStream().Write(toWrite, 0, toWrite.Length);

            try
            {
                return new HttpResponseHandler((HttpWebResponse)webRequest.GetResponse(), webRequest);
            }
            catch (WebException webException)
            {
                if (null != webException.Response)
                    return new HttpResponseHandler((HttpWebResponse)webException.Response, webRequest);

                throw;
            }
        }

        /// <summary>
        /// Performs a POST request to the given URL
        /// </summary>
        /// <param name="url"></param>
        /// <param name="getArguments">The get arugments</param>
        /// <returns></returns>
        public HttpResponseHandler Post(string url, params KeyValuePair<string, string>[] postArguments)
        {
            if (null == postArguments)
                postArguments = new KeyValuePair<string, string>[0];

            return Post(url, new List<KeyValuePair<string, string>>(postArguments));
        }

        public string BuildArguments(IEnumerable<KeyValuePair<string, string>> arguments)
        {
            List<string> encodedArguments = new List<string>();

            foreach(KeyValuePair<string, string> argument in arguments)
                if (null != argument.Key && null != argument.Value)
                    encodedArguments.Add(string.Format(
                        "{0}={1}",
                        Uri.EscapeDataString(argument.Key),
                        Uri.EscapeDataString(argument.Value)));

            return StringGenerator.GenerateSeperatedList(encodedArguments, "&");
        }
    }
}