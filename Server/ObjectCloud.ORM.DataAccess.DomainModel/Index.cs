// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;


namespace ObjectCloud.ORM.DataAccess.DomainModel
{
    public class Index
    {
        public Index(IEnumerable<Column> columns)
            : this(columns, false)
        {
        }

        public Index(params Column[] columns)
            : this(columns as IEnumerable<Column>) { }

        public Index(IEnumerable<Column> columns, bool unique)
        {
            _Columns = columns;
            _Unique = unique;
        }

        public IEnumerable<Column> Columns
        {
            get { return _Columns; }
            set { _Columns = value; }
        }
        private IEnumerable<Column> _Columns;

        public bool Unique
        {
            get { return _Unique; }
            set { _Unique = value; }
        }
        private bool _Unique;

        public static implicit operator Index(Column[] columns)
        {
            return new Index(columns);
        }
    }
}
