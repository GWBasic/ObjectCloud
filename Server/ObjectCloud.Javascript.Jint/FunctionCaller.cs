// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using Jint;
using Jint.Expressions;
using Jint.Native;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;
using ObjectCloud.WebAccessCodeGenerators;

namespace ObjectCloud.Javascript.Jint
{
    /// <summary>
    /// Basic functionality needed by all calling conventions
    /// </summary>
    public class FunctionCaller
    {
        private static ILog log = LogManager.GetLogger<FunctionCaller>();

        /// <summary>
        /// Note:  The scope must be within a context!
        /// </summary>
        /// <param name="theObject"></param>
        /// <param name="method"></param>
        /// <param name="javascriptMethod"></param>
        /// <param name="scope"></param>
        public FunctionCaller(ScopeWrapper scopeWrapper, IFileContainer theObject, string method, JsFunction javascriptMethod)
        {
            _ScopeWrapper = scopeWrapper;
            _TheObject = theObject;
            JavascriptMethod = javascriptMethod;
            _Method = method;

            object webCallingConventionObject = javascriptMethod["webCallable"];

            _WebCallingConvention = Enum<WebCallingConvention>.TryParse(webCallingConventionObject.ToString());

            // Now get the minimum permissions

            FilePermissionEnum? minimumWebPermissionNullable = null;

            if (javascriptMethod.HasProperty("minimumWebPermission"))
            {
                string minimumWebPermissionString = javascriptMethod["minimumWebPermission"].ToString();
                minimumWebPermissionNullable = Enum<FilePermissionEnum>.TryParse(minimumWebPermissionString);
            }

            if (null != minimumWebPermissionNullable)
                _MinimumWebPermission = minimumWebPermissionNullable.Value;
            else
                _MinimumWebPermission = FilePermissionEnum.Administer;

            FilePermissionEnum? minimumLocalPermissionNullable = null;

            if (javascriptMethod.HasProperty("minimumLocalPermission"))
            {
                string minimumLocalPermissionString = javascriptMethod["minimumLocalPermission"].ToString();
                minimumLocalPermissionNullable = Enum<FilePermissionEnum>.TryParse(minimumLocalPermissionString);
            }

            if (null != minimumLocalPermissionNullable)
                _MinimumLocalPermission = _MinimumWebPermission;
            else
                _MinimumLocalPermission = FilePermissionEnum.Administer;

            if (javascriptMethod.HasProperty("namedPermissions"))
                _NamedPermissions = StringParser.ParseCommaSeperated(javascriptMethod["namedPermissions"].ToString());
            else
                _NamedPermissions = new string[0];

            uint ctr = 0;
            foreach (string argname in javascriptMethod.Arguments)
            {
                ArgnameToIndex[argname] = ctr;

                if (javascriptMethod.HasProperty("parser_" + argname))
                {
                    string parser = javascriptMethod["parser_" + argname].ToString();

                    switch (parser)
                    {
                        case ("number"):
                            ArgnameToConversionDelegate[argname] = ParseNumber;
                            break;

                        case ("bool"):
                            ArgnameToConversionDelegate[argname] = ParseBool;
                            break;

                        case ("JSON"):
                            ArgnameToConversionDelegate[argname] = ParseJSON;
                            break;

                        default:
                            ArgnameToConversionDelegate[argname] = NoParsing;
                            break;
                    }
                }
                else
                    ArgnameToConversionDelegate[argname] = NoParsing;

                ctr++;
            }

            // Find out if there is a declared WebReturnConvetion
            WebReturnConvention? webReturnConvention = null;
            if (JavascriptMethod.HasProperty("webReturnConvention"))
            {
                string webReturnConventionString = this.JavascriptMethod["webReturnConvention"].ToString();
                webReturnConvention = Enum<WebReturnConvention>.TryParse(webReturnConventionString);
            }

            // If there is a declared return convention, use the explicit parser
            if (null != webReturnConvention)
                _WebReturnConvention = webReturnConvention.Value;
            else
                _WebReturnConvention = WebReturnConvention.Primitive;
        }

        /// <summary>
        /// The WebCallingConvention
        /// </summary>
        public WebCallingConvention? WebCallingConvention
        {
            get { return _WebCallingConvention; }
        }
        private readonly WebCallingConvention? _WebCallingConvention;

