// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.WebHandlers
{
    /// <summary>
    /// WebHandler that provides a HTTP proxy
    /// </summary>
    public class HTTPWebProxy : WebHandler<IFileHandler>
    {
        /// <summary>
        /// Proxies GET requests
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="targetUrl"></param>
        /// <returns></returns>
        [WebCallable(
            WebCallingConvention.GET_application_x_www_form_urlencoded,
            WebReturnConvention.JavaScriptObject,  // This could return big results, which are slow to parse.  We know these results are safe, so we'll use eval()
            ObjectCloud.Interfaces.Security.FilePermissionEnum.Write,
            ObjectCloud.Interfaces.Security.FilePermissionEnum.Read)]
        public IWebResults GET(IWebConnection webConnection, string targetUrl)
        {
            HttpWebClient httpWebClient = new HttpWebClient();

            // Copy the get arguments, but remove Method and targetUrl
            Dictionary<string, string> getArguments = new Dictionary<string,string>(webConnection.GetParameters);
            getArguments.Remove("Method");
            getArguments.Remove("targetUrl");

            HttpResponseHandler webResponse = httpWebClient.Get(
                targetUrl, getArguments);

            Dictionary<string, object> toReturn = new Dictionary<string, object>();
            toReturn["Status"] = (int)webResponse.StatusCode;
            toReturn["Content"] = webResponse.AsString();
            toReturn["Headers"] = webResponse.HttpWebResponse.Headers;

            return WebResults.ToJson(toReturn);
        }

        /// <summary>
        /// Proxies POST requests that use the urlencoded convention
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="targetUrl"></param>
        /// <returns></returns>
        [WebCallable(
            WebCallingConvention.POST_application_x_www_form_urlencoded,
            WebReturnConvention.JavaScriptObject,  // This could return big results, which are slow to parse.  We know these results are safe, so we'll use eval()
            ObjectCloud.Interfaces.Security.FilePermissionEnum.Write,
            ObjectCloud.Interfaces.Security.FilePermissionEnum.Read)]
        public IWebResults POST_urlencoded(IWebConnection webConnection, string targetUrl)
        {
            HttpWebClient httpWebClient = new HttpWebClient();

            if (null == webConnection.PostParameters)
                throw new WebResultsOverrideException(
                    WebResults.FromString(Status._406_Not_Acceptable, "This method requires the urlencoded convention"));

            // Copy the get arguments, but remove Method and targetUrl
            Dictionary<string, string> postArguments = new Dictionary<string, string>(webConnection.PostParameters);
            postArguments.Remove("targetUrl");

            HttpResponseHandler webResponse = httpWebClient.Post(
                targetUrl, postArguments);

            Dictionary<string, object> toReturn = new Dictionary<string, object>();
            toReturn["Status"] = (int)webResponse.StatusCode;
            toReturn["Content"] = webResponse.AsString();
            toReturn["Headers"] = webResponse.HttpWebResponse.Headers;

            return WebResults.ToJson(toReturn);
        }

        /// <summary>
        /// Proxies all kinds of POST requests
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        [WebCallable(
            WebCallingConvention.Naked,
            WebReturnConvention.JavaScriptObject,  // This could return big results, which are slow to parse.  We know these results are safe, so we'll use eval()
            ObjectCloud.Interfaces.Security.FilePermissionEnum.Write,
            ObjectCloud.Interfaces.Security.FilePermissionEnum.Read)]
        public IWebResults POST(IWebConnection webConnection)
        {
            string targetUrl = webConnection.GetArgumentOrException("targetUrl");

            HttpWebRequest webRequest = (HttpWebRequest)HttpWebRequest.Create(targetUrl);
            webRequest.Method = "POST";
            webRequest.ContentType = webConnection.ContentType;

            byte[] toWrite = webConnection.Content.AsBytes();

            webRequest.ContentLength = toWrite.Length;

            // Write the request
            webRequest.GetRequestStream().Write(toWrite, 0, toWrite.Length);

            // Get the response
            HttpResponseHandler webResponse;
            try
            {
                webResponse = new HttpResponseHandler((HttpWebResponse)webRequest.GetResponse(), webRequest);
            }
            catch (WebException webException)
            {
                webResponse = new HttpResponseHandler((HttpWebResponse)webException.Response, webRequest);
            }

            Dictionary<string, object> toReturn = new Dictionary<string, object>();
            toReturn["Status"] = (int)webResponse.StatusCode;
            toReturn["Content"] = webResponse.AsString();
            toReturn["Headers"] = webResponse.HttpWebResponse.Headers;

            return WebResults.ToJson(toReturn);
        }
    }
}
