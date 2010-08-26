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
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Javascript.SubProcess
{
    public class SubProcess : System.Runtime.ConstrainedExecution.CriticalFinalizerObject, IDisposable
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

        public SubProcess(FileHandlerFactoryLocator fileHandlerFactoryLocator)
        {
            _Process = new Process();
            _Process.StartInfo = new ProcessStartInfo("java", "-cp ." + Path.DirectorySeparatorChar + "js.jar -jar JavascriptProcess.jar " + Process.GetCurrentProcess().Id.ToString());
            _Process.StartInfo.RedirectStandardInput = true;
            _Process.StartInfo.RedirectStandardOutput = true;
            _Process.StartInfo.RedirectStandardError = true;
            _Process.StartInfo.UseShellExecute = false;
            _Process.EnableRaisingEvents = true;
            _Process.Exited += new EventHandler(Process_Exited);

            log.Info("Starting sub process");

            if (!Process.Start())
            {
                Exception e = new JavascriptException("Could not start sub process");
                log.Error("Error starting Javascript sub process", e);

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
            try
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
            string error = e.Data;

            if (null != error)
                try
                {
                    // Try deserializing as if it's a string encoded in JSON.  This is so that many-line strings can be transmitted
                    try
                    {
                        error = JsonReader.Deserialize<string>(error);
                    }
                    catch { }

                    log.Error("Javascript sub process error: " + error);
                }
                catch (Exception ex)
                {
                    log.Error("Error reading from Javascript sub process: ", ex);
                }
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

        ReaderWriterLockSlim ParentFunctionDelegatesByScopeIdLock = new ReaderWriterLockSlim();

        /// <summary>
        /// Registers a callback for when the javascript in the scope calls a parent function
        /// </summary>
        /// <param name="scopeId"></param>
        /// <param name="callParentFunctionDelegate"></param>
        public void RegisterParentFunctionDelegate(int scopeId, CallParentFunctionDelegate parentFunctionDelegate)
        {
            ParentFunctionDelegatesByScopeIdLock.EnterWriteLock();
            try
            {
                ParentFunctionDelegatesByScopeId[scopeId] = parentFunctionDelegate;
            }
            finally
            {
                ParentFunctionDelegatesByScopeIdLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// The results of calling EvalScope
        /// </summary>
        public struct CreateScopeResults
        {
            /// <summary>
            /// The call's result
            /// </summary>
            public object[] Results;

            /// <summary>
            /// The functions that are present in the scope
            /// </summary>
            public Dictionary<string, CreateScopeFunctionInfo> Functions;
        }

        /// <summary>
        /// Information about a function
        /// </summary>
        public struct CreateScopeFunctionInfo
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
        /// Creates a parent scope
        /// </summary>
        /// <param name="parentScopeId"></param>
        /// <param name="threadID"></param>
        /// <param name="data"></param>
        public void CreateParentScope(int parentScopeId, object threadID, Dictionary<string, object> data)
        {
            CheckIfAbortedOrDisposed();

            Dictionary<string, object> command;
            command = new Dictionary<string, object>();
            command["ParentScopeID"] = parentScopeId;
            command["ThreadID"] = threadID;
            command["Data"] = data;

            SendCommandAndHandleResponse(command, int.MaxValue);
        }

        /// <summary>
        /// Disposes a parent scope
        /// </summary>
        /// <param name="parentScopeId"></param>
        /// <param name="threadID"></param>
        /// <param name="data"></param>
        public void DisposeParentScope(int parentScopeId, object threadID)
        {
            CheckIfAbortedOrDisposed();

            Dictionary<string, object> command;
            command = new Dictionary<string, object>();
            command["ParentScopeID"] = parentScopeId;
            command["ThreadID"] = threadID;

            using (TimedLock.Lock(SendKey))
                JSONSender.Write(command);
        }
        
        /// <summary>
        /// Creates a scope
        /// </summary>
        /// <param name="scopeId"></param>
        /// <param name="threadID"></param>
        /// <param name="data">The data that is placed into the scope</param>
        /// <returns></returns>
        public CreateScopeResults CreateScope(int scopeId, int parentScopeId, object threadID, Dictionary<string, object> data)
        {
            CheckIfAbortedOrDisposed();

            Dictionary<string, object> command = CreateCommand(scopeId, threadID, "CreateScope", data);
            command["ParentScopeID"] = parentScopeId;

            Dictionary<string, object> dataToReturn = SendCommandAndHandleResponse(command, scopeId);

            CreateScopeResults toReturn = new CreateScopeResults();
            toReturn.Results = (object[])dataToReturn["Results"];

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

            return toReturn;
        }

        /// <summary>
        /// Throws an exception if the sub process is aborted or disposed
        /// </summary>
        private void CheckIfAbortedOrDisposed()
        {
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

            ParentFunctionDelegatesByScopeIdLock.EnterWriteLock();
            try
            {
                ParentFunctionDelegatesByScopeId.Remove(scopeId);
            }
            finally
            {
                ParentFunctionDelegatesByScopeIdLock.ExitWriteLock();
            }

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
        static uint NumInJavascriptCalls = 0;

        /// <summary>
        /// Helper to block a thread until the response comes back
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, object> SendCommandAndHandleResponse(
            Dictionary<string, object> command,
            int scopeId)
        {
            // Send the command
            using (TimedLock.Lock(SendKey))
                JSONSender.Write(command);

            if (null == TrackedObjects)
                TrackedObjects = new Dictionary<int, object>();

            NumInJavascriptCalls++;

            try
            {
                do
                {
                    Dictionary<string, object> inCommand;
					
					TimerCallback callback = delegate(object state)
					{
						log.Warn("Killing sub-process due to timeout");
						Dispose();
					};
                    
                    // If the thread waits, spin up a timer kill the process in case it runs too long
                    using (new Timer(callback, null, 10000, 0))
                        inCommand = WaitForResponse();

                    Dictionary<string, object> dataToReturn = (Dictionary<string, object>)inCommand["Data"];

                    // If the response is for calling a parent function in this process, then call it, else just return the results
                    if ("CallParentFunction".Equals(inCommand["Command"]))
                    {
                        // Note:  There is a lot of overhead when calling from Javascript into the main process
                        // Stack overflow exceptions are highly probable when there are over 150 "Javascript" calls in the stack
                        // As Stack Overflow can't be caught, it's prevented instead
                        if (NumInJavascriptCalls > 160)
                            throw new WebResultsOverrideException(
                                WebResults.From(Status._500_Internal_Server_Error, "Stack overflow in server-side Javascript"));

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
                            CallParentFunctionDelegate callParentFunctionDelegate;

                            ParentFunctionDelegatesByScopeIdLock.EnterReadLock();
                            try
                            {
                                callParentFunctionDelegate = ParentFunctionDelegatesByScopeId[scopeId];
                            }
                            finally
                            {
                                ParentFunctionDelegatesByScopeIdLock.ExitReadLock();
                            }

                            object parentFunctionDataToReturn = callParentFunctionDelegate(
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

                        if (dataToReturn.TryGetValue("Results", out result))
                        {
                            if (result is object[])
                            {
                                object[] results = (object[])result;
                                for (int ctr = 0; ctr < results.Length; ctr++)
                                    if (results[ctr] is Dictionary<string, object>)
                                    {
                                        object parentObjectId;
                                        if (((Dictionary<string, object>)results[ctr]).TryGetValue("ParentObjectId", out parentObjectId))
                                        {
                                            object parentObject;
                                            if (TrackedObjects.TryGetValue(Convert.ToInt32(parentObjectId), out parentObject))
                                                results[ctr] = parentObject;
                                        }
                                    }
                            }
                        }

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
        /// Provides syncronization when waiting for a response
        /// </summary>
        private object RespondKey = new object();

        //// <summary>
        /// Dictionary of objects that are to be pulsed to re-awake when the response comes in
        /// </summary>
        private Dictionary<object, Wrapped<Dictionary<string, object>>> ResponsePulsers =
            new Dictionary<object, Wrapped<Dictionary<string, object>>>();

        /// <summary>
        /// Syncronizes the sub process's output stream and returns the appropriate response
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, object> WaitForResponse()
        {
            object threadID = Thread.CurrentThread.ManagedThreadId;

            while (!Process.HasExited)
                if (Monitor.TryEnter(RespondKey))
                    try
                    {
                        while (!Process.HasExited)
                        {
                            string inCommandString;

                            do
                            {
                                if (Process.StandardOutput.EndOfStream || Process.HasExited)
                                    throw new JavascriptException("The sub process has exited");

                                inCommandString = _Process.StandardOutput.ReadLine();

                                if (null == inCommandString)
                                    log.Warn("Null recieved from sub process");

                            } while (null == inCommandString);

                            Dictionary<string, object> inCommand = JsonReader.Deserialize<Dictionary<string, object>>(inCommandString);

                            object commandThreadID = inCommand["ThreadID"];

                            Wrapped<Dictionary<string, object>> toPulse;

                            lock (ResponsePulsers)
                                // If this is the command for this thread, return it
                                if (commandThreadID.Equals(threadID))
                                {
                                    Monitor.Exit(RespondKey);

                                    using (IEnumerator<Wrapped<Dictionary<string, object>>> pulserEnumerator = ResponsePulsers.Values.GetEnumerator())
                                        if (pulserEnumerator.MoveNext())
                                            toPulse = pulserEnumerator.Current;

                                    return inCommand;
                                }
                                else
                                {
                                    // Wait until the expected value is put in the dictionary
                                    while (!ResponsePulsers.TryGetValue(commandThreadID, out toPulse))
                                    {
                                        Monitor.Exit(ResponsePulsers);
                                        Thread.Sleep(0);
                                        Monitor.Enter(ResponsePulsers);
                                    }

                                    toPulse.Value = inCommand;
                                }

                            lock (toPulse)
                                Monitor.Pulse(toPulse);
                        }

                        Monitor.Exit(RespondKey);
                    }
                    catch
                    {
                        Monitor.Exit(RespondKey);
                        throw;
                    }
                else
                {
                    Wrapped<Dictionary<string, object>> pulser = new Wrapped<Dictionary<string, object>>();

                    lock (pulser)
                    {
                        lock (ResponsePulsers)
                            ResponsePulsers[threadID] = pulser;

                        Monitor.Wait(pulser, 100);
                    }

                    lock (ResponsePulsers)
                        ResponsePulsers.Remove(threadID);

                    if (null != pulser.Value)
                        return pulser.Value;
                }

            throw new ObjectDisposedException("The sub processes has exited");
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

    /// <summary>
    /// Delegate for calling parent functions
    /// </summary>
    /// <param name="functionName"></param>
    /// <param name="arguments"></param>
    /// <returns></returns>
    public delegate object CallParentFunctionDelegate(string functionName, object threadId, object[] arguments);
}
