// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using ObjectCloud.Interfaces.Disk;

namespace ObjectCloud.Javascript.SubProcess
{
    /// <summary>
    /// Information about a scope
    /// </summary>
    public class ScopeInfo
    {
        public ScopeInfo(IFileContainer javascriptContainer, DateTime javascriptLastModified, Dictionary<string, MethodInfo> functionsInScope, IEnumerable<int> scriptIDsToBuildScope)
        {
            _JavascriptContainer = javascriptContainer;
            _JavascriptLastModified = javascriptLastModified;
            _FunctionsInScope = functionsInScope;
            _ScriptIDsToBuildScope = scriptIDsToBuildScope;
        }

        /// <summary>
        /// The file container that has the Javascript used in the sub-process
        /// </summary>
        public IFileContainer JavascriptContainer
        {
            get { return _JavascriptContainer; }
        }
        private readonly IFileContainer _JavascriptContainer;

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
        public IEnumerable<int> ScriptIDsToBuildScope
        {
            get { return _ScriptIDsToBuildScope; }
        }
        private readonly IEnumerable<int> _ScriptIDsToBuildScope;
    }
}
