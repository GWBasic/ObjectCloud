// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Xml;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.DataAccess.User;
using ObjectCloud.Disk.FileHandlers.Particle;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.ORM.DataAccess.WhereConditionals;

namespace ObjectCloud.Disk.FileHandlers
{
    public class UserHandler : HasDatabaseFileHandler<IDatabaseConnector, IDatabaseConnection, IDatabaseTransaction>, IUserHandler
    {
        //static ILog log = LogManager.GetLogger<UserHandler>();

        public UserHandler(IDatabaseConnector databaseConnector, FileHandlerFactoryLocator fileHandlerFactoryLocator)
            : base(databaseConnector, fileHandlerFactoryLocator) { }
						
        public string this[string name]
        {
            get
            {
                IPairs_Readable pair = DatabaseConnection.Pairs.SelectSingle(Pairs_Table.Name == name);

                if (null == pair)
                    return null;

                return pair.Value;
            }
        }

        public void Set(IUser changer, string name, string value)
        {
            DatabaseConnection.CallOnTransaction(delegate(IDatabaseTransaction transaction)
            {
                // Not sure if it's worth trying to update as opposed to delete...
                // Update might be faster, but right now the data access system doesn't support it!
                DatabaseConnection.Pairs.Delete(Pairs_Table.Name == name);

                if (null != value)
                    DatabaseConnection.Pairs.Insert(delegate(IPairs_Writable pair)
                    {
                        pair.Name = name;
                        pair.Value = value;
                    });

                transaction.Commit();
            });
        }

        public bool Contains(string key)
        {
            return this[key] != null;
        }

