// Copyright 2009, 2010 Andrew Rondeau
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
            bool useChangeDataTable = false;
            bool useNotificationTable = false;

            foreach (NotificationColumn notificationColumn in desiredValues)
            {
                if (NotificationColumn.changeData == notificationColumn)
                    useChangeDataTable = true;
                else if (NotificationColumn.notificationId != notificationColumn)
                    useNotificationTable = true;

                if (NotificationColumn.notificationId == notificationColumn)
                    columns.Add("n.NotificationId");
                else
                    columns.Add(notificationColumn.ToString());
            }

            // Determine which filters to use
            // ***************************************

            List<string> filters = new List<string>();

            if (null != newestNotificationId)
                filters.Add(string.Format("n.NotificationId <= {0}", newestNotificationId.Value.ToString()));

            if (null != oldestNotificationId)
                filters.Add(string.Format("n.NotificationId >= {0}", oldestNotificationId.Value.ToString()));

            if (null != objectUrl)
            {
                useNotificationTable = true;
                filters.Add("objectUrl = @objectUrl");
            }

            if (null != sender)
            {
                useNotificationTable = true;
                filters.Add("sender = @sender");
            }

            string whereClause;
            if (filters.Count > 0)
                whereClause = string.Format(" where {0} ", StringGenerator.GenerateSeperatedList(filters, " and "));
            else
                whereClause = "";

            // Build the columns clause
            string columnsClause = StringGenerator.GenerateCommaSeperatedList(columns);

            // Build the from clause
            string fromClause;
            if (useNotificationTable && !useChangeDataTable)
                fromClause = " from Notification as n ";
            else if (!useNotificationTable && useChangeDataTable)
                fromClause = " from ChangeData as n ";
            else
                fromClause = " from Notification as n left outer join ChangeData on n.NotificationId = ChangeData.NotificationId ";

            StringBuilder queryBuilder = new StringBuilder("select ");

            queryBuilder.Append(columnsClause);
            queryBuilder.Append(fromClause);
            queryBuilder.Append(whereClause);
            queryBuilder.Append(" order by n.NotificationId desc ");

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
                        if (NotificationColumn.timeStamp == desiredValues[colCtr])
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
