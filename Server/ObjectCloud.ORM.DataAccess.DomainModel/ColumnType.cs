// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Common;

namespace ObjectCloud.ORM.DataAccess.DomainModel
{
    public class ColumnType
    {
        public ColumnType() { }

        public ColumnType(SqlTypes sqlType, Null nulls, Type resolvedType, string setConverter, string getConverter)
        {
            _SqlType = sqlType;
            _Nulls = nulls;
            TypeName = StringGenerator.GenerateTypeName(resolvedType);
            _SetConverter = setConverter;
            _GetConverter = getConverter;
        }

        public ColumnType(SqlTypes sqlType, Null nulls)
        {
            _SqlType = sqlType;
            _Nulls = nulls;
            TypeName = StringGenerator.GenerateTypeName(DotNetType);
        }

        /// <summary>
        /// Types that can be mapped into database columns
        /// </summary>
        public enum SqlTypes
        {
            Int, Long, String, Guid, Boolean
        }

        /// <summary>
        /// The column type to be persisted
        /// </summary>
        public SqlTypes SqlType
        {
            get { return _SqlType; }
            set { _SqlType = value; }
        }
        private SqlTypes _SqlType;

        /// <summary>
        /// Potential null values
        /// </summary>
        public enum Null
        {
            Allowed, Forbidden
        }

        /// <summary>
        /// Wether or not nulls are allowed
        /// </summary>
        public Null Nulls
        {
            get { return _Nulls; }
            set { _Nulls = value; }
        }
        private Null _Nulls;

        /// <summary>
        /// All of the corresponding not null types
        /// </summary>
        private static Dictionary<SqlTypes, Type> CorrespondingNotNullTypes = new Dictionary<SqlTypes, Type>();

        /// <summary>
        /// All of the corresponding null types
        /// </summary>
        private static Dictionary<SqlTypes, Type> CorrespondingNullTypes = new Dictionary<SqlTypes, Type>();

        static ColumnType()
        {
            CorrespondingNotNullTypes[SqlTypes.Guid] = typeof(Guid);
            CorrespondingNotNullTypes[SqlTypes.Int] = typeof(int);
            CorrespondingNotNullTypes[SqlTypes.Long] = typeof(long);
            CorrespondingNotNullTypes[SqlTypes.String] = typeof(string);
            CorrespondingNotNullTypes[SqlTypes.Boolean] = typeof(int);

            CorrespondingNullTypes[SqlTypes.Guid] = typeof(Guid?);
            CorrespondingNullTypes[SqlTypes.Int] = typeof(int?);
            CorrespondingNullTypes[SqlTypes.Long] = typeof(long?);
            CorrespondingNullTypes[SqlTypes.String] = typeof(string);
            CorrespondingNullTypes[SqlTypes.Boolean] = typeof(int?);
        }

        /// <summary>
        /// Returns the corresponding SqlType for the given type
        /// </summary>
        /// <param name="type"></param>
        /// <exception cref="TypeNotSupported">Thrown if the type isn't supported</exception>
        /// <returns></returns>
        public static SqlTypes GetSqlType(Type type)
        {
            foreach (SqlTypes sqlType in System.Enum.GetValues(typeof(SqlTypes)))
                if (type == CorrespondingNotNullTypes[sqlType])
                    return sqlType;
                else if (type == CorrespondingNullTypes[sqlType])
                    return sqlType;

            throw new TypeNotSupported(type);
        }

        /// <summary>
        /// Thrown when an attempt is made to get a SqlType from a .Net type, and there is no corresponding SqlType
        /// </summary>
        public class TypeNotSupported : Exception
        {
            internal TypeNotSupported(Type type)
                : base (type.ToString() + " does not have a corresponding database type")
            {
                _Type = type;
            }

            /// <summary>
            /// The missing type
            /// </summary>
            public Type Type
            {
                get { return _Type; }
            }
            private Type _Type;
        }

        /// <summary>
        /// The approrpiate .Net type given SqlType.  The database column will always be converted to this type
        /// </summary>
        public Type DotNetType
        {
            get
            {
                if (Null.Forbidden == Nulls)
                    return CorrespondingNotNullTypes[SqlType];
                else
                    return CorrespondingNullTypes[SqlType];
            }
        }

        /// <summary>
        /// The approrpiate .Net type given SqlType.  The database column will always be converted to this type.  This is never the nullable version
        /// </summary>
        public Type DotNetType_NotNullable
        {
            get
            {
                return CorrespondingNotNullTypes[SqlType];
            }
        }

        /*// <summary>
        /// The type that this column will resolve to in a corresponding object.  This can be different then DotNetType, if it is different, SetConverter and GetConverter must be set.  Set this to null to use DotNetType;
        /// </summary>
        public Type ResolvedType
        {
            get 
            {
                if (null == _ResolvedType)
                    return DotNetType;

                return _ResolvedType; 
            }
            set { _ResolvedType = value; }
        }
        private Type _ResolvedType = null;*/

        /// <summary>
        /// The type name for C#
        /// </summary>
        public string TypeName { get; set; }

        /// <summary>
        /// Converts from a value of DotNetType to ResolvedType; used when populating objects as a result of a query.  Use {0} where the value of DotNetType is expected.
        /// </summary>
        public string SetConverter
        {
            get { return _SetConverter; }
            set { _SetConverter = value; }
        }
        private string _SetConverter = "{0}";

        /// <summary>
        /// Converts from a value of ResolvedType to DotNetType; used when writing objects to the database.  Use {0} where the value of ResolvedType is expected.
        /// </summary>
        public string GetConverter
        {
            get { return _GetConverter; }
            set { _GetConverter = value; }
        }
        private string _GetConverter = "{0}";
    }
}
