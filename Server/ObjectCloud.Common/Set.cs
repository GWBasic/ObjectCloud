// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.Common
{
    /// <summary>
    /// Implements Set functionality by using a dictionary indexed by T
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class HashSet<T> : IEnumerable<T>
    {
        public HashSet() 
        {
            Wrapped = new HashSet<T>();
        }

        public HashSet(IEnumerable<T> contents)
        {
            Wrapped = new HashSet<T>(contents);
        }

        private HashSet<T> Wrapped;

        /// <summary>
        /// Adds an item to the Set. 
        /// </summary>
        /// <param name="item">The object to add to the set</param>
        public void Add(T item)
        {
            Wrapped.Add(item);
        }

        /// <summary>
        /// Removes all items from the Set.
        /// </summary>
        public void Clear()
        {
            Wrapped.Clear();
        }

        /// <summary>
        /// Determines whether the Set contains a specific value.
        /// </summary>
        /// <param name="item">The object to locate in the Set.</param>
        /// <returns>The object to locate in the Set.</returns>
        public bool Contains(T item)
        {
            return Wrapped.Contains(item);
        }

        /*// <summary>
        /// Copies the elements of the Set to an Array, starting at a particular Array index. 
        /// </summary>
        /// <param name="array">The one-dimensional Array that is the destination of the elements copied from ICollection. The Array must have zero-based indexing.</param>
        /// <param name="arrayIndex">The zero-based index in array at which copying begins.</param>
        public void CopyTo(T[] array, int arrayIndex)
        {
            int ctr = arrayIndex;
            foreach (T t in this)
            {
                array[ctr] = t;
                ctr++;
            }
        }*/

        /// <summary>
        /// Gets the number of elements contained in the Set.
        /// </summary>
        public int Count
        {
            get { return Wrapped.Count; }
        }

        /// <summary>
        /// Always false
        /// </summary>
        public bool IsReadOnly
        {
            get { return false; }
        }

        /// <summary>
        /// Removes the object from the Set. 
        /// </summary>
        /// <param name="item">The object to remove from the Set.</param>
        /// <returns>true if item was successfully removed from the Set; otherwise, false. This method also returns false if item is not found in the original Set. </returns>
        public bool Remove(T item)
        {
            return Wrapped.Remove(item);
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection. 
        /// </summary>
        /// <returns></returns>
        public IEnumerator<T> GetEnumerator()
        {
            return Wrapped.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return Wrapped.GetEnumerator();
        }

        public override bool Equals(object obj)
        {
            if (obj is IEnumerable<T>)
            {
                int ctr = 0;

                foreach (T val in (IEnumerable<T>)obj)
                {
                    if (!Contains(val))
                        return false;

                    ctr++;
                }

                return ctr == Count;
            }

            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return Wrapped.GetHashCode();
        }

        public override string ToString()
        {
            return Wrapped.ToString();
        }

        public void IntersectWith(IEnumerable<T> other)
        {
            Wrapped.IntersectWith(other);
        }
    }

    /// <summary>
    /// A set of objects
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class Set : HashSet<object> { }
}