// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace ObjectCloud.Common
{
	/// <summary>
	/// Encapsulates running a sub process.
	/// </summary>
	public class SubProcess : IRunnable
	{
		public SubProcess() 	{ }

		public string Arguments 
		{
			get { return _Arguments; }
			set { _Arguments = value; }
		}
		private string _Arguments;

		public string ExecutableName 
		{
			get { return _ExecutableName; }
			set { _ExecutableName = value; }
		}
		private string _ExecutableName;

		public string WorkingDirectory 
		{
			get { return _WorkingDirectory; }
			set { _WorkingDirectory = value; }
		}	
		private string _WorkingDirectory;
		
		public Dictionary<string, string> EnvironmentVariables 
		{
			get { return _EnvironmentVariables; }
			set { _EnvironmentVariables = value; }
		}
		private Dictionary<string, string> _EnvironmentVariables;
		
		/// <value>
		/// Set to false to disable starting the sub-process
		/// </value>
		public bool Enabled 
		{
			get { return _Enabled; }
			set { _Enabled = value; }
		}	
		private bool _Enabled = true;
		
		/// <summary>
		/// Runs the process.  Blocks until the process ends.  Can be stopped by Thread.Abort()
		/// </summary>
		public void Run()
		{
			if (!Enabled)
				return;
			
			ProcessStartInfo processStartInfo = new ProcessStartInfo();
			processStartInfo.FileName = ExecutableName;
			processStartInfo.Arguments = Arguments;
			processStartInfo.UseShellExecute = false;

			if (null != WorkingDirectory)
				processStartInfo.WorkingDirectory = WorkingDirectory;
			else
				processStartInfo.WorkingDirectory = Directory.GetCurrentDirectory();
			
			if (null != EnvironmentVariables)
				foreach (string varname in EnvironmentVariables.Keys)
					processStartInfo.EnvironmentVariables[varname] = EnvironmentVariables[varname];
			
			Process process = Process.Start(processStartInfo);
			
			try
			{
				do
					Thread.Sleep(10000);
				while (!process.HasExited);
			}
			catch (ThreadAbortException)
			{	
				try
				{
					process.Kill();
				}
				catch {}
				
				throw;
			}
		}
	}
}
