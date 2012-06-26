// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

using Common.Logging;

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
            HttpWebRequest webRequest = CreateGetWebRequest(url, arguments);

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

        class RequestState
        {
            public HttpWebRequest HttpWebRequest;
            public Action<HttpResponseHandler> Callback;
            public Action<Exception> ErrorCallback;
        }

        /// <summary>
        /// Performs a GET request to the given URL
        /// </summary>
        /// <param name="url"></param>
        /// <param name="arguments">The arugments, This can be a Dictionary indexed by name</param>
        /// <returns></returns>
        public void BeginGet(
            string url,
            Action<HttpResponseHandler> callback,
            Action<Exception> errorCallback)
        {
            BeginGet(url, null, callback, errorCallback);
        }

        /// <summary>
        /// Performs a GET request to the given URL
        /// </summary>
        /// <param name="url"></param>
        /// <param name="arguments">The arugments, This can be a Dictionary indexed by name</param>
        /// <returns></returns>
        public void BeginGet(
            string url, 
            ICollection<KeyValuePair<string, string>> arguments,
            Action<HttpResponseHandler> callback,
            Action<Exception> errorCallback)
        {
            HttpWebRequest webRequest = CreateGetWebRequest(url, arguments);

            RequestState state = new RequestState();
            state.HttpWebRequest = webRequest;
            state.Callback = callback;
            state.ErrorCallback = errorCallback;

            webRequest.BeginGetResponse(WebRequestCallback, state);
        }

        private HttpWebRequest CreateGetWebRequest(string url, ICollection<KeyValuePair<string, string>> arguments)
        {
            StringBuilder urlBuilder = new StringBuilder(url);

            if (null != arguments)
                if (arguments.Count > 0)
                    urlBuilder.AppendFormat("?{0}", BuildArguments(arguments));

            HttpWebRequest webRequest = (HttpWebRequest)HttpWebRequest.Create(urlBuilder.ToString());
            webRequest.CookieContainer = CookieContainer;
            webRequest.KeepAlive = true;
            webRequest.UnsafeAuthenticatedConnectionSharing = true;
            //webRequest.ServicePoint.BindIPEndPointDelegate += BindIPEndPointCallback;

            if (null != Timeout)
                webRequest.Timeout = Convert.ToInt32(Timeout.Value.TotalMilliseconds);

            return webRequest;
        }

        /*static int LastBindPortUsed = 5001;

        /// <summary>
        /// http://blogs.msdn.com/b/dgorti/archive/2005/09/18/470766.aspx
        /// </summary>
        /// <param name="servicePoint"></param>
        /// <param name="remoteEndPoint"></param>
        /// <param name="retryCount"></param>
        /// <returns></returns>
        public static IPEndPoint BindIPEndPointCallback(
            ServicePoint servicePoint,
            IPEndPoint remoteEndPoint,
            int retryCount)
        {
            int port = Interlocked.Increment(ref LastBindPortUsed); //increment
            Interlocked.CompareExchange(ref LastBindPortUsed, 5001, 65534);

            if (remoteEndPoint.AddressFamily == AddressFamily.InterNetwork)
                return new IPEndPoint(IPAddress.Any, port);
            else
                return new IPEndPoint(IPAddress.IPv6Any, port);
        }*/

        /// <summary>
        /// Callback for BeginGet
        /// </summary>
        /// <param name="ar"></param>
        private void WebRequestCallback(IAsyncResult ar)
        {
            RequestState state = (RequestState)ar.AsyncState;

            try
            {
                WebResponse webResponse = state.HttpWebRequest.EndGetResponse(ar);
                state.Callback(new HttpResponseHandler((HttpWebResponse)webResponse, state.HttpWebRequest));
            }
            catch (WebException webException)
            {
                if (null != webException.Response)
                    state.Callback(new HttpResponseHandler((HttpWebResponse)webException.Response, state.HttpWebRequest));
                else
                    state.ErrorCallback(webException);
            }
            catch (Exception e)
            {
                state.ErrorCallback(e);
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
            HttpWebRequest webRequest = CreatePostWebRequest(url, arguments);

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
        /// <param name="arguments">The arugments, This can be a Dictionary indexed by name</param>
        /// <returns></returns>
        public void BeginPost(
            string url,
            Action<HttpResponseHandler> callback,
            Action<Exception> errorCallback,
            params KeyValuePair<string, string>[] arguments)
        {
            HttpWebRequest webRequest = CreatePostWebRequest(url, arguments);

            RequestState state = new RequestState();
            state.HttpWebRequest = webRequest;
            state.Callback = callback;
            state.ErrorCallback = errorCallback;

            webRequest.BeginGetResponse(WebRequestCallback, state);
        }

        private HttpWebRequest CreatePostWebRequest(string url, IEnumerable<KeyValuePair<string, string>> arguments)
        {
            HttpWebRequest webRequest = (HttpWebRequest)HttpWebRequest.Create(url);
            webRequest.Method = "POST";
            webRequest.ContentType = "application/x-www-form-urlencoded";
            webRequest.CookieContainer = CookieContainer;
            webRequest.KeepAlive = true;
            webRequest.UnsafeAuthenticatedConnectionSharing = true;
            //webRequest.ServicePoint.BindIPEndPointDelegate += BindIPEndPointCallback;

            if (null != Timeout)
                webRequest.Timeout = Convert.ToInt32(Timeout.Value.TotalMilliseconds);

            string postArgumentsString = BuildArguments(arguments);

            byte[] toWrite = Encoding.UTF8.GetBytes(postArgumentsString);

            webRequest.ContentLength = toWrite.Length;

            // Write the request
            webRequest.GetRequestStream().Write(toWrite, 0, toWrite.Length);
            return webRequest;
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

            foreach (KeyValuePair<string, string> argument in arguments)
                if (null != argument.Key && null != argument.Value)
                    encodedArguments.Add(string.Format(
                        "{0}={1}",
                        StringGenerator.UriEscapeDataString(argument.Key),
                        StringGenerator.UriEscapeDataString(argument.Value)));

            return StringGenerator.GenerateSeperatedList(encodedArguments, "&");
        }
    }
}