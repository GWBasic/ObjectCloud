// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.ORM.DataAccess
{
    /// <summary>
    /// Base class of exceptions that come from the ORM layer
    /// </summary>
    public abstract class ORMException : Exception
    {
        public ORMException(string message) : base(message) { }
        public ORMException(string message, Exception inner) : base(message, inner) { }
    }
}
