// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.ORM.DataAccess
{
    /// <summary>
    /// Interface for objects that create an embedded database
    /// </summary>
    public interface IEmbeddedDatabaseCreator
    {
        /// <summary>
        /// Creates an embedded database with the given filename (or directory)
        /// </summary>
        /// <param name="filename"></param>
        /// <returns>A connection to the newly-created database.  This connection must be disposed</returns>
        void Create(string filename);
    }
}
