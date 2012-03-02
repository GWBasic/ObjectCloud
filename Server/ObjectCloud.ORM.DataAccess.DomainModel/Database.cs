// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.ORM.DataAccess.DomainModel
{
    /// <summary>
    /// Represents a database
    /// </summary>
    public class Database
    {
        public Database() { }

        /// <summary>
        /// All of the tables in the database
        /// </summary>
        public List<Table> Tables
        {
            get { return tables; }
            set 
            {
                if (null == value)
                    throw new NullReferenceException("Tables");

                tables = value;
            }
        }
        private List<Table> tables = new List<Table>();

        /// <summary>
        /// Generates a dictionary of tables associated with the columns.  This does not cache the result in case
        /// any of the data structure changes
        /// </summary>
        /// <returns></returns>
        public IDictionary<Column, Table> IndexTablesByTheirColumns()
        {
            IDictionary<Column, Table> toReturn = new Dictionary<Column,Table>();

            foreach (Table table in Tables)
                foreach (Column column in table.Columns)
                    toReturn[column] = table;

            return toReturn;
        }

        /// <summary>
        /// The version
        /// </summary>
        public int Version
        {
            get { return _Version; }
            set { _Version = value; }
        }
        private int _Version = 0;
    }
}
