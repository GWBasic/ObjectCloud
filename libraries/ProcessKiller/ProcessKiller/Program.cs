using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

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

                while (true)
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
			
            foreach (Process p in SubProcesses)
                try
                {
					Console.WriteLine("Killing: " + p.ToString());
                    p.Kill();
                }
                catch { }

			Console.WriteLine("Killing Process Killer");
            Environment.Exit(0);
        }

        /// <summary>
        /// All of the sub processes
        /// </summary>
        static List<Process> SubProcesses = new List<Process>();
    }
}
