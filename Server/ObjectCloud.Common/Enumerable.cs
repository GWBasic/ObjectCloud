// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

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
