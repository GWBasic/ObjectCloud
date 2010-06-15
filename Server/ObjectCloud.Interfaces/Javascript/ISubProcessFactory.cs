// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Javascript;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Interfaces.Javascript
{
    /// <summary>
    /// Interface for objects that construct and divy sub processes
    /// </summary>
    public interface ISubProcessFactory
    {
        /// <summary>
        /// Returns a sub process that can provide a scope for the given javascript container
        /// </summary>
        /// <returns></returns>
        ISubProcess GetSubProcess();

        /// <summary>
        /// Returns a unique scope ID
        /// </summary>
        /// <returns></returns>
        int GenerateScopeId();
    }
}
