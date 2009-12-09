// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;

namespace ObjectCloud.Common
{
	/// <summary>
	/// Interface for objects that do things on a seperate thread
	/// </summary>
	public interface IRunnable
	{
		/// <summary>
		/// Method that is the ThreadStart; will be ended with Thread.Abort()
		/// </summary>
		void Run();
	}
}
