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
                Process parent = Process.GetProcessById(Convert.ToInt32(args[0]));
				
				Console.WriteLine("Got parent process");
				Console.WriteLine(parent.ToString());
				
				parent.EnableRaisingEvents = true;
                parent.Exited += new EventHandler(parent_Exited);
				
				Console.WriteLine("Registered exit handler");
				
				// If running on unix, trap signals
				int p = (int) Environment.OSVersion.Platform;
				if ((p == 4) || (p == 6) || (p == 128))
				{
					Thread signalTrapper = new Thread(UnixSignalTrapper);
					signalTrapper.Name = "Unix Signal Trapper";
					signalTrapper.IsBackground = true;
						
					signalTrapper.Start();
				}


                while (StayOpen)
                {
					string processId = Console.ReadLine();
					
					if (null != processId)
					{
						if (processId.Length > 0)
						{
							Console.WriteLine(processId);
							
		                    Process newProcess = Process.GetProcessById(Convert.ToInt32(processId.Trim()));
						
							Console.WriteLine("Got sub process");
							Console.WriteLine(newProcess.ToString());
						
							newProcess.EnableRaisingEvents = true;
		                    newProcess.Exited += new EventHandler(newProcess_Exited);
		                    SubProcesses.Add(newProcess);
						}
					}
					else
						Thread.Sleep(3000);
                }

            }
            catch (Exception e)
            {
                System.Console.Error.WriteLine(e.ToString());

                foreach(Process p in SubProcesses)
                    try
                    {
                        p.Kill();
                    }
                    catch { }
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
        			Console.WriteLine(UnixSignal.WaitAny(signals.ToArray(), -1).ToString());
			
			Process.GetCurrentProcess().Kill();
		}

        static void  newProcess_Exited(object sender, EventArgs e)
        {
            try
            {
                SubProcesses.Remove((Process)sender);
            }
            catch { }
        }

        static void parent_Exited(object sender, EventArgs e)
        {
			Console.WriteLine("Parent process exited");
			
			Thread.Sleep(5000);
			
            foreach (Process p in SubProcesses)
                try
                {
					Console.WriteLine("Killing: " + p.ToString());
                    p.Kill();
                }
                catch { }

			Console.WriteLine("Killing Process Killer " + Process.GetCurrentProcess().Id.ToString());
			StayOpen = false;
            Environment.Exit(0);
        }

        /// <summary>
        /// All of the sub processes
        /// </summary>
        static List<Process> SubProcesses = new List<Process>();
    }
}
