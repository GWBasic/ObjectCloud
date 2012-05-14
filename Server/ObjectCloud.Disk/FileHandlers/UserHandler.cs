// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Xml;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.Disk.FileHandlers.Particle;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.ORM.DataAccess.WhereConditionals;

namespace ObjectCloud.Disk.FileHandlers
{
    public class UserHandler : FileHandler, IUserHandler
    {
		[Serializable]
		internal class UserData
		{
			public Dictionary<string, string> nameValuePairs = new Dictionary<string, string>();
			public Dictionary<string, Trusted> trusted = new Dictionary<string, Trusted>();
		}

		[Serializable]
		internal class Trusted
		{
			public bool? login;
			public bool? link;
		}
				
		[Serializable]
		internal class Notification : IHasTimeStamp
		{
			public DateTime timeStamp;
			public string senderIdentity;
			public string objectUrl;
			public string summaryView;
			public string documentType;
			public string verb;
			public string changeData;
			public string linkedSenderIdentity;

			public DateTime TimeStamp
			{
				get { return this.timeStamp; }
			}
		}
		
        //static ILog log = LogManager.GetLogger<UserHandler>();

        internal UserHandler(PersistedBinaryFormatterObject<UserData> persistedUserData, PersistedObjectSequence<Notification> persistedNotifications, FileHandlerFactoryLocator fileHandlerFactoryLocator)
            : base(fileHandlerFactoryLocator) 
		{
			this.persistedUserData = persistedUserData;
			this.persistedNotifications = persistedNotifications;
		}
				
		private PersistedBinaryFormatterObject<UserData> persistedUserData;
		private PersistedObjectSequence<Notification> persistedNotifications;
		
		
        public string this[string name]
        {
            get
            {
				return this.persistedUserData.Read(userData =>
				{
					string value;
					if (userData.nameValuePairs.TryGetValue(name, out value))
						return value;
					
					return null;
				});
            }
        }

        public void Set(IUser changer, string name, string value)
        {
			this.persistedUserData.Write(userData =>
			{
				if (null != value)
					userData.nameValuePairs[name] = value;
				else
					userData.nameValuePairs.Remove(name);
			});
        }

        public bool Contains(string key)
        {
			return this.persistedUserData.Read(userData =>
			{
				return userData.nameValuePairs.ContainsKey(key);
			});
        }

