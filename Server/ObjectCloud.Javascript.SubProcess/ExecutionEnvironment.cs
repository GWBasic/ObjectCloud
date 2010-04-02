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
    /// Provides a Javascript execution environment for an object and insulation from syntax errors
    /// </summary>
    public class ExecutionEnvironment : IExecutionEnvironment
    {
        static ILog log = LogManager.GetLogger<ExecutionEnvironment>();

        /// <summary>
        /// Creates a Javascript execution environment
        /// </summary>
        /// <param name="fileContainer">The object that will be accessed through Javascript</param>
        /// <param name="javascriptContainer">The text file that contains the javascript</param>
        public ExecutionEnvironment(
            FileHandlerFactoryLocator fileHandlerFactoryLocator,
            IFileContainer fileContainer,
            IFileContainer javascriptContainer,
            GenericReturn<SubProcess> getSubProcessDelegate)
        {
            _FileHandlerFactoryLocator = fileHandlerFactoryLocator;
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

            try
            {
                ScopeWrapper = new ObjectCloud.Javascript.SubProcess.ScopeWrapper(
                    fileHandlerFactoryLocator,
                    javascriptBuilder.ToString(),
                    fileContainer,
                    getSubProcessDelegate);
            }
            catch (Exception e)
            {
                // If the Javascript has an error in it, it must be ignored.  If an error were returned, then malformed Javascript could hose the system!

                log.Error("Error creating scope", e);
            }
        }

        /// <summary>
        /// The scope wrapper
        /// </summary>
        ScopeWrapper ScopeWrapper = null;
        
        /// <summary>
        /// The FileHandlerFactoryLocator
        /// </summary>
        public FileHandlerFactoryLocator FileHandlerFactoryLocator
        {
            get { return _FileHandlerFactoryLocator; }
        }
        private readonly FileHandlerFactoryLocator _FileHandlerFactoryLocator;

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

        /// <summary>
        /// The script's attributes.  These are declared at the beginning of the script with lines that start with // @
        /// </summary>
        public Dictionary<string, string> ScriptAnnotations
        {
            get { return _ScriptAnnotations; }
        }
        private readonly Dictionary<string, string> _ScriptAnnotations = new Dictionary<string, string>();

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

                if (null != ScopeWrapper)
                    return ScopeWrapper.GetMethod(method);
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
            if (null != ScopeWrapper)
                return ScopeWrapper.GenerateJavascriptWrapper();
            else
                return new string[0];
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
            if (null != ScopeWrapper)
                return ScopeWrapper.BlockWebMethods;
            else
                return false;
        }
    }
}
