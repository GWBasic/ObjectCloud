// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.Interfaces.WebServer
{
    /// <summary>
    /// Interface for objects that have a WebResults object.  This is primarily intended for
    /// exceptions that can override the result
    /// </summary>
    public interface IHasWebResults
    {
        IWebResults WebResults { get;}
    }
}
