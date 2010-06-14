// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace ObjectCloud.Common
{
    /// <summary>
    /// Helper functions for parsing strings
    /// </summary>
    public static class StringParser
    {
        /// <summary>
        /// Helps parse a string
        /// </summary>
        /// <param name="toParse"></param>
        /// <param name="seperator"></param>
        /// <returns></returns>
        public static IEnumerable<string> Parse(string toParse, string[] seperator)
        {
            foreach (string parsed in toParse.Split(seperator, StringSplitOptions.RemoveEmptyEntries))
                yield return parsed.Trim();
        }

        /// <summary>
        /// Parses a comma-seperated list
        /// </summary>
        /// <param name="toParse"></param>
        /// <returns></returns>
        public static IEnumerable<string> ParseCommaSeperated(string toParse)
        {
            return Parse(toParse, new String[] {","});
        }

        /// <summary>
        /// Returns the MD5 of the string as a string
        /// </summary>
        /// <param name="toHash"></param>
        /// <returns></returns>
        public static string GenerateMD5String(string toHash)
        {
            MD5CryptoServiceProvider hashAlgorithm = Recycler<MD5CryptoServiceProvider>.Get();

            byte[] scriptHash;
            try
            {
                scriptHash = hashAlgorithm.ComputeHash(Encoding.Unicode.GetBytes(toHash));
            }
            finally
            {
                // Save the hash calculator for reuse
                Recycler<MD5CryptoServiceProvider>.Recycle(hashAlgorithm);
            }

            return Convert.ToBase64String(scriptHash);
        }
    }
}
