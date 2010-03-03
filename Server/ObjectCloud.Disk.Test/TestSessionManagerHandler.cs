// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Threading;

using NUnit.Framework;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;
using ObjectCloud.Disk.Implementation;

namespace ObjectCloud.Disk.Test
{
    [TestFixture]
    public class TestSessionManagerHandler : TestBase
    {
        public ISessionManagerHandler SessionManagerHandler
        {
            get
            {
                if (null == _SessionManagerHandler)
                    _SessionManagerHandler = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile("/System/SessionManager").CastFileHandler<ISessionManagerHandler>();

                return _SessionManagerHandler; 
            }
        }
        private ISessionManagerHandler _SessionManagerHandler = null;

        [Test]
        public void TestSessionManagerPresent()
        {
            Assert.IsNotNull(SessionManagerHandler);
        }

        [Test]
        public void TestLastQueryUpdated()
        {
            ISession session = SessionManagerHandler.CreateSession();

            Assert.Less(DateTime.UtcNow - session.LastQuery, TimeSpan.FromSeconds(0.1), "Error in default LastQuery");
            Assert.Greater(DateTime.UtcNow - session.LastQuery, TimeSpan.Zero, "Error in default LastQuery");

            Thread.Sleep(25);

            session = SessionManagerHandler[session.SessionId];

            Assert.Less(DateTime.UtcNow - session.LastQuery, TimeSpan.FromSeconds(0.1), "Error in updated LastQuery");
            Assert.Greater(DateTime.UtcNow - session.LastQuery, TimeSpan.Zero, "Error in updated LastQuery");
        }

        [Test]
        public void TestMaxAge()
        {
            ISession session = SessionManagerHandler.CreateSession();

            session.MaxAge = TimeSpan.FromTicks(3456);

            Assert.AreEqual(TimeSpan.FromTicks(3456), session.MaxAge, "MaxAge not persisted correctly");
        }

        [Test]
        public void TestSessionsDeleted()
        {
            ISession session = SessionManagerHandler.CreateSession();

            session.MaxAge = TimeSpan.FromTicks(1);

            Thread.Sleep(TimeSpan.FromSeconds(2));

            session = SessionManagerHandler[session.SessionId];

            Assert.IsNull(session, "Session not deleted");
        }
    }
}
