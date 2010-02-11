// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.ORM.DataAccess.DomainModel
{
    public class Column
    {
        public Column() { }

        public Column(string name, ColumnType type)
        {
            Name = name;
            Type = type;
        }

        public Column(string name, ColumnType type, bool auto)
            : this(name, type)
        {
            _Auto = auto;
        }

        public Column(string name, ColumnType type, ColumnOption columnOptions)
            : this(name, type)
        {
            _ColumnOptions = columnOptions;
        }

        public Column(string name, ColumnType type, ColumnOption columnOptions, Table foriegnKeyTable, Column foriegnKeyColumn)
            : this(name, type, columnOptions)
        {
            _ForiegnKeyColumn = foriegnKeyColumn;
            _ForiegnKeyTable = foriegnKeyTable;
        }

        /// <summary>
        /// The name of the column
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
        /// The column's type
        /// </summary>
        public ColumnType Type
        {
            get { return type; }
            set 
            {
                if (null == value)
                    throw new NullReferenceException("Type");

                type = value;
            }
        }
        private ColumnType type;

        /// <summary>
        /// True if the column is indexed, false otherwise
        /// </summary>
        public bool Indexed
        {
            get { return (ColumnOptions & ColumnOption.Indexed) == ColumnOption.Indexed; }
            set
            {
                if (value)
                    _ColumnOptions = _ColumnOptions | ColumnOption.Indexed;
                else
                    _ColumnOptions = _ColumnOptions & (~ColumnOption.Indexed);
            }
        }

        /// <summary>
        /// True if the column is unique, false otherwise
        /// </summary>
        public bool Unique
        {
            get { return (ColumnOptions & ColumnOption.Unique) == ColumnOption.Unique; }
            set
            {
                if (value)
                    _ColumnOptions = _ColumnOptions | ColumnOption.Unique;
                else
                    _ColumnOptions = _ColumnOptions & (~ColumnOption.Unique);
            }
        }

        /// <summary>
        /// All of the column options
        /// </summary>
        public ColumnOption ColumnOptions
        {
            get { return _ColumnOptions; }
            set { _ColumnOptions = value; }
        }
        private ColumnOption _ColumnOptions;

        /// <summary>
        /// True if the column should try to automatically populate its value.  This currently is only supported for "PK" primary keys
        /// </summary>
        public bool Auto
        {
            get { return _Auto; }
            set { _Auto = value; }
        }
        private bool _Auto = false;

        /// <summary>
        /// The table that this column is a forien key into
        /// </summary>
        public Table ForiegnKeyTable
        {
            get { return _ForiegnKeyTable; }
        }
        private readonly Table _ForiegnKeyTable;

        /// <summary>
        /// The column that this column is a forien key into
        /// </summary>
        public Column ForiegnKeyColumn
        {
            get { return _ForiegnKeyColumn; }
        }
        private readonly Column _ForiegnKeyColumn;
    }
}
