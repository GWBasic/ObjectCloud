using System;
using System.Collections.Generic;
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
        /// Decodes an XmlEncoded string
        /// </summary>
        /// <param name="toDecode"></param>
        /// <returns></returns>
        public static string XmlDecode(string toDecode)
        {
            return toDecode.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"").Replace("&apos;", "'").Replace("&amp;", "&");
        }

        /// <summary>
        /// Encodes a string for xml
        /// </summary>
        /// <param name="toDecode"></param>
        /// <returns></returns>
        public static string XmlEncode(string toEncode)
        {
            return toEncode.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
        }
    }
}
