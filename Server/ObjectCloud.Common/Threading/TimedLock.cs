// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

// based on code published at:
// http://www.interact-sw.co.uk/iangblog/2004/04/26/yetmoretimedlocking

// ADR:
// An interesting thing to do would be to add deadlock detection like what is described here:
//   http://msdn.microsoft.com/en-us/magazine/cc163352.aspx
// I'd have integrated their code into this system, except for the fact that I think MS's license
// essentially forbids using their code in an open source project.

namespace ObjectCloud.Common.Threading
{
    // Thanks to Eric Gunnerson for recommending this be a struct rather
    // than a class - avoids a heap allocation.
    // Thanks to Change Gillespie and Jocelyn Coulmance for pointing out
    // the bugs that then crept in when I changed it to use struct...
    // Thanks to John Sands for providing the necessary incentive to make
    // me invent a way of using a struct in both release and debug builds
    // without losing the debug leak tracking.

    public struct TimedLock : IDisposable
    {
        /// <value>
        /// The default timeout to use when aquireing a TimedLock.  This is 10 seconds, unless set
        /// </value>
        public static TimeSpan DefaultAquireLockTimeout
        {
            get { return _DefaultAquireLockTimeout; }
            set { _DefaultAquireLockTimeout = value; }
        }
        private static TimeSpan _DefaultAquireLockTimeout = TimeSpan.FromSeconds(10);

        /// <value>
        /// The default maximum amount of time that a lock can be held.  If the lock is held longer then this, an attempt is made to stop the thread holding the lock.  This is disabled by default
        /// </value>
        public static TimeSpan? DefaultLockAquiredTimeout
        {
            get { return _DefaultLockAquiredTimeout; }
            set { _DefaultLockAquiredTimeout = value; }
        }
        private static TimeSpan? _DefaultLockAquiredTimeout = null;

        /// <value>
        /// Default delegate called when a thread holds a lock too long.  Be default, this is TimedLock.AbortThread, which tries to abort the thread
        /// </value>
        public static LockingThreadTimeoutDelegate LockingThreadTimeoutDelegate
        {
            get { return _LockingThreadTimeoutDelegate; }
            set { _LockingThreadTimeoutDelegate = value; }
        }
        private static LockingThreadTimeoutDelegate _LockingThreadTimeoutDelegate = AbortThread;

        /// <value>
        /// Called when AbortThread can not abort a thread, defaults to AbortThreadFailed, which attempts to break into the debugger if attached
        /// </value>
        public static LockingThreadTimeoutDelegate LockingThreadAbortFailed
        {
            get { return _LockingThreadAbortFailed; }
            set { _LockingThreadAbortFailed = value; }
        }
        private static LockingThreadTimeoutDelegate _LockingThreadAbortFailed = AbortThreadFailed;

        public static TimedLock Lock(object o)
        {
            return CreateLock(o, DefaultAquireLockTimeout, DefaultLockAquiredTimeout, LockingThreadTimeoutDelegate);
        }

        public static TimedLock Lock(object o, TimeSpan timeout)
        {
            return CreateLock(o, timeout, DefaultLockAquiredTimeout, LockingThreadTimeoutDelegate);
        }

        public static TimedLock Lock(object o, TimeSpan timeout, TimeSpan lockAquiredTimeout)
        {
            return CreateLock(o, timeout, lockAquiredTimeout, LockingThreadTimeoutDelegate);
        }

        public static TimedLock Lock(object o, TimeSpan timeout, TimeSpan lockAquiredTimeout, LockingThreadTimeoutDelegate lockingThreadTimeoutDelegate)
        {
            return CreateLock(o, timeout, lockAquiredTimeout, lockingThreadTimeoutDelegate);
        }

        /// <summary>
        /// A timer used to watch for when a thread holds on to the lock too long
        /// </summary>
        private Timer Timer;

        /// <summary>
        /// The delegate used when THIS thread holds on to its lock too long
        /// </summary>
        private LockingThreadTimeoutDelegate myLockingThreadTimeoutDelegate;

/*#if DEBUG

        /// <summary>
        /// Guid that identifies the lock; this is only present in Debug mode
        /// </summary>
        public Guid ID;

#endif*/

        /// <summary>
        /// The thread that is holding the lock
        /// </summary>
        private Thread Thread;

