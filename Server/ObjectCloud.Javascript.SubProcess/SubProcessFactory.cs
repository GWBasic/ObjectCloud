// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using ObjectCloud.Common;

namespace ObjectCloud.Javascript.SubProcess
{
    /// <summary>
    /// Simple sub process factory that holds a limited set of sub processes
    /// </summary>
    public class SubProcessFactory : ISubProcessFactory
    {
        /// <summary>
        /// The number of active sub processes
        /// </summary>
        public int NumSubProcesses
        {
            get 
            {
                if (null == _NumSubProcesses)
                    throw new JavascriptException("NumSubProcesses not set");

                return _NumSubProcesses.Value; 
            }
            set 
            {
                using (TimedLock.Lock(SubProcessCtr))
                {
                    _NumSubProcesses = value;

                    // See if new cubbyholes need to be allocated / removed
                    AllocateSubProcesses();
                }
            }
        }
        private int? _NumSubProcesses;

        /// <summary>
        /// The existing sub processes
        /// </summary>
        private SubProcess[] SubProcesses = null;

        /// <summary>
        /// The existing keys
        /// </summary>
        private object[] Keys = null;

        /// <summary>
        /// The current sub process that will be returned
        /// </summary>
        private Wrapped<int> SubProcessCtr = 0;

        public SubProcess GetSubProcess()
        {
            int subProcessCtr;
            using (TimedLock.Lock(SubProcessCtr))
            {
                // Figure out which sub process to return;

                if (SubProcessCtr.Value >= NumSubProcesses)
                    SubProcessCtr.Value = 0;

                subProcessCtr = SubProcessCtr.Value;
                SubProcessCtr.Value++;
            }

            SubProcess toReturn = SubProcesses[subProcessCtr];

            // If the sub-process is alive, return without locking
            if (toReturn.Alive)
                return toReturn;

            // The sub-process isn't alive, create with locking
            using (TimedLock.Lock(Keys[subProcessCtr]))
            {
                toReturn = SubProcesses[subProcessCtr];

                if (!toReturn.Alive)
                {
                    toReturn = new SubProcess();
                    SubProcesses[subProcessCtr] = toReturn;
                }

                return toReturn;
            }
        }

        /// <summary>
        /// Allocates sub processes
        /// </summary>
        private void AllocateSubProcesses()
        {
            if (null != SubProcesses)
                if (SubProcesses.Length != NumSubProcesses)
                {
                    if (null != SubProcesses)
                        foreach (SubProcess oldSubProcess in SubProcesses)
                            try
                            {
                                oldSubProcess.Process.Kill();
                            }
                            catch { }
                }
                else
                    return;

            SubProcesses = new SubProcess[NumSubProcesses];
            Keys = new object[NumSubProcesses];

            List<Thread> threadsToJoin = new List<Thread>();

            for (int ctr = 0; ctr < NumSubProcesses; ctr++)
            {
                Thread createThread = new Thread(new ParameterizedThreadStart(CreateSubProcess));
                createThread.Start(ctr);

                threadsToJoin.Add(createThread);
            }

            foreach (Thread createThread in threadsToJoin)
                createThread.Join();
        }

        /// <summary>
        /// Creates a sub process
        /// </summary>
        /// <param name="state"></param>
        private void CreateSubProcess(object state)
        {
            int subProcessCtr = (int)state;

            SubProcesses[subProcessCtr] = new SubProcess();
            Keys[subProcessCtr] = new object();
        }
    }
}
