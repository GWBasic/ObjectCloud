// Copyright 2009 - 2012 Andrew Rondeau
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
    internal class Session : ISession
    {
        public Session(
			SessionManagerHandler sessionManagerHandler, 
			FileHandlerFactoryLocator fileHandlerFactoryLocator,
			PersistedBinaryFormatterObject<Dictionary<ID<ISession, Guid>, SessionData>> persistedSessionDatas,
			ID<ISession, Guid> sessionId,
			SessionData sessionData)
        {
            this.sessionManagerHandler = sessionManagerHandler;
            this.fileHandlerFactoryLocator = fileHandlerFactoryLocator;
			this.persistedSessionDatas = persistedSessionDatas;
			this.sessionId = sessionId;

			this.maxAge = sessionData.maxAge;
			this.lastQuery = sessionData.lastQuery;
			this.keepAlive = sessionData.keepAlive;
        }

        private readonly SessionManagerHandler sessionManagerHandler;
        private readonly FileHandlerFactoryLocator fileHandlerFactoryLocator;
		private readonly PersistedBinaryFormatterObject<Dictionary<ID<ISession, Guid>, SessionData>> persistedSessionDatas;

        /// <summary>
        /// The session ID
        /// </summary>
        public ID<ISession, Guid> SessionId
        {
            get { return this.sessionId; }
        }
		private readonly ID<ISession, Guid> sessionId;

        public IUser User
        {
			get
			{
				if (null == this.user)
					this.persistedSessionDatas.Read(sessionDatas => 
					{
						var userId = sessionDatas[this.sessionId].userId;
						this.user = this.fileHandlerFactoryLocator.UserManagerHandler.GetUser(userId);
					});
				
				return this.user;
			}
        }
		private IUser user;

        public void Login(IUser user)
        {
			this.persistedSessionDatas.Write(sessionDatas =>
            {
				var session = sessionDatas[this.sessionId];
				session.userId = user.Id;
				this.filesTouchedForUrls = new Dictionary<string, WeakReference>();
				
				this.user = user;
			});
        }

        public TimeSpan MaxAge
        {
            get 
			{
				return this.maxAge;
			}
            set
            {
				this.persistedSessionDatas.Write(sessionDatas => 
                {
					sessionDatas[this.sessionId].maxAge = value;
					this.maxAge = value;
				});
            }
        }
		private TimeSpan maxAge;

        public DateTime LastQuery
        {
            get 
			{
				return this.lastQuery;
			}
            set
            {
				this.persistedSessionDatas.WriteEventual(sessionDatas =>
                {
					sessionDatas[this.sessionId].lastQuery = value;
					this.lastQuery = value;
				});
            }
        }
        private DateTime lastQuery;

        public bool KeepAlive
        {
            get 
			{
				return this.keepAlive;
			}
            set
            {
				this.persistedSessionDatas.Write(sessionDatas =>
                {
					var session = sessionDatas[this.sessionId];
					
					session.keepAlive = value;

					if (value)
                        session.maxAge = TimeSpan.FromDays(30);
                    else
                        session.maxAge = TimeSpan.FromDays(0.5);
					
					this.keepAlive = value;
					this.maxAge = session.maxAge;
				});
            }
        }
        private bool keepAlive;

        /// <summary>
        /// Cache of all files touched for a given URL
        /// TODO: This implementation is a joke, this needs serious optimization
        /// </summary>
        Dictionary<string, WeakReference> filesTouchedForUrls = new Dictionary<string, WeakReference>();

        public HashSet<IFileContainer> GetFilesTouchedForUrl(string url)
        {
            WeakReference wr = null;
            if (filesTouchedForUrls.TryGetValue(url, out wr))
                return (HashSet<IFileContainer>)wr.Target;

            return null;
        }

        public void SetFilesTouchedForUrl(string url, HashSet<IFileContainer> touchedFiles)
        {
			Dictionary<string, WeakReference> filesTouchedForUrls;			
			Dictionary<string, WeakReference> filesTouchedForUrlsCopy;
			
			do
			{
				filesTouchedForUrls = this.filesTouchedForUrls;
				filesTouchedForUrlsCopy = new Dictionary<string, WeakReference>(filesTouchedForUrls);
                filesTouchedForUrlsCopy[url] = new WeakReference(touchedFiles);
			} while (filesTouchedForUrls != Interlocked.CompareExchange(ref this.filesTouchedForUrls, filesTouchedForUrlsCopy, filesTouchedForUrls));
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
			
			if (RunningCometTransports.Count > sessionManagerHandler.MaxCometTransports)
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
