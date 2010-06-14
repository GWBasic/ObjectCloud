// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using ObjectCloud.Common.Threading;

namespace ObjectCloud.Common
{
    /// <summary>
    /// Allows even rotation through a set of resources
    /// </summary>
    public class Rotater<T>
    {
        /// <summary>
        /// Creates the rotater
        /// </summary>
        /// <param name="toRotate">The items to rotate through</param>
        public Rotater(IEnumerable<T> toRotate)
        {
            foreach (T item in toRotate)
                Queue.Enqueue(item);
        }

        private LockFreeQueue<T> Queue = new LockFreeQueue<T>();

        /// <summary>
        /// Returns the next item in rotation
        /// </summary>
        /// <returns></returns>
        public T Next()
        {
            T toReturn;
            while (!Queue.Dequeue(out toReturn))
                Thread.Sleep(0);

            Queue.Enqueue(toReturn);

            return toReturn;
        }
    }
}
