using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

using Common.Logging;
using JsonFx.Json;

using ObjectCloud.Common;

namespace ObjectCloud.Javascript.SubProcess
{
	public class SubProcess : IDisposable
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

        public SubProcess()
        {
            Process = new Process();
            Process.StartInfo = new ProcessStartInfo("java", "-cp ." + Path.DirectorySeparatorChar + "js.jar -jar JavascriptProcess.jar " + Process.GetCurrentProcess().Id.ToString());
            Process.StartInfo.RedirectStandardInput = true;
            Process.StartInfo.RedirectStandardOutput = true;
            Process.StartInfo.RedirectStandardError = true;
            Process.StartInfo.UseShellExecute = false;
            Process.EnableRaisingEvents = true;
            Process.Exited += new EventHandler(Process_Exited);

            if (!Process.Start())
            {
                Exception e = new JavascriptException("Could not start sub process");
                log.Error("Error starting Javascript sub process", e);

                throw e;
            }

            log.Info("Javascript sub process started: " + Process.ToString());

			if (null != SubProcessIdWriteStream)
            		using (TimedLock.Lock(SubProcessIdWriteStream))
                		SubProcessIdWriteStream.WriteLine(Process.Id.ToString());

            JSONSender = new JsonWriter(Process.StandardInput);

            new Thread(new ThreadStart(MonitorForResponses)).Start();
            new Thread(new ThreadStart(MonitorForErrors)).Start();

            using (TimedLock.Lock(SubProcesses))
                SubProcesses.Add(Process);
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
            ((Process)sender).Exited -= new EventHandler(Process_Exited);
            using (TimedLock.Lock(SubProcesses))
                SubProcesses.Remove(((Process)sender));

            if (!Disposed)
            {
                Dispose();
                Aborted = true;
            }
        }

        /// <summary>
        /// Handles incoming responses on a thread
        /// </summary>
        private void MonitorForResponses()
        {
            try
            {
                while (!Process.StandardOutput.EndOfStream)
                {
                    string inCommandString = Process.StandardOutput.ReadLine();
                    Dictionary<string, object> inCommand = JsonReader.Deserialize<Dictionary<string, object>>(inCommandString);

                    object threadID = inCommand["ThreadID"];

                    using (TimedLock.Lock(InCommandsByThreadId))
                        InCommandsByThreadId[threadID] = inCommand;

                    UnblockWaitingThread(threadID, inCommand);
                }
            }
            catch (Exception e)
            {
                log.Error("Error reading from Javascript sub process", e);
                Dispose();
            }
        }

        /// <summary>
        /// Handles incoming errors on a thread
        /// </summary>
        private void MonitorForErrors()
        {
            try
            {
                while (!Process.StandardError.EndOfStream)
                    log.Error("Javascript sub process error: " + Process.StandardError.ReadLine());
            }
            catch (Exception e)
            {
                log.Error("Error reading from Javascript sub process", e);
                Dispose();
            }
        }

        /// <summary>
        /// Unblocks a waiting thread
        /// </summary>
        /// <param name="inCommand"></param>
        private void UnblockWaitingThread(object threadID, Dictionary<string, object> inCommand)
        {
            using (TimedLock.Lock(MonitorObjectsByThreadId))
            {
                object monitorObject;

                if (MonitorObjectsByThreadId.TryGetValue(threadID, out monitorObject))
                    using (TimedLock.Lock(monitorObject))
                        Monitor.Pulse(monitorObject);
                else
                    ThreadPool.QueueUserWorkItem(
                        delegate(object state)
                        {
                            object[] args = (object[])state;
                            UnblockWaitingThread(args[0], (Dictionary<string, object>)args[1]);
                        },
                        new object[] { threadID, inCommand });
            }
        }
		
		Process Process;
		JsonWriter JSONSender;
		
		/// <summary>
		/// Synchronizes sending data to the sub process
		/// </summary>
		private object SendKey = new object();
		
		/// <summary>
		/// Monitor objects for blocked threads
		/// </summary>
		Dictionary<object, object> MonitorObjectsByThreadId = new Dictionary<object, object>();

