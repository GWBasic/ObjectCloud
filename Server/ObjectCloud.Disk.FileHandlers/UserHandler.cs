// Copyright 2009, 2010 Andrew Rondeau
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

namespace ObjectCloud.Disk.FileHandlers
{
    public class UserHandler : HasDatabaseFileHandler<IDatabaseConnector, IDatabaseConnection, IDatabaseTransaction>, IUserHandler
    {
        static ILog log = LogManager.GetLogger<UserHandler>();

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
        /// Sends a notification
        /// </summary>
        /// <param name="openId">The OpenId to send the notification to</param>
        /// <param name="objectUrl">The object that this notification applies to.  This must be the same domain as the OpenId</param>
        /// <param name="title">The document's title</param>
        /// <param name="documentType">The document type</param>
        /// <param name="messageSummary">The message summary.  This is displayed in the user's notifications viewer GUI</param>
        /// <param name="changeData">The changeData</param>
        /// <param name="forceRefreshSenderToken">true to force a refresh of the sender token</param>
        /// <param name="forceRefreshEndpoints">true to force a refresh of the endpoints</param>
        /// <param name="maxRetries">The maximum number of times to retry</param>
        /// <param name="transportErrorDelay">The amount of time to wait before a retry when there is a transport error</param>
        public void SendNotification(
            string openId,
            string objectUrl,
            string title,
            string documentType,
            string messageSummary,
            string changeData)
        {
            SendNotification(openId, objectUrl, title, documentType, messageSummary, changeData, false, false, 42, TimeSpan.FromMinutes(3));
        }

        /// <summary>
        /// Sends a notification
        /// </summary>
        /// <param name="openId">The OpenId to send the notification to</param>
        /// <param name="objectUrl">The object that this notification applies to.  This must be the same domain as the OpenId</param>
        /// <param name="title">The document's title</param>
        /// <param name="documentType">The document type</param>
        /// <param name="messageSummary">The message summary.  This is displayed in the user's notifications viewer GUI</param>
        /// <param name="changeData">The changeData</param>
        /// <param name="forceRefreshSenderToken">true to force a refresh of the sender token</param>
        /// <param name="forceRefreshEndpoints">true to force a refresh of the endpoints</param>
        /// <param name="maxRetries">The maximum number of times to retry</param>
        /// <param name="transportErrorDelay">The amount of time to wait before a retry when there is a transport error</param>
        public void SendNotification(
            string openId,
            string objectUrl,
            string title,
            string documentType,
            string messageSummary,
            string changeData,
            bool forceRefreshSenderToken,
            bool forceRefreshEndpoints,
            int maxRetries,
            TimeSpan transportErrorDelay)
        {
            /*/ TODO:  This should be refactored to be queued!

            // Don't send notifications if the server isn't running
            // TODO:  Move these if local notifications are sent without HTTP
            if (!FileHandlerFactoryLocator.FileSystemResolver.IsStarted)
                return;
            if (null == FileHandlerFactoryLocator.WebServer)
                return;
            if (!FileHandlerFactoryLocator.WebServer.Running)
                return;

            // The notification is lost if it can't be sent.  Notifications are lossy!
            if (maxRetries < 0)
                return;

            OLD_Endpoints endpoints = OLD_Endpoints.GetEndpoints(openId, forceRefreshEndpoints);

            // (For now) senderTokens are always established through the web
            string senderToken = GetSenderToken(openId, forceRefreshSenderToken, endpoints);

            // If the notification goes to a local user, then skip the web (except to establish trust
            if (openId.StartsWith("http://" + FileHandlerFactoryLocator.HostnameAndPort + "/"))
            {
                string userFile = openId.Substring(((string)("http://" + FileHandlerFactoryLocator.HostnameAndPort)).Length);

                if (FileHandlerFactoryLocator.FileSystemResolver.IsFilePresent(userFile))
                {
                    IFileContainer recipientContainer = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(userFile);

                    if (recipientContainer.FileHandler is IUserHandler)
                    {
                        // At this point, the notification is only sent without HTTP if the user is local, a file exists for the user's identity, and that file is an IUserHander
                        IUserHandler recipient = recipientContainer.CastFileHandler<IUserHandler>();

                        try
                        {
                            recipient.ReceiveNotification(senderToken, objectUrl, title, documentType, messageSummary, changeData);
                        }
                        catch (ParticleException.BadToken)
                        {
                            SendNotification(openId, objectUrl, title, documentType, messageSummary, changeData, true, false, maxRetries - 1, transportErrorDelay);
                        }

                        return;
                    }
                }
            }

            string receiveNotificationEndpoint = endpoints["receiveNotification"];

            HttpWebClient httpWebClient = new HttpWebClient();
            httpWebClient.Timeout = transportErrorDelay;

            try
            {
                HttpResponseHandler responseHandler = httpWebClient.Post(receiveNotificationEndpoint,
                    new KeyValuePair<string, string>("senderToken", senderToken),
                    new KeyValuePair<string, string>("objectUrl", objectUrl),
                    new KeyValuePair<string, string>("title", title),
                    new KeyValuePair<string, string>("documentType", documentType),
                    new KeyValuePair<string, string>("messageSummary", messageSummary),
                    new KeyValuePair<string, string>("changeData", changeData));

                // If the notification was sent correctly, return
                if (HttpStatusCode.Accepted == responseHandler.StatusCode)
                    return;

                // If the recipient demands a new senderToken, then recurse
                if (HttpStatusCode.PreconditionFailed == responseHandler.StatusCode)
                    if ("senderToken".Equals(responseHandler.AsString()))
                        SendNotification(openId, objectUrl, title, documentType, messageSummary, changeData, true, false, maxRetries - 1, transportErrorDelay);

                // TODO:  Handle blocked!

                // Keep retrying...
                SendNotification(openId, objectUrl, title, documentType, messageSummary, changeData, false, false, maxRetries - 1, transportErrorDelay);
            }
            catch (WebException we)
            {
                log.Error("Exception occured when attempting to send a notification to " + openId, we);
                SendNotification(openId, objectUrl, title, documentType, messageSummary, changeData, false, false, maxRetries - 1, transportErrorDelay);
            }*/
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

            /*Dictionary<NotificationColumn, object> notificationForEvent = new Dictionary<NotificationColumn, object>();
            notificationForEvent[NotificationColumn.changeData] = changeData;
            notificationForEvent[NotificationColumn.documentType] = documentType;
            notificationForEvent[NotificationColumn.messageSummary] = messageSummary;
            notificationForEvent[NotificationColumn.notificationId] = notificationId;
            notificationForEvent[NotificationColumn.objectUrl] = objectUrl;
            notificationForEvent[NotificationColumn.sender] = sender;
            notificationForEvent[NotificationColumn.state] = NotificationState.unread;
            notificationForEvent[NotificationColumn.timeStamp] = timestamp;
            notificationForEvent[NotificationColumn.title] = title;

            OnNotificationRecieved(notificationForEvent);*/
        }

        public IEnumerable<Dictionary<NotificationColumn, object>> GetNotifications(
            long? newestNotificationId,
            long? oldestNotificationId,
            long? maxNotifications,
            string objectUrl,
            string sender,
            List<NotificationColumn> desiredValues)
        {
            List<Dictionary<NotificationColumn, object>> toReturn = new List<Dictionary<NotificationColumn, object>>(
                DatabaseConnection.GetNotifications(newestNotificationId, oldestNotificationId, maxNotifications, objectUrl, sender, desiredValues));

            return toReturn;
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
    }
}
