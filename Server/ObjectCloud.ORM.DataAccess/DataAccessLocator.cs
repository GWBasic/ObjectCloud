// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.ORM.DataAccess
{
    /// <summary>
    /// Service locator for data access objects
    /// </summary>
    /// <typeparam name="TDatabaseConnectorFactory"></typeparam>
    public class DataAccessLocator<TDatabaseConnectorFactory>
    {
        /// <summary>
        /// Object that assists in making database connections
        /// </summary>
        public TDatabaseConnectorFactory DatabaseConnectorFactory
        {
            get { return _DatabaseConnectorFactory; }
            set { _DatabaseConnectorFactory = value; }
        }
        private TDatabaseConnectorFactory _DatabaseConnectorFactory;

        /// <summary>
        /// Object that creates new embedded databases
        /// </summary>
        public IEmbeddedDatabaseCreator DatabaseCreator
        {
            get { return _DatabaseCreator; }
            set { _DatabaseCreator = value; }
        }
        private IEmbeddedDatabaseCreator _DatabaseCreator;
    }
}
