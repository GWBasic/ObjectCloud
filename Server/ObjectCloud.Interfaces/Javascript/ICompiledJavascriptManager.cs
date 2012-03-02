// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Javascript;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Interfaces.Javascript
{
    /// <summary>
    /// Interface for managing currently-compiled scripts
    /// </summary>
    public interface ICompiledJavascriptManager
    {
        /// <summary>
        /// Returns the script ID for the named script
        /// </summary>
        /// <param name="scriptNameHex">The name of the script.  The caller must ensure that this is unique</param>
        /// <param name="recompileIfOlderThen">If the pre-compiled script is older then this date, then it's recompiled</param>
        /// <param name="loadScript">Delegate used to load the script</param>
        /// <param name="subProcess">The sub process to compile or load the script in</param>
        /// <returns></returns>
        int GetScriptID(string scriptName, string md5, string script, ISubProcess subProcess);
    }
}
