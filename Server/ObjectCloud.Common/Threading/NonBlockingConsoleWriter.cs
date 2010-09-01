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
        /// The queue that prints to the console
        /// </summary>
        private static DelegateQueue DelegateQueue = new DelegateQueue("Console Writer");

        /// <summary>
        /// Stops the thread used to print to the console.
        /// </summary>
        static public void EndThread()
        {
            DelegateQueue.Stop();
        }

        /// <summary>
        /// Prints the text to the console.  Does not block.  Starts a thread if one isn't started
        /// </summary>
        /// <param name="toPrint"></param>
        static public void Print(string toPrint)
        {
            try
            {
                DelegateQueue.QueueUserWorkItem(delegate(object state)
                {
                    Console.Write(toPrint);
                });
            }
            catch (ObjectDisposedException) { }
        }
    }
}