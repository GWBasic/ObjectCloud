// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.ORM.DataAccess.Generator
{
    /// <summary>
    /// Base interface for generating a block of code
    /// </summary>
    public interface ISubGenerator
    {
        /// <summary>
        /// All of the usings needed
        /// </summary>
        IEnumerable<string> Usings { get;}

        /// <summary>
        /// Generates the code
        /// </summary>
        /// <returns></returns>
        IEnumerable<string> Generate();
    }
}
