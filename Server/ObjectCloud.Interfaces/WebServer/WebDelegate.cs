// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Interfaces.WebServer
{
    /// <summary>
    /// Delegate for methods called on files
    /// </summary>
    /// <param name="webConnection"></param>
    /// <returns></returns>
    public delegate IWebResults WebDelegate(IWebConnection webConnection, CallingFrom callingFrom);
}