// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Javascript;

namespace ObjectCloud.Javascript.SubProcess
{
    /// <summary>
    /// Information about a scope
    /// </summary>
    public class ScopeInfo
    {
        public ScopeInfo(
            DateTime javascriptLastModified,
            Dictionary<string, MethodInfo> functionsInScope,
            IEnumerable scriptsAndIDsToBuildScope)
        {
            _JavascriptLastModified = javascriptLastModified;
            _FunctionsInScope = functionsInScope;
            _ScriptsAndIDsToBuildScope = scriptsAndIDsToBuildScope;
        }

        /// <summary>
        /// When the javascript used in this process was last modified.  If the javascript was modified, then the process will be killed
        /// </summary>
        public DateTime JavascriptLastModified
        {
            get { return _JavascriptLastModified; }
        }
        private readonly DateTime _JavascriptLastModified;

        /// <summary>
        /// The functions that are available in the scope
        /// </summary>
        public Dictionary<string, MethodInfo> FunctionsInScope
        {
            get { return _FunctionsInScope; }
        }
        readonly Dictionary<string, MethodInfo> _FunctionsInScope;

        /// <summary>
        /// The script IDs needed to construct the scope
        /// </summary>
        public IEnumerable ScriptsAndIDsToBuildScope
        {
            get { return _ScriptsAndIDsToBuildScope; }
        }
        private readonly IEnumerable _ScriptsAndIDsToBuildScope;
    }
}
