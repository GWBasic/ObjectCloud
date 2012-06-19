// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Xml;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;

using ISession = ObjectCloud.Interfaces.Security.ISession;

namespace ObjectCloud.Disk.FileHandlers
{
    public class SessionManagerHandler : FileHandler, ISessionManagerHandler, IDisposable
    {
		private static ILog log = LogManager.GetLogger<SessionManagerHandler>();
		
        public SessionManagerHandler(FileHandlerFactoryLocator fileHandlerFactoryLocator, string path)
            : base(fileHandlerFactoryLocator)
        {
            this.sessions = new Dictionary<ID<ISession, Guid>, Session>();
			this.persistedSessionDatas = new PersistedBinaryFormatterObject<Dictionary<ID<ISession, Guid>, SessionData>>(path);
            me = this;
			
			this.persistedSessionDatas.Read(sessionDatas =>
			{
				foreach (var sessionDataKVP in sessionDatas)
				{
					var sessionId = sessionDataKVP.Key;
					var sessionData = sessionDataKVP.Value;
					
					this.sessions[sessionId] = new Session(
						this,
						fileHandlerFactoryLocator,
						this.persistedSessionDatas,
						sessionId,
						sessionData);
				}		
			});

            // Clean old sessions every hour or so
            this.cleanOldSessionsTimer = new Timer(CleanOldSessions, null, 0, 6000000);
        }

        /// <summary>
        /// This object should never be garbage collected
        /// </summary>
        static SessionManagerHandler me;

        /// <summary>
        /// Cache of sessions
        /// </summary>
        private Dictionary<ID<ISession, Guid>, Session> sessions;
		
		/// <summary>
		/// All of the data for the persisted sessions
		/// </summary>
		private PersistedBinaryFormatterObject<Dictionary<ID<ISession, Guid>, SessionData>> persistedSessionDatas;

        /// <summary>
        /// Used to clean old sessions
        /// </summary>
        private readonly Timer cleanOldSessionsTimer = null;

        public ISession this[ID<ISession, Guid> sessionId]
        {
            get
            {
				Session session;
				if (!this.sessions.TryGetValue(sessionId, out session))
					return null;
				
				if (session.LastQuery + session.MaxAge < DateTime.UtcNow)
				{
					ThreadPool.QueueUserWorkItem(state =>
					{
						try
						{
							this.EndSession(sessionId);
						}
						catch (Exception e)
						{
							log.Error("Exception cleaning old session", e);
						}
					});
					
					return null;
				}
				
				session.LastQuery = DateTime.UtcNow;
				
				return session;
            }
        }
		
        public void CleanOldSessions()
        {
            CleanOldSessions(null);
        }

        private void CleanOldSessions(object state)
        {
			try
			{
				this.persistedSessionDatas.Write(sessionDatas =>
	            {
					Dictionary<ID<ISession, Guid>, Session> sessions = new Dictionary<ID<ISession, Guid>, Session>(this.sessions);
					
					foreach (var sessionDataKVP in sessionDatas)
					{
						var sessionData = sessionDataKVP.Value;
						
						if (sessionData.lastQuery + sessionData.maxAge < DateTime.UtcNow)
						{
							var sessionId = sessionDataKVP.Key;
							sessionDatas.Remove(sessionId);
							sessions.Remove(sessionId);
						}
					}
					
					this.sessions = sessions;
				});
			}
			catch (Exception e)
			{
				log.Error("Exception cleaning out old sessions", e);
			}
        }

        public ISession CreateSession()
        {
            ID<ISession, Guid> sessionId = new ID<ISession, Guid>(Guid.NewGuid());

			return this.persistedSessionDatas.Write<ISession>(sessionDatas =>
            {
				var sessionData = new SessionData()
				{
					lastQuery = DateTime.UtcNow,
		            maxAge = TimeSpan.FromDays(0.5),
		            userId = FileHandlerFactoryLocator.UserFactory.AnonymousUser.Id,
		            keepAlive = false
				};
				
				sessionDatas[sessionId] = sessionData;
				
				var session = new Session(
					this,
					this.FileHandlerFactoryLocator,
					this.persistedSessionDatas,
					sessionId,
					sessionData);
				
				var sessions = new Dictionary<ID<ISession, Guid>, Session>(this.sessions);
				sessions[sessionId] = session;
				this.sessions = sessions;
				
				return session;
			});
        }

        public override void Dump(string path, ID<IUserOrGroup, Guid> userId)
        {
            throw new NotImplementedException();
        }

        public void EndSession(ID<ISession, Guid> sessionId)
        {
			this.persistedSessionDatas.Write(sessionDatas =>
            {
				Dictionary<ID<ISession, Guid>, Session> sessions = new Dictionary<ID<ISession, Guid>, Session>(this.sessions);

				sessionDatas.Remove(sessionId);
				sessions.Remove(sessionId);
				
				this.sessions = sessions;
			});
        }

        /// <summary>
        /// When this object is disposed, it removes the static reference so it can be GCed
        /// </summary>
        public override void Dispose()
        {
            if (this == me)
                me = null;
			
			this.cleanOldSessionsTimer.Dispose();

            base.Dispose();
        }
        
        public int MaxCometTransports
		{
        	get { return _MaxCometTransports; }
			set { _MaxCometTransports = value; }
        }
        int _MaxCometTransports = 3;
    }
}