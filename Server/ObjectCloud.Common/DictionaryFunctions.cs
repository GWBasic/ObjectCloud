// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
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
	}
}
