// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ObjectCloud.Common
{
    public static class NonBlockingConsoleWriter
    {
        static volatile bool Running = false;
        static object Key = new object();
        static Queue<string> QueuedStrings = new Queue<string>();

        /// <summary>
        /// Prints the text to the console.  Does not block in release builds.  All text is queued up to be printed
        /// </summary>
        /// <param name="task"></param>
        static public void Print(string task)
        {
#if RELEASE
            using (TimedLock.Lock(Key))
            {
                QueuedStrings.Enqueue(task);

                if (!Running)
                {
                    Running = true;

                    ThreadPool.QueueUserWorkItem(Work);
                }
            }
#else
            if (Running || (!Running))
                Console.Write(task);
#endif
        }

        /// <summary>
        /// Runs on the Thread to keep printing on the console
        /// </summary>
        static void Work(object state)
        {
            bool keepRunning = true;

            StringBuilder toWrite = new StringBuilder();

            do
            {
                using (TimedLock.Lock(Key))
                {
                    /*if (QueuedStrings.Count > 200)
                    {
                        toWrite = "I'm really, really busy!\n";
                        QueuedStrings.Clear();
                    }
                    else*/
                        
                    toWrite.Append(QueuedStrings.Dequeue());

                    // If the queue was emptied, then end the loop
                    if (QueuedStrings.Count <= 0)
                    {
                        keepRunning = false;
                        Running = false;
                    }

                    if (toWrite.Length > 10000)
                    {
                        Console.Write(toWrite);
                        toWrite = new StringBuilder();
                    }
                }
            } while (keepRunning);

            // Note:  It's possible that things can be printed out-of-order when a lot of stuff is being printed
            if (toWrite.Length > 0)
                Console.Write(toWrite.ToString());
        }
    }
}