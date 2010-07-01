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
        /// <summary>
        /// Queue of strings to print
        /// </summary>
        static LockFreeQueue<string> QueuedStrings = new LockFreeQueue<string>();

        static NonBlockingConsoleWriter()
        {
            EventHandler<LockFreeQueue<string>, EventArgs> itemAddedToEmptyQueue = delegate(LockFreeQueue<string> q, EventArgs e)
            {
                ThreadPool.QueueUserWorkItem(Work);
            };

            QueuedStrings.ItemAddedToEmptyQueue += itemAddedToEmptyQueue;
        }

        /// <summary>
        /// Prints the text to the console.  Does not block.  All text is queued up to be printed.  There is a small chance that text will sit in the buffer until the next write, thus it is assumed that this will be called on a regular basis
        /// </summary>
        /// <param name="toPrint"></param>
        static public void Print(string toPrint)
        {
            QueuedStrings.Enqueue(toPrint);
        }

        /// <summary>
        /// Runs on the Thread to keep printing on the console
        /// </summary>
        static void Work(object state)
        {
            StringBuilder toWrite = new StringBuilder(3000);

            string toPrint;
            while (QueuedStrings.Dequeue(out toPrint))
            {
                toWrite.Append(toPrint);

                if (toWrite.Length > 2500)
                {
                    Console.Write(toWrite);
                    toWrite = new StringBuilder(3000);
                }
            }

            if (toWrite.Length > 0)
                Console.Write(toWrite.ToString());
        }
    }
}