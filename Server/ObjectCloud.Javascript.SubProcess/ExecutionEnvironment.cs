// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Javascript;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Javascript.SubProcess
{
    /// <summary>
    /// Provides a Javascript execution environment for an object
    /// </summary>
    public class ExecutionEnvironment : IExecutionEnvironment
    {
        static ILog log = LogManager.GetLogger<ExecutionEnvironment>();

        /// <summary>
        /// Creates a Javascript execution environment
        /// </summary>
        /// <param name="fileContainer">The object that will be accessed through Javascript</param>
        /// <param name="javascriptContainer">The text file that contains the javascript</param>
        public ExecutionEnvironment(FileHandlerFactoryLocator fileHandlerFactoryLocator, IFileContainer fileContainer, IFileContainer javascriptContainer)
        {
            _FileHandlerFactoryLocator = fileHandlerFactoryLocator;
            _FileContainer = fileContainer;
            _JavascriptContainer = javascriptContainer;

            // load both the javascript and its date in a lock
            ITextHandler javascriptTextHandler = javascriptContainer.CastFileHandler<ITextHandler>();

            IEnumerable<string> javascript;

            using (TimedLock.Lock(javascriptTextHandler))
            {
                _JavascriptLastModified = javascriptTextHandler.FileContainer.LastModified;
                javascript = javascriptTextHandler.ReadLines();
            }

            StringBuilder javascriptBuilder = new StringBuilder();
            bool buildingAnnotations = true;

            // parse the javascript to find each function and its attributes
            foreach (string line in javascript)
                if (buildingAnnotations)
                    if (line.StartsWith("// @"))
                    {
                        string[] nameAndValue = line.Substring(4).Split(new char[] { ':' }, 2);

                        if (2 == nameAndValue.Length)
                            ScriptAnnotations[nameAndValue[0].Trim()] = nameAndValue[1].Trim();
                        else if (1 == nameAndValue.Length)
                            ScriptAnnotations[nameAndValue[0].Trim()] = string.Empty;
                    }
                    else
                    {
                        javascriptBuilder.AppendLine(line);
                        buildingAnnotations = false;
                    }
                else
                    javascriptBuilder.AppendLine(line);

            _Javascript = javascriptBuilder.ToString();

            ScopeCache = new Cache<ID<IUserOrGroup, Guid>, ScopeWrapper, IWebConnection>(CreateScope);
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
        /// The object being wrapped
        /// </summary>
        public IFileContainer FileContainer
        {
            get { return _FileContainer; }
        }
        private readonly IFileContainer _FileContainer;

        /// <summary>
        /// The file that contains the JavaScript
        /// </summary>
        public IFileContainer JavascriptContainer
        {
            get { return _JavascriptContainer; }
        }
        private readonly IFileContainer _JavascriptContainer;

        public DateTime JavascriptLastModified
        {
            get { return _JavascriptLastModified; }
        }
        private readonly DateTime _JavascriptLastModified;

        public string Javascript
        {
            get { return _Javascript; }
        }
        private readonly string _Javascript;

        /// <summary>
        /// The script's attributes.  These are declared at the beginning of the script with lines that start with // @
        /// </summary>
        public Dictionary<string, string> ScriptAnnotations
        {
            get { return _ScriptAnnotations; }
        }
        private readonly Dictionary<string, string> _ScriptAnnotations = new Dictionary<string, string>();

        /// <summary>
        /// Cache of scopes for each user
        /// </summary>
        Cache<ID<IUserOrGroup, Guid>, ScopeWrapper, IWebConnection> ScopeCache;

        /// <summary>
        /// Gets or creates the scope for the user
        /// </summary>
        /// <param name="context"></param>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        ScopeWrapper GetOrCreateScope(IWebConnection webConnection)
        {
            ScopeWrapper toReturn = ScopeCache.Get(webConnection.Session.User.Id, webConnection);

            if (null == toReturn)
                ScopeCache.Remove(webConnection.Session.User.Id);

            return toReturn;
        }

        /// <summary>
        /// Creates a scope and function callers for the cache
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        ScopeWrapper CreateScope(ID<IUserOrGroup, Guid> userId, IWebConnection webConnection)
        {
            try
            {
                return new ScopeWrapper(FileHandlerFactoryLocator, webConnection, Javascript, FileContainer, GetSubProcess);
            }
            catch (Exception e)
            {
                _ExecutionEnvironmentErrors = e.Message;
                return null;
            }
        }

        /// <summary>
        /// The sub process
        /// </summary>
        private static SubProcess SubProcess = new SubProcess();

        private static object SubProcessKey = new object();

        private static SubProcess GetSubProcess()
        {
            if (!SubProcess.Alive)
                using (TimedLock.Lock(SubProcessKey))
                    if (!SubProcess.Alive)
                        SubProcess = new SubProcess();

            return SubProcess;
        }

        /// <summary>
        /// Returns a delegate to handle the incoming request
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        public virtual WebDelegate GetMethod(IWebConnection webConnection)
        {
            // If the method name was passed through convention...
            if (webConnection.GetParameters.ContainsKey("Method"))
            {
                string method = webConnection.GetParameters["Method"];

                ScopeWrapper scopeWrapper;
                try
                {
                    scopeWrapper = GetOrCreateScope(webConnection);
                }
                catch (Exception e)
                {
                    // If the Javascript has an error in it, it must be ignored.  If an error were returned, then malformed Javascript could hose the system!

                    log.Error("Error creating scope", e);
                    return null;
                }

                if (null != scopeWrapper)
                    return scopeWrapper.GetMethod(method);
            }

            // If all else fails, just return null to indicate that the Javascript environment can't handle this request
            return null;
        }

        /// <summary>
        /// Generates a Javscript wrapper for the browser that calls functions in this javascript
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> GenerateJavascriptWrapper(IWebConnection webConnection)
        {
            return GetOrCreateScope(webConnection).GenerateJavascriptWrapper();
        }

        /// <summary>
        /// Any syntax errors in the javascript execution environment
        /// </summary>
        public string ExecutionEnvironmentErrors
        {
            get { return _ExecutionEnvironmentErrors; }
        }
        private string _ExecutionEnvironmentErrors = null;

        public bool IsBlockWebMethodsEnabled(IWebConnection webConnection)
        {
            ScopeWrapper scopeWrapper = GetOrCreateScope(webConnection);

            if (null != scopeWrapper)
                return scopeWrapper.BlockWebMethods;
            else
                return false;
        }
    }
}
