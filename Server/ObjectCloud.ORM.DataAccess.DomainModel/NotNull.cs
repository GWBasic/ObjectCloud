// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.ORM.DataAccess.DomainModel
{
    /// <summary>
    /// Holder for not null column types
    /// </summary>
    public class NotNull
    {
        /// <summary>
        /// Column type for ints
        /// </summary>
        public static ColumnType Int
        {
            get { return _IntColumnType; }
        }
        private static readonly ColumnType _IntColumnType = new ColumnType(ColumnType.SqlTypes.Int, ColumnType.Null.Forbidden);

        /// <summary>
        /// Column type for longs
        /// </summary>
        public static ColumnType Long
        {
            get { return _LongColumnType; }
        }
        private static readonly ColumnType _LongColumnType = new ColumnType(ColumnType.SqlTypes.Long, ColumnType.Null.Forbidden);

        /// <summary>
        /// Column type for nullable strings
        /// </summary>
        public static ColumnType String
        {
            get { return _StringColumnType; }
        }
        private static readonly ColumnType _StringColumnType = new ColumnType(ColumnType.SqlTypes.String, ColumnType.Null.Forbidden);

        /// <summary>
        /// Column type for guids
        /// </summary>
        public static ColumnType Guid
        {
            get { return _GuidColumnType; }
        }
        private static readonly ColumnType _GuidColumnType = new ColumnType(ColumnType.SqlTypes.Guid, ColumnType.Null.Forbidden);

        /// <summary>
        /// Column type for timestamps.  These are saved in the database as the DateTime's Ticks property, which is a long.
        /// </summary>
        public static ColumnType TimeStamp
        {
            get { return _TimeStamp; }
        }
        private static readonly ColumnType _TimeStamp = new ColumnType(ColumnType.SqlTypes.Long, ColumnType.Null.Forbidden, typeof(DateTime), "new DateTime({0})", "{0}.Ticks");

        /// <summary>
        /// Column type for timespans.  These are saved in the database as the TimeSpans's Ticks property, which is a long.
        /// </summary>
        public static ColumnType TimeSpan
        {
            get { return _TimeSpan; }
        }
        private static readonly ColumnType _TimeSpan = new ColumnType(ColumnType.SqlTypes.Long, ColumnType.Null.Forbidden, typeof(TimeSpan), "TimeSpan.FromTicks({0})", "{0}.Ticks");

        /// <summary>
        /// Column type for booleans
        /// </summary>
        public static ColumnType Bool
        {
            get { return _Bool; }
        }
        private static readonly ColumnType _Bool = new ColumnType(ColumnType.SqlTypes.Boolean, ColumnType.Null.Forbidden, typeof(bool), "1 == {0}", "{0} ? 1 : 0");
    }
}
