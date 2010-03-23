// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Common;
using ObjectCloud.ORM.DataAccess.DomainModel;

namespace ObjectCloud.ORM.DataAccess.Generator.SqLite
{
    public class SchemaGenerator : ObjectCloud.ORM.DataAccess.Generator.SchemaGenerator
    {
        /// <summary>
        /// All of the corresponding SQLite types
        /// </summary>
        private static Dictionary<ColumnType.SqlTypes, string> CorrespondingSQLiteypes = new Dictionary<ColumnType.SqlTypes, string>();

        static SchemaGenerator()
        {
            CorrespondingSQLiteypes[ColumnType.SqlTypes.Guid] = "guid";
            CorrespondingSQLiteypes[ColumnType.SqlTypes.Int] = "integer";
            CorrespondingSQLiteypes[ColumnType.SqlTypes.Long] = "integer";
            CorrespondingSQLiteypes[ColumnType.SqlTypes.String] = "string";
            CorrespondingSQLiteypes[ColumnType.SqlTypes.Boolean] = "boolean";
        }

        public override string Generate(Database database)
        {
            StringBuilder toReturn = new StringBuilder(base.Generate(database));

            toReturn.AppendFormat("\nPRAGMA user_version = {0};\n", database.Version);

            return toReturn.ToString();
        }

        /// <summary>
        /// Returns a SQL string that generates a table
        /// </summary>
        /// <param name="table"></param>
        /// <returns></returns>
        public override string Generate(Table table)
        {
            StringBuilder toReturn = new StringBuilder(base.Generate(table));

            foreach (Column column in table.Columns)
                if (column.Indexed)
                    toReturn.AppendFormat("Create index {0}_{1} on {0} ({1});\n", table.Name, column.Name);

            foreach (Index compoundIndex in table.CompoundIndexes)
            {
                List<string> columnNames = new List<string>();

                foreach (Column column in compoundIndex.Columns)
                    columnNames.Add(column.Name);

                toReturn.AppendFormat("Create {3} index {0}_{1} on {0} ({2});\n",
                    table.Name,
                    StringGenerator.GenerateSeperatedList(columnNames, "_"),
                    StringGenerator.GenerateCommaSeperatedList(columnNames),
                    compoundIndex.Unique ? "unique" : "");
            }

            return toReturn.ToString();
        }

        public override string Generate(Column column, bool isPrimaryKey)
        {
            ColumnType columnType = column.Type;

            string sqlColumnType;
            if (ColumnType.Null.Allowed == columnType.Nulls)
                sqlColumnType = CorrespondingSQLiteypes[columnType.SqlType];
            else
                sqlColumnType = string.Format("{0} not null", CorrespondingSQLiteypes[columnType.SqlType]);

            StringBuilder toReturn;

            if (isPrimaryKey)
            {
                toReturn = new StringBuilder(string.Format("{0}\t\t\t{1}\tprimary key", column.Name, sqlColumnType));

                if (column.Auto)
                    toReturn.Append(" AUTOINCREMENT");
            }
            else
                toReturn = new StringBuilder(string.Format("{0}\t\t\t{1}", column.Name, sqlColumnType));

            if (null != column.ForiegnKeyTable)
                toReturn.AppendFormat(" references {0}({1})", column.ForiegnKeyTable.Name, column.ForiegnKeyColumn.Name);

            if (column.Unique)
                toReturn.Append(" unique");

            return toReturn.ToString();
        }
    }
}
