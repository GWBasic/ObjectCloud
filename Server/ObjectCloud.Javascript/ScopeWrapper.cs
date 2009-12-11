// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using org.mozilla.javascript;

using Common.Logging;
using JsonFx.Json;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

// TODO:  Try to replace IKVM with http://www.codeproject.com/KB/dotnet/Espresso.aspx?msg=1575501

namespace ObjectCloud.Javascript
{
    /// <summary>
    /// Wraps a Javascript scope on a per-user basis
    /// </summary>
    public class ScopeWrapper
    {
        private static ILog log = LogManager.GetLogger<ScopeWrapper>();

        public ScopeWrapper(FileHandlerFactoryLocator fileHandlerFactoryLocator, IWebConnection webConnection, string javascript, IFileContainer theObject)
        {
            _FileHandlerFactoryLocator = fileHandlerFactoryLocator;
            _User = webConnection.Session.User;

            List<string> requestedScripts = new List<string>(new string[] { "/API/json2.js" });

            // Find dependant scripts
            if (javascript.StartsWith("// Scripts:"))
            {
                // get first line
                string scriptsLine = javascript.Split('\n')[0];

                foreach (string script in scriptsLine.Substring(11).Split(','))
                    requestedScripts.Add(script.Trim());
            }

            IEnumerable<ScriptAndMD5> dependantScriptsAndMD5s = webConnection.WebServer.WebComponentResolver.DetermineDependantScripts(
                requestedScripts,
                new BlockingShellWebConnection(
                    webConnection.WebServer,
                    webConnection.Session,
                    theObject.FullPath,
                    null,
                    null,
                    webConnection.CookiesFromBrowser,
                    CallingFrom.Web,
                    WebMethod.GET));

            Context context = Context.enter();

            try
            {
                try
                {
                    context.setClassShutter(RestriciveClassShutter.Instance);
                }
                catch (java.lang.SecurityException)
                { 
                    // For now these are being swallowed...  Hopefully we'll move to Jint before this becomes a real issue
                }

                _TheObject = theObject;

                _Scope = context.initStandardObjects();

                // Load static methods that are passed into the Javascript environment as-is
				foreach (Type javascriptFunctionsType in GetTypesThatHaveJavascriptFunctions(theObject))
				{
					java.lang.Class javascriptFunctionsClass = javascriptFunctionsType;
						
	                foreach (MethodInfo csMethod in javascriptFunctionsType.GetMethods(BindingFlags.Static | BindingFlags.Public))
	                {
	                    ParameterInfo[] parameters = csMethod.GetParameters();
	                    java.lang.Class[] parameterTypes = new java.lang.Class[parameters.Length];
	                    for (int ctr = 0; ctr < parameters.Length; ctr++)
	                        parameterTypes[ctr] = parameters[ctr].ParameterType;
	
	                    string methodName = csMethod.Name;
	                    java.lang.reflect.Method javaMethod = javascriptFunctionsClass.getMethod(methodName, parameterTypes);
	
	                    Function function = new FunctionObject(methodName, javaMethod, Scope);
	                    Scope.put(methodName, Scope, function);
					}
				}
				
                // Load all dependant scripts
                foreach (ScriptAndMD5 dependantScript in dependantScriptsAndMD5s)
                {
                    string resolvedScript = webConnection.ShellTo(dependantScript.ScriptName).ResultsAsString;
                    context.evaluateString(Scope, resolvedScript, "<cmd>", 1, null);
                }

                // Get access to JSON functions from C#
                try
                {
                    Scriptable jsonObject = (Scriptable)Scope.get("JSON", Scope);
                    _JsonParseFunction = (Function)jsonObject.get("parse", Scope);
                    _JsonStringifyFunction = (Function)jsonObject.get("stringify", Scope);
                }
                catch (Exception e)
                {
                    throw new JavascriptException("Can not get JSON functions", e);
                }

                AddMetadata(webConnection, context);

                // Construct Javascript to shell to the "base" webHandler
                string baseWrapper = theObject.WebHandler.GetJavascriptWrapperForBase(webConnection, "base");
                context.evaluateString(Scope, baseWrapper, "<cmd>", 1, null);

                // Load the actual script
                FunctionCaller.UseTemporaryCaller(this, theObject, Scope, context, webConnection, delegate()
                {
                    context.evaluateString(Scope, javascript, "<cmd>", 1, null);
                });

                // Initialize each function caller

                object[] ids = Scope.getIds();

                foreach (object id in ids)
                    if (id is string)
                    {
                        string method = (string)id;

                        object javascriptMethodObject = Scope.get(method, Scope);

                        // If the value is a Javascript function...
                        if (javascriptMethodObject is Function)
                        {
                            Function javascriptMethod = (Function)javascriptMethodObject;

                            // ... and it's marked as webCallable, create a FunctionCaller
                            if (javascriptMethod.has("webCallable", Scope))
                            {
                                FunctionCaller functionCaller = new FunctionCaller(this, TheObject, method, javascriptMethod, Scope);

                                // ... and if the function caller supports the calling convention, then cache it!

                                if (null != functionCaller.WebDelegate)
                                    FunctionCallers[method] = functionCaller;
                            }
                        }
                    }

                // Get options
                if (Scope.has("options", Scope))
                    GetOptions();
            }
            finally
            {
                Context.exit();
            }
        }

