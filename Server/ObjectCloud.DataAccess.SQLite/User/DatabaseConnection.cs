// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Text;

using ObjectCloud.Common;
using ObjectCloud.DataAccess.User;
using ObjectCloud.Interfaces.Disk;

namespace ObjectCloud.DataAccess.SQLite.User
{
    public partial class DatabaseConnection : IDatabaseConnection
    {
        static DatabaseConnection()
        {
            NotificationColumnToDbColumn = new Dictionary<NotificationColumn,string>();

            NotificationColumnToDbColumn[NotificationColumn.notificationId] = "NotificationId";
            NotificationColumnToDbColumn[NotificationColumn.timestamp] = "TimeStamp";
            NotificationColumnToDbColumn[NotificationColumn.senderIdentity] = "SenderIdentity";
            NotificationColumnToDbColumn[NotificationColumn.objectUrl] = "ObjectUrl";
            NotificationColumnToDbColumn[NotificationColumn.summaryView] = "SummaryView";
            NotificationColumnToDbColumn[NotificationColumn.documentType] = "DocumentType";
            NotificationColumnToDbColumn[NotificationColumn.verb] = "Verb";
            NotificationColumnToDbColumn[NotificationColumn.changeData] = "ChangeData";
            NotificationColumnToDbColumn[NotificationColumn.linkedSenderIdentity] = "LinkedSenderIdentity";
        }

        /// <summary>
        /// Maps the strongly-typed enum to the proper database column name
        /// </summary>
        private static readonly Dictionary<NotificationColumn, string> NotificationColumnToDbColumn;

        public IEnumerable<Dictionary<NotificationColumn, object>> GetNotifications(
            long? newestNotificationId,
            long? oldestNotificationId,
            long? maxNotifications,
            string objectUrl,
            string sender,
            List<NotificationColumn> desiredValues)
        {
            // Determine which columns to select and which tables to select from
            // *********************************************
            List<string> columns = new List<string>();

            foreach (NotificationColumn notificationColumn in desiredValues)
                columns.Add(NotificationColumnToDbColumn[notificationColumn]);

            // Determine which filters to use
            // ***************************************

            List<string> filters = new List<string>();

            if (null != newestNotificationId)
                filters.Add(
                    string.Format("{0} <= {1}",
                    NotificationColumnToDbColumn[NotificationColumn.notificationId],
                    newestNotificationId.Value.ToString()));

            if (null != oldestNotificationId)
                filters.Add(
                    string.Format("{0} >= {1}",
                    NotificationColumnToDbColumn[NotificationColumn.notificationId],
                    oldestNotificationId.Value.ToString()));

            if (null != objectUrl)
                filters.Add("objectUrl = @objectUrl");

            if (null != sender)
                filters.Add("sender = @sender");

            string whereClause;
            if (filters.Count > 0)
                whereClause = string.Format(" where {0} ", StringGenerator.GenerateSeperatedList(filters, " and "));
            else
                whereClause = "";

            // Build the columns clause
            string columnsClause = StringGenerator.GenerateCommaSeperatedList(columns);

            // Build the from clause
            string fromClause = " from Notification ";

            StringBuilder queryBuilder = new StringBuilder("select ");

            queryBuilder.Append(columnsClause);
            queryBuilder.Append(fromClause);
            queryBuilder.Append(whereClause);
            queryBuilder.Append(" order by NotificationId desc ");

            if (null != maxNotifications)
            {
                string maxNotificationsString = maxNotifications.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                queryBuilder.AppendFormat(" limit {0} ", maxNotificationsString);
            }


            // Run the query and return the results
            DbCommand command = sqlConnection.CreateCommand();
            command.CommandText = queryBuilder.ToString();

            if (null != objectUrl)
            {
                DbParameter dbParameter = command.CreateParameter();
                dbParameter.ParameterName = "@objectUrl";
                dbParameter.Value = objectUrl;
                command.Parameters.Add(dbParameter);
            }

            if (null != sender)
            {
                DbParameter dbParameter = command.CreateParameter();
                dbParameter.ParameterName = "@sender";
                dbParameter.Value = sender;
                command.Parameters.Add(dbParameter);
            }

            using (IDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    Dictionary<NotificationColumn, object> toYield = new Dictionary<NotificationColumn, object>();

                    for (int colCtr = 0; colCtr < reader.FieldCount; colCtr++)
                        if (NotificationColumn.timestamp == desiredValues[colCtr])
                            // The timestamp should be a DateTime, but the data access layer stores them as ticks
                            toYield[desiredValues[colCtr]] = new DateTime(reader.GetInt64(colCtr));
                        else
                            toYield[desiredValues[colCtr]] = reader.GetValue(colCtr);

                    yield return toYield;
                }
            }
        }
    }
}
