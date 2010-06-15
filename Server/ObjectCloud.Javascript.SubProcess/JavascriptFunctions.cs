// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

using JsonFx.Json;

using ObjectCloud.Common;
using ObjectCloud.Common.Threading;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Javascript;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Javascript.SubProcess
{
    /// <summary>
    /// All static methods in this class are exposed to every Javascript instance
    /// </summary>
    public static class JavascriptFunctions
    {
        /// <summary>
        /// Generates a web result with additional data for the browser
        /// </summary>
        /// <param name="status">The status.  This is required.  It must be a valid HTTP status code.</param>
        /// <param name="message">The message.  This is optional</param>
        /// <returns></returns>
        public static object generateWebResult(Double status, string message)
        {
            // Get the status value
            int statusInt = Convert.ToInt32(status);

            if (!Enum<Status>.IsDefined(statusInt))
                throw new JavascriptException(status.ToString() + " is not a valid HTTP status");

            Status statusValue = (Status)statusInt;

            if (null == message)
                return WebResults.FromStatus(statusValue);
            else
            {
                IWebResults toReturn = WebResults.FromString(statusValue, message.ToString());
                toReturn.ContentType = "text/plain";

                return toReturn;
            }
        }

        /// <summary>
        /// Throws an exception that has information for the browser
        /// </summary>
        /// <param name="status">The status.  This is required.  It must be a valid HTTP status code.</param>
        /// <param name="message">The message.  This is optional</param>
        public static void throwWebResultOverrideException(Double status, string message)
        {
            IWebResults webResults = (IWebResults)generateWebResult(status, message);

            throw new WebResultsOverrideException(webResults);
        }

        /// <summary>
        /// Converts an IWebResults to something that can be passed back to Javascript
        /// </summary>
        /// <param name="webResults"></param>
        /// <returns></returns>
        private static Dictionary<string, object> ConvertWebResultToJavascript(IWebResults webResults)
        {
            // Only return on a successful status, otherwise, throw an exception
            if ((webResults.Status < Status._200_OK) || (webResults.Status > Status._207_Multi_Status))
                throw new WebResultsOverrideException(webResults);

            Dictionary<string, object> toReturn = new Dictionary<string, object>();
            toReturn["Status"] = (int)webResults.Status;
            toReturn["Content"] = webResults.ResultsAsString;
			toReturn["Headers"] = webResults.Headers;

            return toReturn;
        }

        /// <summary>
        /// Assists in converting a dictionary of strings to an object that Json can serialize
        /// </summary>
        /// <param name="toConvert"></param>
        /// <returns></returns>
        private static Dictionary<string, object> ToJsonable(IEnumerable<KeyValuePair<string, string>> toConvert)
        {
            Dictionary<string, object> toReturn = new Dictionary<string, object>();
            foreach (KeyValuePair<string, string> kvp in toConvert)
                toReturn.Add(kvp.Key, kvp.Value);

            return toReturn;
        }

        /// <summary>
        /// Returns all of the GET parameters
        /// </summary>
        /// <returns></returns>
        public static Dictionary<string, object> getGet()
        {
            FunctionCallContext functionCallContext = FunctionCallContext.GetCurrentContext();
            return ToJsonable(functionCallContext.WebConnection.GetParameters);
        }

        /// <summary>
        /// Returns all of the POST parameters
        /// </summary>
        /// <returns></returns>
        public static object getPost()
        {
            FunctionCallContext functionCallContext = FunctionCallContext.GetCurrentContext();

            if (null != functionCallContext.WebConnection.PostParameters)
                return ToJsonable(functionCallContext.WebConnection.PostParameters);
            else
                return null;
        }

        /// <summary>
        /// Returns all of the Cookies
        /// </summary>
        /// <returns></returns>
        public static object getCookies()
        {
            FunctionCallContext functionCallContext = FunctionCallContext.GetCurrentContext();
            return ToJsonable(functionCallContext.WebConnection.CookiesFromBrowser);
        }

        public static object setCookie(string name, string value)
        {
            FunctionCallContext functionCallContext = FunctionCallContext.GetCurrentContext();
            functionCallContext.WebConnection.CookiesToSet.Add(new CookieToSet(name, value));
            return null;
        }

        /// <summary>
        /// Returns all of the Headers
        /// </summary>
        /// <returns></returns>
        public static object getHeaders()
        {
            FunctionCallContext functionCallContext = FunctionCallContext.GetCurrentContext();
            return ToJsonable(functionCallContext.WebConnection.Headers);
        }

        /// <summary>
        /// Returns the POST contents, unparsed
        /// </summary>
        /// <returns></returns>
        public static object getPostContents()
        {
            FunctionCallContext functionCallContext = FunctionCallContext.GetCurrentContext();

            try
            {
                return functionCallContext.WebConnection.Content.AsString();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Assists in letting server-side javascript call into the server
        /// </summary>
        /// <param name="webMethodString"></param>
        /// <param name="url"></param>
        /// <param name="contentType"></param>
        /// <param name="postArguments"></param>
        /// <param name="bypassJavascript"></param>
        /// <returns></returns>
        public static object Shell(
            string webMethodString,
            string url,
            string contentType,
            object postArguments,
		    Dictionary<string, object> options)
        {
            FunctionCallContext functionCallContext = FunctionCallContext.GetCurrentContext();

            WebMethod webMethod = Enum<WebMethod>.Parse(webMethodString);

            byte[] content;

            if (null != postArguments)
            {
                if (postArguments is Dictionary<string, object>)
                {
                    RequestParameters requestParameters = new RequestParameters();

                    foreach (KeyValuePair<string, object> requestParameter in (Dictionary<string, object>)postArguments)
                        requestParameters.Add(requestParameter.Key, requestParameter.Value.ToString());

                    content = requestParameters.ToBytes();
                }
                else
                    content = Encoding.UTF8.GetBytes(postArguments.ToString());
            }
            else
                content = new byte[0];

            IWebResults shellResult = functionCallContext.WebConnection.ShellTo(
                webMethod,
                url,
                content,
                contentType,
                FunctionCaller.CallingFrom,
                FunctionCaller.BypassJavascript);

			if (null != options)
			{
				object resultsAsBase64;
				if (options.TryGetValue("EncodeAsBase64", out resultsAsBase64))
					if (resultsAsBase64 is bool)
						if ((bool)resultsAsBase64)
							using (Stream contentStream = shellResult.ResultsAsStream)
							{
								byte[] buffer = new byte[contentStream.Length];
								contentStream.Read(buffer, 0, buffer.Length);
								return Convert.ToBase64String(buffer);
							}
			}
			
            return ConvertWebResultToJavascript(shellResult);
        }

        /// <summary>
        /// Sends a GET request to the specified object with the given arguments
        /// </summary>
        /// <param name="file"></param>
        /// <param name="method"></param>
        /// <param name="getArguments"></param>
        /// <returns></returns>
        public static object Shell_GET(string file, object method, Dictionary<string, object> getArguments, bool? bypassJavascriptNullable)
        {
            FunctionCallContext functionCallContext = FunctionCallContext.GetCurrentContext();
            bool bypassJavascript = bypassJavascriptNullable != null ? bypassJavascriptNullable.Value : FunctionCaller.BypassJavascript;

            string url = file;

            if (null != getArguments)
                foreach (KeyValuePair<string, object> idAndVal in getArguments)
                    url = HTTPStringFunctions.AppendGetParameter(url, idAndVal.Key, idAndVal.Value.ToString());

            if (null != method)
                url = HTTPStringFunctions.AppendGetParameter(url, "Method", method.ToString());

            IWebResults shellResult = functionCallContext.WebConnection.ShellTo(url, FunctionCaller.CallingFrom, bypassJavascript);

            return ConvertWebResultToJavascript(shellResult);
        }

        /// <summary>
        /// Sends a POST request to the specified object with the given arguments.  PostArguments must be a Javascript array that will be converted to urlencoded (p1=v1&p2=v2) format
        /// </summary>
        /// <param name="file"></param>
        /// <param name="method"></param>
        /// <param name="getArguments"></param>
        /// <returns></returns>
        public static object Shell_POST_urlencoded(string file, object method, Dictionary<string, object> postArguments, bool? bypassJavascriptNullable)
        {
            FunctionCallContext functionCallContext = FunctionCallContext.GetCurrentContext();
            bool bypassJavascript = bypassJavascriptNullable != null ? bypassJavascriptNullable.Value : FunctionCaller.BypassJavascript;

            string url = file;

            RequestParameters requestParameters = new RequestParameters();

            if (null != postArguments)
            {
                foreach (KeyValuePair<string, object> idAndVal in postArguments)
                    requestParameters[idAndVal.Key] = idAndVal.Value.ToString();
            }

            if (null != method)
                url = HTTPStringFunctions.AppendGetParameter(url, "Method", method.ToString());

            IWebResults shellResult = functionCallContext.WebConnection.ShellTo(
                WebMethod.POST,
                url,
                requestParameters.ToBytes(),
                "application/x-www-form-urlencoded",
                FunctionCaller.CallingFrom,
                bypassJavascript);

            return ConvertWebResultToJavascript(shellResult);
        }

        /// <summary>
        /// Sends a POST request to the specified object with the given content.  ToPost must either be a primitive that's sent directly, or an object that will be serialized to JSON
        /// </summary>
        /// <param name="file"></param>
        /// <param name="method"></param>
        /// <param name="toPost">Sent as-if if string, converted to string if number or bool, serialized to JSON otherwise</param>
        /// <returns></returns>
        public static object Shell_POST(string file, object method, object toPost, bool? bypassJavascriptNullable)
        {
            FunctionCallContext functionCallContext = FunctionCallContext.GetCurrentContext();
            bool bypassJavascript = bypassJavascriptNullable != null ? bypassJavascriptNullable.Value : FunctionCaller.BypassJavascript;

            string url = file;

            byte[] content = null;

            if (null != toPost)
            {
                string contentString;
                if (toPost is string)
                    contentString = toPost.ToString();
                else
                    contentString = JsonWriter.Serialize(toPost);

                if (null != contentString)
                    content = Encoding.UTF8.GetBytes(contentString);
            }

            if (null != method)
                url = HTTPStringFunctions.AppendGetParameter(url, "Method", method.ToString());

            IWebResults shellResult = functionCallContext.WebConnection.ShellTo(
                WebMethod.POST,
                url,
                content,
                "",
                FunctionCaller.CallingFrom,
                bypassJavascript);

            return ConvertWebResultToJavascript(shellResult);
        }

        /// <summary>
        /// Calls the given function in an elevated cecurity context
        /// </summary>
        /// <param name="function"></param>
        /// <returns></returns>
        public static object elevate(SubProcess.Callback callback)
        {
            return SetTempCallingFrom(callback, CallingFrom.Local);
        }

        /// <summary>
        /// Calls the given function in an de-elevated cecurity context
        /// </summary>
        /// <param name="function"></param>
        /// <returns></returns>
        public static object deElevate(SubProcess.Callback callback)
        {
            return SetTempCallingFrom(callback, CallingFrom.Web);
        }

        /// <summary>
        /// Calls a function within the context of a CallingFrom
        /// </summary>
        /// <param name="function"></param>
        /// <param name="callingFrom"></param>
        /// <returns></returns>
        private static object SetTempCallingFrom(SubProcess.Callback callback, CallingFrom callingFrom)
        {
            CallingFrom priorCallingFrom = FunctionCaller.CallingFrom;
            FunctionCaller.CallingFrom = callingFrom;

            try
            {
                return callback.Call(new object[0]);
            }
            finally
            {
                FunctionCaller.CallingFrom = priorCallingFrom;
            }
        }

        /// <summary>
        /// Disables server-side JavaScript while calling the passed-in function
        /// </summary>
        /// <param name="function"></param>
        /// <param name="callingFrom"></param>
        /// <returns></returns>
        public static object bypassJavascript(SubProcess.Callback callback)
        {
            bool oldBypassJavascript = FunctionCaller.BypassJavascript;
            FunctionCaller.BypassJavascript = true;

            try
            {
                return callback.Call(new object[0]);
            }
            finally
            {
                FunctionCaller.BypassJavascript = oldBypassJavascript;
            }
        }

        /// <summary>
        /// Locks the owning object so that the passed in function has exclusive access to it
        /// </summary>
        /// <param name="function"></param>
        /// <returns></returns>
        public static object lockMe(SubProcess.Callback callback)
        {
            FunctionCallContext functionCallContext = FunctionCallContext.GetCurrentContext();

            using (TimedLock.Lock(functionCallContext.ScopeWrapper.FileContainer.FileHandler))
            {
                return callback.Call(new object[0]);
            }
        }

        /// <summary>
        /// Calls the function as the owner of the object
        /// </summary>
        /// <param name="function"></param>
        /// <returns></returns>
        public static object callAsOwner(SubProcess.Callback callback)
        {
            FunctionCallContext functionCallContext = FunctionCallContext.GetCurrentContext();

            ID<IUserOrGroup, Guid>? ownerId = functionCallContext.ScopeWrapper.FileContainer.OwnerId;

            IUser owner;
            if (null != ownerId)
                owner = functionCallContext.ScopeWrapper.FileHandlerFactoryLocator.UserManagerHandler.GetUser(ownerId.Value);
            else
                owner = functionCallContext.WebConnection.WebServer.FileHandlerFactoryLocator.UserFactory.AnonymousUser;

            // When calling as the owner, a shell web connection is pushed that impersonates the owner
            IWebConnection shellConnection = functionCallContext.WebConnection.CreateShellConnection(owner);
            FunctionCaller.WebConnectionStack.Push(shellConnection);

            try
            {
                return callback.Call(new object[0]);
            }
            finally
            {
                FunctionCaller.WebConnectionStack.Pop();
            }
        }

        /// <summary>
        /// Removes malicious HTML
        /// </summary>
        /// <param name="toSanitize"></param>
        /// <returns></returns>
        public static object sanitize(string toSanitize)
        {
            return HTTPStringFunctions.Sanitize(toSanitize);
        }

        /// <summary>
        /// Gets the parent directory wrapper
        /// </summary>
        /// <returns></returns>
        public static object getParentDirectoryWrapper()
        {
            FunctionCallContext functionCallContext = FunctionCallContext.GetCurrentContext();
            return functionCallContext.ScopeWrapper.GetParentDirectoryWrapper(functionCallContext.WebConnection);
        }

        /// <summary>
        /// This is where related objects should go.  ObjectCloud's current limitation is that related objects must be in the same directory; but
        /// future versions may not have this limitation.  By using this function, you can ensure that related objects always go where they need
        /// to go!
        /// </summary>
        /// <returns></returns>
        public static object getDefaultRelatedObjectDirectoryWrapper()
        {
            return getParentDirectoryWrapper();
        }

        /// <summary>
        /// Loads the given Javascript library into the scope, if it is not yet loaded
        /// </summary>
        /// <param name="toLoad"></param>
        public static object use(string toLoad)
        {
            FunctionCallContext functionCallContext = FunctionCallContext.GetCurrentContext();
            return functionCallContext.ScopeWrapper.Use(functionCallContext.WebConnection, toLoad);
        }

        /// <summary>
        /// Returns a wrapper to use the specified object
        /// </summary>
        /// <param name="toOpen"></param>
        /// <returns></returns>
        public static object open(string toOpen)
        {
            FunctionCallContext functionCallContext = FunctionCallContext.GetCurrentContext();
            return functionCallContext.ScopeWrapper.Open(functionCallContext.WebConnection, toOpen);
        }

        /// <summary>
        /// Returns information about the current user
        /// </summary>
        /// <returns></returns>
        public static Dictionary<string, object> getConnectionMetadata()
        {
            FunctionCallContext functionCallContext = FunctionCallContext.GetCurrentContext();
            IWebConnection webConnection = functionCallContext.WebConnection;

            Dictionary<string, object> connectionMetadata = new Dictionary<string, object>();
            IUser user = webConnection.Session.User;
            connectionMetadata["id"] = user.Id.Value;
            connectionMetadata["name"] = user.Name;
            connectionMetadata["identity"] = user.Identity;
            connectionMetadata["isLocal"] = user.Local;
            connectionMetadata["remoteEndPoint"] = webConnection.RemoteEndPoint.ToString();

            return connectionMetadata;
        }

        /// <summary>
        /// Returns a url with the browser cache MD5 in it so that the browser can cache the given object for a long time
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static string getBrowserCacheUrl(string url)
        {
            FunctionCallContext functionCallContext = FunctionCallContext.GetCurrentContext();
            IWebConnection webConnection = functionCallContext.WebConnection;

            return webConnection.GetBrowserCacheUrl(url);
        }
    }
}
