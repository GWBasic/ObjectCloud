// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using Jint;
using Jint.Native;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Javascript.Jint
{
    /// <summary>
    /// Provides context about the state of a function call
    /// </summary>
    public struct FunctionCallContext
    {
        public CallingFrom CallingFrom
        {
            get { return _CallingFrom; }
        }
        private CallingFrom _CallingFrom;

        public ScopeWrapper ScopeWrapper
        {
            get { return _ScopeWrapper; }
        }
        private ScopeWrapper _ScopeWrapper;

        public IWebConnection WebConnection
        {
            get { return _WebConnection; }
        }
        private IWebConnection _WebConnection;

        /// <summary>
        /// Attempts to find the current ScopeWrapper by looking at ThreadStatic values in FunctionCaller.
        /// </summary>
        /// <returns></returns>
        public static FunctionCallContext GetCurrentContext()
        {
            FunctionCaller current = FunctionCaller.Current;

            if (null == current)
                throw new JavascriptException("The current Javascript scope is not within the context of a function call, thus the current ScopeWrapper can not be found");

            FunctionCallContext toReturn = new FunctionCallContext();
            toReturn._ScopeWrapper = current.ScopeWrapper;
            toReturn._WebConnection = FunctionCaller.WebConnection;
            toReturn._CallingFrom = FunctionCaller.CallingFrom;

            return toReturn;
        }
    }
}