        /// <summary>
        /// The wrapped object
        /// </summary>
        public IFileContainer TheObject
        {
            get { return _TheObject; }
        }
        private readonly IFileContainer _TheObject;

        /// <summary>
        /// The method being called
        /// </summary>
        private readonly JsFunction JavascriptMethod;

        /// <summary>
        /// The scope wrapper
        /// </summary>
        public ScopeWrapper ScopeWrapper
        {
            get { return _ScopeWrapper; }
        }
        private readonly ScopeWrapper _ScopeWrapper;

        /// <summary>
        /// The WebReturnConvention.  Defauls to Primitive if the javascript function does not declare it
        /// </summary>
        public WebReturnConvention WebReturnConvention
        {
            get { return _WebReturnConvention; }
        }
        private readonly WebReturnConvention _WebReturnConvention;

        /// <summary>
        /// All of the arguments and the order that they're placed into the function call
        /// </summary>
        private readonly Dictionary<string, uint> ArgnameToIndex = new Dictionary<string, uint>();

        /// <summary>
        /// The delegates that convert the passed in argument from the web to the javascript's expected type
        /// </summary>
        private readonly Dictionary<string, GenericArgumentReturn<string, JsInstance>> ArgnameToConversionDelegate = new Dictionary<string, GenericArgumentReturn<string, JsInstance>>();
        
        /// <summary>
        /// Stack of WebConnections.  Each time a new request is made to call a Javascript method, the WebConnection is put on this stack.  A
        /// stack is used in case calls cross objects
        /// </summary>
        public static Stack<IWebConnection> WebConnectionStack
        {
            get
            {
                if (null == _WebConnectionStack)
                    _WebConnectionStack = new Stack<IWebConnection>();

                return FunctionCaller._WebConnectionStack; 
            }
        }
        [ThreadStatic]
        private static Stack<IWebConnection> _WebConnectionStack;

        /// <summary>
        /// The current WebConnection that a FunctionCaller on this thread is handling, if known
        /// </summary>
        public static IWebConnection WebConnection
        {
            get { return WebConnectionStack.Peek(); }
        }

        /// <summary>
        /// The minimum permission needed to call this function from the web
        /// </summary>
        public FilePermissionEnum MinimumWebPermission
        {
            get { return _MinimumWebPermission; }
        }
        private readonly FilePermissionEnum _MinimumWebPermission;

        /// <summary>
        /// The minimum permission needed to call this function locally
        /// </summary>
        public FilePermissionEnum MinimumLocalPermission
        {
            get { return _MinimumLocalPermission; }
        }
        private readonly FilePermissionEnum _MinimumLocalPermission;

        /// <summary>
        /// Any potential named permissions that allow someone to access a method even if he/she doesn't have the minimum permission
        /// </summary>
        public IEnumerable<string> NamedPermissions
        {
            get { return _NamedPermissions; }
        }
        private readonly IEnumerable<string> _NamedPermissions;

        /// <summary>
        /// The current state of CallingFrom.  This always defaults to Web, and only within the context of a call to elevate() is it increased
        /// </summary>
        [ThreadStatic]
        internal static CallingFrom CallingFrom = CallingFrom.Web;

        /// <summary>
        /// The method name
        /// </summary>
        public string Method
        {
            get { return _Method; }
        }
        private readonly string _Method;

        /// <summary>
        /// Delegate for when an argument needs no parsing
        /// </summary>
        /// <param name="argumentValue"></param>
        /// <returns></returns>
        private JsInstance NoParsing(string argumentValue)
        {
            return new JsString(argumentValue);
        }

        /// <summary>
        /// Delegate for when an argument needs to be a number
        /// </summary>
        /// <param name="argumentValue"></param>
        /// <returns></returns>
        private JsInstance ParseNumber(string argumentValue)
        {
            if (null == argumentValue)
                return null;

            // The java double type is what's expected
            double parsed;
            if (double.TryParse(argumentValue, out parsed))
                return new JsNumber(parsed);
            else
                // If parsing fails, the string is passed as-is.  This is more in line with what Javascript expects
                return new JsString(argumentValue);
        }

