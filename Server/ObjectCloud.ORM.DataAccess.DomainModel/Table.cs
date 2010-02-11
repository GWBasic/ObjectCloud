// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.ORM.DataAccess.DomainModel
{
    public class Table
    {
        public Table() { }
        public Table(string name, IEnumerable<Column> columns)
        {
            Name = name;

            if (null == columns)
                throw new NullReferenceException("Columns");

            Columns.AddRange(columns);
        }

        public Table(string name, Column primaryKey)
            : this(name, primaryKey, new Column[0]) { }

        public Table(string name, Column primaryKey, IEnumerable<Column> columns)
            : this(name, columns)
        {
            if (null == primaryKey)
                throw new NullReferenceException("PrimaryKey");

            Columns.Add(primaryKey);
            PrimaryKey = primaryKey;
        }

        /// <summary>
        /// The table's name
        /// </summary>
        public string Name
        {
            get { return name; }
            set 
            {
                if (null == value)
                    throw new NullReferenceException("Name");
                
                name = value; 
            }
        }
        private string name;

        /// <summary>
        /// All of the columns in the table
        /// </summary>
        public List<Column> Columns
        {
            get { return columns; }
            set 
            {
                if (null == value)
                    throw new NullReferenceException("Columns");
                
                columns = value; 
            }
        }
        private List<Column> columns = new List<Column>();

        /// <summary>
        /// The table's primary key.  This must be a member of the columns.  Only one primary key is allowed.
        /// If the prevously-set primary key isn't in the columns when this property is accessed, then the
        /// primary key is set to null
        /// </summary>
        public Column PrimaryKey
        {
            get 
            {
                if (null == _PrimaryKey)
                    return null;

                if (!columns.Contains(_PrimaryKey))
                    _PrimaryKey = null;

                return _PrimaryKey; 
            }
            set 
            {
                if (!columns.Contains(value))
                    throw new ArgumentException("Primary key must be a column in the table; " + _PrimaryKey.Name + " is not in table " + name);

                _PrimaryKey = value; 
            }
        }
        private Column _PrimaryKey = null;

        /// <summary>
        /// Creates a foriegn key column to this table's primary key
        /// </summary>
        /// <param name="columnName"></param>
        /// <returns></returns>
        /// <exception cref="PrimaryKeyCanNotBeNull">Thrown if this table has no primary key</exception>
        public Column CreateForiegnKeyColumn(string columnName)
        {
            if (null == PrimaryKey)
                throw new PrimaryKeyCanNotBeNull("The primary key can not be null when creating a foriegn key column");

            Column toReturn = new Column(columnName, PrimaryKey.Type, ColumnOption.None, this, PrimaryKey);

            return toReturn;
        }

        /// <summary>
        /// Creates a foriegn key column to this table's primary key
        /// </summary>
        /// <param name="columnName"></param>
        /// <returns></returns>
        /// <exception cref="PrimaryKeyCanNotBeNull">Thrown if this table has no primary key</exception>
        public Column CreateForiegnKeyColumn()
        {
            if (null == PrimaryKey)
                throw new PrimaryKeyCanNotBeNull("The primary key can not be null when creating a foriegn key column");

            return CreateForiegnKeyColumn(PrimaryKey.Name);
        }

        /// <summary>
        /// Creates a nullable foriegn key column to this table's primary key
        /// </summary>
        /// <param name="columnName"></param>
        /// <returns></returns>
        /// <exception cref="PrimaryKeyCanNotBeNull">Thrown if this table has no primary key</exception>
        public Column CreateNullableForiegnKeyColumn()
        {
            if (null == PrimaryKey)
                throw new PrimaryKeyCanNotBeNull("The primary key can not be null when creating a foriegn key column");

            Column toReturn = CreateForiegnKeyColumn(PrimaryKey.Name);
            toReturn.Type.Nulls = ColumnType.Null.Allowed;

            return toReturn;
        }

        /// <summary>
        /// Creates a nullable foriegn key column to this table's primary key
        /// </summary>
        /// <param name="columnName"></param>
        /// <returns></returns>
        /// <exception cref="PrimaryKeyCanNotBeNull">Thrown if this table has no primary key</exception>
        public Column CreateNullableForiegnKeyColumn(string columnName)
        {
            Column toReturn = CreateForiegnKeyColumn(columnName);
            toReturn.Type.Nulls = ColumnType.Null.Allowed;

            return toReturn;
        }

        public class PrimaryKeyCanNotBeNull : Exception
        {
            internal PrimaryKeyCanNotBeNull(string message) : base(message) { }
        }

        /// <summary>
        /// Indexes that must exist for two columns
        /// </summary>
        public IList<Index> CompoundIndexes
        {
            get { return _CompoundIndexes; }
            set { _CompoundIndexes = value; }
        }
        private IList<Index> _CompoundIndexes = new List<Index>();

        /// <summary>
        /// This is always run prior to an insert or update; allows setting default values.  Use {0} for the object that was written to
        /// </summary>
        public string RunPriorToInsertOrUpdate
        {
            get { return _RunPriorToInsertOrUpdate; }
            set { _RunPriorToInsertOrUpdate = value; }
        }
        private string _RunPriorToInsertOrUpdate = "";
    }
}
