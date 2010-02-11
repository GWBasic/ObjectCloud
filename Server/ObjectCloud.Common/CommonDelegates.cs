// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

namespace ObjectCloud.Common
{
	/// <summary>
	/// A delegate that takes no arguments and returns no values
	/// </summary>
	public delegate void GenericVoid();

	/// <summary>
	/// A delegate that takes a generic argument and returns a void
	/// </summary>
	public delegate void GenericArgument<T>(T arg);

	/// <summary>
	/// A delegate that takes a no argument and returns a generic
	/// </summary>
	public delegate R GenericReturn<R>();

	/// <summary>
	/// A delegate that takes a generic argument and returns a generic
	/// </summary>
	public delegate R GenericArgumentReturn<T, R>(T arg);

    public delegate void EventHandler<TSender, TEventArgs>(TSender sender, TEventArgs e)
        where TEventArgs : System.EventArgs;
}