using System;
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
            public Dictionary<string, Dictionary<string, object>> Functions;
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
        public EvalScopeResults EvalScope(int scopeId, string script, IEnumerable<string> functions, bool returnFunctions, object threadID)
		{
            if (Disposed)
                throw new ObjectDisposedException("This Javascript sub process is disposed and can no longer be used");

			Dictionary<string, object> args = new Dictionary<string, object>();
			args["ScopeID"] = scopeId;
			args["ThreadID"] = threadID;
			args["Command"] = "EvalScope";
			
			Dictionary<string, object> data = new Dictionary<string, object>();
			data["Script"] = script;
			
			if (null != functions)
				data["Functions"] = functions;
			
			if (returnFunctions)
				data["ReturnFunctions"] = returnFunctions;
			
			args["Data"] = data;

            object monitorObject = new object();
			
			using (TimedLock.Lock(SendKey))
				JSONSender.Write(args);

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

            EvalScopeResults toReturn = new EvalScopeResults();
            toReturn.Result = dataToReturn["Result"];

            object functionsObj;
            if (dataToReturn.TryGetValue("Functions", out functionsObj))
            {
                Dictionary<string, Dictionary<string, object>> functionsToReturn = new Dictionary<string, Dictionary<string, object>>();

                foreach (KeyValuePair<string, object> functionKVP in (IEnumerable<KeyValuePair<string, object>>)functionsObj)
                    functionsToReturn[functionKVP.Key] = (Dictionary<string, object>)functionKVP.Value;

                toReturn.Functions = functionsToReturn;
            }

            return toReturn;
		}

        private bool Disposed = false;
		
		public void Dispose ()
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
