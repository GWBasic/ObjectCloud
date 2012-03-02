// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.ORM.DataAccess.DomainModel
{
    /// <summary>
    /// Assists in creating column types for enumerations
    /// </summary>
    public static class EnumColumn<TEnum>
    {
        static EnumColumn()
        {
            Type enumType = typeof(TEnum);
            Type baseType = System.Enum.GetUnderlyingType(enumType);
            ColumnType.SqlTypes sqlType = ColumnType.GetSqlType(baseType);

            _NotNullColumnType = new ColumnType(
                sqlType,
                ColumnType.Null.Forbidden,
                enumType,
                "((" + enumType.FullName + "){0})",
                "((" + baseType.FullName + "){0})");

            _NullColumnType = new ColumnType(
                sqlType,
                ColumnType.Null.Allowed,
                enumType,
                "((" + enumType.FullName + "){0})",
                "((" + baseType.FullName + "){0})");
        }

        /// <summary>
        /// Cache for not null enum column type
        /// </summary>
        public static ColumnType NotNullColumnType
        {
            get { return _NotNullColumnType; }
        }
        private static readonly ColumnType _NotNullColumnType;

        /// <summary>
        /// Cache for null enum column type
        /// </summary>
        public static ColumnType NullColumnType
        {
            get { return _NullColumnType; }
        }
        private static readonly ColumnType _NullColumnType;
    }
}
