// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading;

using Common.Logging;
using JsonFx.Json;

using ObjectCloud.Common;
using ObjectCloud.Common.Threading;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Javascript.SubProcess
{
    /// <summary>
    /// Wraps parent scopes
    /// </summary>
    public class ParentScopeFactory
    {
        private static ILog log = LogManager.GetLogger<ParentScopeFactory>();

        /// <summary>
        /// The sub process
        /// </summary>
        public SubProcess SubProcess
        {
            get { return _SubProcess; }
        }
        private readonly SubProcess _SubProcess;

        private FileHandlerFactoryLocator FileHandlerFactoryLocator;

        public ParentScopeFactory(FileHandlerFactoryLocator fileHandlerFactoryLocator, SubProcess subProcess)
        {
            FileHandlerFactoryLocator = fileHandlerFactoryLocator;
            _SubProcess = subProcess;
        }

        /// <summary>
        /// All of the loaded parent scopes
        /// </summary>
        private Dictionary<IFileContainer, ParentScope> LoadedParentScopes = new Dictionary<IFileContainer, ParentScope>();

        ReaderWriterLockSlim LoadedParentScopesLock = new ReaderWriterLockSlim();

        /// <summary>
        /// Returns the ID for the javascript container's parent scope, creates the parent scope and disposes an old one, if needed
        /// </summary>
        /// <param name="javascriptContainer"></param>
        /// <returns></returns>
        public ParentScope GetParentScope(IFileContainer javascriptContainer)
        {
            ParentScope parentScope = null;

            LoadedParentScopesLock.EnterReadLock();

            // First try getting the parent scope ID in a read-only lock
            try
            {
                LoadedParentScopes.TryGetValue(javascriptContainer, out parentScope);
            }
            finally
            {
                LoadedParentScopesLock.ExitReadLock();
            }

            if (null != parentScope)
                if (parentScope.StillValid)
                {
                    bool stillValid = true;

                    foreach (KeyValuePair<IFileContainer, DateTime> loadedScriptModifiedTime in parentScope.LoadedScriptsModifiedTimes)
                        stillValid = stillValid & (loadedScriptModifiedTime.Key.LastModified == loadedScriptModifiedTime.Value);

                    if (stillValid)
                        return parentScope;
                }

            // Either there's no parent scope or it's invalid
            using (TimedLock.Lock(javascriptContainer, TimeSpan.FromSeconds(15)))
            {
                ParentScope parentScopeFromOtherThread = null;
                LoadedParentScopesLock.EnterReadLock();

                // First try getting the parent scope ID in a read-only lock
                try
                {
                    LoadedParentScopes.TryGetValue(javascriptContainer, out parentScopeFromOtherThread);
                }
                finally
                {
                    LoadedParentScopesLock.ExitReadLock();
                }

                // If another thread loaded the parent scope, then restart the process of getting it
                if (parentScope != parentScopeFromOtherThread)
                    return GetParentScope(javascriptContainer);

                // If there's an old parent scope, delete all references to it and dispose it
                if (null != parentScope)
                {
                    // If code changed within the scope, then abort
                    IWebHandler webHandler;
                    while (parentScope.WebHandlersWithThisAsParent.Dequeue(out webHandler))
                        webHandler.ResetExecutionEnvironment();

                    // If the parent scope isn't valid, make sure its disposed
                    ThreadPool.QueueUserWorkItem(delegate(object state)
                    {
                        try
                        {
                            _SubProcess.DisposeParentScope((int)state, Thread.CurrentThread.ManagedThreadId);
                        }
                        catch (Exception e)
                        {
                            log.Error("Exception when cleaning up a dead parent scope", e);
                        }
                    }, parentScope.ParentScopeId);
                }

                parentScope = CreateParentScope(javascriptContainer);

                // If the parent scope ID wasn't found, then get a write lock
                LoadedParentScopesLock.EnterWriteLock();

                try
                {
                    LoadedParentScopes[javascriptContainer] = parentScope;
                }
                finally
                {
                    LoadedParentScopesLock.ExitWriteLock();
                }

                return parentScope;
            }
        }

        private ParentScope CreateParentScope(IFileContainer javascriptContainer)
        {
            List<string> scriptsToEval = new List<string>();
            List<string> requestedScripts = new List<string>(new string[] { "/API/AJAX_serverside.js", "/API/json2.js" });

            string fileType = null;
            StringBuilder javascriptBuilder = new StringBuilder();
            foreach (string line in javascriptContainer.CastFileHandler<ITextHandler>().ReadLines())
            {
                // find file type
                if (line.Trim().StartsWith("// FileType:"))
                    fileType = line.Substring(12).Trim();

                // Find dependant scripts
                else if (line.Trim().StartsWith("// Scripts:"))
                    foreach (string script in line.Substring(11).Split(','))
                        requestedScripts.Add(script.Trim());

                else
                    javascriptBuilder.AppendFormat("{0}\n", line);
            }

            string javascript = javascriptBuilder.ToString();

            Dictionary<string, MethodInfo> functionsInScope = new Dictionary<string, MethodInfo>();
            List<KeyValuePair<IFileContainer, DateTime>> loadedScriptsModifiedTimes = new List<KeyValuePair<IFileContainer, DateTime>>();
            ISession ownerSession = FileHandlerFactoryLocator.SessionManagerHandler.CreateSession();

            try
            {
                ownerSession.Login(javascriptContainer.Owner);

                IWebConnection ownerWebConnection = new BlockingShellWebConnection(
                    FileHandlerFactoryLocator.WebServer,
                    ownerSession,
                    javascriptContainer.FullPath,
                    null,
                    null,
                    new CookiesFromBrowser(),
                    CallingFrom.Web,
                    WebMethod.GET);

                IEnumerable<ScriptAndMD5> dependantScriptsAndMD5s = FileHandlerFactoryLocator.WebServer.WebComponentResolver.DetermineDependantScripts(
                    requestedScripts,
                    ownerWebConnection);

                // Load static methods that are passed into the Javascript environment as-is
                foreach (Type javascriptFunctionsType in GetTypesThatHaveJavascriptFunctions(fileType))
                    foreach (MethodInfo method in javascriptFunctionsType.GetMethods(BindingFlags.Static | BindingFlags.Public))
                        functionsInScope[method.Name] = method;

                // Load all dependant scripts
                foreach (ScriptAndMD5 dependantScript in dependantScriptsAndMD5s)
                {
                    string scriptName = dependantScript.ScriptName;

                    if (scriptName.Contains("?"))
                        scriptsToEval.Add(ownerWebConnection.ShellTo(scriptName).ResultsAsString);
                    else
                    {
                        IFileContainer scriptFileContainer = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(scriptName);

                        if (scriptFileContainer.FileHandler is ITextHandler)
                        {
                            loadedScriptsModifiedTimes.Add(new KeyValuePair<IFileContainer, DateTime>(scriptFileContainer, scriptFileContainer.LastModified));
                            scriptsToEval.Add(scriptFileContainer.CastFileHandler<ITextHandler>().ReadAll());
                        }
                        else
                            scriptsToEval.Add(ownerWebConnection.ShellTo(scriptName).ResultsAsString);
                    }
                }

                loadedScriptsModifiedTimes.Add(new KeyValuePair<IFileContainer, DateTime>(javascriptContainer, javascriptContainer.LastModified));

                // Construct Javascript to shell to the "base" webHandler
                Set<Type> webHandlerTypes = new Set<Type>(FileHandlerFactoryLocator.WebHandlerPlugins);
                if (null != fileType)
                    webHandlerTypes.Add(FileHandlerFactoryLocator.WebHandlerClasses[fileType]);

                string baseWrapper = GetJavascriptWrapperForBase("base", webHandlerTypes);

                scriptsToEval.Add(baseWrapper);
                scriptsToEval.Add(javascript);
                scriptsToEval.Add("if (this.options) options; else null;");

                ParentScope parentScope = new ParentScope(loadedScriptsModifiedTimes, functionsInScope);

                Dictionary<string, object> data = new Dictionary<string, object>();
                data["Scripts"] = scriptsToEval;
                data["Functions"] = functionsInScope.Keys;

                _SubProcess.CreateParentScope(parentScope.ParentScopeId, Thread.CurrentThread.ManagedThreadId, data);
                return parentScope;
            }
            finally
            {
                FileHandlerFactoryLocator.SessionManagerHandler.EndSession(ownerSession.SessionId);
            }
        }

        /// <summary>
        /// Returns the types that have static functions to assist with the given FileHandler based on its type 
        /// </summary>
        /// <param name="fileContainer">
        /// A <see cref="IFileContainer"/>
        /// </param>
        /// <returns>
        /// A <see cref="IEnumerable"/>
        /// </returns>
        private static IEnumerable<Type> GetTypesThatHaveJavascriptFunctions(string fileType)
        {
            yield return typeof(JavascriptFunctions);

            if ("database" == fileType)
                yield return typeof(JavascriptDatabaseFunctions);
        }

        /// <summary>
        /// Used internally for server-side Javascript
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="assignToVariable"></param>
        /// <returns></returns>
        public string GetJavascriptWrapperForBase(string assignToVariable, Set<Type> webHandlerTypes)
        {
            List<string> javascriptMethods =
                FileHandlerFactoryLocator.WebServer.JavascriptWebAccessCodeGenerator.GenerateWrapper(webHandlerTypes);

            string javascriptToReturn = StringGenerator.GenerateSeperatedList(javascriptMethods, ",\n");

            // Replace some key constants
            javascriptToReturn = javascriptToReturn.Replace("{0}", "' + fileMetadata.fullpath + '");
            javascriptToReturn = javascriptToReturn.Replace("{1}", "' + fileMetadata.filename + '");

            javascriptToReturn = javascriptToReturn.Replace("{4}", "true");

            // Enclose the functions with { .... }
            javascriptToReturn = "{\n" + javascriptToReturn + "\n}";

            javascriptToReturn = string.Format("var {0} = {1};", assignToVariable, javascriptToReturn);

            return javascriptToReturn;
        }
    }
}
