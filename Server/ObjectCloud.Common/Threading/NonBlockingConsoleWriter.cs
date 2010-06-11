// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ObjectCloud.Common.Threading
{
    public static class NonBlockingConsoleWriter
    {
        /*static NonBlockingConsoleWriter()
        {
            lock (typeof(NonBlockingConsoleWriter))
                if (null == Thread)
                {
                    Thread = new Thread(ConsoleWriterFunction);
                    Thread.Name = "Console writer thread";

                    Thread.Start();
                    Thread.IsBackground = true;
                }
        }

        /// <summary>
        /// The thread that writes to the console
        /// </summary>
        static Thread Thread = null;

        /// <summary>
        /// Strings to print are queued here in a non-blocking manner
        /// </summary>
        static LockFreeQueue<string> StringsToPrint = new LockFreeQueue<string>();

        /// <summary>
        /// This is set to true if the printing thread is waiting
        /// </summary>
        static volatile bool Waiting = false;

        /// <summary>
        /// Prints the text to the console.  Does not block.  All text is queued up to be printed.  There is a small chance that text will sit in the buffer until the next write, thus it is assumed that this will be called on a regular basis
        /// </summary>
        /// <param name="toPrint"></param>
        static public void Print(string toPrint)
        {
            StringsToPrint.Enqueue(toPrint);

            if (Waiting)
                lock (StringsToPrint)
                    Monitor.Pulse(StringsToPrint);
        }

        /// <summary>
        /// This function runs in a thread to write to the console
        /// </summary>
        private static void ConsoleWriterFunction()
        {
            string toPrint;

            while (true)
            {
                StringBuilder toPrintBuilder = new StringBuilder();

                while (StringsToPrint.Dequeue(out toPrint))
                    toPrintBuilder.Append(toPrint);

                if (toPrintBuilder.Length > 0)
                    Console.Write(toPrintBuilder.ToString());

                lock (StringsToPrint)
                {
                    Waiting = true;

                    try
                    {
                        Monitor.Wait(StringsToPrint);
                    }
                    finally
                    {
                        Waiting = false;
                    }
                }
            }
        }*/

        static int Running = 0;

        /// <summary>
        /// Prints the text to the console.  Does not block.  All text is queued up to be printed.  There is a small chance that text will sit in the buffer until the next write, thus it is assumed that this will be called on a regular basis
        /// </summary>
        /// <param name="toPrint"></param>
        static public void Print(string toPrint)
        {
            QueuedStrings.Enqueue(toPrint);

            if (0 == Running)
                if (0 == Interlocked.CompareExchange(ref Running, 1, 0))
                    ThreadPool.QueueUserWorkItem(Work);
        }

        static LockFreeQueue<string> QueuedStrings = new LockFreeQueue<string>();

		/// <summary>
        /// Runs on the Thread to keep printing on the console
        /// </summary>
        static void Work(object state)
        {
            bool keepRunning = true;

            StringBuilder toWrite = new StringBuilder();

            do
            {
                string toPrint;
                if (QueuedStrings.Dequeue(out toPrint))
                    toWrite.Append(toPrint);
                else
                {
                    // If the queue was emptied, then end the loop
                    Running = 0;
                    keepRunning = false;
                }

                if (toWrite.Length > 10000)
                {
                    Console.Write(toWrite);
                    toWrite = new StringBuilder();
                }

            } while (keepRunning);

            // Note:  It's possible that things can be printed out-of-order when a lot of stuff is being printed
            if (toWrite.Length > 0)
                Console.Write(toWrite.ToString());
        }
    }
}