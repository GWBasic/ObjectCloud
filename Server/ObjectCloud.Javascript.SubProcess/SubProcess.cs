using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

using JsonFx.Json;

using ObjectCloud.Common;

namespace ObjectCloud.Javascript.SubProcess
{
	public class SubProcess : IDisposable
	{
		public SubProcess ()
		{
			Process Process = new Process();
			Process.StartInfo = new ProcessStartInfo("java", "JavascriptProcess.jar " + Process.GetCurrentProcess().Id.ToString());
			Process.StartInfo.RedirectStandardInput = true;
			Process.StartInfo.RedirectStandardOutput = true;
			Process.StartInfo.UseShellExecute = false;
			
			if (!Process.Start())
				throw new JavascriptException("Could not start sub process");

			JSONSender = new JsonWriter(Process.StandardInput);
			
			// TODO:  handlers for the sub-process's output and error streams
		}
		
		Process Process;
		JsonWriter JSONSender;
		
		/// <summary>
		/// Synchronizes sending data to the sub process
		/// </summary>
		private object SendKey = new object();
		
		/// <summary>
		/// All of the waiting threads
		/// </summary>
		Dictionary<int, Thread> ThreadsByThreadId = new Dictionary<int, Thread>();
		
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
		public void EvalScope(int scopeId, string script, IEnumerable<string> functions, bool returnFunctions, object threadID)
		{
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
			
			ThreadsByThreadId[Thread.CurrentThread.ManagedThreadId] = Thread.CurrentThread;
			
			using (TimedLock.Lock(SendKey))
				JSONSender.Write(args);
			
			Thread.CurrentThread.Suspend();
		}
		
		public void Dispose ()
		{
			using (TimedLock.Lock(SendKey))
				JsonWriter.Write(new Dictionary<string, object>());
			
			// Make sure process stops on another thread
			DateTime start = DateTime.UtcNow;
			ThreadPool.QueueUserWorkItem(KillSubprocess, start);
			
			GC.SuppressFinalize(this);
		}

		private void KillSubprocess(object state)
		{
			DateTime start = (DateTime)state;

			if (DateTime.UtcNow - start > TimeSpan.FromSeconds(0.25))
				Process.Kill();
			else
				ThreadPool.QueueUserWorkItem(KillSubprocess, start);
		}
		
		~SubProcess()
		{
			Dispose();
		}
	}
}
