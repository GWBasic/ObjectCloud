// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using Jint;
using Jint.Delegates;
using Jint.Native;
using JsonFx.Json;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Javascript.Jint
{
    /// <summary>
    /// Functions exposed to Javascript
    /// </summary>
    public static class JavascriptFunctions
    {
        /// <summary>
        /// All of the functions that are exposed to Javascript
        /// </summary>
        public static IEnumerable<Delegate> Delegates
        {
            get
            {
                yield return new Func<double, object, object>(generateWebResult);
                yield return new Action<double, object>(throwWebResultOverrideException);
                yield return new Func<string, string, JsDictionaryObject, bool?, object>(Shell_GET);
                yield return new Func<string, string, JsObject, bool?, object>(Shell_POST_urlencoded);
                yield return new Func<string, string, JsInstance, bool?, object>(Shell_POST);
                yield return new Func<JsFunction, object>(elevate);
                yield return new Func<JsFunction, object>(deElevate);
                yield return new Func<JsFunction, object>(lockMe);
                yield return new Func<JsFunction, object>(callAsOwner);
                yield return new Func<string, object>(sanitize);
                yield return new Func<JsObject, string>(stringify);
                yield return new Func<string, JsInstance>(parse);
                yield return new Func<JsInstance>(getParentDirectoryWrapper);
                yield return new Func<object>(getDefaultRelatedObjectDirectoryWrapper);
            }
        }

        /// <summary>
        /// Generates a web result with additional data for the browser
        /// </summary>
        /// <param name="status">The status.  This is required.  It must be a valid HTTP status code.</param>
        /// <param name="message">The message.  This is optional</param>
        /// <returns></returns>
        public static object generateWebResult(double status, object message)
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
        public static void throwWebResultOverrideException(double status, object message)
        {
            IWebResults webResults = (IWebResults)generateWebResult(status, message);

            throw new WebResultsOverrideException(webResults);
        }

        /// <summary>
        /// Converts an IWebResults to something that can be passed back to Javascript
        /// </summary>
        /// <param name="webResults"></param>
        /// <returns></returns>
        private static object ConvertWebResultToJavascript(FunctionCallContext functionCallContext, IWebResults webResults)
        {
            // Only return on a successful status, otherwise, throw an exception
            if ((webResults.Status < Status._200_OK) || (webResults.Status > Status._207_Multi_Status))
                throw new WebResultsOverrideException(webResults);

            JsObject toReturn = new JsObject();
            toReturn["Status"] = new JsNumber((int)webResults.Status);
            toReturn["Content"] = new JsString(webResults.ResultsAsString);
            toReturn["Headers"] = DictionaryCreator.ToObject<string, string>(webResults.Headers);

            return toReturn;
        }

        /// <summary>
        /// Sends a GET request to the specified object with the given arguments
        /// </summary>
        /// <param name="file"></param>
        /// <param name="method"></param>
        /// <param name="getArguments"></param>
        /// <returns></returns>
        public static object Shell_GET(string file, string method, JsDictionaryObject getArguments, bool? bypassJavascript)
        {
            FunctionCallContext functionCallContext = FunctionCallContext.GetCurrentContext();
            bool bypassJavascriptB = bypassJavascript != null ? bypassJavascript.Value : false;

            string url = file;

            if (null != getArguments)
            {
                if (!(getArguments is JsObject))
                    throw new JavascriptException("Shell called with getArguments that isn't a Javascript array");

                foreach (string id in getArguments.GetKeys())
                {
                    JsInstance val = getArguments[id];

                    if (null != val && (!(val is JsUndefined)))
                    {
                        string vasAsString = functionCallContext.ScopeWrapper.ConvertObjectFromJavascriptToString(val);

                        if (null != vasAsString)
                            url = HTTPStringFunctions.AppendGetParameter(url, id, vasAsString);
                    }
                }
            }

            if (null != method)
                url = HTTPStringFunctions.AppendGetParameter(url, "Method", method);

            IWebResults shellResult = functionCallContext.WebConnection.ShellTo(url, functionCallContext.CallingFrom, bypassJavascriptB);

            object toReturn = ConvertWebResultToJavascript(functionCallContext, shellResult);
            return toReturn;
        }

        /// <summary>
        /// Sends a POST request to the specified object with the given arguments.  PostArguments must be a Javascript array that will be converted to urlencoded (p1=v1&p2=v2) format
        /// </summary>
        /// <param name="file"></param>
        /// <param name="method"></param>
        /// <param name="getArguments"></param>
        /// <returns></returns>
        public static object Shell_POST_urlencoded(string file, string method, JsObject postArguments, bool? bypassJavascript)
        {
            FunctionCallContext functionCallContext = FunctionCallContext.GetCurrentContext();
            bool bypassJavascriptB = bypassJavascript != null ? bypassJavascript.Value : false;

            string url = file;

            RequestParameters requestParameters = new RequestParameters();

            if (null != postArguments)
            {
                if (!(postArguments is JsObject))
                    throw new JavascriptException("Shell called with postArguments that isn't a Javascript array");

                foreach (KeyValuePair<string, JsInstance> argument in postArguments)
                {
                    JsInstance val = argument.Value;

                    if (null != val && (!(val is JsUndefined)))
                    {
                        string vasAsString = functionCallContext.ScopeWrapper.ConvertObjectFromJavascriptToString(val);

                        if (null != vasAsString)
                            requestParameters[argument.Key] = vasAsString;
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

            object toReturn = ConvertWebResultToJavascript(functionCallContext, shellResult);
            return toReturn;
        }

        /// <summary>
        /// Sends a POST request to the specified object with the given content.  ToPost must either be a primitive that's sent directly, or an object that will be serialized to JSON
        /// </summary>
        /// <param name="file"></param>
        /// <param name="method"></param>
        /// <param name="toPost">Sent as-if if string, converted to string if number or bool, serialized to JSON otherwise</param>
        /// <returns></returns>
        public static object Shell_POST(string file, string method, JsInstance toPost, bool? bypassJavascript)
        {
            FunctionCallContext functionCallContext = FunctionCallContext.GetCurrentContext();
            bool bypassJavascriptB = bypassJavascript != null ? bypassJavascript.Value : false;

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

            object toReturn = ConvertWebResultToJavascript(functionCallContext, shellResult);
            return toReturn;
        }

        /// <summary>
        /// Calls the given function in an elevated cecurity context
        /// </summary>
        /// <param name="function"></param>
        /// <returns></returns>
        public static object elevate(JsFunction function)
        {
            return SetTempCallingFrom(function, CallingFrom.Local);
        }

        /// <summary>
        /// Calls the given function in an de-elevated cecurity context
        /// </summary>
        /// <param name="function"></param>
        /// <returns></returns>
        public static object deElevate(JsFunction function)
        {
            return SetTempCallingFrom(function, CallingFrom.Web);
        }

        /// <summary>
        /// Calls a function within the context of a CallingFrom
        /// </summary>
        /// <param name="function"></param>
        /// <param name="callingFrom"></param>
        /// <returns></returns>
        private static object SetTempCallingFrom(JsFunction function, CallingFrom callingFrom)
        {
            FunctionCallContext functionCallContext = FunctionCallContext.GetCurrentContext();

            CallingFrom priorCallingFrom = FunctionCaller.CallingFrom;
            FunctionCaller.CallingFrom = callingFrom;

            try
            {
                function.Execute(
                    functionCallContext.ScopeWrapper.ExecutionVisitor,
                    functionCallContext.ScopeWrapper.ExecutionVisitor.CurrentScope,
                    new JsInstance[0]);

                return functionCallContext.ScopeWrapper.ExecutionVisitor.Result;
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
        public static object lockMe(JsFunction function)
        {
            FunctionCallContext functionCallContext = FunctionCallContext.GetCurrentContext();

            using (TimedLock.Lock(functionCallContext.ScopeWrapper.TheObject.FileHandler))
            {
                function.Execute(functionCallContext.ScopeWrapper.ExecutionVisitor, functionCallContext.ScopeWrapper.GlobalScope, new JsInstance[0]);
                return functionCallContext.ScopeWrapper.ExecutionVisitor.Result;

            }
        }

        /// <summary>
        /// Calls the function as the owner of the object
        /// </summary>
        /// <param name="function"></param>
        /// <returns></returns>
        public static object callAsOwner(JsFunction function)
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
                functionCallContext.WebConnection.TemporaryChangeSession(tempSession, delegate()
                {
                    function.Execute(functionCallContext.ScopeWrapper.ExecutionVisitor, functionCallContext.ScopeWrapper.GlobalScope, new JsInstance[0]);
                });

                return functionCallContext.ScopeWrapper.ExecutionVisitor.Result;

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
        /// Converts a Javascript object to a JSON String
        /// </summary>
        /// <param name="jsInstance"></param>
        /// <returns></returns>
        public static string stringify(JsObject jsObject)
        {
            if (null == jsObject)
                return "null";

            if (null != jsObject.Value)
                return JsonWriter.Serialize(jsObject.Value);

            /*if (jsObject.Length > 0)
            {
            }

            else if ((jsObject is JsNull) || (jsObject is JsUndefined))
                return JsonWriter.Serialize(null);*/

            IDictionary<object, object> asDictionary = DictionaryCreator.ToDictionary(jsObject);
            return JsonWriter.Serialize(asDictionary);
        }

        /// <summary>
        /// Parses JSON without eval
        /// </summary>
        /// <param name="toParse"></param>
        /// <returns></returns>
        public static JsInstance parse(string toParse)
        {
            object parsed = JsonReader.Deserialize(toParse);

            if (parsed is IDictionary)
                return DictionaryCreator.ToObject<object, object>(ConvertIDictionaryToIEnumerable((IDictionary)parsed));

            if (parsed is string)
                return new JsString(parsed.ToString());

            if (parsed is bool)
                return new JsBoolean((bool)parsed);

            // If this fails then JSON parsing is fubr
            double numericalValue = Convert.ToDouble(parsed);
            return new JsNumber(numericalValue);
        }

        private static IEnumerable<KeyValuePair<object, object>> ConvertIDictionaryToIEnumerable(IDictionary toConvert)
        {
            foreach (object key in toConvert.Keys)
                yield return new KeyValuePair<object, object>(key, toConvert[key]);
        }

        /// <summary>
        /// Gets the parent directory wrapper
        /// </summary>
        /// <returns></returns>
        public static JsInstance getParentDirectoryWrapper()
        {
            FunctionCallContext functionCallContext = FunctionCallContext.GetCurrentContext();

            IDirectoryHandler parentDirectoryHandler = functionCallContext.ScopeWrapper.TheObject.ParentDirectoryHandler;

            if (null == parentDirectoryHandler)
                throw new WebResultsOverrideException(WebResults.FromString(Status._400_Bad_Request, "The root directory has no parent directory"));

            IWebResults webResults = parentDirectoryHandler.FileContainer.WebHandler.GetServersideJavascriptWrapper(functionCallContext.WebConnection, null);
            string webResultsAsString = webResults.ResultsAsString;

            /*object toReturn = functionCallContext.Context.evaluateString(
                functionCallContext.Scope,
                "(" + webResultsAsString + ")",
                "<cmd>",
                1,
                null);

            return toReturn;*/

            throw new NotImplementedException();
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
