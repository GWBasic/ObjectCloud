// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Common;

namespace ObjectCloud.Interfaces.WebServer
{
    /// <summary>
    /// Interface for objects that resolve web components
    /// </summary>
    public interface IWebComponentResolver
    {
        /// <summary>
        /// Resolves all web components.  In the event that the WebComponent syntax is incorrect, no exceptions should occur.  Incorrect syntax will
        /// be left as-is
        /// </summary>
        /// <param name="toResolve"></param>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        string ResolveWebComponents(string toResolve, IWebConnection webConnection);

        /// <summary>
        /// Given an enumeration of script names, this returns ALL of the scripts that are needed, and their MD5s for caching.  This
        /// recursively inspects scripts to find dependant scripts underneath
        /// </summary>
        /// <param name="requestedScripts"></param>
        /// <returns></returns>
        IEnumerable<ScriptAndMD5> DetermineDependantScripts(IEnumerable<string> requestedScripts, IWebConnection webConnection);
    }

    /// <summary>
    /// Encapsulation of a script and its MD5
    /// </summary>
    public struct ScriptAndMD5
    {
        /// <summary>
        /// A script's MD5.  This is used to instruct the browser to cache the script for a very long time
        /// </summary>
        public string MD5;

        /// <summary>
        /// The actual name of the script
        /// </summary>
        public string ScriptName;
    }
}
