// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;

using JsonFx.Json;
using NUnit.Framework;

using ObjectCloud.Common;
using ObjectCloud.Common.Threading;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Disk.Implementation;
using ObjectCloud.WebServer.Test;

namespace ObjectCloud.Particle.UnitTests
{
    public class TestParticle : HasSecondServer
    {
        private IDirectoryHandler TestDirectory
        {
            get
            {
                if (null == _TestDirectory)
                    _TestDirectory = FileHandlerFactoryLocator.FileSystemResolver.RootDirectoryHandler.CreateFile(
                        "scratch" + SRandom.Next().ToString(), "directory", null).FileContainer.CastFileHandler<IDirectoryHandler>();

                return _TestDirectory;
            }
        }
        private IDirectoryHandler _TestDirectory = null;

        [Test]
        public void TestGetRecipientInfoSanity()
        {
            string username = "user" + SRandom.Next(0, int.MaxValue).ToString();
            string secondUsername = "user" + SRandom.Next(0, int.MaxValue).ToString();

            IUser user = FileHandlerFactoryLocator.UserManagerHandler.CreateUser(username, "pw");
            IUser target = SecondFileHandlerFactoryLocator.UserManagerHandler.CreateUser(secondUsername, "pw");

            object pulser = new object();

            RecipientInfo recipientInfo = default(RecipientInfo);
            GenericArgument<RecipientInfo> callback = delegate(RecipientInfo recipientInfoCB)
            {
                recipientInfo = recipientInfoCB;

                lock (pulser)
                    Monitor.Pulse(pulser);
            };

            Exception e = null;
            GenericArgument<IEnumerable<string>> errorCallback = delegate(IEnumerable<string> recipients)
            {
                e = new Exception("Could not establish trust with " + StringGenerator.GenerateCommaSeperatedList(recipients));

                lock (pulser)
                    Monitor.Pulse(pulser);
            };

            GenericArgument<Exception> exceptionCallback = delegate(Exception ex)
            {
                e = ex;

                lock (pulser)
                    Monitor.Pulse(pulser);
            };

            lock (pulser)
            {
                FileHandlerFactoryLocator.UserManagerHandler.GetRecipientInfos(
                    user,
                    false,
                    new string[] { target.Identity },
                    callback,
                    errorCallback,
                    exceptionCallback);

                Monitor.Wait(pulser);
            }

            if (null != e)
            {
                Console.WriteLine(e.StackTrace);
                throw e;
            }

            Assert.IsNotNull(recipientInfo);
            Assert.IsNotNull(recipientInfo.SenderToken);
            Assert.AreEqual(
                    string.Format("http://{0}/Users/UserDB?Method=ReceiveNotification", SecondFileHandlerFactoryLocator.HostnameAndPort),
                    recipientInfo.RecieveNotificationEndpoint);
        }

        [Test]
        public void TestSendLocalNotification()
        {
            string username = "user" + SRandom.Next(0, int.MaxValue).ToString();
            IUser user = FileHandlerFactoryLocator.UserManagerHandler.CreateUser(username, "pw");

            VerifyNotificationRecieved(user, user);
        }

        [Test]
        public void TestSendRemoteNotification()
        {
            string username = "user" + SRandom.Next(0, int.MaxValue).ToString();
            IUser user = SecondFileHandlerFactoryLocator.UserManagerHandler.CreateUser(username, "pw");

            VerifyNotificationRecieved(user, FileHandlerFactoryLocator.UserManagerHandler.GetOpenIdUser(user.Identity));
        }

        private void VerifyNotificationRecieved(IUser recipient, IUser localRecipient)
        {
            string username = "user" + SRandom.Next(0, int.MaxValue).ToString();
            IUser sender = FileHandlerFactoryLocator.UserManagerHandler.CreateUser(username, "pw");

            IFileHandler file = TestDirectory.CreateFile("test" + SRandom.Next().ToString(), "text", sender.Id);

            object key = new object();
            Dictionary<NotificationColumn, object> notification = null;
            recipient.UserHandler.NotificationRecieved += delegate(IUserHandler notificationSender, EventArgs<Dictionary<NotificationColumn, object>> e)
            {
                notification = e.Value;

                lock (key)
                    Monitor.Pulse(key);
            };

            lock (key)
            {
                TestDirectory.SetPermission(sender.Id, file.FileContainer.Filename, new ID<IUserOrGroup, Guid>[] { localRecipient.Id }, FilePermissionEnum.Read, true, true);

                bool notified = Monitor.Wait(key, TimeSpan.FromSeconds(20));
                Assert.IsTrue(notified, "Timeout waiting for notification");
            }

            Assert.IsNotNull(notification);

            VerifyNotificiationRecieved(recipient, sender, file, notification);

            List<Dictionary<NotificationColumn, object>> notifications = new List<Dictionary<NotificationColumn,object>>(
                recipient.UserHandler.GetNotifications(null, null, null, null, null, new Set<NotificationColumn>(Enum<NotificationColumn>.Values)));

            Assert.AreEqual(1, notifications.Count);
            VerifyNotificiationRecieved(recipient, sender, file, notifications[0]);
        }

        private static void VerifyNotificiationRecieved(IUser recipient, IUser sender, IFileHandler file, Dictionary<NotificationColumn, object> notification)
        {
            Assert.AreEqual("share", notification[NotificationColumn.Verb]);
            Assert.AreEqual(file.FileContainer.ObjectUrl, notification[NotificationColumn.ObjectUrl]);
            Assert.AreEqual(sender.Identity, notification[NotificationColumn.SenderIdentity]);
            Assert.AreEqual("text", notification[NotificationColumn.DocumentType]);

            // Verify recipients
            Assert.IsNotNull(notification[NotificationColumn.ChangeData]);
            object parsedChangeData = JsonReader.Deserialize(notification[NotificationColumn.ChangeData].ToString());
            Assert.IsInstanceOf<System.Collections.IEnumerable>(parsedChangeData);
            Set<string> recipients = new Set<string>(Enumerable<string>.Cast((System.Collections.IEnumerable)parsedChangeData));

            Assert.AreEqual(2, recipients.Count);
            Assert.IsTrue(recipients.Contains(sender.Identity));
            Assert.IsTrue(recipients.Contains(recipient.Identity));

            // sumaryView and timestamp are untested
            Assert.IsNotNull(notification[NotificationColumn.SummaryView]);
            Assert.IsNotNull(notification[NotificationColumn.Timestamp]);
        }
    }
}
