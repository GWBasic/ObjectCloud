// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.Common
{
    /// <summary>
    /// Helper functions for arrays
    /// </summary>
    public static class Array<T>
    {
        /// <summary>
        /// Shallow-copies an array.  The new array points to the same contents as the old array
        /// </summary>
        /// <param name="toCopy"></param>
        /// <returns></returns>
        public static T[] ShallowCopy(T[] toCopy)
        {
            T[] toReturn = new T[toCopy.LongLength];
            toCopy.CopyTo(toReturn, 0);

            return toCopy;
        }

        /// <summary>
        /// Copies an array by calling Clone() on all of the elements if they implement ICloneable
        /// </summary>
        /// <param name="toCopy"></param>
        /// <returns></returns>
        public static T[] CloneCopy(T[] toCopy)
        {
            T[] toReturn = new T[toCopy.LongLength];

            for (long ctr = 0; ctr < toCopy.LongLength; ctr++)
            {
                T elementToCopy = toCopy[ctr];
                T elementToAssign;

                if (elementToCopy is ICloneable)
                {
                    object cloneResult = ((ICloneable)elementToCopy).Clone();

                    if (cloneResult is T)
                        elementToAssign = (T)cloneResult;
                    else
                        elementToAssign = elementToCopy;
                }
                else
                    elementToAssign = elementToCopy;

                toReturn[ctr] = elementToAssign;
            }

            toCopy.CopyTo(toReturn, 0);

            return toCopy;
        }

        /// <summary>
        /// Searches for the first occurance of toFind in array
        /// </summary>
        /// <param name="array"></param>
        /// <param name="toFind"></param>
        /// <returns>The index where toFind starts, or, -1 if toFind doesn't occur</returns>
        public static int IndexOf(T[] array, T[] toFind)
        {
            return IndexOf(array, toFind, 0, array.Length);
        }

        /// <summary>
        /// Searches for the next occurance of toFind in array after startIndex
        /// </summary>
        /// <param name="array"></param>
        /// <param name="toFind"></param>
        /// <returns>The index where toFind starts, or, -1 if toFind doesn't occur</returns>
        public static int IndexOf(T[] array, T[] toFind, int startIndex)
        {
            return IndexOf(array, toFind, startIndex, array.Length);
        }

        /// <summary>
        /// Searches for the next occurance of toFind in array after startIndex
        /// </summary>
        /// <param name="array"></param>
        /// <param name="toFind"></param>
        /// <returns>The index where toFind starts, or, -1 if toFind doesn't occur</returns>
        public static int IndexOf(T[] array, T[] toFind, int startIndex, int stopIndex)
        {
            if (toFind.LongLength > array.LongLength)
                return -1;

            stopIndex = stopIndex - toFind.Length;

            for (int ctr = startIndex; ctr <= stopIndex; ctr++)
                if (IsMatchAt(array, toFind, ctr))
                    return ctr;

            // Not found
            return -1;
        }

        /// <summary>
        /// Returns true if array contains a match of toMatch at the index
        /// </summary>
        /// <param name="array"></param>
        /// <param name="toMatch"></param>
        /// <param name="index"></param>
        /// <exception cref="IndexOutOfRange">Thrown if toMatch's length is such that it extends beyond array at the given search index</exception>
        /// <returns></returns>
        public static bool IsMatchAt(T[] array, T[] toMatch, int index)
        {
            for (long ctr = 0; ctr < toMatch.LongLength; ctr++)
                if (!(EqualityComparer<T>.Default.Equals(array[index + ctr], toMatch[ctr])))
                    return false;

            return true;
        }
    }
}
