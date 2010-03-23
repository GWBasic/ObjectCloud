// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Net;
using System.Text;
using System.Threading;

using NUnit.Framework;

using JsonFx.Json;

using ObjectCloud.Common;
using ObjectCloud.Disk.FileHandlers.Particle;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;
using ObjectCloud.WebServer.Implementation;

namespace ObjectCloud.WebServer.Test.Particle
{
    [TestFixture]
    public class TestNotifications : HasSecondServer
    {
        private HttpResponseHandler SendNotification(HttpWebClient httpWebClient, string objectUrl, string title, string documentType, string messageSummary, string changeData)
        {
            HttpResponseHandler webResponse = httpWebClient.Post("http://localhost:" + WebServer.Port.ToString() + "/Users/root.user?Method=SendNotification",
                new KeyValuePair<string, string>("openId", "http://localhost:" + SecondWebServer.Port.ToString() + "/Users/root.user"),
                new KeyValuePair<string, string>("objectUrl", objectUrl),
                new KeyValuePair<string, string>("title", title),
                new KeyValuePair<string, string>("documentType", documentType),
                new KeyValuePair<string, string>("messageSummary", messageSummary),
                new KeyValuePair<string, string>("changeData", changeData));

            Assert.AreEqual(HttpStatusCode.Accepted, webResponse.StatusCode, "Wrong status code when sending a notification");

            return webResponse;
        }

        [Test]
        public void TestSendNotificationSanity()
        {
            string filename = "TestSendNotificationSanity" + SRandom.Next<ulong>().ToString() + ".txt";

            HttpWebClient httpWebClient = new HttpWebClient();

            LoginAsRoot(httpWebClient);

            CreateFile(WebServer, httpWebClient, "/Users/root", filename, "text");

            string objectUrl = "http://localhost:" + WebServer.Port.ToString() + "/Users/root/" + filename;

            string title = Convert.ToBase64String(SRandom.NextBytes(30));
            string messageSummary = Convert.ToBase64String(SRandom.NextBytes(50));
            string changeData = Convert.ToBase64String(SRandom.NextBytes(300));
            string documentType = Convert.ToBase64String(SRandom.NextBytes(35));

            SendNotification(httpWebClient, objectUrl, title, documentType, messageSummary, changeData);

            IFileContainer recipientContainer = SecondFileHandlerFactoryLocator.FileSystemResolver.ResolveFile("/Users/root.user");
            IUserHandler userHander = recipientContainer.CastFileHandler<IUserHandler>();

            List<Dictionary<NotificationColumn, object>> notifications = new List<Dictionary<NotificationColumn, object>>(
                userHander.GetNotifications(null, null, 1, null, null, new List<NotificationColumn>(Enum<NotificationColumn>.Values)));

            Assert.AreEqual(messageSummary, notifications[0][NotificationColumn.messageSummary], "Wrong message summary");
            Assert.AreEqual(changeData, notifications[0][NotificationColumn.changeData], "Wrong change data");
            Assert.AreEqual(title, notifications[0][NotificationColumn.title], "Wrong title");
            Assert.AreEqual(documentType, notifications[0][NotificationColumn.documentType], "Wrong title");
        }

        [Test]
        public void TestSendNotificationRoundTrip()
        {
            string filename = "TestSendNotificationRoundTrip" + SRandom.Next<ulong>().ToString() + ".txt";

            HttpWebClient httpWebClient = new HttpWebClient();

            LoginAsRoot(httpWebClient);

            CreateFile(WebServer, httpWebClient, "/Users/root", filename, "text");

            string objectUrl = "http://localhost:" + WebServer.Port.ToString() + "/Users/root/" + filename;

            string title = Convert.ToBase64String(SRandom.NextBytes(30));
            string messageSummary = Convert.ToBase64String(SRandom.NextBytes(50));
            string changeData = Convert.ToBase64String(SRandom.NextBytes(300));
            string documentType = Convert.ToBase64String(SRandom.NextBytes(35));

            SendNotification(httpWebClient, objectUrl, title, documentType, messageSummary, changeData);

            LoginAsRoot(httpWebClient, SecondWebServer);

            HttpResponseHandler webResponse = httpWebClient.Get("http://localhost:" + SecondWebServer.Port.ToString() + "/Users/root.user",
                new KeyValuePair<string, string>("Method", "GetNotifications"),
                new KeyValuePair<string, string>("maxNotifications", "1"));

            Assert.AreEqual(HttpStatusCode.OK, webResponse.StatusCode);

            JsonReader jsonReader = webResponse.AsJsonReader();
            System.Collections.Generic.Dictionary<string,object>[] notifications = jsonReader.Deserialize<System.Collections.Generic.Dictionary<string,object>[]>();

            Assert.AreEqual(messageSummary, notifications[0][NotificationColumn.messageSummary.ToString()], "Wrong message summary");
            Assert.AreEqual(changeData, notifications[0][NotificationColumn.changeData.ToString()], "Wrong change data");
            Assert.AreEqual(title, notifications[0][NotificationColumn.title.ToString()], "Wrong title");
            Assert.AreEqual(documentType, notifications[0][NotificationColumn.documentType.ToString()], "Wrong title");
        }

