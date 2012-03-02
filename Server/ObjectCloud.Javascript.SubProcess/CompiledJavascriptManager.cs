// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;

using Common.Logging;
using JsonFx.Json;

using ObjectCloud.Common;
using ObjectCloud.Common.Threading;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Javascript;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Javascript.SubProcess
{
    /// <summary>
    /// Manages compiled Javascript
    /// </summary>
    public class CompiledJavascriptManager : ICompiledJavascriptManager
    {
        private static ILog log = LogManager.GetLogger<CompiledJavascriptManager>();

        public CompiledJavascriptManager(FileHandlerFactoryLocator fileHandlerFactoryLocator)
        {
            FileHandlerFactoryLocator = fileHandlerFactoryLocator;
        }

        private FileHandlerFactoryLocator FileHandlerFactoryLocator;

        /// <summary>
        /// The cache for compiled javascript
        /// </summary>
        private IDirectoryHandler CompiledJavascriptCache
        {
            get
            {
                if (null == _CompiledJavascriptCache)
                    _CompiledJavascriptCache = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile("/System/CompiledJavascriptCache").CastFileHandler<IDirectoryHandler>();

                return _CompiledJavascriptCache;
            }
        }
        private IDirectoryHandler _CompiledJavascriptCache = null;

        /// <summary>
        /// Cache of pre-calculated scope infos
        /// </summary>
        private Dictionary<IFileId, ScopeInfo> ScopeInfoCache = new Dictionary<IFileId, ScopeInfo>();

        /// <summary>
        /// Returns all of the script IDs for the class
        /// </summary>
        /// <param name="javascriptClass">The text handler that has the class's javascript</param>
        /// <param name="subProcess">The process that must have the given script loaded</param>
        /// <returns></returns>
        public ScopeInfo GetScopeInfoForClass(ITextHandler javascriptClass, SubProcess subProcess)
        {
            DateTime javascriptLastModified = javascriptClass.FileContainer.LastModified;

            ScopeInfo toReturn = null;
            using (TimedLock.Lock(ScopeInfoCache))
                ScopeInfoCache.TryGetValue(javascriptClass.FileContainer.FileId, out toReturn);

            if (null != toReturn)
                if (toReturn.JavascriptLastModified == javascriptLastModified)
                {
                    using (TimedLock.Lock(PrecompiledScriptDataByID))
                        foreach (int scriptID in toReturn.ScriptsAndIDsToBuildScope)
                            subProcess.LoadCompiled(
                                Thread.CurrentThread.ManagedThreadId,
                                PrecompiledScriptDataByID[scriptID],
                                scriptID);

                    return toReturn;
                }

            string javascript = javascriptClass.ReadAll();
            string fileType = null;
            List<int> scriptIDsToBuildScope = new List<int>();

            ISession ownerSession = FileHandlerFactoryLocator.SessionManagerHandler.CreateSession();

            try
            {
                ownerSession.Login(javascriptClass.FileContainer.Owner);

                IWebConnection ownerWebConnection = new BlockingShellWebConnection(
                    FileHandlerFactoryLocator.WebServer,
                    ownerSession,
                    javascriptClass.FileContainer.FullPath,
                    null,
                    null,
                    new CookiesFromBrowser(),
                    CallingFrom.Web,
                    WebMethod.GET);

                IEnumerable<ScriptAndMD5> dependantScriptsAndMD5s = FileHandlerFactoryLocator.WebServer.WebComponentResolver.DetermineDependantScripts(
                    GetRequiredScriptURLs(javascriptClass, out fileType),
                    ownerWebConnection);

                // Load static methods that are passed into the Javascript environment as-is
                Dictionary<string, MethodInfo> functionsInScope = SubProcessFactory.GetFunctionsForFileType(fileType);

                // Load all dependant scripts
                foreach (ScriptAndMD5 dependantScript in dependantScriptsAndMD5s)
                {
                    int scriptID = GetScriptID(
                        dependantScript.ScriptName + "___" + ownerSession.User.Identity,
                        dependantScript.MD5,
                        dependantScript.Script,
                        subProcess);

                    scriptIDsToBuildScope.Add(scriptID);
                }

                // Construct Javascript to shell to the "base" webHandler
                Set<Type> webHandlerTypes = new Set<Type>(FileHandlerFactoryLocator.WebHandlerPlugins);
                if (null != fileType)
                    webHandlerTypes.Add(FileHandlerFactoryLocator.WebHandlerClasses[fileType]);

                string baseWrapper = GetJavascriptWrapperForBase("base", webHandlerTypes);
                scriptIDsToBuildScope.Add(
                    GetScriptID(
                        javascriptClass.FileContainer.FullPath + "___" + "serversideBaseWrapper",
                        StringParser.GenerateMD5String(baseWrapper),
                        baseWrapper,
                        subProcess));

                // Get the ID for the actual javascript
                scriptIDsToBuildScope.Add(
                    GetScriptID(
                        javascriptClass.FileContainer.FullPath,
                        StringParser.GenerateMD5String(javascript),
                        javascript,
                        subProcess));
                
                // Add a little shunt to return information about the options
                scriptIDsToBuildScope.Add(
                    GetScriptID(
                        "____scopeshunt",
                        "xxx",
                        "\nif (this.options) options; else null;",
                        subProcess));

                toReturn = new ScopeInfo(
                    javascriptLastModified,
                    functionsInScope,
                    scriptIDsToBuildScope);

                using (TimedLock.Lock(ScopeInfoCache))
                    ScopeInfoCache[javascriptClass.FileContainer.FileId] = toReturn;

                return toReturn;
            }
            finally
            {
                FileHandlerFactoryLocator.SessionManagerHandler.EndSession(ownerSession.SessionId);
            }
        }

        /// <summary>
        /// Returns the script URLs that this script depends on
        /// </summary>
        /// <param name="javascriptClass"></param>
        /// <returns></returns>
        public IEnumerable<string> GetRequiredScriptURLs(ITextHandler javascriptClass, out string fileType)
        {
            fileType = null;
            List<string> toReturn = new List<string>();

            toReturn.Add("/API/AJAX_serverside.js");

            foreach (string line in javascriptClass.ReadLines())
            {
                // find file type
                if (line.Trim().StartsWith("// FileType:"))
                    fileType = line.Substring(12).Trim();

                // Find dependant scripts
                else if (line.Trim().StartsWith("// Scripts:"))
                    foreach (string script in line.Substring(11).Split(','))
                        toReturn.Add(script.Trim());
            }

            return toReturn;
        }

        /// <summary>
        /// All of the pre-compiled scripts by ID
        /// </summary>
        private Dictionary<int, object> PrecompiledScriptDataByID = new Dictionary<int, object>();

        /// <summary>
        /// Returns the script ID for the named script
        /// </summary>
        /// <param name="scriptNameHex">The name of the script.  The caller must ensure that this is unique</param>
        /// <param name="recompileIfOlderThen">If the pre-compiled script is older then this date, then it's recompiled</param>
        /// <param name="loadScript">Delegate used to load the script</param>
        /// <param name="subProcess">The sub process to compile or load the script in</param>
        /// <returns></returns>
        public int GetScriptID(string scriptName, string md5, string script, ISubProcess subProcess)
        {
            // Unfortunately, all of this data won't fit in a name-value-pairs file
            // This is a quick-and-dirty way to keep filenames valid
            string scriptNameHex = StringGenerator.ToHexString(Encoding.Unicode.GetBytes(scriptName));

            string storedJSONobject = null;
            using (TimedLock.Lock(CompiledJavascriptCache))
                if (CompiledJavascriptCache.IsFilePresent(scriptNameHex))
                    storedJSONobject = CompiledJavascriptCache.OpenFile(scriptNameHex).CastFileHandler<ITextHandler>().ReadAll();

            Dictionary<string, object> precompiled = null;
            if (null != storedJSONobject)
                if (storedJSONobject.Length > 0)
                {
                    precompiled = JsonReader.Deserialize<Dictionary<string, object>>(storedJSONobject);

                    if (md5 != precompiled["MD5"].ToString())
                        precompiled = null;
                }

            int scriptID;
            if (null == precompiled)
            {
                // compile
                scriptID = script.GetHashCode();

                DateTime start = DateTime.UtcNow;
                if (log.IsDebugEnabled)
                    log.Debug("Compiling " + scriptName);

                object data;
                try
                {
                    data = subProcess.Compile(Thread.CurrentThread.ManagedThreadId, script, scriptID);
                }
                catch (Exception e)
                {
                    log.ErrorFormat("Exception compiling {0}\n{1}", e, scriptName, script);
                    throw e;
                }

                // If another thread compiled the script, we'll get back null
                if (null != data)
                {
                    log.InfoFormat("Compiling {0} took {1}", scriptName, DateTime.UtcNow - start);

                    precompiled = new Dictionary<string, object>();
                    precompiled["ScriptID"] = scriptID;
                    precompiled["MD5"] = md5;
                    precompiled["Data"] = data;

                    using (TimedLock.Lock(PrecompiledScriptDataByID))
                        PrecompiledScriptDataByID[scriptID] = data;

                    storedJSONobject = JsonWriter.Serialize(precompiled);
                    using (TimedLock.Lock(CompiledJavascriptCache))
                        if (CompiledJavascriptCache.IsFilePresent(scriptNameHex))
                            CompiledJavascriptCache.OpenFile(scriptNameHex).CastFileHandler<ITextHandler>().WriteAll(null, storedJSONobject);
                        else
                            ((ITextHandler)CompiledJavascriptCache.CreateFile(scriptNameHex, "text", null)).WriteAll(null, storedJSONobject);
                }
            }
            else
            {
                // load
                scriptID = Convert.ToInt32(precompiled["ScriptID"]);

                object data = precompiled["Data"];
                using (TimedLock.Lock(PrecompiledScriptDataByID))
                    PrecompiledScriptDataByID[scriptID] = data;

                log.Info("Loading " + scriptName);

                subProcess.LoadCompiled(Thread.CurrentThread.ManagedThreadId, data, scriptID);
            }

            return scriptID;
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
