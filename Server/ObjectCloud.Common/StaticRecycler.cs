// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Common.Threading;

namespace ObjectCloud.Common
{
    /// <summary>
    /// Allows re-use of objects that are computationally complex to generate.  This is thread-safe
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public static class StaticRecycler<T>
        where T : new()
    {
        private static LockFreeStack<T> Stack = new LockFreeStack<T>();

        /// <summary>
        /// Gets or creates an object
        /// </summary>
        /// <returns></returns>
        public static T Get()
        {
            T toReturn;
            if (Stack.Pop(out toReturn))
                return toReturn;

            return new T();
        }

        /// <summary>
        /// Holds onto an object for re-use by calling Get
        /// </summary>
        /// <param name="toRecycle"></param>
        public static void Recycle(T toRecycle)
        {
            Stack.Push(toRecycle);
        }

        /// <summary>
        /// Clears out any recycled objects
        /// </summary>
        public static void Clear()
        {
            Stack = new LockFreeStack<T>();
        }
    }
}
