// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

using ObjectCloud.Common;
using ObjectCloud.DataAccess.SessionManager;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;

using ISession = ObjectCloud.Interfaces.WebServer.ISession;

namespace ObjectCloud.Disk.FileHandlers
{
    public class Session : ISession
    {
        public Session(SessionManagerHandler sessionManagerHandler, FileHandlerFactoryLocator fileHandlerFactoryLocator, ID<ISession, Guid> sessionId)
        {
            SessionManagerHandler = sessionManagerHandler;
            FileHandlerFactoryLocator = fileHandlerFactoryLocator;
            _SessionId = sessionId;
        }

        SessionManagerHandler SessionManagerHandler;
        FileHandlerFactoryLocator FileHandlerFactoryLocator;

        /// <summary>
        /// The session ID
        /// </summary>
        public ID<ISession, Guid> SessionId
        {
            get { return _SessionId; }
        }
        private readonly ID<ISession, Guid> _SessionId;

        public IUser User
        {
            get
            {
                using (TimedLock.Lock(userLock))
                {
                    if (null == _User)
                    {
                        ID<IUserOrGroup, Guid> userId;

                        ISession_Readable session = SessionManagerHandler.DatabaseConnection.Session.SelectSingle(Session_Table.SessionID == SessionId.Value);
                        userId = session.UserID;

                        _User = FileHandlerFactoryLocator.UserManagerHandler.GetUser(userId);
                    }

                    return _User;
                }
            }
            set
            {
                using (TimedLock.Lock(userLock))
                {
                    SessionManagerHandler.DatabaseConnection.Session.Update(Session_Table.SessionID == SessionId.Value,
                        delegate(ISession_Writable session)
                        {
                            session.UserID = value.Id;
                        });

                    _User = value;
                    FilesTouchedForUrls.Clear();
                }
            }
        }
        private IUser _User = null;

        /// <summary>
        /// Provides a lock for syncronizing user access
        /// </summary>
        private object userLock = new object();

        public TimeSpan MaxAge
        {
            get
            {
                ISession_Readable session = SessionManagerHandler.DatabaseConnection.Session.SelectSingle(Session_Table.SessionID == SessionId.Value);
                return session.MaxAge;
            }
            set
            {
                DateTime lastQuery = LastQuery;

                SessionManagerHandler.DatabaseConnection.Session.Update(Session_Table.SessionID == SessionId.Value,
                    delegate(ISession_Writable session)
                    {
                        session.MaxAge = value;
                        session.WhenToDelete = lastQuery + value;
                    });
            }
        }

        public DateTime LastQuery
        {
            get
            {
                ISession_Readable session = SessionManagerHandler.DatabaseConnection.Session.SelectSingle(Session_Table.SessionID == SessionId.Value);
                return session.WhenToDelete - session.MaxAge;
            }
        }


        public bool KeepAlive
        {
            get
            {
                ISession_Readable session = SessionManagerHandler.DatabaseConnection.Session.SelectSingle(Session_Table.SessionID == SessionId.Value);
                return session.KeepAlive;
            }
            set
            {
                SessionManagerHandler.DatabaseConnection.Session.Update(Session_Table.SessionID == SessionId.Value,
                    delegate(ISession_Writable session)
                    {
                        session.KeepAlive = value;

                        if (value)
                            session.MaxAge = TimeSpan.FromDays(30);
                        else
                            session.MaxAge = TimeSpan.FromDays(0.5);
                    });
            }
        }

        /// <summary>
        /// Cache of all files touched for a given URL
        /// </summary>
        Dictionary<string, WeakReference> FilesTouchedForUrls = new Dictionary<string, WeakReference>();

        public Set<IFileContainer> GetFilesTouchedForUrl(string url)
        {
            using (TimedLock.Lock(userLock))
            {
                WeakReference wr = null;
                if (FilesTouchedForUrls.TryGetValue(url, out wr))
                    return (Set<IFileContainer>)wr.Target;
            }

            return null;
        }

        public void SetFilesTouchedForUrl(string url, Set<IFileContainer> touchedFiles)
        {
            using (TimedLock.Lock(userLock))
                FilesTouchedForUrls[url] = new WeakReference(touchedFiles);
        }

        public override int GetHashCode()
        {
            return SessionId.GetHashCode();
        }
    }
}
