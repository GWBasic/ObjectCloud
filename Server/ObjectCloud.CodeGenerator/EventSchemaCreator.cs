using System;

using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;
using ObjectCloud.ORM.DataAccess.DomainModel;

namespace ObjectCloud.CodeGenerator
{
    public class EventSchemaCreator
    {
        public Database Create()
        {
            Database database = new Database();

            Column userIdColumn = new Column("UserId", IDColumn<IUserOrGroup, Guid>.NotNullColumnType, ColumnOption.Indexed);
            Column methodColumn = new Column("Method", NotNull.String, ColumnOption.Indexed);
            Column contentTypeColumn = new Column("ContentType", NotNull.String, ColumnOption.Indexed);
            Column returnCodeColumn = new Column("ReturnCode", NotNull.Int, ColumnOption.Indexed);
            Column timestampColumn = new Column("Timestamp", NotNull.TimeStamp, ColumnOption.Indexed);

            Table eventLogTable = new Table(
                    "EventLog",
                    new Column[]
                    {
                        userIdColumn,
                        methodColumn,
                        contentTypeColumn,
                        returnCodeColumn,
                        timestampColumn,
                        new Column("Content", NotNull.String),
                        new Column("ReturnData", NotNull.String)
                    });

            eventLogTable.CompoundIndexes.Add(new Index(userIdColumn, methodColumn, contentTypeColumn, returnCodeColumn, timestampColumn));
            eventLogTable.CompoundIndexes.Add(new Index(userIdColumn, timestampColumn));
            eventLogTable.CompoundIndexes.Add(new Index(methodColumn, timestampColumn));
            eventLogTable.CompoundIndexes.Add(new Index(userIdColumn, methodColumn));
            eventLogTable.CompoundIndexes.Add(new Index(returnCodeColumn, timestampColumn));
            eventLogTable.CompoundIndexes.Add(new Index(userIdColumn, returnCodeColumn, timestampColumn));
            eventLogTable.CompoundIndexes.Add(new Index(contentTypeColumn, timestampColumn));
            eventLogTable.CompoundIndexes.Add(new Index(userIdColumn, methodColumn, timestampColumn));


            database.Tables.Add(eventLogTable);

            return database;
        }
    }
}
