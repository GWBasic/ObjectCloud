// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Common.Threading;

namespace ObjectCloud.Common
{
    /// <summary>
    /// Allows for re-use of objects that are computationally complex to construct or collect
    /// </summary>
    public abstract class Recycler<T>
    {
        private LockFreeStack<T> Stack = new LockFreeStack<T>();

        /// <summary>
        /// Gets or creates an object
        /// </summary>
        /// <returns></returns>
        public T Get()
        {
            T toReturn;
            if (Stack.Pop(out toReturn))
                return toReturn;

            return Construct();
        }

        /// <summary>
        /// Constructs an object when there are no objects available for re-use
        /// </summary>
        /// <returns></returns>
        protected abstract T Construct();

        /// <summary>
        /// Holds onto an object for re-use by calling Get
        /// </summary>
        /// <param name="toRecycle"></param>
        public void Recycle(T toRecycle)
        {
            RecycleInt(toRecycle);
            Stack.Push(toRecycle);
        }

        /// <summary>
        /// Allows cleanup to occur prior to making an object available for recycling.
        /// </summary>
        /// <param name="toRecycle"></param>
        protected virtual void RecycleInt(T toRecycle)
        {
        }

        /// <summary>
        /// Clears out any recycled objects
        /// </summary>
        public void Clear()
        {
            Stack = new LockFreeStack<T>();
        }
    }
}
