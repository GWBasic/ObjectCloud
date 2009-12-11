// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using org.mozilla.javascript;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;
using ObjectCloud.WebAccessCodeGenerators;

namespace ObjectCloud.Javascript
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
        public FunctionCaller(ScopeWrapper scopeWrapper, IFileContainer theObject, string method, Function javascriptMethod, Scriptable scope)
        {
            _ScopeWrapper = scopeWrapper;
            _TheObject = theObject;
            JavascriptMethod = javascriptMethod;
            _Scope = scope;
            _Method = method;

            object webCallingConventionObject = javascriptMethod.get("webCallable", scope);

            _WebCallingConvention = Enum<WebCallingConvention>.TryParse(webCallingConventionObject.ToString());

            // Now get the minimum permissions

            FilePermissionEnum? minimumWebPermissionNullable = null;

            if (javascriptMethod.has("minimumWebPermission", scope))
            {
                object minimumWebPermissionObject = javascriptMethod.get("minimumWebPermission", scope);

                if (null != minimumWebPermissionObject)
                    minimumWebPermissionNullable = Enum<FilePermissionEnum>.TryParse(minimumWebPermissionObject.ToString());
            }

            if (null != minimumWebPermissionNullable)
                _MinimumWebPermission = minimumWebPermissionNullable.Value;
            else
                _MinimumWebPermission = FilePermissionEnum.Administer;

            FilePermissionEnum? minimumLocalPermissionNullable = null;

            if (javascriptMethod.has("minimumLocalPermission", scope))
            {
                object minimumLocalPermissionObject = javascriptMethod.get("minimumLocalPermission", scope);

                if (null != minimumLocalPermissionObject)
                    minimumLocalPermissionNullable = Enum<FilePermissionEnum>.TryParse(minimumLocalPermissionObject.ToString());
            }

            if (null != minimumLocalPermissionNullable)
                _MinimumLocalPermission = _MinimumWebPermission;
            else
                _MinimumLocalPermission = FilePermissionEnum.Administer;

            if (javascriptMethod.has("namedPermissions", scope))
            {
                object namedPermissionsObject = javascriptMethod.get("namedPermissions", scope);

                if (null != namedPermissionsObject)
                    _NamedPermissions = StringParser.ParseCommaSeperated(namedPermissionsObject.ToString());
            }
            else
                _NamedPermissions = new string[0];

            Context context = Context.enter();

            string uncompiledMethod;

            try
            {
                uncompiledMethod = context.evaluateString(scope, method + ".toSource();", "<cmd>", 1, null).ToString();
            }
            finally
            {
                Context.exit();
            }

            // Determine the ordinal order of each named argument, and any potential processing that needs to happen to each argument
            try
            {
                string[] prefixAndArgs = uncompiledMethod.Split(new char[] { '(' }, 2);
                string[] argsAndPostfix = prefixAndArgs[1].Split(')');

                string unbrokenArgs = argsAndPostfix[0].Trim();

                if (unbrokenArgs.Length > 0)
                {
                    string[] args = unbrokenArgs.Split(',');

                    for (uint ctr = 0; ctr < args.Length; ctr++)
                    {
                        string argname = args[ctr].Trim();
                        ArgnameToIndex[argname] = ctr;

                        if (javascriptMethod.has("parser_" + argname, scope))
                        {
                            string parser = javascriptMethod.get("parser_" + argname, scope).ToString();

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
                    }
                }
            }
            catch (Exception e)
            {
                throw new JavascriptException("Could not parse the arguments from the function", e);
            }

            // Find out if there is a declared WebReturnConvetion
            WebReturnConvention? webReturnConvention = null;
            if (JavascriptMethod.has("webReturnConvention", Scope))
            {
                string webReturnConventionString = this.JavascriptMethod.get("webReturnConvention", Scope).ToString();
                webReturnConvention = Enum<WebReturnConvention>.TryParse(webReturnConventionString);
            }

            // If there is a declared return convention, use the explicit parser
            if (null != webReturnConvention)
                _WebReturnConvention = webReturnConvention.Value;
            else
                _WebReturnConvention = WebReturnConvention.Primitive;
        }

        /// <summary>
        /// The owning scope
        /// </summary>
        public ScopeWrapper ScopeWrapper
        {
            get { return _ScopeWrapper; }
        }
        private readonly ScopeWrapper _ScopeWrapper;

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
        private readonly Function JavascriptMethod;

        /// <summary>
        /// The scope that it's in
        /// </summary>
        public Scriptable Scope
        {
            get { return _Scope; }
        }
        private readonly Scriptable _Scope;

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
        private readonly Dictionary<string, GenericArgumentReturn<string, object>> ArgnameToConversionDelegate = new Dictionary<string, GenericArgumentReturn<string, object>>();
        
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
        private object NoParsing(string argumentValue)
        {
            return argumentValue;
        }

        /// <summary>
        /// Delegate for when an argument needs to be a number
        /// </summary>
        /// <param name="argumentValue"></param>
        /// <returns></returns>
        private object ParseNumber(string argumentValue)
        {
            if (null == argumentValue)
                return null;

            // The java double type is what's expected
            double parsed;
            if (double.TryParse(argumentValue, out parsed))
                return new java.lang.Double(parsed);
            else
                // If parsing fails, the string is passed as-is.  This is more in line with what Javascript expects
                return argumentValue;
        }

        /// <summary>
        /// Delegate for when an argument needs to be a boolean
        /// </summary>
        /// <param name="argumentValue"></param>
        /// <returns></returns>
        private object ParseBool(string argumentValue)
        {
            if (null == argumentValue)
                return null;

            // The java boolean type is what's expected
            bool parsed;
            if (bool.TryParse(argumentValue, out parsed))
                return new java.lang.Boolean(parsed);
            else
                // If parsing fails, the string is passed as-is.  This is more in line with what Javascript expects
                return argumentValue;
        }

        /// <summary>
        /// Delegate for when an argument needs to be an object passed as a JSON-encoded string
        /// </summary>
        /// <param name="argumentValue"></param>
        /// <returns></returns>
        private object ParseJSON(string argumentValue)
        {
            Context context = Context.enter();

            try
            {
                context.setClassShutter(RestriciveClassShutter.Instance);
                Scriptable scope = context.initStandardObjects();

                object toReturn;
                try
                {
                    toReturn = ScopeWrapper.JsonParseFunction.call(context, scope, scope, new object[] { argumentValue });
                }
                catch
                {
                    throw new WebResultsOverrideException(
                        WebResults.FromString(Status._422_Unprocessable_Entity, "JSON could not be parsed correctly.  Malformed JSON: " + argumentValue));
                }

                return toReturn;
            }
            finally
            {
                Context.exit();
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
        /// Provides access to the current context
        /// </summary>
        public static Stack<Context> ContextStack
        {
            get 
            {
                if (null == _ContextStack)
                    _ContextStack = new Stack<Context>();

                return FunctionCaller._ContextStack; 
            }
        }
        [ThreadStatic]
        private static Stack<Context> _ContextStack;

        /// <summary>
        /// The most recently-entered Javascript context.  This is ThreadStatic
        /// </summary>
        public static Context Context
        {
            get { return ContextStack.Peek(); }
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

                object[] parsedArguments = new object[ArgnameToIndex.Count];

                foreach (KeyValuePair<string, string> argnameAndValue in arguments)
                    if (ArgnameToIndex.ContainsKey(argnameAndValue.Key))
                        parsedArguments[ArgnameToIndex[argnameAndValue.Key]] = ArgnameToConversionDelegate[argnameAndValue.Key](argnameAndValue.Value);

                Context context = Context.enter();

                ContextStack.Push(context);

                object callResults;
                try
                {
                    // If Context.enter() was called prior on this thread, then there will be an exception
                    if (0 == ThreadContextCount)
                        context.setClassShutter(RestriciveClassShutter.Instance);

                    ThreadContextCount++;

                    DateTime start = DateTime.UtcNow;

                    callResults = JavascriptMethod.call(context, Scope, Scope, parsedArguments);

                    TimeSpan callTime = DateTime.UtcNow - start;
                    string logMessage = ScopeWrapper.TheObject.FullPath + "?Method=" + Method + " called in " + callTime.ToString();

                    if (callTime.TotalSeconds > 10)
                        log.Warn(logMessage);
                    else if (callTime.TotalSeconds > 1)
                        log.Info(logMessage);
                    else
                        log.Trace(logMessage);

                    IWebResults toReturn;

                    // If the script is able to construct an IWebResults object, return it
                    if (callResults == null || callResults is Undefined)
                    {
                        if (WebReturnConvention == WebReturnConvention.JavaScriptObject || WebReturnConvention == WebReturnConvention.JSON)
                            return WebResults.ToJson(null);
                        else
                            return WebResults.FromStatus(Status._200_OK);
                    }

                    else if (callResults is IWebResults)
                        return (IWebResults)callResults;

                    else if (callResults is java.lang.Double)
                    {
                        double callResultAsDouble = ((java.lang.Double)callResults).doubleValue();

                        toReturn = WebResults.FromString(Status._200_OK, callResultAsDouble.ToString("R"));
                        toReturn.ContentType = "text/plain";
                        return toReturn;
                    }

                    else if (callResults is java.lang.Boolean)
                    {
                        bool callResultAsBool = ((java.lang.Boolean)callResults).booleanValue();

                        toReturn = WebResults.FromString(Status._200_OK, callResultAsBool ? "true" : " false");
                        toReturn.ContentType = "text/plain";
                        return toReturn;
                    }

                    else if (callResults is string)
                    {
                        toReturn = WebResults.FromString(Status._200_OK, callResults.ToString());
                        toReturn.ContentType = "text/plain";
                        return toReturn;
                    }

                    // The function result isn't a known Javscript primitive.  Stringify it and return it as JSON
                    object callResultsAsJSON = ScopeWrapper.JsonStringifyFunction.call(context, Scope, Scope, new object[] { callResults });

                    toReturn = WebResults.FromString(Status._200_OK, callResultsAsJSON.ToString());
                    toReturn.ContentType = "application/JSON";

                    return toReturn;
                }
                catch (WrappedException we)
                {
                    log.Error("Exception occured in server-side Javascript", we);

                    Exception cause = we.getCause();
                    throw cause;
                }
                catch (EcmaError ee)
                {
                    string exceptionString = "Exception occured in server-side Javascript: " + ee.getMessage() + ", " + ee.details();
                    log.Error(exceptionString);

                    // If the user is an administrator, then the error will be returned
                    if (FilePermissionEnum.Administer == usersPermission)
                        throw new WebResultsOverrideException(
                            WebResults.FromString(Status._500_Internal_Server_Error, exceptionString));

                    throw ee;
                }
                finally
                {
                    Context.exit();
                    ContextStack.Pop();
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
        public string GenerateWrapper()
        {
            if (null == WrapperCache)
                switch (WebCallingConvention.Value)
                {
                    case ObjectCloud.Interfaces.WebServer.WebCallingConvention.GET:
                        WrapperCache = GenerateClientWrapper_GET_application_x_www_form_urlencoded();
                        break;

                    case ObjectCloud.Interfaces.WebServer.WebCallingConvention.GET_application_x_www_form_urlencoded:
                        WrapperCache = GenerateClientWrapper_GET_application_x_www_form_urlencoded();
                        break;

                    case ObjectCloud.Interfaces.WebServer.WebCallingConvention.POST_application_x_www_form_urlencoded:
                        WrapperCache = GenerateClientWrapper_POST_application_x_www_form_urlencoded();
                        break;

                    /*case ObjectCloud.Interfaces.WebServer.WebCallingConvention.POST_string:
                        WrapperCache[wrapperCallsThrough] = GenerateLegacyClientWrapper_POST_string(wrapperCallsThrough);
                        break;*/

                    default:
                        return null;
                }

            return WrapperCache;
        }

        /// <summary>
        /// The generated wrapper for the function
        /// </summary>
        private string WrapperCache = null;

        /// <summary>
        /// Generates a clent-side Javascript wrapper as if this is a GET request without urlencoded arguments
        /// </summary>
        /// <returns></returns>
        private string GenerateClientWrapper_GET()
        {
            return JavascriptWrapperGenerator.GenerateGET(
                Method,
                WebReturnConvention);
        }

        /// <summary>
        /// Generates a clent-side Javascript wrapper as if this is a GET request with urlencoded arguments
        /// </summary>
        /// <returns></returns>
        private string GenerateClientWrapper_GET_application_x_www_form_urlencoded()
        {
            return JavascriptWrapperGenerator.GenerateGET_urlencoded(
                Method,
                WebReturnConvention);
        }

        /// <summary>
        /// Generates a clent-side Javascript wrapper as if this is a POST request with urlencoded arguments
        /// </summary>
        /// <returns></returns>
        private string GenerateClientWrapper_POST_application_x_www_form_urlencoded()
        {
            return JavascriptWrapperGenerator.GeneratePOST_urlencoded(
                Method,
                WebReturnConvention);
        }

        /// <summary>
        /// Generates the wrapper for the given calling source
        /// </summary>
        /// <param name="wrapperCallsThrough"></param>
        /// <returns></returns>
        public string GenerateLegacyWrapper(WrapperCallsThrough wrapperCallsThrough)
        {
            if (!LegacyWrapperCache.ContainsKey(wrapperCallsThrough))
                switch (WebCallingConvention.Value)
                {
                    case ObjectCloud.Interfaces.WebServer.WebCallingConvention.GET:
                        LegacyWrapperCache[wrapperCallsThrough] = GenerateLegacyClientWrapper_GET_application_x_www_form_urlencoded(wrapperCallsThrough);
                        break;

                    case ObjectCloud.Interfaces.WebServer.WebCallingConvention.GET_application_x_www_form_urlencoded:
                        LegacyWrapperCache[wrapperCallsThrough] = GenerateLegacyClientWrapper_GET_application_x_www_form_urlencoded(wrapperCallsThrough);
                        break;

                    case ObjectCloud.Interfaces.WebServer.WebCallingConvention.POST_application_x_www_form_urlencoded:
                        LegacyWrapperCache[wrapperCallsThrough] = GenerateLegacyClientWrapper_POST_application_x_www_form_urlencoded(wrapperCallsThrough);
                        break;

                    case ObjectCloud.Interfaces.WebServer.WebCallingConvention.POST_string:
                        LegacyWrapperCache[wrapperCallsThrough] = GenerateLegacyClientWrapper_POST_string(wrapperCallsThrough);
                        break;

                    default:
                        return null;
                }

            return LegacyWrapperCache[wrapperCallsThrough];
        }

        /// <summary>
        /// Cache of pre-generated wrappers
        /// </summary>
        private Dictionary<WrapperCallsThrough, string> LegacyWrapperCache = new Dictionary<WrapperCallsThrough, string>();

        /// <summary>
        /// Generates a client-side Javascript wrapper as if this is a GET request with urlencoded or no arguments
        /// </summary>
        /// <returns></returns>
        private string GenerateLegacyClientWrapper_GET_application_x_www_form_urlencoded(WrapperCallsThrough wrapperCallsThrough) 
        {
            return JavascriptWrapperGenerator.GenerateLegacyGET_urlencoded(
                Method,
                new List<string>(ArgnameToIndex.Keys),
                WebReturnConvention,
                wrapperCallsThrough);
        }

        /// <summary>
        /// Generates a clent-side Javascript wrapper as if this is a POST request with urlencoded arguments
        /// </summary>
        /// <returns></returns>
        private string GenerateLegacyClientWrapper_POST_application_x_www_form_urlencoded(WrapperCallsThrough wrapperCallsThrough)
        {
            return JavascriptWrapperGenerator.GenerateLegacyPOST_urlencoded(
                Method,
                new List<string>(ArgnameToIndex.Keys),
                WebReturnConvention,
                wrapperCallsThrough);
        }

        /// <summary>
        /// Generates a clent-side Javascript wrapper as if this is a POST request that just takes a string
        /// </summary>
        /// <returns></returns>
        private string GenerateLegacyClientWrapper_POST_string(WrapperCallsThrough wrapperCallsThrough)
        {
            return JavascriptWrapperGenerator.GenerateLegacyPOST(
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
        private FunctionCaller(ScopeWrapper scopeWrapper, IFileContainer theObject, Scriptable scope)
        {
            _ScopeWrapper = scopeWrapper;
            _TheObject = theObject;
            _Scope = scope;
        }

        /// <summary>
        /// Creates a temporary FunctionCaller and then calls del.  Allows Javascript to run outside of the context of a function call
        /// </summary>
        /// <param name="del"></param>
        internal static void UseTemporaryCaller(ScopeWrapper scopeWrapper, IFileContainer theObject, Scriptable scope, Context context, IWebConnection webConnection, GenericVoid del)
        {
            // This value is ThreadStatic so that if the function shells, it can still know about the connection
            FunctionCaller oldMe = _Me;
            _Me = new FunctionCaller(scopeWrapper, theObject, scope);

            try
            {
                WebConnectionStack.Push(webConnection);

                try
                {
                    ContextStack.Push(context);

                    try
                    {
                        del();
                    }
                    finally
                    {
                        ContextStack.Pop();
                    }
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
