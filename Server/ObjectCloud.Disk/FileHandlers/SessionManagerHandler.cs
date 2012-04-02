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
using ObjectCloud.DataAccess.SessionManager;
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
			this.path = path;
            this.sessions = new Dictionary<ID<ISession, Guid>, Session>();
            me = this;
			
			// Load all sessions
			foreach (var filename in Directory.GetFiles(path))
			{
				var sessionString = Path.GetFileName(filename);
				
				//Console.WriteLine("Looking at:\n\tFile: {0}\n\tGuid: {1}", filename, sessionString);
				
				Guid sessionGuid;
				if (GuidFunctions.TryParse(sessionString, out sessionGuid))
				{
					//Console.WriteLine("Loading session: {0}", sessionGuid);
					
					var session = new Session(
						this,
						fileHandlerFactoryLocator,
						new PersistedObject<SessionData>(filename));
					
					//Console.WriteLine("session.LastQuery + session.MaxAge: {0}", session.LastQuery.ToLocalTime() + session.MaxAge);
					
					if (session.LastQuery + session.MaxAge > DateTime.UtcNow)
						sessions[session.SessionId] = session;
					else
						try
						{
							//Console.WriteLine("Session expired: {0}", sessionGuid);
							File.Delete(filename);
						}
						catch (Exception e)
						{
							log.WarnFormat(
								string.Format("Exception deleting session {0}", sessionGuid),
								e);
						}
				}
				else
					// Delete junk left over from accidents, crashes, ect
					try
					{
						//Console.WriteLine("Deleting: {0}", filename);
						File.Delete(filename);
					}
					catch (Exception e)
					{
						log.WarnFormat(
							string.Format("Exception deleting leftover session file {0}", filename),
							e);
					}
			}

            // Clean old sessions every minute
            this.cleanOldSessionsTimer = new Timer(CleanOldSessions, null, 0, 6000000);
        }
		
		/// <summary>
		/// Where all the sessions are stored
		/// </summary>
		private readonly string path;
		
        /// <summary>
        /// This object should never be garbage collected
        /// </summary>
        static SessionManagerHandler me;

        /// <summary>
        /// Cache of sessions
        /// </summary>
        private Dictionary<ID<ISession, Guid>, Session> sessions;

        /// <summary>
        /// Used to clean old sessions
        /// </summary>
        private readonly Timer cleanOldSessionsTimer = null;
		
		/// <summary>
		/// Returns the session path.
		/// </summary>
		private string GetSessionPath(ID<ISession, Guid> sessionId)
		{
			return string.Format(
				"{0}{1}{2}",
				this.path,
				Path.DirectorySeparatorChar,
				sessionId);
		}

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
		
		/// <summary>
		/// Non-blocking way to remove old sessions
		/// </summary>
		/// <param name='sessionIds'>
		/// Session identifiers.
		/// </param>
		private void DeleteSessions(HashSet<ID<ISession, Guid>> sessionIds)
		{
			Dictionary<ID<ISession, Guid>, Session> sessions;
			Dictionary<ID<ISession, Guid>, Session> sessionsCopy;
			
			// Remove the sessions from RAM
			do
			{
				sessions = this.sessions;
				sessionsCopy = new Dictionary<ID<ISession, Guid>, Session>(sessions);
				
				foreach (var sessionId in sessionIds)
					sessionsCopy.Remove(sessionId);
				
			} while (sessions != Interlocked.CompareExchange(ref this.sessions, sessionsCopy, sessions));
			
			foreach (var sessionId in sessionIds)
				try
				{
					File.Delete(this.GetSessionPath(sessionId));
				}
				catch (Exception e)
				{
					log.WarnFormat(
						string.Format("Exception deleting session {0}", sessionId),
						e);
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
				HashSet<ID<ISession, Guid>> sessionIdsToDelete = new HashSet<ID<ISession, Guid>>();
				
				foreach (var session in this.sessions.Values)
					if (session.LastQuery + session.MaxAge < DateTime.UtcNow)
						sessionIdsToDelete.Add(session.SessionId);
				
				this.DeleteSessions(sessionIdsToDelete);
			}
			catch (Exception e)
			{
				log.Error("Exception cleaning out old sessions", e);
			}
        }

        public ISession CreateSession()
        {
            ID<ISession, Guid> sessionId = new ID<ISession, Guid>(Guid.NewGuid());
			var sessionPath = this.GetSessionPath(sessionId);
			
			var sessionData = new SessionData()
			{
				sessionId = sessionId,
				lastQuery = DateTime.UtcNow,
	            maxAge = TimeSpan.FromDays(0.5),
	            userId = FileHandlerFactoryLocator.UserFactory.AnonymousUser.Id,
	            keepAlive = false
			};
			
			var session = new Session(
				this,
				this.FileHandlerFactoryLocator,
				new PersistedObject<SessionData>(sessionPath, sessionData));
			
			Dictionary<ID<ISession, Guid>, Session> sessions;
			Dictionary<ID<ISession, Guid>, Session> sessionsCopy;
			
			do
			{
				sessions = this.sessions;
				sessionsCopy = new Dictionary<ID<ISession, Guid>, Session>(sessions);
				sessionsCopy[sessionId] = session;
			} while (sessions != Interlocked.CompareExchange(ref this.sessions, sessionsCopy, sessions));
			
			return session;
        }

        public override void Dump(string path, ID<IUserOrGroup, Guid> userId)
        {
            throw new NotImplementedException();
        }

        public void EndSession(ID<ISession, Guid> sessionId)
        {
            this.DeleteSessions(new HashSet<ID<ISession, Guid>> {sessionId});
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