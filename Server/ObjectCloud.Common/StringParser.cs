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
    }
}
