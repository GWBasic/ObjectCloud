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

		public SubProcess ()
		{
			Process = new Process();
            Process.StartInfo = new ProcessStartInfo("java", "-cp ." + Path.DirectorySeparatorChar + "js.jar -jar JavascriptProcess.jar " + Process.GetCurrentProcess().Id.ToString());
			Process.StartInfo.RedirectStandardInput = true;
            Process.StartInfo.RedirectStandardOutput = true;
            Process.StartInfo.RedirectStandardError = true;
            Process.StartInfo.UseShellExecute = false;
            Process.Exited += new EventHandler(Process_Exited);

            if (!Process.Start())
            {
                Exception e = new JavascriptException("Could not start sub process");
                log.Error("Error starting Javascript sub process", e);

                throw e;
            }

            log.Info("Javascript sub process started: " + Process.ToString());

			JSONSender = new JsonWriter(Process.StandardInput);

            new Thread(new ThreadStart(MonitorForResponses)).Start();
            new Thread(new ThreadStart(MonitorForErrors)).Start();
        }

        void Process_Exited(object sender, EventArgs e)
        {
            if (!Disposed)
                Dispose();
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
                    Dictionary<string, object> inCommand = JsonReader.Deserialize<Dictionary<string, object>>(Process.StandardOutput.ReadLine());

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
        public EvalScopeResults EvalScope(int scopeId, object threadID, string script, IEnumerable<string> functions, bool returnFunctions)
		{
            if (Disposed)
                throw new ObjectDisposedException("This Javascript sub process is disposed and can no longer be used");

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

            Dictionary<string, object> dataToReturn = WaitForResponse();

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
        /// Calls a function in the sub-process
        /// </summary>
        /// <param name="scopeID"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public object CallFunctionInScope(int scopeId, object threadID, string functionName, IEnumerable arguments)
        {
            Dictionary<string, object> command;
            Dictionary<string, object> data;
            CreateCommand(scopeId, threadID, "CallFunctionInScope", out command, out data);

            data["FunctionName"] = functionName;
            data["Arguments"] = arguments;

            using (TimedLock.Lock(SendKey))
                JSONSender.Write(command);

            Dictionary<string, object> dataToReturn = WaitForResponse();
            return dataToReturn["Result"];
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
        /// Helper to block a thread until the response comes back
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, object> WaitForResponse()
        {
            object monitorObject = new object();
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

            Dictionary<string, object> inCommand;
            using (TimedLock.Lock(InCommandsByThreadId))
                inCommand = InCommandsByThreadId[Thread.CurrentThread.ManagedThreadId];

            Dictionary<string, object> dataToReturn = (Dictionary<string, object>)inCommand["Data"];

            object exceptionFromJavascript;
            if (dataToReturn.TryGetValue("Exception", out exceptionFromJavascript))
                throw new JavascriptException(JsonWriter.Serialize(exceptionFromJavascript));

            return dataToReturn;
        }

        private bool Disposed = false;
		
		public void Dispose()
		{
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
}