        /// <summary>
        /// Delegate for when an argument needs to be a boolean
        /// </summary>
        /// <param name="argumentValue"></param>
        /// <returns></returns>
        private JsInstance ParseBool(string argumentValue)
        {
            if (null == argumentValue)
                return null;

            // The java boolean type is what's expected
            bool parsed;
            if (bool.TryParse(argumentValue, out parsed))
                return new JsBoolean(parsed);
            else
                // If parsing fails, the string is passed as-is.  This is more in line with what Javascript expects
                return new JsString(argumentValue);
        }

        /// <summary>
        /// Delegate for when an argument needs to be an object passed as a JSON-encoded string
        /// </summary>
        /// <param name="argumentValue"></param>
        /// <returns></returns>
        private JsInstance ParseJSON(string argumentValue)
        {
            try
            {
                IDictionary<string, object> parsedArgument = JsonFx.Json.JsonReader.Deserialize<Dictionary<string, object>>(argumentValue);
                return DictionaryCreator.ToObject(parsedArgument);
            }
            catch
            {
                throw new WebResultsOverrideException(
                    WebResults.FromString(Status._422_Unprocessable_Entity, "JSON could not be parsed correctly.  Malformed JSON: " + argumentValue));
            }
        }

        /// <summary>
        /// Calls the function when there is only a single argument, such as with JSON or POST
        /// </summary>
        /// <param name="argument"></param>
        /// <returns></returns>
        protected IWebResults CallFunction(IWebConnection webConnection, CallingFrom callingFrom, string argument)
        {
            if (ArgnameToIndex.Count != 1)
                throw new JavascriptException("This calling convention can only be used when there is one argument");

            string argname = "???";
            foreach (string key in ArgnameToIndex.Keys)
                argname = key;

            Dictionary<string, string> arguments = new Dictionary<string, string>();
            arguments[argname] = argument;

            return CallFunction(webConnection, callingFrom, arguments);
        }

        /// <summary>
        /// ThreadStatic pointer back to this object.  This is used because functions exposed to Javascript need to be static.
        /// </summary>
        [ThreadStatic]
        private static FunctionCaller _Me;

        /// <summary>
        /// The most recent FunctionCaller to enter the scope on a given thread
        /// </summary>
        internal static FunctionCaller Current
        {
            get { return FunctionCaller._Me; }
        }

        /// <summary>
        /// The number of times that a context has been entered on this thread
        /// </summary>
        [ThreadStatic]
        private static uint ThreadContextCount = 0;

