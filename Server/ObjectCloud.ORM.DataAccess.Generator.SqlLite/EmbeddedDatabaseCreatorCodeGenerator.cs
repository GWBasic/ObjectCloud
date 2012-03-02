// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.ORM.DataAccess.DomainModel;
using ObjectCloud.ORM.DataAccess.Generator;

namespace ObjectCloud.ORM.DataAccess.Generator.SqLite
{
    public class EmbeddedDatabaseCreatorCodeGenerator : ISubGenerator
    {
        public EmbeddedDatabaseCreatorCodeGenerator() { }

        public EmbeddedDatabaseCreatorCodeGenerator(
            ObjectCloud.ORM.DataAccess.Generator.SchemaGenerator schemaGenerator, 
            Database database, 
            string dataAccessNamespace)
        {
            SchemaGenerator = schemaGenerator;
            Database = database;
            DataAccessNamespace = dataAccessNamespace;
        }

        public ObjectCloud.ORM.DataAccess.Generator.SchemaGenerator SchemaGenerator
        {
            get { return schemaGenerator; }
            set { schemaGenerator = value; }
        }
        private ObjectCloud.ORM.DataAccess.Generator.SchemaGenerator schemaGenerator;

        public Database Database
        {
            get { return database; }
            set { database = value; }
        }
        private Database database;

        public string DataAccessNamespace
        {
            get { return dataAccessNamespace; }
            set { dataAccessNamespace = value; }
        }
        private string dataAccessNamespace;

        public IEnumerable<string> Usings
        {
            get
            {
                yield return "System";
                yield return "System.Data.Common";
                yield return "ObjectCloud.Interfaces.Database";
                yield return "ObjectCloud.ORM.DataAccess";
                yield return "ObjectCloud.ORM.DataAccess.SQLite";
            }
        }

        public IEnumerable<string> Generate()
        {
            string schemaSql = schemaGenerator.Generate(Database);
            schemaSql = schemaSql.Replace("\"", "\" + '\"' + @\"");

            yield return "    public partial class EmbeddedDatabaseCreator : " + DataAccessNamespace + ".IEmbeddedDatabaseCreator\n";
            yield return "    {\n";
            yield return "        /// <summary>\n";
            yield return "        /// Schema creation sql\n";
            yield return "        /// </summary>\n";
            yield return "        const string schemaSql =\n";
            yield return "@\"" + schemaSql + "\";\n";
            yield return "\n";
            yield return "        /// <summary>\n";
            yield return "        /// Used to connect to the embedded database\n";
            yield return "        /// </summary>\n";
            yield return "        public IEmbeddedDatabaseConnector EmbeddedDatabaseConnector\n";
            yield return "        {\n";
            yield return "            get { return _EmbeddedDatabaseConnector; }\n";
            yield return "            set { _EmbeddedDatabaseConnector = value; }\n";
            yield return "        }\n";
            yield return "        private IEmbeddedDatabaseConnector _EmbeddedDatabaseConnector;\n";
            yield return "\n";
            yield return "        public void Create(string filename)\n";
            yield return "        {\n";
            yield return "            EmbeddedDatabaseConnector.CreateFile(filename);\n";
            yield return "\n";
            yield return "            DbConnection connection = EmbeddedDatabaseConnector.OpenEmbedded(filename);\n";
            yield return "            connection.Open();\n";
            yield return "\n";
            yield return "            try\n";
            yield return "            {\n";
            yield return "                using (DbCommand command = connection.CreateCommand())\n";
            yield return "                {\n";
            yield return "                    command.CommandText = schemaSql;\n";
            yield return "                    command.ExecuteNonQuery();\n";
            yield return "                }\n";
            yield return "            }\n";
            yield return "            finally\n";
            yield return "            {\n";
            yield return "                connection.Close();\n";
            yield return "                connection.Dispose();\n";
            yield return "            }\n";
            yield return "\n";
            yield return "        }\n";
            yield return "    }\n";
        }
    }
}
