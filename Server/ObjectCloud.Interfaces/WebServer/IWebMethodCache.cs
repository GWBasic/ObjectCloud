// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.Interfaces.WebServer
{
    public interface IWebMethodCache
    {
        /// <summary>
        /// Returns the WebDelegate for the given method name
        /// </summary>
        /// <param name="methodName"></param>
        /// <returns></returns>
        WebDelegate this[MethodNameAndFileContainer methodNameAndHandler] { get;}
    }
}
