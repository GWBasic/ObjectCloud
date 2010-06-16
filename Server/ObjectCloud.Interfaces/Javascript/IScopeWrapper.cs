// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Interfaces.Javascript
{
    /// <summary>
    /// Interface for an object that wraps a scope
    /// </summary>
    public interface IScopeWrapper : IDisposable
    {
        IFileContainer FileContainer { get; }
        object GetParentDirectoryWrapper(IWebConnection webConnection);
        object Open(IWebConnection webConnection, string toOpen);
        int ScopeId { get; }
        ISubProcess SubProcess { get; }
        object Use(IWebConnection webConnection, string toLoad);

        /// <summary>
        /// Evaluates the given script
        /// </summary>
        /// <param name="scriptIDsAndScripts">The scripts and precompiled script IDs to execute</param>
        /// <param name="webConnection">The web connection</param>
        /// <returns></returns>
        object[] EvalScope(IWebConnection webConnection, IEnumerable scriptIDsAndScripts);
    }
}
