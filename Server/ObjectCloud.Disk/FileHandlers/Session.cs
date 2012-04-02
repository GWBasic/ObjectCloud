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
			PersistedObject<SessionData> persistedSessionData)
        {
            this.sessionManagerHandler = sessionManagerHandler;
            this.fileHandlerFactoryLocator = fileHandlerFactoryLocator;
			this.persistedSessionData = persistedSessionData;
        }

        private readonly SessionManagerHandler sessionManagerHandler;
        private readonly FileHandlerFactoryLocator fileHandlerFactoryLocator;
		private readonly PersistedObject<SessionData> persistedSessionData;

        /// <summary>
        /// The session ID
        /// </summary>
        public ID<ISession, Guid> SessionId
        {
            get { return this.persistedSessionData.DirtyObject.sessionId; }
        }

        public IUser User
        {
			get
			{
				return this.fileHandlerFactoryLocator.UserManagerHandler.GetUser(
					this.persistedSessionData.DirtyObject.userId); 
			}
        }

        public void Login(IUser user)
        {
			this.persistedSessionData.Write(session =>
			{
				session.userId = user.Id;
				this.filesTouchedForUrls = new Dictionary<string, WeakReference>();
			});
        }

        public TimeSpan MaxAge
        {
            get { return persistedSessionData.DirtyObject.maxAge; }
            set
            {
				this.persistedSessionData.Write(session => session.maxAge = value);
            }
        }

        public DateTime LastQuery
        {
            get { return persistedSessionData.DirtyObject.lastQuery; }
            set
            {
				this.persistedSessionData.Write(session => session.lastQuery = value);
            }
        }


        public bool KeepAlive
        {
            get { return persistedSessionData.DirtyObject.keepAlive; }
            set
            {
				this.persistedSessionData.Write(session =>
				{
					session.keepAlive = value;

					if (value)
                        session.maxAge = TimeSpan.FromDays(30);
                    else
                        session.maxAge = TimeSpan.FromDays(0.5);
				});
            }
        }

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
