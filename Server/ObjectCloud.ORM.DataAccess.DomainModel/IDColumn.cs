// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Common;

namespace ObjectCloud.ORM.DataAccess.DomainModel
{
    /// <summary>
    /// Generates column types for ID wrappers
    /// </summary>
    public class IDColumn<T, TID>
        where TID : struct
    {
        static IDColumn()
        {
            Type idWrapperType = typeof(ID<T, TID>);
            Type tType = typeof(T);
            Type baseType = typeof(TID);
            ColumnType.SqlTypes sqlType = ColumnType.GetSqlType(baseType);

            _NotNullColumnType = new ColumnType(
                sqlType,
                ColumnType.Null.Forbidden,
                idWrapperType,
                "new ObjectCloud.Common.ID<" + tType.FullName + ", " + baseType.FullName + ">({0})",
                "{0}.Value");

            _NullColumnType = new ColumnType(
                sqlType,
                ColumnType.Null.Allowed,
                typeof(ID<T, TID>?),
                "new ObjectCloud.Common.ID<" + tType.FullName + ", " + baseType.FullName + ">({0})",
                "ObjectCloud.Common.ID<" + tType.FullName + ", " + baseType.FullName + ">.GetValueOrNull({0})");
        }

        /// <summary>
        /// Cache for not null ID column type
        /// </summary>
        public static ColumnType NotNullColumnType
        {
            get { return _NotNullColumnType; }
        }
        private static readonly ColumnType _NotNullColumnType;

        /// <summary>
        /// Cache for null ID column type
        /// </summary>
        public static ColumnType NullColumnType
        {
            get { return _NullColumnType; }
        }
        private static readonly ColumnType _NullColumnType;
    }
}
