// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.Common
{
    /// <summary>
    /// Strongly-typed generic wrappers for the Enum class
    /// </summary>
    /// <typeparam name="TEnum"></typeparam>
    public static class Enum<TEnum>
        where TEnum : struct
    {
        /// <summary>
        /// Returns the enum parsed from value
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static TEnum Parse(string value)
        {
            return (TEnum)Enum.Parse(typeof(TEnum), value);
        }
		
        /// <summary>
        /// Tries to parse the enum, returns null if it can not be parsed
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
		public static TEnum? TryParse(string value)
		{
			if (Enum.IsDefined(typeof(TEnum), value))
				return Parse(value);
			
			return null;
		}

        /// <summary>
        /// Tries to parse the enum, assigns it to target if it can be parsed
        /// </summary>
        /// <param name="value"></param>
        /// <param name="target"></param>
        public static bool TryParse(string value, out TEnum target)
        {
            if (null == value)
            {
                target = default(TEnum);
                return false;
            }
            else if (Enum.IsDefined(typeof(TEnum), value))
            {
                target = Parse(value);
                return true;
            }

            target = default(TEnum);
            return false;
        }

        /// <summary>
        /// Retrieves an array of the values of the constants in a specified enumeration. 
        /// </summary>
        /// <returns>An Array of the values of the constants in enumType. The elements of the array are sorted by the values of the enumeration constants.</returns>
        public static TEnum[] GetValues()
        {
            Array values = Enum.GetValues(typeof(TEnum));

            TEnum[] toReturn = new TEnum[values.Length];

            for (int ctr = 0; ctr < values.Length; ctr++)
                toReturn[ctr] = (TEnum)values.GetValue(ctr);

            return toReturn;
        }

        /// <summary>
        /// All of the enumerations values.  This is cached so it can be called repeatedly
        /// </summary>
        public static IEnumerable<TEnum> Values
        {
            get 
            {
                if (null == _Values)
                    _Values = GetValues();

                foreach (TEnum value in _Values)
                    yield return value;
            }
        }
        private static IEnumerable<TEnum> _Values;

        /// <summary>
        /// Returns an indication whether a constant with a specified value exists in a specified enumeration. 
        /// </summary>
        /// <param name="value">The value or name of a constant in TEnum.</param>
        /// <returns>true if a constant in TEnum has a value equal to value; otherwise, false. </returns>
        public static bool IsDefined(object value)
        {
            return Enum.IsDefined(typeof(TEnum), value);
        }
    }
}