        /// <summary>
        /// Calls the function.  All arguments must be passed as a dictionary of name/value pairs
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns></returns>
        protected IWebResults CallFunction(IWebConnection webConnection, CallingFrom callingFrom, IDictionary<string, string> arguments)
        {
            if (0 == ThreadContextCount)
            {
                // Semaphores aren't re-entrant like "lock"
                if (!ScopeWrapper.Semaphore.WaitOne(15000))
                {
                    if (log.IsErrorEnabled)
                    {
                        StringBuilder errorBuilder = new StringBuilder("Javascript is blocked!\n");
                        errorBuilder.AppendFormat("Requested file: {0}\n", ScopeWrapper.TheObject.FullPath);
                        errorBuilder.AppendFormat("Blocking function: {0}\n", ScopeWrapper.BlockingFunctionCaller._Method);
                        errorBuilder.AppendFormat("Parameters: {0}\n", ScopeWrapper.BlockingWebConnection.GetParameters);

                        try
                        {
                            if (null != ScopeWrapper.BlockingWebConnection.Content)
                                errorBuilder.AppendFormat("POST Content: {0}", ScopeWrapper.BlockingWebConnection.Content.AsString());
                        }
                        catch { }

                        log.Error(errorBuilder);
                    }

                    throw new JavascriptException("Could not enter Javascript because another thread is blocking");
                }

                ScopeWrapper.BlockingFunctionCaller = this;
                ScopeWrapper.BlockingWebConnection = webConnection;
            }

            try
            {

                // This value is ThreadStatic so that if the function shells, it can still know about the connection
                FunctionCaller oldMe = _Me;
                _Me = this;
                CallingFrom priorCallingFrom = CallingFrom;
                CallingFrom = CallingFrom.Web;

                FilePermissionEnum? usersPermission = TheObject.LoadPermission(webConnection.Session.User.Id);

                WebConnectionStack.Push(webConnection);

                try
                {
                    // Determine which permission to use, web or local
                    FilePermissionEnum minimumPermission;
                    if (callingFrom == CallingFrom.Local)
                        minimumPermission = MinimumLocalPermission;
                    else
                        minimumPermission = MinimumWebPermission;

                    // If the user doesn't have the minimum permission, check for a named permission
                    if (minimumPermission > usersPermission)
                        if (!TheObject.HasNamedPermissions(webConnection.Session.User.Id, NamedPermissions))
                            // Enforce the permission
                            return WebResults.FromStatus(Status._401_Unauthorized);

                    JsInstance[] parsedArguments = new JsInstance[ArgnameToIndex.Count];

                    foreach (KeyValuePair<string, string> argnameAndValue in arguments)
                        if (ArgnameToIndex.ContainsKey(argnameAndValue.Key))
                            parsedArguments[ArgnameToIndex[argnameAndValue.Key]] = ArgnameToConversionDelegate[argnameAndValue.Key](argnameAndValue.Value);

                    try
                    {
                        // If Context.enter() was called prior on this thread, then there will be an exception
                        //if (0 == ThreadContextCount)
                        //    context.setClassShutter(RestriciveClassShutter.Instance);

                        ThreadContextCount++;

                        JavascriptMethod.Execute(ScopeWrapper.JintEngine.Visitor, ScopeWrapper.GlobalScope, parsedArguments);
                        JsInstance callResults = ScopeWrapper.JintEngine.Visitor.Result;

                        IWebResults toReturn;

                        // If the script is able to construct an IWebResults object, return it
                        if (callResults == null || callResults is JsUndefined)
                        {
                            if (WebReturnConvention == WebReturnConvention.JavaScriptObject || WebReturnConvention == WebReturnConvention.JSON)
                                return WebResults.ToJson(null);
                            else
                                return WebResults.FromStatus(Status._200_OK);
                        }

                            // Return WebResults as-is
                        else if (callResults is IWebResults)
                            return (IWebResults)callResults;
                        else if (callResults.Value is IWebResults)
                            return (IWebResults)callResults.Value;

                        else if (callResults.Value is double)
                        {
                            double callResultAsDouble = (double)callResults.Value;

                            toReturn = WebResults.FromString(Status._200_OK, callResultAsDouble.ToString("R"));
                            toReturn.ContentType = "text/plain";
                            return toReturn;
                        }

                        else if (callResults.Value is bool)
                        {
                            bool callResultAsBool = (bool)callResults.Value;

                            toReturn = WebResults.FromString(Status._200_OK, callResultAsBool ? "true" : " false");
                            toReturn.ContentType = "text/plain";
                            return toReturn;
                        }

                        else if (callResults.Value is string)
                        {
                            toReturn = WebResults.FromString(Status._200_OK, callResults.Value.ToString());
                            toReturn.ContentType = "text/plain";
                            return toReturn;
                        }

                        else if (callResults is JsObject)
                        {
                            IDictionary<object, object> callResultsAsDictionary = DictionaryCreator.ToDictionary((JsObject)callResults);
                            return WebResults.ToJson(callResultsAsDictionary);
                        }

                        else if (null == callResults.Value)
                            return WebResults.ToJson(null);

                        /*// The function result isn't a known Javscript primitive.  Stringify it and return it as JSON
                        object callResultsAsJSON = ScopeWrapper.JsonStringifyFunction.Execute(
                            ScopeWrapper.JintEngine.Visitor, ScopeWrapper.GlobalScope, new JsInstance[] { (JsInstance)callResults });

                        toReturn = WebResults.FromString(Status._200_OK, callResultsAsJSON.ToString());*/

                        /*IDictionary<object, object> callResultsAsDictionary = DictionaryCreator.ToDictionary(callResults);
                        toReturn = WebResults.ToJson(callResultsAsDictionary);
                        toReturn.ContentType = "application/JSON";
                        return toReturn;*/

                        throw new JavascriptException("I do not know how to handle a " + callResults.GetType().ToString() + " result of " + callResults.Value != null ? callResults.Value.ToString() : "null");
                    }
                    /*catch (EcmaError ee)
                    {
                        string exceptionString = "Exception occured in server-side Javascript: " + ee.getMessage() + ", " + ee.details();
                        log.Error(exceptionString);

                        // If the user is an administrator, then the error will be returned
                        if (FilePermissionEnum.Administer == usersPermission)
                            throw new WebResultsOverrideException(
                                WebResults.FromString(Status._500_Internal_Server_Error, exceptionString));

                        throw ee;
                    }*/
                    finally
                    {
                        ThreadContextCount--;
                    }
                }
                finally
                {
                    // Ensures that this object can be garbage collected
                    _Me = oldMe;
                    WebConnectionStack.Pop();
                    CallingFrom = priorCallingFrom;
                }
            }
            finally
            {
                if (0 == ThreadContextCount)
                {
                    // Release the semaphore
                    ScopeWrapper.BlockingFunctionCaller = null;
                    ScopeWrapper.BlockingWebConnection = null;
                    ScopeWrapper.Semaphore.Release();
                }
            }
        }

