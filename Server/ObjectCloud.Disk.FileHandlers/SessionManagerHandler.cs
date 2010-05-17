// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Xml;

using ObjectCloud.Common;
using ObjectCloud.DataAccess.SessionManager;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;

using ISession = ObjectCloud.Interfaces.Security.ISession;

namespace ObjectCloud.Disk.FileHandlers
{
    public class SessionManagerHandler : HasDatabaseFileHandler<IDatabaseConnector, IDatabaseConnection, IDatabaseTransaction>, ISessionManagerHandler, IDisposable
    {
        public SessionManagerHandler(IDatabaseConnector databaseConnector, FileHandlerFactoryLocator fileHandlerFactoryLocator)
            : base(databaseConnector, fileHandlerFactoryLocator)
        {
            SessionCache = new Cache<ID<ISession, Guid>, ISession>(CreateForCache);
            me = this;

            // Clean old sessions every minute
			if (null == CleanOldSessionsTimer) // (The if statement is to disable a silly warning when compiling on Mono)
	            CleanOldSessionsTimer = new Timer(CleanOldSessions, null, 0, 6000000);
        }

        /// <summary>
        /// This object should never be garbage collected
        /// </summary>
        static SessionManagerHandler me;

        /// <summary>
        /// Cache of sessions
        /// </summary>
        Cache<ID<ISession, Guid>, ISession> SessionCache;

        /// <summary>
        /// Used to clean old sessions
        /// </summary>
        Timer CleanOldSessionsTimer = null;

        public ISession this[ID<ISession, Guid> sessionId]
        {
            get
            {
                ISession_Readable session = DatabaseConnection.Session.SelectSingle(Session_Table.SessionID == sessionId);

                if (null == session)
                    return null;

                // If session is expired
                if (session.WhenToDelete.Ticks <= DateTime.UtcNow.Ticks)
                    return null;

                DatabaseConnection.Session.Update(Session_Table.SessionID == sessionId,
                    delegate(ISession_Writable sessionW)
                    {
                        sessionW.WhenToDelete = DateTime.UtcNow + session.MaxAge;
                    });

                return SessionCache[sessionId];
            }
        }

        public void CleanOldSessions()
        {
            CleanOldSessions(null);
        }

        private void CleanOldSessions(object state)
        {
            DatabaseConnection.Session.Delete(Session_Table.WhenToDelete <= DateTime.UtcNow);
        }

        public ISession CreateSession()
        {
            ID<ISession, Guid> sessionId = new ID<ISession, Guid>(Guid.NewGuid());

            DatabaseConnection.Session.Insert(delegate(ISession_Writable session)
            {
                session.MaxAge = TimeSpan.FromDays(0.5);
                session.SessionID = sessionId;
                session.UserID = FileHandlerFactoryLocator.UserFactory.AnonymousUser.Id;
                session.KeepAlive = false;
                session.WhenToDelete = DateTime.UtcNow + TimeSpan.FromDays(0.5);
            });

            return SessionCache[sessionId];
        }

        public override void Dump(string path, ID<IUserOrGroup, Guid> userId)
        {
            throw new NotImplementedException();
        }

        private ISession CreateForCache(ID<ISession, Guid> sessionId)
        {
            return new Session(this, FileHandlerFactoryLocator, sessionId);
        }

        public void EndSession(ID<ISession, Guid> sessionId)
        {
            DatabaseConnection.Session.Delete(Session_Table.SessionID == sessionId);
        }

        /// <summary>
        /// When this object is disposed, it removes the static reference so it can be GCed
        /// </summary>
        public override void Dispose()
        {
            if (this == me)
                me = null;

            base.Dispose();
        }
    }
}