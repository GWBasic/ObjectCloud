// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Threading;

using ObjectCloud.Common;
using ObjectCloud.Common.Threading;
using ObjectCloud.DataAccess.SessionManager;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

using ISession = ObjectCloud.Interfaces.Security.ISession;

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
        }
        private IUser _User = null;

        public void Login(IUser user)
        {
            using (TimedLock.Lock(userLock))
            {
                SessionManagerHandler.DatabaseConnection.Session.Update(Session_Table.SessionID == SessionId.Value,
                    delegate(ISession_Writable session)
                    {
                        session.UserID = user.Id;
                    });

                _User = user;
                FilesTouchedForUrls.Clear();
            }
        }

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

        public HashSet<IFileContainer> GetFilesTouchedForUrl(string url)
        {
            using (TimedLock.Lock(userLock))
            {
                WeakReference wr = null;
                if (FilesTouchedForUrls.TryGetValue(url, out wr))
                    return (HashSet<IFileContainer>)wr.Target;
            }

            return null;
        }

        public void SetFilesTouchedForUrl(string url, HashSet<IFileContainer> touchedFiles)
        {
            using (TimedLock.Lock(userLock))
                FilesTouchedForUrls[url] = new WeakReference(touchedFiles);
        }

        public override int GetHashCode()
        {
            return SessionId.GetHashCode();
        }

		/// <summary>
		/// All of the running comet transports 
		/// </summary>
		private LockFreeQueue_WithCount<WeakReference> RunningCometTransports = new LockFreeQueue_WithCount<WeakReference>();
		
		public void RegisterCometTransport (ICometTransport cometTransport)
        {
			RunningCometTransports.Enqueue(new WeakReference(cometTransport));
			
			if (RunningCometTransports.Count > SessionManagerHandler.MaxCometTransports)
			{
				WeakReference wr;
				if (RunningCometTransports.Dequeue(out wr))
				{
					ICometTransport toDispose = (ICometTransport)wr.Target;
					
					if (null != toDispose)
					{
						toDispose.Dispose();
						toDispose.GetDataToSend();
					}
				}
			}
        }

        public HttpWebClient HttpWebClient
        {
            get
            {
                if (null == _HttpWebClient)
                    Interlocked.CompareExchange<HttpWebClient>(ref _HttpWebClient, new HttpWebClient(), null);

                return _HttpWebClient; 
            }
        }
        private HttpWebClient _HttpWebClient = null;
    }
}
