// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

﻿using System;
using System.Collections;
using System.Collections.Generic;

namespace ObjectCloud.Interfaces.Javascript
{
    public interface ISubProcess
    {
        bool Alive { get; }
        object CallCallback(int scopeId, object threadID, object callbackId, System.Collections.IEnumerable arguments);
        object CallFunctionInScope(int scopeId, object threadID, string functionName, System.Collections.IEnumerable arguments);
        object Compile(object threadID, string script, int scriptID);
        void Dispose();
        void DisposeScope(int scopeId, object threadID);
        
        /// <summary>
        /// Evaluates the given script
        /// </summary>
        /// <param name="scopeId"></param>
        /// <param name="threadID"></param>
        /// <param name="metadata"></param>
        /// <param name="scriptIDsAndScripts">Script IDs and scripts</param>
        /// <param name="functionsToAdd"></param>
        /// <param name="returnFunctions"></param>
        /// <returns></returns>
        EvalScopeResults EvalScope(
            int scopeId,
            object threadID,
            Dictionary<string, object> metadata,
            IEnumerable scriptIDsAndScripts,
            IEnumerable<string> functionsToAdd,
            bool returnFunctions);
        
        void LoadCompiled(object threadID, object preCompiled, int scriptID);
        System.Diagnostics.Process Process { get; }
        void RegisterParentFunctionDelegate(int scopeId, CallParentFunctionDelegate parentFunctionDelegate);
    }
}
