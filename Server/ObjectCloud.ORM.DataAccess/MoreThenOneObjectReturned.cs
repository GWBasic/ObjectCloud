// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.ORM.DataAccess
{
    /// <summary>
    /// Thrown when more then one object is returned from a query for a single object
    /// </summary>
    public class MoreThenOneObjectReturned : ORMException
    {
        public MoreThenOneObjectReturned(string message) : base(message) { }
        public MoreThenOneObjectReturned(string message, Exception inner) : base(message, inner) { }
    }
}
