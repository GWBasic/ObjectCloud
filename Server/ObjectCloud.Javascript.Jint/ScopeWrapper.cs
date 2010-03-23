// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;

using Common.Logging;

using Jint;
using Jint.Expressions;
using Jint.Native;
using JsonFx.Json;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Javascript.Jint
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
            _TheObject = theObject;

            // Load static methods that are passed into the Javascript environment as-is
            foreach (Type javascriptFunctionsType in GetTypesThatHaveJavascriptFunctions(theObject))
            {
                PropertyInfo DelegatesProperty = javascriptFunctionsType.GetProperty("Delegates", BindingFlags.Public | BindingFlags.Static);
                IEnumerable<Delegate> delegates = (IEnumerable<Delegate>)DelegatesProperty.GetValue(null, null);

                foreach (Delegate del in delegates)
                    GlobalScope[del.Method.Name] = ExecutionVisitor.Global.FunctionClass.New(del);
            }

            StringBuilder scopeBuilder = new StringBuilder();

            scopeBuilder.Append(@"
var JSON =
{
   stringify: stringify,
   parse: parse
};

");

            List<string> requestedScripts = new List<string>();

            // Find dependant scripts
            if (javascript.StartsWith("// Scripts:"))
            {
                // get first line
                string scriptsLine = javascript.Split('\n')[0];

                foreach (string script in scriptsLine.Substring(11).Split(','))
                    requestedScripts.Add(script.Trim());
            }

            IEnumerable<ScriptAndMD5> dependantScriptsAndMD5s = webConnection.WebServer.WebComponentResolver.DetermineDependantScripts(
                requestedScripts, webConnection);

            // TODO:  Need a Jint equivilent of a class shutter
            //context.setClassShutter(RestriciveClassShutter.Instance);

            // Load all dependant scripts
            foreach (ScriptAndMD5 dependantScript in dependantScriptsAndMD5s)
            {
                string resolvedScript = webConnection.ShellTo(dependantScript.ScriptName).ResultsAsString;
                //JintEngine.Run(resolvedScript);

                scopeBuilder.Append(resolvedScript);
            }

            AddMetadata(webConnection, scopeBuilder);

            // Construct Javascript to shell to the "base" webHandler
            string baseWrapper = theObject.WebHandler.zGetJavascriptWrapperForBase(webConnection, "base");
            scopeBuilder.Append(baseWrapper);

            // Load the actual script

            scopeBuilder.Append(javascript);
            
            //object globalScope = null;
            FunctionCaller.UseTemporaryCaller(this, theObject, webConnection, delegate()
            {
                Run(scopeBuilder.ToString());
            });

            // Initialize each function caller
            // Iterate over each global value
            foreach (string globalKey in GlobalScope.GetKeys())
            {
                JsInstance globalValue = GlobalScope[globalKey];

                // If the value is a Javascript function...
                if (globalValue is JsFunction)
                {
                    string method = globalKey.ToString();
                    JsFunction javascriptMethod = (JsFunction)globalValue;

                    List<string> keys = new List<string>(javascriptMethod.GetKeys());
                    List<JsInstance> values = new List<JsInstance>(javascriptMethod.GetValues());

                    // ... and it's marked as webCallable, create a FunctionCaller
                    if (javascriptMethod.HasProperty("webCallable"))
                    {
                        FunctionCaller functionCaller = new FunctionCaller(this, TheObject, method, javascriptMethod);

                        // ... and if the function caller supports the calling convention, then cache it!

                        if (null != functionCaller.WebDelegate)
                            FunctionCallers[method] = functionCaller;
                    }
                }
            }

            // Load additional runtime options
            if (GlobalScope.HasProperty("options"))
                GetOptions();
        }

        /// <summary>
        /// Runs a set of JavaScript statements and optionally returns a value if return is called  (NOTE: This function was pulled out of Jint's source code)
        /// </summary>
        /// <param name="program">The expression tree to execute</param>
        /// <param name="unwrap">Whether to unwrap the returned value to a CLR instance. <value>True</value> by default.</param>
        /// <returns>Optionaly, returns a value from the scripts</returns>
        /// <exception cref="System.ArgumentException" />
        /// <exception cref="System.Security.SecurityException" />
        /// <exception cref="Jint.JintException" />
        public void Run(string script)
        {
            Program program = JintEngine.Compile(script, false);

            if (program == null)
                throw new
                    ArgumentException("Script can't be null", "script");

            ExecutionVisitor.DebugMode = false;
            ExecutionVisitor.PermissionSet = new System.Security.PermissionSet(System.Security.Permissions.PermissionState.None);

            try
            {
                ExecutionVisitor.Visit(program);
            }
            catch (SecurityException)
            {
                throw;
            }
            catch (JsException e)
            {
                string message = e.Message;
                if (e.Value.Class == JsError.TYPEOF)
                    message = ((JsError)e.Value).Value.ToString();
                StringBuilder stackTrace = new StringBuilder();
                string source = String.Empty;

                if (ExecutionVisitor.CurrentStatement.Source != null)
                {
                    source = Environment.NewLine + ExecutionVisitor.CurrentStatement.Source.ToString()
                            + Environment.NewLine + ExecutionVisitor.CurrentStatement.Source.Code;
                }

                throw new JintException(message + source + stackTrace, e);
            }
            catch (Exception e)
            {
                StringBuilder stackTrace = new StringBuilder();
                string source = String.Empty;

                if (ExecutionVisitor.CurrentStatement.Source != null)
                {
                    source = Environment.NewLine + ExecutionVisitor.CurrentStatement.Source.ToString()
                            + Environment.NewLine + ExecutionVisitor.CurrentStatement.Source.Code;
                }

                throw new JintException(e.Message + source + stackTrace, e.InnerException);
            }
        }

        /// <summary>
        /// The execution visitor
        /// </summary>
        public ExecutionVisitor ExecutionVisitor
        {
            get { return _ExecutionVisitor; }
        }
        private readonly ExecutionVisitor _ExecutionVisitor = new ExecutionVisitor();

        /// <summary>
        /// The global scope
        /// </summary>
        public JsDictionaryObject GlobalScope
        {
            get { return _ExecutionVisitor.GlobalScope; }
        }

        /// <summary>
        /// The semaphore used to prevent concurrent access to the same JintEngine
        /// </summary>
        internal Semaphore Semaphore
        {
            get { return _Semaphore; }
        }
        private Semaphore _Semaphore = new Semaphore(1, 1);

        /// <summary>
        /// The function caller that's currently holding the semaphore and blocking everyone else
        /// </summary>
        internal FunctionCaller BlockingFunctionCaller = null;

        /// <summary>
        /// The WebConnection that's currently holding the semaphore and blocking everyone else
        /// </summary>
        internal IWebConnection BlockingWebConnection = null;

        /// <summary>
        /// Adds runtime metadata to the server-side javascript
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="context"></param>
        private void AddMetadata(IWebConnection webConnection, StringBuilder scopeBuilder)
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

            AddObject("fileMetadata", fileMetadata, scopeBuilder);

            Dictionary<string, object> userMetadata = new Dictionary<string, object>();
            IUser user = webConnection.Session.User;
            userMetadata["id"] = user.Id;
            userMetadata["name"] = user.Name;
            userMetadata["identity"] = user.Identity;

            AddObject("userMetadata", userMetadata, scopeBuilder);

            Dictionary<string, object> hostMetadata = new Dictionary<string, object>();
            hostMetadata["host"] = FileHandlerFactoryLocator.HostnameAndPort;
            hostMetadata["justHost"] = FileHandlerFactoryLocator.Hostname;
            hostMetadata["port"] = FileHandlerFactoryLocator.WebServer.Port;

            AddObject("hostMetadata", hostMetadata, scopeBuilder);
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
        /// The FileHandlerFactoryLocator
        /// </summary>
        public FileHandlerFactoryLocator FileHandlerFactoryLocator
        {
            get { return _FileHandlerFactoryLocator; }
        }
        private readonly FileHandlerFactoryLocator _FileHandlerFactoryLocator;

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

        /*// <summary>
        /// The function to parse JSON data
        /// </summary>
        public JsFunction JsonParseFunction
        {
            get { return _JsonParseFunction; }
        }
        private readonly JsFunction _JsonParseFunction = null;

        /// <summary>
        /// The function to stringify JSON data
        /// </summary>
        public JsFunction JsonStringifyFunction
        {
            get { return _JsonStringifyFunction; }
        }
        private readonly JsFunction _JsonStringifyFunction = null;*/

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
        /// Generates a Javscript wrapper for the browser that calls functions in this javascript.  Assumes that the prototype AJAX library is present
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> GenerateLegacyJavascriptWrapper(WrapperCallsThrough wrapperCallsThrough)
        {
            foreach (string method in FunctionCallers.Keys)
                yield return FunctionCallers[method].GenerateLegacyWrapper(wrapperCallsThrough);
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
        /// Converts an object from Javascript to a string.  Doubles, bools, and strings are returned via ToString, everything else is JSON-stringified
        /// </summary>
        /// <param name="fromJavascript"></param>
        /// <returns></returns>
        public string ConvertObjectFromJavascriptToString(object fromJavascript)
        {
            if (null == fromJavascript)
                return null;

            else if (fromJavascript is JsUndefined)
                return null;

            else if (fromJavascript is JsNumber)
                return ((JsNumber)fromJavascript).ToNumber().ToString("R");

            else if (fromJavascript is JsBoolean)
                return ((JsBoolean)fromJavascript).ToBoolean().ToString();

            else if (fromJavascript is JsString)
                return fromJavascript.ToString();

            else if (fromJavascript is JsDictionaryObject)
            {
                // The object isn't a known Javscript primitive.  Stringify it and return it as JSON
                IDictionary<object, object> converted = DictionaryCreator.ToDictionary((JsObject)fromJavascript);
                return JsonWriter.Serialize(converted);
            }

            throw new JavascriptException("Can not convert a " + fromJavascript.GetType().ToString() + " to a string");
        }

        /// <summary>
        /// Adds an object to be accessible from within the scope
        /// </summary>
        /// <param name="name"></param>
        /// <param name="?"></param>
        private void AddObject(string name, object toAdd, StringBuilder scopeBuilder)
        {
            string serialized = JsonWriter.Serialize(toAdd);

            scopeBuilder.AppendFormat("{0} = {1};", name, serialized);
        }

        /// <summary>
        /// Loads options from the javascript
        /// </summary>
        private void GetOptions()
        {
            JsDictionaryObject optionsObj = (JsDictionaryObject)GlobalScope["options"];
            if (optionsObj.HasProperty("BlockWebMethods"))
                try
                {
                    _BlockWebMethods = optionsObj["BlockWebMethods"].ToBoolean();
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
