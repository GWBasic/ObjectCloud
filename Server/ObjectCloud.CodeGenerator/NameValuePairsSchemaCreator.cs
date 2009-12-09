// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.ORM.DataAccess.DomainModel;

namespace ObjectCloud.CodeGenerator
{
    public class NameValuePairsCreator
    {
        public Database Create()
        {
            Database database = new Database();

            database.Tables.Add(
                new Table(
                    "Pairs",
                    new Column("Name", NotNull.String),
                    new Column[]
                    {
                        new Column("Value", NotNull.String),
                    }));

            database.Version = 2;

            return database;
        }
    }
}