        /// <summary>
        /// Place to stuff the SubProcess's response
        /// </summary>
        Dictionary<object, Dictionary<string, object>> InCommandsByThreadId = new Dictionary<object, Dictionary<string, object>>();

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
        /// The results of calling EvalScope
        /// </summary>
        public struct EvalScopeResults
        {
            /// <summary>
            /// The call's result
            /// </summary>
            public object Result;

            /// <summary>
            /// The functions that are present in the scope
            /// </summary>
            public Dictionary<string, EvalScopeFunctionInfo> Functions;
        }

        /// <summary>
        /// Information about a function
        /// </summary>
        public struct EvalScopeFunctionInfo
        {
            /// <summary>
            /// The function's properties
            /// </summary>
            public Dictionary<string, object> Properties;

            /// <summary>
            /// The function's arguments
            /// </summary>
            public IEnumerable<string> Arguments;
        }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="scopeId">
		/// A <see cref="System.Int32"/>
		/// </param>
		/// <param name="script">
		/// A <see cref="System.String"/>
		/// </param>
		/// <param name="functions">
		/// A <see cref="IEnumerable<System.String>"/>
		/// </param>
		/// <param name="returnFunctions">
		/// A <see cref="System.Boolean"/>
		/// </param>
		/// <param name="threadID">
		/// This must be unique for each stack trace.  If calls cross scopes, new threadIDs must be used <see cref="System.Object"/>
		/// </param>
        /// <exception cref="ObjectDisposedException">Thrown if the sub process was disposed through normal execution.</exception>
        /// <exception cref="AbortedException">Thrown if the sub process aborted anormally.  Callers should recover from this error condition</exception>
        public EvalScopeResults EvalScope(int scopeId, object threadID, string script, IEnumerable<string> functions, bool returnFunctions)
		{
            CheckIfAbortedOrDisposed();

            Dictionary<string, object> command;
            Dictionary<string, object> data;
            CreateCommand(scopeId, threadID,  "EvalScope", out command, out data);
            
            data["Script"] = script;
			
			if (null != functions)
				data["Functions"] = functions;
			
			if (returnFunctions)
				data["ReturnFunctions"] = returnFunctions;
			
			using (TimedLock.Lock(SendKey))
				JSONSender.Write(command);

            Dictionary<string, object> dataToReturn = WaitForResponse(scopeId);

            EvalScopeResults toReturn = new EvalScopeResults();
            toReturn.Result = dataToReturn["Result"];

            object functionsObj;
            if (dataToReturn.TryGetValue("Functions", out functionsObj))
            {
                Dictionary<string, EvalScopeFunctionInfo> functionsToReturn = new Dictionary<string, EvalScopeFunctionInfo>();

                foreach (KeyValuePair<string, object> functionKVP in (IEnumerable<KeyValuePair<string, object>>)functionsObj)
                {
                    EvalScopeFunctionInfo functionInfo = new EvalScopeFunctionInfo();
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

            Dictionary<string, object> command;
            Dictionary<string, object> data;
            CreateCommand(scopeId, threadID, "DisposeScope", out command, out data);

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

            Dictionary<string, object> command;
            Dictionary<string, object> data;
            CreateCommand(scopeId, threadID, "CallFunctionInScope", out command, out data);

            data["FunctionName"] = functionName;
            data["Arguments"] = arguments;

            using (TimedLock.Lock(SendKey))
                JSONSender.Write(command);

            Dictionary<string, object> dataToReturn = WaitForResponse(scopeId);
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

            Dictionary<string, object> command;
            Dictionary<string, object> data;
            CreateCommand(scopeId, threadID, "CallCallback", out command, out data);

            data["CallbackId"] = callbackId;
            data["Arguments"] = arguments;

            using (TimedLock.Lock(SendKey))
                JSONSender.Write(command);

            Dictionary<string, object> dataToReturn = WaitForResponse(scopeId);
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
                return SubProcess.CallCallback(ScopeId, ThreadId, CallbackId, arguments);
            }
        }

        /// <summary>
        /// Helper to create a command
        /// </summary>
        /// <param name="scopeId"></param>
        /// <param name="threadID"></param>
        /// <param name="command"></param>
        /// <param name="data"></param>
        private static void CreateCommand(
            int scopeId, object threadID, string commandName, out Dictionary<string, object> command, out Dictionary<string, object> data)
        {
            command = new Dictionary<string, object>();
            command["ScopeID"] = scopeId;
            command["ThreadID"] = threadID;
            command["Command"] = commandName;

            data = new Dictionary<string, object>();
            command["Data"] = data;
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
        /// Helper to block a thread until the response comes back
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, object> WaitForResponse(int scopeId)
        {
            object monitorObject = new object();
            Dictionary<string, object> inCommand;

            if (null == TrackedObjects)
                TrackedObjects = new Dictionary<int, object>();

            NumInJavascriptCalls++;

            try
            {
                do
                {
                    bool needWait;
                    using (TimedLock.Lock(InCommandsByThreadId))
                        needWait = !InCommandsByThreadId.TryGetValue(Thread.CurrentThread.ManagedThreadId, out inCommand);

                    if (needWait)
                    {
                        using (TimedLock.Lock(MonitorObjectsByThreadId))
                            MonitorObjectsByThreadId[Thread.CurrentThread.ManagedThreadId] = monitorObject;

                        try
                        {
                            lock (monitorObject)
                                Monitor.Wait(monitorObject);
                        }
                        finally
                        {
                            using (TimedLock.Lock(MonitorObjectsByThreadId))
                                MonitorObjectsByThreadId.Remove(Thread.CurrentThread.ManagedThreadId);
                        }

                        using (TimedLock.Lock(InCommandsByThreadId))
                        {
                            inCommand = InCommandsByThreadId[Thread.CurrentThread.ManagedThreadId];
                            InCommandsByThreadId.Remove(Thread.CurrentThread.ManagedThreadId);
                        }
                    }

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

                        Dictionary<string, object> outCommand;
                        Dictionary<string, object> outData;
                        CreateCommand(scopeId, threadId, "RespondCallParentFunction", out outCommand, out outData);

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

                            else if (parentFunctionDataToReturn is CachedObjectId)
                                outData["CacheID"] = ((CachedObjectId)parentFunctionDataToReturn).CacheId;

                            else if (parentFunctionDataToReturn != Undefined.Value)
                            {
                                string nameSpace = parentFunctionDataToReturn.GetType().Namespace;

                                if (!(nameSpace.StartsWith("System.") || nameSpace.Equals("System")))//    parentFunctionDataToReturn.GetType().Namespace.StartsWith("System"))
                                {
                                    Dictionary<string, object> jsoned = JsonReader.Deserialize<Dictionary<string, object>>(
                                        JsonWriter.Serialize(parentFunctionDataToReturn));

                                    int parentObjectId = SRandom.Next();
                                    jsoned["ParentObjectId"] = parentObjectId;

                                    TrackedObjects[parentObjectId] = parentFunctionDataToReturn;
                                    parentFunctionDataToReturn = jsoned;
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

                        using (TimedLock.Lock(SendKey))
                            JSONSender.Write(outCommand);
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

        /// <summary>
        /// Represents an undefined value
        /// </summary>
        public class Undefined
        {
            private Undefined() { }

            public static Undefined Value
            {
                get { return _Instance; }
            }
            private static readonly Undefined _Instance = new Undefined();
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

            foreach (object monitorObject in MonitorObjectsByThreadId.Values)
                lock (monitorObject)
                    Monitor.Pulse(monitorObject);

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
                DateTime start = (DateTime)state;

                if (DateTime.UtcNow - start > TimeSpan.FromSeconds(0.25))
                    Process.Kill();
                else
                    ThreadPool.QueueUserWorkItem(KillSubprocess, start);
            }
            catch { }
		}
		
		~SubProcess()
		{
			Dispose();
		}
	}

    /// <summary>
    /// Delegate for calling parent functions
    /// </summary>
    /// <param name="functionName"></param>
    /// <param name="arguments"></param>
    /// <returns></returns>
    public delegate object CallParentFunctionDelegate(string functionName, object threadId, object[] arguments);
}
