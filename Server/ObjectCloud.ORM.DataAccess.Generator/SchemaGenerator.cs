// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Common;
using ObjectCloud.ORM.DataAccess.DomainModel;

namespace ObjectCloud.ORM.DataAccess.Generator
{
    /// <summary>
    /// Generates a schema given a database
    /// </summary>
    public abstract class SchemaGenerator
    {
        /// <summary>
        /// Returns a SQL string that generates a schema for the given database
        /// </summary>
        /// <param name="database"></param>
        /// <returns></returns>
        public virtual string Generate(Database database)
        {
            StringBuilder toReturn = new StringBuilder();

            foreach (Table table in database.Tables)
                toReturn.Append(Generate(table));

            return toReturn.ToString();
        }

        /// <summary>
        /// Returns a SQL string that generates a table
        /// </summary>
        /// <param name="table"></param>
        /// <returns></returns>
        public virtual string Generate(Table table)
        {
            StringBuilder toReturn = new StringBuilder(string.Format("create table {0} \n(\n", table.Name));

            Column primaryKey = table.PrimaryKey;

            List<string> columnStrings = new List<string>();
            foreach (Column column in table.Columns)
                columnStrings.Add("\t" + Generate(column, column == primaryKey));

            toReturn.Append(
                StringGenerator.GenerateSeperatedList(
                    columnStrings, ",\n"));

            toReturn.Append("\n);");

            return toReturn.ToString();
        }

        /// <summary>
        /// Returns the string that generates the column
        /// </summary>
        /// <param name="column"></param>
        /// <param name="isPrimaryKey"></param>
        /// <returns></returns>
        public abstract string Generate(Column column, bool isPrimaryKey);

        /// <summary>
        /// Thrown if a particular column type isn't supported
        /// </summary>
        public class ColumnTypeNotSupported : Exception
        {
            public ColumnTypeNotSupported(ColumnType columnType)
                :
                base("The column type " + columnType.GetType().FullName + " is not supported") { }
        }
    }
}
