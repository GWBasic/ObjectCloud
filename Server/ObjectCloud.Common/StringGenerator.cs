// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.Common
{
    /// <summary>
    /// Methods to assist in string generation
    /// </summary>
    public static class StringGenerator
    {
        /// <summary>
        /// Generates a comma-seperated list of each item in toEnumerate
        /// </summary>
        /// <param name="toEnumerate"></param>
        /// <returns></returns>
        public static string GenerateCommaSeperatedList(IEnumerable toEnumerate)
        {
            return GenerateSeperatedList(toEnumerate, ", ");
        }

        /// <summary>
        /// Generates a list of each item in toEnumerate seperated by seperator
        /// </summary>
        /// <param name="toEnumerate"></param>
        /// <param name="seperator"></param>
        /// <returns></returns>
        public static string GenerateSeperatedList(IEnumerable toEnumerate, string seperator)
        {
            IEnumerator enumerator = toEnumerate.GetEnumerator();

            // Handle case where there are no items
            if (!enumerator.MoveNext())
                return "";

            // Initialise the string builder to be the first item
            StringBuilder toReturn = new StringBuilder(enumerator.Current.ToString());

            // while there are additional items, keep adding them
            while (enumerator.MoveNext())
            {
                toReturn.Append(seperator);
                toReturn.Append(enumerator.Current.ToString());
            }

            return toReturn.ToString();
        }

        /// <summary>
        /// Generates a type name to use from the given type
        /// </summary>
        /// <param name="mtype"></param>
        /// <returns></returns>
        public static string GenerateTypeName(Type type)
        {
            if (type.IsGenericType)
            {
                List<string> genericParameterNames = new List<string>();

                foreach (Type subType in type.GetGenericArguments())
                    genericParameterNames.Add(GenerateTypeName(subType));

                return type.FullName.Split('`')[0] + "<" + GenerateCommaSeperatedList(genericParameterNames) + ">";
            }
            else
                return type.FullName;
        }

        /// <summary>
        /// Adds " quotes to each string
        /// </summary>
        /// <param name="toQuote"></param>
        /// <returns></returns>
        public static IEnumerable<string> AddLargeQuotes(IEnumerable toQuote)
        {
            return WrapStrings(toQuote, "\"", "\"");
        }

        /// <summary>
        /// Adds ' quotes to each string
        /// </summary>
        /// <param name="toQuote"></param>
        /// <returns></returns>
        public static IEnumerable<string> AddSmallQuotes(IEnumerable toQuote)
        {
            return WrapStrings(toQuote, "'", "'");
        }

        /// <summary>
        /// Wraps each string with the given prefix and suffix
        /// </summary>
        /// <param name="toWrap"></param>
        /// <param name="prefix"></param>
        /// <param name="postfix"></param>
        /// <returns></returns>
        public static IEnumerable<string> WrapStrings(IEnumerable toWrap, string prefix, string postfix)
        {
            foreach (object subToWrap in toWrap)
                yield return string.Format("{0}{1}{2}", prefix, subToWrap.ToString(), postfix);
        }

        /// <summary>
        /// Quick-and-dirty way to convert a byte array to hex, see http://www.daveandrews.org/2009/09/10/convert-byte-array-to-hexidecimal-string-in-c/
        /// </summary>
        /// <param name="array"></param>
        /// <returns></returns>
        public static string ToHexString(byte[] array)
        {
            string vals = "0123456789ABCDEF";
            StringBuilder result = new StringBuilder();

            for (int i = 0; i < array.Length; i++)
            {
                result.Append(vals[array[i] >> 4]);
                result.Append(vals[array[i] & 15]);
            }

            return result.ToString();
        }
    }
}
