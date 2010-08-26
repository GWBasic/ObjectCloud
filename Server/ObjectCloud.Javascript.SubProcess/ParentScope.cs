// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading;

using Common.Logging;
using JsonFx.Json;

using ObjectCloud.Common;
using ObjectCloud.Common.Threading;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Javascript.SubProcess
{
    public class ParentScope
    {
        private static int ParentScopeIDctr = 0;

        public ParentScope(
            IEnumerable<KeyValuePair<IFileContainer, DateTime>> loadedScriptsModifiedTimes,
            Dictionary<string, MethodInfo> functionsInScope)
        {
            _ParentScopeId = Interlocked.Increment(ref ParentScopeIDctr);
            _LoadedScriptsModifiedTimes = loadedScriptsModifiedTimes;
            _FunctionsInScope = functionsInScope;

            foreach (KeyValuePair<IFileContainer, DateTime> kvp in loadedScriptsModifiedTimes)
                if (kvp.Key.FileHandler is ITextHandler)
                    kvp.Key.CastFileHandler<ITextHandler>().ContentsChanged += new EventHandler<ITextHandler, EventArgs>(ParentScope_ContentsChanged);
        }

        void ParentScope_ContentsChanged(ITextHandler sender, EventArgs e)
        {
            _StillValid = false;

            // If code changed within the scope, then reset the execution environment so it'll be recreated next time its used
            IWebHandler webHandler;
            while (WebHandlersWithThisAsParent.Dequeue(out webHandler))
                webHandler.ResetExecutionEnvironment();
        }

        public int ParentScopeId
        {
            get { return _ParentScopeId; }
        }
        private readonly int _ParentScopeId;

        public IEnumerable<KeyValuePair<IFileContainer, DateTime>> LoadedScriptsModifiedTimes
        {
            get { return _LoadedScriptsModifiedTimes; }
        }
        private readonly IEnumerable<KeyValuePair<IFileContainer, DateTime>> _LoadedScriptsModifiedTimes;

        public Dictionary<string, MethodInfo> FunctionsInScope
        {
            get { return _FunctionsInScope; }
        }
        readonly Dictionary<string, MethodInfo> _FunctionsInScope = new Dictionary<string, MethodInfo>();

        public LockFreeQueue<IWebHandler> WebHandlersWithThisAsParent
        {
            get { return _WebHandlersWithThisAsParent; }
        }
        private readonly LockFreeQueue<IWebHandler> _WebHandlersWithThisAsParent = new LockFreeQueue<IWebHandler>();

        /// <summary>
        /// Set to false if the parent scope no longer is valid
        /// </summary>
        public bool StillValid
        {
            get { return _StillValid; }
        }
        private bool _StillValid = true;
    }
}
