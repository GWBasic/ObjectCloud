// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Common;
using ObjectCloud.ORM.DataAccess.DomainModel;

namespace ObjectCloud.ORM.DataAccess.Generator
{
    public class EntityGenerator : ISubGenerator
    {
        public EntityGenerator() { }

        public EntityGenerator(
            Database database,
            string explicitNamespace)
        {
            Database = database;
            ExplicitNamespace = explicitNamespace;
        }

        public Database Database
        {
            get { return database; }
            set { database = value; }
        }
        private Database database;

        public string ExplicitNamespace
        {
            get { return explicitNamespace; }
            set { explicitNamespace = value; }
        }
        private string explicitNamespace;

        public IEnumerable<string> Usings
        {
            get
            {
                yield return "System";
                yield return "System.Collections.Generic";
                yield return "ObjectCloud.ORM.DataAccess";
                yield return "ObjectCloud.ORM.DataAccess.WhereConditionals";
            }
        }

        public IEnumerable<string> Generate()
        {
            yield return "\tpublic class DataAccessLocator : ObjectCloud.ORM.DataAccess.DataAccessLocator<IDatabaseConnectorFactory> { }\n";
            yield return "\t\n";
            yield return "\tpublic partial interface IDatabaseConnection : ObjectCloud.ORM.DataAccess.IDatabaseConnection<IDatabaseTransaction>\n";
            yield return "\t{\n";

            foreach (Table table in Database.Tables)
            {
                yield return "\t\t" + table.Name + "_Table " + table.Name + " { get; }\n";
            }

            yield return "\t}\n";
            yield return "\t\n";
            yield return "\tpublic interface IDatabaseConnector : ObjectCloud.ORM.DataAccess.IDatabaseConnector<IDatabaseConnector, IDatabaseConnection, IDatabaseTransaction> { }\n";
            yield return "\t\n";
            yield return "\tpublic interface IDatabaseConnectorFactory : ObjectCloud.ORM.DataAccess.IDatabaseConnectorFactory<" + ExplicitNamespace + ".IDatabaseConnector, " + ExplicitNamespace + ".IDatabaseConnection, IDatabaseTransaction> { }\n";
            yield return "\t\n";
            yield return "\tpublic interface IEmbeddedDatabaseCreator : ObjectCloud.ORM.DataAccess.IEmbeddedDatabaseCreator { }\n";
            yield return "\t\n";

            foreach (Table table in Database.Tables)
            {
                yield return "\tpublic interface I" + table.Name + "_Readable\n";
                yield return "\t{\n";

                foreach (Column column in table.Columns)
                    yield return "\t\t" + column.Type.TypeName + " " + column.Name + " { get; }\n";

                yield return "\t}\n";
                yield return "\n";
                yield return "\tpublic interface I" + table.Name + "_Writable : I" + table.Name + "_Readable\n";
                yield return "\t{\n";

                foreach (Column column in table.Columns)
                {
                    yield return "\t\tnew " + column.Type.TypeName + " " + column.Name + " { set; }\n";
                    yield return "\t\tbool " + column.Name + "_Changed { get; }\n";
                }

                yield return "\t}\n";
                yield return "\n";

                yield return "\tpublic abstract partial class " + table.Name + "_Table : Table<I" + table.Name + "_Writable, I" + table.Name + "_Readable, " + table.Name + "_Table." + table.Name + "_Inserter>\n";
                yield return "\t{\n";

                foreach (Column column in table.Columns)
                {
                    yield return "\t\tpublic static Column " + column.Name + "\n";
                    yield return "\t\t{\n";
                    yield return "\t\t\tget\n";
                    yield return "\t\t\t{\n";
                    yield return "\t\t\t\treturn _" + column.Name + ";\n";
                    yield return "\t\t\t}\n";
                    yield return "\t\t}\n";
                    yield return "\t\tprotected static Column _" + column.Name + ";\n\n";
                }

                yield return "\t\t\n";
                yield return "\t\tpublic class " + table.Name + "_Inserter : I" + table.Name + "_Writable\n";
                yield return "\t\t{\n";

                foreach (Column column in table.Columns)
                {
                    yield return "\t\t\tpublic " + column.Type.TypeName + " " + column.Name + "\n";
                    yield return "\t\t\t{\n";
                    yield return "\t\t\t\tget { return _" + column.Name + "; }\n";
                    yield return "\t\t\t\tset\n";
                    yield return "\t\t\t\t{\n";
                    yield return "\t\t\t\t\t_" + column.Name + " = value;\n";
                    yield return "\t\t\t\t\t_" + column.Name + "_Changed = true;\n";
                    yield return "\t\t\t\t}\n";
                    yield return "\t\t\t}\n";
                    yield return "\t\t\tpublic " + column.Type.TypeName + " _" + column.Name + ";\n";
                    yield return "\t\t\tpublic bool " + column.Name + "_Changed\n";
                    yield return "\t\t\t{\n";
                    yield return "\t\t\t\tget { return _" + column.Name + "_Changed; }\n";
                    yield return "\t\t\t}\n";
                    yield return "\t\t\tprivate bool _" + column.Name + "_Changed = false;\n";
                    yield return "\t\t\t\n";
                }
                yield return "\t\t}\n";
                yield return "\t}\n";
            }

            yield return "\n";
            yield return "\tpublic partial interface IDatabaseTransaction : ObjectCloud.ORM.DataAccess.IDatabaseTransaction { }\n";
        }
    }
}