        private static TimedLock CreateLock(object o, TimeSpan timeout, TimeSpan? aquiredLockTimeout, LockingThreadTimeoutDelegate lockingThreadTimeoutDelegate)
        {
            TimedLock toReturn = new TimedLock();

            toReturn.Thread = Thread.CurrentThread;
            toReturn.target = o;

            if (!Monitor.TryEnter(o, timeout))
            {
/*#if DEBUG
                Thread lockHolder = null; ;
                lock (LockHolders)
                    LockHolders.TryGetValue(o, out lockHolder);

                throw new LockTimeoutException(o, lockHolder);
#else*/
                throw new LockTimeoutException(o);
//#endif
            }

/*#if DEBUG
            lock (LockHolders)
                LockHolders[o] = Thread.CurrentThread;
#endif*/

            toReturn.myLockingThreadTimeoutDelegate = lockingThreadTimeoutDelegate;

            if (null != aquiredLockTimeout)
                toReturn.Timer = new Timer(toReturn.Timeout, null, Convert.ToInt32(aquiredLockTimeout.Value.TotalMilliseconds), -1);
            else
                toReturn.Timer = null;

/*#if DEBUG
            toReturn.ID = Guid.NewGuid();
            OnLockCreated(toReturn);
#endif*/

            return toReturn;
        }

/*#if DEBUG
        static Dictionary<object, Thread> LockHolders = new Dictionary<object, Thread>();
#endif*/

        /// <summary>
        /// This is the target of the lock
        /// </summary>
        public object Target
        {
            get { return target; }
        }
        private object target;

        public void Dispose()
        {
/*#if DEBUG
            lock (LockHolders)
                LockHolders.Remove(Target);
#endif*/

            Monitor.Exit(target);

            Thread = null;

            if (null != Timer)
                lock (Timer)
                    if (null != Timer)
                    {
                        Timer.Dispose();
                        Timer = null;
                    }

/*#if DEBUG
            OnLockComplete(this);
#endif*/
        }

        private void Timeout(object state)
        {
            if (null != Timer)
                lock (Timer)
                    if (null != Timer)
                    {
                        Timer.Dispose();
                        Timer = null;
                    }

            Thread thread = Thread;

            if (null != thread)
                myLockingThreadTimeoutDelegate(thread);
        }

        /// <summary>
        /// Aborts whatever thread is passed in, used when a thread holds a lock too long
        /// </summary>
        /// <param name="toAbort">
        /// A <see cref="Thread"/>
        /// </param>
        public static void AbortThread(Thread toAbort)
        {
            if (null == toAbort)
                return;

            toAbort.Abort();

            DateTime timeout = DateTime.UtcNow.AddSeconds(15);

            while (toAbort.ThreadState != ThreadState.Aborted)
            {
                if (timeout < DateTime.UtcNow)
                {
                    LockingThreadAbortFailed(toAbort);
                    return;
                }
            }
        }

        /// <summary>
        /// Attempts to hop into the debugger if a thread that is holding a lock can not be aborted
        /// </summary>
        /// <param name="toAbort">
        /// A <see cref="Thread"/>
        /// </param>
        public static void AbortThreadFailed(Thread toAbort)
        {
            if (System.Diagnostics.Debugger.IsAttached)
                System.Diagnostics.Debugger.Break();
        }

/*#if DEBUG
        /// <summary>
        /// Occurs whenever a lock is created.  This is only availalbe in debug builds
        /// </summary>
        public static event GenericArgument<TimedLock> LockCreated;

        internal static void OnLockCreated(TimedLock timedLock)
        {
            if (null != LockCreated)
                LockCreated(timedLock);
        }

        /// <summary>
        /// Occurs whenever a lock is complete.  This is only availalbe in debug builds
        /// </summary>
        public static event GenericArgument<TimedLock> LockComplete;

        internal static void OnLockComplete(TimedLock timedLock)
        {
            if (null != LockComplete)
                LockComplete(timedLock);
        }
#endif*/
    }

    /// <summary>
    /// Thrown if a there is a timeout when trying to aquire a lock
    /// </summary>
    public class LockTimeoutException : ApplicationException
    {
        /// <value>
        /// The object that was attempted to be locked
        /// </value>
        public object AttemptedToLock
        {
            get { return _AttemptedToLock; }
        }
        private readonly object _AttemptedToLock;

/*#if DEBUG

        public Thread LockHolder;

        public LockTimeoutException(object attemptedToLock, Thread lockHolder)
            : this(attemptedToLock)
        {
            LockHolder = lockHolder;
        }
#endif*/

        public LockTimeoutException(object attemptedToLock)
            : base("Timeout waiting for lock")
        {
            _AttemptedToLock = attemptedToLock;
        }
    }

    /// <summary>
    /// Delegate that's used when a thread holds onto a lock too long
    /// </summary>
    public delegate void LockingThreadTimeoutDelegate(Thread blockingThread);
}