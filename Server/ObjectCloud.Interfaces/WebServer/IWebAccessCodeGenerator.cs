// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Common;

namespace ObjectCloud.Interfaces.WebServer
{
    /// <summary>
    /// Interface for objects that generate code that wraps a WebHandler
    /// </summary>
    public interface IWebAccessCodeGenerator
    {
        /// <summary>
        /// Returns an AJAX wrapper for the specific WebHandler type
        /// </summary>
        /// <typeparam name="TWebHandler"></typeparam>
        /// <returns>Enumerable of the "string" of each function.  These will need to be enclosed in { funcA, funcB, ... funcZ } </returns>
        List<string> GenerateWrapper(Set<Type> webHandlerTypes);
    }
}