        /// <summary>
        /// Empty argument list
        /// </summary>
        private static readonly Dictionary<string, string> EmptyArguments = new Dictionary<string, string>();

        /// <summary>
        /// Calls the function from a GET request
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        private IWebResults GET(IWebConnection webConnection, CallingFrom callingFrom)
        {
            return CallFunction(webConnection, callingFrom, EmptyArguments);
        }

        /// <summary>
        /// Calls the function from a GET request that has arguments
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        private IWebResults GET_application_x_www_form_urlencoded(IWebConnection webConnection, CallingFrom callingFrom)
        {
            return CallFunction(webConnection, callingFrom, webConnection.GetParameters);
        }

        /// <summary>
        /// Calls the function from a POST request that has arguments
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        private IWebResults POST_application_x_www_form_urlencoded(IWebConnection webConnection, CallingFrom callingFrom)
        {
            if (null != webConnection.PostParameters)
                return CallFunction(webConnection, callingFrom, webConnection.PostParameters);
            else
                return CallFunction(webConnection, callingFrom, EmptyArguments);
        }

        /// <summary>
        /// Calls the function from a POST request that just sends a string
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        private IWebResults POST_string(IWebConnection webConnection, CallingFrom callingFrom)
        {
            return CallFunction(webConnection, callingFrom, webConnection.Content.AsString());
        }

        /// <summary>
        /// The WebDelegate for calling this function, or null if this function can not be called
        /// </summary>
        public WebDelegate WebDelegate
        {
            get
            {
                if (null == WebCallingConvention)
                    return null;

                switch (WebCallingConvention.Value)
                {
                    case ObjectCloud.Interfaces.WebServer.WebCallingConvention.GET:
                        return GET;

                    case ObjectCloud.Interfaces.WebServer.WebCallingConvention.GET_application_x_www_form_urlencoded:
                        return GET_application_x_www_form_urlencoded;

                    case ObjectCloud.Interfaces.WebServer.WebCallingConvention.POST_application_x_www_form_urlencoded:
                        return POST_application_x_www_form_urlencoded;

                    /*case WebCallingConvention.POST_bytes:
                        toReturn[methodInfo.Name] = new WebCallableMethod.POST_bytes(methodInfo, webCallableAttribute);
                        break;

                    case WebCallingConvention.POST_JSON:
                        return functionCaller.POST_string;

                    case WebCallingConvention.POST_multipart_form_data:
                        toReturn[methodInfo.Name] = new WebCallableMethod.POST_Multipart(methodInfo, webCallableAttribute);
                        break;

                    case WebCallingConvention.POST_stream:
                        toReturn[methodInfo.Name] = new WebCallableMethod.POST_stream(methodInfo, webCallableAttribute);
                        break;*/

                    case ObjectCloud.Interfaces.WebServer.WebCallingConvention.POST_string:
                        return POST_string;

                    /*case WebCallingConvention.other:
                        toReturn[methodInfo.Name] = new WebCallableMethod.Other(methodInfo, webCallableAttribute);
                        break;

                    case WebCallingConvention.Naked:
                        toReturn[methodInfo.Name] = new WebCallableMethod.Naked(methodInfo, webCallableAttribute);
                        break;*/

                    default:
                        return null;
                }
            }
        }

