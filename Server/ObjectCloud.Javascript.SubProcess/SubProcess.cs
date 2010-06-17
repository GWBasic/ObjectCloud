// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
    public class SubProcess : System.Runtime.ConstrainedExecution.CriticalFinalizerObject, IDisposable, ISubProcess
    {
        static ILog log = LogManager.GetLogger<SubProcess>();

        /// <summary>
        /// Whenever a sub process is started, its ID must be sent to this sub stream
        /// </summary>
        static StreamWriter SubProcessIdWriteStream = null;

        /// <summary>
        /// The sub processes
        /// </summary>
        static Set<Process> SubProcesses = new Set<Process>();

        static SubProcess()
        {
            StartProcessKiller();
        }

        /// <summary>
        /// Helper to start the process killer
        /// </summary>
        private static void StartProcessKiller()
        {
            try
            {
                Process pkp = new Process();
                pkp.StartInfo = new ProcessStartInfo("ProcessKiller.exe", Process.GetCurrentProcess().Id.ToString());
                pkp.StartInfo.RedirectStandardInput = true;
                pkp.StartInfo.UseShellExecute = false;

                pkp.EnableRaisingEvents = true;
                pkp.Exited += new EventHandler(pkp_Exited);

                if (!pkp.Start())
                {
                    Exception e = new JavascriptException("Could not start sub process");
                    log.Error("Error starting Process Killer sub process", e);

                    throw e;
                }

                log.Info("Process Killer started, parent process id (" + Process.GetCurrentProcess().Id.ToString() + "): " + pkp.ToString());

                SubProcessIdWriteStream = pkp.StandardInput;

                Set<Process> subProcesses;
                using (TimedLock.Lock(SubProcesses))
                    subProcesses = new Set<Process>(SubProcesses);

                using (TimedLock.Lock(SubProcessIdWriteStream))
                    foreach (Process subProcess in subProcesses)
                        SubProcessIdWriteStream.WriteLine(subProcess.Id.ToString());

            }
            catch (Exception e)
            {
                log.Error("Error starting process killer", e);
            }
        }

        static void pkp_Exited(object sender, EventArgs e)
        {
            ((Process)sender).Exited -= new EventHandler(pkp_Exited);
            StartProcessKiller();
        }

        private readonly SubProcessFactory SubProcessFactory;

        public SubProcess(SubProcessFactory subProcessFactory)
        {
            SubProcessFactory = subProcessFactory;

            _Process = new Process();
            _Process.StartInfo = new ProcessStartInfo("java", "-cp ." + Path.DirectorySeparatorChar + "js.jar -jar JavascriptProcess.jar " + Process.GetCurrentProcess().Id.ToString());
            _Process.StartInfo.RedirectStandardInput = true;
            _Process.StartInfo.RedirectStandardOutput = true;
            _Process.StartInfo.RedirectStandardError = true;
            _Process.StartInfo.UseShellExecute = false;
            _Process.EnableRaisingEvents = true;
            _Process.Exited += new EventHandler(Process_Exited);

            if (log.IsDebugEnabled)
                log.Debug("Starting Javascript sub process");

            if (!Process.Start())
            {
                Exception e = new JavascriptException("Could not start sub process");
                log.Error("Error starting Javascript sub process");

                throw e;
            }

            log.Info("Javascript sub process started: " + _Process.ToString());

            if (null != SubProcessIdWriteStream)
                using (TimedLock.Lock(SubProcessIdWriteStream))
                    SubProcessIdWriteStream.WriteLine(_Process.Id.ToString());

            JSONSender = new JsonWriter(_Process.StandardInput);

            // Failed attempt to handle processes without Threads
            _Process.ErrorDataReceived += new DataReceivedEventHandler(Process_ErrorDataReceived);
            _Process.BeginErrorReadLine();

            using (TimedLock.Lock(SubProcesses))
                SubProcesses.Add(_Process);
        }

        /// <summary>
        /// Set to true if the sub process terminates abnormally; indicating to owners that they need to take actions to recreate their scopes
        /// </summary>
        bool Aborted = false;

        /// <summary>
        /// True if the sub process is still alive, false otherwise
        /// </summary>
        public bool Alive
        {
            get { return (!Aborted) & (!Disposed); }
        }

        /// <summary>
        /// Thrown when an attempt is made to use an aborted Javascript sub-process; usage of long-lived scopes should handle this exception
        /// </summary>
        public class AbortedException : ApplicationException
        {
            internal AbortedException() : base("The javascript sub-process was aborted, thus the scope is no longer available") { }
        }

        void Process_Exited(object sender, EventArgs e)
        {
            if (!Disposed)
                Aborted = true;

			try
            {
                ((Process)sender).Exited -= new EventHandler(Process_Exited);
                using (TimedLock.Lock(SubProcesses))
                    SubProcesses.Remove(((Process)sender));

                if (!Disposed)
                    Dispose();
            }
            catch (Exception ex)
            {
                log.Error("Exception when a sub process exited", ex);
            }
        }

        /// <summary>
        /// Handles incoming errors 
        /// </summary>
        /// <param name="sender">
        /// A <see cref="System.Object"/>
        /// </param>
        /// <param name="e">
        /// A <see cref="DataReceivedEventArgs"/>
        /// </param>
        void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            Dictionary<string, object> subProcessError;

            try
            {
                subProcessError = JsonReader.Deserialize<Dictionary<string, object>>(e.Data);
            }
            catch
            {
                log.Error("Javascript sub process error: " + e.Data);
                return;
            }

            LogSubProcessError(subProcessError);
        }

        /// <summary>
        /// Assists in logging a sub process error
        /// </summary>
        /// <param name="subProcessError"></param>
        private static void LogSubProcessError(Dictionary<string, object> subProcessError)
        {
            StringBuilder errorStringBuilder = new StringBuilder("Javascript sub process error");

            object message;
            if (subProcessError.TryGetValue("Message", out message))
                errorStringBuilder.Append("\nMessage: " + message.ToString());

            object stackTrace;
            if (subProcessError.TryGetValue("StackTrace", out stackTrace))
                errorStringBuilder.Append("\nStack Trace: " + stackTrace.ToString());

            log.Error(errorStringBuilder.ToString());
        }

        /// <summary>
        /// The actual process
        /// </summary>
        public Process Process
        {
            get { return _Process; }
        }
        Process _Process;

        JsonWriter JSONSender;

        /// <summary>
        /// Synchronizes sending data to the sub process
        /// </summary>
        private object SendKey = new object();

        /// <summary>
        /// Callback for when a parent function is called
        /// </summary>
        Dictionary<int, CallParentFunctionDelegate> ParentFunctionDelegatesByScopeId = new Dictionary<int, CallParentFunctionDelegate>();

        /// <summary>
        /// Registers a callback for when the javascript in the scope calls a parent function
        /// </summary>
        /// <param name="scopeId"></param>
        /// <param name="callParentFunctionDelegate"></param>
        public void RegisterParentFunctionDelegate(int scopeId, CallParentFunctionDelegate parentFunctionDelegate)
        {
            ParentFunctionDelegatesByScopeId[scopeId] = parentFunctionDelegate;
        }

        /// <summary>
        /// ScriptIDs that are already loaded
        /// </summary>
        private Set<int> LoadedScriptIDs = new Set<int>();

        /// <summary>
        /// ScriptIDs that currently are being loaded and thus another user must block
        /// </summary>
        private Dictionary<int, object> LoadingScriptIDs = new Dictionary<int, object>();

        /// <summary>
        /// Compiles the script
        /// </summary>
        /// <param name="threadID"></param>
        /// <param name="script"></param>
        /// <param name="scriptID"></param>
        /// <returns>An object that can be used to re-load the script, or null if another thread happened to compile at the same time</returns>
        public object Compile(object threadID, string script, int scriptID)
        {
            CheckIfAbortedOrDisposed();

            object loadingMonitor;
            if (BlockWhileAnotherThreadIsLoading(scriptID, out loadingMonitor))
                return null;

            try
            {
                Dictionary<string, object> data = new Dictionary<string, object>();
                data["Script"] = script;
                data["ScriptID"] = scriptID;

                Dictionary<string, object> command = CreateCommand(threadID, "Compile", data);
                Dictionary<string, object> dataToReturn = SendCommandAndHandleResponse(command);

                using (TimedLock.Lock(LoadedScriptIDs))
                    LoadedScriptIDs.Add(scriptID);

                return dataToReturn["CompiledScript"];
            }
            finally
            {
                using (TimedLock.Lock(LoadingScriptIDs))
                    LoadingScriptIDs.Remove(scriptID);

                lock(loadingMonitor)
                    Monitor.Pulse(loadingMonitor);
            }
        }

        /// <summary>
        /// Blocks if another thread is loading or compiling the same scriptID, returns true if another thread loaded or compiled the same script ID
        /// </summary>
        /// <param name="scriptID"></param>
        /// <param name="loadingMonitor"></param>
        /// <param name="otherThreadLoading"></param>
        private bool BlockWhileAnotherThreadIsLoading(int scriptID, out object loadingMonitor)
        {
            bool otherThreadLoading;

            using (TimedLock.Lock(LoadingScriptIDs))
            {
                otherThreadLoading = LoadingScriptIDs.TryGetValue(scriptID, out loadingMonitor);

                if (!otherThreadLoading)
                {
                    loadingMonitor = new object();
                    LoadingScriptIDs[scriptID] = loadingMonitor;
                }
            }

            // If another thread is loading, monitor it, but put a timeout in case the other thread
            // removed the token when we weren't looking
            bool doneWaiting;
            if (otherThreadLoading)
                do
                {
                    lock (loadingMonitor)
                        Monitor.Wait(loadingMonitor, 250);

                    using (TimedLock.Lock(LoadedScriptIDs))
                        doneWaiting = LoadedScriptIDs.Contains(scriptID);

                } while (doneWaiting);

            return otherThreadLoading;
        }

        /// <summary>
        /// Loads a pre-compiled script from ID
        /// </summary>
        /// <param name="threadID"></param>
        /// <param name="preCompiled"></param>
        /// <param name="scriptID"></param>
        public void LoadCompiled(object threadID, object preCompiled, int scriptID)
        {
            using (TimedLock.Lock(LoadedScriptIDs))
                if (LoadedScriptIDs.Contains(scriptID))
                    return;

            object loadingMonitor;
            if (BlockWhileAnotherThreadIsLoading(scriptID, out loadingMonitor))
                return;

            try
            {
                Dictionary<string, object> data = new Dictionary<string, object>();
                data["CompiledScript"] = preCompiled;
                data["ScriptID"] = scriptID;

                Dictionary<string, object> command = CreateCommand(threadID, "LoadCompiled", data);
                //Dictionary<string, object> dataToReturn = 
                SendCommandAndHandleResponse(command);

                using (TimedLock.Lock(LoadedScriptIDs))
                    LoadedScriptIDs.Add(scriptID);
            }
            finally
            {
                using (TimedLock.Lock(LoadingScriptIDs))
                    LoadingScriptIDs.Remove(scriptID);

                lock (loadingMonitor)
                    Monitor.Pulse(loadingMonitor);
            }
        }

        /// <summary>
        /// Creates a scope
        /// </summary>
        /// <param name="scopeId"></param>
        /// <param name="threadID"></param>
        /// <param name="data">The data that is placed into the scope</param>
        /// <returns></returns>
        public EvalScopeResults EvalScope(
            int scopeId,
            object threadID,
            Dictionary<string, object> metadata,
            IEnumerable scriptIDsAndScriptsEnum,
            IEnumerable<string> functionsToAdd,
            bool returnFunctions)
        {
            CheckIfAbortedOrDisposed();

            Dictionary<string, object> data = new Dictionary<string, object>();
            if (null != metadata)
                data["Data"] = metadata;

            ArrayList scriptIDsAndScripts = new ArrayList();
            foreach (object scriptOrID in scriptIDsAndScriptsEnum)
                scriptIDsAndScripts.Add(scriptOrID);

            data["Scripts"] = scriptIDsAndScripts;

            if (null != functionsToAdd)
                data["Functions"] = new List<string>(functionsToAdd);

            if (returnFunctions)
                data["ReturnFunctions"] = true;

            Dictionary<string, object> command = CreateCommand(scopeId, threadID, "EvalScope", data);

            Dictionary<string, object> dataToReturn = SendCommandAndHandleResponse(command, scopeId);

            EvalScopeResults toReturn = new EvalScopeResults();

            List<object> results = new List<object>();
            foreach (object result in (IEnumerable)dataToReturn["Results"])
                results.Add(result);

            toReturn.Results = results;

            if (returnFunctions)
            {
                object functionsObj = dataToReturn["Functions"];
                Dictionary<string, CreateScopeFunctionInfo> functionsToReturn = new Dictionary<string, CreateScopeFunctionInfo>();

                foreach (KeyValuePair<string, object> functionKVP in (IEnumerable<KeyValuePair<string, object>>)functionsObj)
                {
                    CreateScopeFunctionInfo functionInfo = new CreateScopeFunctionInfo();
                    Dictionary<string, object> value = (Dictionary<string, object>)functionKVP.Value;

                    functionInfo.Properties = (Dictionary<string, object>)value["Properties"];

                    object arguments;
                    if (value.TryGetValue("Arguments", out arguments))
                        functionInfo.Arguments = Enumerable<string>.Cast((IEnumerable)arguments);

                    functionsToReturn[functionKVP.Key] = functionInfo;
                }

                toReturn.Functions = functionsToReturn;
            }

            return toReturn;
        }

        /// <summary>
        /// Throws an exception if the sub process is aborted or disposed
        /// </summary>
        private void CheckIfAbortedOrDisposed()
        {
            if (Aborted)
                throw new AbortedException();

            if (Disposed)
                throw new ObjectDisposedException("This Javascript sub process is disposed and can no longer be used");
        }

        /// <summary>
        /// Disposes a scope, removing all references to the call parent function callback
        /// </summary>
        /// <param name="scopeId"></param>
        /// <param name="callParentFunctionDelegate"></param>
        public void DisposeScope(int scopeId, object threadID)
        {
            // If the sub process was aborted or disposed, then just ignore further cleanup requests
            if (Aborted || Disposed)
                return;

            ParentFunctionDelegatesByScopeId.Remove(scopeId);

            Dictionary<string, object> command = CreateCommand(scopeId, threadID, "DisposeScope", new Dictionary<string, object>());

            using (TimedLock.Lock(SendKey))
                JSONSender.Write(command);
        }

        /// <summary>
        /// Calls a function in the sub-process
        /// </summary>
        /// <param name="scopeID"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        /// <exception cref="ObjectDisposedException">Thrown if the sub process was disposed through normal execution.</exception>
        /// <exception cref="AbortedException">Thrown if the sub process aborted anormally.  Callers should recover from this error condition</exception>
        public object CallFunctionInScope(int scopeId, object threadID, string functionName, IEnumerable arguments)
        {
            CheckIfAbortedOrDisposed();

            Dictionary<string, object> data = new Dictionary<string, object>();
            data["FunctionName"] = functionName;
            data["Arguments"] = arguments;

            Dictionary<string, object> command = CreateCommand(scopeId, threadID, "CallFunctionInScope", data);

            Dictionary<string, object> dataToReturn = SendCommandAndHandleResponse(command, scopeId);
            return dataToReturn["Result"];
        }

        /// <summary>
        /// Calls a function in the sub-process
        /// </summary>
        /// <param name="scopeID"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        /// <exception cref="ObjectDisposedException">Thrown if the sub process was disposed through normal execution.</exception>
        /// <exception cref="AbortedException">Thrown if the sub process aborted anormally.  Callers should recover from this error condition</exception>
        public object CallCallback(int scopeId, object threadID, object callbackId, IEnumerable arguments)
        {
            CheckIfAbortedOrDisposed();

            Dictionary<string, object> data = new Dictionary<string, object>();
            data["CallbackId"] = callbackId;
            data["Arguments"] = arguments;

            Dictionary<string, object> command = CreateCommand(scopeId, threadID, "CallCallback", data);

            Dictionary<string, object> dataToReturn = SendCommandAndHandleResponse(command, scopeId);
            return dataToReturn["Result"];
        }

        /// <summary>
        /// Encapsulates callbacks that come from the sub process
        /// </summary>
        public class Callback
        {
            public Callback(SubProcess subProcess, int scopeId, object threadId, object callbackId)
            {
                _SubProcess = subProcess;
                _ScopeId = scopeId;
                _ThreadId = threadId;
                _CallbackId = callbackId;
            }

            public SubProcess SubProcess
            {
                get { return _SubProcess; }
            }
            private readonly SubProcess _SubProcess;

            public int ScopeId
            {
                get { return _ScopeId; }
            }
            private readonly int _ScopeId;

            public object ThreadId
            {
                get { return _ThreadId; }
            }
            private readonly object _ThreadId;

            public object CallbackId
            {
                get { return _CallbackId; }
            }
            private readonly object _CallbackId;

            public object Call(IEnumerable<object> arguments)
            {
                try
                {
                    return SubProcess.CallCallback(ScopeId, ThreadId, CallbackId, arguments);
                }
                catch (JavascriptException je)
                {
                    // If there is an exception creating the scope, log some important information and then re-throw
                    log.ErrorFormat("Exception in Javascript calling callback", je);

                    throw;
                }
            }
        }

        /// <summary>
        /// Helper to create a command
        /// </summary>
        /// <param name="scopeId"></param>
        /// <param name="threadID"></param>
        /// <param name="command"></param>
        /// <param name="data"></param>
        private static Dictionary<string, object> CreateCommand(
            object threadID, string commandName, Dictionary<string, object> data)
        {
            Dictionary<string, object> command;
            command = new Dictionary<string, object>();
            command["ThreadID"] = threadID;
            command["Command"] = commandName;
            command["Data"] = data;

            return command;
        }

        /// <summary>
        /// Helper to create a command
        /// </summary>
        /// <param name="scopeId"></param>
        /// <param name="threadID"></param>
        /// <param name="command"></param>
        /// <param name="data"></param>
        private static Dictionary<string, object> CreateCommand(
            int scopeId, object threadID, string commandName, Dictionary<string, object> data)
        {
            Dictionary<string, object> command;
            command = new Dictionary<string, object>();
            command["ScopeID"] = scopeId;
            command["ThreadID"] = threadID;
            command["Command"] = commandName;
            command["Data"] = data;

            return command;
        }

        /// <summary>
        /// Tracks specific objects while the call static is in Javascript.  Once the callstack leaves Javascript, these objects are forgotten
        /// </summary>
        [ThreadStatic]
        static Dictionary<int, object> TrackedObjects = null;

        /// <summary>
        /// The number of calls that are in Javascript
        /// </summary>
        [ThreadStatic]
        uint NumInJavascriptCalls = 0;

        /// <summary>
        /// Helper callback for timers to kill timed out sub processes
        /// </summary>
        /// <param name="state"></param>
        private void HandleSubProcessTimeout(object state)
        {
            log.Warn("Killing sub-process due to timeout");
            Dispose();
        }

        /// <summary>
        /// Helper to block a thread until the response comes back
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, object> SendCommandAndHandleResponse(object command)
        {
            Dictionary<string, object> inCommand;

            // If the thread waits, spin up a timer kill the process in case it runs too long
            using (new Timer(HandleSubProcessTimeout, null, SubProcessFactory.CompileTimeout, 0))
                inCommand = SendCommandAndWaitForResponse(command);

            Dictionary<string, object> dataToReturn = (Dictionary<string, object>)inCommand["Data"];

            object exceptionFromJavascript;
            if (dataToReturn.TryGetValue("Exception", out exceptionFromJavascript))
            {
                if (exceptionFromJavascript is Dictionary<string, object>)
                {
                    object parentObjectId;
                    if (((Dictionary<string, object>)exceptionFromJavascript).TryGetValue("ParentObjectId", out parentObjectId))
                        if (TrackedObjects.TryGetValue(Convert.ToInt32(parentObjectId), out exceptionFromJavascript))
                            if (exceptionFromJavascript is Exception)
                                throw (Exception)exceptionFromJavascript;
                }

                throw new JavascriptException(JsonWriter.Serialize(exceptionFromJavascript));
            }

            return dataToReturn;
        }

        /// <summary>
        /// Helper to block a thread until the response comes back
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, object> SendCommandAndHandleResponse(object command, int scopeId)
        {
            if (null == TrackedObjects)
                TrackedObjects = new Dictionary<int, object>();

            NumInJavascriptCalls++;

            try
            {
                do
                {
                    Dictionary<string, object> inCommand;

                    // If the thread waits, spin up a timer kill the process in case it runs too long
                    using (new Timer(HandleSubProcessTimeout, null, SubProcessFactory.ExecuteTimeout, 0))
                        inCommand = SendCommandAndWaitForResponse(command);

                    Dictionary<string, object> dataToReturn = (Dictionary<string, object>)inCommand["Data"];

                    // If the response is for calling a parent function in this process, then call it, else just return the results
                    if ("CallParentFunction".Equals(inCommand["Command"]))
                    {
                        string functionName = dataToReturn["FunctionName"].ToString();
                        object[] arguments = new List<object>((IEnumerable<object>)dataToReturn["Arguments"]).ToArray();
                        object threadId = inCommand["ThreadID"];

                        // Convert callbacks to usable objects
                        for (int argCtr = 0; argCtr < arguments.Length; argCtr++)
                            if (arguments[argCtr] is Dictionary<string, object>)
                            {
                                Dictionary<string, object> argument = (Dictionary<string, object>)arguments[argCtr];
                                object isCallback;
                                if (argument.TryGetValue("Callback", out isCallback))
                                    if (isCallback is bool)
                                        if (true == (bool)isCallback)
                                        {
                                            object callbackId;
                                            if (argument.TryGetValue("CallbackID", out callbackId))
                                                arguments[argCtr] = new Callback(this, scopeId, threadId, callbackId);
                                        }
                            }

                        Dictionary<string, object> outData = new Dictionary<string, object>();
                        Dictionary<string, object> outCommand = CreateCommand(scopeId, threadId, "RespondCallParentFunction", outData);

                        try
                        {
                            object parentFunctionDataToReturn = this.ParentFunctionDelegatesByScopeId[scopeId](
                                functionName,
                                threadId,
                                arguments);

                            if (parentFunctionDataToReturn is StringToEval)
                            {
                                StringToEval stringToEval = (StringToEval)parentFunctionDataToReturn;
                                outData["Eval"] = stringToEval.ToEval;

                                if (null != stringToEval.CacheId)
                                    outData["CacheID"] = stringToEval.CacheId;
                            }
                            else if (parentFunctionDataToReturn is ScriptToRun)
                            {
                                ScriptToRun scriptToRun = (ScriptToRun)parentFunctionDataToReturn;
                                outData["Eval"] = scriptToRun.ScriptID;

                                if (null != scriptToRun.CacheId)
                                    outData["CacheID"] = scriptToRun.CacheId;
                            }

                            else if (parentFunctionDataToReturn is CachedObjectId)
                                outData["CacheID"] = ((CachedObjectId)parentFunctionDataToReturn).CacheId;

                            else if (parentFunctionDataToReturn != Undefined.Value)
                            {
                                if (null != parentFunctionDataToReturn)
                                {
                                    string nameSpace = parentFunctionDataToReturn.GetType().Namespace;

                                    if (!(nameSpace.StartsWith("System.") || nameSpace.Equals("System")))
                                    {
                                        Dictionary<string, object> jsoned = JsonReader.Deserialize<Dictionary<string, object>>(
                                            JsonWriter.Serialize(parentFunctionDataToReturn));

                                        int parentObjectId = SRandom.Next();
                                        jsoned["ParentObjectId"] = parentObjectId;

                                        TrackedObjects[parentObjectId] = parentFunctionDataToReturn;
                                        parentFunctionDataToReturn = jsoned;
                                    }
                                }

                                outData["Result"] = parentFunctionDataToReturn;
                            }
                        }
                        catch (Exception e)
                        {
                            Dictionary<string, object> jsoned = JsonReader.Deserialize<Dictionary<string, object>>(
                                JsonWriter.Serialize(e));

                            int parentObjectId = SRandom.Next();
                            jsoned["ParentObjectId"] = parentObjectId;

                            TrackedObjects[parentObjectId] = e;

                            outData["Exception"] = jsoned;
                        }

                        //using (TimedLock.Lock(SendKey))
                            //JSONSender.Write(outCommand);
                        command = outCommand;
                    }
                    else
                    {
                        object exceptionFromJavascript;
                        if (dataToReturn.TryGetValue("Exception", out exceptionFromJavascript))
                        {
                            if (exceptionFromJavascript is Dictionary<string, object>)
                            {
                                object parentObjectId;
                                if (((Dictionary<string, object>)exceptionFromJavascript).TryGetValue("ParentObjectId", out parentObjectId))
                                    if (TrackedObjects.TryGetValue(Convert.ToInt32(parentObjectId), out exceptionFromJavascript))
                                        if (exceptionFromJavascript is Exception)
                                            throw (Exception)exceptionFromJavascript;
                            }

                            if (exceptionFromJavascript is string)
                                throw new JavascriptException((string)exceptionFromJavascript);
                            else
                                throw new JavascriptException(JsonWriter.Serialize(exceptionFromJavascript));
                        }

                        object result;
                        if (dataToReturn.TryGetValue("Result", out result))
                        {
                            if (result is Dictionary<string, object>)
                            {
                                object parentObjectId;
                                if (((Dictionary<string, object>)result).TryGetValue("ParentObjectId", out parentObjectId))
                                    if (TrackedObjects.TryGetValue(Convert.ToInt32(parentObjectId), out result))
                                        dataToReturn["Result"] = result;
                            }
                        }
                        else if (dataToReturn.TryGetValue("Results", out result))
                        {
                            if (result is IEnumerable)
                            {
                                List<object> newResults = new List<object>();

                                foreach (object subResult in (IEnumerable)result)
                                    if (subResult is Dictionary<string, object>)
                                    {
                                        object parentObjectId;
                                        if (((Dictionary<string, object>)subResult).TryGetValue("ParentObjectId", out parentObjectId))
                                        {
                                            object tracked;
                                            if (TrackedObjects.TryGetValue(Convert.ToInt32(parentObjectId), out tracked))
                                                dataToReturn["Result"] = tracked;
                                            else
                                                newResults.Add(subResult);
                                        }
                                        else
                                            newResults.Add(subResult);
                                    }
                                    else
                                        newResults.Add(subResult);

                                dataToReturn["Results"] = newResults;
                            }
                        }
                        else
                            dataToReturn["Result"] = Undefined.Value;

                        return dataToReturn;
                    }
                }
                while (true);
            }
            finally
            {
                NumInJavascriptCalls--;

                if (0 == NumInJavascriptCalls)
                    TrackedObjects.Clear();
            }
        }

        /*// <summary>
        /// Provides syncronization when waiting for a response
        /// </summary>
        private object RespondKey = new object();

        /// <summary>
        /// The most recent command returned from the sub process 
        /// </summary>
        Dictionary<string, object> InCommand = null;

        /// <summary>
        /// The thread that has a result waiting for it 
        /// </summary>
        int InCommandThreadId;*/

        /// <summary>
        /// Syncronizes the sub process's output stream and returns the appropriate response
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, object> SendCommandAndWaitForResponse(object command)
        {
            // Send the command
            using (TimedLock.Lock(SendKey))
            {
                JSONSender.Write(command);

                string inCommandString;
                do
                    inCommandString = _Process.StandardOutput.ReadLine();
                while (null == inCommandString);

                Dictionary<string, object> toReturn = JsonReader.Deserialize<Dictionary<string, object>>(inCommandString);

                // If the sub process had a recoverable error, just push it up the stack
                if (toReturn.ContainsKey("StackTrace"))
                {
                    LogSubProcessError(toReturn);
                    throw new JavascriptException(toReturn["Message"].ToString());
                }

                return toReturn;
            }

            /*int threadID = Thread.CurrentThread.ManagedThreadId;

            do
            {
                // If there is a waiting result and it's for this thread, return it
                if (null != InCommand)
                    if (threadID == InCommandThreadId)
                    {
                        Dictionary<string, object> toReturn = InCommand;
                        InCommand = null;

                        // If the sub process had a recoverable error, just push it up the stack
                        if (toReturn.ContainsKey("StackTrace"))
                        {
                            LogSubProcessError(toReturn);
                            throw new JavascriptException(toReturn["Message"].ToString());
                        }

                        return toReturn;
                    }

                // Only 1 thread can read from the sub process at a time
                // all threads spin while waiting for a response
                if (null == InCommand)
                {
                    lock (RespondKey)
                        if (null == InCommand)
                        {
                            // else, if there isn't a waiting response that came in on another thread, and if there aren't waiting responses, wait for a response
                            //if (Process.StandardOutput.EndOfStream || Process.HasExited)
                            if (Process.HasExited)
                                throw new JavascriptException("The sub process has exited");

                            string inCommandString = _Process.StandardOutput.ReadLine();

                            if (null == inCommandString)
                                log.Warn("Null recieved from sub process");
                            else
                            {
                                InCommand = JsonReader.Deserialize<Dictionary<string, object>>(inCommandString);
                                InCommandThreadId = Convert.ToInt32(InCommand["ThreadID"]);
                            }
                        }
                }
                else
                    Thread.Sleep(0);

            } while (true);*/
        }

        /// <summary>
        /// Indicates to the caller that the returned value is Javascript that must be evaled
        /// </summary>
        public struct StringToEval
        {
            public StringToEval(string toEval)
            {
                _ToEval = toEval;
                _CacheId = null;
            }

            public StringToEval(string toEval, object cacheId)
            {
                _ToEval = toEval;
                _CacheId = cacheId;
            }

            public string ToEval
            {
                get { return _ToEval; }
            }
            private readonly string _ToEval;

            public object CacheId
            {
                get { return _CacheId; }
            }
            private readonly object _CacheId;
        }

        /// <summary>
        /// Indicates to the caller that the returned value comes from a script to run in the scope
        /// </summary>
        public struct ScriptToRun
        {
            public ScriptToRun(int scriptID)
            {
                _ScriptID = scriptID;
                _CacheId = null;
            }

            public ScriptToRun(int scriptID, object cacheId)
            {
                _ScriptID = scriptID;
                _CacheId = cacheId;
            }

            public int ScriptID
            {
                get { return _ScriptID; }
            }
            private readonly int _ScriptID;

            public object CacheId
            {
                get { return _CacheId; }
            }
            private readonly object _CacheId;
        }

        /// <summary>
        /// Indicates to the caller that the returned value is already cached and should be referenced by ID
        /// </summary>
        public struct CachedObjectId
        {
            public CachedObjectId(object cacheId)
            {
                _CacheId = cacheId;
            }

            public object CacheId
            {
                get { return _CacheId; }
            }
            private readonly object _CacheId;
        }

        private bool Disposed = false;

        public void Dispose()
        {
            if (Disposed)
                return;

            if (Aborted)
                return;

            Disposed = true;

            using (TimedLock.Lock(SendKey))
                try
                {
                    JSONSender.Write(new Dictionary<string, object>());
                }
                // Errors trying to gracefully close the subprocess are swallowed
                catch { }

            // Make sure process stops on another thread
            DateTime start = DateTime.UtcNow;
            ThreadPool.QueueUserWorkItem(KillSubprocess, start);

            GC.SuppressFinalize(this);
        }

        private void KillSubprocess(object state)
        {
            try
            {
                if (!_Process.HasExited)
                {
                    DateTime start = (DateTime)state;

                    if (DateTime.UtcNow - start > TimeSpan.FromSeconds(0.25))
                        _Process.Kill();
                    else
                    {
                        Thread.Sleep(25);
                        ThreadPool.QueueUserWorkItem(KillSubprocess, start);
                    }
                }
            }
            catch { }
        }

        ~SubProcess()
        {
            try
            {
                Dispose();
            }
            catch { }
            finally
            {
                try
                {
                    if (!_Process.HasExited)
                        _Process.Kill();
                }
                catch { }
            }
        }
    }
}
