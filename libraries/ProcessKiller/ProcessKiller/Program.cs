// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

using Mono.Unix;
using Mono.Unix.Native;


namespace ProcessKiller
{
    class Program
    {
        static void Main(string[] args)
        {
            if (1 != args.Length)
            {
                Console.WriteLine("Process killer.\n\tUsage:  Pass a process ID on the command line, and then continue to pass process IDs through stdin.\n\tWhen the parent process (specified on the command line) ends, all child processes (passed through stdin) are killed");
                return;
            }

            try
            {
				int parentProcessId = Convert.ToInt32(args[0]);
				
				Process parent;
				try
				{
					parent = Process.GetProcessById(parentProcessId);
				}
				catch (ArgumentException)
				{
					// If the parent process can't be obtained, just return.  It means that the parent process is done
					return;
				}
				
				try
				{
					//Console.WriteLine("Got parent process");
					//Console.WriteLine(parent.ToString());
					
					parent.EnableRaisingEvents = true;
					
					// If running on unix, trap signals
					int p = (int) Environment.OSVersion.Platform;
					if ((p == 4) || (p == 6) || (p == 128))
					{
						Thread signalTrapper = new Thread(UnixSignalTrapper);
						signalTrapper.Name = "Unix Signal Trapper";
						signalTrapper.IsBackground = true;
							
						signalTrapper.Start();
					}
					
					Thread readThread = new Thread(new ThreadStart(ReadFromConsole));
					readThread.IsBackground = true;
					readThread.Start();
					
					// All this junk, instead of a simple WaitForExit() call, is to work around Mono issues
					do 
					{
						Thread.Sleep(3000);
						parent.WaitForExit();
						
						// parent.HasExited is problematic on Mono
						if (!parent.HasExited)
						{
							parent.Dispose();
							parent = Process.GetProcessById(parentProcessId);
							parent.EnableRaisingEvents = true;
						}
					} while (!parent.HasExited);
					
					//Console.WriteLine("Parent process ended");
					StayOpen = false;
				}
				finally
				{
					parent.Dispose();
				}
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.ToString());
			}
			finally
			{
				lock (SubProcesses)
                	foreach(Process p in new List<Process>(SubProcesses))
                    	try
	                    {
							//Console.WriteLine("Killing: " + p.ToString());
        	                p.Kill();
            	        }
                	    catch { }
            }
			
			Process.GetCurrentProcess().Kill();
            Environment.Exit(0);
        }
	
		private static void ReadFromConsole()
		{
            while (StayOpen)
            {
				string processId = Console.ReadLine();
				
				if (null != processId)
				{
					if (processId.Length > 0)
					{
						//Console.WriteLine(processId);
						
	                    Process newProcess = Process.GetProcessById(Convert.ToInt32(processId.Trim()));
					
						//Console.WriteLine("Got sub process");
						//Console.WriteLine(newProcess.ToString());
					
						newProcess.EnableRaisingEvents = true;
	                    newProcess.Exited += new EventHandler(newProcess_Exited);
						
						lock (SubProcesses)
	                    	SubProcesses.Add(newProcess);
					}
				}
				else
					Thread.Sleep(3000);
            }
		}
		
		static bool StayOpen = true;
		
		static void UnixSignalTrapper()
		{
			Signum[] quitSignals = new Signum[]
			{
				Signum.SIGABRT,
				//Signum.SIGBUS, Disabled because of mysterious MacOS behavior
				//Signum.SIGCHLD, Disabled because child processes stopping shouldn't kill the parent process
				Signum.SIGHUP,
				Signum.SIGILL,
				Signum.SIGINT,
				Signum.SIGQUIT,
				Signum.SIGTERM,
				Signum.SIGTSTP,
				Signum.SIGUSR1,
				Signum.SIGUSR2
			};
			
			List<UnixSignal> signals = new List<UnixSignal>();
			foreach (Signum quitSignal in quitSignals)
				signals.Add(new UnixSignal(quitSignal));
 
	        // Wait for a signal to be delivered
			while (StayOpen)
        			//Console.WriteLine(UnixSignal.WaitAny(signals.ToArray(), -1).ToString());
        			UnixSignal.WaitAny(signals.ToArray(), -1);
			
			Process.GetCurrentProcess().Kill();
		}

        static void  newProcess_Exited(object sender, EventArgs e)
        {
            try
            {
				lock (SubProcesses)
                	SubProcesses.Remove((Process)sender);
            }
            catch { }
        }

        /// <summary>
        /// All of the sub processes
        /// </summary>
        static List<Process> SubProcesses = new List<Process>();
    }
}
