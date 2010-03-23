// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.Common
{
    /// <summary>
    /// Read-only wrapper for a dictionary
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class ReadOnlyDictionaryWrapper<TKey, TValue>
    {
        public ReadOnlyDictionaryWrapper(IDictionary<TKey, TValue> wrapped)
        {
            Wrapped = wrapped;
        }

        private IDictionary<TKey, TValue> Wrapped;

        /// <summary>
        /// Returns the value assocated with the key or throws an exception if the value isn't present
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public TValue this[TKey key]
        {
            get { return Wrapped[key];}
        }

        /// <summary>
        /// Returns true if the key is present
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool ContainsKey(TKey key)
        {
            return Wrapped.ContainsKey(key);
        }

        /// <summary>
        /// Returns true if the value is present
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return Wrapped.Contains(item);
        }
    }
}
