// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Xml;

using JsonFx.Json;
using NUnit.Framework;

using ObjectCloud.Common;
using ObjectCloud.Common.Threading;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;
using ObjectCloud.Disk.Implementation;
using ObjectCloud.WebServer.Test;

namespace ObjectCloud.Particle.UnitTests
{
    public class TestParticle : HasThirdServer
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

            EndpointInfo recipientInfo = default(EndpointInfo);
            GenericArgument<EndpointInfo> callback = delegate(EndpointInfo recipientInfoCB)
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
                FileHandlerFactoryLocator.UserManagerHandler.GetEndpointInfos(
                    user,
                    false,
                    new string[] { target.Identity },
                    ParticleEndpoint.ReceiveNotification,
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
                    recipientInfo.Endpoint);
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
            Assert.AreEqual(file.FileContainer.GenerateSummaryView(), notification[NotificationColumn.SummaryView]);
            Assert.IsNotNull(notification[NotificationColumn.Timestamp]);
        }

        /// <summary>
        /// Verifies linking
        /// </summary>
        [Test]
        public void VerifyLinkConfirmation()
        {
            string hostUsername = "user" + SRandom.Next(0, int.MaxValue).ToString();
            string identityUsername = "user" + SRandom.Next(0, int.MaxValue).ToString();
            string recipientUsername = "user" + SRandom.Next(0, int.MaxValue).ToString();

            IUser hostUser = FileHandlerFactoryLocator.UserManagerHandler.CreateUser(hostUsername, "pw");
            IUser identityUser = SecondFileHandlerFactoryLocator.UserManagerHandler.CreateUser(identityUsername, "pw");
            IUser recipientUser = ThirdFileHandlerFactoryLocator.UserManagerHandler.CreateUser(recipientUsername, "pw");

            List<Dictionary<NotificationColumn, object>> incomingNotifications = new List<Dictionary<NotificationColumn, object>>();
            EventHandler<IUserHandler,EventArgs<Dictionary<NotificationColumn,object>>> nreh = 
                delegate(IUserHandler sender, EventArgs<Dictionary<NotificationColumn, object>> e)
                {
                    lock (incomingNotifications)
                    {
                        incomingNotifications.Add(e.Value);
                        Monitor.Pulse(incomingNotifications);
                    }
                };

            hostUser.UserHandler.NotificationRecieved += nreh;
            identityUser.UserHandler.NotificationRecieved += nreh;
            recipientUser.UserHandler.NotificationRecieved += nreh;

            IUser identityUserOnHost = FileHandlerFactoryLocator.UserManagerHandler.GetOpenIdUser(identityUser.Identity);
            IUser recipientUserOnHost = FileHandlerFactoryLocator.UserManagerHandler.GetOpenIdUser(recipientUser.Identity);

            IFileHandler file = TestDirectory.CreateFile("test" + SRandom.Next().ToString(), "text", hostUser.Id);
            TestDirectory.SetPermission(
                hostUser.Id,
                file.FileContainer.Filename,
                new ID<IUserOrGroup, Guid>[] { identityUserOnHost.Id, recipientUserOnHost.Id },
                FilePermissionEnum.Read,
                true,
                true);

            IFileHandler linked = TestDirectory.CreateFile("linked" + SRandom.Next().ToString(), "text", identityUserOnHost.Id);
            LinkNotificationInformation lci = TestDirectory.AddRelationship(
                file.FileContainer,
                linked.FileContainer,
                "reply",
                true);

            HttpWebClient httpWebClient = new HttpWebClient();

            Login(httpWebClient, identityUser.Name, "pw", SecondFileHandlerFactoryLocator.WebServer.Port);

            HttpResponseHandler response = httpWebClient.Post(
                SecondFileHandlerFactoryLocator.UserManagerHandler.FileContainer.ObjectUrl + "?Method=UserConfirmLink",
                    new KeyValuePair<string, string>("objectUrl", file.FileContainer.ObjectUrl),
                    new KeyValuePair<string, string>("ownerIdentity", identityUser.Identity),
                    new KeyValuePair<string, string>("linkSummaryView", lci.linkSummaryView),
                    new KeyValuePair<string, string>("linkUrl", linked.FileContainer.ObjectUrl),
                    new KeyValuePair<string, string>("linkDocumentType", linked.FileContainer.DocumentType),
                    new KeyValuePair<string, string>("linkID", linked.FileContainer.DocumentType),
                    new KeyValuePair<string, string>("recipients", JsonWriter.Serialize(new string[] { hostUser.Identity, identityUser.Identity, recipientUser.Identity })),
                    new KeyValuePair<string, string>("linkID", lci.linkID),
                    new KeyValuePair<string, string>("redirectUrl", "/"));

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            lock (incomingNotifications)
            {
                while (incomingNotifications.Count < 6)

                    if (!Monitor.Wait(incomingNotifications, 10000))
                        Assert.Fail("Did not get a confirmation notification");
            }

            Assert.AreEqual(6, incomingNotifications.Count);

            int numLinkNotifications = 0;
            int numLinkIDs = 0;

            foreach (Dictionary<NotificationColumn, object> notification in incomingNotifications)
                if ("link" == notification[NotificationColumn.Verb].ToString())
                {
                    numLinkNotifications++;

                    Assert.AreEqual(file.FileContainer.ObjectUrl, notification[NotificationColumn.ObjectUrl]);
                    Assert.AreEqual(file.FileContainer.Owner.Identity, notification[NotificationColumn.SenderIdentity]);

                    Dictionary<string, object> linkChangeData =
                        JsonReader.Deserialize<Dictionary<string, object>>(notification[NotificationColumn.ChangeData].ToString());

                    Assert.IsTrue(linkChangeData.ContainsKey("linkUrl"));
                    Assert.AreEqual(linked.FileContainer.ObjectUrl, linkChangeData["linkUrl"]);

                    Assert.IsTrue(linkChangeData.ContainsKey("linkSummaryView"));
                    Assert.AreEqual(linked.FileContainer.GenerateSummaryView(), linkChangeData["linkSummaryView"]);

                    Assert.IsTrue(linkChangeData.ContainsKey("linkDocumentType"));
                    Assert.AreEqual(linked.FileContainer.DocumentType, linkChangeData["linkDocumentType"]);

                    Assert.IsTrue(linkChangeData.ContainsKey("ownerIdentity"));
                    Assert.AreEqual(linked.FileContainer.Owner.Identity, linkChangeData["ownerIdentity"]);

                    if (linkChangeData.ContainsKey("linkID"))
                        numLinkIDs++;
                }

            Assert.AreEqual(3, numLinkNotifications);
            Assert.AreEqual(1, numLinkIDs); // Because linkID isn't removed from local notifications
        }
    }
}
