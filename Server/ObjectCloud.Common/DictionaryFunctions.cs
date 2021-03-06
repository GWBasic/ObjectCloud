// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace ObjectCloud.Common
{
	/// <summary>
	/// Functions that assist in working with Dictionaries
	/// </summary>
	public static class DictionaryFunctions
	{
		/// <summary>
		/// Converts a dictionary of strings to a NameValueCollection
		/// </summary>
		/// <param name="toConvert">
		/// A <see cref="IDictionary"/>
		/// </param>
		/// <returns>
		/// A <see cref="NameValueCollection"/>
		/// </returns>
		public static NameValueCollection ToNameValueCollection(IDictionary<string, string> toConvert)
		{
			NameValueCollection toReturn = new NameValueCollection();
			
			foreach (KeyValuePair<string, string> kvp in toConvert)
				toReturn.Add(kvp.Key, kvp.Value);
			
			return toReturn;
		}

        /// <summary>
        /// Returns a dictionary object created from an enumeration of key-value pairs
        /// </summary>
        /// <typeparam name="TDictionary"></typeparam>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="values"></param>
        /// <returns></returns>
        public static Dictionary<TKey, TValue> Create<TKey, TValue>(params KeyValuePair<TKey, TValue>[] values)
        {
            return Create<TKey, TValue>(values as IEnumerable<KeyValuePair<TKey, TValue>>);
        }

        /// <summary>
        /// Returns a dictionary object created from an enumeration of key-value pairs
        /// </summary>
        /// <typeparam name="TDictionary"></typeparam>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="values"></param>
        /// <returns></returns>
        public static Dictionary<TKey, TValue> Create<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> values)
        {
            Dictionary<TKey, TValue> toReturn = new Dictionary<TKey, TValue>();

            foreach (KeyValuePair<TKey, TValue> value in values)
                toReturn[value.Key] = value.Value;

            return toReturn;
        }

        /// <summary>
        /// Returns true if the dictionaries are functionally equivilent.  (Both have the same keys and values)
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="right"></param>
        /// <param name="left"></param>
        /// <returns></returns>
        public static bool Equals<TKey, TValue>(IDictionary<TKey, TValue> right, IDictionary<TKey, TValue> left)
            where TValue : class
        {
            HashSet<TKey> rightKeys = new HashSet<TKey>(right.Keys);
            HashSet<TKey> leftKeys = new HashSet<TKey>(left.Keys);

            if (!rightKeys.Equals(leftKeys))
                return false;

            foreach (KeyValuePair<TKey, TValue> rightKvp in right)
            {
                TValue rightValue = rightKvp.Value;
                TValue leftValue = left[rightKvp.Key];

                if ((leftValue == default(TValue)) && (rightValue != default(TValue)))
                    return false;

                if (!leftValue.Equals(rightValue))
                    return false;
            }

            return true;
        }
	}
}
