// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.Common
{
    /// <summary>
    /// Allows for type-free handling of an ID, helpful for building where clauses
    /// </summary>
    public interface IID
    {
        /// <summary>
        /// The untyped value
        /// </summary>
        object Value { get;}
    }
}
