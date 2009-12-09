using System;

using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;
using ObjectCloud.ORM.DataAccess.DomainModel;

namespace ObjectCloud.CodeGenerator
{
	public class LogSchemaCreator
	{
        public Database Create()
        {
            Database database = new Database();

            Table classesTable =
                new Table(
                    "Classes",
                    new Column("ClassId", NotNull.Long, true),
                    new Column[]
                    {
                        new Column("Name", NotNull.String, ColumnOption.Indexed | ColumnOption.Unique),
                    });

			database.Tables.Add(classesTable);
			
Column timestampColumn = new Column("TimeStamp", NotNull.TimeStamp, ColumnOption.Indexed);
Column levelColumn = new Column("Level", EnumColumn<LoggingLevel>.NotNullColumnType, ColumnOption.Indexed);

            
            Table logTable = 
                new Table(
                    "Log",
                    new Column[]
                    {
	                    new Column("ClassId", NotNull.Long, ColumnOption.Indexed),
                        timestampColumn,
                        levelColumn,
						new Column("ThreadId", NotNull.Int, ColumnOption.Indexed),
						new Column("SessionId", IDColumn<ISession, Guid>.NullColumnType, ColumnOption.Indexed),
						new Column("RemoteEndPoint", Null.String, ColumnOption.Indexed),
						new Column("UserId", IDColumn<IUserOrGroup, Guid>.NullColumnType, ColumnOption.Indexed),
						new Column("Message", NotNull.String),
	                    new Column("ExceptionClassId", Null.Long, ColumnOption.Indexed),
						new Column("ExceptionMessage", Null.String),
						new Column("ExceptionStackTrace", Null.String)
                    });

            logTable.CompoundIndexes.Add(new Column[] { timestampColumn, levelColumn });

            database.Tables.Add(logTable);

            Table lifespanTable =
                new Table(
                    "Lifespan",
                    new Column("Level", EnumColumn<LoggingLevel>.NotNullColumnType),
                    new Column[]
                    {
                        new Column("Timespan", NotNull.TimeSpan)
                    });

            database.Tables.Add(lifespanTable);

            database.Version = 4;

            return database;
        }
	}
}
