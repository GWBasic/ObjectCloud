// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ObjectCloud.Common
{
    public struct Timeout : IDisposable
    {
        /// <summary>
        /// The timer
        /// </summary>
        private Timer Timer;

        /// <summary>
        /// The thread
        /// </summary>
        private Thread Thread;

        /// <summary>
        /// The delegate called when there is a timeout
        /// </summary>
        LockingThreadTimeoutDelegate LockingThreadTimeoutDelegate;

        public static Timeout RunMax(TimeSpan timeSpan, LockingThreadTimeoutDelegate lockingThreadTimeoutDelegate)
        {
            Timeout toReturn = new Timeout();

            toReturn.Thread = Thread.CurrentThread;
            toReturn.LockingThreadTimeoutDelegate = lockingThreadTimeoutDelegate;
            toReturn.Timer = new Timer(toReturn.HandleTimeout, null, Convert.ToInt32(timeSpan.TotalMilliseconds), System.Threading.Timeout.Infinite);

            return toReturn;
        }

        /// <summary>
        /// Called by the timer
        /// </summary>
        /// <param name="state"></param>
        private void HandleTimeout(object state)
        {
            try
            {
                if (null != Timer)
                    LockingThreadTimeoutDelegate(Thread);
            }
            finally
            {
                Dispose();
            }
        }

        public void Dispose()
        {
            if (null != Timer)
                lock (Timer)
                    if (null != Timer)
                    {
                        Timer.Dispose();
                        Timer = null;
                    }
        }
    }
}
