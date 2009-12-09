// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using ObjectCloud.ORM.DataAccess.DomainModel;
using ObjectCloud.ORM.DataAccess.Generator;

namespace ObjectCloud.CodeGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            string baseDirectoryPrefix = Path.GetFullPath(args[0]);
            string sqliteDirectoryPrefix = Path.GetFullPath(args[1]);
            string testDirectoryPrefix = Path.GetFullPath(args[2]);

            string mainFilename = baseDirectoryPrefix + Path.DirectorySeparatorChar + "Directory.cs";
            string sqliteFilename = sqliteDirectoryPrefix + Path.DirectorySeparatorChar + "Directory.cs";

            // Directory database
            // **************************

            Database database = (new DirectorySchemaCreator()).Create();
            SchemaGenerator schemaGenerator = new ObjectCloud.ORM.DataAccess.Generator.SqLite.SchemaGenerator();

            CSharpGenerator csharpGenerator = new CSharpGenerator(mainFilename, "ObjectCloud.DataAccess.Directory", new ISubGenerator[] 
                { 
                    new ObjectCloud.ORM.DataAccess.Generator.EntityGenerator(database, "ObjectCloud.DataAccess.Directory")
                });

            csharpGenerator.GenerateToFile();

            csharpGenerator = new CSharpGenerator(sqliteFilename, "ObjectCloud.DataAccess.SQLite.Directory", new ISubGenerator[] 
                { 
                    new ObjectCloud.ORM.DataAccess.Generator.SqLite.EmbeddedDatabaseCreatorCodeGenerator(schemaGenerator, database, "ObjectCloud.DataAccess.Directory"),
                    new ObjectCloud.ORM.DataAccess.Generator.SqLite.EntityGenerator(database, "ObjectCloud.DataAccess.Directory")
                },
                "ObjectCloud.DataAccess.Directory");

            csharpGenerator.GenerateToFile();

            // NameValuePairs database
            // **************************

            mainFilename = baseDirectoryPrefix + Path.DirectorySeparatorChar + "NameValuePairs.cs";
            sqliteFilename = sqliteDirectoryPrefix + Path.DirectorySeparatorChar + "NameValuePairs.cs";

            database = (new NameValuePairsCreator()).Create();

            csharpGenerator = new CSharpGenerator(mainFilename, "ObjectCloud.DataAccess.NameValuePairs", new ISubGenerator[] 
                { 
                    new ObjectCloud.ORM.DataAccess.Generator.EntityGenerator(database, "ObjectCloud.DataAccess.NameValuePairs")
                });

            csharpGenerator.GenerateToFile();

            csharpGenerator = new CSharpGenerator(sqliteFilename, "ObjectCloud.DataAccess.SQLite.NameValuePairs", new ISubGenerator[] 
                { 
                    new ObjectCloud.ORM.DataAccess.Generator.SqLite.EmbeddedDatabaseCreatorCodeGenerator(schemaGenerator, database, "ObjectCloud.DataAccess.NameValuePairs"),
                    new ObjectCloud.ORM.DataAccess.Generator.SqLite.EntityGenerator(database, "ObjectCloud.DataAccess.NameValuePairs")
                },
                "ObjectCloud.DataAccess.NameValuePairs");

            csharpGenerator.GenerateToFile();

            // UserManager database
            // **************************

            mainFilename = baseDirectoryPrefix + Path.DirectorySeparatorChar + "UserManager.cs";
            sqliteFilename = sqliteDirectoryPrefix + Path.DirectorySeparatorChar + "UserManager.cs";

            database = (new UserManagerSchemaCreator()).Create();

            csharpGenerator = new CSharpGenerator(mainFilename, "ObjectCloud.DataAccess.UserManager", new ISubGenerator[] 
                { 
                    new ObjectCloud.ORM.DataAccess.Generator.EntityGenerator(database, "ObjectCloud.DataAccess.UserManager")
                });

            csharpGenerator.GenerateToFile();

            csharpGenerator = new CSharpGenerator(sqliteFilename, "ObjectCloud.DataAccess.SQLite.UserManager", new ISubGenerator[] 
                { 
                    new ObjectCloud.ORM.DataAccess.Generator.SqLite.EmbeddedDatabaseCreatorCodeGenerator(schemaGenerator, database, "ObjectCloud.DataAccess.UserManager"),
                    new ObjectCloud.ORM.DataAccess.Generator.SqLite.EntityGenerator(database, "ObjectCloud.DataAccess.UserManager")
                },
                "ObjectCloud.DataAccess.UserManager");

            csharpGenerator.GenerateToFile();

            // SessionManager database
            // **************************

            mainFilename = baseDirectoryPrefix + Path.DirectorySeparatorChar + "SessionManager.cs";
            sqliteFilename = sqliteDirectoryPrefix + Path.DirectorySeparatorChar + "SessionManager.cs";

            database = (new SessionManagerSchemaCreator()).Create();

            csharpGenerator = new CSharpGenerator(mainFilename, "ObjectCloud.DataAccess.SessionManager", new ISubGenerator[] 
                { 
                    new ObjectCloud.ORM.DataAccess.Generator.EntityGenerator(database, "ObjectCloud.DataAccess.SessionManager")
                });

            csharpGenerator.GenerateToFile();

            csharpGenerator = new CSharpGenerator(sqliteFilename, "ObjectCloud.DataAccess.SQLite.SessionManager", new ISubGenerator[] 
                { 
                    new ObjectCloud.ORM.DataAccess.Generator.SqLite.EmbeddedDatabaseCreatorCodeGenerator(schemaGenerator, database, "ObjectCloud.DataAccess.SessionManager"),
                    new ObjectCloud.ORM.DataAccess.Generator.SqLite.EntityGenerator(database, "ObjectCloud.DataAccess.SessionManager")
                },
                "ObjectCloud.DataAccess.SessionManager");

            csharpGenerator.GenerateToFile();

            // User database
            // **************************

            mainFilename = baseDirectoryPrefix + Path.DirectorySeparatorChar + "User.cs";
            sqliteFilename = sqliteDirectoryPrefix + Path.DirectorySeparatorChar + "User.cs";

            database = (new UserSchemaCreator()).Create();

            csharpGenerator = new CSharpGenerator(mainFilename, "ObjectCloud.DataAccess.User", new ISubGenerator[] 
                { 
                    new ObjectCloud.ORM.DataAccess.Generator.EntityGenerator(database, "ObjectCloud.DataAccess.User")
                });

            csharpGenerator.GenerateToFile();

            csharpGenerator = new CSharpGenerator(sqliteFilename, "ObjectCloud.DataAccess.SQLite.User", new ISubGenerator[] 
                { 
                    new ObjectCloud.ORM.DataAccess.Generator.SqLite.EmbeddedDatabaseCreatorCodeGenerator(schemaGenerator, database, "ObjectCloud.DataAccess.User"),
                    new ObjectCloud.ORM.DataAccess.Generator.SqLite.EntityGenerator(database, "ObjectCloud.DataAccess.User")
                },
                "ObjectCloud.DataAccess.User");

            csharpGenerator.GenerateToFile();

            // Test database
            // **************************

            mainFilename = testDirectoryPrefix + Path.DirectorySeparatorChar + "TestDatabase.cs";
            sqliteFilename = testDirectoryPrefix + Path.DirectorySeparatorChar + "TestDatabaseSQLite.cs";

            database = (new TestDatabaseSchemaCreator()).Create();

            csharpGenerator = new CSharpGenerator(mainFilename, "ObjectCloud.ORM.DataAccess.Test", new ISubGenerator[] 
                { 
                    new ObjectCloud.ORM.DataAccess.Generator.EntityGenerator(database, "ObjectCloud.ORM.DataAccess.Test"),
                });

            csharpGenerator.GenerateToFile();

            database = (new TestDatabaseSchemaCreator()).Create();

            csharpGenerator = new CSharpGenerator(sqliteFilename, "ObjectCloud.ORM.DataAccess.SQLite.Test", new ISubGenerator[] 
                { 
                    new ObjectCloud.ORM.DataAccess.Generator.SqLite.EmbeddedDatabaseCreatorCodeGenerator(schemaGenerator, database, "ObjectCloud.ORM.DataAccess.Test"),
                    new ObjectCloud.ORM.DataAccess.Generator.SqLite.EntityGenerator(database, "ObjectCloud.ORM.DataAccess.Test")                
                },
                "ObjectCloud.ORM.DataAccess.Test");

            csharpGenerator.GenerateToFile();

            // Log database
            // **************************

            mainFilename = baseDirectoryPrefix + Path.DirectorySeparatorChar + "Log.cs";
            sqliteFilename = sqliteDirectoryPrefix + Path.DirectorySeparatorChar + "Log.cs";

            database = (new LogSchemaCreator()).Create();

            csharpGenerator = new CSharpGenerator(mainFilename, "ObjectCloud.DataAccess.Log", new ISubGenerator[] 
                { 
                    new ObjectCloud.ORM.DataAccess.Generator.EntityGenerator(database, "ObjectCloud.DataAccess.Log")
                });

            csharpGenerator.GenerateToFile();

            csharpGenerator = new CSharpGenerator(sqliteFilename, "ObjectCloud.DataAccess.SQLite.Log", new ISubGenerator[] 
                { 
                    new ObjectCloud.ORM.DataAccess.Generator.SqLite.EmbeddedDatabaseCreatorCodeGenerator(schemaGenerator, database, "ObjectCloud.DataAccess.Log"),
                    new ObjectCloud.ORM.DataAccess.Generator.SqLite.EntityGenerator(database, "ObjectCloud.DataAccess.Log")
                },
                "ObjectCloud.DataAccess.Log");

            csharpGenerator.GenerateToFile();
        }
    }
}
