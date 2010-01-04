// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using org.mozilla.javascript;

using JsonFx.Json;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

using Double = java.lang.Double;

namespace ObjectCloud.Javascript
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
        public static object generateWebResult(Double status, object message)
        {
            // Get the status value
            int statusInt = status.intValue();

            if (!Enum<Status>.IsDefined(statusInt))
                throw new JavascriptException(status.toString() + " is not a valid HTTP status");

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
        public static void throwWebResultOverrideException(Double status, object message)
        {
            IWebResults webResults = (IWebResults)generateWebResult(status, message);

            throw new WebResultsOverrideException(webResults);
        }

        /// <summary>
        /// Converts an IWebResults to something that can be passed back to Javascript
        /// </summary>
        /// <param name="webResults"></param>
        /// <returns></returns>
        private static Scriptable ConvertWebResultToJavascript(FunctionCallContext functionCallContext, IWebResults webResults)
        {
            // Only return on a successful status, otherwise, throw an exception
            if ((webResults.Status < Status._200_OK) || (webResults.Status > Status._207_Multi_Status))
                throw new WebResultsOverrideException(webResults);

            // For some reason, I can't figure out how to construct an object in C# using the rhino APIs...
            // Therefore, I'm serializing to JSON and then un-serializing in Javascript
            // TODO:  Optimize this

            Dictionary<string, object> toJSON = new Dictionary<string, object>();
            toJSON["Status"] = (int)webResults.Status;
            toJSON["Content"] = webResults.ResultsAsString;
            toJSON["Headers"] = webResults.Headers;

            string webResultsAsJSON = JsonWriter.Serialize(toJSON);

            // eval() is used because it's safe and faster
            object toReturn = functionCallContext.Context.evaluateString(
                functionCallContext.Scope,
                "(" + webResultsAsJSON + ")",
                "<cmd>",
                1,
                null);

            return (Scriptable)toReturn;
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
            java.lang.Boolean bypassJavascript)
        {
            FunctionCallContext functionCallContext = FunctionCallContext.GetCurrentContext();
            bool bypassJavascriptB = bypassJavascript != null ? bypassJavascript.booleanValue() : false;

            WebMethod webMethod = Enum<WebMethod>.Parse(webMethodString);

            byte[] content;

            if (null != postArguments)
            {
                if (postArguments is Scriptable)
                {
                    RequestParameters requestParameters = new RequestParameters();

                    Scriptable postArgumentsS = (Scriptable)postArguments;

                    foreach (object id in postArgumentsS.getIds())
                    {
                        object val;

                        if (id is int)
                            val = postArgumentsS.get((int)id, functionCallContext.Scope);
                        else
                            val = postArgumentsS.get(id.ToString(), functionCallContext.Scope);

                        if (null != val)
                        {
                            string vasAsString = functionCallContext.ScopeWrapper.ConvertObjectFromJavascriptToString(val);

                            if (null != vasAsString)
                                requestParameters[id.ToString()] = vasAsString;
                        }
                    }

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
                functionCallContext.CallingFrom,
                bypassJavascriptB);

            Scriptable toReturn = ConvertWebResultToJavascript(functionCallContext, shellResult);
            return toReturn;
        }

        /// <summary>
        /// Sends a GET request to the specified object with the given arguments
        /// </summary>
        /// <param name="file"></param>
        /// <param name="method"></param>
        /// <param name="getArguments"></param>
        /// <returns></returns>
        public static object Shell_GET(string file, object method, object getArguments, java.lang.Boolean bypassJavascript)
        {
            FunctionCallContext functionCallContext = FunctionCallContext.GetCurrentContext();
            bool bypassJavascriptB = bypassJavascript != null ? bypassJavascript.booleanValue() : false;

            string url = file;

            if ((null != getArguments) && (!(getArguments is Undefined)))
            {
                if (!(getArguments is Scriptable))
                    throw new JavascriptException("Shell called with getArguments that isn't a Javascript array");

                Scriptable getArgumentsS = (Scriptable)getArguments;

                foreach (object id in getArgumentsS.getIds())
                {
                    object val;

                    if (id is int)
                        val = getArgumentsS.get((int)id, functionCallContext.Scope);
                    else
                        val = getArgumentsS.get(id.ToString(), functionCallContext.Scope);

                    if (null != val)
                    {
                        string vasAsString = functionCallContext.ScopeWrapper.ConvertObjectFromJavascriptToString(val);

                        if (null != vasAsString)
                            url = HTTPStringFunctions.AppendGetParameter(url, id.ToString(), vasAsString);
                    }
                }
            }

            if ((null != method) && (!(method is Undefined)))
                url = HTTPStringFunctions.AppendGetParameter(url, "Method", method.ToString());

            IWebResults shellResult = functionCallContext.WebConnection.ShellTo(url, functionCallContext.CallingFrom, bypassJavascriptB);

            Scriptable toReturn = ConvertWebResultToJavascript(functionCallContext, shellResult);
            return toReturn;
        }

        /// <summary>
        /// Sends a POST request to the specified object with the given arguments.  PostArguments must be a Javascript array that will be converted to urlencoded (p1=v1&p2=v2) format
        /// </summary>
        /// <param name="file"></param>
        /// <param name="method"></param>
        /// <param name="getArguments"></param>
        /// <returns></returns>
        public static object Shell_POST_urlencoded(string file, string method, object postArguments, java.lang.Boolean bypassJavascript)
        {
            FunctionCallContext functionCallContext = FunctionCallContext.GetCurrentContext();
            bool bypassJavascriptB = bypassJavascript != null ? bypassJavascript.booleanValue() : false;

            string url = file;

            RequestParameters requestParameters = new RequestParameters();

            if (null != postArguments)
            {
                if (!(postArguments is Scriptable))
                    throw new JavascriptException("Shell called with postArguments that isn't a Javascript array");

                Scriptable postArgumentsS = (Scriptable)postArguments;

                foreach (object id in postArgumentsS.getIds())
                {
                    object val;

                    if (id is int)
                        val = postArgumentsS.get((int)id, functionCallContext.Scope);
                    else
                        val = postArgumentsS.get(id.ToString(), functionCallContext.Scope);

                    if (null != val)
                    {
                        string vasAsString = functionCallContext.ScopeWrapper.ConvertObjectFromJavascriptToString(val);

                        if (null != vasAsString)
                            requestParameters[id.ToString()] = vasAsString;
                    }
                }
            }

            if (null != method)
                url = HTTPStringFunctions.AppendGetParameter(url, "Method", method);

            IWebResults shellResult = functionCallContext.WebConnection.ShellTo(
                WebMethod.POST,
                url,
                requestParameters.ToBytes(),
                "application/x-www-form-urlencoded",
                functionCallContext.CallingFrom,
                bypassJavascriptB);

            Scriptable toReturn = ConvertWebResultToJavascript(functionCallContext, shellResult);
            return toReturn;
        }

        /// <summary>
        /// Sends a POST request to the specified object with the given content.  ToPost must either be a primitive that's sent directly, or an object that will be serialized to JSON
        /// </summary>
        /// <param name="file"></param>
        /// <param name="method"></param>
        /// <param name="toPost">Sent as-if if string, converted to string if number or bool, serialized to JSON otherwise</param>
        /// <returns></returns>
        public static object Shell_POST(string file, string method, object toPost, java.lang.Boolean bypassJavascript)
        {
            FunctionCallContext functionCallContext = FunctionCallContext.GetCurrentContext();
            bool bypassJavascriptB = bypassJavascript != null ? bypassJavascript.booleanValue() : false;

            string url = file;

            byte[] content = null;

            if (null != toPost)
            {
                string contentString = functionCallContext.ScopeWrapper.ConvertObjectFromJavascriptToString(toPost);

                if (null != contentString)
                    content = Encoding.UTF8.GetBytes(contentString);
            }

            if (null != method)
                url = HTTPStringFunctions.AppendGetParameter(url, "Method", method);

            IWebResults shellResult = functionCallContext.WebConnection.ShellTo(
                WebMethod.POST,
                url,
                content,
                "",
                functionCallContext.CallingFrom,
                bypassJavascriptB);

            Scriptable toReturn = ConvertWebResultToJavascript(functionCallContext, shellResult);
            return toReturn;
        }

        /// <summary>
        /// Calls the given function in an elevated cecurity context
        /// </summary>
        /// <param name="function"></param>
        /// <returns></returns>
        public static object elevate(Function function)
        {
            return SetTempCallingFrom(function, CallingFrom.Local);
        }

        /// <summary>
        /// Calls the given function in an de-elevated cecurity context
        /// </summary>
        /// <param name="function"></param>
        /// <returns></returns>
        public static object deElevate(Function function)
        {
            return SetTempCallingFrom(function, CallingFrom.Web);
        }

        /// <summary>
        /// Calls a function within the context of a CallingFrom
        /// </summary>
        /// <param name="function"></param>
        /// <param name="callingFrom"></param>
        /// <returns></returns>
        private static object SetTempCallingFrom(Function function, CallingFrom callingFrom)
        {
            FunctionCallContext functionCallContext = FunctionCallContext.GetCurrentContext();

            CallingFrom priorCallingFrom = FunctionCaller.CallingFrom;
            FunctionCaller.CallingFrom = callingFrom;

            try
            {
                object toReturn =  function.call(functionCallContext.Context, functionCallContext.Scope, functionCallContext.Scope, new object[0]);
                return toReturn;
            }
            finally
            {
                FunctionCaller.CallingFrom = priorCallingFrom;
            }
        }

        /// <summary>
        /// Locks the owning object so that the passed in function has exclusive access to it
        /// </summary>
        /// <param name="function"></param>
        /// <returns></returns>
        public static object lockMe(Function function)
        {
            FunctionCallContext functionCallContext = FunctionCallContext.GetCurrentContext();

            using (TimedLock.Lock(functionCallContext.ScopeWrapper.TheObject.FileHandler))
            {
                object toReturn = function.call(functionCallContext.Context, functionCallContext.Scope, functionCallContext.Scope, new object[0]);
                return toReturn;
            }
        }

        /// <summary>
        /// Calls the function as the owner of the object
        /// </summary>
        /// <param name="function"></param>
        /// <returns></returns>
        public static object callAsOwner(Function function)
        {
            FunctionCallContext functionCallContext = FunctionCallContext.GetCurrentContext();

            ISession tempSession = functionCallContext.ScopeWrapper.FileHandlerFactoryLocator.SessionManagerHandler.CreateSession();

            ID<IUserOrGroup, Guid>? ownerId = functionCallContext.ScopeWrapper.TheObject.OwnerId;

            if (null != ownerId)
            {
                IUser owner = functionCallContext.ScopeWrapper.FileHandlerFactoryLocator.UserManagerHandler.GetUser(ownerId.Value);
                tempSession.User = owner;
            }

            try
            {
                object toReturn = null;

                functionCallContext.WebConnection.TemporaryChangeSession(tempSession, delegate()
                {
                    toReturn = function.call(functionCallContext.Context, functionCallContext.Scope, functionCallContext.Scope, new object[0]);
                });

                return toReturn;
            }
            finally
            {
                functionCallContext.ScopeWrapper.FileHandlerFactoryLocator.SessionManagerHandler.EndSession(tempSession.SessionId);
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

            IDirectoryHandler parentDirectoryHandler = functionCallContext.ScopeWrapper.TheObject.ParentDirectoryHandler;

            if (null == parentDirectoryHandler)
                throw new WebResultsOverrideException(WebResults.FromString(Status._400_Bad_Request, "The root directory has no parent directory"));

            IWebResults webResults = parentDirectoryHandler.FileContainer.WebHandler.GetServersideJavascriptWrapper(functionCallContext.WebConnection, null);
            string webResultsAsString = webResults.ResultsAsString;

            object toReturn = functionCallContext.Context.evaluateString(
                functionCallContext.Scope,
                "(" + webResultsAsString + ")",
                "<cmd>",
                1,
                null);

            return toReturn;
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
    }
}
