// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.ORM.DataAccess.DomainModel;

namespace ObjectCloud.CodeGenerator
{
    public class TestDatabaseSchemaCreator
    {
        public Database Create()
        {
            Database database = new Database();

            database.Tables.Add(
                new Table(
                    "TestTable",
                    new Column("TestColumn", NotNull.String),
                    new Column[0]));

            database.Version = 2;

            return database;
        }
    }
}