        public void Clear(IUser changer)
        {
            DatabaseConnection.Pairs.Delete();
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            List<KeyValuePair<string, string>> toReturn = new List<KeyValuePair<string, string>>();

            foreach (IPairs_Readable pair in DatabaseConnection.Pairs.Select())
                toReturn.Add(new KeyValuePair<string, string>(pair.Name, pair.Value));

            return toReturn.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void WriteAll(IUser changer, IEnumerable<KeyValuePair<string, string>> contents, bool clearExisting)
        {
            DatabaseConnection.CallOnTransaction(delegate(IDatabaseTransaction transaction)
            {
                if (clearExisting)
                    DatabaseConnection.Pairs.Delete();

                foreach (KeyValuePair<string, string> kvp in contents)
                    Write(transaction, kvp.Key, kvp.Value);

                transaction.Commit();
            });
        }

        /// <summary>
        /// Writes a pair onto a transaction
        /// </summary>
        /// <param name="transaction"></param>
        /// <param name="name"></param>
        /// <param name="value"></param>
        private void Write(IDatabaseTransaction transaction, string name, string value)
        {
            DatabaseConnection.Pairs.Delete(Pairs_Table.Name == name);

            DatabaseConnection.Pairs.Insert(delegate(IPairs_Writable pair)
            {
                pair.Name = name;
                pair.Value = value;
            });
        }

        public override void Dump(string path, ID<IUserOrGroup, Guid> userId)
        {
            throw new NotImplementedException("Can not dump the user database");
        }

        public string Name
        {
            get { return this["name"]; }
            set { Set(null, "name", value); }
        }

        public string Identity
        {
            get
            {
                string name = Name;

                if (name.StartsWith("http://") || name.StartsWith("https://"))
                    return name;

                return string.Format(
                    "http://{0}/Users/{1}.user",
                    FileHandlerFactoryLocator.HostnameAndPort,
                    name);
            }
        }

        /// <summary>
        /// Receives a notification
        /// </summary>
        /// <param name="senderToken"></param>
        /// <param name="objectUrl"></param>
        /// <param name="title"></param>
        /// <param name="documentType"></param>
        /// <param name="messageSummary"></param>
        /// <param name="changeData"></param>
        public void ReceiveNotification(
            string senderIdentity,
            string objectUrl,
            string summaryView,
            string documentType,
            string verb,
            string changeData,
            string linkedSenderIdentity)
        {
            if (this == FileHandlerFactoryLocator.UserFactory.AnonymousUser.UserHandler)
                throw new SecurityException("The anonymous user can not recieve notifications");

            DateTime timestamp = DateTime.UtcNow;

            long notificationId = DatabaseConnection.Notification.InsertAndReturnPK<long>(delegate(INotification_Writable notification)
            {
                notification.ChangeData = changeData;
                notification.DocumentType = documentType;
                notification.LinkedSenderIdentity = linkedSenderIdentity;
                notification.ObjectUrl = objectUrl;
                notification.SenderIdentity = senderIdentity;
                notification.SummaryView = summaryView;
                notification.TimeStamp = timestamp;
                notification.Verb = verb;
            });

            Dictionary<NotificationColumn, object> notificationForEvent = new Dictionary<NotificationColumn, object>();
            notificationForEvent[NotificationColumn.ChangeData] = changeData;
            notificationForEvent[NotificationColumn.DocumentType] = documentType;
            notificationForEvent[NotificationColumn.NotificationId] = notificationId;
            notificationForEvent[NotificationColumn.ObjectUrl] = objectUrl;
            notificationForEvent[NotificationColumn.SenderIdentity] = senderIdentity;
            notificationForEvent[NotificationColumn.SummaryView] = summaryView;
            notificationForEvent[NotificationColumn.Timestamp] = timestamp;
            notificationForEvent[NotificationColumn.LinkedSenderIdentity] = linkedSenderIdentity;
            notificationForEvent[NotificationColumn.Verb] = verb;

            OnNotificationRecieved(notificationForEvent);
        }

        public IEnumerable<Dictionary<NotificationColumn, object>> GetNotifications(
            long? newestNotificationId,
            long? oldestNotificationId,
            long? maxNotificationsLong,
            IEnumerable<string> objectUrls,
            IEnumerable<string> senderIdentities,
            HashSet<NotificationColumn> desiredValues)
        {
            if (this == FileHandlerFactoryLocator.UserFactory.AnonymousUser.UserHandler)
                throw new SecurityException("The anonymous user can not recieve notifications");

            uint? maxNotifications = null;
            if (null != maxNotificationsLong)
                maxNotifications = Convert.ToUInt32(maxNotificationsLong.Value);

            // Build conditions
            List<ComparisonCondition> comparisonConditions = new List<ComparisonCondition>();

            if (null != newestNotificationId)
                comparisonConditions.Add(Notification_Table.NotificationId <= newestNotificationId.Value);

            if (null != oldestNotificationId)
                comparisonConditions.Add(Notification_Table.NotificationId >= oldestNotificationId.Value);

            if (null != objectUrls)
                comparisonConditions.Add(Notification_Table.ObjectUrl.In(objectUrls));

            if (null != senderIdentities)
                comparisonConditions.Add(Notification_Table.SenderIdentity.In(senderIdentities) | Notification_Table.LinkedSenderIdentity.In(senderIdentities));

            // run query and construct result
            foreach (INotification_Readable notification in DatabaseConnection.Notification.Select(
                ComparisonCondition.Condense(comparisonConditions),
                maxNotifications,
                ORM.DataAccess.OrderBy.Desc,
                Notification_Table.NotificationId))
            {
                Dictionary<NotificationColumn, object> toYield = new Dictionary<NotificationColumn, object>();

                if (desiredValues.Contains(NotificationColumn.NotificationId))
                    toYield[NotificationColumn.NotificationId] = notification.NotificationId;

                if (desiredValues.Contains(NotificationColumn.ChangeData))
                    toYield[NotificationColumn.ChangeData] = notification.ChangeData;

                if (desiredValues.Contains(NotificationColumn.DocumentType))
                    toYield[NotificationColumn.DocumentType] = notification.DocumentType;

                if (desiredValues.Contains(NotificationColumn.LinkedSenderIdentity))
                    toYield[NotificationColumn.LinkedSenderIdentity] = notification.LinkedSenderIdentity;

                if (desiredValues.Contains(NotificationColumn.ObjectUrl))
                    toYield[NotificationColumn.ObjectUrl] = notification.ObjectUrl;

                if (desiredValues.Contains(NotificationColumn.SenderIdentity))
                    toYield[NotificationColumn.SenderIdentity] = notification.SenderIdentity;

                if (desiredValues.Contains(NotificationColumn.SummaryView))
                    toYield[NotificationColumn.SummaryView] = notification.SummaryView;

                if (desiredValues.Contains(NotificationColumn.Timestamp))
                    toYield[NotificationColumn.Timestamp] = notification.TimeStamp;

                if (desiredValues.Contains(NotificationColumn.Verb))
                    toYield[NotificationColumn.Verb] = notification.Verb;

                yield return toYield;
            }

            /*List<Dictionary<NotificationColumn, object>> toReturn = new List<Dictionary<NotificationColumn, object>>(
                DatabaseConnection.GetNotifications(newestNotificationId, oldestNotificationId, maxNotifications, objectUrl, sender, desiredValues));

            return toReturn;*/
        }

        public override string ToString()
        {
            return Identity;
        }

        public event EventHandler<IUserHandler, EventArgs<Dictionary<NotificationColumn, object>>> NotificationRecieved;

        /// <summary>
        /// Call whenever NotificationRecieved is to occur
        /// </summary>
        /// <param name="notification"></param>
        protected void OnNotificationRecieved(Dictionary<NotificationColumn, object> notification)
        {
            if (null != NotificationRecieved)
                NotificationRecieved(this, new EventArgs<Dictionary<NotificationColumn, object>>(notification));
        }


        public void SetRememberOpenIDLogin(string domain, bool remember)
        {
            DatabaseConnection.Trusted.Upsert(
                Trusted_Table.Domain == domain.ToLowerInvariant(),
                delegate(ITrusted_Writable trusted)
                {
                    trusted.Login = remember;
                });
        }

        public bool IsRememberOpenIDLogin(string domain)
        {
            ITrusted_Readable trusted = DatabaseConnection.Trusted.SelectSingle(
                Trusted_Table.Domain == domain.ToLowerInvariant());

            if (null != trusted)
                if (null != trusted.Login)
                    return trusted.Login.Value;

            return false;
        }

        public void SetRememberOpenIDLink(string domain, bool remember)
        {
            DatabaseConnection.Trusted.Upsert(
                Trusted_Table.Domain == domain.ToLowerInvariant(),
                delegate(ITrusted_Writable trusted)
                {
                    trusted.Link = remember;
                });
        }

        public bool IsRememberOpenIDLink(string domain)
        {
            ITrusted_Readable trusted = DatabaseConnection.Trusted.SelectSingle(
                Trusted_Table.Domain == domain.ToLowerInvariant());

            if (null != trusted)
                if (null != trusted.Link)
                    return trusted.Link.Value;

            return false;
        }
    }
}