        /// <summary>
        /// Generates the wrapper for the given calling source
        /// </summary>
        /// <param name="wrapperCallsThrough"></param>
        /// <returns></returns>
        public string GenerateWrapper(WrapperCallsThrough wrapperCallsThrough)
        {
            if (!WrapperCache.ContainsKey(wrapperCallsThrough))
                switch (WebCallingConvention.Value)
                {
                    case ObjectCloud.Interfaces.WebServer.WebCallingConvention.GET:
                        WrapperCache[wrapperCallsThrough] = GenerateClientWrapper_GET_application_x_www_form_urlencoded(wrapperCallsThrough);
                        break;

                    case ObjectCloud.Interfaces.WebServer.WebCallingConvention.GET_application_x_www_form_urlencoded:
                        WrapperCache[wrapperCallsThrough] = GenerateClientWrapper_GET_application_x_www_form_urlencoded(wrapperCallsThrough);
                        break;

                    case ObjectCloud.Interfaces.WebServer.WebCallingConvention.POST_application_x_www_form_urlencoded:
                        WrapperCache[wrapperCallsThrough] = GenerateClientWrapper_POST_application_x_www_form_urlencoded(wrapperCallsThrough);
                        break;

                    case ObjectCloud.Interfaces.WebServer.WebCallingConvention.POST_string:
                        WrapperCache[wrapperCallsThrough] = GenerateClientWrapper_POST_string(wrapperCallsThrough);
                        break;

                    default:
                        return null;
                }

            return WrapperCache[wrapperCallsThrough];
        }

        /// <summary>
        /// Cache of pre-generated wrappers
        /// </summary>
        private Dictionary<WrapperCallsThrough, string> WrapperCache = new Dictionary<WrapperCallsThrough, string>();

        /// <summary>
        /// Generates a client-side Javascript wrapper as if this is a GET request with urlencoded or no arguments
        /// </summary>
        /// <returns></returns>
        private string GenerateClientWrapper_GET_application_x_www_form_urlencoded(WrapperCallsThrough wrapperCallsThrough) 
        {
            return JavascriptWrapperGenerator.GenerateGET_urlencoded(
                Method,
                new List<string>(ArgnameToIndex.Keys),
                WebReturnConvention,
                wrapperCallsThrough);
        }

        /// <summary>
        /// Generates a clent-side Javascript wrapper as if this is a POST request with urlencoded arguments
        /// </summary>
        /// <returns></returns>
        private string GenerateClientWrapper_POST_application_x_www_form_urlencoded(WrapperCallsThrough wrapperCallsThrough)
        {
            return JavascriptWrapperGenerator.GeneratePOST_urlencoded(
                Method,
                new List<string>(ArgnameToIndex.Keys),
                WebReturnConvention,
                wrapperCallsThrough);
        }

        /// <summary>
        /// Generates a clent-side Javascript wrapper as if this is a POST request that just takes a string
        /// </summary>
        /// <returns></returns>
        private string GenerateClientWrapper_POST_string(WrapperCallsThrough wrapperCallsThrough)
        {
            return JavascriptWrapperGenerator.GeneratePOST(
                Method,
                new List<string>(ArgnameToIndex.Keys),
                WebReturnConvention,
                wrapperCallsThrough);
        }

         /// <summary>
        /// Note:  The scope must be within a context!
        /// </summary>
        /// <param name="theObject"></param>
        /// <param name="method"></param>
        /// <param name="javascriptMethod"></param>
        /// <param name="scope"></param>
        private FunctionCaller(ScopeWrapper scopeWrapper, IFileContainer theObject)
        {
            _ScopeWrapper = scopeWrapper;
            _TheObject = theObject;
        }

        /// <summary>
        /// Creates a temporary FunctionCaller and then calls del.  Allows Javascript to run outside of the context of a function call
        /// </summary>
        /// <param name="del"></param>
        internal static void UseTemporaryCaller(ScopeWrapper scopeWrapper, IFileContainer theObject, IWebConnection webConnection, GenericVoid del)
        {
            // This value is ThreadStatic so that if the function shells, it can still know about the connection
            FunctionCaller oldMe = _Me;
            _Me = new FunctionCaller(scopeWrapper, theObject);

            try
            {
                try
                {
                    WebConnectionStack.Push(webConnection);

                    del();
                }
                finally
                {
                    // Ensures that this object can be garbage collected
                    WebConnectionStack.Pop();
                }
            }
            finally
            {
                _Me = oldMe;
            }
        }
    }
}
