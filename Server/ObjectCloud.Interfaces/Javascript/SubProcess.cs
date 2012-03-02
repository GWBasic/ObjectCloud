// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ObjectCloud.Interfaces.Javascript
{
    /// <summary>
    /// The results of calling EvalScope
    /// </summary>
    public class EvalScopeResults
    {
        /// <summary>
        /// The call's results
        /// </summary>
        public List<object> Results;

        /// <summary>
        /// The functions that are present in the scope
        /// </summary>
        public Dictionary<string, CreateScopeFunctionInfo> Functions;
    }

    /// <summary>
    /// Information about a function
    /// </summary>
    public struct CreateScopeFunctionInfo
    {
        /// <summary>
        /// The function's properties
        /// </summary>
        public Dictionary<string, object> Properties;

        /// <summary>
        /// The function's arguments
        /// </summary>
        public IEnumerable<string> Arguments;
    }

    /// <summary>
    /// Delegate for calling parent functions
    /// </summary>
    /// <param name="functionName"></param>
    /// <param name="arguments"></param>
    /// <returns></returns>
    public delegate object CallParentFunctionDelegate(string functionName, object threadId, object[] arguments);

    /// <summary>
    /// Represents an undefined value
    /// </summary>
    public class Undefined
    {
        private Undefined() { }

        public static Undefined Value
        {
            get { return _Instance; }
        }
        private static readonly Undefined _Instance = new Undefined();
    }
}
