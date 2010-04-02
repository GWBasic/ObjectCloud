// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.Javascript.SubProcess
{
    /// <summary>
    /// Interface for objects that construct and divy sub processes
    /// </summary>
    public interface ISubProcessFactory
    {
        /// <summary>
        /// Returns a sub process using whatever algoritm is used to divy up sub processes
        /// </summary>
        /// <returns></returns>
        SubProcess GetSubProcess();
    }
}
