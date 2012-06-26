// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Javascript;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;
using ObjectCloud.WebAccessCodeGenerators;

namespace ObjectCloud.Javascript.SubProcess
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
        /// <param name="fileContainer"></param>
        /// <param name="functionName"></param>
        /// <param name="javascriptMethod"></param>
        /// <param name="scope"></param>
        public FunctionCaller(
            ScopeWrapper scopeWrapper,
            IFileContainer fileContainer,
            string functionName,
            SubProcess.CreateScopeFunctionInfo functionInfo)
        {
            Dictionary<string, object> properties = functionInfo.Properties;

            _ScopeWrapper = scopeWrapper;
            _FileContainer = fileContainer;
            _FunctionName = functionName;

            _WebCallingConvention = Enum<WebCallingConvention>.TryParse(properties["webCallable"].ToString());

            // Now get the minimum permissions

            FilePermissionEnum? minimumWebPermissionNullable = null;
            object minimumWebPermissionObject;
            if (properties.TryGetValue("minimumWebPermission", out minimumWebPermissionObject))
                minimumWebPermissionNullable = Enum<FilePermissionEnum>.TryParse(minimumWebPermissionObject.ToString());

            if (null != minimumWebPermissionNullable)
                _MinimumWebPermission = minimumWebPermissionNullable.Value;
            else
                _MinimumWebPermission = FilePermissionEnum.Administer;

            FilePermissionEnum? minimumLocalPermissionNullable = null;
            object minimumLocalPermissionObject;
            if (properties.TryGetValue("minimumLocalPermission", out minimumLocalPermissionObject))
                minimumLocalPermissionNullable = Enum<FilePermissionEnum>.TryParse(minimumLocalPermissionObject.ToString());

            if (null != minimumLocalPermissionNullable)
                _MinimumLocalPermission = _MinimumWebPermission;
            else
                _MinimumLocalPermission = FilePermissionEnum.Administer;

            object namedPermissionsObject;
            if (properties.TryGetValue("namedPermissions", out namedPermissionsObject))
                if (null != namedPermissionsObject)
                    _NamedPermissions = new List<string>(StringParser.ParseCommaSeperated(namedPermissionsObject.ToString())).ToArray();
                else
                    _NamedPermissions = new string[0];
            else
                _NamedPermissions = new string[0];

            uint numArgs = 0;
            foreach (string argname in functionInfo.Arguments)
            {
                ArgnameToIndex[argname] = numArgs;
                numArgs++;

                object parser;
                if (properties.TryGetValue("parser_" + argname, out parser))
                {
                    switch (parser.ToString())
                    {
                        case ("number"):
                            ArgnameToConversionDelegate[argname] = ParseNumber;
                            break;

                        case ("bool"):
                            ArgnameToConversionDelegate[argname] = ParseBool;
                            break;

                        case ("JSON"):
                            ArgnameToConversionDelegate[argname] = JsonFx.Json.JsonReader.Deserialize;// ParseJSON;
                            break;

                        default:
                            ArgnameToConversionDelegate[argname] = NoParsing;
                            break;
                    }
                }
                else
                    ArgnameToConversionDelegate[argname] = NoParsing;
            }

            // Find out if there is a declared WebReturnConvetion
            WebReturnConvention? webReturnConvention = null;
            object webReturnConventionObject;
            if (properties.TryGetValue("webReturnConvention", out webReturnConventionObject))
                webReturnConvention = Enum<WebReturnConvention>.TryParse(webReturnConventionObject.ToString());

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
        private ScopeWrapper _ScopeWrapper;

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
        public IFileContainer FileContainer
        {
            get { return _FileContainer; }
        }
        private readonly IFileContainer _FileContainer;

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
        private readonly Dictionary<string, Func<string, object>> ArgnameToConversionDelegate = new Dictionary<string, Func<string, object>>();
        
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
        public string[] NamedPermissions
        {
            get { return _NamedPermissions; }
        }
        private readonly string[] _NamedPermissions;

        /// <summary>
        /// The current state of CallingFrom.  This always defaults to Web, and only within the context of a call to elevate() is it increased
        /// </summary>
		[ThreadStatic]
		internal static CallingFrom CallingFrom;

        /// <summary>
        /// The current state of bypassing Javascript
        /// </summary>
        [ThreadStatic]
		internal static bool BypassJavascript;

        /// <summary>
        /// The method name
        /// </summary>
        public string FunctionName
        {
            get { return _FunctionName; }
        }
        private readonly string _FunctionName;

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
                return parsed;
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
                return parsed;
            else
                // If parsing fails, the string is passed as-is.  This is more in line with what Javascript expects
                return argumentValue;
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
        /// The most recent FunctionCaller to enter the scope on a given thread
        /// </summary>
        internal static FunctionCaller Current
        {
            get { return FunctionCaller._Current; }
        }

        /// <summary>
        /// ThreadStatic pointer back to this object.  This is used because functions exposed to Javascript need to be static.
        /// </summary>
        [ThreadStatic]
        private static FunctionCaller _Current;

        /// <summary>
        /// Calls the function.  All arguments must be passed as a dictionary of name/value pairs
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns></returns>
        protected IWebResults CallFunction(IWebConnection webConnection, CallingFrom callingFrom, IDictionary<string, string> arguments)
        {
            if (ScopeWrapper.Disposed)
                throw new ObjectDisposedException(FileContainer.FullPath + "'s javascript scope is disposed");
			
			if (!ScopeWrapper.SubProcess.Alive)
			{
				FileContainer.WebHandler.ResetExecutionEnvironment();
				
				IExecutionEnvironment newExecutionEnvironment = FileContainer.WebHandler.GetOrCreateExecutionEnvironment();
				
				if (newExecutionEnvironment is ExecutionEnvironment)
					_ScopeWrapper = ((ExecutionEnvironment)newExecutionEnvironment).ScopeWrapper;
			}

            // This value is ThreadStatic so that if the function shells, it can still know about the connection
            FunctionCaller oldMe = _Current;
            _Current = this;
            CallingFrom priorCallingFrom = CallingFrom;
            CallingFrom = CallingFrom.Web;
			BypassJavascript = webConnection.BypassJavascript;

            FilePermissionEnum? usersPermission = FileContainer.LoadPermission(webConnection.Session.User.Id);

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
                    if (!FileContainer.HasNamedPermissions(webConnection.Session.User.Id, NamedPermissions))
                        // Enforce the permission
                        return WebResults.From(Status._401_Unauthorized);

                object[] parsedArguments = new object[ArgnameToIndex.Count];

                foreach (KeyValuePair<string, string> argnameAndValue in arguments)
                    if (ArgnameToIndex.ContainsKey(argnameAndValue.Key))
                        parsedArguments[ArgnameToIndex[argnameAndValue.Key]] = ArgnameToConversionDelegate[argnameAndValue.Key](argnameAndValue.Value);

                DateTime start = DateTime.UtcNow;

                object callResults = ScopeWrapper.CallFunction(
                    webConnection,
                    FunctionName,
                    parsedArguments);

                TimeSpan callTime = DateTime.UtcNow - start;
                string logMessage = FileContainer.FullPath + "?Method=" + FunctionName + " called in " + callTime.ToString();

                if (callTime.TotalSeconds > 10)
                    log.Warn(logMessage);
                else if (callTime.TotalSeconds > 1)
                    log.Info(logMessage);
                else
                    log.Trace(logMessage);

                IWebResults toReturn;

                // If the script is able to construct an IWebResults object, return it
                if (callResults == null)
                {
                    if (WebReturnConvention == WebReturnConvention.JavaScriptObject || WebReturnConvention == WebReturnConvention.JSON)
                        return WebResults.ToJson(null);
                    else
                        return WebResults.From(Status._200_OK);
                }

                else if (callResults is IWebResults)
                    return (IWebResults)callResults;

                else if (callResults is double)
                {
                    double callResultAsDouble = ((double)callResults);

                    toReturn = WebResults.From(Status._200_OK, callResultAsDouble.ToString("R"));
                    toReturn.ContentType = "text/plain";
                    return toReturn;
                }

                else if (callResults is int)
                {
                    int callResultAsInt = ((int)callResults);

                    toReturn = WebResults.From(Status._200_OK, callResultAsInt.ToString("R"));
                    toReturn.ContentType = "text/plain";
                    return toReturn;
                }

                else if (callResults is bool)
                {
                    bool callResultAsBool = (bool)callResults;

                    toReturn = WebResults.From(Status._200_OK, callResultAsBool ? "true" : " false");
                    toReturn.ContentType = "text/plain";
                    return toReturn;
                }

                else if (callResults is string)
                {
                    toReturn = WebResults.From(Status._200_OK, callResults.ToString());
                    toReturn.ContentType = "text/plain";
                    return toReturn;
                }

                // The function result isn't a known primitive.  Return it as JSON
                return WebResults.ToJson(callResults);
            }
            finally
            {
                // Ensures that this object can be garbage collected
                _Current = oldMe;
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
        public IEnumerable<string> GenerateWrapper()
        {
            if (null == WrapperCache)
                switch (WebCallingConvention.Value)
                {
                    case ObjectCloud.Interfaces.WebServer.WebCallingConvention.GET:
                        WrapperCache = new string[]
                        {
                            JavascriptWrapperGenerator.GenerateGET(
    			                FunctionName,
	    		                WebReturnConvention),

                            JavascriptWrapperGenerator.GenerateGET_Sync(
    			                FunctionName,
	    		                WebReturnConvention),
                        };
                        break;

                    case ObjectCloud.Interfaces.WebServer.WebCallingConvention.GET_application_x_www_form_urlencoded:
                        WrapperCache = new string[]
                        {
                            JavascriptWrapperGenerator.GenerateGET_urlencoded(
			                    FunctionName,
			                    WebReturnConvention),

                            JavascriptWrapperGenerator.GenerateGET_urlencoded_Sync(
			                    FunctionName,
			                    WebReturnConvention)
                        };
                        break;

                    case ObjectCloud.Interfaces.WebServer.WebCallingConvention.POST_application_x_www_form_urlencoded:
                        WrapperCache = new string[]
                        {
                            JavascriptWrapperGenerator.GeneratePOST_urlencoded(
			                    FunctionName,
			                    WebReturnConvention),

                            JavascriptWrapperGenerator.GeneratePOST_urlencoded_Sync(
			                    FunctionName,
			                    WebReturnConvention)
                        };
                        break;

                    case ObjectCloud.Interfaces.WebServer.WebCallingConvention.POST_string:
                        WrapperCache = new string[]
                        {
                            JavascriptWrapperGenerator.GeneratePOST(
			                    FunctionName,
			                    WebReturnConvention),

                            JavascriptWrapperGenerator.GeneratePOST_Sync(
			                    FunctionName,
			                    WebReturnConvention)
                        };
                        break;

                    default:
                        return null;
                }

            return WrapperCache;
        }

        /// <summary>
        /// The generated wrapper for the function
        /// </summary>
        private IEnumerable<string> WrapperCache = null;

         /// <summary>
        /// Note:  The scope must be within a context!
        /// </summary>
        /// <param name="fileContainer"></param>
        /// <param name="method"></param>
        /// <param name="javascriptMethod"></param>
        /// <param name="scope"></param>
        private FunctionCaller(ScopeWrapper scopeWrapper, IFileContainer fileContainer)
        {
            _ScopeWrapper = scopeWrapper;
            _FileContainer = fileContainer;
        }

        /// <summary>
        /// Creates a temporary FunctionCaller and then calls del.  Allows Javascript to run outside of the context of a function call
        /// </summary>
        /// <param name="del"></param>
        internal static R UseTemporaryCaller<R>(
            ScopeWrapper scopeWrapper,
            IFileContainer fileContainer,
            IWebConnection webConnection,
            Func<R> del)
        {
            // This value is ThreadStatic so that if the function shells, it can still know about the connection
            FunctionCaller oldMe = _Current;
            _Current = new FunctionCaller(scopeWrapper, fileContainer);

            try
            {
                WebConnectionStack.Push(webConnection);

                try
                {
                    return del();
                }
                finally
                {
                    // Ensures that this object can be garbage collected
                    WebConnectionStack.Pop();
                }
            }
            finally
            {
                _Current = oldMe;
            }
        }
    }
}
