// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

using JmBucknall.Structures;

namespace ObjectCloud.Common
{
    /// <summary>
    /// An isolated controllable threadpool instance
    /// </summary>
    public class ThreadPoolInstance : IDisposable
    {
        public ThreadPoolInstance(string threadNamePrefix, int numIdleThreads)
        {
            _ThreadNamePrefix = threadNamePrefix;
            NumIdleThreads = numIdleThreads;
        }

        public ThreadPoolInstance(string threadNamePrefix) : this(threadNamePrefix, 10) { }

        /// <summary>
        /// The number of idle threads to keep around
        /// </summary>
        public int NumIdleThreads
        {
            get { return _NumIdleThreads; }
            set
            {
                _NumIdleThreads = value;

                long difference = Math.Abs(value - CurrentIdleThreads.Count);

                for (long ctr = 0; ctr < difference; ctr++)
                    RunThreadStart(delegate(){});
            }
        }
        private volatile int _NumIdleThreads;

        /// <summary>
        /// The prefix to use for naming threads created in this pool
        /// </summary>
        public string ThreadNamePrefix
        {
            get { return _ThreadNamePrefix; }
        }
        private readonly string _ThreadNamePrefix;

        /// <summary>
        /// The next ID
        /// </summary>
        public long NextId
        {
            get
            {
                long toReturn;
                long nextId;
                do
                {
                    nextId = _NextId;

                    if (nextId < long.MaxValue)
                        toReturn = nextId + 1;
                    else
                        toReturn = 0;

                } while (nextId != Interlocked.CompareExchange(ref _NextId, toReturn, nextId));

                return toReturn;
            }
        }
        private long _NextId = 0;

        /// <summary>
        /// Queue of objects waiting to be pulsed to start an idle thread
        /// </summary>
        private LockFreeStack_WithCount<Wrapped<ThreadStart>> CurrentIdleThreads =
            new LockFreeStack_WithCount<Wrapped<ThreadStart>>();

        /// <summary>
        /// Runs the delegate on its own thread, re-using a thread if it's sitting around
        /// </summary>
        /// <param name="threadStart"></param>
        public void RunThreadStart(ThreadStart threadStart)
        {
            Wrapped<ThreadStart> toPulse;

            if (CurrentIdleThreads.Pop(out toPulse))
            {
                toPulse.Value = threadStart;

                lock(toPulse)
                    Monitor.Pulse(toPulse);
            }
            else
            {
                Thread thread = new Thread(delegate()
                {
                    threadStart();
                    RunPooledThread();
                });

                thread.Name = ThreadNamePrefix + " " + NextId.ToString();
                thread.IsBackground = true;

                thread.Start();
            }
        }

        /// <summary>
        /// Either ends the thread by returning, or waits for another call to RunThreadStart
        /// </summary>
        private void RunPooledThread()
        {
            Wrapped<ThreadStart> toBePulsed = new Wrapped<ThreadStart>();

            while (true)
            {
                // Kill the thread if there is already enough idle threads
                if (CurrentIdleThreads.Count >= NumIdleThreads)
                    return;

                CurrentIdleThreads.Push(toBePulsed);

                lock(toBePulsed)
                    Monitor.Wait(toBePulsed);

                toBePulsed.Value();
            }
        }

        /// <summary>
        /// Warning:  Disposing just sets NumIdleThreads to 0 but does not stop any running tasks
        /// </summary>
        public void Dispose()
        {
            NumIdleThreads = 0;
        }
    }
}