        public void Clear(IUser changer)
        {
			this.persistedUserData.Write(userData =>
			{
				userData.nameValuePairs.Clear();
			});
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
			IEnumerable<KeyValuePair<string, string>> enumerable = this.persistedUserData.Read(userData =>
			{
				return userData.nameValuePairs.ToArray();
			});
			
			return enumerable.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void WriteAll(IUser changer, IEnumerable<KeyValuePair<string, string>> contents, bool clearExisting)
        {
			this.persistedUserData.Write(userData =>
			{
				if (clearExisting)
					userData.nameValuePairs.Clear();
				
				foreach (var pair in contents)
					userData.nameValuePairs[pair.Key] = pair.Value;
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
			
			this.persistedNotifications.Append(new Notification()
			{
                changeData = changeData,
                documentType = documentType,
                linkedSenderIdentity = linkedSenderIdentity,
                objectUrl = objectUrl,
                senderIdentity = senderIdentity,
                summaryView = summaryView,
                timeStamp = timestamp,
                verb = verb
			});

            Dictionary<NotificationColumn, object> notificationForEvent = new Dictionary<NotificationColumn, object>();
            notificationForEvent[NotificationColumn.ChangeData] = changeData;
            notificationForEvent[NotificationColumn.DocumentType] = documentType;
            notificationForEvent[NotificationColumn.NotificationId] = timestamp.Ticks;
            notificationForEvent[NotificationColumn.ObjectUrl] = objectUrl;
            notificationForEvent[NotificationColumn.SenderIdentity] = senderIdentity;
            notificationForEvent[NotificationColumn.SummaryView] = summaryView;
            notificationForEvent[NotificationColumn.Timestamp] = timestamp;
            notificationForEvent[NotificationColumn.LinkedSenderIdentity] = linkedSenderIdentity;
            notificationForEvent[NotificationColumn.Verb] = verb;

            OnNotificationRecieved(notificationForEvent);
        }

        public IEnumerable<Dictionary<NotificationColumn, object>> GetNotifications(
            DateTime? newestNotificationNullable,
            long? maxNotificationsLong,
            HashSet<string> objectUrls,
            HashSet<string> senderIdentities,
            HashSet<NotificationColumn> desiredValues)
        {
            if (this == FileHandlerFactoryLocator.UserFactory.AnonymousUser.UserHandler)
                throw new SecurityException("The anonymous user can not recieve notifications");

            int maxNotifications;
            if (null != maxNotificationsLong)
                maxNotifications = Convert.ToInt32(maxNotificationsLong.Value);
			else
				maxNotifications = 5000;
			
			
			var newest = newestNotificationNullable ?? DateTime.MaxValue;
			
			var notifications = this.persistedNotifications.ReadSequence(newest, maxNotifications, notification =>
			{
				if (null != objectUrls)
					if (!objectUrls.Contains(notification.objectUrl))
						return false;
				
				if (null != senderIdentities)
					if (!senderIdentities.Contains(notification.senderIdentity))
					    return false;
					    
				return true;
			});

			foreach (var notification in notifications)
            {
                Dictionary<NotificationColumn, object> toYield = new Dictionary<NotificationColumn, object>();

                if (desiredValues.Contains(NotificationColumn.NotificationId))
                    toYield[NotificationColumn.NotificationId] = notification.timeStamp.Ticks;

                if (desiredValues.Contains(NotificationColumn.ChangeData))
                    toYield[NotificationColumn.ChangeData] = notification.changeData;

                if (desiredValues.Contains(NotificationColumn.DocumentType))
                    toYield[NotificationColumn.DocumentType] = notification.documentType;

                if (desiredValues.Contains(NotificationColumn.LinkedSenderIdentity))
                    toYield[NotificationColumn.LinkedSenderIdentity] = notification.linkedSenderIdentity;

                if (desiredValues.Contains(NotificationColumn.ObjectUrl))
                    toYield[NotificationColumn.ObjectUrl] = notification.objectUrl;

                if (desiredValues.Contains(NotificationColumn.SenderIdentity))
                    toYield[NotificationColumn.SenderIdentity] = notification.senderIdentity;

                if (desiredValues.Contains(NotificationColumn.SummaryView))
                    toYield[NotificationColumn.SummaryView] = notification.summaryView;

                if (desiredValues.Contains(NotificationColumn.Timestamp))
                    toYield[NotificationColumn.Timestamp] = notification.timeStamp;

                if (desiredValues.Contains(NotificationColumn.Verb))
                    toYield[NotificationColumn.Verb] = notification.verb;

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
			domain = domain.ToLowerInvariant();
			this.persistedUserData.Write(userData =>
			{
				Trusted trusted;
				if (!userData.trusted.TryGetValue(domain, out trusted))
					userData.trusted[domain] = trusted = new Trusted();
				
				trusted.login = remember;
			});
        }

        public bool IsRememberOpenIDLogin(string domain)
        {
			return this.persistedUserData.Read(userData =>
			{
				Trusted trusted;
				if (userData.trusted.TryGetValue(domain, out trusted))
	                if (null != trusted.login)
    	                return trusted.login.Value;
				
				return false;
			});
        }

        public void SetRememberOpenIDLink(string domain, bool remember)
        {
			domain = domain.ToLowerInvariant();
			this.persistedUserData.Write(userData =>
			{
				Trusted trusted;
				if (!userData.trusted.TryGetValue(domain, out trusted))
					userData.trusted[domain] = trusted = new Trusted();

				trusted.link = remember;
			});
        }

        public bool IsRememberOpenIDLink(string domain)
        {
			return this.persistedUserData.Read(userData =>
			{
				Trusted trusted;
				if (userData.trusted.TryGetValue(domain, out trusted))
	                if (null != trusted.link)
    	                return trusted.link.Value;
				
				return false;
			});
        }
    }
}
