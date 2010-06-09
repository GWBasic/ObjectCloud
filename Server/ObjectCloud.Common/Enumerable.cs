// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using JmBucknall.Structures;

using ObjectCloud.Common.Threading;

namespace ObjectCloud.Common
{
    public static class Enumerable<T>
    {
        /// <summary>
        /// Allows iteration over many enumerables
        /// </summary>
        /// <param name="enumerables"></param>
        /// <returns></returns>
        public static IEnumerable<T> Join(params IEnumerable<T>[] enumerables)
        {
            foreach (IEnumerable<T> enumerable in enumerables)
                foreach (T val in enumerable)
                    yield return val;
        }

        /// <summary>
        /// Casts all of the objects in the enumeration to T
        /// </summary>
        /// <param name="toCast"></param>
        /// <returns></returns>
        public static IEnumerable<T> Cast(IEnumerable toCast)
        {
            foreach (object o in toCast)
                yield return (T)o;
        }
		
		/// <summary>
		/// Enumerates over all of the members of a collection in a multithreaded manner 
		/// </summary>
		/// <param name="numThreadsPerCPU">
		/// The number of threads per CPU.  This can be a fractional number.  If the total number of threads is less then a whole number, it will be rounded up
		/// </param>
		/// <param name="toEnumerate">
		/// The objects to enumerate over
		/// </param>
		/// <param name="del">
		/// The delegate called for each object
		/// </param>
		/// <returns>
		/// All of the unhandled exceptions that occured, paired with the object that triggered the exception
		/// </returns>
		public static IEnumerable<KeyValuePair<T,Exception>> MultithreadedEach(
			double numThreadsPerCPU,
		    IEnumerable<T> toEnumerate,
		    GenericArgument<T> del)
		{
            LockFreeQueue_WithCount<T> list = new LockFreeQueue_WithCount<T>();
            foreach (T t in toEnumerate)
                list.Enqueue(t);
			
			List<KeyValuePair<T,Exception>> exceptions = new List<KeyValuePair<T, Exception>>();
			
			// If there's nothing to enumerate, then just return now
			if (0 == list.Count)
				return exceptions;
				
			double numThreadsFloat = numThreadsPerCPU * Convert.ToSingle(Environment.ProcessorCount);
			int numThreads = Convert.ToInt32(numThreadsFloat);
			
			// If the number of threads is less then a whole number, increase it
			if (Convert.ToDouble(numThreads) < numThreadsFloat)
				numThreads++;
			
			// Make sure that there is at least one thread
			if (numThreads < 1)
				numThreads = 1;
			
			// Make sure that there isn't more threads then items to iterate over
			if (list.Count < numThreads)
				numThreads = Convert.ToInt32(list.Count);
			
			// Watch out for a special case on systems that have a low number of cores
			// When the number of threads per CPU is less then one, it implies that the task shouldn't use all CPUs
			if (numThreadsPerCPU < 1)
				if (numThreads >= Environment.ProcessorCount)
				{
					numThreads = Environment.ProcessorCount - 1;
				
				    if (numThreads == 0)
						numThreads = 1;
				}
			
			/* // Decent way to diagnose livelocks
			TimerCallback callback = delegate(object state)
			{
				Thread thread = (Thread)state;
				Console.WriteLine("I'm stuck!!! (ThreadID: " + thread.ManagedThreadId + ")");
				thread.Abort();
			};*/
			
			// This is the wrapper delegate used on every thread
			ThreadStart threadStart = delegate()
			{
				T t;
				
				while (list.Dequeue(out t))
					try
					{
						// Commented-out code assists in diagnosing livelocks
						//using (new Timer(callback, Thread.CurrentThread, 7500, 7500))
						//{
						del(t);
						//}
					}
					/*catch (ThreadAbortException tae)
					{
						Console.WriteLine(tae.StackTrace);
						Thread.ResetAbort();
					}*/
					catch (Exception e)
					{
						using (TimedLock.Lock(exceptions))
							exceptions.Add(new KeyValuePair<T, Exception>(t, e));
					}
			};
			
			// Allocate the threads; note that this thread also does calculations
			Thread[] threads = new Thread[numThreads - 1];
			for (int ctr = 0; ctr < (numThreads - 1); ctr++)
				threads[ctr] = new Thread(threadStart);
			
			// Start the threads
			foreach (Thread thread in threads)
				thread.Start();
			
			// Do some calculations on this thread
			threadStart();
			
			// Block until the other threads are complete
			foreach (Thread thread in threads)
				thread.Join();
			
			return exceptions;
		}
    }

    public static class Enumerable
    {
        public static bool Equals(IEnumerable l, IEnumerable r)
        {
            if (null == l && null == r)
                return true;

            if (null == l || null == r)
                return false;

            IEnumerator lE = l.GetEnumerator();

            try
            {
                IEnumerator rE = r.GetEnumerator();

                try
                {
                    return Equals(lE, rE);
                }
                finally
                {
                    if (rE is IDisposable)
                        ((IDisposable)rE).Dispose();
                }
            }
            finally
            {
                if (lE is IDisposable)
                    ((IDisposable)lE).Dispose();
            }
        }

        public static bool Equals(IEnumerator l, IEnumerator r)
        {
            do
            {
                bool lHasNext = l.MoveNext();
                bool rHasNext = r.MoveNext();

                if (lHasNext != rHasNext)
                    return false;

                if (!lHasNext)
                    return true;
            }
            while (l.Current.Equals(r.Current));

            return false;
        }
    }
}
