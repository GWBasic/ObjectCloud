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

            lock (pulser)
            {
                FileHandlerFactoryLocator.UserManagerHandler.GetRecipientInfos(
                    user,
                    false,
                    new string[] { target.Identity },
                    callback);

                Monitor.Wait(pulser);
            }

            Assert.IsNotNull(recipientInfo);
            Assert.IsNotNull(recipientInfo.SenderToken);
            Assert.AreEqual(
                    string.Format("http://{0}/Users/UserDB?Method=RecieveNotification", SecondFileHandlerFactoryLocator.HostnameAndPort),
                    recipientInfo.RecieveNotificationEndpoint);
        }
    }
}
