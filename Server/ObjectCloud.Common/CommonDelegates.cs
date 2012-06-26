// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

namespace ObjectCloud.Common
{
    public delegate void EventHandler<TSender, TEventArgs>(TSender sender, TEventArgs e)
        where TEventArgs : System.EventArgs;
}