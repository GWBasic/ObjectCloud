// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.Interfaces.Security
{
    /// <summary>
    /// Indicates if a call originated from a web or local
    /// </summary>
    public enum CallingFrom
    {
        /// <summary>
        /// Indicates that the call originates from an untrusted source
        /// </summary>
        Web,

        /// <summary>
        /// Indicates that the originates from a trusted source and its data is not malicous
        /// </summary>
        Local
    }
}