        /// <summary>
        /// Adds runtime metadata to the server-side javascript
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="context"></param>
        private void AddMetadata(IWebConnection webConnection, Context context)
        {
            //Ability to know the following from within Javascript:  File name, file path, owner name, owner ID, connected user name, connected user id
            Dictionary<string, object> fileMetadata = new Dictionary<string, object>();
            fileMetadata["filename"] = TheObject.Filename;
            fileMetadata["fullpath"] = TheObject.FullPath;
            fileMetadata["url"] = TheObject.ObjectUrl;

            if (null != TheObject.OwnerId)
            {
                fileMetadata["ownerId"] = TheObject.OwnerId.Value;

                IUserOrGroup owner = FileHandlerFactoryLocator.UserManagerHandler.GetUserOrGroupNoException(TheObject.OwnerId.Value);
                if (null != owner)
                {
                    fileMetadata["owner"] = owner.Name;

                    if (owner is IUser)
                        fileMetadata["ownerIdentity"] = ((IUser)owner).Identity;
                }
            }

            AddObject(context, "fileMetadata", fileMetadata);

            Dictionary<string, object> userMetadata = new Dictionary<string, object>();
            IUser user = webConnection.Session.User;
            userMetadata["id"] = user.Id.Value;
            userMetadata["name"] = user.Name;
            userMetadata["identity"] = user.Identity;

            AddObject(context, "userMetadata", userMetadata);

            Dictionary<string, object> hostMetadata = new Dictionary<string, object>();
            hostMetadata["host"] = FileHandlerFactoryLocator.HostnameAndPort;
            hostMetadata["justHost"] = FileHandlerFactoryLocator.Hostname;
            hostMetadata["port"] = FileHandlerFactoryLocator.WebServer.Port;

            AddObject(context, "hostMetadata", hostMetadata);
        }
		
		/// <summary>
		/// Returns the types that have static functions to assist with the given FileHandler based on its type 
		/// </summary>
		/// <param name="theObject">
		/// A <see cref="IFileContainer"/>
		/// </param>
		/// <returns>
		/// A <see cref="IEnumerable"/>
		/// </returns>
		private static IEnumerable<Type> GetTypesThatHaveJavascriptFunctions(IFileContainer theObject)
		{
			yield return typeof(JavascriptFunctions);
			
			IFileHandler fileHandler = theObject.FileHandler;
			
			if (fileHandler is IDatabaseHandler)
				yield return typeof(JavascriptDatabaseFunctions);
		}

        /// <summary>
        /// The user who owns this scope
        /// </summary>
        public IUser User
        {
            get { return _User; }
        }
        private readonly IUser _User;

        /// <summary>
        /// The FileHandlerFactoryLocator
        /// </summary>
        public FileHandlerFactoryLocator FileHandlerFactoryLocator
        {
            get { return _FileHandlerFactoryLocator; }
        }
        private readonly FileHandlerFactoryLocator _FileHandlerFactoryLocator;

        /// <summary>
        /// The wrapped scope
        /// </summary>
        public ScriptableObject Scope
        {
            get { return _Scope; }
        }
        private readonly ScriptableObject _Scope;

