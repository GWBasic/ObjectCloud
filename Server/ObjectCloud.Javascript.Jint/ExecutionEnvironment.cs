// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using Common.Logging;
using Jint;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Javascript;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Javascript.Jint
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
        /// <param name="theObject">The object that will be accessed through Javascript</param>
        /// <param name="javascriptContainer">The text file that contains the javascript</param>
        public ExecutionEnvironment(FileHandlerFactoryLocator fileHandlerFactoryLocator, IFileContainer theObject, IFileContainer javascriptContainer)
        {
            _FileHandlerFactoryLocator = fileHandlerFactoryLocator;
            _TheObject = theObject;
            _JavascriptContainer = javascriptContainer;

            // load both the javascript and its date in a lock
            ITextHandler javascriptTextHandler = javascriptContainer.CastFileHandler<ITextHandler>();

            IEnumerable<string> javascript;

            using (TimedLock.Lock(javascriptTextHandler))
            {
                _JavascriptLastModified = javascriptTextHandler.LastModified;
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
        public IFileContainer TheObject
        {
            get { return _TheObject; }
        }
        private readonly IFileContainer _TheObject;

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

        /*// <summary>
        /// Returns true if the script is configured to be fast and insecure
        /// </summary>
        public bool IsFastAndInsecure
        {
            get
            {
                if (ScriptAnnotations.ContainsKey("state"))
                    if (ScriptAnnotations["state"] == "shared")
                        return true;

                return false;
            }
        }*/

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
            return ScopeCache.Get(webConnection.Session.User.Id, webConnection);
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
                return new ScopeWrapper(FileHandlerFactoryLocator, webConnection, Javascript, TheObject);
            }
            catch (Exception e)
            {
                _ExecutionEnvironmentErrors = e.Message;
                return null;
            }
        }


        /*// <summary>
        /// Cache of scopes for each user
        /// </summary>
        // TODO:  This could SUCK UP memory if not careful, the ScopeCaches will only be collected if the underlying WebHandler
        // is collected.  If the underlying WebHandler has a life that's much longer then each user, a user could have a Scope
        // that's very old!!!
        Dictionary<ID<IUserOrGroup, Guid>, ScopeWrapper> ScopeCache = new Dictionary<ID<IUserOrGroup, Guid>, ScopeWrapper>();

        /// <summary>
        /// Gets or creates the scope for the user
        /// </summary>
        /// <param name="context"></param>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        ScopeWrapper GetOrCreateScope(IWebConnection webConnection)
        {
            ID<IUserOrGroup, Guid> userId = webConnection.Session.User.Id;

            bool construct = false;

            ScopeWrapper toReturn = null;
            using (TimedLock.Lock(ScopeCache))
                construct = !ScopeCache.TryGetValue(userId, out toReturn);

            if (construct)
            {
                object toLock = null;

                using (TimedLock.Lock(ConstructionLocks))
                {
                    if (!ConstructionLocks.TryGetValue(userId, out toLock))
                    {
                        toLock = new object();
                        ConstructionLocks[userId] = toLock;
                    }
                }

                using (TimedLock.Lock(toLock))
                {
                    using (TimedLock.Lock(ScopeCache))
                        if (!ScopeCache.TryGetValue(userId, out toReturn))
                        {
                            DateTime start = DateTime.UtcNow;

                            toReturn = new ScopeWrapper(FileHandlerFactoryLocator, webConnection, Javascript, TheObject);

                            TimeSpan contructionTime = DateTime.UtcNow - start;

                            if (contructionTime.TotalSeconds > 3)
                                log.Warn("Constructing a ScopeWrapper for " + TheObject.FullPath + " for " + webConnection.Session.User + " took " + contructionTime.ToString());

                            ScopeCache[userId] = toReturn;
                        }
                }
            }

            using (TimedLock.Lock(ScopeLastAccessed))
                ScopeLastAccessed[toReturn] = DateTime.Now;

            // 1 in 100 chance of cleaning old memory
            if (0 == SRandom.Next(0, 99))
                ThreadPool.QueueUserWorkItem(CleanOldScopes);

            return toReturn;
        }

        /// <summary>
        /// Assists in cleaning old scopes
        /// </summary>
        /// <param name="state"></param>
        private void CleanOldScopes(object state)
        {
            IEnumerable<KeyValuePair<ScopeWrapper, DateTime>> toIterate;

            using (TimedLock.Lock(ScopeLastAccessed))
                toIterate = new List<KeyValuePair<ScopeWrapper, DateTime>>(ScopeLastAccessed);

            foreach (KeyValuePair<ScopeWrapper, DateTime> scopeLastAccessed in toIterate)
                if (scopeLastAccessed.Value.AddMinutes(10) < DateTime.UtcNow)
                {
                    using (TimedLock.Lock(ScopeCache))
                        ScopeCache.Remove(scopeLastAccessed.Key.User.Id);

                    using (TimedLock.Lock(ScopeLastAccessed))
                        ScopeLastAccessed.Remove(scopeLastAccessed.Key);
                }
        }*/


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
        /// Generates a Javscript wrapper for the browser that calls functions in this javascript.  Assumes that the prototype AJAX library is present
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> GenerateLegacyJavascriptWrapper(IWebConnection webConnection, WrapperCallsThrough wrapperCallsThrough)
        {
            return GetOrCreateScope(webConnection).GenerateLegacyJavascriptWrapper(wrapperCallsThrough);
        }

        /// <summary>
        /// Any syntax errors in the javascript execution environment
        /// </summary>
        public string ExecutionEnvironmentErrors
        {
            get { return _ExecutionEnvironmentErrors; }
        }
        private string _ExecutionEnvironmentErrors = null;

        /// <summary>
        /// Generates a Javscript wrapper for the browser that calls functions in this javascript
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> GenerateJavascriptWrapper(IWebConnection webConnection)
        {
            return GetOrCreateScope(webConnection).GenerateJavascriptWrapper();
        }

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