        public void SetupAndVerifyInitialNotification<TFileHandler>(string filetype, GenericArgument<TFileHandler> del)
            where TFileHandler : IFileHandler
        {
            IUser secondRootUser = SecondFileHandlerFactoryLocator.UserManagerHandler.GetUser("root");
            IUserHandler secondRootHandler = secondRootUser.UserHandler;

            string filename = "TestNotification" + SRandom.Next<long>().ToString();

            // Determine the highest notification ID
            List<Dictionary<NotificationColumn, object>> existingNotifications = new List<Dictionary<NotificationColumn, object>>(
                secondRootHandler.GetNotifications(
                    null, null, 1, null, null, new List<NotificationColumn>(new NotificationColumn[] { NotificationColumn.notificationId })));

            long highestNotificationId = 0;
            if (existingNotifications.Count > 0)
                highestNotificationId = (long)existingNotifications[0][NotificationColumn.notificationId];

            // Get the user ID on the first server for the second server's root user
            ID<IUserOrGroup, Guid> secondRootThroughOpenIdId = FileHandlerFactoryLocator.UserManagerHandler.GetUserOrGroupOrOpenId(
                "http://" + SecondFileHandlerFactoryLocator.HostnameAndPort + "/Users/root.user").Id;

            // Add / update the permission
            IFileContainer rootDirectoryContainer = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile("/Users/root");
            IDirectoryHandler rootUserDirectory = rootDirectoryContainer.CastFileHandler<IDirectoryHandler>();
            TFileHandler fileHandler = (TFileHandler)rootUserDirectory.CreateFile(filename, filetype, FileHandlerFactoryLocator.UserFactory.RootUser.Id);
            rootUserDirectory.SetPermission(
                FileHandlerFactoryLocator.UserFactory.RootUser.Id, filename, secondRootThroughOpenIdId, FilePermissionEnum.Read, false, true);

            // Ensure that the notification was sent
            IList<Dictionary<NotificationColumn, object>> newNotifications = new List<Dictionary<NotificationColumn, object>>(
                secondRootHandler.GetNotifications(
                    null, highestNotificationId + 1, 1, null, null, new List<NotificationColumn>(Enum<NotificationColumn>.Values)));

            Assert.IsTrue(newNotifications.Count > 0, "Notification not sent");

            Dictionary<NotificationColumn, object> notification = newNotifications[0];

            Assert.AreEqual("http://" + FileHandlerFactoryLocator.HostnameAndPort + "/Users/root/" + filename, notification[NotificationColumn.objectUrl], "Wrong objectUrl sent");

            if (null != del)
            {
                del(fileHandler);

                // Ensure that the second notification was sent
                newNotifications = new List<Dictionary<NotificationColumn, object>>(
                    secondRootHandler.GetNotifications(
                        null, highestNotificationId + 2, 1, null, null, new List<NotificationColumn>(Enum<NotificationColumn>.Values)));

                Assert.IsTrue(newNotifications.Count > 0, "Notification not sent");

                notification = newNotifications[0];

                Assert.AreEqual("http://" + FileHandlerFactoryLocator.HostnameAndPort + "/Users/root/" + filename, notification[NotificationColumn.objectUrl], "Wrong objectUrl sent");
            }
        }

        [Test]
        public void TestNotificationSentWhenPermissionAdded()
        {
            SetupAndVerifyInitialNotification<IFileHandler>("text", null);
        }

        [Test]
        public void VerifyNotificationSentOnTextModification()
        {
            SetupAndVerifyInitialNotification<ITextHandler>("text", delegate(ITextHandler textHandler)
            {
                textHandler.WriteAll(FileHandlerFactoryLocator.UserFactory.RootUser, "gresfdssa");
            });
        }

        [Test]
        public void VerifyNotificationSentOnNVPModification()
        {
            SetupAndVerifyInitialNotification<INameValuePairsHandler>("name-value", delegate(INameValuePairsHandler nameValuePairsHandler)
            {
                nameValuePairsHandler.Set(FileHandlerFactoryLocator.UserFactory.RootUser, "grewgs", "h7u8ro3w");
            });
        }

        [Test]
        public void VerifyNotificationSentOnDirectoryModification()
        {
            SetupAndVerifyInitialNotification<IDirectoryHandler>("directory", delegate(IDirectoryHandler directoryHandler)
            {
                directoryHandler.CreateFile("frafaesrt", "text", null);
            });
        }
    }
}
