using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace ObjectCloud.Common
{
    /// <summary>
    /// Functions to assist with Guids
    /// </summary>
    public static class GuidFunctions
    {
        /// <summary>
        /// Regex for parsing guids
        /// </summary>
        private static readonly Regex GuidRegex = new Regex("^[A-Fa-f0-9]{32}$|^({|\\()?[A-Fa-f0-9]{8}-([A-Fa-f0-9]{4}-){3}[A-Fa-f0-9]{12}(}|\\))?$|^({)?[0xA-Fa-f0-9]{3,10}(, {0,1}[0xA-Fa-f0-9]{3,6}){2}, {0,1}({)([0xA-Fa-f0-9]{3,4}, {0,1}){7}[0xA-Fa-f0-9]{3,4}(}})$");

        /// <summary>
        /// Attempts to parse a guid
        /// </summary>
        /// <param name="value"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        public static bool TryParse(string value, out Guid result)
        {
            if (IsGuid(value))
            {
                result = new Guid(value);
                return true;
            }
            else
            {
                result = default(Guid);
                return false;
            }
        }

        /// <summary>
        /// Determines if the passed in string is a guid
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool IsGuid(string value)
        {
            return GuidRegex.IsMatch(value);
        }
    }
}
