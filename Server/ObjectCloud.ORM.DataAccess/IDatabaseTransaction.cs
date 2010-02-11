// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.ORM.DataAccess
{
    /// <summary>
    /// Encapsulates all changes made to the database in a form that can be rolled back
    /// </summary>
    public interface IDatabaseTransaction : IDisposable
    {
        /// <summary>
        /// Commits all data changes currently made during the transaction
        /// </summary>
        void Commit();

        /// <summary>
        /// Rolls back all data changes made during the transaction
        /// </summary>
        void Rollback();
    }
}
