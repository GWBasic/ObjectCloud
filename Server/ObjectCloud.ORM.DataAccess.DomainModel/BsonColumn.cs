// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Common;

namespace ObjectCloud.ORM.DataAccess.DomainModel
{
    public class BsonColumn : Column
    {
        public BsonColumn(
            string name,
            string typeName)
            : base(name, GetBsonColumnType(typeName)) { }

        private static Dictionary<string, ColumnType> ColumnTypes = new Dictionary<string, ColumnType>();

        private static ColumnType GetBsonColumnType(string typeName)
        {
            ColumnType toReturn;
            if (!(ColumnTypes.TryGetValue(typeName, out toReturn)))
            {
                toReturn = new BsonColumnType(typeName);
                ColumnTypes[typeName] = toReturn;
            }

            return toReturn;
        }

        private class BsonColumnType : ColumnType
        {
            public BsonColumnType(string typeName)
            {
                SqlType = SqlTypes.String;
                Nulls = Null.Forbidden;
                TypeName = typeName;
                GetConverter = "Convert.ToBase64String({0}.ToBson())";
                SetConverter = "BsonSerializer.Deserialize<" + TypeName + ">(Convert.FromBase64String({0}))";
            }
        }
    }
}