        /// <summary>
        /// The Function callers, indexed by function name
        /// </summary>
        private readonly Dictionary<string, FunctionCaller> FunctionCallers = new Dictionary<string,FunctionCaller>();

        /// <summary>
        /// The wrapped object
        /// </summary>
        public IFileContainer TheObject
        {
            get { return _TheObject; }
        }
        private readonly IFileContainer _TheObject;

        /// <summary>
        /// The function to parse JSON data
        /// </summary>
        public Function JsonParseFunction
        {
            get { return _JsonParseFunction; }
        }
        private readonly Function _JsonParseFunction;

        /// <summary>
        /// The function to stringify JSON data
        /// </summary>
        public Function JsonStringifyFunction
        {
            get { return _JsonStringifyFunction; }
        }
        private readonly Function _JsonStringifyFunction;

        /// <summary>
        /// Returns the appropriate delegate for the named method, or null if it doesn't exist
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public WebDelegate GetMethod(string method)
        {
            if (FunctionCallers.ContainsKey(method))
                return FunctionCallers[method].WebDelegate;
            else
                return null;
        }

        /// <summary>
        /// Generates a Javscript wrapper for the browser that calls functions in this javascript
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> GenerateJavascriptWrapper()
        {
            foreach (string method in FunctionCallers.Keys)
                yield return FunctionCallers[method].GenerateWrapper();
        }

        /// <summary>
        /// Generates a Javscript wrapper for the browser that calls functions in this javascript.  Assumes that the prototype AJAX library is present
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> GenerateLegacyJavascriptWrapper(WrapperCallsThrough wrapperCallsThrough)
        {
            foreach (string method in FunctionCallers.Keys)
                yield return FunctionCallers[method].GenerateLegacyWrapper(wrapperCallsThrough);
        }

        /// <summary>
        /// Converts an object from Javascript to a string.  Doubles, bools, and strings are returned via ToString, everything else is JSON-stringified
        /// </summary>
        /// <param name="fromJavascript"></param>
        /// <returns></returns>
        public string ConvertObjectFromJavascriptToString(object fromJavascript)
        {
            if (null == fromJavascript)
                return null;

            else if (fromJavascript is Undefined)
                return null;

            else if (fromJavascript is java.lang.Double)
                return ((java.lang.Double)fromJavascript).doubleValue().ToString("R");

            else if (fromJavascript is java.lang.Boolean)
                return ((java.lang.Boolean)fromJavascript).booleanValue() ? "true" : "false";

            else if (fromJavascript is string)
                return fromJavascript.ToString();

            // The object isn't a known Javscript primitive.  Stringify it and return it as JSON
            Context context = Context.enter();
            try
            {
                object stringified = JsonStringifyFunction.call(context, Scope, Scope, new object[] { fromJavascript });
                return stringified.ToString();
            }
            finally
            {
                Context.exit();
            }
        }

        /// <summary>
        /// Adds an object to be accessible from within the scope
        /// </summary>
        /// <param name="name"></param>
        /// <param name="?"></param>
        private void AddObject(Context context, string name, object toAdd)
        {
            string serialized = JsonWriter.Serialize(toAdd);

            string toEvaluate = string.Format("{0} = {1};", name, serialized);

            context.evaluateString(Scope, toEvaluate, "<cmd>", 1, null);
        }

        /// <summary>
        /// Loads options from the javascript
        /// </summary>
        private void GetOptions()
        {
            object optionsObj = Scope.get("options", Scope);
            string optionsJson = ConvertObjectFromJavascriptToString(optionsObj);
            IDictionary<string, object> options = JsonReader.Deserialize<Dictionary<string, object>>(optionsJson);

            object blockWebMethods = null;
            if (options.TryGetValue("BlockWebMethods", out blockWebMethods))
                try
                {
                    _BlockWebMethods = Convert.ToBoolean(blockWebMethods);
                }
                catch (Exception e)
                {
                    log.Error("Error when parsing options.BlockWebMethods", e);
                }
        }

        /// <summary>
        /// Returns true if underlying web methods are blocked, false otherwise
        /// </summary>
        public bool BlockWebMethods
        {
            get { return _BlockWebMethods; }
        }
        private bool _BlockWebMethods = true;
    }
}
