// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;

namespace ObjectCloud.Common
{
	/// <summary>
	/// Generic object wrapper for a value; intended to be used as an alternative to "ref" and "out"
	/// </summary>
	public class Shared<T>
	{
		public Shared() 
		{
			Value = default(T);
		}
		
		public Shared(T value)
		{
			Value = value;
		}
		
		/// <value>
		/// The shared value, defaults to default(T)
		/// </value>
		public T Value
		{
			get { return _Value; }
			set { _Value = value; }
		}
		private T _Value;
	}

	/// <summary>
	/// Object wrapper for a value; intended to be used as an alternative to "ref" and "out"
	/// </summary>
	public class Shared : Shared<object> { }
}
