using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

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
                parent.Exited += new EventHandler(parent_Exited);

                while (true)
                {
                    Process newProcess = Process.GetProcessById(Convert.ToInt32(Console.In.ReadLine().Trim()));
                    newProcess.Exited += new EventHandler(newProcess_Exited);
                    SubProcesses.Add(newProcess);
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
            foreach (Process p in SubProcesses)
                try
                {
                    p.Kill();
                }
                catch { }

            Environment.Exit(0);
        }

        /// <summary>
        /// All of the sub processes
        /// </summary>
        static List<Process> SubProcesses = new List<Process>();
    }
}
