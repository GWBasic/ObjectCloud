// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ObjectCloud.Common
{
	/// <summary>
	/// Not quite a reader / writer lock, but instead lets a reader delay a write lock for a short period of time so it can safely read
	/// </summary>
	public class WeakLock
	{
		private object Key = new object();
		
		private volatile bool LockAquired = false;

        /// <summary>
        /// When the next wite lock is allowed
        /// </summary>
        private DateTime NextWritelock = DateTime.MinValue;
		
		/// <summary>
		/// Blocks while there is a lock.  After calling this function, the resource will be read-safe for 25 miliseconds, or whatever is set in LockDelay
		/// </summary>
		public void PeekRead()
		{
			// Block while there is a writer active
			while (LockAquired)
                lock (Key)
                { }

            // A write lock won't be allowed until after the given instant
            NextWritelock = DateTime.UtcNow + LockDelay;
		}
		
        /// <summary>
        /// Helps end a write lock by allowing the caller to use the "using" syntax
        /// </summary>
        private struct WriteLockCompleter : IDisposable
        {
            internal WriteLockCompleter(WeakLock me)
            {
                Me = me;
            }

            WeakLock Me;

            public void Dispose()
            {
				Me.LockAquired = false;
                Monitor.Exit(Me.Key);
            }
        }

        /// <summary>
        /// Locks, but delays returning if PeekRead was called recently
        /// </summary>
        /// <returns></returns>
        public IDisposable Lock()
        {
            return Lock(TimedLock.DefaultAquireLockTimeout);
        }
		
		/// <summary>
		/// The amount of time that a lock is delayed to give readers a chance to read
		/// </summary>
		public TimeSpan LockDelay 
		{
			get { return _LockDelay; }
			set { _LockDelay = value; }
		}
		private TimeSpan _LockDelay = TimeSpan.FromMilliseconds(25);

        /// <summary>
        /// Locks, but delays returning if PeekRead was called recently
        /// </summary>
        /// <returns></returns>
        public IDisposable Lock(TimeSpan timeout)
        {
            if (!Monitor.TryEnter(Key, timeout))
                throw new TimeoutException("Timeout establishing a lock");
			
			LockAquired = true;

            // Sleep until any required delay from readers passes
            DateTime nextWritelock = NextWritelock;
            DateTime now = DateTime.UtcNow;
            while (nextWritelock < now)
            {
                Thread.Sleep(now - nextWritelock);
                nextWritelock = NextWritelock;
                now = DateTime.UtcNow;
            }

			// Only sleep based on a set value
			Thread.Sleep(LockDelay);
			
            return new WriteLockCompleter(this);
        }
	}
}
