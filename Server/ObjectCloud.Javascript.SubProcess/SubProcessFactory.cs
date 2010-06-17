// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;

using ObjectCloud.Common;
using ObjectCloud.Common.Threading;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Javascript;

namespace ObjectCloud.Javascript.SubProcess
{
    /// <summary>
    /// Simple sub process factory that holds a limited set of sub processes
    /// </summary>
    public class SubProcessFactory : ISubProcessFactory
    {
        /// <summary>
        /// The number of sub processes to create
        /// </summary>
        public int NumSubProcesses
        {
            get { return _NumSubProcesses; }
            set { _NumSubProcesses = value; }
        }
        private int _NumSubProcesses = Environment.ProcessorCount;

        /// <summary>
        /// The amount of time in milliseconds that must elapse before the sub process is killed when compiling
        /// </summary>
        public int CompileTimeout
        {
            get { return _CompileTimeout; }
            set { _CompileTimeout = value; }
        }
        private int _CompileTimeout = 60000;

        /// <summary>
        /// The amount of time in milliseconds that must elapse before the sub process is killed when executing
        /// </summary>
        public int ExecuteTimeout
        {
            get { return _ExecuteTimeout; }
            set { _ExecuteTimeout = value; }
        }
        private int _ExecuteTimeout = 30000;

        /// <summary>
        /// All of the sub processes
        /// </summary>
        private Set<SubProcess> SubProcesses = new Set<SubProcess>();

        public FileHandlerFactoryLocator FileHandlerFactoryLocator
        {
            get { return _FileHandlerFactoryLocator; }
            set 
            {
                _FileHandlerFactoryLocator = value;

                _CompiledJavascriptManager = new CompiledJavascriptManager(value);

                using (TimedLock.Lock(SubProcesses))
                {
                    foreach (SubProcess subProcess in SubProcesses)
                        subProcess.Dispose();

                    SubProcesses.Clear();

                    for (int ctr = 0; ctr < NumSubProcesses; ctr++)
                    {
                        SubProcess subProcess = new SubProcess(this);
                        SubProcesses.Add(subProcess);
                        Queue.Enqueue(subProcess);
                    }
                }
            }
        }
        FileHandlerFactoryLocator _FileHandlerFactoryLocator;

        public CompiledJavascriptManager CompiledJavascriptManager
        {
            get { return _CompiledJavascriptManager; }
        }
        ICompiledJavascriptManager ISubProcessFactory.CompiledJavascriptManager
        {
            get { return _CompiledJavascriptManager; }
        }
        private CompiledJavascriptManager _CompiledJavascriptManager;

		/// <summary>
		/// A queue of sub processes that is rotated through 
		/// </summary>
        private LockFreeQueue<SubProcess> Queue = new LockFreeQueue<SubProcess>();
		
        /// <summary>
        /// Returns a sub-process
        /// </summary>
        /// <param name="javascriptContainer"></param>
        /// <returns></returns>
        public SubProcess GetSubProcess()
        {
            SubProcess toReturn;
            while (!Queue.Dequeue(out toReturn))
                Thread.Sleep(0);

            if (!toReturn.Alive)
            {
                SubProcess newSubProcess = new SubProcess(this);

                using (TimedLock.Lock(SubProcesses))
                {
                    SubProcesses.Remove(toReturn);
                    toReturn = newSubProcess;
                    SubProcesses.Add(toReturn);
                }
            }

            Queue.Enqueue(toReturn);

            return toReturn;
        }

        ISubProcess ISubProcessFactory.GetSubProcess()
        {
            return GetSubProcess();
        }

        ~SubProcessFactory()
        {
            try
            {
                foreach (SubProcess subProcess in SubProcesses)
                    try
                    {
                        subProcess.Dispose();
                    }
                    catch { }
            }
            catch { }
        }

        private int IdCtr = int.MinValue;

        public int GenerateScopeId()
        {
            return Interlocked.Increment(ref IdCtr);
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
        /// Load static methods that are passed into the Javascript environment as-is
        /// </summary>
        /// <param name="fileType"></param>
        /// <returns></returns>
        public static Dictionary<string, MethodInfo> GetFunctionsForFileType(string fileType)
        {
            Dictionary<string, MethodInfo> functionsInScope = new Dictionary<string, MethodInfo>();
            foreach (Type javascriptFunctionsType in GetTypesThatHaveJavascriptFunctions(fileType))
                foreach (MethodInfo method in javascriptFunctionsType.GetMethods(BindingFlags.Static | BindingFlags.Public))
                    functionsInScope[method.Name] = method;

            return functionsInScope;
        }

        public IScopeWrapper GenerateScopeWrapper(
            Dictionary<string, object> metadata,
            IEnumerable scriptsAndIDsToBuildScope,
            IFileContainer fileContainer)
        {
            SubProcess subProcess = GetSubProcess();

            // Prepare to load /API/AJAX_serverside.js
            IFileContainer ajaxDriver = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(
                "/API/AJAX_serverside.js");

            int scriptID = CompiledJavascriptManager.GetScriptID(
                "/API/AJAX_serverside.js",
                ajaxDriver.LastModified.Ticks.ToString(),
                ajaxDriver.CastFileHandler<ITextHandler>().ReadAll(),
                subProcess);

            ArrayList modifiedScriptsAndIDsToBuildScope = new ArrayList();
            modifiedScriptsAndIDsToBuildScope.Add(scriptID);
            foreach (object scriptOrId in scriptsAndIDsToBuildScope)
                modifiedScriptsAndIDsToBuildScope.Add(scriptOrId);

            ScopeInfo scopeInfo = new ScopeInfo(
                DateTime.MinValue, GetFunctionsForFileType(fileContainer.TypeId), modifiedScriptsAndIDsToBuildScope);

            EvalScopeResults evalScopeResults;
            ScopeWrapper toReturn = new ScopeWrapper(
                FileHandlerFactoryLocator,
                subProcess,
                scopeInfo,
                fileContainer,
                CompiledJavascriptManager,
                GenerateScopeId(),
                metadata,
                out evalScopeResults);

            if (null != evalScopeResults)
                if (null != evalScopeResults.Results)
                    evalScopeResults.Results.RemoveAt(0);

            return toReturn;
        }
    }
}